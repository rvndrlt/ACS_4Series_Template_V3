//-----------------------------------------------------------------------
// <copyright file="TouchpanelUI.SigChange.cs" company="Crestron">
//     Copyright (c) Crestron Electronics. All rights reserved.
// </copyright>
//-----------------------------------------------------------------------
using Crestron.SimplSharp;
using Crestron.SimplSharpPro;
using Crestron.SimplSharpPro.CrestronThread;
using Crestron.SimplSharpPro.DeviceSupport;

namespace ACS_4Series_Template_V3.UI
{
    /// <summary>
    /// UserInterface signal change handling for TouchpanelUI
    /// </summary>
    public partial class TouchpanelUI
    {
        private void UserInterfaceObject_SigChange(BasicTriList currentDevice, SigEventArgs args)
        {
            if (args.Sig.Type == eSigType.Bool)
            {
                HandleBooleanSigChange(currentDevice, args);
            }
            else if (args.Sig.Type == eSigType.UShort)
            {
                // CrestronConsole.PrintLine("Sig Change Event: {0}, Value: {1}", args.Sig.Number, args.Sig.UShortValue);
            }
        }

        private void HandleBooleanSigChange(BasicTriList currentDevice, SigEventArgs args)
        {
            // Video volume buttons
            if (args.Sig.Number > 150 && args.Sig.Number < 160)
            {
                _parent.subsystemControlEISC.BooleanInput[(ushort)(((Number - 1) * 200) + args.Sig.Number)].BoolValue = args.Sig.BoolValue;
            }
            // 160 is the sleep button 180 is the format button
            else if (args.Sig.Number > 180 && args.Sig.Number <= 200)
            {
                _parent.subsystemControlEISC.BooleanInput[(ushort)(((Number - 1) * 200) + args.Sig.Number)].BoolValue = args.Sig.BoolValue;
            }
            else if (args.Sig.Number > 200 && args.Sig.Number <= 350)
            {
                _parent.subsystemControlEISC.BooleanInput[(ushort)(((Number - 1) * 200) + args.Sig.Number - 200)].BoolValue = args.Sig.BoolValue;
            }
            else if (args.Sig.Number > 600 && args.Sig.Number < 701)
            {
                HandleSubsystemButtons(args);
            }
            else if (args.Sig.Number > 750 && args.Sig.Number < 800)
            {
                _parent.securityEISC.BooleanInput[(ushort)(args.Sig.Number - 750)].BoolValue = args.Sig.BoolValue;
            }
            else if (args.Sig.Number == 1007)
            {
                // Main volume up
                CrestronConsole.PrintLine("TP-{0} audioID: {1}", this.Number, _parent.manager.RoomZ[this.CurrentRoomNum].AudioID);
                _parent.musicEISC1.BooleanInput[(ushort)(_parent.manager.RoomZ[this.CurrentRoomNum].AudioID)].BoolValue = args.Sig.BoolValue;
            }
            else if (args.Sig.Number == 1008)
            {
                // Main volume down
                CrestronConsole.PrintLine("TP-{0} audioID: {1}", this.Number, _parent.manager.RoomZ[this.CurrentRoomNum].AudioID);
                _parent.musicEISC1.BooleanInput[(ushort)(_parent.manager.RoomZ[this.CurrentRoomNum].AudioID + 100)].BoolValue = args.Sig.BoolValue;
            }
            else if (args.Sig.BoolValue == true)
            {
                HandleBooleanPressEvents(currentDevice, args);
            }
        }

        private void HandleSubsystemButtons(SigEventArgs args)
        {
            if (this.CurrentSubsystemIsClimate)
            {
                ushort climateID = _parent.manager.RoomZ[this.CurrentRoomNum].ClimateID;
                ushort buttonNumber = (ushort)(climateID * 30 + args.Sig.Number - 130);
                _parent.HVACEISC.BooleanInput[buttonNumber].BoolValue = args.Sig.BoolValue;
            }
            else
            {
                ushort buttonNumber = (ushort)(args.Sig.Number - 600);
                ushort eiscPos = (ushort)(((this.Number - 1) * 200) + buttonNumber);
                _parent.subsystemControlEISC.BooleanInput[(ushort)(eiscPos)].BoolValue = args.Sig.BoolValue;
            }
        }

