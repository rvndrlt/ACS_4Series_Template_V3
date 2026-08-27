using System;
using System.Collections.Generic;
using Crestron.SimplSharp;
using Crestron.SimplSharpPro;
using Crestron.SimplSharpPro.DeviceSupport;
using Crestron.SimplSharpPro.EthernetCommunication;

namespace ACS_4Series_Template_V3
{
    public partial class ControlSystem
    {
        #region Security Zone Tracking

        // Track security zone state from EISC (1-based zone number, arrays are 0-based so index = zoneNumber - 1)
        private const int MaxSecurityZones = 200;
        private bool[] _securityZoneVisible = new bool[MaxSecurityZones];
        private bool[] _securityZoneBypassed = new bool[MaxSecurityZones];
        private string[] _securityZoneName = new string[MaxSecurityZones];

        // Ordered list of visible zone numbers (1-based EISC zone numbers)
        public List<ushort> VisibleSecurityZones { get; private set; } = new List<ushort>();

        /// <summary>
        /// Rebuilds the compacted visible security zone list and pushes all data to HTML panels.
        /// Smart Graphics panels are not affected.
        /// </summary>
        private void RebuildSecurityZoneListForHTML()
        {
            VisibleSecurityZones.Clear();
            for (ushort z = 0; z < MaxSecurityZones; z++)
            {
                if (_securityZoneVisible[z])
                    VisibleSecurityZones.Add((ushort)(z + 1)); // store 1-based zone number
            }

            ushort visibleCount = (ushort)VisibleSecurityZones.Count;

            foreach (var tp in manager.touchpanelZ)
            {
                if (!tp.Value.HTML_UI) continue;

                tp.Value._HTMLContract.NumberOfSecurityZones.NumberOfSecurityZones(
                    (sig, wh) => sig.UShortValue = visibleCount);

                for (int slot = 0; slot < visibleCount && slot < tp.Value._HTMLContract.SecurityZone.Length; slot++)
                {
                    ushort eiscZone = VisibleSecurityZones[slot]; // 1-based
                    string name = _securityZoneName[eiscZone - 1] ?? "";
                    bool bypassed = _securityZoneBypassed[eiscZone - 1];

                    int capturedSlot = slot;
                    string capturedName = name;
                    bool capturedBypassed = bypassed;

                    tp.Value._HTMLContract.SecurityZone[capturedSlot].ZoneName(
                        (sig, wh) => sig.StringValue = capturedName);
                    tp.Value._HTMLContract.SecurityZone[capturedSlot].ZoneBypassed(
                        (sig, wh) => sig.BoolValue = capturedBypassed);
                }
            }
        }

        #endregion

        #region EISC Signal Change Handlers

        void MainsigChangeHandler(GenericBase currentDevice, SigEventArgs args)
        {
            if (args.Event == eSigEvent.UShortChange)
            {
                if (args.Sig.Number <= 100)
                {
                    if (args.Sig.UShortValue <= 100)
                    {
                    }
                }
                else if (args.Sig.Number > 100 && args.Sig.Number < 201)
                {
                }
                else if (args.Sig.Number > 200)
                {
                }
            }
            if (args.Event == eSigEvent.StringChange)
            {
                if (args.Sig.Number > 0)
                {
                }
            }
            if (args.Event == eSigEvent.BoolChange)
            {
            }
        }

        void SubsystemSigChangeHandler(GenericBase currentDevice, SigEventArgs args)
        {
            if (args.Event == eSigEvent.BoolChange && args.Sig.BoolValue == true)
            {
                if (args.Sig.Number > 600 && args.Sig.Number <= 700)
                {
                    ushort TPNumber = (ushort)(args.Sig.Number - 600);
                    // 'true' here means "use DefaultRoom", not "came from the idle timeout" — the
                    // flag carries both meanings. As a side effect this EISC-driven nav does not
                    // reset the idle timer, which is intentional: it is not a panel touch, and any
                    // real interaction that follows resets the timer through
                    // UserInterfaceObject_SigChange anyway. Do not "fix" this by resetting here —
                    // that reopens the self-retriggering loop if SIMPL ever pulses this join.
                    RoomButtonPress(TPNumber, true);
                }
            }
        }

