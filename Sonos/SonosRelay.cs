using System;
using System.Collections.Generic;
using Crestron.SimplSharp;
using Crestron.SimplSharpPro;
using Crestron.SimplSharpPro.EthernetCommunication;

namespace ACS_4Series_Template_V3.Sonos
{
    /// <summary>
    /// Relay between the Sonos controller in App03 (VizioTVControl) and the HTML panels.
    ///
    /// WHY THIS EXISTS AT ALL: the panels are registered to THIS program, so App03 cannot push
    /// anything to them. Same constraint that puts the doorbell popup on the 0xC0 link. There is
    /// no route that skips this program.
    ///
    /// WHAT IT DELIBERATELY DOES NOT DO: parse, interpret, cache or reassemble any of the
    /// payload. It does not know what Sonos is, what a favorite is, or how zones map to players.
    /// It demultiplexes by AudioID, stamps outbound commands with the panel's current AudioID,
    /// and forwards strings. All Sonos logic lives in App03; all of it stays there.
    ///
    /// JOINS
    ///   0xC0 to App03 (raw EISC serials, JSON payloads - NO CH5 Contract Editor anywhere):
    ///     serial 10 out  command        serial 11 out  browse request
    ///     serial 12 in   zone state     serial 13 in   browse frames
    ///
    ///   Panel (raw serial joins, same idiom as quick actions 1530/1531/1533):
    ///     1590 C#->HTML  zone state     1591 HTML->C#  command
    ///     1592 C#->HTML  browse frames  1593 HTML->C#  browse request
    ///
    /// Frames arrive pre-chunked from App03 and are forwarded VERBATIM. Only one chunker exists
    /// in the whole path, and it is the one that knows the byte budget.
    /// </summary>
    public class SonosRelay
    {
        // App03 side (0xC0)
        public const uint App03CommandJoin = 10;
        public const uint App03BrowseRequestJoin = 11;
        public const uint App03StateJoin = 12;
        public const uint App03BrowseFrameJoin = 13;

        // Panel side. 1580 is the highest join otherwise in use in this program.
        public const uint PanelStateJoin = 1590;
        public const uint PanelCommandJoin = 1591;
        public const uint PanelBrowseFrameJoin = 1592;
        public const uint PanelBrowseRequestJoin = 1593;

        private readonly ControlSystem _parent;
        private int _cmdSeq = (int)(DateTime.Now.Ticks / TimeSpan.TicksPerSecond % 1000000);

        public bool Logging { get; set; }

        public SonosRelay(ControlSystem parent)
        {
            _parent = parent;
        }

        // ─── App03 -> panels ────────────────────────────────────────────────────────────

        /// <summary>
        /// Handle a 0xC0 serial from App03. Returns false for joins this does not own so the
        /// camera popup path is untouched.
        /// </summary>
        public bool HandleApp03Sig(SigEventArgs args)
        {
            uint join = args.Sig.Number;
            if (join != App03StateJoin && join != App03BrowseFrameJoin) return false;

            try
            {
                string payload = args.Sig.StringValue;
                if (string.IsNullOrEmpty(payload)) return true; // normal at EISC link-up

                // The ONLY field this program reads. Everything else is opaque.
                ushort audioId = AudioIdFromPayload(payload);
                if (audioId == 0) return true;

                uint panelJoin = (join == App03StateJoin) ? PanelStateJoin : PanelBrowseFrameJoin;
                int sent = 0;
                foreach (var tp in PanelsForAudioId(audioId))
                {
                    try { tp.UserInterface.StringInput[panelJoin].StringValue = payload; sent++; }
                    catch (Exception ex) { Log("push TP-" + tp.Number + ": " + ex.Message); }
                }
                if (Logging) Log("audioId " + audioId + " -> " + sent + " panel(s) on join " + panelJoin);
            }
            catch (Exception ex)
            {
                // Shared handler with the doorbell popup - it must never go down.
                Log("App03 sig error: " + ex.Message);
            }
            return true;
        }

        /// <summary>
        /// Pull "audioId":N out of the payload without deserialising it.
        ///
        /// Deliberately a string scan and not a JSON parse: this program has no business
        /// knowing the payload's shape, and App03 is free to add fields without anything here
        /// needing to change. Returns 0 if absent, which drops the frame quietly.
        /// </summary>
        private static ushort AudioIdFromPayload(string payload)
        {
            const string key = "\"audioId\":";
            int i = payload.IndexOf(key, StringComparison.Ordinal);
            if (i < 0) return 0;
            i += key.Length;
            while (i < payload.Length && payload[i] == ' ') i++;
            int start = i;
            while (i < payload.Length && payload[i] >= '0' && payload[i] <= '9') i++;
            if (i == start) return 0;
            ushort v;
            return ushort.TryParse(payload.Substring(start, i - start), out v) ? v : (ushort)0;
        }

