using System;
using System.Collections.Generic;
using Crestron.SimplSharp;
using Newtonsoft.Json.Linq;

namespace ACS_4Series_Template_V3.Diagnostics
{
    /// <summary>
    /// The panel-side twin of <see cref="RamMonitor"/>: logs what each HTML panel's web app
    /// reports about itself, and asks a panel to reload its page.
    ///
    /// Why (2026-09-29): camera video on TP-1 died and a panel REBOOT fixed it, twice. A reboot
    /// restarts the HTML web app too, so it could never say whether the fault is the panel's
    /// native video stack or the HTML project. And the panel's own `uptime` is the OS — loading
    /// a new HTML project restarts the page without rebooting the OS — so nobody knew how long
    /// the web app had actually been running when it broke.
    ///
    ///   [PANELMEM] lines: page uptime, JS heap (with drift since the page loaded), DOM node
    ///   count, ch5-button count. Hourly, plus one at every page load. Read them like the
    ///   [RAM] lines:
    ///       heap climbs, dom flat   -> detached nodes / closures leaking (the observedButtons
    ///                                  Set was one — every ch5-button ever created, retained)
    ///       dom climbs              -> something keeps appending to the page
    ///       pageUp resets           -> the web app restarted (HTML load, reload, or reboot)
    ///
    ///   `reloadpanel N`: reload TP-N's web page WITHOUT rebooting the panel. When video next
    ///   dies, try this first. Fixed by a reload = the fault is in the HTML project, and the
    ///   'before-reload' sample shows its state. Only fixed by a reboot = the fault is native.
    ///
    /// Joins: 1558 serial HTML→C# (report), 1559 serial C#→HTML (command, seq-guarded).
    /// Logged at Warn for the same reason as RamMonitor: the trace must still be there when
    /// someone comes back to it days later.
    /// </summary>
    public class PanelHealth
    {
        public const ushort ReportJoin = 1558;   // serial HTML→C#
        public const ushort CommandJoin = 1559;  // serial C#→HTML
        public const ushort TraceJoin = 1571;    // serial HTML→C# (tapTrace.js)

        private const string Tag = "[PANELMEM]";

        private readonly ControlSystem _cs;
        private readonly object _lock = new object();
        private int _seq;

        private class PanelBaseline
        {
            public double Heap = -1;   // MB at page load, for drift
            public long LastUp = -1;   // seconds, to spot a page restart
            public int Samples;
        }

        private readonly Dictionary<ushort, PanelBaseline> _baselines = new Dictionary<ushort, PanelBaseline>();

        public PanelHealth(ControlSystem cs)
        {
            _cs = cs;
            // Seed from the clock so a program restart can never re-send a seq the panel has
            // already obeyed (the same trap App03's popup seq fell into).
            _seq = (int)(DateTime.Now.Ticks / TimeSpan.TicksPerSecond % 1000000);
        }

        /// <summary>Entry point from TouchpanelUI.SigChange for serial 1558.</summary>
        public void LogReport(ushort tpNumber, string json)
        {
            if (string.IsNullOrEmpty(json)) { return; }
            try
            {
                JObject o = JObject.Parse(json);
                string reason = (string)o["reason"] ?? "?";
                long up = (long?)o["up"] ?? -1;
                double? heap = (double?)o["heap"];
                double? heapLimit = (double?)o["heapLimit"];
                int dom = (int?)o["dom"] ?? -1;
                int btns = (int?)o["btns"] ?? -1;

                string drift = "";
                int sample;
                lock (_lock)
                {
                    PanelBaseline b;
                    if (!_baselines.TryGetValue(tpNumber, out b) || b == null)
                    {
                        b = new PanelBaseline();
                        _baselines[tpNumber] = b;
                    }

                    // Uptime going backwards means the page restarted: new baseline, and say so,
                    // because "when did the web app last restart" is half the question.
                    if (b.LastUp >= 0 && up >= 0 && up < b.LastUp)
                    {
                        CrestronConsole.PrintLine("{0} TP-{1} web page RESTARTED (uptime {2} -> {3})",
                            Tag, tpNumber, FormatUp(b.LastUp), FormatUp(up));
                        b.Heap = -1;
                    }
                    b.LastUp = up;
                    b.Samples++;
                    sample = b.Samples;

                    if (heap.HasValue)
                    {
                        if (b.Heap < 0) { b.Heap = heap.Value; }
                        else { drift = string.Format(" (drift {0:+0.0;-0.0}MB)", heap.Value - b.Heap); }
                    }
                }

                string line = string.Format(
                    "{0} TP-{1} #{2} {3} pageUp={4} heap={5}{6} limit={7} dom={8} ch5-buttons={9}",
                    Tag, tpNumber, sample, reason, FormatUp(up),
                    heap.HasValue ? heap.Value.ToString("0.0") + "MB" : "n/a", drift,
                    heapLimit.HasValue ? heapLimit.Value.ToString("0") + "MB" : "n/a",
                    dom, btns);

                CrestronConsole.PrintLine(line);
                ErrorLog.Warn(line);
            }
            catch (Exception ex)
            {
                ErrorLog.Error("{0} bad report from TP-{1}: {2} ({3})", Tag, tpNumber, ex.Message, json);
            }
        }