        public void subysystemControl_SigChange(BasicTriList currentDevice, SigEventArgs args)
        {
            if (args.Sig.Type == eSigType.UShort)
            {
                ushort TPNumber = (ushort)((args.Sig.Number / 100) + 1);
                if (args.Sig.Number % 100 == 1)
                {
                    // This analog is MULTIPLEXED: depending on the panel's current subsystem it
                    // carries video volume, number-of-light-buttons, number-of-shade-columns, or a
                    // quick-action refresh. Lights/Shades/QuickAction own it while those pages are
                    // up, so they keep first claim. Otherwise it is video volume.
                    //
                    // It used to relay to analog 1 ONLY when CurrentSubsystemIsVideo, which
                    // navigation clears — so the home page saw no video volume feedback even
                    // though SIMPL was ramping the value. Now, when no other subsystem is claiming
                    // the join, we relay whenever the room's resolved volume target is video, which
                    // is exactly when the volume buttons are ramping video. See
                    // ResolveVolumeTargetIsAudio in ControlSystem.Subsystems.cs.
                    bool tpIsLights = manager.touchpanelZ[TPNumber].CurrentSubsystemIsLights;
                    bool tpIsShades = manager.touchpanelZ[TPNumber].CurrentSubsystemIsShades;
                    ushort qaProbe = subsystemEISC.UShortInput[(ushort)(TPNumber + 200)].UShortValue;
                    bool tpIsQuickAction = qaProbe > 300 && qaProbe < 400;
                    bool joinClaimedByOther = tpIsLights || tpIsShades || tpIsQuickAction;

                    if (manager.touchpanelZ[TPNumber].CurrentSubsystemIsVideo
                        || (!joinClaimedByOther && !ResolveVolumeTargetIsAudio(TPNumber)))
                    {
                        manager.touchpanelZ[TPNumber].UserInterface.UShortInput[1].UShortValue = args.Sig.UShortValue;
                    }
                    else if (tpIsLights)
                    {
                        CrestronConsole.PrintLine("numberoflightbuttons{0}", args.Sig.UShortValue);
                        if (manager.touchpanelZ[TPNumber].HTML_UI)
                        {
                            manager.touchpanelZ[TPNumber]._HTMLContract.LightButtonList.NumberOfLightButtons(
                                (sig, wh) => sig.UShortValue = args.Sig.UShortValue);
                        }
                        else
                        {
                            manager.touchpanelZ[TPNumber].UserInterface.SmartObjects[8].UShortInput[4].UShortValue = args.Sig.UShortValue;
                        }
                    }
                    else if (tpIsShades)
                    {
                        CrestronConsole.PrintLine("numberofShadesColumns{0}", args.Sig.UShortValue);
                        if (manager.touchpanelZ[TPNumber].HTML_UI)
                        {
                            manager.touchpanelZ[TPNumber]._HTMLContract.ShadesList.numberOfShadeColumns(
                                (sig, wh) => sig.UShortValue = args.Sig.UShortValue);
                        }
                        else
                        {
                            CrestronConsole.PrintLine("SHADESYNC: TP-{0} LIVE count={1} from analog {2} -> SO19",
                                TPNumber, args.Sig.UShortValue, args.Sig.Number);
                            manager.touchpanelZ[TPNumber].UserInterface.SmartObjects[19].UShortInput[3].UShortValue = args.Sig.UShortValue;
                        }
                    }
                    else if (tpIsQuickAction)
                    {
                        CrestronConsole.PrintLine("currentsubsystemisquickaction");
                        quickActionControl.RefreshQuickAction(TPNumber);//from subsystemControl_Sigchange
                    }
                }
                ushort sigNumber = (ushort)(args.Sig.Number % 100);
                manager.touchpanelZ[TPNumber].UserInterface.UShortInput[(ushort)(sigNumber + 100)].UShortValue = args.Sig.UShortValue;
            }
            else if (args.Sig.Type == eSigType.String)
            {
                ushort TPNumber = (ushort)((args.Sig.Number / 100) + 1);
                ushort stringNumber = (ushort)(args.Sig.Number % 100);
                if (stringNumber == 0 || !manager.touchpanelZ.ContainsKey(TPNumber))
                    return;
                if (manager.touchpanelZ[TPNumber].HTML_UI)
                {
                    ushort index = (ushort)(stringNumber - 1);
                    if (manager.touchpanelZ[TPNumber].CurrentSubsystemIsShades)
                    {
                        if (index < manager.touchpanelZ[TPNumber]._HTMLContract.ShadeButtons.Length)
                        {
                            manager.touchpanelZ[TPNumber]._HTMLContract.ShadeButtons[index].ShadeName(
                                (sig, wh) => sig.StringValue = args.Sig.StringValue);
                        }
                    }
                    else if (index < manager.touchpanelZ[TPNumber]._HTMLContract.LightButton.Length)
                    {
                        manager.touchpanelZ[TPNumber]._HTMLContract.LightButton[index].LightButtonName(
                            (sig, wh) => sig.StringValue = args.Sig.StringValue);
                    }
                }
                else
                {
                    if (manager.touchpanelZ[TPNumber].CurrentSubsystemIsShades)
                    {
                        // Shade names: string increment is 2 on the smart object.
                        // EISC stringNumber 1 = shade 1 name → SO StringInput offset 4010
                        // stringNumber 1 → slot 1, stringNumber 2 → slot 3, etc. (skip every other)
                        ushort soStringIndex = (ushort)((stringNumber - 1) * 2 + 1 + 4010);
                        manager.touchpanelZ[TPNumber].UserInterface.SmartObjects[19].StringInput[soStringIndex].StringValue = args.Sig.StringValue;
                    }
                    else
                    {
                        manager.touchpanelZ[TPNumber].UserInterface.SmartObjects[8].StringInput[(ushort)(stringNumber + 10)].StringValue = args.Sig.StringValue;
                    }
                }
                manager.touchpanelZ[TPNumber].UserInterface.StringInput[(ushort)(stringNumber + 300)].StringValue = args.Sig.StringValue;
            }
            else if (args.Sig.Type == eSigType.Bool)
            {
                ushort TPNumber = (ushort)((args.Sig.Number / 200) + 1);
                ushort boolNumber = (ushort)(args.Sig.Number % 200);
                if (manager.touchpanelZ[TPNumber].CurrentSubsystemIsVideo)
                {
                    if (boolNumber < 140)
                    {
                        manager.touchpanelZ[TPNumber].UserInterface.BooleanInput[(ushort)(boolNumber + 200)].BoolValue = args.Sig.BoolValue;
                    }
                    else if (boolNumber >= 140)
                    {
                        manager.touchpanelZ[TPNumber].UserInterface.BooleanInput[(ushort)(boolNumber)].BoolValue = args.Sig.BoolValue;
                    }
                }
                else if (manager.touchpanelZ[TPNumber].CurrentSubsystemIsShades)
                {
                    if (manager.touchpanelZ[TPNumber].HTML_UI)
                    {
                        ushort shadeIndex = (ushort)((boolNumber - 1) / 3);
                        ushort signalType = (ushort)((boolNumber - 1) % 3);

                        if (shadeIndex < manager.touchpanelZ[TPNumber]._HTMLContract.ShadeButtons.Length)
                        {
                            switch (signalType)
                            {
                                case 0:
                                    manager.touchpanelZ[TPNumber]._HTMLContract.ShadeButtons[shadeIndex].ShadeOpened(
                                        (sig, wh) => sig.BoolValue = args.Sig.BoolValue);
                                    break;
                                case 1:
                                    manager.touchpanelZ[TPNumber]._HTMLContract.ShadeButtons[shadeIndex].ShadeStopped(
                                        (sig, wh) => sig.BoolValue = args.Sig.BoolValue);
                                    break;
                                case 2:
                                    manager.touchpanelZ[TPNumber]._HTMLContract.ShadeButtons[shadeIndex].ShadeClosed(
                                        (sig, wh) => sig.BoolValue = args.Sig.BoolValue);
                                    break;
                            }
                        }
                    }
                    else
                    {
                        manager.touchpanelZ[TPNumber].UserInterface.SmartObjects[19].BooleanInput[(ushort)(boolNumber + 4010)].BoolValue = args.Sig.BoolValue;
                    }
                }
                else if (manager.touchpanelZ[TPNumber].CurrentSubsystemIsLights)
                {
                    if (manager.touchpanelZ[TPNumber].HTML_UI)
                    {
                        if (boolNumber > 0 && boolNumber <= manager.touchpanelZ[TPNumber]._HTMLContract.LightButton.Length)
                        {
                            manager.touchpanelZ[TPNumber]._HTMLContract.LightButton[boolNumber - 1].LightButtonSelected(
                                (sig, wh) => sig.BoolValue = args.Sig.BoolValue);
                        }
                    }
                    else
                    {
                        manager.touchpanelZ[TPNumber].UserInterface.SmartObjects[8].BooleanInput[(ushort)(boolNumber + 10)].BoolValue = args.Sig.BoolValue;
                    }
                }
                else if (subsystemEISC.UShortInput[(ushort)(TPNumber + 200)].UShortValue > 300 && subsystemEISC.UShortInput[(ushort)(TPNumber + 200)].UShortValue < 400)
                {
                    if (manager.touchpanelZ[TPNumber].HTML_UI)
                    {
                    }
                    else
                    {
                        manager.touchpanelZ[TPNumber].UserInterface.SmartObjects[15].BooleanInput[(ushort)(boolNumber + 15)].BoolValue = args.Sig.BoolValue;
                    }
                }
                else if (boolNumber < 101)
                {
                    manager.touchpanelZ[TPNumber].UserInterface.BooleanInput[(ushort)(boolNumber + 600)].BoolValue = args.Sig.BoolValue;
                }
            }
        }

