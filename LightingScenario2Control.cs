using System;
using System.Collections.Generic;
using Crestron.SimplSharp;
using Crestron.SimplSharpPro;
using Crestron.SimplSharpPro.DeviceSupport;
using Crestron.SimplSharpPro.EthernetCommunication;

namespace ACS_4Series_Template_V3
{
    /// <summary>
    /// Bridges EISC 0xB3 (Lighting4Series) with the HTML contract for LightsScenario2.
    /// 
    /// Each touchpanel is assigned a slot (0-based). Commands from the HTML contract are
    /// written directly to that panel's signal block on the EISC. Feedback from the EISC
    /// for each slot is pushed directly to that slot's panel's HTML contract.
    ///
    /// Signal map per panel (must match RoomLightingManager in Lighting4Series):
    ///
    /// Digital block = 50 per panel (offset = slot * 50):
    ///   offset+1-10:   Input = scene select,  Output = scene active FB
    ///   offset+11-30:  Input = load on,       Output = load isOn FB
    ///   offset+31-50:  Input = load off
    ///
    /// Analog block = 25 per panel (offset = slot * 25):
    ///   offset+1:      Input = lightsID
    ///   offset+2:      Output = numScenes
    ///   offset+3:      Output = numLoads
    ///   offset+4-23:   Input = load level set, Output = load level FB
    ///
    /// Serial block = 30 per panel (offset = slot * 30):
    ///   offset+1-10:   Output = scene names
    ///   offset+11-30:  Output = load names
    /// </summary>
    public class LightingScenario2Control
    {
        private readonly ControlSystem cs;
        public ThreeSeriesTcpIpEthernetIntersystemCommunications lightingEISC2;

        // Block sizes (must match Lighting4Series)
        private const int DIGITAL_BLOCK = 50;
        private const int ANALOG_BLOCK = 25;
        private const int SERIAL_BLOCK = 30;
        private const int MAX_PANELS = 20;
        private const int MAX_SCENES = 10;
        private const int MAX_LOADS = 20;

        // Offsets within each panel's digital block
        private const int D_SCENE_SELECT = 1;   // 1-10 (input)
        private const int D_LOAD_ON = 11;       // 11-30 (input)
        private const int D_LOAD_OFF = 31;      // 31-50 (input)
        private const int D_SCENE_ACTIVE = 1;   // 1-10 (output)
        private const int D_LOAD_ISON = 11;     // 11-30 (output)

        // Offsets within each panel's analog block
        private const int A_LIGHTS_ID = 1;      // input
        private const int A_NUM_SCENES = 2;     // output
        private const int A_NUM_LOADS = 3;      // output
        private const int A_LOAD_LEVEL = 4;     // 4-23: input = set, output = FB

        // Global analog joins for house-scene metadata (not per-panel block based)
        private const int A_NUM_HOUSE_SCENES = 521;

        // Offsets within each panel's serial block
        private const int S_SCENE_NAME = 1;     // 1-10 (output)
        private const int S_LOAD_NAME = 11;     // 11-30 (output)

        // Global serial joins for house-scene names (not per-panel block based)
        private const int S_HOUSE_SCENE_NAME_BASE = 601; // 601-610

        // Global digital join base for per-panel save confirm feedback
        private const int D_SAVE_CONFIRM_BASE = 1101; // 1101-1120

        private const int MAX_HOUSE_SCENES = 10;

        // EISC global save command analogs (one per panel slot, joins 501-520)
        private const int A_SAVE_COMMAND_BASE = 501;
        private const int HOUSE_SCENE_RECALL_CMD = 201; // value = 201 + houseSceneIndex

        private const ushort BUTTON_RELEASE_DELAY_MS = 120;

        // TSR-310 direct join numbers (same on every device instance, routed by IPID)
        private const uint TSR_D_SCENE_BASE = 1101;           // 1101-1110: scene select (in) / scene active (fb)
        private const uint TSR_D_HOUSE_SCENE_BASE = 1111;     // 1111-1120: house scene recall (in)
        private const uint TSR_A_NUM_SCENES = 1101;            // number of scenes (fb)
        private const uint TSR_A_NUM_HOUSE_SCENES = 1102;      // number of house scenes (fb)
        private const uint TSR_S_SCENE_NAME_BASE = 1101;       // 1101-1110: scene names (fb)
        private const uint TSR_S_HOUSE_SCENE_NAME_BASE = 1111; // 1111-1120: house scene names (fb)
        private const uint TSR_S_ROOM_NAME = 1121;             // room name (fb)