        /// <summary>HTML panels whose current room uses this AudioID.</summary>
        private List<UI.TouchpanelUI> PanelsForAudioId(ushort audioId)
        {
            var list = new List<UI.TouchpanelUI>();
            if (_parent == null || _parent.manager == null) return list;

            foreach (var kv in _parent.manager.touchpanelZ)
            {
                var tp = kv.Value;
                if (tp == null || !tp.HTML_UI || tp.UserInterface == null) continue;
                if (AudioIdForPanel(tp) == audioId) list.Add(tp);
            }
            return list;
        }

        /// <summary>The AudioID of the room a panel is currently on, or 0.</summary>
        private ushort AudioIdForPanel(UI.TouchpanelUI tp)
        {
            if (tp == null || _parent == null || _parent.manager == null) return 0;
            ushort room = tp.CurrentRoomNum;
            if (room == 0 || !_parent.manager.RoomZ.ContainsKey(room)) return 0;
            return _parent.manager.RoomZ[room].AudioID;
        }

        // ─── Panels -> App03 ────────────────────────────────────────────────────────────

        /// <summary>
        /// A panel serial arrived. Returns false for joins this does not own.
        ///
        /// The panel sends only what it wants done - {"cmd":"play"} - and THIS stamps the
        /// AudioID of the room that panel is on. The HTML never learns about AudioIDs, and a
        /// panel cannot address a room it is not looking at.
        /// </summary>
        public bool HandlePanelSig(UI.TouchpanelUI tp, SigEventArgs args)
        {
            uint join = args.Sig.Number;
            if (join != PanelCommandJoin && join != PanelBrowseRequestJoin) return false;

            try
            {
                string payload = args.Sig.StringValue;
                if (string.IsNullOrEmpty(payload)) return true;

                ushort audioId = AudioIdForPanel(tp);
                if (audioId == 0)
                {
                    Log("TP-" + (tp == null ? 0 : tp.Number) + " has no AudioID for its current room");
                    return true;
                }

                uint outJoin = (join == PanelCommandJoin) ? App03CommandJoin : App03BrowseRequestJoin;
                SendToApp03(outJoin, audioId, payload);
            }
            catch (Exception ex)
            {
                Log("panel sig error: " + ex.Message);
            }
            return true;
        }

        /// <summary>
        /// Forward to App03 with an AudioID and a sequence number injected.
        ///
        /// `seq` is clock-seeded for the same reason as the unifi relay's: a counter restarting
        /// at 1 on every App01 redeploy collides with App03's replay guard, and the command then
        /// vanishes silently - which is indistinguishable from a dead link. That cost real debug
        /// time once already.
        ///
        /// String splicing rather than parse-and-reserialise, to keep this program ignorant of
        /// the payload shape.
        /// </summary>
        private void SendToApp03(uint join, ushort audioId, string panelPayload)
        {
            var eisc = _parent.cameraPopupEISC;
            if (eisc == null || !eisc.IsOnline)
            {
                CrestronConsole.PrintLine("Sonos: App03 link (EISC 0xC0) is {0} - is VizioTVControl running?",
                    eisc == null ? "not constructed" : "offline");
                return;
            }

            string body = panelPayload.Trim();
            if (!body.StartsWith("{")) { Log("panel payload is not an object: " + body); return; }

            string inject = "{\"seq\":" + (++_cmdSeq) + ",\"audioId\":" + audioId;
            body = body.Length <= 2 ? inject + "}" : inject + "," + body.Substring(1);

            try { eisc.StringInput[join].StringValue = body; }
            catch (Exception ex) { Log("send to App03 failed: " + ex.Message); }
        }

        /// <summary>
        /// Tell App03 whether a panel is looking at a room, which is what gates transport
        /// polling over there. Called on media-page arrival and departure.
        /// </summary>
        public void SetWatching(UI.TouchpanelUI tp, bool watching)
        {
            ushort audioId = AudioIdForPanel(tp);
            if (audioId == 0) return;
            SendToApp03(App03CommandJoin, audioId,
                "{\"cmd\":\"watch\",\"arg\":\"" + (watching ? "on" : "off") + "\"}");
        }

        private void Log(string msg)
        {
            CrestronConsole.PrintLine("Sonos relay: {0}", msg);
        }
    }
}