        /// <summary>
        /// Camera-popup EISC (IPID 0xC0 @ 127.0.0.2). The far end is the VizioTVControl
        /// program (App03), which watches UniFi Protect for doorbell rings and person
        /// detection and sends "show this camera now".
        ///
        /// This program deliberately knows nothing about UniFi — the payload is just a
        /// camera name and a reason string, so the same EISC works for any future trigger
        /// source without changes here.
        ///
        /// Serial 1 only: { "seq": &lt;n&gt;, "camera": "Front Gate", "reason": "ring"|"person" }
        /// </summary>
        void CameraPopupSigChangeHandler(GenericBase currentDevice, SigEventArgs args)
        {
            // ⚠ ANNOUNCE EVERYTHING THAT ARRIVES ON THIS EISC. Nothing else is wired to 0xC0
            // inbound (App03 sends serial 1 and nothing more), so this is not a volume concern —
            // and "the payload reached App01 at all" is the single fact that splits a UniFi-side
            // failure from an ACS-side one. Without it, a press that dies here looks exactly like
            // a press that never left App03.
            if (args.Event != eSigEvent.StringChange)
            {
                CrestronConsole.PrintLine("Cameras: unexpected {0} sig {1} on the popup EISC - ignoring",
                    args.Event, args.Sig.Number);
                return;
            }

            try
            {
                if (args.Sig.Number != Cameras.CameraManager.PopupEiscCommandJoin)
                {
                    CrestronConsole.PrintLine("Cameras: popup EISC serial {0} is not the command join ({1}) - ignoring",
                        args.Sig.Number, Cameras.CameraManager.PopupEiscCommandJoin);
                    return;
                }
                if (cameraManager == null)
                {
                    CrestronConsole.PrintLine("Cameras: popup received but manager not initialized");
                    return;
                }
                cameraManager.HandlePopupCommand(args.Sig.StringValue);
            }
            catch (Exception ex)
            {
                ErrorLog.Error("CameraPopupSigChangeHandler error: {0}", ex.Message);
            }
        }

