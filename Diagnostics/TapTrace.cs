using System;
using System.Collections.Generic;
using System.Text;
using Crestron.SimplSharp;
using Crestron.SimplSharp.CrestronIO;
using Newtonsoft.Json.Linq;

namespace ACS_4Series_Template_V3.Diagnostics
{
    /// <summary>
    /// The processor end of html/app/project/libraries/tapTrace.js: logs every tap an HTML panel
    /// reports, and checks each one against the press that actually arrived here.
    ///
    /// Why (2026-10-05): on the TST-1080s and TSW-1070s a button often needs two or more presses.
    /// The panels are at a client's house and can't be watched, so this has to run unattended for
    /// a day or more and leave a record that answers, for every dead tap, which side dropped it:
    ///
    ///   [TAP] ... click=N              the panel never turned the touch into a press
    ///   [TAP] ... then LOST            the panel sent it; the press never arrived here
    ///   [TAP] ... then [PRESS]         it arrived — so a missing page flip is on this side
    ///   MISSING n=a..b                 trace reports themselves never arrived: the link dropped
    ///                                  panel→processor traffic, presses included
    ///   [REPLAY] ... MISSED LIVE       from `tapdump`: in the panel's own buffer, never received
    ///                                  live. The proof for anything the link swallowed whole.
    ///   [STALL]                        the panel's page froze; rx = joins we sent it meanwhile
    ///   [WAKE] / [VIS]                 the panel's page woke after its timers were frozen (screen
    ///                                  asleep), or was hidden/shown. wake=Nms on a tap = that long
    ///                                  after waking; wake=0 = the touch that woke it
    ///   [ONLINE]                       the processor's own view of the panel connecting/dropping,
    ///                                  for every HTML panel, traced or not
    ///   [MARK]                         a note typed on the console with tapmark, for on-site tests
    ///   touch=a/b/c                    raw input since the panel's previous report: touchstarts /
    ///                                  pointerdowns / pointerdowns on nothing pressable. touches
    ///                                  well above pointerdowns = touches never reached the page
    ///
    /// Everything goes to a daily file, User/taptrace/taptrace-yyyyMMdd.log (pull it with SFTP or
    /// Toolbox's file manager, or `taplog` on the console), so normal taps are kept as well as
    /// failures — a failure only means something next to the taps around it. The [PRESS] lines
    /// for traced panels are written into the same file so both sides read in one place.
    /// Failures also go to the error log. Files older than RetainDays are deleted.
    /// </summary>
    public class TapTrace
    {
        private const int LostCheckMs = 3000;     // how long a sent press has to show up
        private const int PressWindowSec = 4;     // press may arrive just before the tap report
        private const int RetainDays = 14;
        private const int SeenMax = 2000;         // per panel, for matching replays to live

        private readonly object _lock = new object();
        private readonly string _dir;
        private string _currentDay = "";

        private readonly Dictionary<ulong, DateTime> _lastPress = new Dictionary<ulong, DateTime>();
        private readonly HashSet<ushort> _tracing = new HashSet<ushort>();
        private readonly HashSet<CTimer> _pendingChecks = new HashSet<CTimer>();
        private readonly Dictionary<ushort, long> _lastN = new Dictionary<ushort, long>();
        private readonly Dictionary<ushort, HashSet<string>> _seen = new Dictionary<ushort, HashSet<string>>();
        private readonly Dictionary<ushort, Queue<string>> _seenOrder = new Dictionary<ushort, Queue<string>>();

        public TapTrace()
        {
            _dir = string.Format(@"{0}/User/taptrace", Directory.GetApplicationRootDirectory());
        }

        public string LogDirectory { get { return _dir; } }

