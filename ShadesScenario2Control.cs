using System;
using System.Collections.Generic;
using System.Linq;
using Crestron.SimplSharp;
using Crestron.SimplSharpPro;
using Crestron.SimplSharpPro.DeviceSupport;
using Crestron.SimplSharpPro.EthernetCommunication;

namespace ACS_4Series_Template_V3
{
    /// <summary>
    /// Bridges EISC 0xB4 (Lighting4Series shades) with the HTML contract for ShadesScenario2.
    /// 
    /// Each touchpanel is assigned a slot (0-based). Commands from the HTML contract are
    /// written directly to that panel's signal block on the EISC. Feedback from the EISC
    /// for each slot is pushed directly to that slot's panel's HTML contract.
    ///
    /// Signal map per panel (must match RoomShadeManager in Lighting4Series):
    ///
    /// Digital block = 70 per panel (offset = slot * 70):
    ///   offset+1-10:   Input = scene select,       Output = scene active FB
    ///   offset+11-30:  Input = shade open,         Output = (unused)
    ///   offset+31-50:  Input = shade stop
    ///   offset+51-70:  Input = shade close
    ///
    /// Analog block = 25 per panel (offset = slot * 25):
    ///   offset+1:      Input = shadesID
    ///   offset+2:      Output = numScenes
    ///   offset+3:      Output = numShades
    ///   offset+4-23:   Input = shade level set,    Output = shade level FB
    ///   offset+24:     Input = saveCommand
    ///   offset+25:     Output = numHouseScenes
    ///
    /// Serial block = 30 per panel (offset = slot * 30):
    ///   offset+1-10:   Output = scene names
    ///   offset+11-30:  Output = shade names
    /// </summary>
    public class ShadesScenario2Control : QuickActions.IHouseSceneBridge
    {
        private readonly ControlSystem cs;
        public ThreeSeriesTcpIpEthernetIntersystemCommunications shadesEISC;

        // Block sizes (must match Lighting4Series shades module)
        private const int DIGITAL_BLOCK = 70;
        private const int ANALOG_BLOCK = 25;
        private const int SERIAL_BLOCK = 30;
        private const int MAX_PANELS = 20;
        private const int MAX_SCENES = 10;
        private const int MAX_SHADES = 20;
        private const int MAX_HOUSE_SCENES = 10;

        // Offsets within each panel's digital block
        private const int D_SCENE_SELECT = 1;    // 1-10 (input)
        private const int D_SHADE_OPEN = 11;     // 11-30 (input)
        private const int D_SHADE_STOP = 31;     // 31-50 (input)
        private const int D_SHADE_CLOSE = 51;    // 51-70 (input)
        private const int D_SCENE_ACTIVE = 1;    // 1-10 (output)
        private const int D_SHADE_IS_OPEN = 11;  // 11-30 (output)
        private const int D_SHADE_IS_STOPPED = 31; // 31-50 (output)
        private const int D_SHADE_IS_CLOSED = 51;  // 51-70 (output)

        // Offsets within each panel's analog block
        private const int A_SHADES_ID = 1;       // input
        private const int A_NUM_SCENES = 2;      // output
        private const int A_NUM_SHADES = 3;      // output
        private const int A_SHADE_LEVEL = 4;     // 4-23: input = set, output = FB
        private const int A_SAVE_COMMAND = 24;   // input
        private const int A_NUM_HOUSE_SCENES = 25; // output

        // Offsets within each panel's serial block
        private const int S_SCENE_NAME = 1;      // 1-10 (output)
        private const int S_SHADE_NAME = 11;     // 11-30 (output)

        // Global serial joins for house-scene names (outside per-panel blocks)
        private const int S_HOUSE_SCENE_NAME_BASE = 601; // 601-610

        // Global serial joins to App03: pending scene name / include-room list for the
        // next 301+idx create command
        private const int S_PENDING_SCENE_NAME = 620;
        private const int S_PENDING_INCLUDE_ROOMS = 621;

        private const ushort BUTTON_RELEASE_DELAY_MS = 120;

        private readonly HashSet<ushort> _subscribedPanels = new HashSet<ushort>();

        // Reusable pulse timers keyed by EISC signal number — prevents fire-and-forget CTimer leaks
        private readonly Dictionary<uint, CTimer> _pulseTimers = new Dictionary<uint, CTimer>();
        private CTimer _saveConfirmTimer;

        // Analog save-command reset timers (quick actions) — kept separate from
        // _pulseTimers because analog and digital sig numbers overlap numerically.
        private readonly Dictionary<uint, CTimer> _analogResetTimers = new Dictionary<uint, CTimer>();

