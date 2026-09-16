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
    ///
    /// PANEL BANKS. One EISC carries twenty panel slots, and the block layout below is what
    /// both ends agree on, so a job with more than twenty touchpanels gets more EISCs rather
    /// than a wider map. Slots run straight through the banks - panel 21 is bank 1 slot 0,
    /// which is join 1 again on the next IPID - so every join number below is relative to the
    /// bank the panel is on. Signals outside the per-panel blocks (house scene names, room
    /// status) are copied onto every bank; flat slot-indexed regions (save command, save
    /// confirm) are sized for one bank and indexed by the slot within it.
    ///
    /// The IPID list has to match on both ends, in the same order, or the two programs
    /// disagree about which panel is which.
    /// </summary>
    public class LightingScenario2Control : QuickActions.IHouseSceneBridge
    {
        private readonly ControlSystem cs;

        /// <summary>
        /// One EISC per bank of PANELS_PER_BANK panel slots, in slot order.
        ///
        /// Twenty panels fit in one EISC and that block layout is shared with the lighting
        /// processor, so a bigger job gets more EISCs rather than a bigger map. Panel 21 lands
        /// on bank 1 slot 0, which is join 1 all over again on a different IPID.
        /// </summary>
        private ThreeSeriesTcpIpEthernetIntersystemCommunications[] eiscs =
            new ThreeSeriesTcpIpEthernetIntersystemCommunications[0];

        /// <summary>Bank 0, or null if nothing was ever registered.</summary>
        private ThreeSeriesTcpIpEthernetIntersystemCommunications lightingEISC2
        {
            get { return eiscs.Length > 0 ? eiscs[0] : null; }
        }

        // Block sizes (must match Lighting4Series)
        private const int DIGITAL_BLOCK = 50;
        private const int ANALOG_BLOCK = 25;
        private const int SERIAL_BLOCK = 30;

        /// <summary>Panel slots per EISC. Shared with the lighting processor; not ours to change.</summary>
        private const int PANELS_PER_BANK = 20;

        /// <summary>How many EISCs may be banked together.</summary>
        private const int MAX_BANKS = 8;

        /// <summary>Ceiling on slots across every bank; the live figure is SlotCapacity.</summary>
        private const int MAX_PANELS = PANELS_PER_BANK * MAX_BANKS;
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

        // Global serial joins to App03: pending scene name / include-room list for the
        // next 301+idx create command
        private const int S_PENDING_SCENE_NAME = 620;
        private const int S_PENDING_INCLUDE_ROOMS = 621;

        // Global digital join base for per-panel save confirm feedback
        private const int D_SAVE_CONFIRM_BASE = 1101; // 1101-1120

        private const int MAX_HOUSE_SCENES = 10;

        // Room lights-off status base (Lighting4Series sends digital 1000+lightsID)
        private const uint D_ROOM_STATUS_BASE = 1000;

        // EISC global save command analogs (one per panel slot, joins 501-520)
        private const int A_SAVE_COMMAND_BASE = 501;

        /// <summary>
        /// Global input from the lighting processor: scene saving is turned OFF. Negative, so a
        /// program that never drives it leaves saving enabled - which is how the production
        /// program behaves.
        /// </summary>
        private const uint D_SCENE_SAVE_DISABLED = 1200;

        /// <summary>
        /// Serial join carrying the lighting capability descriptor to HTML panels, consumed by
        /// lightsScenario2.js. HTML-only reserved range (1500+), next to the shade one on 1540.
        ///
        /// A direct join rather than a CH5 contract signal: the contract is owned by the
        /// Contract Editor and regenerating it would drop anything added by hand.
        /// </summary>
        private const ushort LightingCapabilityDescriptorJoin = 1541;
        private const int HOUSE_SCENE_RECALL_CMD = 201; // value = 201 + houseSceneIndex

        private const ushort BUTTON_RELEASE_DELAY_MS = 120;

        private readonly HashSet<ushort> _subscribedPanels = new HashSet<ushort>();

        // Reusable pulse timers keyed by EISC signal number — prevents fire-and-forget CTimer leaks
        private readonly Dictionary<long, CTimer> _pulseTimers = new Dictionary<long, CTimer>();

        // Analog save-command reset timers (quick actions) — kept separate from
        // _pulseTimers because analog and digital sig numbers overlap numerically.
        private readonly Dictionary<long, CTimer> _analogResetTimers = new Dictionary<long, CTimer>();

        /// <summary>
        /// Press and release a button for one panel.
        ///
        /// Timers are keyed by bank as well as join: join 1 exists on every EISC, so keying on
        /// the number alone would let panel 21 cancel panel 1's release and leave a button
        /// stuck down.
        /// </summary>
        private void PulseBooleanInput(int slot, int offsetWithinBlock)
        {
            var eisc = BankOf(slot);
            if (eisc == null) return;

            uint sig = DigitalJoin(slot, offsetWithinBlock);
            long key = TimerKey(slot, sig);
            eisc.BooleanInput[sig].BoolValue = true;

            if (_pulseTimers.ContainsKey(key))
            {
                _pulseTimers[key].Stop();
                _pulseTimers[key].Dispose();
            }
            _pulseTimers[key] = new CTimer(o =>
            {
                eisc.BooleanInput[sig].BoolValue = false;
                _pulseTimers.Remove(key);
            }, BUTTON_RELEASE_DELAY_MS);
        }

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

        /// <summary>How many panel slots the registered banks add up to.</summary>
        private int SlotCapacity
        {
            get { return eiscs.Length * PANELS_PER_BANK; }
        }

        /// <summary>The EISC carrying a panel slot, or null if that bank does not exist.</summary>
        private ThreeSeriesTcpIpEthernetIntersystemCommunications BankOf(int slot)
        {
            if (slot < 0) return null;
            int bank = slot / PANELS_PER_BANK;
            return bank < eiscs.Length ? eiscs[bank] : null;
        }

        /// <summary>Which bank a device is, or -1 if it is not one of ours.</summary>
        private int BankIndexOf(GenericBase device)
        {
            for (int i = 0; i < eiscs.Length; i++)
            {
                if (ReferenceEquals(eiscs[i], device)) return i;
            }
            return -1;
        }

        private static int LocalSlot(int slot)
        {
            return slot % PANELS_PER_BANK;
        }

        /// <summary>Bank and join together, so two banks cannot share a timer slot.</summary>
        private static long TimerKey(int slot, uint sig)
        {
            return ((long)(slot / PANELS_PER_BANK) << 32) | sig;
        }

        // Joins are numbered within a bank, so a global slot comes back down to a local one
        // before the block arithmetic.
        private uint DigitalJoin(int slot, int offsetWithinBlock)
        {
            return (uint)(LocalSlot(slot) * DIGITAL_BLOCK + offsetWithinBlock);
        }

        private uint AnalogJoin(int slot, int offsetWithinBlock)
        {
            return (uint)(LocalSlot(slot) * ANALOG_BLOCK + offsetWithinBlock);
        }

        private uint SerialJoin(int slot, int offsetWithinBlock)
        {
            return (uint)(LocalSlot(slot) * SERIAL_BLOCK + offsetWithinBlock);
        }

        /// <summary>
        /// A flat, slot-indexed join outside the per-panel blocks (save command, save confirm).
        /// Those regions are sized for one bank, so the slot has to be the local one.
        /// </summary>
        private uint FlatJoin(int slot, int baseJoin)
        {
            return (uint)(baseJoin + LocalSlot(slot));
        }

        // Reads and writes go through these so a slot in a bank that was never registered is
        // simply inert rather than a null dereference.
        private void SetAnalogIn(int slot, int offsetWithinBlock, ushort value)
        {
            var eisc = BankOf(slot);
            if (eisc == null) return;
            eisc.UShortInput[AnalogJoin(slot, offsetWithinBlock)].UShortValue = value;
        }

        /// <summary>
        /// A global (non-block) output. Every bank carries its own copy, so bank 0 answers for
        /// all of them.
        /// </summary>
        private ushort GetGlobalAnalogOut(uint sig)
        {
            var eisc = lightingEISC2;
            return eisc == null ? (ushort)0 : eisc.UShortOutput[sig].UShortValue;
        }

        private bool GetGlobalBoolOut(uint sig)
        {
            var eisc = lightingEISC2;
            return eisc != null && eisc.BooleanOutput[sig].BoolValue;
        }

        private string GetGlobalStringOut(uint sig)
        {
            var eisc = lightingEISC2;
            return eisc == null ? string.Empty : eisc.StringOutput[sig].StringValue;
        }

        /// <summary>
        /// Turn a signal number into a *global* panel slot. The number alone is ambiguous once
        /// there is more than one bank - join 1 exists on every EISC - so which device raised
        /// it is part of the answer.
        /// </summary>
        private int GetSlotFromSignal(GenericBase device, uint sigNumber, int blockSize,
            out int offsetInBlock)
        {
            offsetInBlock = 0;
            if (sigNumber < 1) return -1;
            int bank = BankIndexOf(device);
            if (bank < 0) return -1;
            int zeroBasedSig = (int)sigNumber - 1;
            int localSlot = zeroBasedSig / blockSize;
            offsetInBlock = (zeroBasedSig % blockSize) + 1;
            if (localSlot >= PANELS_PER_BANK) return -1;
            return bank * PANELS_PER_BANK + localSlot;
        }

        // ─── Initialization ────────────────────────────────────────────────

        /// <summary>
        /// Create and register the EISC, wire up feedback handler.
        /// The IPID comes from the Lights subsystem config and must match the
        /// Lighting4Series program's lightsEISC IPID. Falls back to 0xB3.
        /// </summary>
        /// <summary>
        /// Register one EISC per twenty panel slots.
        ///
        /// A bank that fails to register is kept in the array all the same. Dropping it would
        /// shift every later bank down one and quietly hand panel 41 the signals meant for
        /// panel 21 - far worse than a dead subsystem that is loudly logged.
        /// </summary>
        public void Initialize(uint ipid, uint[] extraIpIds, string address)
        {
            if (ipid == 0)
                ipid = 0xB3;
            if (string.IsNullOrEmpty(address))
                address = "192.168.1.156";

            var ipIds = new List<uint>();
            ipIds.Add(ipid);
            if (extraIpIds != null)
            {
                foreach (uint extra in extraIpIds)
                {
                    if (extra == 0) continue;
                    if (ipIds.Contains(extra))
                    {
                        ErrorLog.Error("LightsS2: IPID 0x{0:X2} listed twice - ignoring the repeat",
                            extra);
                        continue;
                    }
                    if (ipIds.Count >= MAX_BANKS)
                    {
                        ErrorLog.Error("LightsS2: more than {0} EISC banks configured - 0x{1:X2} ignored",
                            MAX_BANKS, extra);
                        continue;
                    }
                    ipIds.Add(extra);
                }
            }

            eiscs = new ThreeSeriesTcpIpEthernetIntersystemCommunications[ipIds.Count];
            for (int i = 0; i < ipIds.Count; i++)
            {
                var bankEisc = new ThreeSeriesTcpIpEthernetIntersystemCommunications(
                    ipIds[i], address, cs);
                bankEisc.SigChange += new SigEventHandler(EISC_SigChangeHandler);
                eiscs[i] = bankEisc;

                var bankResp = bankEisc.Register();
                if (bankResp != eDeviceRegistrationUnRegistrationResponse.Success)
                {
                    ErrorLog.Error("lightingEISC2 bank {0} (0x{1:X2}) failed: {2}",
                        i, ipIds[i], bankEisc.RegistrationFailureReason);
                }
                else
                {
                    CrestronConsole.PrintLine(
                        "lightingEISC2 bank {0} (0x{1:X2}) registered for LightsScenario2, panel slots {2}-{3}",
                        i, ipIds[i], i * PANELS_PER_BANK, i * PANELS_PER_BANK + PANELS_PER_BANK - 1);
                }
            }

            if (lightingEISC2 != null)
            {
                ushort initHouseCount = GetGlobalAnalogOut(A_NUM_HOUSE_SCENES);
                if (cs.logging) CrestronConsole.PrintLine("LightsS2: EISC init — house scene count on wire = {0}", initHouseCount);
                for (int h = 0; h < MAX_HOUSE_SCENES; h++)
                {
                    string hsn = GetGlobalStringOut((uint)(S_HOUSE_SCENE_NAME_BASE + h));
                    if (!string.IsNullOrEmpty(hsn))
                        if (cs.logging) CrestronConsole.PrintLine("LightsS2: EISC init — house scene[{0}] = \"{1}\"", h, hsn);
                }
            }
        }

        /// <summary>
        /// Reads the current room lights-off status directly off the EISC wire
        /// (digital 1000+lightsID). Used to refresh the whole-house room list at
        /// display time: a "lights on" room drives this digital FALSE, which is the
        /// signal's default state, so it does NOT re-fire a SigChange event when the
        /// ACS re-registers the EISC after a program reload — leaving LightStatusText
        /// blank for exactly those rooms. Polling the wire value sidesteps that.
        /// Returns true when the room's lights are OFF.
        /// </summary>
        public bool GetRoomLightsAreOff(ushort lightsID)
        {
            if (lightingEISC2 == null || lightsID == 0) return false;
            return GetGlobalBoolOut((uint)(D_ROOM_STATUS_BASE + lightsID));
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
                if (nextSlot >= SlotCapacity)
                {
                    ErrorLog.Error(
                        "LightsS2: No slots available for TP-{0} - {1} EISC bank(s) hold {2} panels. "
                        + "Add another IPID to EISCExtraIPIDs (and to the lighting processor).",
                        tpNumber, eiscs.Length, SlotCapacity);
                    return;
                }
                panelSlotMap[tpNumber] = nextSlot;
                slotPanelMap[nextSlot] = tpNumber;
                if (cs.logging) CrestronConsole.PrintLine("LightsS2: TP-{0} assigned slot {1}", tpNumber, nextSlot);
                nextSlot++;
            }

            int slot = panelSlotMap[tpNumber];

            if (!_subscribedPanels.Contains(tpNumber))
            {
                _subscribedPanels.Add(tpNumber);

                // Subscribe scene select
                for (int i = 0; i < tp._HTMLContract.LightingScene.Length && i < MAX_SCENES; i++)
                {
                    int sceneIndex = i;
                    tp._HTMLContract.LightingScene[i].selectScene += (sender, args) =>
                    {
                        if (args.SigArgs.Sig.BoolValue && lightingEISC2 != null)
                        {
                            PulseBooleanInput(slot, D_SCENE_SELECT + sceneIndex);
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
                            PulseBooleanInput(slot, D_LOAD_ON + loadIndex);
                        }
                    };

                    tp._HTMLContract.LightingLoad[i].loadOff += (sender, args) =>
                    {
                        if (args.SigArgs.Sig.BoolValue && lightingEISC2 != null)
                        {
                            PulseBooleanInput(slot, D_LOAD_OFF + loadIndex);
                        }
                    };

                    tp._HTMLContract.LightingLoad[i].setLoadLevel += (sender, args) =>
                    {
                        if (lightingEISC2 != null)
                        {
                            SetAnalogIn(slot, A_LOAD_LEVEL + loadIndex,
                                args.SigArgs.Sig.UShortValue);
                        }
                    };
                }

                // Subscribe save command (scene save / house scene save+recall)
                tp._HTMLContract.LightingRoomList.saveCommand += (sender, args) =>
                {
                    ushort cmdValue = args.SigArgs.Sig.UShortValue;
                    if (cs.logging) CrestronConsole.PrintLine("LightsS2: saveCommand received from TP-{0} slot {1} value={2}, eisc={3}",
                        tpNumber, slot, cmdValue, lightingEISC2 != null);
                    if (lightingEISC2 != null && cmdValue > 0)
                    {
                        if (cs.logging) CrestronConsole.PrintLine("LightsS2: Writing save cmd {0} to bank {1} analog {2}",
                            cmdValue, slot / PANELS_PER_BANK, FlatJoin(slot, A_SAVE_COMMAND_BASE));
                        WriteSaveCommandAnalog(slot, cmdValue);
                    }
                };
            }

            // Push current EISC state to this panel (house scene count + names may already be set)
            if (lightingEISC2 != null)
            {
                ushort houseCount = GetGlobalAnalogOut(A_NUM_HOUSE_SCENES);
                if (cs.logging) CrestronConsole.PrintLine("LightsS2: TP-{0} init — EISC house scene count={1}", tpNumber, houseCount);
                if (houseCount > 0)
                {
                    tp._HTMLContract.LightingRoomList.numberOfHouseScenes((sig, wh) => sig.UShortValue = houseCount);
                    for (int h = 0; h < houseCount && h < MAX_HOUSE_SCENES; h++)
                    {
                        string hsName = GetGlobalStringOut((uint)(S_HOUSE_SCENE_NAME_BASE + h));
                        if (!string.IsNullOrEmpty(hsName) && h < tp._HTMLContract.LightingHouseScene.Length)
                        {
                            int hIdx = h;
                            tp._HTMLContract.LightingHouseScene[hIdx].houseSceneName((sig, wh) => sig.StringValue = hsName);
                        }
                    }
                    if (cs.logging) CrestronConsole.PrintLine("LightsS2: TP-{0} pushed {1} house scenes from EISC state", tpNumber, houseCount);
                }
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
                if (nextSlot >= SlotCapacity)
                {
                    ErrorLog.Error(
                        "LightsS2: No slots available for TSR TP-{0} - {1} EISC bank(s) hold {2} panels. "
                        + "Add another IPID to EISCExtraIPIDs (and to the lighting processor).",
                        tpNumber, eiscs.Length, SlotCapacity);
                    return;
                }
                panelSlotMap[tpNumber] = nextSlot;
                slotPanelMap[nextSlot] = tpNumber;
                if (cs.logging) CrestronConsole.PrintLine("LightsS2: TSR TP-{0} assigned slot {1}", tpNumber, nextSlot);
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
                    if (cs.logging) CrestronConsole.PrintLine("LightsS2: TSR TP-{0} resending lightsID {1} (room {2})", tpNumber, lightsID, currentRoom);
                    SendLightsID(tpNumber, lightsID);
                }
                else
                {
                    if (cs.logging) CrestronConsole.PrintLine("LightsS2: TSR TP-{0} room {1} has no lightsID", tpNumber, currentRoom);
                }
            }
            else
            {
                if (cs.logging) CrestronConsole.PrintLine("LightsS2: TSR TP-{0} has no current room (CurrentRoomNum={1})", tpNumber, currentRoom);
            }
        }

        // ─── TSR-310 Command Routing ──────────────────────────────────────

        /// <summary>
        /// Route a TSR-310 scene select button press to the EISC.
        /// </summary>
        public void TSRSceneSelect(ushort tpNumber, int sceneIndex)
        {
            if (cs.logging) CrestronConsole.PrintLine("LightsS2: TSRSceneSelect TP-{0} scene {1}, hasSlot={2}, eisc={3}",
                tpNumber, sceneIndex, panelSlotMap.ContainsKey(tpNumber), lightingEISC2 != null);
            if (!panelSlotMap.ContainsKey(tpNumber) || lightingEISC2 == null) return;
            int slot = panelSlotMap[tpNumber];
            if (cs.logging) CrestronConsole.PrintLine("LightsS2: TSR TP-{0} slot {1} → bank {2} digital {3} (scene {4})",
                tpNumber, slot, slot / PANELS_PER_BANK, DigitalJoin(slot, D_SCENE_SELECT + sceneIndex), sceneIndex);
            PulseBooleanInput(slot, D_SCENE_SELECT + sceneIndex);
        }

        /// <summary>
        /// Route a TSR-310 house scene recall button press to the EISC.
        /// </summary>
        public void TSRHouseSceneRecall(ushort tpNumber, int houseSceneIndex)
        {
            if (cs.logging) CrestronConsole.PrintLine("LightsS2: TSRHouseSceneRecall TP-{0} house scene {1}, hasSlot={2}, eisc={3}",
                tpNumber, houseSceneIndex, panelSlotMap.ContainsKey(tpNumber), lightingEISC2 != null);
            if (!panelSlotMap.ContainsKey(tpNumber) || lightingEISC2 == null) return;
            int slot = panelSlotMap[tpNumber];
            ushort cmdValue = (ushort)(HOUSE_SCENE_RECALL_CMD + houseSceneIndex);
            if (cs.logging) CrestronConsole.PrintLine("LightsS2: TSR TP-{0} slot {1} → EISC analog {2} value {3} (house scene recall {4})",
                tpNumber, slot, A_SAVE_COMMAND_BASE + slot, cmdValue, houseSceneIndex);
            WriteSaveCommandAnalog(slot, cmdValue);
        }

        // ─── IHouseSceneBridge (Quick Actions) ─────────────────────────────

        /// <summary>Raised when the house-scene count or a house-scene name changes
        /// on the EISC — QuickActionManager uses this to confirm App03 create/delete
        /// operations and re-validate stored scene bindings.</summary>
        public event Action HouseSceneMetadataChanged;

        public bool IsConfigured
        {
            get { return lightingEISC2 != null; }
        }

        public bool EiscOnline
        {
            get
            {
                if (eiscs.Length == 0) return false;
                for (int i = 0; i < eiscs.Length; i++)
                {
                    if (eiscs[i] == null || !eiscs[i].IsOnline) return false;
                }
                return true;
            }
        }

        public ushort GetHouseSceneCount()
        {
            if (lightingEISC2 == null) return 0;
            return GetGlobalAnalogOut(A_NUM_HOUSE_SCENES);
        }

        public string GetHouseSceneName(int index)
        {
            if (lightingEISC2 == null || index < 0 || index >= MAX_HOUSE_SCENES) return string.Empty;
            return GetGlobalStringOut((uint)(S_HOUSE_SCENE_NAME_BASE + index));
        }

        public bool SendHouseSceneCommand(ushort tpNumber, ushort commandValue)
        {
            if (lightingEISC2 == null) return false;
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
                CrestronConsole.PrintLine("LightsS2: no panel slots assigned, cannot send command {0}", commandValue);
                return false;
            }
            if (cs.logging) CrestronConsole.PrintLine("LightsS2: QuickAction TP-{0} slot {1} → bank {2} analog {3} value {4}",
                tpNumber, slot, slot / PANELS_PER_BANK, FlatJoin(slot, A_SAVE_COMMAND_BASE), commandValue);
            WriteSaveCommandAnalog(slot, commandValue);
            return true;
        }

        /// <summary>
        /// Write a save-command analog, then return it to 0 shortly after.
        ///
        /// The join has to be treated as a command, not a state. An EISC retains analog values
        /// and redelivers them when it registers, so a join left at its last value is replayed
        /// to App03 on every program restart and read there as a fresh button press - it once
        /// re-saved a lighting scene on every boot. Returning to 0 also means the same command
        /// value fires a change event next time it is sent.
        ///
        /// Separate timer map from _pulseTimers: analog sig numbers can collide numerically
        /// with digital pulse sig numbers.
        /// </summary>
        private void WriteSaveCommandAnalog(int slot, ushort commandValue)
        {
            var eisc = BankOf(slot);
            if (eisc == null) return;
            uint sig = FlatJoin(slot, A_SAVE_COMMAND_BASE);
            long key = TimerKey(slot, sig);
            eisc.UShortInput[sig].UShortValue = commandValue;
            if (_analogResetTimers.ContainsKey(key))
            {
                _analogResetTimers[key].Stop();
                _analogResetTimers[key].Dispose();
            }
            _analogResetTimers[key] = new CTimer(o =>
            {
                eisc.UShortInput[sig].UShortValue = 0;
                _analogResetTimers.Remove(key);
            }, 300);
        }

        public bool SendPendingSceneName(string name)
        {
            if (lightingEISC2 == null) return false;
            // Clear first so an identical value still fires a change event on App03.
            lightingEISC2.StringInput[S_PENDING_SCENE_NAME].StringValue = string.Empty;
            lightingEISC2.StringInput[S_PENDING_SCENE_NAME].StringValue = name ?? string.Empty;
            return true;
        }

        public bool SendPendingIncludeRooms(string csv)
        {
            if (lightingEISC2 == null) return false;
            // Clear first so an identical value still fires a change event on App03.
            lightingEISC2.StringInput[S_PENDING_INCLUDE_ROOMS].StringValue = string.Empty;
            lightingEISC2.StringInput[S_PENDING_INCLUDE_ROOMS].StringValue = csv ?? string.Empty;
            return true;
        }

        // ─── Room Selection ────────────────────────────────────────────────

        /// <summary>
        /// Send lightsID for a panel. Writes to that panel's slot on the EISC.
        /// </summary>
        public void SendLightsID(ushort tpNumber, ushort lightsID)
        {
            if (!panelSlotMap.ContainsKey(tpNumber))
            {
                if (cs.logging) CrestronConsole.PrintLine("LightsS2: TP-{0} has no slot, ignoring lightsID {1}", tpNumber, lightsID);
                return;
            }

            int slot = panelSlotMap[tpNumber];
            if (lightingEISC2 != null)
            {
                if (cs.logging) CrestronConsole.PrintLine("LightsS2: TP-{0} slot {1} → lightsID {2}", tpNumber, slot, lightsID);
                SetAnalogIn(slot, A_LIGHTS_ID, lightsID);
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
                        HandleAnalogFeedback(currentDevice, args.Sig.Number, args.Sig.UShortValue);
                        break;
                    case eSigEvent.BoolChange:
                        HandleBoolFeedback(currentDevice, args.Sig.Number, args.Sig.BoolValue);
                        break;
                    case eSigEvent.StringChange:
                        HandleStringFeedback(currentDevice, args.Sig.Number, args.Sig.StringValue);
                        break;
                }
            }
            catch (Exception e)
            {
                ErrorLog.Error("LightsS2 EISC error: {0}", e.Message);
            }
        }

        private void HandleAnalogFeedback(GenericBase device, uint sigNumber, ushort value)
        {
            // Global house-scene count applies to all assigned panels.
            if (sigNumber == A_NUM_HOUSE_SCENES)
            {
                if (cs.logging) CrestronConsole.PrintLine("LightsS2: EISC analog {0} (houseSceneCount) = {1}, panels={2}", sigNumber, value, panelSlotMap.Count);
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
                if (HouseSceneMetadataChanged != null) HouseSceneMetadataChanged();
                return;
            }

            int offsetInBlock;
            int slot = GetSlotFromSignal(device, sigNumber, ANALOG_BLOCK, out offsetInBlock);
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
                {
                    // Capabilities first: the page reads the descriptor as it lays out, so it
                    // has to be on the wire before the count that triggers that.
                    PushLightingCapabilities(slot, tp);
                    tp._HTMLContract.LightingRoomList.numberOfScenes((sig, wh) => sig.UShortValue = value);
                }
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

        private void HandleBoolFeedback(GenericBase device, uint sigNumber, bool value)
        {
            if (sigNumber == D_SCENE_SAVE_DISABLED)
            {
                PushLightingCapabilitiesToAllPanels(device);
                return;
            }

            // Save confirm is published per slot on global joins 1101-1120.
            if (sigNumber >= D_SAVE_CONFIRM_BASE && sigNumber < D_SAVE_CONFIRM_BASE + PANELS_PER_BANK)
            {
                // Flat region, sized for one bank: the number gives the slot within the bank
                // the pulse arrived on, not the global slot.
                int confirmBank = BankIndexOf(device);
                if (confirmBank < 0) return;
                int confirmSlot = confirmBank * PANELS_PER_BANK
                    + (int)(sigNumber - D_SAVE_CONFIRM_BASE);
                if (cs.logging) CrestronConsole.PrintLine("LightsS2: EISC digital {0} (saveConfirm) slot={1} value={2}", sigNumber, confirmSlot, value);
                if (!slotPanelMap.ContainsKey(confirmSlot)) return;

                ushort confirmTp = slotPanelMap[confirmSlot];
                if (!cs.manager.touchpanelZ.ContainsKey(confirmTp)) return;
                var tp2 = cs.manager.touchpanelZ[confirmTp];
                if (tp2.HTML_UI && tp2._HTMLContract != null)
                    tp2._HTMLContract.LightingRoomList.saveConfirm((sig, wh) => sig.BoolValue = value);
                return;
            }

            // Room lights on/off status (digital 1000+lightsID from Lighting4Series)
            if (sigNumber >= D_ROOM_STATUS_BASE && sigNumber < D_ROOM_STATUS_BASE + 100)
            {
                ushort lightsID = (ushort)(sigNumber - D_ROOM_STATUS_BASE);
                if (cs.logging) CrestronConsole.PrintLine("LightsS2: EISC digital {0} (roomStatus) lightsID={1} lightsAreOff={2}", sigNumber, lightsID, value);
                if (lightsID > 0)
                    cs.UpdateLightingStatusFromScenario2(lightsID, value);
                return;
            }

            int offsetInBlock;
            int slot = GetSlotFromSignal(device, sigNumber, DIGITAL_BLOCK, out offsetInBlock);
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

        /// <summary>
        /// Tell a panel whether lighting scenes may be saved, as a JSON descriptor.
        ///
        /// Read off the wire rather than driven by change events: false is also the unset
        /// state, so a program that never touches the join raises nothing, and a panel would
        /// otherwise never be told anything at all.
        /// </summary>
        private void PushLightingCapabilities(int slot, ACS_4Series_Template_V3.UI.TouchpanelUI tp)
        {
            var eisc = BankOf(slot);
            if (eisc == null || tp == null || tp.UserInterface == null) return;

            bool saveEnabled = !eisc.BooleanOutput[D_SCENE_SAVE_DISABLED].BoolValue;
            string json = "{\"saveEnabled\":" + (saveEnabled ? "true" : "false") + "}";
            tp.UserInterface.StringInput[LightingCapabilityDescriptorJoin].StringValue = json;
            if (cs.logging)
            {
                CrestronConsole.PrintLine("TP-{0} lightingCapabilities -> {1}", tp.Number, json);
            }
        }

        /// <summary>Re-send to every panel on one bank; the switch is global, not per panel.</summary>
        private void PushLightingCapabilitiesToAllPanels(GenericBase device)
        {
            int bank = BankIndexOf(device);
            if (bank < 0) return;

            for (int local = 0; local < PANELS_PER_BANK; local++)
            {
                int slot = bank * PANELS_PER_BANK + local;
                if (!slotPanelMap.ContainsKey(slot)) continue;
                ushort tpNumber = slotPanelMap[slot];
                if (!cs.manager.touchpanelZ.ContainsKey(tpNumber)) continue;
                var tp = cs.manager.touchpanelZ[tpNumber];
                if (!tp.HTML_UI) continue;
                PushLightingCapabilities(slot, tp);
            }
        }

        private void HandleStringFeedback(GenericBase device, uint sigNumber, string value)
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
                if (HouseSceneMetadataChanged != null) HouseSceneMetadataChanged();
                return;
            }

            int offsetInBlock;
            int slot = GetSlotFromSignal(device, sigNumber, SERIAL_BLOCK, out offsetInBlock);
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
            foreach (var timer in _pulseTimers.Values)
            {
                timer.Stop();
                timer.Dispose();
            }
            _pulseTimers.Clear();

            for (int i = 0; i < eiscs.Length; i++)
            {
                if (eiscs[i] == null) continue;
                eiscs[i].UnRegister();
                eiscs[i].Dispose();
            }
            eiscs = new ThreeSeriesTcpIpEthernetIntersystemCommunications[0];
            panelSlotMap.Clear();
            slotPanelMap.Clear();
        }
    }
}
