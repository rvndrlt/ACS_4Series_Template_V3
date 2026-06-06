using System;
using System.Collections.Generic;
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
    public class ShadesScenario2Control
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

        private const ushort BUTTON_RELEASE_DELAY_MS = 120;

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

            // Subscribe scene select
            for (int i = 0; i < tp._HTMLContract.ShadesScene.Length && i < MAX_SCENES; i++)
            {
                int sceneIndex = i;
                tp._HTMLContract.ShadesScene[i].selectScene += (sender, args) =>
                {
                    if (args.SigArgs.Sig.BoolValue && shadesEISC != null)
                    {
                        uint sig = DigitalJoin(slot, D_SCENE_SELECT + sceneIndex);
                        shadesEISC.BooleanInput[sig].BoolValue = true;
                        new CTimer(o => shadesEISC.BooleanInput[sig].BoolValue = false, BUTTON_RELEASE_DELAY_MS);
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
                        uint sig = DigitalJoin(slot, D_SHADE_OPEN + shadeIndex);
                        shadesEISC.BooleanInput[sig].BoolValue = true;
                        new CTimer(o => shadesEISC.BooleanInput[sig].BoolValue = false, BUTTON_RELEASE_DELAY_MS);
                    }
                };

                tp._HTMLContract.ShadesLoad[i].shadeStop += (sender, args) =>
                {
                    if (args.SigArgs.Sig.BoolValue && shadesEISC != null)
                    {
                        uint sig = DigitalJoin(slot, D_SHADE_STOP + shadeIndex);
                        shadesEISC.BooleanInput[sig].BoolValue = true;
                        new CTimer(o => shadesEISC.BooleanInput[sig].BoolValue = false, BUTTON_RELEASE_DELAY_MS);
                    }
                };

                tp._HTMLContract.ShadesLoad[i].shadeClose += (sender, args) =>
                {
                    if (args.SigArgs.Sig.BoolValue && shadesEISC != null)
                    {
                        uint sig = DigitalJoin(slot, D_SHADE_CLOSE + shadeIndex);
                        shadesEISC.BooleanInput[sig].BoolValue = true;
                        new CTimer(o => shadesEISC.BooleanInput[sig].BoolValue = false, BUTTON_RELEASE_DELAY_MS);
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
            int offsetInBlock;
            int slot = GetSlotFromSignal(sigNumber, SERIAL_BLOCK, out offsetInBlock);
            if (slot < 0 || !slotPanelMap.ContainsKey(slot)) return;

            ushort tpNumber = slotPanelMap[slot];
            if (!cs.manager.touchpanelZ.ContainsKey(tpNumber)) return;
            var tp = cs.manager.touchpanelZ[tpNumber];
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
            new CTimer(o =>
            {
                tp._HTMLContract.ShadesRoomList.saveConfirm((sig, wh) => sig.BoolValue = false);
            }, 200);
        }

        // ─── Cleanup ───────────────────────────────────────────────────────

        public void Dispose()
        {
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