        private void PulseBooleanInput(uint sig)
        {
            if (shadesEISC == null) return;
            shadesEISC.BooleanInput[sig].BoolValue = true;

            if (_pulseTimers.ContainsKey(sig))
            {
                _pulseTimers[sig].Stop();
                _pulseTimers[sig].Dispose();
            }
            _pulseTimers[sig] = new CTimer(o =>
            {
                shadesEISC.BooleanInput[sig].BoolValue = false;
                _pulseTimers.Remove(sig);
            }, BUTTON_RELEASE_DELAY_MS);
        }

        // Maps TPNumber → slot index (0-based)
        private readonly Dictionary<ushort, int> panelSlotMap = new Dictionary<ushort, int>();
        // Reverse: slot → TPNumber
        private readonly Dictionary<int, ushort> slotPanelMap = new Dictionary<int, ushort>();
        private int nextSlot = 0;

        public ShadesScenario2Control(ControlSystem controlSystem)
        {
            this.cs = controlSystem;
        }

        // ─── Signal offset helpers ─────────────────────────────────────────

        private uint DigitalJoin(int slot, int offsetWithinBlock)
        {
            return (uint)(slot * DIGITAL_BLOCK + offsetWithinBlock);
        }

        private uint AnalogJoin(int slot, int offsetWithinBlock)
        {
            return (uint)(slot * ANALOG_BLOCK + offsetWithinBlock);
        }

        private uint SerialJoin(int slot, int offsetWithinBlock)
        {
            return (uint)(slot * SERIAL_BLOCK + offsetWithinBlock);
        }

        private int GetSlotFromSignal(uint sigNumber, int blockSize, out int offsetInBlock)
        {
            if (sigNumber < 1) { offsetInBlock = 0; return -1; }
            int zeroBasedSig = (int)sigNumber - 1;
            int slot = zeroBasedSig / blockSize;
            offsetInBlock = (zeroBasedSig % blockSize) + 1;
            if (slot >= MAX_PANELS) return -1;
            return slot;
        }

        // ─── Initialization ────────────────────────────────────────────────

        /// <summary>
        /// Create and register the EISC, wire up feedback handler.
        /// </summary>
        public void Initialize(string address)
        {
            if (string.IsNullOrEmpty(address))
                address = "192.168.1.229";

            shadesEISC = new ThreeSeriesTcpIpEthernetIntersystemCommunications(0xB4, address, cs);
            shadesEISC.SigChange += new SigEventHandler(EISC_SigChangeHandler);

            var resp = shadesEISC.Register();
            if (resp != eDeviceRegistrationUnRegistrationResponse.Success)
            {
                ErrorLog.Error("shadesEISC (0xB4) failed: {0}", shadesEISC.RegistrationFailureReason);
            }
            else
            {
                CrestronConsole.PrintLine("shadesEISC (0xB4) registered for ShadesScenario2");
            }
        }

        // ─── Panel Registration ────────────────────────────────────────────

