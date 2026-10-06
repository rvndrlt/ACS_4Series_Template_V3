using System;
using System.Collections.Generic;
using System.Text;
using Crestron.SimplSharp;

namespace ACS_4Series_Template_V3
{
    /// <summary>
    /// Fireplace bridge: Lighting4Series' EISC joins on one side, panel joins 1600-1615 on the
    /// other. Lives with LightingScenario2Control because the fireplace joins ride the lighting
    /// EISC — a fireplace is a Lutron phantom keypad like everything else here.
    ///
    /// The EISC side is discrete joins, one per fireplace, because the EISC is local and cheap.
    /// The panel side is a single JSON catalog, because ten fireplaces on per-fireplace panel
    /// joins would be forty direct joins and a re-allocation every time one is added. Same
    /// shape as the camera catalog and the Quick Actions descriptor.
    ///
    /// Fireplaces are addressed by INDEX, not by the config's id: the index is what maps to a
    /// join, and it is the only key Lighting4Series publishes over the EISC.
    ///
    /// See html/DIRECT-JOINS.md before touching any number in here.
    /// </summary>
    public partial class LightingScenario2Control
    {
        public const int MAX_FIREPLACES = 10;

        // EISC side — must match FireplaceManager in Lighting4Series.
        private const uint FP_D_ISON_BASE = 1301;   // 1301-1310  from lighting: fireplace N is on
        private const uint FP_D_ON_BASE = 1311;     // 1311-1320  to lighting:   turn N on
        private const uint FP_D_OFF_BASE = 1321;    // 1321-1330  to lighting:   turn N off
        private const uint FP_A_COUNT = 522;        // from lighting: how many are configured
        private const uint FP_A_ROOM_BASE = 531;    // 531-540  from lighting: N's lightsID
        private const uint FP_S_NAME_BASE = 630;    // 630-639  from lighting: N's name

        // Panel side — html/DIRECT-JOINS.md, 1600-1615.
        public const ushort FireplaceCatalogJoin = 1600;    // s  C#→HTML
        public const ushort FireplaceCommandJoin = 1601;    // s  HTML→C#
        public const ushort FireplaceRevJoin = 1602;        // n  C#→HTML
        public const ushort FireplaceRepublishJoin = 1603;  // n  HTML→C#

        // TSR-310 side — html/DIRECT-JOINS.md, 1604-1608. Plain panel joins, not JSON: a dumb
        // panel has no catalog to pick from, so it controls the fireplace in its current room.
        public const ushort TsrFireplaceHoldJoin = 1604;     // b  panel→C#  press and hold 2 s; fb: on
        public const ushort TsrFireplaceOffJoin = 1605;      // b  panel→C#  off, immediately
        public const ushort TsrFireplaceConfirmJoin = 1606;  // b  panel→C#  confirm on
        public const ushort TsrFireplaceCancelJoin = 1607;   // b  panel→C#  cancel
        public const ushort TsrFireplacePopupJoin = 1608;    // b  C#→panel  confirm subpage visible
        // Serial 1604, C#→panel: this room's fireplace name, for the popup's question text.

        private const int TSR_HOLD_MS = 2000;
        private const int TSR_CONFIRM_TIMEOUT_MS = 5000;     // same as the HTML dialog

        private readonly object tsrFpLock = new object();
        private readonly Dictionary<ushort, CTimer> tsrHoldTimers = new Dictionary<ushort, CTimer>();
        private readonly Dictionary<ushort, CTimer> tsrConfirmTimers = new Dictionary<ushort, CTimer>();
        private readonly Dictionary<ushort, int> tsrPending = new Dictionary<ushort, int>();

        private readonly string[] fpName = new string[MAX_FIREPLACES];
        private readonly ushort[] fpRoom = new ushort[MAX_FIREPLACES];
        private readonly bool[] fpIsOn = new bool[MAX_FIREPLACES];
        private int fpCount;
        private ushort fpRev;

        // ── EISC → here ────────────────────────────────────────────────────

        /// <summary>True if this digital was a fireplace join and has been consumed.</summary>
        private bool HandleFireplaceBool(uint sigNumber, bool value)
        {
            if (sigNumber < FP_D_ISON_BASE || sigNumber >= FP_D_ISON_BASE + MAX_FIREPLACES)
            {
                return false;
            }
            int i = (int)(sigNumber - FP_D_ISON_BASE);
            if (fpIsOn[i] == value) return true;
            fpIsOn[i] = value;
            if (cs.logging) CrestronConsole.PrintLine("LightsS2: fireplace[{0}] is {1}", i, value ? "ON" : "off");
            PublishFireplaces();
            return true;
        }

