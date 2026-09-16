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
    ///
    ///
    /// Digital 1401-1800 (outside the per-panel blocks, bank-local): "shade i has no level",
    ///   1401 + slot*20 + shade index. Set for a shade driven by phantom keypad buttons, which
    ///   can be pressed but not positioned. False is the legacy meaning - the panel shows its
    ///   slider - so a program that never drives these changes nothing.
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
    public class ShadesScenario2Control : QuickActions.IHouseSceneBridge
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
        private ThreeSeriesTcpIpEthernetIntersystemCommunications shadesEISC
        {
            get { return eiscs.Length > 0 ? eiscs[0] : null; }
        }

        // Block sizes (must match Lighting4Series shades module)
        private const int DIGITAL_BLOCK = 70;
        private const int ANALOG_BLOCK = 25;
        private const int SERIAL_BLOCK = 30;

        /// <summary>Panel slots per EISC. Shared with the lighting processor; not ours to change.</summary>
        private const int PANELS_PER_BANK = 20;

        /// <summary>How many EISCs may be banked together.</summary>
        private const int MAX_BANKS = 8;

        /// <summary>Ceiling on slots across every bank; the live figure is SlotCapacity.</summary>
        private const int MAX_PANELS = PANELS_PER_BANK * MAX_BANKS;
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
        /// <summary>
        /// "This shade has no level", one digital per shade per panel, base 1401 - above the
        /// whole per-panel map so it is purely additive. The production Crestron-shade program
        /// never drives these, and false carries the legacy meaning: the shade has a level and
        /// the panel shows its slider.
        ///
        /// Join = 1401 + (slot within bank) * MAX_SHADES + shade index.
        /// </summary>
        private const int D_SHADE_NO_LEVEL_BASE = 1401;

        private const int S_HOUSE_SCENE_NAME_BASE = 601; // 601-610

        // Global serial joins to App03: pending scene name / include-room list for the
        // next 301+idx create command
        private const int S_PENDING_SCENE_NAME = 620;
        private const int S_PENDING_INCLUDE_ROOMS = 621;

        private const ushort BUTTON_RELEASE_DELAY_MS = 120;

        private readonly HashSet<ushort> _subscribedPanels = new HashSet<ushort>();

        // Reusable pulse timers keyed by EISC signal number — prevents fire-and-forget CTimer leaks
        private readonly Dictionary<long, CTimer> _pulseTimers = new Dictionary<long, CTimer>();
        private CTimer _saveConfirmTimer;

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

        /// <summary>Bank and join together, so two banks cannot share a timer slot.</summary>
        private static long TimerKey(int slot, uint sig)
        {
            return ((long)(slot / PANELS_PER_BANK) << 32) | sig;
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

        // Reads and writes go through these so a slot in a bank that was never registered is
        // simply inert rather than a null dereference.
        private void SetAnalogIn(int slot, int offsetWithinBlock, ushort value)
        {
            var eisc = BankOf(slot);
            if (eisc == null) return;
            eisc.UShortInput[AnalogJoin(slot, offsetWithinBlock)].UShortValue = value;
        }

        private ushort GetAnalogOut(int slot, int offsetWithinBlock)
        {
            var eisc = BankOf(slot);
            return eisc == null ? (ushort)0
                : eisc.UShortOutput[AnalogJoin(slot, offsetWithinBlock)].UShortValue;
        }

        private bool GetBoolOut(int slot, int offsetWithinBlock)
        {
            var eisc = BankOf(slot);
            return eisc != null
                && eisc.BooleanOutput[DigitalJoin(slot, offsetWithinBlock)].BoolValue;
        }

        private string GetStringOut(int slot, int offsetWithinBlock)
        {
            var eisc = BankOf(slot);
            return eisc == null ? string.Empty
                : eisc.StringOutput[SerialJoin(slot, offsetWithinBlock)].StringValue;
        }

        /// <summary>
        /// A global (non-block) output, read off the bank the given panel slot lives on. Every
        /// bank carries its own copy of these, so any one of them answers.
        /// </summary>
        private string GetGlobalStringOut(int slot, uint sig)
        {
            var eisc = BankOf(slot);
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
            if (string.IsNullOrEmpty(address))
                address = "192.168.1.229";
            if (ipid == 0)
                ipid = 0xB4;

            var ipIds = new List<uint>();
            ipIds.Add(ipid);
            if (extraIpIds != null)
            {
                foreach (uint extra in extraIpIds)
                {
                    if (extra == 0) continue;
                    if (ipIds.Contains(extra))
                    {
                        ErrorLog.Error("ShadesS2: IPID 0x{0:X2} listed twice - ignoring the repeat",
                            extra);
                        continue;
                    }
                    if (ipIds.Count >= MAX_BANKS)
                    {
                        ErrorLog.Error("ShadesS2: more than {0} EISC banks configured - 0x{1:X2} ignored",
                            MAX_BANKS, extra);
                        continue;
                    }
                    ipIds.Add(extra);
                }
            }

            eiscs = new ThreeSeriesTcpIpEthernetIntersystemCommunications[ipIds.Count];
            for (int i = 0; i < ipIds.Count; i++)
            {
                var eisc = new ThreeSeriesTcpIpEthernetIntersystemCommunications(ipIds[i], address, cs);
                eisc.SigChange += new SigEventHandler(EISC_SigChangeHandler);
                eiscs[i] = eisc;

                var resp = eisc.Register();
                if (resp != eDeviceRegistrationUnRegistrationResponse.Success)
                {
                    ErrorLog.Error("shadesEISC bank {0} (0x{1:X2}) failed: {2}",
                        i, ipIds[i], eisc.RegistrationFailureReason);
                }
                else
                {
                    CrestronConsole.PrintLine(
                        "shadesEISC bank {0} (0x{1:X2}) registered for ShadesScenario2, panel slots {2}-{3}",
                        i, ipIds[i], i * PANELS_PER_BANK, i * PANELS_PER_BANK + PANELS_PER_BANK - 1);
                }
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
                if (nextSlot >= SlotCapacity)
                {
                    ErrorLog.Error(
                        "ShadesS2: No slots available for TP-{0} - {1} EISC bank(s) hold {2} panels. "
                        + "Add another IPID to shadesEISCExtraIPIDs (and to the lighting processor).",
                        tpNumber, eiscs.Length, SlotCapacity);
                    return;
                }
                panelSlotMap[tpNumber] = nextSlot;
                slotPanelMap[nextSlot] = tpNumber;
                if (cs.logging) CrestronConsole.PrintLine("ShadesS2: TP-{0} assigned slot {1}", tpNumber, nextSlot);
                nextSlot++;
            }

            int slot = panelSlotMap[tpNumber];

            // If metadata was pushed before this panel got a slot, hydrate from current EISC outputs.
            if (shadesEISC != null)
            {
                ushort houseCount = GetAnalogOut(slot, A_NUM_HOUSE_SCENES);
                tp._HTMLContract.ShadesRoomList.numberOfHouseScenes((sig, wh) => sig.UShortValue = houseCount);

                int nameCount = houseCount;
                if (nameCount > MAX_HOUSE_SCENES) nameCount = MAX_HOUSE_SCENES;
                for (int i = 0; i < nameCount; i++)
                {
                    string name = GetGlobalStringOut(slot, S_HOUSE_SCENE_NAME_BASE + (uint)i);
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
                            PulseBooleanInput(slot, D_SCENE_SELECT + sceneIndex);
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
                            PulseBooleanInput(slot, D_SHADE_OPEN + shadeIndex);
                        }
                    };

                    tp._HTMLContract.ShadesLoad[i].shadeStop += (sender, args) =>
                    {
                        if (args.SigArgs.Sig.BoolValue && shadesEISC != null)
                        {
                            PulseBooleanInput(slot, D_SHADE_STOP + shadeIndex);
                        }
                    };

                    tp._HTMLContract.ShadesLoad[i].shadeClose += (sender, args) =>
                    {
                        if (args.SigArgs.Sig.BoolValue && shadesEISC != null)
                        {
                            PulseBooleanInput(slot, D_SHADE_CLOSE + shadeIndex);
                        }
                    };

                    tp._HTMLContract.ShadesLoad[i].setShadeLevel += (sender, args) =>
                    {
                        if (shadesEISC != null)
                        {
                            SetAnalogIn(slot, A_SHADE_LEVEL + shadeIndex,
                                args.SigArgs.Sig.UShortValue);
                        }
                    };
                }

                // Subscribe save command
                tp._HTMLContract.ShadesRoomList.saveCommand += (sender, args) =>
                {
                    if (shadesEISC != null)
                    {
                        SetAnalogIn(slot, A_SAVE_COMMAND, args.SigArgs.Sig.UShortValue);
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
                if (cs.logging) CrestronConsole.PrintLine("ShadesS2: TSR TP-{0} assigned slot {1}", tpNumber, nextSlot);
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

                ushort count = GetAnalogOut(slot, A_NUM_SHADES);
                so.UShortInput[3].UShortValue = count;

                int n = count > MAX_SHADES ? MAX_SHADES : count;
                for (int i = 0; i < n; i++)
                {
                    so.StringInput[(uint)(SO_STRING_BASE + i * 2 + 1)].StringValue =
                        GetStringOut(slot, S_SHADE_NAME + i);

                    so.StringInput[(uint)(SO_STRING_BASE + i * 2 + 2)].StringValue =
                        FormatShadeLevel(GetAnalogOut(slot, A_SHADE_LEVEL + i));

                    so.BooleanInput[(uint)(SO_BOOL_BASE + i * 3 + 1)].BoolValue =
                        GetBoolOut(slot, D_SHADE_IS_OPEN + i);
                    so.BooleanInput[(uint)(SO_BOOL_BASE + i * 3 + 2)].BoolValue =
                        GetBoolOut(slot, D_SHADE_IS_STOPPED + i);
                    so.BooleanInput[(uint)(SO_BOOL_BASE + i * 3 + 3)].BoolValue =
                        GetBoolOut(slot, D_SHADE_IS_CLOSED + i);
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
                if (cs.logging) CrestronConsole.PrintLine("ShadesS2: TP-{0} press {1} but no slot assigned", tpNumber, buttonNumber);
                return;
            }

            int slot = panelSlotMap[tpNumber];
            int shadeIndex = (buttonNumber - 1) / 3;
            int action = (buttonNumber - 1) % 3;   // 0 open, 1 stop, 2 close
            if (shadeIndex >= MAX_SHADES) return;

            int offset = (action == 0) ? D_SHADE_OPEN : (action == 1) ? D_SHADE_STOP : D_SHADE_CLOSE;
            if (cs.logging) CrestronConsole.PrintLine("ShadesS2: TP-{0} slot {1} btn {2} -> shade {3} {4} (bank {5} digital {6})",
                tpNumber, slot, buttonNumber, shadeIndex + 1,
                (action == 0) ? "OPEN" : (action == 1) ? "STOP" : "CLOSE",
                slot / PANELS_PER_BANK, DigitalJoin(slot, offset + shadeIndex));
            PulseBooleanInput(slot, offset + shadeIndex);
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
            if (shadesEISC == null) return 0;
            // Count is pushed per-panel (offset 25 in each slot's analog block); the
            // value is the same on every slot, so read the first assigned one (slot 0
            // if none assigned yet — App03 pushes all slots at boot).
            int slot = panelSlotMap.Count > 0 ? panelSlotMap.Values.First() : 0;
            return GetAnalogOut(slot, A_NUM_HOUSE_SCENES);
        }

        public string GetHouseSceneName(int index)
        {
            if (shadesEISC == null || index < 0 || index >= MAX_HOUSE_SCENES) return string.Empty;
            // Every bank carries the same house scene names, so any assigned panel answers.
            int slot = panelSlotMap.Count > 0 ? panelSlotMap.Values.First() : 0;
            return GetGlobalStringOut(slot, (uint)(S_HOUSE_SCENE_NAME_BASE + index));
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
            var eisc = BankOf(slot);
            if (eisc == null) return false;
            uint sig = AnalogJoin(slot, A_SAVE_COMMAND);
            long key = TimerKey(slot, sig);
            if (cs.logging) CrestronConsole.PrintLine("ShadesS2: QuickAction TP-{0} slot {1} → bank {2} analog {3} value {4}", tpNumber, slot, slot / PANELS_PER_BANK, sig, commandValue);
            eisc.UShortInput[sig].UShortValue = commandValue;
            // Reset so the same command value re-fires a change event next time.
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
            return true;
        }

        /// <summary>
        /// The lighting processor keeps a single pending name regardless of which bank delivered
        /// it, so bank 0 carries these and the save command that follows can land anywhere.
        /// </summary>
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
                if (cs.logging) CrestronConsole.PrintLine("ShadesS2: TP-{0} has no slot, ignoring shadesID {1}", tpNumber, shadesID);
                return;
            }

            int slot = panelSlotMap[tpNumber];
            if (shadesEISC != null)
            {
                if (cs.logging) CrestronConsole.PrintLine("ShadesS2: TP-{0} slot {1} → shadesID {2}", tpNumber, slot, shadesID);
                SetAnalogIn(slot, A_SHADES_ID, shadesID);
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
                ErrorLog.Error("ShadesS2 EISC error: {0}", e.Message);
            }
        }

        private void HandleAnalogFeedback(GenericBase device, uint sigNumber, ushort value)
        {
            int offsetInBlock;
            int slot = GetSlotFromSignal(device, sigNumber, ANALOG_BLOCK, out offsetInBlock);
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
                PushShadeCapabilities(slot, tp);
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

        private void HandleBoolFeedback(GenericBase device, uint sigNumber, bool value)
        {
            // "No level" flags sit above the per-panel blocks, in their own flat region that is
            // sized for one bank, so they are decoded before the block arithmetic gets a look.
            if (sigNumber >= D_SHADE_NO_LEVEL_BASE
                && sigNumber < D_SHADE_NO_LEVEL_BASE + PANELS_PER_BANK * MAX_SHADES)
            {
                HandleNoLevelFlag(device, sigNumber, value);
                return;
            }

            int offsetInBlock;
            int slot = GetSlotFromSignal(device, sigNumber, DIGITAL_BLOCK, out offsetInBlock);
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

        /// <summary>
        /// A no-level flag changed, so the panel needs a fresh capability descriptor.
        ///
        /// The whole descriptor is rebuilt rather than one shade patched: it is a few bytes, it
        /// arrives rarely - only when a room is re-mapped or the config reloads - and a whole
        /// picture cannot go out of step with itself the way a stream of edits can.
        /// </summary>
        private void HandleNoLevelFlag(GenericBase device, uint sigNumber, bool noLevel)
        {
            int bank = BankIndexOf(device);
            if (bank < 0) return;

            int offset = (int)(sigNumber - D_SHADE_NO_LEVEL_BASE);
            int slot = bank * PANELS_PER_BANK + offset / MAX_SHADES;

            if (!slotPanelMap.ContainsKey(slot)) return;
            ushort tpNumber = slotPanelMap[slot];
            if (!cs.manager.touchpanelZ.ContainsKey(tpNumber)) return;
            var tp = cs.manager.touchpanelZ[tpNumber];
            if (!tp.HTML_UI) return;

            PushShadeCapabilities(slot, tp);
        }

        /// <summary>
        /// Serial join carrying the JSON shade-capability descriptor to HTML panels, consumed by
        /// shadesScenario2.js. In the HTML-only reserved range (1500+), alongside the page
        /// descriptor on 1520 and Quick Actions on 1530.
        ///
        /// A direct join rather than a CH5 contract signal ON PURPOSE. The contract is owned by
        /// the Crestron Contract Editor: it regenerates both the .cse2j and the .g.cs, and would
        /// silently drop anything added to either by hand.
        /// </summary>
        private const ushort ShadeCapabilityDescriptorJoin = 1540;

        /// <summary>
        /// Tell a panel which of its shades have no level, as a JSON descriptor.
        ///
        /// Built by READING the wire rather than waiting for change events. The no-level joins
        /// sit at false for an ordinary positional shade, and false is also their unset state,
        /// so a program that never drives them - the production Crestron-shade program - raises
        /// no SigChange at all. Polling when the shade count arrives gives the right answer
        /// either way.
        ///
        /// An index absent from the list has a level, so a panel that never receives this looks
        /// exactly as it always did.
        /// </summary>
        private void PushShadeCapabilities(int slot, ACS_4Series_Template_V3.UI.TouchpanelUI tp)
        {
            var eisc = BankOf(slot);
            if (eisc == null || tp == null || tp.UserInterface == null) return;

            var sb = new System.Text.StringBuilder();
            sb.Append("{\"noLevel\":[");
            bool first = true;
            for (int i = 0; i < MAX_SHADES; i++)
            {
                uint sig = (uint)(D_SHADE_NO_LEVEL_BASE + LocalSlot(slot) * MAX_SHADES + i);
                if (!eisc.BooleanOutput[sig].BoolValue) continue;
                if (!first) sb.Append(",");
                sb.Append(i);
                first = false;
            }
            sb.Append("]}");

            string json = sb.ToString();
            tp.UserInterface.StringInput[ShadeCapabilityDescriptorJoin].StringValue = json;
            if (cs.logging)
            {
                CrestronConsole.PrintLine("TP-{0} shadeCapabilities -> {1}", tp.Number, json);
            }
        }

        private void HandleStringFeedback(GenericBase device, uint sigNumber, string value)
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
            int slot = GetSlotFromSignal(device, sigNumber, SERIAL_BLOCK, out offsetInBlock);
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