        /// <summary>
        /// Register a panel and subscribe its HTML contract events.
        /// Assigns the panel the next available slot.
        /// </summary>
        public void SubscribeContractEvents(ushort tpNumber)
        {
            if (!cs.manager.touchpanelZ.ContainsKey(tpNumber)) return;
            var tp = cs.manager.touchpanelZ[tpNumber];
            if (!tp.HTML_UI || tp._HTMLContract == null) return;

            // Assign a slot if not already assigned
            if (!panelSlotMap.ContainsKey(tpNumber))
            {
                if (nextSlot >= MAX_PANELS)
                {
                    ErrorLog.Error("ShadesS2: No slots available for TP-{0}", tpNumber);
                    return;
                }
                panelSlotMap[tpNumber] = nextSlot;
                slotPanelMap[nextSlot] = tpNumber;
                CrestronConsole.PrintLine("ShadesS2: TP-{0} assigned slot {1}", tpNumber, nextSlot);
                nextSlot++;
            }

            int slot = panelSlotMap[tpNumber];

            // If metadata was pushed before this panel got a slot, hydrate from current EISC outputs.
            if (shadesEISC != null)
            {
                ushort houseCount = shadesEISC.UShortOutput[AnalogJoin(slot, A_NUM_HOUSE_SCENES)].UShortValue;
                tp._HTMLContract.ShadesRoomList.numberOfHouseScenes((sig, wh) => sig.UShortValue = houseCount);

                int nameCount = houseCount;
                if (nameCount > MAX_HOUSE_SCENES) nameCount = MAX_HOUSE_SCENES;
                for (int i = 0; i < nameCount; i++)
                {
                    uint join = S_HOUSE_SCENE_NAME_BASE + (uint)i;
                    string name = shadesEISC.StringOutput[join].StringValue;
                    if (i < tp._HTMLContract.ShadesHouseScene.Length)
                        tp._HTMLContract.ShadesHouseScene[i].houseSceneName((sig, wh) => sig.StringValue = name);
                }
            }

            if (!_subscribedPanels.Contains(tpNumber))
            {
                _subscribedPanels.Add(tpNumber);

                // Subscribe scene select
                for (int i = 0; i < tp._HTMLContract.ShadesScene.Length && i < MAX_SCENES; i++)
                {
                    int sceneIndex = i;
                    tp._HTMLContract.ShadesScene[i].selectScene += (sender, args) =>
                    {
                        if (args.SigArgs.Sig.BoolValue && shadesEISC != null)
                        {
                            PulseBooleanInput(DigitalJoin(slot, D_SCENE_SELECT + sceneIndex));
                        }
                    };
                }

                // Subscribe shade open/stop/close/level
                for (int i = 0; i < tp._HTMLContract.ShadesLoad.Length && i < MAX_SHADES; i++)
                {
                    int shadeIndex = i;

                    tp._HTMLContract.ShadesLoad[i].shadeOpen += (sender, args) =>
                    {
                        if (args.SigArgs.Sig.BoolValue && shadesEISC != null)
                        {
                            PulseBooleanInput(DigitalJoin(slot, D_SHADE_OPEN + shadeIndex));
                        }
                    };

                    tp._HTMLContract.ShadesLoad[i].shadeStop += (sender, args) =>
                    {
                        if (args.SigArgs.Sig.BoolValue && shadesEISC != null)
                        {
                            PulseBooleanInput(DigitalJoin(slot, D_SHADE_STOP + shadeIndex));
                        }
                    };

                    tp._HTMLContract.ShadesLoad[i].shadeClose += (sender, args) =>
                    {
                        if (args.SigArgs.Sig.BoolValue && shadesEISC != null)
                        {
                            PulseBooleanInput(DigitalJoin(slot, D_SHADE_CLOSE + shadeIndex));
                        }
                    };

                    tp._HTMLContract.ShadesLoad[i].setShadeLevel += (sender, args) =>
                    {
                        if (shadesEISC != null)
                        {
                            uint sig = AnalogJoin(slot, A_SHADE_LEVEL + shadeIndex);
                            shadesEISC.UShortInput[sig].UShortValue = args.SigArgs.Sig.UShortValue;
                        }
                    };
                }

                // Subscribe save command
                tp._HTMLContract.ShadesRoomList.saveCommand += (sender, args) =>
                {
                    if (shadesEISC != null)
                    {
                        uint sig = AnalogJoin(slot, A_SAVE_COMMAND);
                        shadesEISC.UShortInput[sig].UShortValue = args.SigArgs.Sig.UShortValue;
                    }
                };
            }
        }

        // ─── TSR-310 / dumb-panel support (SmartObject 19) ─────────────────
        //
        // ShadesScenario2 was built HTML-only: every feedback handler below returns early on
        // !tp.HTML_UI, so a TSR-310 was never fed at all and its shades page drew nothing. These
        // panels have no contract — they render shades through SmartObject 19, a Subpage
        // Reference List Horizontal (20 items) present in both TSR-310.sgd and TSW-770-DARK.sgd.
        //
        // SmartObject 19 join map, per shade i (0-based):
        //   count            -> UShortInput[3]              ("Set Number of Items")
        //   name             -> StringInput [10 + i*2 + 1]  (text-o1, text-o3, ...)
        //   level            -> StringInput [10 + i*2 + 2]  (text-o2, text-o4, ... as "NN%")
        //   open/stop/close  -> BooleanInput[4010 + i*3 + 1..3]  and the matching press outputs
        // so shade 1 owns buttons 1,2,3, shade 2 owns 4,5,6 — matching the SGD's press cues.
        //
        // THE TWO BASES REALLY DO DIFFER — don't "fix" the serial one to match the digital.
        // Sig numbers are derived from the .sgd cue indices by skipping [~BeginGroup~]/[~EndGroup~]
        // markers, while [~UNUSED~] slots DO consume a number:
        //   press1  is OutputCue4016    - 5 preceding markers = sig 4011
        //   text-o1 is InputList3Cue12  - 1 preceding marker  = sig 11
        // Writing serials at 4010+ silently goes nowhere: the labels and levels just never appear.
        private readonly HashSet<ushort> tsrPanels = new HashSet<ushort>();
        private const uint SO_SHADES = 19;
        private const int SO_STRING_BASE = 10;
        private const int SO_BOOL_BASE = 4010;