        /// <summary>Every rising digital edge from an HTML panel, from TouchpanelUI.SigChange.
        /// A dictionary write; costs nothing for panels that are not being traced.</summary>
        public void NotePress(ushort tpNumber, uint join)
        {
            bool tracing;
            lock (_lock)
            {
                _lastPress[Key(tpNumber, join)] = DateTime.Now;
                tracing = _tracing.Contains(tpNumber);
            }
            if (tracing) { Write(string.Format("[PRESS] TP-{0} join {1} arrived", tpNumber, join), false); }
        }

        /// <summary>The processor's view of an HTML panel connecting or dropping, from
        /// TouchpanelUI.ConnectionStatusChange. Logged for every HTML panel: the panel's own
        /// link reports only arrive after it reconnects, so these are the real times.</summary>
        public void NoteOnline(ushort tpNumber, string name, bool online)
        {
            Write(string.Format("[ONLINE] TP-{0} {1} {2} (processor's view)", tpNumber, name,
                online ? "ONLINE" : "OFFLINE"), false);
        }

        /// <summary>`tapmark <text>`: a note in the log, so on-site tests can be found later.</summary>
        public void Mark(string text)
        {
            Write("[MARK] ---------- " + text + " ----------", false);
        }

        /// <summary>Entry point from TouchpanelUI.SigChange for serial 1571.</summary>
        public void Handle(ushort tpNumber, string json)
        {
            if (string.IsNullOrEmpty(json)) { return; }
            JObject o;
            try { o = JObject.Parse(json); }
            catch (Exception ex)
            {
                ErrorLog.Error("[TAP] bad trace from TP-{0}: {1} ({2})", tpNumber, ex.Message, json);
                return;
            }

            Expand(o);
            string kind = (string)o["k"] ?? "?";
            bool replay = ((int?)o["rp"] ?? 0) == 1;
            long n = (long?)o["n"] ?? -1;
            long ts = (long?)o["ts"] ?? -1;

            if (kind == "dump")
            {
                Write(((int?)o["done"] ?? 0) == 1
                    ? string.Format("[REPLAY] TP-{0} dump finished", tpNumber)
                    : string.Format("[REPLAY] TP-{0} {1}: {2} buffered entries", tpNumber,
                        ((int?)o["auto"] ?? 0) == 1 ? "link back up, replaying the outage" : "dump starting",
                        (int?)o["count"] ?? -1), false);
                return;
            }

            if (replay)
            {
                bool missedLive;
                lock (_lock) { missedLive = !SeenContains(tpNumber, n, ts); }
                string line = string.Format("[REPLAY] TP-{0} panelTime={1} {2}{3}",
                    tpNumber, PanelTime(ts), Format(tpNumber, kind, o),
                    missedLive ? "  MISSED LIVE: never reached the processor when it happened" : "");
                Write(line, missedLive && kind != "trace");
                return;
            }

            CheckSequence(tpNumber, n, ts);

            if (kind == "trace")
            {
                bool on = ((int?)o["on"] ?? 0) == 1;
                lock (_lock) { if (on) { _tracing.Add(tpNumber); } else { _tracing.Remove(tpNumber); } }
            }
            else if (kind == "tap")
            {
                lock (_lock) { _tracing.Add(tpNumber); }   // a tap means it's on, whatever we missed
            }

            bool click = ((int?)o["click"] ?? 0) == 1;
            Write(Format(tpNumber, kind, o), (kind == "tap" && !click) || kind == "stall"
                || (kind == "link" && ((int?)o["on"] ?? 1) == 0));

            // Every tap with a join is checked, not just clicked ones: buttons that send on
            // touch-down (the bottom bar, 2026-10-08) work with no click at all.
            uint? j = (uint?)o["j"];
            if (kind == "tap" && j.HasValue)
            {
                ScheduleLostCheck(tpNumber, j.Value, (string)o["el"] ?? "?", ((int?)o["ol"] ?? 1) == 0, click);
            }
        }

