//-----------------------------------------------------------------------
// <copyright file="TouchpanelUI.SmartObjects.cs" company="Crestron">
//     Copyright (c) Crestron Electronics. All rights reserved.
// </copyright>
//-----------------------------------------------------------------------
using Crestron.SimplSharp;
using Crestron.SimplSharpPro;
using Crestron.SimplSharpPro.DeviceSupport;

namespace ACS_4Series_Template_V3.UI
{
    /// <summary>
    /// SmartObject signal change handling for TouchpanelUI
    /// </summary>
    public partial class TouchpanelUI
    {
        private void SmartObject_SigChange(GenericBase currentDevice, SmartObjectEventArgs args)
        {
            ushort TPNumber = this.Number;

            switch ((SmartObjectIDs)args.SmartObjectArgs.ID)
            {
                case SmartObjectIDs.cameraKeypad:
                    break;

                case SmartObjectIDs.securityPartitions:
                    if (args.Event == eSigEvent.BoolChange)
                    {
                        ushort buttonNumber = (ushort)(args.Sig.Number);
                        _parent.securityEISC.BooleanInput[(ushort)(buttonNumber + 50)].BoolValue = args.Sig.BoolValue;
                    }
                    break;

                case SmartObjectIDs.quickActions:
                    if (args.Event == eSigEvent.BoolChange)
                    {
                        CrestronConsole.PrintLine("quickActions: {0} {1}", args.Sig.Number, args.Sig.BoolValue);
                        ushort buttonNumber = (ushort)(args.Sig.Number - 15);
                        _parent.subsystemControlEISC.BooleanInput[(ushort)((TPNumber * 100) - 100 + buttonNumber)].BoolValue = args.Sig.BoolValue;
                    }
                    break;

                case SmartObjectIDs.securityKeypad:
                    if (args.Event == eSigEvent.BoolChange)
                    {
                        ushort buttonNumber = (ushort)(args.Sig.Number);
                        _parent.securityEISC.BooleanInput[(ushort)(buttonNumber + 60)].BoolValue = args.Sig.BoolValue;
                    }
                    break;

                case SmartObjectIDs.securityZoneList:
                    if (args.Event == eSigEvent.BoolChange)
                    {
                        ushort buttonNumber = (ushort)(args.Sig.Number);
                        _parent.securityEISC.BooleanInput[(ushort)(buttonNumber + 85)].BoolValue = args.Sig.BoolValue;
                    }
                    break;

                case SmartObjectIDs.spa:
                    break;

                case SmartObjectIDs.poolTab:
                    break;

                case SmartObjectIDs.lightingButtons:
                    {
                        ushort buttonNumber = (ushort)(args.Sig.Number - 10);
                        if (args.Sig.Type == eSigType.Bool)
                        {
                            _parent.subsystemControlEISC.BooleanInput[(ushort)((TPNumber - 1) * 200 + buttonNumber)].BoolValue = args.Sig.BoolValue;
                        }
                    }
                    break;

                case SmartObjectIDs.quickViewSubsystems:
                case SmartObjectIDs.quickActionViewStatus:
                case SmartObjectIDs.quickActionSaveCheckbox:
                case SmartObjectIDs.quickActionMusic:
                case SmartObjectIDs.quickActionClimate:
                    break;

                case SmartObjectIDs.subsystemSelect:
                    if (args.Event == eSigEvent.UShortChange)
                    {
                        if (args.Sig.Number == 1)
                        {
                            ushort subsystemButtonNumber = (ushort)args.Sig.UShortValue;
                            _parent.SelectSubsystem(TPNumber, subsystemButtonNumber);
                        }
                    }
                    break;

                case SmartObjectIDs.DVRTab:
                    HandleDVRTabChange(args);
                    break;

                case SmartObjectIDs.dpad:
                    if (args.Event == eSigEvent.BoolChange)
                    {
                        ushort buttonNumber = (ushort)(args.Sig.Number);
                        _parent.subsystemControlEISC.BooleanInput[(ushort)(((TPNumber - 1) * 200) + buttonNumber + 21)].BoolValue = args.Sig.BoolValue;
                    }
                    break;

                case SmartObjectIDs.DVRKeypad:
                    if (args.Event == eSigEvent.BoolChange)
                    {
                        ushort buttonNumber = (ushort)(args.Sig.Number);
                        _parent.subsystemControlEISC.BooleanInput[(ushort)(((TPNumber - 1) * 200) + buttonNumber + 30)].BoolValue = args.Sig.BoolValue;
                    }
                    break;

                case SmartObjectIDs.floorSelect:
                    if (args.Event == eSigEvent.UShortChange)
                    {
                        if (args.Sig.Number == 1)
                        {
                            ushort floorButtonNumber = (ushort)args.Sig.UShortValue;
                            if (this.CurrentPageNumber == 0)
                            {
                                _parent.SelectWholeHouseFloor(TPNumber, floorButtonNumber);
                            }
                            else
                            {
                                _parent.SelectFloor(TPNumber, floorButtonNumber);
                            }
                        }
                    }
                    break;

                case SmartObjectIDs.zoneSelect:
                    if (args.Event == eSigEvent.UShortChange)
                    {
                        if (args.Sig.Number == 1)
                        {
                            CrestronConsole.PrintLine("zoneSelect: {0} {1}", args.Sig.Number, args.Sig.UShortValue);
                            this.CurrentPageNumber = 2;
                            _parent.SelectZone((TPNumber), (ushort)args.Sig.UShortValue, true);
                            this.UserInterface.BooleanInput[100].BoolValue = true;
                            this.UserInterface.BooleanInput[50].BoolValue = false;
                            this.UserInterface.BooleanInput[51].BoolValue = false;
                        }
                    }
                    break;

                case SmartObjectIDs.musicMenu:
                    HandleMusicMenuChange(args, TPNumber);
                    break;

                case SmartObjectIDs.musicFloorSelect:
                    if (args.Event == eSigEvent.UShortChange)
                    {
                        if (args.Sig.Number == 1)
                        {
                            ushort floorButtonNumber = (ushort)args.Sig.UShortValue;
                            _parent.SelectMusicFloor(TPNumber, floorButtonNumber);
                        }
                    }
                    break;

                case SmartObjectIDs.musicSources:
                    HandleMusicSourcesChange(args, TPNumber);
                    break;

                case SmartObjectIDs.wholeHouseSubsystems:
                    if (args.Event == eSigEvent.UShortChange)
                    {
                        if (args.Sig.Number == 1)
                        {
                            ushort subsystemButtonNumber = (ushort)args.Sig.UShortValue;
                            _parent.SelectSubsystem(TPNumber, subsystemButtonNumber);
                        }
                    }
                    break;

                case SmartObjectIDs.wholeHouseZoneList:
                    HandleWholeHouseZoneListChange(args, TPNumber);
                    break;

                case SmartObjectIDs.videoSources:
                    if (args.Event == eSigEvent.UShortChange)
                    {
                        if (args.Sig.Number == 1 && args.Sig.UShortValue > 0)
                        {
                            ushort vsrcButtonNumber = (ushort)args.Sig.UShortValue;
                            _parent.videoSystemControl.SelectVideoSourceFromTP(TPNumber, vsrcButtonNumber);
                        }
                    }
                    break;

                case SmartObjectIDs.videoDisplays:
                    if (args.Event == eSigEvent.UShortChange)
                    {
                        if (args.Sig.Number == 1 && args.Sig.UShortValue > 0)
                        {
                            ushort vdisplayButtonNumber = (ushort)args.Sig.UShortValue;
                            this.UserInterface.BooleanInput[351].BoolValue = false;
                            _parent.videoSystemControl.SelectDisplay(TPNumber, vdisplayButtonNumber);
                        }
                    }
                    break;

                default:
                    break;
            }
        }