        /// <summary>
        /// Register a dumb panel (TSR-310) for SmartObject 19 shade control. Assigns a slot from
        /// the same pool the HTML panels use, then hydrates the smart object from the EISC's
        /// retained outputs — the EISC only raises SigChange on change, so whatever
        /// Lighting4Series pushed before this panel had a slot would otherwise never arrive.
        /// </summary>
        public void SubscribeTSRPanel(ushort tpNumber)
        {
            if (!cs.manager.touchpanelZ.ContainsKey(tpNumber)) return;
            var tp = cs.manager.touchpanelZ[tpNumber];
            if (tp.HTML_UI || tp.UserInterface == null) return;

            if (!panelSlotMap.ContainsKey(tpNumber))
            {
                if (nextSlot >= MAX_PANELS)
                {
                    ErrorLog.Error("ShadesS2: No slots available for TSR TP-{0}", tpNumber);
                    return;
                }
                panelSlotMap[tpNumber] = nextSlot;
                slotPanelMap[nextSlot] = tpNumber;
                CrestronConsole.PrintLine("ShadesS2: TSR TP-{0} assigned slot {1}", tpNumber, nextSlot);
                nextSlot++;
            }
            tsrPanels.Add(tpNumber);

            // UpdateEquipIDsForSubsystems runs before this, so SendShadesID found no slot and
            // bailed. Resend it now that the panel has one, then pull current state.
            ushort currentRoom = tp.CurrentRoomNum;
            if (currentRoom > 0 && cs.manager.RoomZ.ContainsKey(currentRoom))
            {
                ushort shadesID = cs.manager.RoomZ[currentRoom].ShadesID;
                if (shadesID > 0) { SendShadesID(tpNumber, shadesID); }
                else { CrestronConsole.PrintLine("ShadesS2: TSR TP-{0} room {1} has no shadesID", tpNumber, currentRoom); }
            }

            PushShadesToSmartObject(tpNumber);
        }

        /// <summary>
        /// Push the full current shade picture from the EISC's retained outputs into SmartObject
        /// 19. Safe to call repeatedly — used on registration and on every entry to the shades
        /// page, so the panel never depends on a change event arriving at the right moment.
        /// </summary>
        public void PushShadesToSmartObject(ushort tpNumber)
        {
            try
            {
                if (shadesEISC == null || !panelSlotMap.ContainsKey(tpNumber)) return;
                if (!cs.manager.touchpanelZ.ContainsKey(tpNumber)) return;
                var tp = cs.manager.touchpanelZ[tpNumber];
                if (tp.HTML_UI || tp.UserInterface == null) return;

                int slot = panelSlotMap[tpNumber];
                var so = tp.UserInterface.SmartObjects[SO_SHADES];

                ushort count = shadesEISC.UShortOutput[AnalogJoin(slot, A_NUM_SHADES)].UShortValue;
                so.UShortInput[3].UShortValue = count;

                int n = count > MAX_SHADES ? MAX_SHADES : count;
                for (int i = 0; i < n; i++)
                {
                    so.StringInput[(uint)(SO_STRING_BASE + i * 2 + 1)].StringValue =
                        shadesEISC.StringOutput[SerialJoin(slot, S_SHADE_NAME + i)].StringValue;

                    so.StringInput[(uint)(SO_STRING_BASE + i * 2 + 2)].StringValue =
                        FormatShadeLevel(shadesEISC.UShortOutput[AnalogJoin(slot, A_SHADE_LEVEL + i)].UShortValue);

                    so.BooleanInput[(uint)(SO_BOOL_BASE + i * 3 + 1)].BoolValue =
                        shadesEISC.BooleanOutput[DigitalJoin(slot, D_SHADE_IS_OPEN + i)].BoolValue;
                    so.BooleanInput[(uint)(SO_BOOL_BASE + i * 3 + 2)].BoolValue =
                        shadesEISC.BooleanOutput[DigitalJoin(slot, D_SHADE_IS_STOPPED + i)].BoolValue;
                    so.BooleanInput[(uint)(SO_BOOL_BASE + i * 3 + 3)].BoolValue =
                        shadesEISC.BooleanOutput[DigitalJoin(slot, D_SHADE_IS_CLOSED + i)].BoolValue;
                }

                CrestronConsole.PrintLine("SHADESYNC: TP-{0} slot {1} count={2} (0xB4 analog {3}) pushed to SO19",
                    tpNumber, slot, count, AnalogJoin(slot, A_NUM_SHADES));
            }
            catch (Exception ex)
            {
                ErrorLog.Error("ShadesS2 PushShadesToSmartObject TP-{0}: {1}", tpNumber, ex.Message);
                CrestronConsole.PrintLine("SHADESYNC: TP-{0} FAILED: {1}", tpNumber, ex.Message);
            }
        }

        /// <summary>Shade level analog (0-65535) as display text for the smart object serial.</summary>
        private static string FormatShadeLevel(ushort raw)
        {
            int pct = (int)((raw * 100L) / 65535L);
            return pct.ToString() + "%";
        }

