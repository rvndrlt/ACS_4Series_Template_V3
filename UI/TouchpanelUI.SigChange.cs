//-----------------------------------------------------------------------
// <copyright file="TouchpanelUI.SigChange.cs" company="Crestron">
//     Copyright (c) Crestron Electronics. All rights reserved.
// </copyright>
//-----------------------------------------------------------------------
using System;
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
            else if (args.Sig.Type == eSigType.String)
            {
                _parent.manager.ipidToNumberMap.TryGetValue(currentDevice.ID, out ushort tpNumber);
                CrestronConsole.PrintLine("Serial Event: join {0}, TP Number: {1}, Value: \"{2}\"",
                    args.Sig.Number, tpNumber, args.Sig.StringValue);

                // TSR-310 voice/speech recognition result → route to subsystem EISC for Apple TV module Voice_Data
                if (args.Sig.Number == 29000 && this.TSR310 != null && !string.IsNullOrEmpty(args.Sig.StringValue))
                {
                    CrestronConsole.PrintLine("TP-{0} Voice command: \"{1}\"", this.Number, args.Sig.StringValue);
                    ushort eiscJoin = (ushort)((this.Number - 1) * 100 + 2);
                    if (this.Number <= 20)
                    {
                        _parent.subsystemControlEISC.StringInput[eiscJoin].StringValue = args.Sig.StringValue;
                    }
                    else
                    {
                        ushort adjustedJoin = (ushort)(eiscJoin - (20 * 100));
                        _parent.subsystemControlEISC2.StringInput[adjustedJoin].StringValue = args.Sig.StringValue;
                    }
                }
            }
        }

        private void HandleUShortSigChange(BasicTriList currentDevice, SigEventArgs args)
        {
            // Audio source page volume slider (AUDIO_SUB1) — raw analog join 2.
            // Feedback also uses join 2 (UShortInput[2] set by Volume_Sigchange).
            if (args.Sig.Number == 2)
            {
                if (this.CurrentSubsystemIsAudio &&
                    _parent.manager.RoomZ.ContainsKey(this.CurrentRoomNum))
                {
                    ushort audioID = _parent.manager.RoomZ[this.CurrentRoomNum].AudioID;
                    _parent.VOLUMEEISC.UShortInput[audioID].UShortValue = args.Sig.UShortValue;
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
                    ShowVolumePopup(args.Sig.BoolValue);
                    //route to audio volume up
                    if (this.CurrentSubsystemIsAudio)
                    {
                        _parent.musicEISC1.BooleanInput[(ushort)(_parent.manager.RoomZ[this.CurrentRoomNum].AudioID)].BoolValue = args.Sig.BoolValue;
                    }
                    //route to video volume up - always send to EISC, also send to NVX IR if defined
                    else
                    {
                        SendToSubsystemEISC((ushort)(((Number - 1) * 200) + 154), args.Sig.BoolValue);
                        _parent.videoSystemControl.RouteVideoVolumeCommand(this.CurrentDisplayNumber, "volumeUp", args.Sig.BoolValue);
                    }
                }
            }
            else if (args.Sig.Number == 7)
            {
                if (this.TSR310 != null)
                {
                    ShowVolumePopup(args.Sig.BoolValue);
                    //route to audio volume down
                    if (this.CurrentSubsystemIsAudio)
                    {
                        _parent.musicEISC1.BooleanInput[(ushort)(_parent.manager.RoomZ[this.CurrentRoomNum].AudioID + 100)].BoolValue = args.Sig.BoolValue;
                    }
                    //route to video volume down - always send to EISC, also send to NVX IR if defined
                    else
                    {
                        SendToSubsystemEISC((ushort)(((Number - 1) * 200) + 155), args.Sig.BoolValue);
                        _parent.videoSystemControl.RouteVideoVolumeCommand(this.CurrentDisplayNumber, "volumeDown", args.Sig.BoolValue);
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
                    //route to video mute - always send to EISC, also send to NVX IR if defined
                    else
                    {
                        SendToSubsystemEISC((ushort)(((Number - 1) * 200) + 156), true);
                        SendToSubsystemEISC((ushort)(((Number - 1) * 200) + 156), false);
                        _parent.videoSystemControl.RouteVideoVolumeCommand(this.CurrentDisplayNumber, "mute", true);
                    }
                }
            }
            // TSR-310 mic/voice button
            else if (args.Sig.Number == 31 && args.Sig.BoolValue)
            {
                if (this.TSR310 != null)
                {
                    CrestronConsole.PrintLine("TP-{0} Mic button pressed", this.Number);
                    SendToSubsystemEISC((ushort)(((Number - 1) * 200) + 157), true);
                    SendToSubsystemEISC((ushort)(((Number - 1) * 200) + 157), false);
                }
            }
            // Video volume buttons (from iPad/touchpanel) - always send to EISC, also to NVX IR
            else if (args.Sig.Number > 150 && args.Sig.Number < 160)
            {
                SendToSubsystemEISC((ushort)(((Number - 1) * 200) + args.Sig.Number), args.Sig.BoolValue);

                // Also route to NVX IR if applicable
                string volCmd = null;
                if (args.Sig.Number == 154) volCmd = "volumeUp";
                else if (args.Sig.Number == 155) volCmd = "volumeDown";
                else if (args.Sig.Number == 156) volCmd = "mute";

                if (volCmd != null)
                {
                    _parent.videoSystemControl.RouteVideoVolumeCommand(this.CurrentDisplayNumber, volCmd, args.Sig.BoolValue);
                }
            }
            // 160 is the sleep button 180 is the format button
            else if (args.Sig.Number > 180 && args.Sig.Number <= 200)
            {
                SendToSubsystemEISC((ushort)(((Number - 1) * 200) + args.Sig.Number), args.Sig.BoolValue);
            }
            else if (args.Sig.Number > 200 && args.Sig.Number <= 350)
            {
                SendToSubsystemEISC((ushort)(((Number - 1) * 200) + args.Sig.Number - 200), args.Sig.BoolValue);
            }
            else if (args.Sig.Number >= 351 && args.Sig.Number <= 356)
            {
                if (this.TSR310 != null && args.Sig.BoolValue && _parent.channelSettings != null)
                {
                    _parent.channelSettings.HandleChannelButtonPress(this.Number, (ushort)args.Sig.Number);
                }
            }
            else if (args.Sig.Number == 357)
            {
                if (this.TSR310 != null && args.Sig.BoolValue && _parent.channelSettings != null)
                {
                    _parent.channelSettings.HandleMoreButtonPress(this.Number);
                }
            }
            else if (args.Sig.Number > 500 && args.Sig.Number < 510) {
                if (this.TSR310 != null && args.Sig.BoolValue) {
                    HandleTSRVideoSourceSelect(args);
                }
            }
            else if (args.Sig.Number == 510) {
                if (this.TSR310 != null && args.Sig.BoolValue) {
                    this.CurrentVSrcGroupNum++;
                    _parent.SetVSRCGroup(this.Number, this.CurrentVSrcGroupNum);
                }
            }
            else if (args.Sig.Number == 530)
            {
                if (this.TSR310 != null && args.Sig.BoolValue)
                {
                    CrestronConsole.PrintLine("TP-{0} More Audio Sources pressed, group {1}", this.Number, this.CurrentASrcGroupNum + 1);
                    this.CurrentASrcGroupNum++;
                    _parent.SetASRCGroup(this.Number, this.CurrentASrcGroupNum);
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
                SendToSubsystemEISC(eiscPos, args.Sig.BoolValue);
            }
        }

        /// <summary>
        /// Routes a boolean signal to the appropriate subsystem control EISC.
        /// TP 1-20 use subsystemControlEISC (0x9D), TP 21+ use subsystemControlEISC2 (0x9E) with offset reset.
        /// </summary>
        private void SendToSubsystemEISC(ushort eiscPosition, bool value)
        {
            if (this.Number <= 20)
            {
                _parent.subsystemControlEISC.BooleanInput[eiscPosition].BoolValue = value;
            }
            else
            {
                // TP 21+ goes to EISC2 with offset recalculated from TP 21 as "TP 1"
                ushort adjustedPos = (ushort)(eiscPosition - (20 * 200));
                _parent.subsystemControlEISC2.BooleanInput[adjustedPos].BoolValue = value;
            }
        }



        private void ShowVolumePopup(bool buttonPressed)
        {
            // Show the appropriate volume subpage
            ushort volumeJoin = (ushort)(this.CurrentSubsystemIsAudio ? 45 : 44);

            // For video volume (join 44), only show if the config scenario has volume feedback
            if (volumeJoin == 44)
            {
                bool showVolumeFB = false;
                if (this.CurrentDisplayNumber > 0 &&
                    _parent.manager.VideoDisplayZ.ContainsKey(this.CurrentDisplayNumber))
                {
                    ushort vidConfigScenario = _parent.manager.VideoDisplayZ[this.CurrentDisplayNumber].VidConfigurationScenario;
                    if (vidConfigScenario > 0 && _parent.manager.VideoConfigScenarioZ.ContainsKey(vidConfigScenario))
                    {
                        var scenario = _parent.manager.VideoConfigScenarioZ[vidConfigScenario];
                        showVolumeFB = (scenario.HasReceiver && scenario.ReceiverHasVolFB) || scenario.VideoVolThroughDistAudio || scenario.TvHasVolFB;
                    }
                }

                if (!showVolumeFB) return;
            }

            this.UserInterface.BooleanInput[volumeJoin].BoolValue = true;

            // Reset the hide timer
            if (_volumePopupTimer != null)
            {
                _volumePopupTimer.Stop();
                _volumePopupTimer.Dispose();
                _volumePopupTimer = null;
            }

            // If button released, start 1-second hide timer
            if (!buttonPressed)
            {
                ushort capturedJoin = volumeJoin;
                _volumePopupTimer = new CTimer(o =>
                {
                    this.UserInterface.BooleanInput[capturedJoin].BoolValue = false;
                    _volumePopupTimer = null;
                }, 1000);
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
                case 141:
                    _parent.manager.VideoSourceZ[this.CurrentVSrcNum].CurrentSubpageScenario = 1;
                    this.UserInterface.BooleanInput[141].BoolValue = true;
                    this.UserInterface.BooleanInput[142].BoolValue = false;
                    break;
                case 142:
                    _parent.manager.VideoSourceZ[this.CurrentVSrcNum].CurrentSubpageScenario = 2;
                    this.UserInterface.BooleanInput[141].BoolValue = false;
                    this.UserInterface.BooleanInput[142].BoolValue = true;
                    break;
                case 149:
                case 150:
                    this.videoPageFlips(0);
                    this.videoButtonFB(0);
                    _parent.videoSystemControl.SelectVideoSourceFromTP(tpNumber, 0);
                    _parent.musicSystemControl.PanelSelectMusicSource(tpNumber, 0);
                    ushort audioID = _parent.manager.RoomZ[this.CurrentRoomNum].AudioID;
                    _parent.musicSystemControl.SwitcherAudioZoneOff(audioID);
                    if (this.TSR310 != null)
                    {
                        for (ushort i = 0; i < 6; i++)
                        {
                            this.UserInterface.BooleanInput[(ushort)(531 + i)].BoolValue = false;
                        }
                    }
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
            _parent.SetASRCGroup(this.Number, this.CurrentASrcGroupNum);

            // TSR-310: set audio subpage directly since CurrentSubsystemIsAudio may be false
            if (this.TSR310 != null && asrc > 0)
            {
                ushort flipsToPage = _parent.manager.MusicSourceZ[asrc].FlipsToPageNumber;
                for (ushort i = 0; i < 20; i++)
                {
                    this.UserInterface.BooleanInput[(ushort)(i + 1011)].BoolValue = false;
                }
                if (flipsToPage > 0)
                {
                    this.UserInterface.BooleanInput[(ushort)(flipsToPage + 1010)].BoolValue = true;
                }
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