        private void HandleBooleanPressEvents(BasicTriList currentDevice, SigEventArgs args)
        {
            _parent.manager.ipidToNumberMap.TryGetValue(currentDevice.ID, out ushort tpNumber);

            switch (args.Sig.Number)
            {
                case 14:
                    HandleHomeButton(tpNumber);
                    break;
                case 15:
                    HandleRoomButton(tpNumber);
                    break;
                case 16:
                    HandleRoomListButton(tpNumber);
                    break;
                case 21:
                    this.UserInterface.BooleanInput[21].BoolValue = !this.UserInterface.BooleanInput[21].BoolValue;
                    break;
                case 50:
                    HandleChangeRoomButton(tpNumber);
                    break;
                case 53:
                    this.UserInterface.BooleanInput[53].BoolValue = !this.UserInterface.BooleanInput[53].BoolValue;
                    break;
                case 55:
                    this.UserInterface.BooleanInput[55].BoolValue = !this.UserInterface.BooleanInput[55].BoolValue;
                    break;
                case 60:
                    SleepFormatLiftMenu("LIFT", 30);
                    break;
                case 70:
                    _parent.manager.RoomZ[this.CurrentRoomNum].LiftGoWithOff = !_parent.manager.RoomZ[this.CurrentRoomNum].LiftGoWithOff;
                    this.UserInterface.BooleanInput[70].BoolValue = _parent.manager.RoomZ[this.CurrentRoomNum].LiftGoWithOff;
                    break;
                case 99:
                    HandleBackArrow(tpNumber);
                    break;
                case 100:
                    _parent.PressCloseXButton(tpNumber);
                    break;
                case 149:
                case 150:
                    this.videoPageFlips(0);
                    this.videoButtonFB(0);
                    _parent.videoSystemControl.SelectVideoSourceFromTP(tpNumber, 0);
                    break;
                case 160:
                    SleepFormatLiftMenu("SLEEP", 30);
                    break;
                case 180:
                    SleepFormatLiftMenu("FORMAT", 30);
                    break;
                case 351:
                    this.UserInterface.BooleanInput[351].BoolValue = !this.UserInterface.BooleanInput[351].BoolValue;
                    break;
                case 1002:
                    HandleSharingButton();
                    break;
                case 1003:
                    HandleMusicOff();
                    break;
                case 1004:
                    HandleMusicShareToAll();
                    break;
                case 1005:
                    HandleMusicUnshareAll();
                    break;
                case 1006:
                    HandleMusicAllOff();
                    break;
                case 1009:
                    _parent.musicEISC1.BooleanInput[(ushort)(_parent.manager.RoomZ[this.CurrentRoomNum].AudioID + 200)].BoolValue = true;
                    _parent.musicEISC1.BooleanInput[(ushort)(_parent.manager.RoomZ[this.CurrentRoomNum].AudioID + 200)].BoolValue = false;
                    break;
                default:
                    HandleOtherButtons(tpNumber, args);
                    break;
            }
        }

        private void HandleHomeButton(ushort tpNumber)
        {
            this.CurrentPageNumber = 0;
            _parent.HomeButtonPress(tpNumber);
            this.UserInterface.BooleanInput[11].BoolValue = true;
            this.UserInterface.BooleanInput[12].BoolValue = false;
            this.UserInterface.BooleanInput[998].BoolValue = false;
            this.UserInterface.BooleanInput[999].BoolValue = false;
            this.UserInterface.BooleanInput[1002].BoolValue = false;
            this.UserInterface.BooleanInput[21].BoolValue = false;
        }

        private void HandleRoomButton(ushort tpNumber)
        {
            this.CurrentPageNumber = 2;
            this.musicPageFlips(0);
            CrestronConsole.PrintLine("RoomButtonPress: CurrentPageNumber {0}", this.CurrentPageNumber);
            this.UserInterface.BooleanInput[11].BoolValue = false;
            this.UserInterface.BooleanInput[12].BoolValue = true;
            this.UserInterface.BooleanInput[998].BoolValue = false;
            this.UserInterface.BooleanInput[999].BoolValue = false;
            this.UserInterface.BooleanInput[1002].BoolValue = false;
            this.UserInterface.BooleanInput[21].BoolValue = false;
            _parent.RoomButtonPress(tpNumber, false);
            this.UserInterface.BooleanInput[100].BoolValue = true;
            CrestronConsole.PrintLine("RoomButtonPress: CurrentPageNumber {0}", this.CurrentPageNumber);
        }

        private void HandleRoomListButton(ushort tpNumber)
        {
            _parent.RoomListButtonPress(tpNumber);
            this.CurrentPageNumber = 1;
            this.musicPageFlips(0);
            CrestronConsole.PrintLine("RoomListButtonPress: CurrentPageNumber {0}", this.CurrentPageNumber);
            this.UserInterface.BooleanInput[11].BoolValue = false;
            this.UserInterface.BooleanInput[12].BoolValue = true;
            this.UserInterface.BooleanInput[998].BoolValue = false;
            this.UserInterface.BooleanInput[999].BoolValue = false;
            this.UserInterface.BooleanInput[1002].BoolValue = false;
            this.UserInterface.BooleanInput[21].BoolValue = false;
        }