        /// <summary>
        /// Route a SmartObject 19 button press. buttonNumber is 1-based over the whole list:
        /// 1,2,3 = shade 1 open/stop/close, 4,5,6 = shade 2, and so on.
        /// </summary>
        public void TSRShadeButtonPress(ushort tpNumber, ushort buttonNumber)
        {
            if (shadesEISC == null || buttonNumber < 1) return;
            if (!panelSlotMap.ContainsKey(tpNumber))
            {
                CrestronConsole.PrintLine("ShadesS2: TP-{0} press {1} but no slot assigned", tpNumber, buttonNumber);
                return;
            }

            int slot = panelSlotMap[tpNumber];
            int shadeIndex = (buttonNumber - 1) / 3;
            int action = (buttonNumber - 1) % 3;   // 0 open, 1 stop, 2 close
            if (shadeIndex >= MAX_SHADES) return;

            int offset = (action == 0) ? D_SHADE_OPEN : (action == 1) ? D_SHADE_STOP : D_SHADE_CLOSE;
            uint sig = DigitalJoin(slot, offset + shadeIndex);
            CrestronConsole.PrintLine("ShadesS2: TP-{0} slot {1} btn {2} -> shade {3} {4} (0xB4 digital {5})",
                tpNumber, slot, buttonNumber, shadeIndex + 1,
                (action == 0) ? "OPEN" : (action == 1) ? "STOP" : "CLOSE", sig);
            PulseBooleanInput(sig);
        }

        /// <summary>True when this panel is driven through SmartObject 19 rather than the contract.</summary>
        private bool IsTsrPanel(ushort tpNumber)
        {
            return tsrPanels.Contains(tpNumber);
        }

        // ─── Live feedback → SmartObject 19 ────────────────────────────────
        // These mirror the HTML branches of the three Handle*Feedback methods, writing the smart
        // object instead of the contract. Offsets are the same EISC block offsets, so the two
        // renderings stay in lockstep.

        private void TsrAnalogFeedback(UI.TouchpanelUI tp, int offsetInBlock, ushort value)
        {
            if (tp.UserInterface == null) return;
            var so = tp.UserInterface.SmartObjects[SO_SHADES];

            if (offsetInBlock == A_NUM_SHADES)
            {
                CrestronConsole.PrintLine("SHADESYNC: TP-{0} LIVE count={1} -> SO19", tp.Number, value);
                so.UShortInput[3].UShortValue = value;
                return;
            }

            // Shade level FB (offsets 4-23) → the item's second serial, as "NN%"
            if (offsetInBlock >= A_SHADE_LEVEL && offsetInBlock < A_SHADE_LEVEL + MAX_SHADES)
            {
                int i = offsetInBlock - A_SHADE_LEVEL;
                so.StringInput[(uint)(SO_STRING_BASE + i * 2 + 2)].StringValue = FormatShadeLevel(value);
            }
        }

        private void TsrBoolFeedback(UI.TouchpanelUI tp, int offsetInBlock, bool value)
        {
            if (tp.UserInterface == null) return;
            var so = tp.UserInterface.SmartObjects[SO_SHADES];

            int i;
            if (offsetInBlock >= D_SHADE_IS_OPEN && offsetInBlock < D_SHADE_IS_OPEN + MAX_SHADES)
            {
                i = offsetInBlock - D_SHADE_IS_OPEN;
                so.BooleanInput[(uint)(SO_BOOL_BASE + i * 3 + 1)].BoolValue = value;
            }
            else if (offsetInBlock >= D_SHADE_IS_STOPPED && offsetInBlock < D_SHADE_IS_STOPPED + MAX_SHADES)
            {
                i = offsetInBlock - D_SHADE_IS_STOPPED;
                so.BooleanInput[(uint)(SO_BOOL_BASE + i * 3 + 2)].BoolValue = value;
            }
            else if (offsetInBlock >= D_SHADE_IS_CLOSED && offsetInBlock < D_SHADE_IS_CLOSED + MAX_SHADES)
            {
                i = offsetInBlock - D_SHADE_IS_CLOSED;
                so.BooleanInput[(uint)(SO_BOOL_BASE + i * 3 + 3)].BoolValue = value;
            }
        }

        private void TsrStringFeedback(UI.TouchpanelUI tp, int offsetInBlock, string value)
        {
            if (tp.UserInterface == null) return;

            // Shade names (offsets 11-30) → the item's first serial
            if (offsetInBlock >= S_SHADE_NAME && offsetInBlock < S_SHADE_NAME + MAX_SHADES)
            {
                int i = offsetInBlock - S_SHADE_NAME;
                tp.UserInterface.SmartObjects[SO_SHADES]
                    .StringInput[(uint)(SO_STRING_BASE + i * 2 + 1)].StringValue = value;
            }
        }

        // ─── IHouseSceneBridge (Quick Actions) ─────────────────────────────