        private bool HandleFireplaceAnalog(uint sigNumber, ushort value)
        {
            if (sigNumber == FP_A_COUNT)
            {
                fpCount = value < MAX_FIREPLACES ? value : MAX_FIREPLACES;
                PublishFireplaces();
                return true;
            }
            if (sigNumber >= FP_A_ROOM_BASE && sigNumber < FP_A_ROOM_BASE + MAX_FIREPLACES)
            {
                fpRoom[(int)(sigNumber - FP_A_ROOM_BASE)] = value;
                PublishFireplaces();
                return true;
            }
            return false;
        }

        private bool HandleFireplaceString(uint sigNumber, string value)
        {
            if (sigNumber < FP_S_NAME_BASE || sigNumber >= FP_S_NAME_BASE + MAX_FIREPLACES)
            {
                return false;
            }
            fpName[(int)(sigNumber - FP_S_NAME_BASE)] = value ?? string.Empty;
            PublishFireplaces();
            return true;
        }

        // ── here → panels ──────────────────────────────────────────────────

        private string BuildFireplaceCatalog()
        {
            var sb = new StringBuilder("[");
            for (int i = 0; i < fpCount; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append("{\"i\":").Append(i)
                  .Append(",\"name\":\"").Append(JsonEscape(fpName[i] ?? string.Empty)).Append('"')
                  .Append(",\"room\":").Append(RoomNumberForLightsID(fpRoom[i]))
                  .Append(",\"isOn\":").Append(fpIsOn[i] ? "true" : "false")
                  .Append('}');
            }
            return sb.Append(']').ToString();
        }

        /// <summary>
        /// Lighting4Series knows rooms by lightsID; the page descriptor speaks room numbers.
        /// They are equal on 30 BPT and nothing anywhere guarantees that, so translate rather
        /// than rely on it. 0 (whole-house only) passes straight through.
        /// </summary>
        private ushort RoomNumberForLightsID(ushort lightsID)
        {
            if (lightsID == 0 || cs == null || cs.manager == null || cs.manager.RoomZ == null)
            {
                return 0;
            }
            foreach (var kv in cs.manager.RoomZ)
            {
                if (kv.Value != null && kv.Value.LightsID == lightsID) return kv.Key;
            }
            return 0;
        }

        private static string JsonEscape(string s)
        {
            var sb = new StringBuilder(s.Length + 8);
            foreach (char c in s)
            {
                if (c == '"' || c == '\\') sb.Append('\\').Append(c);
                else if (c == '\n') sb.Append("\\n");
                else if (c == '\r') sb.Append("\\r");
                else if (c == '\t') sb.Append("\\t");
                else if (c < ' ') sb.Append("\\u").Append(((int)c).ToString("x4"));
                else sb.Append(c);
            }
            return sb.ToString();
        }

        /// <summary>
        /// Push the catalog to every HTML panel. Every panel, not just lighting-assigned slots:
        /// the whole-house fireplace page can be opened from any of them.
        /// </summary>
        public void PublishFireplaces()
        {
            if (cs == null || cs.manager == null || cs.manager.touchpanelZ == null) return;
            string json = BuildFireplaceCatalog();

            // A counter, because a republished identical string is not a change and therefore
            // publishes nothing — a page that opens late would see no state at all. Skips zero,
            // which is the power-on value and so not an edge either.
            fpRev = (ushort)(fpRev + 1);
            if (fpRev == 0) fpRev = 1;

            foreach (var kv in cs.manager.touchpanelZ)
            {
                var tp = kv.Value;
                if (tp == null || !tp.HTML_UI || tp.UserInterface == null) continue;
                tp.UserInterface.StringInput[FireplaceCatalogJoin].StringValue = json;
                tp.UserInterface.UShortInput[FireplaceRevJoin].UShortValue = fpRev;
            }
            if (cs.logging) CrestronConsole.PrintLine("LightsS2: fireplace catalog rev {0} -> {1}", fpRev, json);

            // A copy: panels register at startup, possibly while the EISC is already publishing.
            foreach (ushort tpNumber in new List<ushort>(tsrPanels)) UpdateTsrFireplaceFeedback(tpNumber);
        }

        /// <summary>One panel asked for the current state (its page just opened).</summary>
        public void HandleFireplaceRepublish(ushort tpNumber)
        {
            if (cs == null || cs.manager == null
                || !cs.manager.touchpanelZ.ContainsKey(tpNumber)) return;
            var tp = cs.manager.touchpanelZ[tpNumber];
            if (tp == null || !tp.HTML_UI || tp.UserInterface == null) return;

            fpRev = (ushort)(fpRev + 1);
            if (fpRev == 0) fpRev = 1;
            tp.UserInterface.StringInput[FireplaceCatalogJoin].StringValue = BuildFireplaceCatalog();
            tp.UserInterface.UShortInput[FireplaceRevJoin].UShortValue = fpRev;
            if (cs.logging) CrestronConsole.PrintLine("LightsS2: TP-{0} asked for the fireplace catalog", tpNumber);
        }