        /// <summary>Print the last `lines` lines of today's file to the console.</summary>
        public void PrintTail(int lines)
        {
            string path = PathFor(DateTime.Now);
            try
            {
                if (!File.Exists(path))
                {
                    CrestronConsole.PrintLine("[TAP] no log for today yet ({0})", path);
                    return;
                }
                string all;
                using (var r = new StreamReader(path)) { all = r.ReadToEnd(); }
                string[] split = all.Split('\n');
                int start = Math.Max(0, split.Length - lines - 1);
                for (int i = start; i < split.Length; i++)
                {
                    if (split[i].Length > 0) { CrestronConsole.PrintLine(split[i].TrimEnd('\r')); }
                }
                CrestronConsole.PrintLine("[TAP] ({0})", path);
            }
            catch (Exception ex)
            {
                CrestronConsole.PrintLine("[TAP] can't read {0}: {1}", path, ex.Message);
            }
        }

        // ─── checks ────────────────────────────────────────────────────────

        // Each live report carries n = 1, 2, 3... from page load. A jump means reports never
        // arrived, which means the link was dropping panel→processor traffic — presses too.
        private void CheckSequence(ushort tp, long n, long ts)
        {
            if (n < 0) { return; }
            string gap = null;
            lock (_lock)
            {
                long last;
                if (_lastN.TryGetValue(tp, out last) && n > last + 1)
                {
                    gap = n == last + 2
                        ? string.Format("n={0}", last + 1)
                        : string.Format("n={0}..{1}", last + 1, n - 1);
                    gap = string.Format("[TAP] TP-{0} MISSING {1}: {2} trace report(s) never arrived - link dropped panel->processor traffic. `tapdump {0}` to recover them",
                        tp, gap, n - last - 1);
                }
                else if (_lastN.TryGetValue(tp, out last) && n <= last)
                {
                    gap = string.Format("[TAP] TP-{0} page restarted (trace seq {1} -> {2})", tp, last, n);
                }
                _lastN[tp] = n;
                SeenAdd(tp, n, ts);
            }
            if (gap != null) { Write(gap, gap.Contains("MISSING")); }
        }

        // Tap reports go out with short keys to stay under the panel's ~120-char serial limit
        // (see tapTrace.js SIZE LIMIT). Put the long names back, with the omitted defaults.
        private static readonly string[][] ShortKeys =
        {
            new[] { "e", "el" }, new[] { "d", "dur" }, new[] { "m", "mv" }, new[] { "c", "click" },
            new[] { "x", "cancel" }, new[] { "r", "re" }, new[] { "l", "lagIn" }, new[] { "cm", "clickMs" },
            new[] { "rx", "rx2s" }, new[] { "o", "ol" }, new[] { "w", "wk" }, new[] { "tc", "tch" },
            new[] { "px", "pdx" }
        };

        private static void Expand(JObject o)
        {
            foreach (var pair in ShortKeys)
            {
                JToken v = o[pair[0]];
                if (v != null && o[pair[1]] == null) { o.Remove(pair[0]); o[pair[1]] = v; }
            }
            if ((string)o["k"] == "t")
            {
                o["k"] = "tap";
                if (o["mv"] == null) { o["mv"] = 0; }
                if (o["cancel"] == null) { o["cancel"] = 0; }
                if (o["re"] == null) { o["re"] = 0; }
                if (o["rx2s"] == null) { o["rx2s"] = 0; }
                if (o["ol"] == null) { o["ol"] = 1; }
            }
        }