        /// <summary>Raised when the house-scene count or a house-scene name changes
        /// on the EISC — QuickActionManager uses this to confirm App03 create/delete
        /// operations and re-validate stored scene bindings.</summary>
        public event Action HouseSceneMetadataChanged;

        public bool IsConfigured
        {
            get { return shadesEISC != null; }
        }

        public bool EiscOnline
        {
            get { return shadesEISC != null && shadesEISC.IsOnline; }
        }

        public ushort GetHouseSceneCount()
        {
            if (shadesEISC == null) return 0;
            // Count is pushed per-panel (offset 25 in each slot's analog block); the
            // value is the same on every slot, so read the first assigned one (slot 0
            // if none assigned yet — App03 pushes all slots at boot).
            int slot = panelSlotMap.Count > 0 ? panelSlotMap.Values.First() : 0;
            return shadesEISC.UShortOutput[AnalogJoin(slot, A_NUM_HOUSE_SCENES)].UShortValue;
        }

        public string GetHouseSceneName(int index)
        {
            if (shadesEISC == null || index < 0 || index >= MAX_HOUSE_SCENES) return string.Empty;
            return shadesEISC.StringOutput[(uint)(S_HOUSE_SCENE_NAME_BASE + index)].StringValue;
        }

        public bool SendHouseSceneCommand(ushort tpNumber, ushort commandValue)
        {
            if (shadesEISC == null) return false;
            int slot;
            if (panelSlotMap.ContainsKey(tpNumber))
            {
                slot = panelSlotMap[tpNumber];
            }
            else if (panelSlotMap.Count > 0)
            {
                // Quick actions are whole-house; recall/create/delete ignore the panel's
                // room, so any assigned slot carries the command.
                slot = panelSlotMap.Values.First();
            }
            else
            {
                CrestronConsole.PrintLine("ShadesS2: no panel slots assigned, cannot send command {0}", commandValue);
                return false;
            }
            uint sig = AnalogJoin(slot, A_SAVE_COMMAND);
            CrestronConsole.PrintLine("ShadesS2: QuickAction TP-{0} slot {1} → EISC analog {2} value {3}", tpNumber, slot, sig, commandValue);
            shadesEISC.UShortInput[sig].UShortValue = commandValue;
            // Reset so the same command value re-fires a change event next time.
            if (_analogResetTimers.ContainsKey(sig))
            {
                _analogResetTimers[sig].Stop();
                _analogResetTimers[sig].Dispose();
            }
            _analogResetTimers[sig] = new CTimer(o =>
            {
                shadesEISC.UShortInput[sig].UShortValue = 0;
                _analogResetTimers.Remove(sig);
            }, 300);
            return true;
        }

        public bool SendPendingSceneName(string name)
        {
            if (shadesEISC == null) return false;
            // Clear first so an identical value still fires a change event on App03.
            shadesEISC.StringInput[S_PENDING_SCENE_NAME].StringValue = string.Empty;
            shadesEISC.StringInput[S_PENDING_SCENE_NAME].StringValue = name ?? string.Empty;
            return true;
        }

        public bool SendPendingIncludeRooms(string csv)
        {
            if (shadesEISC == null) return false;
            // Clear first so an identical value still fires a change event on App03.
            shadesEISC.StringInput[S_PENDING_INCLUDE_ROOMS].StringValue = string.Empty;
            shadesEISC.StringInput[S_PENDING_INCLUDE_ROOMS].StringValue = csv ?? string.Empty;
            return true;
        }

        // ─── Room Selection ────────────────────────────────────────────────

        /// <summary>
        /// Send shadesID for a panel. Writes to that panel's slot on the EISC.
        /// </summary>
        public void SendShadesID(ushort tpNumber, ushort shadesID)
        {
            if (!panelSlotMap.ContainsKey(tpNumber))
            {
                CrestronConsole.PrintLine("ShadesS2: TP-{0} has no slot, ignoring shadesID {1}", tpNumber, shadesID);
                return;
            }

            int slot = panelSlotMap[tpNumber];
            if (shadesEISC != null)
            {
                CrestronConsole.PrintLine("ShadesS2: TP-{0} slot {1} → shadesID {2}", tpNumber, slot, shadesID);
                shadesEISC.UShortInput[AnalogJoin(slot, A_SHADES_ID)].UShortValue = shadesID;
            }
        }

        // ─── EISC Feedback → HTML Contract ─────────────────────────────────

        private void EISC_SigChangeHandler(GenericBase currentDevice, SigEventArgs args)
        {
            try
            {
                switch (args.Event)
                {
                    case eSigEvent.UShortChange:
                        HandleAnalogFeedback(args.Sig.Number, args.Sig.UShortValue);
                        break;
                    case eSigEvent.BoolChange:
                        HandleBoolFeedback(args.Sig.Number, args.Sig.BoolValue);
                        break;
                    case eSigEvent.StringChange:
                        HandleStringFeedback(args.Sig.Number, args.Sig.StringValue);
                        break;
                }
            }
            catch (Exception e)
            {
                ErrorLog.Error("ShadesS2 EISC error: {0}", e.Message);
            }
        }