        // Maps TPNumber → slot index (0-based). Assigned sequentially.
        private readonly Dictionary<ushort, int> panelSlotMap = new Dictionary<ushort, int>();
        // Reverse: slot → TPNumber
        private readonly Dictionary<int, ushort> slotPanelMap = new Dictionary<int, ushort>();
        private int nextSlot = 0;

        // Tracks which panels are TSR-310 (use direct joins instead of HTML contract)
        private readonly HashSet<ushort> tsrPanels = new HashSet<ushort>();

        public LightingScenario2Control(ControlSystem controlSystem)
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
                address = "192.168.1.156";

            lightingEISC2 = new ThreeSeriesTcpIpEthernetIntersystemCommunications(0xB3, address, cs);
            lightingEISC2.SigChange += new SigEventHandler(EISC_SigChangeHandler);

            var resp = lightingEISC2.Register();
            if (resp != eDeviceRegistrationUnRegistrationResponse.Success)
            {
                ErrorLog.Error("lightingEISC2 (0xB3) failed: {0}", lightingEISC2.RegistrationFailureReason);
            }
            else
            {
                CrestronConsole.PrintLine("lightingEISC2 (0xB3) registered for LightsScenario2");
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
                    ErrorLog.Error("LightsS2: No slots available for TP-{0}", tpNumber);
                    return;
                }
                panelSlotMap[tpNumber] = nextSlot;
                slotPanelMap[nextSlot] = tpNumber;
                CrestronConsole.PrintLine("LightsS2: TP-{0} assigned slot {1}", tpNumber, nextSlot);
                nextSlot++;
            }

            int slot = panelSlotMap[tpNumber];

            // Subscribe scene select
            for (int i = 0; i < tp._HTMLContract.LightingScene.Length && i < MAX_SCENES; i++)
            {
                int sceneIndex = i;
                tp._HTMLContract.LightingScene[i].selectScene += (sender, args) =>
                {
                    if (args.SigArgs.Sig.BoolValue && lightingEISC2 != null)
                    {
                        uint sig = DigitalJoin(slot, D_SCENE_SELECT + sceneIndex);
                        lightingEISC2.BooleanInput[sig].BoolValue = true;
                        new CTimer(o => lightingEISC2.BooleanInput[sig].BoolValue = false, BUTTON_RELEASE_DELAY_MS);
                    }
                };
            }