        private void HandleChangeRoomButton(ushort tpNumber)
        {
            if (!_parent.manager.touchpanelZ[tpNumber].Name.ToUpper().Contains("IPHONE"))
            {
                _parent.imageEISC.BooleanInput[tpNumber].BoolValue = false;
                this.CurrentSubsystemIsVideo = false;
                subsystemPageFlips(1000);
            }
            _parent.SelectOnlyFloor(tpNumber);
            _parent.manager.touchpanelZ[tpNumber].CurrentPageNumber = 1;
            _parent.UpdateRoomsPageStatusText(tpNumber);
        }

        private void HandleBackArrow(ushort tpNumber)
        {
            if (this.CurrentPageNumber == (ushort)CurrentPageType.Home)
            {
                this.UserInterface.BooleanInput[91].BoolValue = false;
                this.UserInterface.BooleanInput[94].BoolValue = false;
            }
            subsystemPageFlips(0);
            this.CurrentSubsystemIsLights = false;
            _parent.subsystemEISC.UShortInput[(ushort)(tpNumber + 200)].UShortValue = (ushort)(tpNumber + 300);
        }

        private void HandleOtherButtons(ushort tpNumber, SigEventArgs args)
        {
            if (args.Sig.Number > 160 && args.Sig.Number < 167)
            {
                HandleSleepButtons(args);
            }
        }

        private void HandleSleepButtons(SigEventArgs args)
        {
            for (ushort i = 0; i < 5; i++)
            {
                this.UserInterface.BooleanInput[(ushort)(161 + i)].BoolValue = false;
            }
            if (args.Sig.Number == 166)
            {
                _parent.manager.RoomZ[this.CurrentRoomNum].StartSleepTimer(0, _parent, Number);
            }
            else
            {
                ushort button = (ushort)(args.Sig.Number - 160);
                ushort sleepCmd = _parent.manager.SleepScenarioZ[_parent.manager.RoomZ[this.CurrentRoomNum].SleepScenario].SleepCmds[button - 1];
                ushort time = _parent.manager.SleepCmdZ[sleepCmd].Length;
                CrestronConsole.PrintLine("Sleep button {0} cmd {1} time {2}", button, sleepCmd, time);
                this.UserInterface.BooleanInput[(ushort)(160 + button)].BoolValue = true;
                _parent.manager.RoomZ[this.CurrentRoomNum].StartSleepTimer(time, _parent, Number);
            }
        }

        private void HandleSharingButton()
        {
            if (_sharingMenuTimer != null)
            {
                _sharingMenuTimer.Stop();
                _sharingMenuTimer.Dispose();
            }

            this.SrcSharingButtonFB = !this.SrcSharingButtonFB;
            this.UserInterface.BooleanInput[1002].BoolValue = this.SrcSharingButtonFB;
            ushort rm = this.CurrentRoomNum;
            ushort asrcSharingScenario = _parent.manager.RoomZ[rm].AudioSrcSharingScenario;
            if (!this.SrcSharingButtonFB)
            {
                this.UserInterface.BooleanInput[998].BoolValue = false;
                this.UserInterface.BooleanInput[999].BoolValue = false;
                UnsubscribeFromMusicSharingChanges();
            }
            else
            {
                _sharingMenuTimer = new CTimer(HideSharingMenu, null, 60000, -1);
                if (this.CurrentSubsystemIsAudio && asrcSharingScenario > 50)
                {
                    this.UserInterface.BooleanInput[998].BoolValue = false;
                    this.UserInterface.BooleanInput[999].BoolValue = true;
                }
                else
                {
                    this.UserInterface.BooleanInput[998].BoolValue = true;
                    this.UserInterface.BooleanInput[999].BoolValue = false;
                }
                SubscribeToMusicSharingChanges();
            }
        }