        private void HandleDVRTabChange(SmartObjectEventArgs args)
        {
            if (args.Event == eSigEvent.BoolChange && args.Sig.BoolValue == true)
            {
                CrestronConsole.PrintLine("DVRTab: {0} {1}", args.Sig.Number, args.Sig.BoolValue);
                ushort buttonNumber = (ushort)args.Sig.Number;
                if (buttonNumber == 1)
                {
                    _parent.manager.VideoSourceZ[this.CurrentVSrcNum].CurrentSubpageScenario = 1;
                    this.UserInterface.BooleanInput[141].BoolValue = true;
                    this.UserInterface.SmartObjects[26].BooleanInput[2].BoolValue = true;
                    this.UserInterface.BooleanInput[142].BoolValue = false;
                    this.UserInterface.SmartObjects[26].BooleanInput[4].BoolValue = false;
                }
                else if (buttonNumber == 3)
                {
                    _parent.manager.VideoSourceZ[this.CurrentVSrcNum].CurrentSubpageScenario = 2;
                    this.UserInterface.BooleanInput[141].BoolValue = false;
                    this.UserInterface.SmartObjects[26].BooleanInput[2].BoolValue = false;
                    this.UserInterface.BooleanInput[142].BoolValue = true;
                    this.UserInterface.SmartObjects[26].BooleanInput[4].BoolValue = true;
                }
            }
        }