        private void HandleAnalogFeedback(uint sigNumber, ushort value)
        {
            int offsetInBlock;
            int slot = GetSlotFromSignal(sigNumber, ANALOG_BLOCK, out offsetInBlock);
            if (slot < 0 || !slotPanelMap.ContainsKey(slot)) return;

            ushort tpNumber = slotPanelMap[slot];
            if (!cs.manager.touchpanelZ.ContainsKey(tpNumber)) return;
            var tp = cs.manager.touchpanelZ[tpNumber];

            // Dumb panels (TSR-310) have no contract — they take the SmartObject 19 path.
            if (IsTsrPanel(tpNumber))
            {
                TsrAnalogFeedback(tp, offsetInBlock, value);
                return;
            }
            if (!tp.HTML_UI || tp._HTMLContract == null) return;

            if (offsetInBlock == A_NUM_SCENES)
            {
                tp._HTMLContract.ShadesRoomList.numberOfScenes((sig, wh) => sig.UShortValue = value);
                return;
            }

            if (offsetInBlock == A_NUM_SHADES)
            {
                tp._HTMLContract.ShadesRoomList.numberOfShades((sig, wh) => sig.UShortValue = value);
                return;
            }

            if (offsetInBlock == A_NUM_HOUSE_SCENES)
            {
                tp._HTMLContract.ShadesRoomList.numberOfHouseScenes((sig, wh) => sig.UShortValue = value);
                if (HouseSceneMetadataChanged != null) HouseSceneMetadataChanged();
                return;
            }

            // Shade level FB (offsets 4-23)
            if (offsetInBlock >= A_SHADE_LEVEL && offsetInBlock < A_SHADE_LEVEL + MAX_SHADES)
            {
                int shadeIndex = offsetInBlock - A_SHADE_LEVEL;
                if (shadeIndex < tp._HTMLContract.ShadesLoad.Length)
                    tp._HTMLContract.ShadesLoad[shadeIndex].shadeLevel((sig, wh) => sig.UShortValue = value);
                return;
            }
        }

        private void HandleBoolFeedback(uint sigNumber, bool value)
        {
            int offsetInBlock;
            int slot = GetSlotFromSignal(sigNumber, DIGITAL_BLOCK, out offsetInBlock);
            if (slot < 0 || !slotPanelMap.ContainsKey(slot)) return;

            ushort tpNumber = slotPanelMap[slot];
            if (!cs.manager.touchpanelZ.ContainsKey(tpNumber)) return;
            var tp = cs.manager.touchpanelZ[tpNumber];

            if (IsTsrPanel(tpNumber))
            {
                TsrBoolFeedback(tp, offsetInBlock, value);
                return;
            }
            if (!tp.HTML_UI || tp._HTMLContract == null) return;

            // Scene active (offsets 1-10)
            if (offsetInBlock >= D_SCENE_ACTIVE && offsetInBlock < D_SCENE_ACTIVE + MAX_SCENES)
            {
                int sceneIndex = offsetInBlock - D_SCENE_ACTIVE;
                if (sceneIndex < tp._HTMLContract.ShadesScene.Length)
                    tp._HTMLContract.ShadesScene[sceneIndex].sceneIsActive((sig, wh) => sig.BoolValue = value);
                return;
            }

            // Shade isOpen (offsets 11-30)
            if (offsetInBlock >= D_SHADE_IS_OPEN && offsetInBlock < D_SHADE_IS_OPEN + MAX_SHADES)
            {
                int shadeIndex = offsetInBlock - D_SHADE_IS_OPEN;
                if (shadeIndex < tp._HTMLContract.ShadesLoad.Length)
                    tp._HTMLContract.ShadesLoad[shadeIndex].shadeIsOpen((sig, wh) => sig.BoolValue = value);
                return;
            }

            // Shade isStopped (offsets 31-50)
            if (offsetInBlock >= D_SHADE_IS_STOPPED && offsetInBlock < D_SHADE_IS_STOPPED + MAX_SHADES)
            {
                int shadeIndex = offsetInBlock - D_SHADE_IS_STOPPED;
                if (shadeIndex < tp._HTMLContract.ShadesLoad.Length)
                    tp._HTMLContract.ShadesLoad[shadeIndex].shadeIsStopped((sig, wh) => sig.BoolValue = value);
                return;
            }

            // Shade isClosed (offsets 51-70)
            if (offsetInBlock >= D_SHADE_IS_CLOSED && offsetInBlock < D_SHADE_IS_CLOSED + MAX_SHADES)
            {
                int shadeIndex = offsetInBlock - D_SHADE_IS_CLOSED;
                if (shadeIndex < tp._HTMLContract.ShadesLoad.Length)
                    tp._HTMLContract.ShadesLoad[shadeIndex].shadeIsClosed((sig, wh) => sig.BoolValue = value);
                return;
            }
        }