        void ImageSigChangeHandler(GenericBase currentDevice, SigEventArgs args)
        {
            if (args.Event == eSigEvent.UShortChange)
            {
                try
                {
                    if (args.Sig.Number <= 100 && args.Sig.UShortValue > 0)
                    {
                        ushort TPNumber = (ushort)args.Sig.Number;
                    }
                    else if (args.Sig.Number == 101)
                    {
                    }
                    else if (args.Sig.Number == 102)
                    {
                        if (args.Sig.UShortValue > 0)
                        {
                            CrestronConsole.PrintLine("preparing to save preset{0}", args.Sig.UShortValue);
                            quickActionXML.saving = true;
                            for (ushort i = 0; i < 100; i++)
                            {
                                imageEISC.BooleanInput[(ushort)(i + 401)].BoolValue = false;
                                quickActionXML.climateCheckboxes[i] = false;
                                quickActionXML.musicCheckboxes[i] = false;
                            }
                            quickActionXML.quickActionToRecallOrSave = args.Sig.UShortValue;
                            quickActionXML.SetQuickActionSubsystemVisibility();
                            imageEISC.StringInput[3100].StringValue = quickActionXML.PresetName[quickActionXML.quickActionToRecallOrSave - 1];
                        }
                    }
                    else if (args.Sig.Number == 103)
                    {
                        if (args.Sig.UShortValue > 0)
                        {
                            quickActionXML.saving = false;
                            quickActionXML.quickActionToRecallOrSave = args.Sig.UShortValue;
                            quickActionXML.SelectQuickActionToView();
                        }
                    }
                }
                catch (Exception e)
                {
                    ErrorLog.Warn("imagesigchangehandler number {0} value {1} {2}", args.Sig.Number, args.Sig.UShortValue, e.Message);
                }
            }
            else if (args.Event == eSigEvent.BoolChange && args.Sig.BoolValue == true)
            {
                if (args.Sig.Number > 200 && args.Sig.Number < 211)
                {
                    ushort idx = (ushort)(args.Sig.Number - 220);
                    quickActionControl.SelectQuickActionIncludedSubsystem(idx);
                }
                else if (args.Sig.Number > 210 && args.Sig.Number < 221)
                {
                    ushort idx = (ushort)(args.Sig.Number - 210);
                    quickActionXML.SelectQuickActionSubsystem(idx);
                }
                else if (args.Sig.Number > 220 && args.Sig.Number < 231)
                {
                    ushort idx = (ushort)(args.Sig.Number - 220);
                    quickActionXML.SelectSubsystemCurrentStatusToSave(idx);
                }
                else if (args.Sig.Number == 231)
                {
                    quickActionXML.writeSubsystems(quickActionXML.quickActionToRecallOrSave);
                }
                else if (args.Sig.Number == 232)
                {
                    if (isThisSubsystemInQuickActionList("audio") || isThisSubsystemInQuickActionList("music"))
                    {
                        musicSystemControl.RecallMusicPreset(quickActionXML.quickActionToRecallOrSave);
                    }
                    if (isThisSubsystemInQuickActionList("climate") || isThisSubsystemInQuickActionList("hvac"))
                    {
                        quickActionControl.RecallClimatePreset(quickActionXML.quickActionToRecallOrSave);
                    }
                }
                else if (args.Sig.Number == 233)
                {
                    quickActionXML.saving = false;
                }
                else if (args.Sig.Number > 400 && args.Sig.Number <= 500)
                {
                    ushort idx = (ushort)(args.Sig.Number - 400);
                    imageEISC.BooleanInput[(ushort)(idx + 400)].BoolValue = !imageEISC.BooleanInput[(ushort)(idx + 400)].BoolValue;
                    if (quickActionXML.currentSubsysIsMusic)
                    {
                        quickActionXML.musicCheckboxes[idx - 1] = imageEISC.BooleanInput[(ushort)(idx + 400)].BoolValue;
                    }
                    else if (quickActionXML.currentSubsystemIsClimate)
                    {
                        quickActionXML.climateCheckboxes[idx - 1] = imageEISC.BooleanInput[(ushort)(idx + 400)].BoolValue;
                    }
                }
            }
        }