        private void HandleMusicMenuChange(SmartObjectEventArgs args, ushort TPNumber)
        {
            if (args.Event == eSigEvent.UShortChange)
            {
                CrestronConsole.PrintLine("musicMenu: {0} {1}", args.Sig.Number, args.Sig.UShortValue);
            }
            else if (args.Event == eSigEvent.BoolChange)
            {
                ushort buttonNumber = (ushort)(args.Sig.Number - 4010);
                ushort command = (ushort)(buttonNumber % 7);
                ushort roomListPosition = (ushort)(buttonNumber / 7 + 1);
                ushort roomNumber = this.MusicRoomsToShareSourceTo[roomListPosition - 1];
                string tpCurrentRoom = _parent.manager.RoomZ[this.CurrentRoomNum].Name;
                ushort audioID = _parent.manager.RoomZ[roomNumber].AudioID;
                string roomname = _parent.manager.RoomZ[roomNumber].Name;
                ushort audioSrcNum = _parent.manager.RoomZ[this.CurrentRoomNum].CurrentMusicSrc;
                string audioSrcName = _parent.manager.MusicSourceZ[audioSrcNum].Name;

                if (audioID > 0)
                {
                    switch (command)
                    {
                        case 0: // save volume
                            break;
                        case 1: // checkbox toggle
                            if (args.Sig.BoolValue == true)
                            {
                                this.MusicRoomsToShareCheckbox[roomListPosition - 1] = !this.MusicRoomsToShareCheckbox[roomListPosition - 1];
                            }
                            this.UserInterface.SmartObjects[7].BooleanInput[(ushort)(roomListPosition * 7 + 4004)].BoolValue = this.MusicRoomsToShareCheckbox[roomListPosition - 1];
                            this.UserInterface.SmartObjects[7].BooleanInput[(ushort)(roomListPosition * 7 + 4009)].BoolValue = this.MusicRoomsToShareCheckbox[roomListPosition - 1];
                            if (this.MusicRoomsToShareCheckbox[roomListPosition - 1])
                            {
                                this.UserInterface.SmartObjects[7].StringInput[(ushort)(roomListPosition * 2 + 10)].StringValue = _parent.BuildHTMLString(TPNumber, audioSrcName, "24");
                                this.UserInterface.SmartObjects[7].UShortInput[(ushort)(roomListPosition + 10)].UShortValue = _parent.manager.RoomZ[roomNumber].MusicVolume;
                                uint vol = _parent.manager.RoomZ[roomNumber].MusicVolume;
                                ushort volPercent = (ushort)(vol * 100 / 65535);
                                CrestronConsole.PrintLine("rm {0} vol{1} pos{2}", _parent.manager.RoomZ[roomNumber].Name, volPercent, roomListPosition);
                                _parent.musicSystemControl.SwitcherSelectMusicSource(audioID, audioSrcNum);
                            }
                            else
                            {
                                this.UserInterface.SmartObjects[7].StringInput[(ushort)(roomListPosition * 2 + 10)].StringValue = _parent.BuildHTMLString(TPNumber, "Off", "24");
                                _parent.musicSystemControl.SwitcherSelectMusicSource(audioID, 0);
                            }
                            break;
                        case 2: // vol up
                            _parent.musicEISC1.BooleanInput[(ushort)(audioID)].BoolValue = args.Sig.BoolValue;
                            break;
                        case 3: // vol dn
                            _parent.musicEISC1.BooleanInput[(ushort)(audioID + 100)].BoolValue = args.Sig.BoolValue;
                            break;
                        case 4: // mute
                            _parent.musicEISC1.BooleanInput[(ushort)(audioID + 200)].BoolValue = args.Sig.BoolValue;
                            break;
                        default:
                            break;
                    }
                }
            }
        }

