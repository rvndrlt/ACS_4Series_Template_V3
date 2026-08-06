using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Crestron.SimplSharp;
using Crestron.SimplSharp.CrestronIO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ACS_4Series_Template_V3.Cameras
{
    /// <summary>
    /// Cameras subsystem for HTML panels. Reads a whole-house camera list
    /// (name + RTSP url) from \NVRAM\cameraConfig.json and exposes it to the
    /// panels over raw serial/analog joins — no contract/cse2j involvement, same
    /// style as the page descriptor (1520) and quick actions (1530-1532):
    ///
    ///   1541 (serial,  C#→HTML): catalog { "cameras": [ { "name": "..." }, ... ] } (names only)
    ///   1542 (serial,  HTML→C#): select  { "seq": &lt;n&gt;, "index": &lt;1-based&gt; }
    ///   1544 (analog,  C#→HTML): selected camera number feedback (1-based; 0 = none)
    ///   1545 (serial,  C#→HTML): active RTSP url for the selected camera → ch5-video
    ///
    /// C# is the source of truth for the stream: the RTSP urls never leave the
    /// processor. HTML sends "I want camera N" and C# answers with the url (1545)
    /// + highlight (1544). Selection is tracked per panel so two panels can view
    /// different cameras independently; to mirror all panels instead, change
    /// ApplySelection to write every HTML panel.
    ///
    /// Page-flip: the "Cameras" subsystem routes through the existing descriptor
    /// on serial 1520 (SubsystemPageKey("Cameras") → "cameras"); pageRouter.js
    /// shows the CH5-free page. No work needed here for navigation.
    /// </summary>
    public class CameraManager
    {
        private readonly ControlSystem _parent;

        private const string ConfigFilePath = @"\NVRAM\cameraConfig.json";

        public const ushort CatalogJoin = 1541;    // serial C#→HTML
        public const ushort SelectJoin = 1542;     // serial HTML→C#
        public const ushort SelectedFbJoin = 1544; // analog C#→HTML
        public const ushort UrlJoin = 1545;        // serial C#→HTML
        public const ushort RetryJoin = 1546;      // analog C#→HTML: retry nonce; bump = "re-open the current stream"
        public const ushort GapJoin = 1547;        // analog C#→HTML: per-panel teardown gap (ms) for stop→start

        // ch5-video's own diagnostics, reported by the panel's decoder (HTML→C#).
        // These are the ONLY reliable way to find out why a given stream will not
        // render — the component tells you instead of us guessing about codecs.
        public const ushort VideoStateJoin = 1550;        // analog
        public const ushort VideoErrorCodeJoin = 1551;    // analog
        public const ushort VideoErrorMessageJoin = 1552; // serial
        public const ushort VideoResolutionJoin = 1553;   // serial
        public const ushort VideoRetryCountJoin = 1554;   // analog

        public static bool IsVideoDiagAnalogJoin(uint join)
        {
            return join == VideoStateJoin || join == VideoErrorCodeJoin || join == VideoRetryCountJoin;
        }

        public static bool IsVideoDiagSerialJoin(uint join)
        {
            return join == VideoErrorMessageJoin || join == VideoResolutionJoin;
        }

        /// <summary>
        /// Logs a ch5-video diagnostic event to the Crestron console. Watch this
        /// while selecting a camera on the panel to see exactly why it fails —
        /// state/errorCode/errorMessage/resolution/retryCount come from the
        /// panel's decoder, not from us.
        /// </summary>
        // Millisecond timestamp so the console log can be used to measure how long
        // a stream takes to connect or fail (instead of eyeballing it). Local time,
        // HH:mm:ss.fff.
        private static string Ts()
        {
            return DateTime.Now.ToString("HH:mm:ss.fff");
        }

        public void LogVideoDiag(ushort tpNumber, uint join, string value)
        {
            string label;
            switch (join)
            {
                case VideoStateJoin:        label = "state";        break;
                case VideoErrorCodeJoin:    label = "errorCode";    break;
                case VideoErrorMessageJoin: label = "errorMessage"; break;
                case VideoResolutionJoin:   label = "resolution";   break;
                case VideoRetryCountJoin:   label = "retryCount";   break;
                default:                    label = "join " + join; break;
            }

            int sel;
            string camName = "(none)";
            if (selectedByTp.TryGetValue(tpNumber, out sel) && sel >= 1)
            {
                lock (camerasLock)
                {
                    if (sel <= cameras.Count) { camName = cameras[sel - 1].Name ?? "(unnamed)"; }
                }
            }

            CrestronConsole.PrintLine("{0} TP-{1} ch5-video {2} = \"{3}\"  [camera: {4}]",
                Ts(), tpNumber, label, value, camName);

            // Feed the decoder state/errorCode into the auto-retry + adaptive gap.
            // HTML cannot observe these send-joins, so this logic lives here where
            // we DO see them.
            if (join == VideoErrorCodeJoin)
            {
                int code;
                if (int.TryParse(value, out code))
                {
                    lock (retryLock) { lastErrorCodeByTp[tpNumber] = code; }
                }
            }
            else if (join == VideoStateJoin)
            {
                int state;
                if (int.TryParse(value, out state)) { OnVideoState(tpNumber, state); }
            }
        }

        // ─── Auto-retry on decoder failure ──────────────────────────────────
        //
        // A stream can connect (state 2) and then die a few seconds later with a
        // session error (56529/56532), or fail to open at all — either way it lands
        // on state 7 (FAILED) and STAYS there; ch5-video does not recover on its own
        // and the picture is dead until the play gate is cycled. HTML can't see the
        // state (it's a send-join), so C# is the detector: on a confirmed failure we
        // bump the retry nonce (1546) and HTML re-opens the current stream — the gate
        // stays HTML-owned (its local bridge), so we never fight it over CIP. Capped
        // so a truly dead/unsupported camera doesn't loop forever leaking sessions.
        //
        // Gated on the panel actually being ON the cameras page (SetPageActive, driven
        // from the page descriptor) so a retry never fires after the user navigates
        // away. Timer callbacks run off-thread → retryLock.
        //
        // Adaptive per-panel teardown gap: panels vary wildly in how fast they release
        // an RTSP session (a TSW-1060 is fine at 3s; a TST-1080 leaks sessions and needs
        // much longer). Rather than one global gap that's too slow for fast panels or
        // too fast for slow ones, each panel starts at BaseGapMs and ratchets UP by
        // GapStepMs (capped at MaxGapMs) every time it hits a session error — so it
        // self-tunes to its own hardware. C# owns the value and pushes it to HTML on
        // GapJoin (1547); HTML uses it as the stop→start gap. Codec errors (64533) do
        // NOT raise the gap — more time won't fix an unsupported codec.
        private const int VideoStatePlaying = 2;
        private const int VideoStateFailed = 7;
        private const long FailConfirmMs = 2500; // let a transient state-7 self-recover first
        private const int MaxAutoRetries = 2;

        private const ushort BaseGapMs = 3000;
        private const ushort GapStepMs = 1500;
        private const ushort MaxGapMs = 8000;

        private readonly object retryLock = new object();
        private readonly Dictionary<ushort, int> lastStateByTp = new Dictionary<ushort, int>();
        private readonly Dictionary<ushort, int> lastErrorCodeByTp = new Dictionary<ushort, int>();
        private readonly Dictionary<ushort, int> retryCountByTp = new Dictionary<ushort, int>();
        private readonly Dictionary<ushort, bool> pageActiveByTp = new Dictionary<ushort, bool>();
        private readonly Dictionary<ushort, CTimer> confirmTimerByTp = new Dictionary<ushort, CTimer>();
        private readonly Dictionary<ushort, ushort> retryNonceByTp = new Dictionary<ushort, ushort>();
        private readonly Dictionary<ushort, ushort> gapByTp = new Dictionary<ushort, ushort>();

        private static bool IsSessionError(int code)
        {
            return code == 56529 || code == 56532;
        }

        /// <summary>Current teardown gap for a panel (BaseGapMs until it has ratcheted).</summary>
        private ushort GapFor(ushort tpNumber)
        {
            ushort g;
            return gapByTp.TryGetValue(tpNumber, out g) && g > 0 ? g : BaseGapMs;
        }

        /// <summary>Push a panel's current teardown gap to HTML on 1547.</summary>
        private void SendGap(ushort tpNumber)
        {
            UI.TouchpanelUI tp;
            if (!_parent.manager.touchpanelZ.TryGetValue(tpNumber, out tp) ||
                tp == null || !tp.HTML_UI || tp.UserInterface == null) { return; }
            tp.UserInterface.UShortInput[GapJoin].UShortValue = GapFor(tpNumber);
        }

        /// <summary>Increase a panel's teardown gap after a session error (capped).
        /// Returns true and pushes the new value to HTML if it actually changed.
        /// Caller holds retryLock.</summary>
        private bool BumpGap(ushort tpNumber)
        {
            ushort current = GapFor(tpNumber);
            if (current >= MaxGapMs) { return false; }
            ushort next = (ushort)Math.Min(MaxGapMs, current + GapStepMs);
            gapByTp[tpNumber] = next;
            SendGap(tpNumber);
            CrestronConsole.PrintLine("{0} Cameras: TP-{1} session error - teardown gap {2} -> {3} ms",
                Ts(), tpNumber, current, next);
            return true;
        }

        /// <summary>
        /// Called from the page-descriptor path: true when this panel is shown the
        /// Cameras page, false when it navigates away (any other page / home). While
        /// false, no auto-retry runs and any pending retry for the panel is cancelled.
        /// </summary>
        public void SetPageActive(ushort tpNumber, bool active)
        {
            lock (retryLock)
            {
                pageActiveByTp[tpNumber] = active;
                if (!active)
                {
                    CancelConfirm(tpNumber);
                    retryCountByTp[tpNumber] = 0;
                }
            }
        }

        private bool PageActive(ushort tpNumber)
        {
            bool a;
            return pageActiveByTp.TryGetValue(tpNumber, out a) && a;
        }

        private void CancelConfirm(ushort tpNumber)
        {
            CTimer t;
            if (confirmTimerByTp.TryGetValue(tpNumber, out t) && t != null) { t.Stop(); t.Dispose(); }
            confirmTimerByTp[tpNumber] = null;
        }

        private void OnVideoState(ushort tpNumber, int state)
        {
            lock (retryLock)
            {
                lastStateByTp[tpNumber] = state;

                if (state == VideoStatePlaying)
                {
                    // Recovered / healthy — cancel any pending retry. We deliberately
                    // do NOT reset the retry budget here: a camera that connects then
                    // dies a few seconds later would otherwise reset on every reconnect
                    // and retry forever. The budget is per-selection (reset on a fresh
                    // pick / page open), so a connect→die→connect→die stream still stops
                    // after MaxAutoRetries.
                    CancelConfirm(tpNumber);
                    return;
                }

                if (state == VideoStateFailed && PageActive(tpNumber))
                {
                    CTimer existing;
                    bool pending = confirmTimerByTp.TryGetValue(tpNumber, out existing) && existing != null;
                    if (!pending)
                    {
                        ushort tp = tpNumber;
                        confirmTimerByTp[tpNumber] = new CTimer(o => ConfirmFailureAndRetry(tp), FailConfirmMs);
                    }
                }
            }
        }

        private void ConfirmFailureAndRetry(ushort tpNumber)
        {
            lock (retryLock)
            {
                confirmTimerByTp[tpNumber] = null;
                if (!PageActive(tpNumber)) { return; }

                int state;
                if (lastStateByTp.TryGetValue(tpNumber, out state) && state == VideoStatePlaying) { return; }

                // A session error means the panel opened the new stream before it
                // finished releasing the old one — give THIS panel more teardown time
                // next round. Self-tunes slow leakers (TST-1080) without slowing fast
                // panels (TSW-1060). Codec errors don't get more time (won't help).
                int errCode;
                lastErrorCodeByTp.TryGetValue(tpNumber, out errCode);
                if (IsSessionError(errCode)) { BumpGap(tpNumber); }

                int count;
                retryCountByTp.TryGetValue(tpNumber, out count);
                if (count >= MaxAutoRetries)
                {
                    CrestronConsole.PrintLine("{0} Cameras: TP-{1} stream still failed after {2} retries - giving up until reselected",
                        Ts(), tpNumber, MaxAutoRetries);
                    return;
                }
                retryCountByTp[tpNumber] = count + 1;
                CrestronConsole.PrintLine("{0} Cameras: TP-{1} stream failed (state {2}, err {3}) - auto-retry {4} of {5}",
                    Ts(), tpNumber, state, errCode, count + 1, MaxAutoRetries);
                RequestRetry(tpNumber);
            }
        }

        /// <summary>Ask HTML to re-open the current stream by bumping the retry
        /// nonce (1546). HTML owns the play gate and does the actual stop→gap→start,
        /// so the gate is never driven over CIP. Caller holds retryLock.</summary>
        private void RequestRetry(ushort tpNumber)
        {
            UI.TouchpanelUI tp;
            if (!_parent.manager.touchpanelZ.TryGetValue(tpNumber, out tp) ||
                tp == null || !tp.HTML_UI || tp.UserInterface == null) { return; }

            ushort nonce;
            retryNonceByTp.TryGetValue(tpNumber, out nonce);
            nonce = (ushort)(nonce + 1);
            retryNonceByTp[tpNumber] = nonce;
            tp.UserInterface.UShortInput[RetryJoin].UShortValue = nonce;
        }

        /// <summary>Reset the auto-retry budget for a panel — a fresh user selection
        /// is not a recovery attempt.</summary>
        private void ResetRetry(ushort tpNumber)
        {
            lock (retryLock)
            {
                CancelConfirm(tpNumber);
                retryCountByTp[tpNumber] = 0;
            }
        }

        private readonly object camerasLock = new object();
        private List<CameraEntry> cameras = new List<CameraEntry>();
        private string catalogJson = "{\"cameras\":[]}";

        // Per-panel current selection (1-based; absent/0 = none).
        private readonly Dictionary<ushort, int> selectedByTp = new Dictionary<ushort, int>();

        public class CameraEntry
        {
            [JsonProperty("name")]
            public string Name { get; set; }

            [JsonProperty("rtspUrl")]
            public string RtspUrl { get; set; }
        }

        private class CameraFile
        {
            [JsonProperty("cameras")]
            public List<CameraEntry> Cameras { get; set; }
        }

        public CameraManager(ControlSystem parent)
        {
            _parent = parent;
        }

        // ─── Config load ────────────────────────────────────────────────────

        public void Load()
        {
            try
            {
                if (File.Exists(ConfigFilePath))
                {
                    string json = File.ReadToEnd(ConfigFilePath, Encoding.UTF8);
                    var loaded = JsonConvert.DeserializeObject<CameraFile>(json);
                    lock (camerasLock)
                    {
                        cameras = (loaded != null && loaded.Cameras != null)
                            ? loaded.Cameras
                            : new List<CameraEntry>();
                        catalogJson = BuildCatalogJson();
                    }
                    CrestronConsole.PrintLine("Cameras: loaded {0} cameras from {1}", cameras.Count, ConfigFilePath);
                }
                else
                {
                    CrestronConsole.PrintLine("Cameras: no config file ({0})", ConfigFilePath);
                    lock (camerasLock)
                    {
                        cameras = new List<CameraEntry>();
                        catalogJson = BuildCatalogJson();
                    }
                }
            }
            catch (Exception ex)
            {
                ErrorLog.Error("Cameras Load error: {0}", ex.Message);
                lock (camerasLock)
                {
                    cameras = new List<CameraEntry>();
                    catalogJson = BuildCatalogJson();
                }
            }
        }

        private string BuildCatalogJson()
        {
            var payload = new
            {
                cameras = cameras.Select(c => new { name = c.Name ?? string.Empty }).ToArray()
            };
            return JsonConvert.SerializeObject(payload);
        }

        // ─── Catalog (1541) ─────────────────────────────────────────────────

        /// <summary>Push the catalog to one HTML panel and re-apply its selection
        /// (boot / panel-online reconnect replay).</summary>
        public void SendCatalogTo(UI.TouchpanelUI tp)
        {
            try
            {
                if (tp == null || !tp.HTML_UI || tp.UserInterface == null) return;
                string json;
                lock (camerasLock) { json = catalogJson; }
                tp.UserInterface.StringInput[CatalogJoin].StringValue = json;

                // Push this panel's teardown gap so HTML starts with the right value
                // (a panel that already ratcheted up keeps it across reconnect).
                lock (retryLock) { SendGap(tp.Number); }

                // Reconnect replay: restore this panel's active stream + highlight.
                int sel;
                if (selectedByTp.TryGetValue(tp.Number, out sel) && sel > 0)
                {
                    ApplySelection(tp, sel);
                }
                else
                {
                    tp.UserInterface.UShortInput[SelectedFbJoin].UShortValue = 0;
                }
            }
            catch (Exception ex)
            {
                ErrorLog.Error("Cameras SendCatalogTo error: {0}", ex.Message);
            }
        }

        public void SendCatalogToAll()
        {
            foreach (var tp in _parent.manager.touchpanelZ)
            {
                SendCatalogTo(tp.Value);
            }
        }

        /// <summary>
        /// Re-reads \NVRAM\cameraConfig.json and re-pushes the catalog to every
        /// panel so edits take effect without a program restart (console command
        /// "reloadcameras"). Each panel's current selection is re-applied, which
        /// re-sends its RTSP url — so an edited url reconnects on its own; the
        /// HTML side only restarts the stream when the url actually changed.
        /// Caveat: selection is tracked by list index, so if you REORDER or REMOVE
        /// cameras a panel may briefly show the camera now at its old index until
        /// you reselect.
        /// </summary>
        public void Reload()
        {
            Load();
            SendCatalogToAll();
            int count;
            lock (camerasLock) { count = cameras.Count; }
            CrestronConsole.PrintLine("Cameras: reloaded {0} cameras and re-pushed to all panels", count);
        }

        /// <summary>
        /// Camera-popup EISC join numbers (IPID 0xC0 @ 127.0.0.2, shared with the
        /// VizioTVControl program).
        ///
        /// Serial-only with a `seq` counter, deliberately NOT an analog + digital pulse:
        /// re-sending an identical serial raises no sig change, so `seq` is what makes a
        /// repeat event fire — and a single self-describing serial avoids the
        /// "set the value, then pulse the trigger" ordering hazard entirely. Same pattern as
        /// quick actions (1531), camera select (1542) and intercom commands (1561).
        /// </summary>
        public const ushort PopupEiscCommandJoin = 1; // serial, App03 → App01

        private int lastPopupSeq = -1;

        /// <summary>
        /// Entry point from the camera-popup EISC handler in ControlSystem.
        /// Payload: { "seq": &lt;n&gt;, "camera": "Front Gate", "reason": "ring"|"person" }
        /// </summary>
        public void HandlePopupCommand(string json)
        {
            if (string.IsNullOrEmpty(json)) { return; }
            try
            {
                var obj = JObject.Parse(json);
                int seq = (int?)obj["seq"] ?? 0;
                string camera = (string)obj["camera"] ?? string.Empty;
                string reason = (string)obj["reason"] ?? "external";

                // Ignore a replayed identical seq. The EISC re-asserts its serial values when
                // the link re-establishes (program restart on either side), and without this
                // an App01 restart would pop a camera for an event that happened minutes ago.
                if (seq != 0 && seq == lastPopupSeq)
                {
                    CrestronConsole.PrintLine("Cameras: popup seq {0} already handled - ignoring replay", seq);
                    return;
                }
                lastPopupSeq = seq;

                CrestronConsole.PrintLine("{0} Cameras: popup command seq={1} camera=\"{2}\" reason={3}",
                    Ts(), seq, camera, reason);
                PopupCameraOnAllPanels(camera, reason);
            }
            catch (Exception ex)
            {
                ErrorLog.Error("Cameras HandlePopupCommand error: {0} (payload: {1})", ex.Message, json);
            }
        }

        // ─── Select command (1542) ──────────────────────────────────────────

        /// <summary>Entry point from TouchpanelUI.SigChange for serial join 1542.</summary>
        public void HandleSelect(ushort tpNumber, string json)
        {
            if (string.IsNullOrEmpty(json)) return;
            try
            {
                var obj = JObject.Parse(json);
                int index = (int?)obj["index"] ?? 0; // 1-based
                CrestronConsole.PrintLine("{0} Cameras: TP-{1} select index {2}", Ts(), tpNumber, index);

                int count;
                lock (camerasLock) { count = cameras.Count; }
                if (index < 1 || index > count)
                {
                    CrestronConsole.PrintLine("Cameras: TP-{0} select index {1} out of range (1..{2})", tpNumber, index, count);
                    return;
                }

                selectedByTp[tpNumber] = index;
                if (_parent.manager.touchpanelZ.ContainsKey(tpNumber))
                {
                    ApplySelection(_parent.manager.touchpanelZ[tpNumber], index);
                }
            }
            catch (Exception ex)
            {
                ErrorLog.Error("Cameras HandleSelect error: {0}", ex.Message);
            }
        }

        // ─── External camera popup ──────────────────────────────────────────
        //
        // Entry point for "something happened, show camera X on the panels now". The
        // trigger lives outside this program — a UniFi Protect doorbell ring or person
        // detection, watched by the VizioTVControl program (App03) and delivered over the
        // camera-popup EISC (IPID 0xC0, serial 1). Kept deliberately generic: this program
        // knows nothing about UniFi, only "pop this camera".

        /// <summary>
        /// Resolves a camera NAME to its 1-based catalog index, or 0 when unknown.
        /// Case-insensitive, trimmed.
        ///
        /// Name rather than index is the wire format on purpose: the index is just the
        /// position in cameraConfig.json, so an index would silently point at the wrong
        /// camera the first time that file is reordered.
        /// </summary>
        public int IndexOfCameraName(string name)
        {
            if (string.IsNullOrEmpty(name)) { return 0; }
            string wanted = name.Trim();

            lock (camerasLock)
            {
                for (int i = 0; i < cameras.Count; i++)
                {
                    string n = cameras[i].Name;
                    if (!string.IsNullOrEmpty(n) &&
                        n.Trim().Equals(wanted, StringComparison.OrdinalIgnoreCase))
                    {
                        return i + 1; // 1-based on the wire
                    }
                }
            }
            return 0;
        }

        /// <summary>
        /// Selects a camera by name and forces the Cameras page onto every HTML panel.
        /// `reason` is free text used only for logging ("ring", "person", "manual").
        /// </summary>
        public void PopupCameraOnAllPanels(string cameraName, string reason)
        {
            int index = IndexOfCameraName(cameraName);
            if (index < 1)
            {
                // Loud, and it lists what IS available — a name mismatch between this
                // program's cameraConfig.json and whatever fired the trigger is the most
                // likely failure here, and it would otherwise be completely silent.
                string available;
                lock (camerasLock)
                {
                    available = cameras.Count == 0
                        ? "(catalog empty)"
                        : string.Join(", ", cameras.Select(c => "\"" + (c.Name ?? "") + "\"").ToArray());
                }
                CrestronConsole.PrintLine("{0} Cameras: popup for \"{1}\" IGNORED - no such camera. Catalog: {2}",
                    Ts(), cameraName, available);
                return;
            }

            int popped = 0;
            foreach (var kv in _parent.manager.touchpanelZ)
            {
                var tp = kv.Value;
                if (tp == null || !tp.HTML_UI || tp.UserInterface == null) { continue; }

                selectedByTp[tp.Number] = index;

                // ⚠ PER-PANEL ISOLATION. Without it, one panel throwing anywhere below abandons
                // every panel after it — and because the wake call was added to the front of this
                // block, a wake failure on the FIRST panel would silently kill the entire popup.
                // That is indistinguishable from "the trigger never arrived", which is exactly the
                // ambiguity that has made this feature hard to debug.
                try
                {
                    // WAKE FIRST. A popup on a sleeping panel is useless — the page flip happens
                    // behind a dark screen and nobody sees the person at the door, which is the
                    // entire point of the feature. Mirrors IntercomManager, which wakes before
                    // flipping for the same reason.
                    //
                    // Wrapped separately from the page flip: waking is the NICE-TO-HAVE and the
                    // page flip is the feature. A wake that throws must never cost the flip.
                    try { tp.WakePanel(); }
                    catch (Exception wakeEx)
                    {
                        CrestronConsole.PrintLine("Cameras: TP-{0} wake failed ({1}) - continuing with the page flip",
                            tp.Number, wakeEx.Message);
                    }

                    // ⚠ SELECTION BEFORE THE PAGE FLIP. ApplySelection sets the RTSP url, and
                    // ch5-video will not re-open a stream whose url changes while it is already
                    // playing — so a url that lands after the page opens costs a full stop/start
                    // with the ~3s RTSP teardown gap. Setting it first means the gate comes up
                    // already pointing at the right stream. Same ordering rule as
                    // IntercomManager.OnVoipStateChanged.
                    ApplySelection(tp, index);
                    tp.ShowCamerasPage();
                    popped++;
                }
                catch (Exception ex)
                {
                    CrestronConsole.PrintLine("Cameras: TP-{0} popup FAILED: {1}", tp.Number, ex.Message);
                    ErrorLog.Error("Cameras popup failed for TP-{0}: {1} | {2}", tp.Number, ex.Message, ex.StackTrace);
                }
            }

            CrestronConsole.PrintLine("{0} Cameras: popup \"{1}\" (index {2}, reason {3}) -> {4} HTML panel(s)",
                Ts(), cameraName, index, reason ?? "?", popped);
        }

        /// <summary>Drive one panel's active RTSP url (1545) + selected-number
        /// highlight (1544) for a 1-based camera index.</summary>
        private void ApplySelection(UI.TouchpanelUI tp, int index)
        {
            if (tp == null || !tp.HTML_UI || tp.UserInterface == null) return;

            // Fresh selection (or reconnect replay) — not a recovery, so clear the
            // auto-retry budget and cancel any pending retry for this panel.
            ResetRetry(tp.Number);

            string url = string.Empty;
            lock (camerasLock)
            {
                if (index >= 1 && index <= cameras.Count)
                {
                    url = cameras[index - 1].RtspUrl ?? string.Empty;
                }
            }

            tp.UserInterface.StringInput[UrlJoin].StringValue = url;
            tp.UserInterface.UShortInput[SelectedFbJoin].UShortValue = (ushort)index;
            CrestronConsole.PrintLine("{0} TP-{1} camera {2} -> url set (len {3})", Ts(), tp.Number, index, url.Length);
        }
    }
}