        private void HandleStringFeedback(uint sigNumber, string value)
        {
            // House scene names (global serials 601-610) apply to all assigned panels.
            if (sigNumber >= S_HOUSE_SCENE_NAME_BASE && sigNumber < S_HOUSE_SCENE_NAME_BASE + MAX_HOUSE_SCENES)
            {
                int houseIdx = (int)(sigNumber - S_HOUSE_SCENE_NAME_BASE);
                foreach (var kv in panelSlotMap)
                {
                    ushort tpNum = kv.Key;
                    if (!cs.manager.touchpanelZ.ContainsKey(tpNum)) continue;
                    var tp2 = cs.manager.touchpanelZ[tpNum];
                    if (!tp2.HTML_UI || tp2._HTMLContract == null) continue;
                    if (houseIdx < tp2._HTMLContract.ShadesHouseScene.Length)
                        tp2._HTMLContract.ShadesHouseScene[houseIdx].houseSceneName((sig, wh) => sig.StringValue = value);
                }
                if (HouseSceneMetadataChanged != null) HouseSceneMetadataChanged();
                return;
            }

            int offsetInBlock;
            int slot = GetSlotFromSignal(sigNumber, SERIAL_BLOCK, out offsetInBlock);
            if (slot < 0 || !slotPanelMap.ContainsKey(slot)) return;

            ushort tpNumber = slotPanelMap[slot];
            if (!cs.manager.touchpanelZ.ContainsKey(tpNumber)) return;
            var tp = cs.manager.touchpanelZ[tpNumber];

            if (IsTsrPanel(tpNumber))
            {
                TsrStringFeedback(tp, offsetInBlock, value);
                return;
            }
            if (!tp.HTML_UI || tp._HTMLContract == null) return;

            // Scene names (offsets 1-10)
            if (offsetInBlock >= S_SCENE_NAME && offsetInBlock < S_SCENE_NAME + MAX_SCENES)
            {
                int sceneIndex = offsetInBlock - S_SCENE_NAME;
                if (sceneIndex < tp._HTMLContract.ShadesScene.Length)
                    tp._HTMLContract.ShadesScene[sceneIndex].sceneName((sig, wh) => sig.StringValue = value);
                return;
            }

            // Shade names (offsets 11-30)
            if (offsetInBlock >= S_SHADE_NAME && offsetInBlock < S_SHADE_NAME + MAX_SHADES)
            {
                int shadeIndex = offsetInBlock - S_SHADE_NAME;
                if (shadeIndex < tp._HTMLContract.ShadesLoad.Length)
                    tp._HTMLContract.ShadesLoad[shadeIndex].shadeName((sig, wh) => sig.StringValue = value);
                return;
            }
        }

        // ─── Save Confirm Feedback ─────────────────────────────────────────

        /// <summary>
        /// Called from Lighting4Series when save is confirmed. 
        /// Pulses the saveConfirm boolean on all panels.
        /// </summary>
        public void SendSaveConfirm(ushort tpNumber)
        {
            if (!cs.manager.touchpanelZ.ContainsKey(tpNumber)) return;
            var tp = cs.manager.touchpanelZ[tpNumber];
            if (!tp.HTML_UI || tp._HTMLContract == null) return;

            tp._HTMLContract.ShadesRoomList.saveConfirm((sig, wh) => sig.BoolValue = true);
            if (_saveConfirmTimer != null)
            {
                _saveConfirmTimer.Stop();
                _saveConfirmTimer.Dispose();
            }
            _saveConfirmTimer = new CTimer(o =>
            {
                tp._HTMLContract.ShadesRoomList.saveConfirm((sig, wh) => sig.BoolValue = false);
            }, 200);
        }

        // ─── Cleanup ───────────────────────────────────────────────────────

        public void Dispose()
        {
            foreach (var timer in _pulseTimers.Values)
            {
                timer.Stop();
                timer.Dispose();
            }
            _pulseTimers.Clear();

            if (_saveConfirmTimer != null)
            {
                _saveConfirmTimer.Stop();
                _saveConfirmTimer.Dispose();
                _saveConfirmTimer = null;
            }

            if (shadesEISC != null)
            {
                shadesEISC.UnRegister();
                shadesEISC.Dispose();
                shadesEISC = null;
            }
            panelSlotMap.Clear();
            slotPanelMap.Clear();
        }
    }
}