        private void HandleMusicSourcesChange(SmartObjectEventArgs args, ushort TPNumber)
        {
            if (args.Event == eSigEvent.UShortChange)
            {
                if (args.Sig.Number == 1 && args.Sig.UShortValue > 0)
                {
                    ushort asrcButtonNumber = (ushort)args.Sig.UShortValue;
                    ushort asrcScenario = _parent.manager.RoomZ[this.CurrentRoomNum].AudioSrcScenario;
                    ushort asrcNumberToSend = _parent.manager.AudioSrcScenarioZ[asrcScenario].IncludedSources[asrcButtonNumber - 1];
                    _parent.musicSystemControl.PanelSelectMusicSource(TPNumber, asrcNumberToSend);

                    if (this.UserInterface.BooleanInput[1002].BoolValue == true)
                    {
                        for (int i = 0; i < this.MusicRoomsToShareSourceTo.Count; i++)
                        {
                            if (this.MusicRoomsToShareCheckbox[i] == true)
                            {
                                _parent.musicSystemControl.SwitcherSelectMusicSource(_parent.manager.RoomZ[this.MusicRoomsToShareSourceTo[i]].AudioID, asrcNumberToSend);
                                this.UserInterface.SmartObjects[7].StringInput[(ushort)(i * 2 + 12)].StringValue = _parent.BuildHTMLString(TPNumber, _parent.manager.MusicSourceZ[asrcNumberToSend].Name, "24");
                            }
                        }
                    }
                }
            }
        }

        private void HandleWholeHouseZoneListChange(SmartObjectEventArgs args, ushort TPNumber)
        {
            if (args.Event == eSigEvent.UShortChange)
            {
                if (args.Sig.Number == 1)
                {
                    this.CurrentPageNumber = 0;
                    ushort subsystemNumber = this.CurrentSubsystemNumber;
                    ushort currentRoomNumber = 0;
                    if (this.WholeHouseRoomList.Count > 0 && args.Sig.UShortValue > 0)
                    {
                        currentRoomNumber = this.WholeHouseRoomList[args.Sig.UShortValue - 1];
                        this.CurrentRoomNum = currentRoomNumber;
                        this.UserInterface.StringInput[1].StringValue = _parent.manager.RoomZ[currentRoomNumber].Name;
                    }
                    CrestronConsole.PrintLine("wholeHouseZoneList: {0} {1} subsystem{2} room{3}", args.Sig.Number, args.Sig.UShortValue, subsystemNumber, _parent.manager.RoomZ[currentRoomNumber].Name);
                    if (subsystemNumber > 0)
                    {
                        this.subsystemPageFlips(_parent.manager.SubsystemZ[subsystemNumber].FlipsToPageNumber);

                        if (_parent.manager.SubsystemZ[subsystemNumber].EquipID > 99)
                        {
                            _parent.subsystemEISC.UShortInput[(ushort)(TPNumber + 200)].UShortValue = (ushort)(_parent.manager.SubsystemZ[subsystemNumber].EquipID + TPNumber);
                        }
                        else
                        {
                            _parent.subsystemEISC.UShortInput[(ushort)(TPNumber + 200)].UShortValue = (ushort)(_parent.manager.SubsystemZ[subsystemNumber].EquipID);
                        }
                    }
                    if (currentRoomNumber > 0)
                    {
                        _parent.subsystemEISC.UShortInput[(ushort)((TPNumber - 1) * 10 + 303)].UShortValue = _parent.manager.RoomZ[currentRoomNumber].LightsID;
                        _parent.subsystemEISC.UShortInput[(ushort)((TPNumber - 1) * 10 + 304)].UShortValue = _parent.manager.RoomZ[currentRoomNumber].ShadesID;

                        this.CurrentClimateID = _parent.manager.RoomZ[currentRoomNumber].ClimateID;
                        _parent.climateControl.SyncPanelToClimateZone(TPNumber);
                    }
                }
            }
        }

        private void onAnalogChangeEvent(uint deviceID, SigEventArgs args)
        {
        }
    }
}