        private void ScheduleLostCheck(ushort tp, uint join, string el, bool panelOffline, bool click)
        {
            DateTime tapAt = DateTime.Now;
            CTimer t = null;
            // Held until it fires: an unreferenced CTimer can be collected before its callback runs.
            t = new CTimer(_ =>
            {
                try
                {
                    DateTime pressAt;
                    bool arrived;
                    lock (_lock)
                    {
                        arrived = _lastPress.TryGetValue(Key(tp, join), out pressAt)
                            && pressAt >= tapAt.AddSeconds(-PressWindowSec);
                    }
                    if (!arrived && click)
                    {
                        Write(string.Format("[TAP] TP-{0} LOST: {1} was pressed on the panel (click=Y), join {2} never arrived here{3}",
                            tp, el, join, panelOffline ? " - panel reported its link DOWN at the time" : ""), true);
                    }
                    else if (!arrived)
                    {
                        Write(string.Format("[TAP] TP-{0} DEAD TAP: {1} - no click and join {2} never arrived. Nothing was sent.",
                            tp, el, join), true);
                    }
                    else if (!click)
                    {
                        Write(string.Format("[TAP] TP-{0} {1}: join {2} arrived without a click (sent on touch-down)",
                            tp, el, join), false);
                    }
                }
                finally
                {
                    lock (_lock) { _pendingChecks.Remove(t); }
                    if (t != null) { t.Dispose(); }
                }
            }, LostCheckMs);
            lock (_lock) { _pendingChecks.Add(t); }
        }

        // ─── formatting ────────────────────────────────────────────────────

        private static string Format(ushort tp, string kind, JObject o)
        {
            string line = FormatBody(tp, kind, o);
            int? tch = (int?)o["tch"], pd = (int?)o["pd"], pdx = (int?)o["pdx"];
            if (tch.HasValue || pd.HasValue)
            {
                line += string.Format(" touch={0}/{1}/{2}", tch ?? 0, pd ?? 0, pdx ?? 0);
            }
            return line;
        }

        private static string FormatBody(ushort tp, string kind, JObject o)
        {
            switch (kind)
            {
                case "tap":
                {
                    bool click = ((int?)o["click"] ?? 0) == 1;
                    bool cancel = ((int?)o["cancel"] ?? 0) == 1;
                    bool reinserted = ((int?)o["re"] ?? 0) == 1;
                    uint? j = (uint?)o["j"];
                    int? ol = (int?)o["ol"];
                    string why = click ? "" :
                        cancel ? "  NO CLICK: browser cancelled (scroll/gesture)" :
                        reinserted ? "  NO CLICK: button pulled from DOM mid-press (buttonPressedReset)" :
                        "  NO CLICK";
                    return string.Format(
                        "[TAP] TP-{0} {1}{2} click={3} down={4}ms moved={5}px inputLag={6} clickDelay={7} rx2s={8}{9}{10}{11}",
                        tp, (string)o["el"] ?? "?", j.HasValue ? "(" + j.Value + ")" : "",
                        click ? "Y" : "N", (int?)o["dur"] ?? -1, (int?)o["mv"] ?? -1,
                        Ms(o["lagIn"]), Ms(o["clickMs"]), (int?)o["rx2s"] ?? -1,
                        o["wk"] != null ? " wake=" + Ms(o["wk"]) : "",
                        ol == 0 ? " LINK-DOWN" : "", why);
                }
                case "stall":
                    return string.Format(
                        "[STALL] TP-{0} page froze: worst {1}ms, {2}ms blocked over {3}ms, {4} joins received (top: {5})",
                        tp, (int?)o["worst"] ?? -1, (int?)o["blocked"] ?? -1, (int?)o["dur"] ?? -1,
                        (int?)o["rx"] ?? -1, (string)o["top"] ?? "");
                case "link":
                    return string.Format("[TAP] TP-{0} panel says processor link {1}", tp,
                        ((int?)o["on"] ?? 0) == 1 ? "UP" : "DOWN");
                case "resume":
                    return string.Format("[WAKE] TP-{0} page woke after its timers were frozen {1} (screen asleep)",
                        tp, Duration((long?)o["slept"] ?? 0));
                case "vis":
                    return string.Format("[VIS] TP-{0} page {1}", tp, ((int?)o["on"] ?? 0) == 1 ? "shown" : "hidden");
                case "trace":
                    return string.Format("[TAP] TP-{0} tracing {1}", tp, ((int?)o["on"] ?? 0) == 1 ? "ON" : "OFF");
                case "btnreset":
                    return string.Format("[TAP] TP-{0} buttonPressedReset {1}", tp, ((int?)o["on"] ?? 0) == 1 ? "ON" : "OFF");
                default:
                    return string.Format("[TAP] TP-{0} {1}", tp, o.ToString(Newtonsoft.Json.Formatting.None));
            }
        }

