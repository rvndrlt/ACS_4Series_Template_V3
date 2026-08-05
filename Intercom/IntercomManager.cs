using System;
using System.Collections.Generic;
using System.Text;
using Crestron.SimplSharp;
using Crestron.SimplSharp.CrestronIO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ACS_4Series_Template_V3.Intercom
{
    /// <summary>
    /// Intercom subsystem for HTML panels — a SIP door-station page driven by the
    /// panel's own VOIP reserved joins (see UI/TouchpanelUI.Voip.cs for why that
    /// access is reflection-based).
    ///
    /// Joins (all raw, HTML-reserved 1500+ — no contract/cse2j involvement, same
    /// style as the page descriptor 1520, quick actions 1530-1534, cameras 1540-1554):
    ///
    ///   1560 (serial,  C#→HTML): state  { "state":"incoming", "name":"Front Door",
    ///                                     "num":"1001", "dnd":0, "mute":0,
    ///                                     "reg":1, "voip":1 }
    ///   1561 (serial,  HTML→C#): command { "seq":&lt;n&gt;, "cmd":"answer|reject|hangup|dnd|mute|pageall" }
    ///   1562 (serial,  C#→HTML): video url for the active/incoming call → ch5-video
    ///   1564 (analog,  HTML→C#): panel speaker volume set
    ///   1565 (analog,  C#→HTML): panel speaker volume feedback
    ///
    /// The video url is deliberately on its OWN join and not folded into 1560: urls
    /// are long, 1560 is always-on, and long always-on serial joins get TRUNCATED on
    /// hardware and mobile panels (xpanel does not, which is how that bites you late).
    /// Same reason the quick-actions descriptor is kept lean.
    ///
    /// VIDEO SOURCE — two paths, in priority order:
    ///   1. VOIPVideoURLFeedback from the panel's VOIP extender. On the Rava
    ///      peer-to-peer path the panel negotiates the door station's video itself,
    ///      so this is the url that costs nothing extra and needs no 2N licence.
    ///   2. A configured RTSP url from \NVRAM\intercomConfig.json, matched to the
    ///      caller by SIP number then by display name, else the first station.
    ///      This is the fallback for when (1) comes back empty — and it requires the
    ///      2N Enhanced Video licence (RTSP server), so prefer (1).
    /// Whichever wins is logged, so the first hardware call tells you which path is
    /// actually live.
    ///
    /// Page-flip: the "Intercom" subsystem name routes through the normal descriptor
    /// (SubsystemPageKey("Intercom") → "intercom") when the user taps the home-page
    /// button. An INCOMING CALL additionally FORCES the page via
    /// TouchpanelUI.ShowIntercomPage(), whatever the panel was showing.
    /// </summary>
    public class IntercomManager
    {
        private readonly ControlSystem _parent;

        private const string ConfigFilePath = @"\NVRAM\intercomConfig.json";

        public const ushort StateJoin = 1560;    // serial C#→HTML
        public const ushort CommandJoin = 1561;  // serial HTML→C#
        public const ushort VideoUrlJoin = 1562; // serial C#→HTML
        public const ushort VolumeSetJoin = 1564; // analog HTML→C#
        public const ushort VolumeFbJoin = 1565;  // analog C#→HTML

        private static string Ts()
        {
            return DateTime.Now.ToString("HH:mm:ss.fff");
        }

        // ─── Config ─────────────────────────────────────────────────────────

        public class StationEntry
        {
            /// <summary>Display name. Matched (case-insensitive, contains) against the
            /// panel's IncomingDisplayNameFeedback when the SIP number does not match.</summary>
            [JsonProperty("name")]
            public string Name { get; set; }

            /// <summary>SIP extension/ID this station calls from. Primary match key.</summary>
            [JsonProperty("sipNumber")]
            public string SipNumber { get; set; }

            /// <summary>Fallback RTSP url, used only when the panel reports no video url.</summary>
            [JsonProperty("rtspUrl")]
            public string RtspUrl { get; set; }
        }

        private class IntercomFile
        {
            [JsonProperty("stations")]
            public List<StationEntry> Stations { get; set; }
        }

        private readonly object stationsLock = new object();
        private List<StationEntry> stations = new List<StationEntry>();

        public IntercomManager(ControlSystem parent)
        {
            _parent = parent;
        }

        public void Load()
        {
            try
            {
                if (File.Exists(ConfigFilePath))
                {
                    string json = File.ReadToEnd(ConfigFilePath, Encoding.UTF8);
                    var loaded = JsonConvert.DeserializeObject<IntercomFile>(json);
                    lock (stationsLock)
                    {
                        stations = (loaded != null && loaded.Stations != null)
                            ? loaded.Stations
                            : new List<StationEntry>();
                    }
                    CrestronConsole.PrintLine("Intercom: loaded {0} station(s) from {1}", stations.Count, ConfigFilePath);
                }
                else
                {
                    // Not an error: with the panel-reported video url (path 1) this file
                    // is optional. It only matters as the RTSP fallback.
                    CrestronConsole.PrintLine("Intercom: no config file ({0}) - RTSP fallback unavailable", ConfigFilePath);
                    lock (stationsLock) { stations = new List<StationEntry>(); }
                }
            }
            catch (Exception ex)
            {
                ErrorLog.Error("Intercom Load error: {0}", ex.Message);
                lock (stationsLock) { stations = new List<StationEntry>(); }
            }
        }

        /// <summary>Re-reads the config and re-pushes state to every panel (console
        /// command "reloadintercom"), so station/url edits take effect without a restart.</summary>
        public void Reload()
        {
            Load();
            foreach (var tp in _parent.manager.touchpanelZ) { SendStateTo(tp.Value); }
            int count;
            lock (stationsLock) { count = stations.Count; }
            CrestronConsole.PrintLine("Intercom: reloaded {0} station(s) and re-pushed to all panels", count);
        }

        // ─── Per-panel call tracking ────────────────────────────────────────

        // Last derived state per panel, so we only act on TRANSITIONS. The extender
        // fires a sig change for every individual feedback, so a single incoming call
        // produces several events with the same derived state — without this the page
        // would be re-forced (and the panel re-woken) on each one.
        //
        // stateLock is NOT optional: DeviceExtenderSigChange fires off-thread and once
        // per panel, so a house-wide ring (every panel in the call group, simultaneously)
        // mutates this dictionary concurrently — which corrupts a plain Dictionary
        // rather than merely racing on a value.
        private readonly object stateLock = new object();
        private readonly Dictionary<ushort, string> lastStateByTp = new Dictionary<ushort, string>();

        private const string StateIdle = "idle";
        private const string StateIncoming = "incoming";
        private const string StateRinging = "ringing";
        private const string StateActive = "active";
        private const string StateBusy = "busy";

        /// <summary>
        /// Derives ONE primary state from the panel's VOIP feedback. HTML also gets the
        /// raw flags, but the notification banner keys off this so exactly one shows.
        ///
        /// Order matters: an answered call asserts CallActive while IncomingCall may
        /// still be latched, so active must win. Busy is last because it is only
        /// meaningful when nothing else is happening (an outbound attempt to a busy
        /// far end).
        /// </summary>
        private static string DeriveState(UI.TouchpanelUI tp)
        {
            if (tp.VoipCallActive) { return StateActive; }
            if (tp.VoipIncoming) { return StateIncoming; }
            if (tp.VoipRinging) { return StateRinging; }
            if (tp.VoipBusy) { return StateBusy; }
            return StateIdle;
        }

        private static bool IsCallState(string state)
        {
            return state == StateIncoming || state == StateRinging || state == StateActive;
        }

        /// <summary>
        /// Called from the panel's VOIP DeviceExtenderSigChange hook. Recomputes state,
        /// pushes it to HTML, and on a transition INTO a call: wakes the panel, forces
        /// the Intercom page, suppresses the idle timeout, and resolves the video url.
        ///
        /// On the way out of a call it clears the idle suppression but deliberately
        /// does NOT navigate away — the panel stays on the Intercom page and the normal
        /// idle timeout takes it home, which is what was asked for.
        /// </summary>
        public void OnVoipStateChanged(UI.TouchpanelUI tp)
        {
            if (tp == null) { return; }

            string state = DeriveState(tp);
            string previous;
            bool hadPrevious;
            lock (stateLock)
            {
                hadPrevious = lastStateByTp.TryGetValue(tp.Number, out previous);
                lastStateByTp[tp.Number] = state;
            }
            bool changed = !hadPrevious || previous != state;

            if (changed)
            {
                CrestronConsole.PrintLine("{0} INTERCOM TP-{1} state {2} -> {3}  (name=\"{4}\" num=\"{5}\" dnd={6} mute={7})",
                    Ts(), tp.Number, hadPrevious ? previous : "(init)", state,
                    tp.VoipCallerName, tp.VoipCallerNumber, tp.VoipDndActive, tp.VoipMicMuted);
            }

            bool wasInCall = hadPrevious && IsCallState(previous);
            bool nowInCall = IsCallState(state);

            // Entering a call: make sure the panel is awake, on the page, and will not
            // be pulled home mid-call by the idle timeout.
            if (nowInCall && !wasInCall)
            {
                tp.IntercomCallActive = true;   // suppresses the idle-timeout go-home
                tp.WakePanel();

                // ⚠ ORDER MATTERS: url BEFORE the page flip. ch5-video will not re-open
                // a stream when receivestateurl changes while it is already playing, so
                // HTML has to cycle the play gate to pick up a late url — and that costs
                // a ~3s RTSP teardown gap. Setting the url while the page is still CLOSED
                // means the gate is down anyway, and pageRouter raises it as part of
                // opening the page, so the very first read is already the right url and
                // no restart is needed. Flip first and you pay the gap on every call.
                ApplyVideoUrl(tp);
                if (tp.HTML_UI) { tp.ShowIntercomPage(); }
            }
            else if (!nowInCall && wasInCall)
            {
                tp.IntercomCallActive = false;
                // Clear the stream so the viewer does not sit on a dead url.
                SetVideoUrl(tp, string.Empty);
            }
            else if (nowInCall && changed)
            {
                // Still in a call but the stage changed (e.g. incoming -> active): the
                // panel may only publish the video url once it answers, so re-resolve.
                ApplyVideoUrl(tp);
            }

            SendStateTo(tp);
        }

        // ─── State push (1560) ──────────────────────────────────────────────

        /// <summary>
        /// Pushes this panel's intercom state and current speaker volume. Also the
        /// reconnect-replay entry point (called from StartupPanel at boot and on
        /// panel-online), so a panel that reloads mid-call comes back correct.
        /// </summary>
        public void SendStateTo(UI.TouchpanelUI tp)
        {
            try
            {
                if (tp == null || !tp.HTML_UI || tp.UserInterface == null) { return; }

                string state = tp.HasVoip ? DeriveState(tp) : StateIdle;

                var sb = new StringBuilder();
                sb.Append("{\"state\":\"").Append(state).Append("\"");
                sb.Append(",\"name\":\"").Append(Escape(tp.VoipCallerName)).Append("\"");
                sb.Append(",\"num\":\"").Append(Escape(tp.VoipCallerNumber)).Append("\"");
                sb.Append(",\"dnd\":").Append(tp.VoipDndActive ? 1 : 0);
                sb.Append(",\"mute\":").Append(tp.VoipMicMuted ? 1 : 0);
                sb.Append(",\"reg\":").Append(tp.VoipRegistered ? 1 : 0);
                // voip:0 tells HTML this panel has no VOIP extender at all (xpanel), so
                // the page can say so instead of presenting dead buttons.
                sb.Append(",\"voip\":").Append(tp.HasVoip ? 1 : 0);
                sb.Append("}");

                tp.UserInterface.StringInput[StateJoin].StringValue = sb.ToString();

                // Echo the panel's own speaker level so the slider starts in the right place.
                if (tp.HasVoip)
                {
                    tp.UserInterface.UShortInput[VolumeFbJoin].UShortValue = tp.GetPanelSpeakerVolume();
                }
            }
            catch (Exception ex)
            {
                ErrorLog.Error("Intercom SendStateTo error: {0}", ex.Message);
            }
        }

        private static string Escape(string s)
        {
            if (string.IsNullOrEmpty(s)) { return string.Empty; }
            return s.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }

        // ─── Video url (1562) ───────────────────────────────────────────────

        /// <summary>
        /// Resolves and pushes the video url for the current call: the panel's own
        /// VOIPVideoURLFeedback if it has one, else the configured RTSP fallback for
        /// the matched station. Logs which path won — that is how you find out whether
        /// the Rava-negotiated video works on your hardware.
        /// </summary>
        private void ApplyVideoUrl(UI.TouchpanelUI tp)
        {
            string url = tp.VoipVideoUrl ?? string.Empty;
            string source = "panel VOIPVideoURLFeedback";

            if (string.IsNullOrEmpty(url))
            {
                var station = MatchStation(tp.VoipCallerNumber, tp.VoipCallerName);
                if (station != null && !string.IsNullOrEmpty(station.RtspUrl))
                {
                    url = station.RtspUrl;
                    source = "intercomConfig RTSP (" + (station.Name ?? "unnamed") + ")";
                }
            }

            if (string.IsNullOrEmpty(url))
            {
                CrestronConsole.PrintLine("{0} INTERCOM TP-{1} no video url (panel reported none, no RTSP fallback matched)",
                    Ts(), tp.Number);
            }
            else
            {
                CrestronConsole.PrintLine("{0} INTERCOM TP-{1} video url from {2} (len {3})",
                    Ts(), tp.Number, source, url.Length);
            }

            SetVideoUrl(tp, url);
        }

        private void SetVideoUrl(UI.TouchpanelUI tp, string url)
        {
            try
            {
                if (tp == null || !tp.HTML_UI || tp.UserInterface == null) { return; }
                tp.UserInterface.StringInput[VideoUrlJoin].StringValue = url ?? string.Empty;
            }
            catch (Exception ex)
            {
                ErrorLog.Error("Intercom SetVideoUrl error: {0}", ex.Message);
            }
        }

        /// <summary>
        /// Finds the configured station for a call: exact SIP number first (the
        /// reliable key), then a case-insensitive name match, then — when only one
        /// station is configured — that one, since a single-door system has no
        /// ambiguity to resolve.
        /// </summary>
        private StationEntry MatchStation(string sipNumber, string displayName)
        {
            lock (stationsLock)
            {
                if (stations.Count == 0) { return null; }

                if (!string.IsNullOrEmpty(sipNumber))
                {
                    foreach (var s in stations)
                    {
                        if (!string.IsNullOrEmpty(s.SipNumber) &&
                            s.SipNumber.Trim().Equals(sipNumber.Trim(), StringComparison.OrdinalIgnoreCase))
                        {
                            return s;
                        }
                    }
                }

                if (!string.IsNullOrEmpty(displayName))
                {
                    foreach (var s in stations)
                    {
                        if (!string.IsNullOrEmpty(s.Name) &&
                            displayName.IndexOf(s.Name, StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            return s;
                        }
                    }
                }

                return stations.Count == 1 ? stations[0] : null;
            }
        }

        // ─── Command channel (1561) ─────────────────────────────────────────

        /// <summary>Entry point from TouchpanelUI.SigChange for serial join 1561.</summary>
        public void HandleCommand(ushort tpNumber, string json)
        {
            if (string.IsNullOrEmpty(json)) { return; }

            UI.TouchpanelUI tp;
            if (!_parent.manager.touchpanelZ.TryGetValue(tpNumber, out tp) || tp == null)
            {
                CrestronConsole.PrintLine("Intercom: command from unknown TP-{0}", tpNumber);
                return;
            }

            try
            {
                var obj = JObject.Parse(json);
                string cmd = (string)obj["cmd"] ?? string.Empty;
                CrestronConsole.PrintLine("{0} INTERCOM TP-{1} cmd \"{2}\"", Ts(), tpNumber, cmd);

                if (!tp.HasVoip)
                {
                    CrestronConsole.PrintLine("Intercom: TP-{0} ({1}) has no VOIP extender - \"{2}\" ignored",
                        tpNumber, tp.Type, cmd);
                    return;
                }

                switch (cmd.ToLower())
                {
                    case "answer":
                        tp.VoipAnswer();
                        // Answering is an explicit user action mid-call: keep the idle
                        // suppression alive even if the state sig lags.
                        tp.IntercomCallActive = true;
                        break;
                    case "reject":
                        tp.VoipReject();
                        break;
                    case "hangup":
                        tp.VoipHangup();
                        break;
                    case "dnd":
                        tp.VoipDnd();
                        break;
                    case "mute":
                        tp.VoipMicMute();
                        break;
                    case "pageall":
                        tp.VoipPageAll();
                        break;
                    default:
                        CrestronConsole.PrintLine("Intercom: TP-{0} unknown cmd \"{1}\"", tpNumber, cmd);
                        return;
                }

                // The extender sig change will republish state, but DND/mute toggles are
                // the ones users tap repeatedly and expect instant feedback on, and some
                // families do not raise a sig event for their own echo. Cheap to be sure.
                SendStateTo(tp);
            }
            catch (Exception ex)
            {
                ErrorLog.Error("Intercom HandleCommand error: {0}", ex.Message);
            }
        }

        // ─── Volume (1564 in / 1565 out) ────────────────────────────────────

        /// <summary>Entry point from TouchpanelUI.SigChange for analog join 1564.</summary>
        public void HandleVolume(ushort tpNumber, ushort value)
        {
            UI.TouchpanelUI tp;
            if (!_parent.manager.touchpanelZ.TryGetValue(tpNumber, out tp) || tp == null) { return; }

            if (!tp.SetPanelSpeakerVolume(value))
            {
                CrestronConsole.PrintLine("Intercom: TP-{0} ({1}) has no speaker-volume signal - volume ignored",
                    tpNumber, tp.Type);
                return;
            }

            // Echo back so the slider tracks what the panel actually accepted rather
            // than only what the finger did.
            try
            {
                if (tp.HTML_UI && tp.UserInterface != null)
                {
                    tp.UserInterface.UShortInput[VolumeFbJoin].UShortValue = tp.GetPanelSpeakerVolume();
                }
            }
            catch (Exception ex)
            {
                ErrorLog.Error("Intercom HandleVolume echo error: {0}", ex.Message);
            }
        }
    }
}