        void LightingSigChangeHandler(GenericBase currentDevice, SigEventArgs args)
        {
            if (args.Event == eSigEvent.BoolChange)
            {
                UpdateLightingStatus((ushort)args.Sig.Number, args.Sig.BoolValue);
            }
        }

        void HVACSigChangeHandler(GenericBase currentDevice, SigEventArgs args)
        {
            if (args.Event == eSigEvent.UShortChange)
            {
                ushort zoneNumber = 0;
                ushort function = 0;

                if (args.Sig.Number <= 100)
                {
                    zoneNumber = (ushort)args.Sig.Number;
                    function = 1;
                }
                else if (args.Sig.Number <= 200)
                {
                    zoneNumber = (ushort)(args.Sig.Number - 100);
                    function = 2;
                }
                else if (args.Sig.Number <= 300)
                {
                    zoneNumber = (ushort)(args.Sig.Number - 200);
                    function = 3;
                }
                else if (args.Sig.Number <= 400)
                {
                    zoneNumber = (ushort)(args.Sig.Number - 300);
                    function = 4;
                }
                else if (args.Sig.Number <= 500)
                {
                    zoneNumber = (ushort)(args.Sig.Number - 400);
                    function = 5;
                }

                foreach (var room in manager.RoomZ.Values)
                {
                    if (room.ClimateID != zoneNumber || args.Sig.UShortValue == 0)
                        continue;

                    // Scenario (function 5) is a control channel, not a displayed climate
                    // value, so it is never subject to the serial/analog source latch.
                    if (function == 5)
                    {
                        room.HVACScenario = args.Sig.UShortValue;
                        continue;
                    }

                    // Functions 1-4 are displayed climate values. If this zone has already
                    // latched to serial, ignore the analog. Otherwise latch to analog.
                    if (room.ClimateSourceIsSerial == true)
                        continue;
                    room.ClimateSourceIsSerial = false;

                    switch (function)
                    {
                        case 1:
                            room.CurrentTemperature = args.Sig.UShortValue;
                            break;
                        case 2:
                            room.CurrentHeatSetpoint = args.Sig.UShortValue;
                            break;
                        case 3:
                            room.CurrentCoolSetpoint = args.Sig.UShortValue;
                            break;
                        case 4:
                            room.CurrentAutoSingleSetpoint = args.Sig.UShortValue;
                            break;
                    }
                }
            }
            else if (args.Event == eSigEvent.StringChange)
            {
                // Serial climate values mirror the analog join map on joins 1-400:
                // 1-100 temperature, 101-200 heat, 201-300 cool, 301-400 auto single setpoint.
                // Values are assumed to be a bare number (no unit/degree suffix).
                ushort zoneNumber = 0;
                ushort function = 0;

                if (args.Sig.Number <= 100)
                {
                    zoneNumber = (ushort)args.Sig.Number;
                    function = 1;
                }
                else if (args.Sig.Number <= 200)
                {
                    zoneNumber = (ushort)(args.Sig.Number - 100);
                    function = 2;
                }
                else if (args.Sig.Number <= 300)
                {
                    zoneNumber = (ushort)(args.Sig.Number - 200);
                    function = 3;
                }
                else if (args.Sig.Number <= 400)
                {
                    zoneNumber = (ushort)(args.Sig.Number - 300);
                    function = 4;
                }
                else
                {
                    return;
                }

                // Only a valid, non-zero number counts as a real value (parallels the
                // "> 0" gate on the analog side). This is what makes the source latch
                // deterministic: the unused transport sends nothing usable and never latches.
                if (!ushort.TryParse(args.Sig.StringValue?.Trim(), out ushort value) || value == 0)
                    return;

                foreach (var room in manager.RoomZ.Values)
                {
                    if (room.ClimateID != zoneNumber)
                        continue;

                    // If this zone has already latched to analog, ignore the serial.
                    // Otherwise latch to serial.
                    if (room.ClimateSourceIsSerial == false)
                        continue;
                    room.ClimateSourceIsSerial = true;

                    switch (function)
                    {
                        case 1:
                            room.CurrentTemperature = value;
                            break;
                        case 2:
                            room.CurrentHeatSetpoint = value;
                            break;
                        case 3:
                            room.CurrentCoolSetpoint = value;
                            break;
                        case 4:
                            room.CurrentAutoSingleSetpoint = value;
                            break;
                    }
                }
            }
            else if (args.Event == eSigEvent.BoolChange)
            {
                if (args.Sig.Number > 500)
                {
                    ushort index = (ushort)(args.Sig.Number - 500);
                    ushort climateID = (ushort)(((index - 1) / 30) + 1);
                    ushort signalIndex = (ushort)(((index - 1) % 30) + 1);
                    ushort tpInputNumber = (ushort)(signalIndex + 600);
                    //CrestronConsole.PrintLine("climateID {0} signalIndex {1} tpInputNumber {2}", climateID, signalIndex, tpInputNumber);
                    //CrestronConsole.PrintLine("TP-1 climate id{0}", manager.touchpanelZ[1].CurrentClimateID);
                    foreach (var panel in manager.touchpanelZ)
                    {
                        if (manager.touchpanelZ[panel.Value.Number].CurrentClimateID == climateID)
                        {
                            manager.touchpanelZ[panel.Value.Number].UserInterface.BooleanInput[tpInputNumber].BoolValue = args.Sig.BoolValue;
                        }
                    }
                }
                else if (args.Sig.BoolValue == true)
                {
                    ushort zoneNumber = 0;
                    ushort function = 0;
                    if (args.Sig.Number <= 100)
                    {
                        zoneNumber = (ushort)args.Sig.Number;
                        function = 1;
                        climateControl.UpdateRoomClimateMode(zoneNumber, function);
                    }
                    else if (args.Sig.Number <= 200)
                    {
                        zoneNumber = (ushort)(args.Sig.Number - 100);
                        function = 2;
                        climateControl.UpdateRoomClimateMode(zoneNumber, function);
                    }
                    else if (args.Sig.Number <= 300)
                    {
                        zoneNumber = (ushort)(args.Sig.Number - 200);
                        function = 3;
                        climateControl.UpdateRoomClimateMode(zoneNumber, function);
                    }
                    else if (args.Sig.Number <= 400)
                    {
                        zoneNumber = (ushort)(args.Sig.Number - 300);
                        function = 4;
                        climateControl.UpdateRoomClimateMode(zoneNumber, function);
                    }
                    else if (args.Sig.Number <= 500)
                    {
                        zoneNumber = (ushort)(args.Sig.Number - 400);
                        function = 5;
                        climateControl.UpdateRoomClimateMode(zoneNumber, function);
                    }
                }
                else if (args.Sig.BoolValue == false)
                {
                }
            }
        }