            // Subscribe load on/off/level
            for (int i = 0; i < tp._HTMLContract.LightingLoad.Length && i < MAX_LOADS; i++)
            {
                int loadIndex = i;

                tp._HTMLContract.LightingLoad[i].loadOn += (sender, args) =>
                {
                    if (args.SigArgs.Sig.BoolValue && lightingEISC2 != null)
                    {
                        uint sig = DigitalJoin(slot, D_LOAD_ON + loadIndex);
                        lightingEISC2.BooleanInput[sig].BoolValue = true;
                        new CTimer(o => lightingEISC2.BooleanInput[sig].BoolValue = false, BUTTON_RELEASE_DELAY_MS);
                    }
                };

                tp._HTMLContract.LightingLoad[i].loadOff += (sender, args) =>
                {
                    if (args.SigArgs.Sig.BoolValue && lightingEISC2 != null)
                    {
                        uint sig = DigitalJoin(slot, D_LOAD_OFF + loadIndex);
                        lightingEISC2.BooleanInput[sig].BoolValue = true;
                        new CTimer(o => lightingEISC2.BooleanInput[sig].BoolValue = false, BUTTON_RELEASE_DELAY_MS);
                    }
                };

                tp._HTMLContract.LightingLoad[i].setLoadLevel += (sender, args) =>
                {
                    if (lightingEISC2 != null)
                    {
                        uint sig = AnalogJoin(slot, A_LOAD_LEVEL + loadIndex);
                        lightingEISC2.UShortInput[sig].UShortValue = args.SigArgs.Sig.UShortValue;
                    }
                };
            }
        }

        // ─── TSR-310 Panel Registration ───────────────────────────────────

        /// <summary>
        /// Register a TSR-310 panel for direct-join lighting control.
        /// Assigns the panel the next available slot (shared pool with HTML panels).
        /// </summary>
        public void SubscribeTSRPanel(ushort tpNumber)
        {
            if (!cs.manager.touchpanelZ.ContainsKey(tpNumber)) return;

            if (!panelSlotMap.ContainsKey(tpNumber))
            {
                if (nextSlot >= MAX_PANELS)
                {
                    ErrorLog.Error("LightsS2: No slots available for TSR TP-{0}", tpNumber);
                    return;
                }
                panelSlotMap[tpNumber] = nextSlot;
                slotPanelMap[nextSlot] = tpNumber;
                CrestronConsole.PrintLine("LightsS2: TSR TP-{0} assigned slot {1}", tpNumber, nextSlot);
                nextSlot++;
            }
            tsrPanels.Add(tpNumber);

            // Send lightsID now — UpdateEquipIDsForSubsystems runs before this method,
            // so SendLightsID would have failed (no slot yet). Resend it.
            var tp = cs.manager.touchpanelZ[tpNumber];
            ushort currentRoom = tp.CurrentRoomNum;
            if (currentRoom > 0 && cs.manager.RoomZ.ContainsKey(currentRoom))
            {
                ushort lightsID = cs.manager.RoomZ[currentRoom].LightsID;
                if (lightsID > 0)
                {
                    CrestronConsole.PrintLine("LightsS2: TSR TP-{0} resending lightsID {1} (room {2})", tpNumber, lightsID, currentRoom);
                    SendLightsID(tpNumber, lightsID);
                }
                else
                {
                    CrestronConsole.PrintLine("LightsS2: TSR TP-{0} room {1} has no lightsID", tpNumber, currentRoom);
                }
            }
            else
            {
                CrestronConsole.PrintLine("LightsS2: TSR TP-{0} has no current room (CurrentRoomNum={1})", tpNumber, currentRoom);
            }
        }

        // ─── TSR-310 Command Routing ──────────────────────────────────────

        /// <summary>
        /// Route a TSR-310 scene select button press to the EISC.
        /// </summary>
        public void TSRSceneSelect(ushort tpNumber, int sceneIndex)
        {
            CrestronConsole.PrintLine("LightsS2: TSRSceneSelect TP-{0} scene {1}, hasSlot={2}, eisc={3}",
                tpNumber, sceneIndex, panelSlotMap.ContainsKey(tpNumber), lightingEISC2 != null);
            if (!panelSlotMap.ContainsKey(tpNumber) || lightingEISC2 == null) return;
            int slot = panelSlotMap[tpNumber];
            uint sig = DigitalJoin(slot, D_SCENE_SELECT + sceneIndex);
            CrestronConsole.PrintLine("LightsS2: TSR TP-{0} slot {1} → EISC digital {2} (scene {3})", tpNumber, slot, sig, sceneIndex);
            lightingEISC2.BooleanInput[sig].BoolValue = true;
            new CTimer(o => lightingEISC2.BooleanInput[sig].BoolValue = false, BUTTON_RELEASE_DELAY_MS);
        }

        /// <summary>
        /// Route a TSR-310 house scene recall button press to the EISC.
        /// </summary>
        public void TSRHouseSceneRecall(ushort tpNumber, int houseSceneIndex)
        {
            CrestronConsole.PrintLine("LightsS2: TSRHouseSceneRecall TP-{0} house scene {1}, hasSlot={2}, eisc={3}",
                tpNumber, houseSceneIndex, panelSlotMap.ContainsKey(tpNumber), lightingEISC2 != null);
            if (!panelSlotMap.ContainsKey(tpNumber) || lightingEISC2 == null) return;
            int slot = panelSlotMap[tpNumber];
            ushort cmdValue = (ushort)(HOUSE_SCENE_RECALL_CMD + houseSceneIndex);
            CrestronConsole.PrintLine("LightsS2: TSR TP-{0} slot {1} → EISC analog {2} value {3} (house scene recall {4})",
                tpNumber, slot, A_SAVE_COMMAND_BASE + slot, cmdValue, houseSceneIndex);
            lightingEISC2.UShortInput[(uint)(A_SAVE_COMMAND_BASE + slot)].UShortValue = cmdValue;
        }

        // ─── Room Selection ────────────────────────────────────────────────

        /// <summary>
        /// Send lightsID for a panel. Writes to that panel's slot on the EISC.
        /// </summary>
        public void SendLightsID(ushort tpNumber, ushort lightsID)
        {
            if (!panelSlotMap.ContainsKey(tpNumber))
            {
                CrestronConsole.PrintLine("LightsS2: TP-{0} has no slot, ignoring lightsID {1}", tpNumber, lightsID);
                return;
            }

            int slot = panelSlotMap[tpNumber];
            if (lightingEISC2 != null)
            {
                CrestronConsole.PrintLine("LightsS2: TP-{0} slot {1} → lightsID {2}", tpNumber, slot, lightsID);
                lightingEISC2.UShortInput[AnalogJoin(slot, A_LIGHTS_ID)].UShortValue = lightsID;
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
                ErrorLog.Error("LightsS2 EISC error: {0}", e.Message);
            }
        }

        private void HandleAnalogFeedback(uint sigNumber, ushort value)
        {
            // Global house-scene count applies to all assigned panels.
            if (sigNumber == A_NUM_HOUSE_SCENES)
            {
                foreach (var kv in panelSlotMap)
                {
                    ushort tpNum = kv.Key;
                    if (!cs.manager.touchpanelZ.ContainsKey(tpNum)) continue;
                    var tp2 = cs.manager.touchpanelZ[tpNum];
                    if (tp2.HTML_UI && tp2._HTMLContract != null)
                        tp2._HTMLContract.LightingRoomList.numberOfHouseScenes((sig, wh) => sig.UShortValue = value);
                    else if (tsrPanels.Contains(tpNum))
                        tp2.UserInterface.UShortInput[TSR_A_NUM_HOUSE_SCENES].UShortValue = value;
                }
                return;
            }

            int offsetInBlock;
            int slot = GetSlotFromSignal(sigNumber, ANALOG_BLOCK, out offsetInBlock);
            if (slot < 0 || !slotPanelMap.ContainsKey(slot)) return;

            ushort tpNumber = slotPanelMap[slot];
            if (!cs.manager.touchpanelZ.ContainsKey(tpNumber)) return;
            var tp = cs.manager.touchpanelZ[tpNumber];
            bool isHTML = tp.HTML_UI && tp._HTMLContract != null;
            bool isTSR = tsrPanels.Contains(tpNumber);
            if (!isHTML && !isTSR) return;

            if (offsetInBlock == A_NUM_SCENES)
            {
                if (isHTML)
                    tp._HTMLContract.LightingRoomList.numberOfScenes((sig, wh) => sig.UShortValue = value);
                else if (isTSR)
                    tp.UserInterface.UShortInput[TSR_A_NUM_SCENES].UShortValue = value;
                return;
            }

            if (offsetInBlock == A_NUM_LOADS)
            {
                if (isHTML)
                    tp._HTMLContract.LightingRoomList.numberOfLoads((sig, wh) => sig.UShortValue = value);
                return;
            }

            // Load level FB (offsets 4-23) - HTML only for now
            if (offsetInBlock >= A_LOAD_LEVEL && offsetInBlock < A_LOAD_LEVEL + MAX_LOADS)
            {
                if (isHTML)
                {
                    int loadIndex = offsetInBlock - A_LOAD_LEVEL;
                    if (loadIndex < tp._HTMLContract.LightingLoad.Length)
                        tp._HTMLContract.LightingLoad[loadIndex].loadLevel((sig, wh) => sig.UShortValue = value);
                }
                return;
            }
        }

        private void HandleBoolFeedback(uint sigNumber, bool value)
        {
            // Save confirm is published per slot on global joins 1101-1120.
            if (sigNumber >= D_SAVE_CONFIRM_BASE && sigNumber < D_SAVE_CONFIRM_BASE + MAX_PANELS)
            {
                int confirmSlot = (int)(sigNumber - D_SAVE_CONFIRM_BASE);
                if (!slotPanelMap.ContainsKey(confirmSlot)) return;

                ushort confirmTp = slotPanelMap[confirmSlot];
                if (!cs.manager.touchpanelZ.ContainsKey(confirmTp)) return;
                var tp2 = cs.manager.touchpanelZ[confirmTp];
                if (tp2.HTML_UI && tp2._HTMLContract != null)
                    tp2._HTMLContract.LightingRoomList.saveConfirm((sig, wh) => sig.BoolValue = value);
                return;
            }

            int offsetInBlock;
            int slot = GetSlotFromSignal(sigNumber, DIGITAL_BLOCK, out offsetInBlock);
            if (slot < 0 || !slotPanelMap.ContainsKey(slot)) return;

            ushort tpNumber = slotPanelMap[slot];
            if (!cs.manager.touchpanelZ.ContainsKey(tpNumber)) return;
            var tp = cs.manager.touchpanelZ[tpNumber];
            bool isHTML = tp.HTML_UI && tp._HTMLContract != null;
            bool isTSR = tsrPanels.Contains(tpNumber);
            if (!isHTML && !isTSR) return;

            // Scene active (offsets 1-10)
            if (offsetInBlock >= D_SCENE_ACTIVE && offsetInBlock < D_SCENE_ACTIVE + MAX_SCENES)
            {
                int sceneIndex = offsetInBlock - D_SCENE_ACTIVE;
                if (isHTML && sceneIndex < tp._HTMLContract.LightingScene.Length)
                    tp._HTMLContract.LightingScene[sceneIndex].sceneIsActive((sig, wh) => sig.BoolValue = value);
                else if (isTSR)
                    tp.UserInterface.BooleanInput[(ushort)(TSR_D_SCENE_BASE + sceneIndex)].BoolValue = value;
                return;
            }

            // Load isOn (offsets 11-30) - HTML only for now
            if (offsetInBlock >= D_LOAD_ISON && offsetInBlock < D_LOAD_ISON + MAX_LOADS)
            {
                if (isHTML)
                {
                    int loadIndex = offsetInBlock - D_LOAD_ISON;
                    if (loadIndex < tp._HTMLContract.LightingLoad.Length)
                        tp._HTMLContract.LightingLoad[loadIndex].loadIsOn((sig, wh) => sig.BoolValue = value);
                }
                return;
            }
        }

        private void HandleStringFeedback(uint sigNumber, string value)
        {
            // Global house-scene names (601-610) apply to all assigned panels.
            if (sigNumber >= S_HOUSE_SCENE_NAME_BASE && sigNumber < S_HOUSE_SCENE_NAME_BASE + MAX_HOUSE_SCENES)
            {
                int houseIdx = (int)(sigNumber - S_HOUSE_SCENE_NAME_BASE);
                foreach (var kv in panelSlotMap)
                {
                    ushort tpNum = kv.Key;
                    if (!cs.manager.touchpanelZ.ContainsKey(tpNum)) continue;
                    var tp2 = cs.manager.touchpanelZ[tpNum];
                    if (tp2.HTML_UI && tp2._HTMLContract != null)
                    {
                        if (houseIdx < tp2._HTMLContract.LightingHouseScene.Length)
                            tp2._HTMLContract.LightingHouseScene[houseIdx].houseSceneName((sig, wh) => sig.StringValue = value);
                    }
                    else if (tsrPanels.Contains(tpNum))
                    {
                        tp2.UserInterface.StringInput[(ushort)(TSR_S_HOUSE_SCENE_NAME_BASE + houseIdx)].StringValue = value;
                    }
                }
                return;
            }

            int offsetInBlock;
            int slot = GetSlotFromSignal(sigNumber, SERIAL_BLOCK, out offsetInBlock);
            if (slot < 0 || !slotPanelMap.ContainsKey(slot)) return;

            ushort tpNumber = slotPanelMap[slot];
            if (!cs.manager.touchpanelZ.ContainsKey(tpNumber)) return;
            var tp = cs.manager.touchpanelZ[tpNumber];
            bool isHTML = tp.HTML_UI && tp._HTMLContract != null;
            bool isTSR = tsrPanels.Contains(tpNumber);
            if (!isHTML && !isTSR) return;

            // Scene names (offsets 1-10)
            if (offsetInBlock >= S_SCENE_NAME && offsetInBlock < S_SCENE_NAME + MAX_SCENES)
            {
                int sceneIndex = offsetInBlock - S_SCENE_NAME;
                if (isHTML && sceneIndex < tp._HTMLContract.LightingScene.Length)
                    tp._HTMLContract.LightingScene[sceneIndex].sceneName((sig, wh) => sig.StringValue = value);
                else if (isTSR)
                    tp.UserInterface.StringInput[(ushort)(TSR_S_SCENE_NAME_BASE + sceneIndex)].StringValue = value;
                return;
            }

            // Load names (offsets 11-30) - HTML only for now
            if (offsetInBlock >= S_LOAD_NAME && offsetInBlock < S_LOAD_NAME + MAX_LOADS)
            {
                if (isHTML)
                {
                    int loadIndex = offsetInBlock - S_LOAD_NAME;
                    if (loadIndex < tp._HTMLContract.LightingLoad.Length)
                        tp._HTMLContract.LightingLoad[loadIndex].loadName((sig, wh) => sig.StringValue = value);
                }
                return;
            }
        }

        // ─── Cleanup ───────────────────────────────────────────────────────

        public void Dispose()
        {
            if (lightingEISC2 != null)
            {
                lightingEISC2.UnRegister();
                lightingEISC2.Dispose();
                lightingEISC2 = null;
            }
            panelSlotMap.Clear();
            slotPanelMap.Clear();
        }
    }
}