        // ── panels → here ──────────────────────────────────────────────────

        /// <summary>
        /// "&lt;index&gt;:on" / "&lt;index&gt;:off" from a panel. The hold-to-arm and the confirm
        /// dialog are entirely in the browser; nothing reaches here until the user says yes,
        /// so a dropped connection cannot half-fire a fireplace.
        /// </summary>
        public void HandleFireplaceCommand(ushort tpNumber, string payload)
        {
            if (string.IsNullOrEmpty(payload)) return;

            int colon = payload.IndexOf(':');
            if (colon <= 0)
            {
                CrestronConsole.PrintLine("LightsS2: TP-{0} bad fireplace command \"{1}\"", tpNumber, payload);
                return;
            }

            int index;
            if (!int.TryParse(payload.Substring(0, colon).Trim(), out index)
                || index < 0 || index >= MAX_FIREPLACES)
            {
                CrestronConsole.PrintLine("LightsS2: TP-{0} bad fireplace index in \"{1}\"", tpNumber, payload);
                return;
            }

            string verb = payload.Substring(colon + 1).Trim().ToLower();
            bool on;
            if (verb == "on") on = true;
            else if (verb == "off") on = false;
            else
            {
                CrestronConsole.PrintLine("LightsS2: TP-{0} bad fireplace verb in \"{1}\"", tpNumber, payload);
                return;
            }

            SendFireplace(tpNumber, index, on);
        }

        private void SendFireplace(ushort tpNumber, int index, bool on)
        {
            var eisc = lightingEISC2;
            if (eisc == null)
            {
                CrestronConsole.PrintLine("LightsS2: no lighting EISC; dropped fireplace[{0}] {1}", index, on ? "on" : "off");
                return;
            }

            // Always logged, whatever cs.logging says: a fireplace turning on is worth a line.
            CrestronConsole.PrintLine("LightsS2: TP-{0} fireplace[{1}] -> {2}", tpNumber, index, on ? "ON" : "OFF");

            // Pulse: Lighting4Series acts on the rising edge, and leaving it high would re-fire
            // on the next reconnect when the EISC re-sends its state.
            uint join = (on ? FP_D_ON_BASE : FP_D_OFF_BASE) + (uint)index;
            eisc.BooleanInput[join].BoolValue = true;
            eisc.BooleanInput[join].BoolValue = false;
        }

        // ── TSR-310 ────────────────────────────────────────────────────────
        //
        // The same rules as fireplace.js, enforced here because a SmartGraphics panel has no
        // script: hold On for 2 s to raise the confirm subpage, and nothing reaches Lutron until
        // its On is pressed. Off is immediate. The confirm closes itself after 5 s.
        //
        // Routing is by room. The fireplace is the one whose lightsID matches the panel's
        // current room, so adding a fireplace is a Lutron config entry and nothing more.

        /// <summary>
        /// The fireplace in this panel's current room, or -1. First one wins if a room ever has
        /// two; a TSR has one set of buttons, so a second would need its own joins anyway.
        /// </summary>
        private int FireplaceForPanel(ushort tpNumber)
        {
            if (cs == null || cs.manager == null || !cs.manager.touchpanelZ.ContainsKey(tpNumber)) return -1;
            ushort room = cs.manager.touchpanelZ[tpNumber].CurrentRoomNum;
            if (room == 0 || !cs.manager.RoomZ.ContainsKey(room)) return -1;
            ushort lightsID = cs.manager.RoomZ[room].LightsID;
            if (lightsID == 0) return -1;
            for (int i = 0; i < fpCount; i++)
            {
                if (fpRoom[i] == lightsID) return i;
            }
            return -1;
        }

        /// <summary>On feedback (on the hold button) and the name, for whichever room the panel is in.</summary>
        private void UpdateTsrFireplaceFeedback(ushort tpNumber)
        {
            if (!cs.manager.touchpanelZ.ContainsKey(tpNumber)) return;
            var tp = cs.manager.touchpanelZ[tpNumber];
            if (tp == null || tp.UserInterface == null) return;

            int i = FireplaceForPanel(tpNumber);
            tp.UserInterface.BooleanInput[TsrFireplaceHoldJoin].BoolValue = i >= 0 && fpIsOn[i];
            tp.UserInterface.StringInput[TsrFireplaceHoldJoin].StringValue = i >= 0 ? (fpName[i] ?? string.Empty) : string.Empty;
        }