        private static string Duration(long ms)
        {
            TimeSpan d = TimeSpan.FromMilliseconds(ms);
            return d.TotalMinutes >= 1 ? string.Format("{0}h{1:00}m", (int)d.TotalHours, d.Minutes)
                : string.Format("{0:0.0}s", d.TotalSeconds);
        }

        private static string Ms(JToken t)
        {
            int? v = (int?)t;
            return v.HasValue ? v.Value + "ms" : "n/a";
        }

        private static string PanelTime(long epochMs)
        {
            if (epochMs <= 0) { return "?"; }
            DateTime utc = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddMilliseconds(epochMs);
            return utc.ToLocalTime().ToString("MM-dd HH:mm:ss.fff");
        }

        private static ulong Key(ushort tp, uint join) { return ((ulong)tp << 32) | join; }

        // ─── replay matching (call under _lock) ────────────────────────────

        // n restarts at every page load, so n alone is ambiguous across a reload; the panel's
        // own timestamp disambiguates.
        private void SeenAdd(ushort tp, long n, long ts)
        {
            HashSet<string> set;
            Queue<string> order;
            if (!_seen.TryGetValue(tp, out set))
            {
                set = new HashSet<string>();
                order = new Queue<string>();
                _seen[tp] = set;
                _seenOrder[tp] = order;
            }
            else { order = _seenOrder[tp]; }
            string k = n + ":" + ts;
            if (set.Add(k))
            {
                order.Enqueue(k);
                while (order.Count > SeenMax) { set.Remove(order.Dequeue()); }
            }
        }

        private bool SeenContains(ushort tp, long n, long ts)
        {
            HashSet<string> set;
            return _seen.TryGetValue(tp, out set) && set.Contains(n + ":" + ts);
        }

        // ─── file ──────────────────────────────────────────────────────────

        private string PathFor(DateTime d)
        {
            return string.Format("{0}/taptrace-{1:yyyyMMdd}.log", _dir, d);
        }

        private void Write(string line, bool alsoErrorLog)
        {
            CrestronConsole.PrintLine(line);
            if (alsoErrorLog) { ErrorLog.Warn(line); }

            DateTime now = DateTime.Now;
            lock (_lock)
            {
                try
                {
                    string day = now.ToString("yyyyMMdd");
                    if (day != _currentDay)
                    {
                        if (!Directory.Exists(_dir)) { Directory.CreateDirectory(_dir); }
                        _currentDay = day;
                        PruneOldFiles(now);
                    }
                    using (var fs = new FileStream(PathFor(now), FileMode.Append))
                    using (var w = new StreamWriter(fs, Encoding.UTF8))
                    {
                        w.Write(now.ToString("yyyy-MM-dd HH:mm:ss.fff "));
                        w.Write(line);
                        w.Write("\n");
                    }
                }
                catch (Exception ex)
                {
                    ErrorLog.Error("[TAP] can't write trace file: {0}", ex.Message);
                }
            }
        }

        private void PruneOldFiles(DateTime now)
        {
            try
            {
                foreach (string f in Directory.GetFiles(_dir, "taptrace-*.log"))
                {
                    if (File.GetLastWriteTime(f) < now.AddDays(-RetainDays)) { File.Delete(f); }
                }
            }
            catch (Exception ex)
            {
                ErrorLog.Error("[TAP] can't prune old trace files: {0}", ex.Message);
            }
        }
    }
}
