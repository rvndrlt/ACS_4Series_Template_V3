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
                HandleUShortSigChange(currentDevice, args);
            }
        }

        private void HandleUShortSigChange(BasicTriList currentDevice, SigEventArgs args)
        {
            // Audio source page volume slider (AUDIO_SUB1) — same join as the
            // readback (UShortInput[2] is set by MusicSigChange when the music
            // processor reports volume). Mirror the up/down 1007/1008 routing:
            // those write to musicEISC1.BooleanInput[AudioID] / [AudioID+100],
            // so the absolute set goes to musicEISC1.UShortInput[AudioID].
            if (args.Sig.Number == 2)
            {
                if (this.CurrentSubsystemIsAudio &&
                    _parent.manager.RoomZ.ContainsKey(this.CurrentRoomNum))
                {
                    ushort audioID = _parent.manager.RoomZ[this.CurrentRoomNum].AudioID;
                    _parent.musicEISC1.UShortInput[audioID].UShortValue = args.Sig.UShortValue;
                }
            }
        }

        private void HandleBooleanSigChange(BasicTriList currentDevice, SigEventArgs args)
        {
            //TSR-310 VOLUME
            if (args.Sig.Number == 6)
            {
                if (this.TSR310 != null)
                {

                    //route to audio volume up
                    if (this.CurrentSubsystemIsAudio)
                    {
                        _parent.musicEISC1.BooleanInput[(ushort)(_parent.manager.RoomZ[this.CurrentRoomNum].AudioID)].BoolValue = args.Sig.BoolValue;
                    }
                    //route to video
                    else {
                        _parent.subsystemControlEISC.BooleanInput[(ushort)(((Number - 1) * 200) +154)].BoolValue = args.Sig.BoolValue;
                    }
                }
            }
            else if (args.Sig.Number == 7)
            {
                if (this.TSR310 != null)
                {
                    //route to audio volume down
                    if (this.CurrentSubsystemIsAudio)
                    {
                        _parent.musicEISC1.BooleanInput[(ushort)(_parent.manager.RoomZ[this.CurrentRoomNum].AudioID + 100)].BoolValue = args.Sig.BoolValue;
                    }
                    //route to video
                    else {
                        _parent.subsystemControlEISC.BooleanInput[(ushort)(((Number - 1) * 200) + 155)].BoolValue = args.Sig.BoolValue;
                    }
                }
            }
            else if (args.Sig.Number == 8 && args.Sig.BoolValue)
            {
                if (this.TSR310 != null)
                {
                    //route to audio mute
                    if (this.CurrentSubsystemIsAudio)
                    {
                        _parent.musicEISC1.BooleanInput[(ushort)(_parent.manager.RoomZ[this.CurrentRoomNum].AudioID + 200)].BoolValue = true;
                        _parent.musicEISC1.BooleanInput[(ushort)(_parent.manager.RoomZ[this.CurrentRoomNum].AudioID + 200)].BoolValue = false;
                    }
                    //route to video mute
                    else
                    {
                        _parent.subsystemControlEISC.BooleanInput[(ushort)(((Number - 1) * 200) + 156)].BoolValue = true;
                        _parent.subsystemControlEISC.BooleanInput[(ushort)(((Number - 1) * 200) + 156)].BoolValue = false;
                    }
                }
            }
            // Video volume buttons
            else if (args.Sig.Number > 150 && args.Sig.Number < 160)
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
            else if (args.Sig.Number > 500 && args.Sig.Number < 510) {
                if (this.TSR310 != null && args.Sig.BoolValue) {
                    HandleTSRVideoSourceSelect(args);
                }
            }
            else if (args.Sig.Number > 530 && args.Sig.Number < 540)
            {
                if (this.TSR310 != null && args.Sig.BoolValue)
                {
                    HandleTSRAudioSourceSelect(args);
                }
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
                //CrestronConsole.PrintLine("TP-{0} audioID: {1}", this.Number, _parent.manager.RoomZ[this.CurrentRoomNum].AudioID);
                _parent.musicEISC1.BooleanInput[(ushort)(_parent.manager.RoomZ[this.CurrentRoomNum].AudioID)].BoolValue = args.Sig.BoolValue;
            }
            else if (args.Sig.Number == 1008)
            {
                // Main volume down
                //CrestronConsole.PrintLine("TP-{0} audioID: {1}", this.Number, _parent.manager.RoomZ[this.CurrentRoomNum].AudioID);
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
            CrestronConsole.PrintLine("Boolean Press Event: {0}, TP Number: {1}", args.Sig.Number, tpNumber);
            switch (args.Sig.Number)
            {
                case 2:
                    if (this.TSR310 != null) { 
                        this.TSR310.HomeButtonPress();
                    }
                    break;
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
                    if (this.UserInterface.BooleanInput[21].BoolValue == false)
                    {
                        this.UserInterface.BooleanInput[1021].BoolValue = false;//close the media player menu on the home screen
                    }
                    break;
                case 22://this is the close button on the media player / audio source menu 
                    this.UserInterface.BooleanInput[1021].BoolValue = false;//close the media player menu on the home screen
                    break;
                case 50:
                    HandleChangeRoomButton(tpNumber);//go back to the list of rooms.
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
                    _parent.musicSystemControl.PanelSelectMusicSource(tpNumber, 0);
                    ushort audioID = _parent.manager.RoomZ[this.CurrentRoomNum].AudioID;
                    _parent.musicSystemControl.SwitcherAudioZoneOff(audioID);
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
                    _parent.musicEISC1.BooleanInput[(ushort)(_parent.manager.RoomZ[this.CurrentRoomNum].AudioID + 200)].BoolValue = true;//mute
                    _parent.musicEISC1.BooleanInput[(ushort)(_parent.manager.RoomZ[this.CurrentRoomNum].AudioID + 200)].BoolValue = false;
                    break;
                case 1501:
                    // Open "Add room to this group" menu for the slot index staged on analog 1500.
                    _parent.OpenAddToGroupMenu(tpNumber, this.UserInterface.UShortOutput[1500].UShortValue);
                    break;
                case 1502:
                    _parent.CloseAddToGroupMenu(tpNumber);
                    break;
                case 1504:
                    // Open "Change music source for this group" menu; slot index on analog 1501.
                    _parent.OpenChangeGroupSourceMenu(tpNumber, this.UserInterface.UShortOutput[1501].UShortValue);
                    break;
                case 1505:
                    _parent.CloseChangeGroupSourceMenu(tpNumber);
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
            _parent.UpdateRoomListNameAndImage(tpNumber);//from 'HandleChangeRoomButton'
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
        private void HandleTSRVideoSourceSelect(SigEventArgs args)
        {
            ushort videoSourceButtonNum = (ushort)(args.Sig.Number - 500);
            _parent.videoSystemControl.SelectVideoSourceFromTP(this.Number, videoSourceButtonNum);//from TSR-310
        }
        private void HandleTSRAudioSourceSelect(SigEventArgs args)
        {
            ushort audioSourceButtonNum = (ushort)(args.Sig.Number - 530);
            ushort asrc = _parent.TranslateButtonNumberToASrc(Number, audioSourceButtonNum);
            ushort audioID = _parent.manager.RoomZ[this.CurrentRoomNum].AudioID;
            _parent.musicSystemControl.PanelSelectMusicSource(Number, asrc);//from the music sources smart object
            _parent.musicSystemControl.SwitcherSelectMusicSource(audioID,asrc);//from the music sources smart object
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
                _parent.musicSystemControl.BeginSuppressRebuild();
                try
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
                finally
                {
                    _parent.musicSystemControl.EndSuppressRebuild();
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
            _parent.musicSystemControl.BeginSuppressRebuild();
            try
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
            finally
            {
                _parent.musicSystemControl.EndSuppressRebuild();
            }
        }

        private void HandleMusicAllOff()
        {
            this.musicButtonFB(0);
            this.musicPageFlips(0);
            this.UserInterface.StringInput[3].StringValue = "Off";
            _parent.musicSystemControl.BeginSuppressRebuild();
            try
            {
                foreach (var room in _parent.manager.RoomZ)
                {
                    if (room.Value.AudioID > 0)
                    {
                        _parent.musicSystemControl.SwitcherSelectMusicSource(room.Value.AudioID, 0);
                    }
                }
            }
            finally
            {
                _parent.musicSystemControl.EndSuppressRebuild();
            }
        }
    }
}