        void securitySigChangeHandler(GenericBase currentDevice, SigEventArgs args)
        {
            if (args.Event == eSigEvent.BoolChange)
            {
                if (args.Sig.Number > 0 && args.Sig.Number < 50)
                {
                    foreach (var tp in manager.touchpanelZ)
                    {
                        if (tp.Value.UserInterface == null) continue;
                        tp.Value.UserInterface.BooleanInput[(ushort)(args.Sig.Number + 750)].BoolValue = args.Sig.BoolValue;
                    }
                }
                else if (args.Sig.Number > 50 && args.Sig.Number < 55)
                {
                    foreach (var tp in manager.touchpanelZ)
                    {
                        if (tp.Value.UserInterface == null) continue;
                        if (tp.Value.HTML_UI)
                        {
                        }
                        else
                        {
                            // Not every panel type has Smart Object 22 with sigs 51-54
                            // (e.g. TSR-310/HR-310), so swallow the IndexOutOfRangeException.
                            try
                            {
                                tp.Value.UserInterface.SmartObjects[22].BooleanInput[(ushort)(args.Sig.Number)].BoolValue = args.Sig.BoolValue;
                            }
                            catch (IndexOutOfRangeException) { }
                        }
                    }
                }
                else if (args.Sig.Number > 100 && args.Sig.Number < 300)
                {
                    ushort zoneNumber = (ushort)(args.Sig.Number - 100); // 1-based EISC zone number
                    // Update tracking state
                    if (zoneNumber > 0 && zoneNumber <= MaxSecurityZones)
                        _securityZoneBypassed[zoneNumber - 1] = args.Sig.BoolValue;

                    foreach (var tp in manager.touchpanelZ)
                    {
                        if (tp.Value.UserInterface == null) continue;
                        if (tp.Value.HTML_UI)
                        {
                            // Find which compacted slot this zone is in
                            int slot = VisibleSecurityZones.IndexOf(zoneNumber);
                            if (slot >= 0 && slot < tp.Value._HTMLContract.SecurityZone.Length)
                            {
                                int capturedSlot = slot;
                                tp.Value._HTMLContract.SecurityZone[capturedSlot].ZoneBypassed(
                                    (sig, wh) => sig.BoolValue = args.Sig.BoolValue);
                            }
                        }
                        else
                        {
                            tp.Value.UserInterface.SmartObjects[21].BooleanInput[(ushort)(zoneNumber + 15)].BoolValue = args.Sig.BoolValue;
                        }
                    }
                }
                else if (args.Sig.Number > 300)
                {
                    ushort zoneNumber = (ushort)(args.Sig.Number - 300); // 1-based EISC zone number
                    // Update tracking state and rebuild compacted list
                    if (zoneNumber > 0 && zoneNumber <= MaxSecurityZones)
                    {
                        _securityZoneVisible[zoneNumber - 1] = args.Sig.BoolValue;
                        RebuildSecurityZoneListForHTML();
                    }

                    foreach (var tp in manager.touchpanelZ)
                    {
                        if (tp.Value.UserInterface == null) continue;
                        if (!tp.Value.HTML_UI)
                        {
                            tp.Value.UserInterface.SmartObjects[21].BooleanInput[(ushort)(zoneNumber + 4015)].BoolValue = args.Sig.BoolValue;
                        }
                    }
                }
            }
            else if (args.Event == eSigEvent.UShortChange)
            {
                foreach (var tp in manager.touchpanelZ)
                {
                    if (tp.Value.UserInterface == null) continue;
                    if (tp.Value.HTML_UI)
                    {
                        // For HTML panels, NumberOfSecurityZones is driven by the visible zone count
                        // so we just rebuild to be sure
                        RebuildSecurityZoneListForHTML();
                    }
                    else
                    {
                        tp.Value.UserInterface.SmartObjects[21].UShortInput[4].UShortValue = 100;
                    }
                }
            }
            else if (args.Event == eSigEvent.StringChange)
            {
                if (args.Sig.Number > 200)
                {
                    foreach (var tp in manager.touchpanelZ)
                    {
                        if (tp.Value.UserInterface == null) continue;
                        if (tp.Value.HTML_UI)
                        {
                            // HTML panels: EISC serial 201/202/203 → panel string 751/752/753
                            ushort panelJoin = (ushort)(args.Sig.Number - 200 + 750);
                            tp.Value.UserInterface.StringInput[panelJoin].StringValue = args.Sig.StringValue;
                        }
                        else
                        {
                            ushort buttonNumber = (ushort)(args.Sig.Number - 200);
                            tp.Value.UserInterface.StringInput[(ushort)(buttonNumber + 11)].StringValue = args.Sig.StringValue;
                        }
                    }
                }
                else
                {
                    ushort zoneNumber = (ushort)args.Sig.Number; // 1-based EISC zone number
                    // Update tracking state
                    if (zoneNumber > 0 && zoneNumber <= MaxSecurityZones)
                        _securityZoneName[zoneNumber - 1] = args.Sig.StringValue;

                    foreach (var tp in manager.touchpanelZ)
                    {
                        if (tp.Value.UserInterface == null) continue;
                        if (tp.Value.HTML_UI)
                        {
                            // Find which compacted slot this zone is in
                            int slot = VisibleSecurityZones.IndexOf(zoneNumber);
                            if (slot >= 0 && slot < tp.Value._HTMLContract.SecurityZone.Length)
                            {
                                int capturedSlot = slot;
                                tp.Value._HTMLContract.SecurityZone[capturedSlot].ZoneName(
                                    (sig, wh) => sig.StringValue = args.Sig.StringValue);
                            }
                        }
                        else
                        {
                            tp.Value.UserInterface.SmartObjects[21].StringInput[(ushort)(zoneNumber + 15)].StringValue = args.Sig.StringValue;
                        }
                    }
                }
            }
        }

        #endregion
    }
}