        /// <summary>
        /// Entry point from TouchpanelUI.SigChange for serial 1571 (tapTrace.js, enabled with
        /// `tracetaps N on`). Why it exists: buttons on TST-1080/TSW-1070 often need two presses,
        /// and the processor's "Boolean Press Event" line only shows the presses that arrived.
        /// A [TAP] line is the panel's side of the same press, so for a dead tap:
        ///   click=N                      -> the panel never turned the touch into a press
        ///   click=Y, no Press Event line -> sent but lost between panel and processor
        ///   Press Event line, no flip    -> processor / feedback side
        /// [STALL] lines are main-thread freezes on the panel, with how many joins the processor
        /// sent during them. Taps print to console only (chatty). Dead taps and stalls also go to
        /// the error log, so a trace left running overnight can still be read in the morning.
        /// </summary>
        public void LogTrace(ushort tpNumber, string json)
        {
            if (string.IsNullOrEmpty(json)) { return; }
            try
            {
                JObject o = JObject.Parse(json);
                string kind = (string)o["k"] ?? "?";
                string line;
                bool notable;

                if (kind == "tap")
                {
                    bool click = ((int?)o["click"] ?? 0) == 1;
                    bool cancel = ((int?)o["cancel"] ?? 0) == 1;
                    bool reinserted = ((int?)o["re"] ?? 0) == 1;
                    string why = click ? "" :
                        cancel ? "  NO CLICK: browser cancelled (scroll/gesture)" :
                        reinserted ? "  NO CLICK: button pulled from DOM mid-press (buttonPressedReset)" :
                        "  NO CLICK";
                    line = string.Format(
                        "[TAP] TP-{0} {1} click={2} down={3}ms moved={4}px inputLag={5} clickDelay={6} rx2s={7}{8}",
                        tpNumber, (string)o["el"] ?? "?", click ? "Y" : "N",
                        (int?)o["dur"] ?? -1, (int?)o["mv"] ?? -1,
                        Ms(o["lagIn"]), Ms(o["clickMs"]), (int?)o["rx2s"] ?? -1, why);
                    notable = !click;
                }
                else if (kind == "stall")
                {
                    line = string.Format(
                        "[STALL] TP-{0} page froze: worst {1}ms, {2}ms blocked over {3}ms, {4} joins received (top: {5})",
                        tpNumber, (int?)o["worst"] ?? -1, (int?)o["blocked"] ?? -1, (int?)o["dur"] ?? -1,
                        (int?)o["rx"] ?? -1, (string)o["top"] ?? "");
                    notable = true;
                }
                else if (kind == "trace")
                {
                    line = string.Format("[TAP] TP-{0} tracing {1}", tpNumber, ((int?)o["on"] ?? 0) == 1 ? "ON" : "OFF");
                    notable = false;
                }
                else
                {
                    line = string.Format("[TAP] TP-{0} {1}", tpNumber, json);
                    notable = false;
                }

                CrestronConsole.PrintLine(line);
                if (notable) { ErrorLog.Warn(line); }
            }
            catch (Exception ex)
            {
                ErrorLog.Error("[TAP] bad trace from TP-{0}: {1} ({2})", tpNumber, ex.Message, json);
            }
        }

        private static string Ms(JToken t)
        {
            int? v = (int?)t;
            return v.HasValue ? v.Value + "ms" : "n/a";
        }

        /// <summary>Ask a panel's web app to reload itself (cmd "reload") or report now ("report").
        /// tpNumber 0 = every online HTML panel.</summary>
        public void SendCommand(ushort tpNumber, string cmd)
        {
            int sent = 0;
            foreach (var kv in _cs.manager.touchpanelZ)
            {
                if (tpNumber != 0 && kv.Key != tpNumber) { continue; }
                var tp = kv.Value;
                if (tp == null || !tp.HTML_UI || tp.UserInterface == null) { continue; }
                if (!tp.UserInterface.IsOnline)
                {
                    CrestronConsole.PrintLine("{0} TP-{1} offline - '{2}' not sent", Tag, kv.Key, cmd);
                    continue;
                }

                int seq;
                lock (_lock) { seq = ++_seq; }
                tp.UserInterface.StringInput[CommandJoin].StringValue =
                    "{\"seq\":" + seq + ",\"cmd\":\"" + cmd + "\"}";
                sent++;
                CrestronConsole.PrintLine("{0} TP-{1} '{2}' sent", Tag, kv.Key, cmd);
            }
            if (sent == 0)
            {
                CrestronConsole.PrintLine("{0} '{1}': no online HTML panel matched TP-{2}", Tag, cmd, tpNumber);
            }
        }

        private static string FormatUp(long seconds)
        {
            if (seconds < 0) { return "?"; }
            TimeSpan t = TimeSpan.FromSeconds(seconds);
            return t.Days > 0
                ? string.Format("{0}d{1:00}h{2:00}m", t.Days, t.Hours, t.Minutes)
                : string.Format("{0}h{1:00}m", t.Hours, t.Minutes);
        }
    }
}
