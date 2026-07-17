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

            // Feed the decoder state into the auto-retry. HTML cannot observe these
            // send-joins, so recovery has to live here where we DO see them.
            if (join == VideoStateJoin)
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
        private const int VideoStatePlaying = 2;
        private const int VideoStateFailed = 7;
        private const long FailConfirmMs = 2500; // let a transient state-7 self-recover first
        private const int MaxAutoRetries = 2;

        private readonly object retryLock = new object();
        private readonly Dictionary<ushort, int> lastStateByTp = new Dictionary<ushort, int>();
        private readonly Dictionary<ushort, int> retryCountByTp = new Dictionary<ushort, int>();
        private readonly Dictionary<ushort, bool> pageActiveByTp = new Dictionary<ushort, bool>();
        private readonly Dictionary<ushort, CTimer> confirmTimerByTp = new Dictionary<ushort, CTimer>();
        private readonly Dictionary<ushort, ushort> retryNonceByTp = new Dictionary<ushort, ushort>();

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

                int count;
                retryCountByTp.TryGetValue(tpNumber, out count);
                if (count >= MaxAutoRetries)
                {
                    CrestronConsole.PrintLine("{0} Cameras: TP-{1} stream still failed after {2} retries — giving up until reselected",
                        Ts(), tpNumber, MaxAutoRetries);
                    return;
                }
                retryCountByTp[tpNumber] = count + 1;
                CrestronConsole.PrintLine("{0} Cameras: TP-{1} stream failed (state {2}) — auto-retry {3} of {4}",
                    Ts(), tpNumber, state, count + 1, MaxAutoRetries);
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
