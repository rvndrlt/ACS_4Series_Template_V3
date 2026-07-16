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

            CrestronConsole.PrintLine("TP-{0} ch5-video {1} = \"{2}\"  [camera: {3}]",
                tpNumber, label, value, camName);
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

        // ─── Select command (1542) ──────────────────────────────────────────

        /// <summary>Entry point from TouchpanelUI.SigChange for serial join 1542.</summary>
        public void HandleSelect(ushort tpNumber, string json)
        {
            if (string.IsNullOrEmpty(json)) return;
            try
            {
                var obj = JObject.Parse(json);
                int index = (int?)obj["index"] ?? 0; // 1-based
                CrestronConsole.PrintLine("Cameras: TP-{0} select index {1}", tpNumber, index);

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
            CrestronConsole.PrintLine("TP-{0} camera {1} -> url set (len {2})", tp.Number, index, url.Length);
        }
    }
}