        private void HandleMusicOff()
        {
            this.musicButtonFB(0);
            this.musicPageFlips(0);
            _parent.manager.RoomZ[this.CurrentRoomNum].CurrentMusicSrc = 0;
            this.UserInterface.StringInput[3].StringValue = "Off";
            _parent.manager.RoomZ[this.CurrentRoomNum].MusicStatusText = "";

            if (this.UserInterface.BooleanInput[1002].BoolValue == true)
            {
                for (int i = 0; i < this.MusicRoomsToShareSourceTo.Count; i++)
                {
                    if (this.MusicRoomsToShareCheckbox[i] == true)
                    {
                        _parent.musicSystemControl.SwitcherSelectMusicSource(_parent.manager.RoomZ[this.MusicRoomsToShareSourceTo[i]].AudioID, 0);
                        if (this.HTML_UI)
                        {
                            this._HTMLContract.MusicRoomControl[i].musicZoneSelected((sig, wh) => sig.BoolValue = false);
                            this._HTMLContract.MusicRoomControl[i].musicVolEnable((sig, wh) => sig.BoolValue = false);
                            this._HTMLContract.MusicRoomControl[i].musicZoneSource((sig, wh) => sig.StringValue = "Off");
                        }
                        else
                        {
                            this.UserInterface.SmartObjects[7].BooleanInput[(ushort)(i * 7 + 4011)].BoolValue = false;
                            this.UserInterface.SmartObjects[7].BooleanInput[(ushort)(i * 7 + 4016)].BoolValue = false;
                            this.UserInterface.SmartObjects[7].StringInput[(ushort)(i * 2 + 12)].StringValue = _parent.BuildHTMLString(this.Number, "Off", "24");
                        }
                    }
                    this.MusicRoomsToShareCheckbox[i] = false;
                }
            }
            this.SrcSharingButtonFB = false;
            this.UserInterface.BooleanInput[1001].BoolValue = false;
            this.UserInterface.BooleanInput[1002].BoolValue = false;
            this.UserInterface.BooleanInput[998].BoolValue = false;
            this.UserInterface.BooleanInput[999].BoolValue = false;
        }

        private void HandleMusicShareToAll()
        {
            for (ushort i = 0; i < this.MusicRoomsToShareSourceTo.Count; i++)
            {
                ushort roomNumber = this.MusicRoomsToShareSourceTo[i];
                ushort audioSrcNum = _parent.manager.RoomZ[this.CurrentRoomNum].CurrentMusicSrc;
                if (this.HTML_UI)
                {
                    this._HTMLContract.MusicRoomControl[i].musicZoneSelected((sig, wh) => sig.BoolValue = true);
                    this._HTMLContract.MusicRoomControl[i].musicVolEnable((sig, wh) => sig.BoolValue = true);
                    this._HTMLContract.MusicRoomControl[i].musicZoneSource((sig, wh) => sig.StringValue = _parent.manager.MusicSourceZ[audioSrcNum].Name);
                }
                else
                {
                    this.UserInterface.SmartObjects[7].BooleanInput[(ushort)(i * 7 + 4011)].BoolValue = true;
                    this.UserInterface.SmartObjects[7].BooleanInput[(ushort)(i * 7 + 4016)].BoolValue = true;
                    this.UserInterface.SmartObjects[7].StringInput[(ushort)(i * 2 + 12)].StringValue = _parent.BuildHTMLString(this.Number, _parent.manager.MusicSourceZ[audioSrcNum].Name, "24");
                }
                this.MusicRoomsToShareCheckbox[i] = true;
                _parent.musicSystemControl.SwitcherSelectMusicSource(_parent.manager.RoomZ[roomNumber].AudioID, audioSrcNum);
            }
        }

        private void HandleMusicUnshareAll()
        {
            for (ushort i = 0; i < this.MusicRoomsToShareSourceTo.Count; i++)
            {
                ushort roomNumber = this.MusicRoomsToShareSourceTo[i];
                if (this.MusicRoomsToShareCheckbox[i])
                {
                    _parent.musicSystemControl.SwitcherSelectMusicSource(_parent.manager.RoomZ[roomNumber].AudioID, 0);
                    if (this.HTML_UI)
                    {
                        this._HTMLContract.MusicRoomControl[i].musicZoneSelected((sig, wh) => sig.BoolValue = false);
                        this._HTMLContract.MusicRoomControl[i].musicVolEnable((sig, wh) => sig.BoolValue = false);
                        this._HTMLContract.MusicRoomControl[i].musicZoneSource((sig, wh) => sig.StringValue = "Off");
                    }
                    else
                    {
                        this.UserInterface.SmartObjects[7].StringInput[(ushort)(i * 2 + 12)].StringValue = _parent.BuildHTMLString(this.Number, "Off", "24");
                        this.UserInterface.SmartObjects[7].BooleanInput[(ushort)(i * 7 + 4011)].BoolValue = false;
                        this.UserInterface.SmartObjects[7].BooleanInput[(ushort)(i * 7 + 4016)].BoolValue = false;
                    }
                }
                this.MusicRoomsToShareCheckbox[i] = false;
            }
        }

        private void HandleMusicAllOff()
        {
            this.musicButtonFB(0);
            this.musicPageFlips(0);
            this.UserInterface.StringInput[3].StringValue = "Off";
            foreach (var room in _parent.manager.RoomZ)
            {
                if (room.Value.AudioID > 0)
                {
                    _parent.musicSystemControl.SwitcherSelectMusicSource(room.Value.AudioID, 0);
                }
            }
        }
    }
}