        /// <summary>A TSR-310 fireplace button changed (joins 1604-1607). Called on press and release.</summary>
        public void HandleTsrFireplaceButton(ushort tpNumber, uint join, bool pressed)
        {
            if (join == TsrFireplaceHoldJoin)
            {
                if (pressed) StartTsrHold(tpNumber);
                else CancelTsrHold(tpNumber);
                return;
            }
            if (!pressed) return;   // the other three act on the press

            if (join == TsrFireplaceOffJoin)
            {
                // An Off while the confirm is up means they changed their mind.
                CloseTsrConfirm(tpNumber);
                int i = FireplaceForPanel(tpNumber);
                if (i < 0)
                {
                    CrestronConsole.PrintLine("LightsS2: TP-{0} fireplace Off, but its room has no fireplace", tpNumber);
                    return;
                }
                SendFireplace(tpNumber, i, false);
            }
            else if (join == TsrFireplaceConfirmJoin)
            {
                int pending;
                lock (tsrFpLock)
                {
                    if (!tsrPending.TryGetValue(tpNumber, out pending)) pending = -1;
                }
                CloseTsrConfirm(tpNumber);
                // No pending means the confirm had already timed out or been cancelled, and a
                // stale press must not light anything.
                if (pending >= 0) SendFireplace(tpNumber, pending, true);
            }
            else if (join == TsrFireplaceCancelJoin)
            {
                CloseTsrConfirm(tpNumber);
            }
        }

        private void StartTsrHold(ushort tpNumber)
        {
            lock (tsrFpLock)
            {
                StopTimer(tsrHoldTimers, tpNumber);
                tsrHoldTimers[tpNumber] = new CTimer(o => OpenTsrConfirm(tpNumber), TSR_HOLD_MS);
            }
        }

        private void CancelTsrHold(ushort tpNumber)
        {
            lock (tsrFpLock) { StopTimer(tsrHoldTimers, tpNumber); }
        }

        private void OpenTsrConfirm(ushort tpNumber)
        {
            // Resolved at the moment the hold completes, and remembered: Confirm turns on the
            // fireplace that was asked about, not whatever the room maps to a second later.
            int i = FireplaceForPanel(tpNumber);
            if (i < 0)
            {
                CrestronConsole.PrintLine("LightsS2: TP-{0} fireplace hold, but its room has no fireplace", tpNumber);
                lock (tsrFpLock) { StopTimer(tsrHoldTimers, tpNumber); }
                return;
            }

            lock (tsrFpLock)
            {
                StopTimer(tsrHoldTimers, tpNumber);
                tsrPending[tpNumber] = i;
                StopTimer(tsrConfirmTimers, tpNumber);
                tsrConfirmTimers[tpNumber] = new CTimer(o =>
                {
                    if (cs.logging) CrestronConsole.PrintLine("LightsS2: TP-{0} fireplace confirm timed out", tpNumber);
                    CloseTsrConfirm(tpNumber);
                }, TSR_CONFIRM_TIMEOUT_MS);
            }

            UpdateTsrFireplaceFeedback(tpNumber);   // the name the popup asks about
            SetTsrPopup(tpNumber, true);
            if (cs.logging) CrestronConsole.PrintLine("LightsS2: TP-{0} fireplace[{1}] confirm shown", tpNumber, i);
        }

        private void CloseTsrConfirm(ushort tpNumber)
        {
            lock (tsrFpLock)
            {
                tsrPending.Remove(tpNumber);
                StopTimer(tsrConfirmTimers, tpNumber);
            }
            SetTsrPopup(tpNumber, false);
        }

        private void SetTsrPopup(ushort tpNumber, bool visible)
        {
            if (!cs.manager.touchpanelZ.ContainsKey(tpNumber)) return;
            var tp = cs.manager.touchpanelZ[tpNumber];
            if (tp == null || tp.UserInterface == null) return;
            tp.UserInterface.BooleanInput[TsrFireplacePopupJoin].BoolValue = visible;
        }

        private static void StopTimer(Dictionary<ushort, CTimer> timers, ushort tpNumber)
        {
            CTimer t;
            if (!timers.TryGetValue(tpNumber, out t)) return;
            timers.Remove(tpNumber);
            t.Stop();
            t.Dispose();
        }

        /// <summary>
        /// The panel changed room: its feedback now describes a different fireplace, and a
        /// confirm left up would be asking about the old one.
        /// </summary>
        public void OnTsrRoomChangedFireplace(ushort tpNumber)
        {
            if (!tsrPanels.Contains(tpNumber)) return;
            CancelTsrHold(tpNumber);
            CloseTsrConfirm(tpNumber);
            UpdateTsrFireplaceFeedback(tpNumber);
        }
    }
}
