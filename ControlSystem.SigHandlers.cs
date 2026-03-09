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
                    if (manager.touchpanelZ[TPNumber].CurrentSubsystemIsVideo)
                    {
                        manager.touchpanelZ[TPNumber].UserInterface.UShortInput[1].UShortValue = args.Sig.UShortValue;
                    }
                    else if (manager.touchpanelZ[TPNumber].CurrentSubsystemIsLights)
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
                    else if (manager.touchpanelZ[TPNumber].CurrentSubsystemIsShades)
                    {
                        CrestronConsole.PrintLine("numberofShadesColumns{0}", args.Sig.UShortValue);
                        if (manager.touchpanelZ[TPNumber].HTML_UI)
                        {
                            manager.touchpanelZ[TPNumber]._HTMLContract.ShadesList.numberOfShadeColumns(
                                (sig, wh) => sig.UShortValue = args.Sig.UShortValue);
                        }
                    }
                    else if (subsystemEISC.UShortInput[(ushort)(TPNumber + 200)].UShortValue > 300 && subsystemEISC.UShortInput[(ushort)(TPNumber + 200)].UShortValue < 400)
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
                    manager.touchpanelZ[TPNumber].UserInterface.SmartObjects[8].StringInput[(ushort)(stringNumber + 10)].StringValue = args.Sig.StringValue;
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
                    if (room.ClimateID == zoneNumber && args.Sig.UShortValue > 0)
                    {
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
                            case 5:
                                room.HVACScenario = args.Sig.UShortValue;
                                break;
                        }
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
                            tp.Value.UserInterface.SmartObjects[19].BooleanInput[(ushort)(args.Sig.Number)].BoolValue = args.Sig.BoolValue;
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
                        ushort buttonNumber = (ushort)(args.Sig.Number - 200);
                        tp.Value.UserInterface.StringInput[(ushort)(buttonNumber + 11)].StringValue = args.Sig.StringValue;
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
