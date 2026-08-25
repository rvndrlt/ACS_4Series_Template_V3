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
            ResetIdleTimer();   // any panel signal counts as activity
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
                //CrestronConsole.PrintLine("Serial Event: join {0}, TP Number: {1}, Value: \"{2}\"",
                    //args.Sig.Number, tpNumber, args.Sig.StringValue);

                // Quick-actions command channel (raw serial 1531, JSON) from HTML panels
                if (args.Sig.Number == QuickActions.QuickActionManager.CommandJoin && this.HTML_UI)
                {
                    _parent.quickActionManager.HandleCommand(this.Number, args.Sig.StringValue);
                    return;
                }

                // Music menu state report (raw serial 1527, JSON) from HTML panels.
                // The panel telling us what is actually on screen — see TouchpanelUI.Menus.cs.
                if (args.Sig.Number == MenuStateJoin && this.HTML_UI)
                {
                    HandleMenuState(args.Sig.StringValue);
                    return;
                }

                // Cameras select channel (raw serial 1542, JSON) from HTML panels
                if (args.Sig.Number == Cameras.CameraManager.SelectJoin && this.HTML_UI)
                {
                    _parent.cameraManager.HandleSelect(this.Number, args.Sig.StringValue);
                    return;
                }

                // Intercom command channel (raw serial 1561, JSON) from HTML panels
                if (args.Sig.Number == Intercom.IntercomManager.CommandJoin && this.HTML_UI
                    && _parent.intercomManager != null)
                {
                    _parent.intercomManager.HandleCommand(this.Number, args.Sig.StringValue);
                    return;
                }

                // ch5-video diagnostics from the Cameras page (raw serials 1552/1553)
                if (this.HTML_UI && _parent.cameraManager != null
                    && Cameras.CameraManager.IsVideoDiagSerialJoin(args.Sig.Number))
                {
                    _parent.cameraManager.LogVideoDiag(this.Number, args.Sig.Number, args.Sig.StringValue);
                    return;
                }

                // ch5-video diagnostics from the Intercom page (raw serials 1568/1569).
                // Its own joins, not the Cameras ones, so the console line names the page
                // the stream actually belongs to.
                if (this.HTML_UI && _parent.intercomManager != null
                    && Intercom.IntercomManager.IsVideoDiagSerialJoin(args.Sig.Number))
                {
                    _parent.intercomManager.LogVideoDiag(this.Number, args.Sig.Number, args.Sig.StringValue);
                    return;
                }

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
            // ch5-video diagnostics from the Cameras page (raw analogs 1550/1551/1554).
            if (this.HTML_UI && _parent.cameraManager != null
                && Cameras.CameraManager.IsVideoDiagAnalogJoin(args.Sig.Number))
            {
                _parent.cameraManager.LogVideoDiag(this.Number, args.Sig.Number, args.Sig.UShortValue.ToString());
                return;
            }

            // ch5-video diagnostics from the Intercom page (raw analogs 1566/1567/1570).
            if (this.HTML_UI && _parent.intercomManager != null
                && Intercom.IntercomManager.IsVideoDiagAnalogJoin(args.Sig.Number))
            {
                _parent.intercomManager.LogVideoDiag(this.Number, args.Sig.Number, args.Sig.UShortValue.ToString());
                return;
            }

            // Intercom panel-speaker volume slider (raw analog 1564) from HTML panels.
            if (this.HTML_UI && _parent.intercomManager != null
                && args.Sig.Number == Intercom.IntercomManager.VolumeSetJoin)
            {
                _parent.intercomManager.HandleVolume(this.Number, args.Sig.UShortValue);
                return;
            }

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

        // HTML "page ready" pull. The HTML app pulses this digital join when it (re)loads to
        // ask the program to (re)drive its page. Needed because a CH5/HTML-only reload does NOT
        // raise a panel offline->online event (the CIP device stays online), so the reconnect
        // path (ConnectionStatusChange -> StartupPanel -> GoToDefaultPage) never fires and the
        // top-level home/room page bools — reset to false by the reload — are never re-asserted,
        // leaving the panel blank until a full program restart.
        private const ushort PageReadyJoin = 1521;
        private DateTime _lastPageReady = DateTime.MinValue;

        private void HandleBooleanSigChange(BasicTriList currentDevice, SigEventArgs args)
        {
            // HTML page-ready pull (see PageReadyJoin). Handle before anything else and return.
            if (args.Sig.Number == PageReadyJoin && this.HTML_UI && args.Sig.BoolValue)
            {
                // Debounce: the HTML retries a few times to beat the connect race, and each
                // retry that lands would otherwise re-drive the page redundantly.
                if ((DateTime.Now - _lastPageReady).TotalMilliseconds >= 1500)
                {
                    _lastPageReady = DateTime.Now;
                    CrestronConsole.PrintLine(LogHeader + "TP-{0} HTML page-ready -> GoToDefaultPage", this.Number);

                    // A reload wipes every menu off the screen without the panel ever
                    // reporting them closed — it has no memory of having opened them. Without
                    // this, the open-set keeps entries for menus that no longer exist and
                    // AnyMusicMenuOpen() suppresses the media player forever. The panel is
                    // showing nothing at this instant, so the empty set is the truth.
                    ForgetOpenMenus();

                    _parent.GoToDefaultPage(this.Number, true);
                }
                return;
            }
            //TSR-310 VOLUME — joins 6 (up), 7 (down), 8 (mute).
            //Target (audio vs video) comes from RouteVolume, NOT from CurrentSubsystemIsAudio:
            //that flag is cleared by navigation, so browsing to Lights used to silently hand the
            //buttons to video. See ControlSystem.ResolveVolumeTargetIsAudio.
            if (args.Sig.Number == 6)
            {
                if (this.TSR310 != null)
                {
                    ShowVolumePopup(args.Sig.BoolValue);
                    RouteVolume(eVolumeCommand.Up, args.Sig.BoolValue);
                }
            }
            else if (args.Sig.Number == 7)
            {
                if (this.TSR310 != null)
                {
                    ShowVolumePopup(args.Sig.BoolValue);
                    RouteVolume(eVolumeCommand.Down, args.Sig.BoolValue);
                }
            }
            else if (args.Sig.Number == 8 && args.Sig.BoolValue)
            {
                if (this.TSR310 != null)
                {
                    RouteVolume(eVolumeCommand.Mute, true);
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
            else if (args.Sig.Number >= 351 && args.Sig.Number <= 361)
            {
                if (this.TSR310 != null && args.Sig.BoolValue && _parent.channelSettings != null && args.Sig.Number <= 356)
                {
                    _parent.channelSettings.HandleChannelButtonPress(this.Number, (ushort)args.Sig.Number);
                }
                else if (this.HTML_UI && args.Sig.BoolValue)
                {
                    // Join 351 (open/close the select-display panel) is no longer sent by HTML
                    // panels — that is local state now, like the lift/format/sleep dropdowns,
                    // and its contents are kept fresh by UpdateDisplaysAvailableForSelection on
                    // every room/source change rather than on open. Only the SELECTION
                    // (352-361) still reaches the program, because picking a display is a real
                    // command. The panel closes its own menu afterwards.
                    if (args.Sig.Number > 351)
                    {
                        // Display selection buttons (joins 352-361 → button number 1-10)
                        ushort displayButtonNumber = (ushort)(args.Sig.Number - 351);
                        _parent.videoSystemControl.SelectDisplay(this.Number, displayButtonNumber);
                    }
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
                HandleSubsystemButtons(args);//600 to 700 subsystem buttons
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
            // TSR-310 Lighting: Scene select (1101-1110)
            else if (args.Sig.Number >= 1101 && args.Sig.Number <= 1110)
            {
                CrestronConsole.PrintLine("TP-{0} Lighting scene join {1}, BoolValue={2}, TSR310={3}, lightingCtrl={4}",
                    this.Number, args.Sig.Number, args.Sig.BoolValue,
                    this.TSR310 != null ? "yes" : "no",
                    _parent.lightingScenario2Control != null ? "yes" : "no");
                if (this.TSR310 != null && args.Sig.BoolValue && _parent.lightingScenario2Control != null)
                {
                    int sceneIndex = (int)(args.Sig.Number - 1101);
                    _parent.lightingScenario2Control.TSRSceneSelect(this.Number, sceneIndex);
                }
            }
            // TSR-310 Lighting: House scene recall (1111-1120)
            else if (args.Sig.Number >= 1111 && args.Sig.Number <= 1120)
            {
                CrestronConsole.PrintLine("TP-{0} Lighting house scene join {1}, BoolValue={2}, TSR310={3}, lightingCtrl={4}",
                    this.Number, args.Sig.Number, args.Sig.BoolValue,
                    this.TSR310 != null ? "yes" : "no",
                    _parent.lightingScenario2Control != null ? "yes" : "no");
                if (this.TSR310 != null && args.Sig.BoolValue && _parent.lightingScenario2Control != null)
                {
                    int houseSceneIndex = (int)(args.Sig.Number - 1111);
                    _parent.lightingScenario2Control.TSRHouseSceneRecall(this.Number, houseSceneIndex);
                }
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
                SendToSubsystemEISC(eiscPos, args.Sig.BoolValue);//from HandleSubsystemButtons
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
            // Show the appropriate volume subpage. Must use the SAME resolver as RouteVolume,
            // otherwise the popup can show the audio bar while the buttons ramp video.
            bool? toAudio = _parent.ResolveVolumeTargetIsAudio(this.Number);
            if (toAudio == null) return;
            ushort volumeJoin = (ushort)(toAudio.Value ? 45 : 44);

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
                    SendMenuCommand(MenuHomeMusicControl, this.UserInterface.BooleanInput[21].BoolValue);
                    if (this.UserInterface.BooleanInput[21].BoolValue == false)
                    {
                        ClearMusicSourcePage();//close the media player menu on the home screen
                    }
                    break;
                case 22://this is the close button on the media player / audio source menu
                    ClearMusicSourcePage();//close the media player menu on the home screen
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
                // Source main/keypad sub-page selection — DUMB PANELS ONLY.
                // On HTML panels this is a local VIEW owned by pageRouter.js and never reaches
                // the program (the ch5-tab-button contract bindings were removed from the source
                // snippets, which is what actually severs the path; this guard is belt-and-braces
                // against any other panel type driving these joins). See
                // PAGE-FLIP-DESCRIPTOR-PLAN.md, Phase 3.
                case 141:
                    if (this.HTML_UI) break;
                    _parent.manager.VideoSourceZ[this.CurrentVSrcNum].CurrentSubpageScenario = 1;
                    this.UserInterface.BooleanInput[141].BoolValue = true;
                    this.UserInterface.BooleanInput[142].BoolValue = false;
                    break;
                case 142:
                    if (this.HTML_UI) break;
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

        /// <summary>
        /// Physical hard-key presses on TSW-x60 hardware panels arrive here (subscribed in
        /// Register()), separate from the UI joins. The Home hard key drives the exact same
        /// navigation as the on-screen bottom-bar Home button — HandleHomeButton — so the two
        /// behave identically. Only the Pressed edge is acted on (ignore Released/Held/etc.) so
        /// Home fires once per press. Other hard keys are left alone here.
        /// </summary>
        private void HardKey_StateChange(GenericBase device, ButtonEventArgs args)
        {
            ResetIdleTimer(); // a hard-key press counts as user activity
            if (args == null || args.Button == null) return;

            bool pressed = args.NewButtonState == eButtonState.Pressed;
            bool released = args.NewButtonState == eButtonState.Released;

            switch (args.Button.Name)
            {
                case eButtonName.Home:
                    // Same navigation as the on-screen bottom-bar Home button. Press edge only.
                    if (pressed)
                    {
                        CrestronConsole.PrintLine("TP-{0} HARD Home key -> HandleHomeButton", this.Number);
                        HandleHomeButton(this.Number);
                    }
                    break;

                case eButtonName.VolumeUp:
                    // Ramp needs both edges: press starts, release stops.
                    if (pressed || released) { RouteVolume(eVolumeCommand.Up, pressed); }
                    break;

                case eButtonName.VolumeDown:
                    if (pressed || released) { RouteVolume(eVolumeCommand.Down, pressed); }
                    break;

                case eButtonName.Power:
                    // Show the power-off menu for the current/on subsystem (video priority).
                    if (pressed) { _parent.HardKeyPower(this.Number); }
                    break;

                case eButtonName.Lights:
                    // Flip to the current room's lighting page.
                    if (pressed) { _parent.HardKeyLights(this.Number); }
                    break;
            }
        }

        private enum eVolumeCommand { Up, Down, Mute }

        /// <summary>
        /// Route one volume command to audio or video for this panel's current room.
        ///
        /// Single entry point for BOTH volume input paths — the TSR-310 raw joins 6/7/8 and the
        /// hard-key ButtonStateChange path — so they can no longer disagree about the target.
        /// The audio/video decision belongs entirely to ControlSystem.ResolveVolumeTargetIsAudio
        /// (room capability -> only-one-on -> room's sticky last selection).
        ///
        /// `active` carries the press/release edge for the ramps (Up/Down); Mute ignores it and
        /// pulses. Joins are unchanged: music = musicEISC1 AudioID / +100 / +200 (mirrors 1007/1008);
        /// video = subsystem EISC 154/155/156 + NVX IR.
        /// </summary>
        private void RouteVolume(eVolumeCommand cmd, bool active)
        {
            bool? toAudio = _parent.ResolveVolumeTargetIsAudio(this.Number);
            if (toAudio == null) return;// room has neither audio nor video -> ignore the press

            if (toAudio.Value)
            {
                ushort audioID = _parent.manager.RoomZ[this.CurrentRoomNum].AudioID;
                if (cmd == eVolumeCommand.Mute)
                {
                    _parent.musicEISC1.BooleanInput[(ushort)(audioID + 200)].BoolValue = true;
                    _parent.musicEISC1.BooleanInput[(ushort)(audioID + 200)].BoolValue = false;
                }
                else
                {
                    ushort join = (ushort)(audioID + (cmd == eVolumeCommand.Up ? 0 : 100));
                    _parent.musicEISC1.BooleanInput[join].BoolValue = active;
                }
            }
            else
            {
                ushort baseJoin = (ushort)((this.Number - 1) * 200);
                if (cmd == eVolumeCommand.Mute)
                {
                    SendToSubsystemEISC((ushort)(baseJoin + 156), true);
                    SendToSubsystemEISC((ushort)(baseJoin + 156), false);
                    _parent.videoSystemControl.RouteVideoVolumeCommand(this.CurrentDisplayNumber, "mute", true);
                }
                else
                {
                    bool up = cmd == eVolumeCommand.Up;
                    SendToSubsystemEISC((ushort)(baseJoin + (up ? 154 : 155)), active);
                    _parent.videoSystemControl.RouteVideoVolumeCommand(this.CurrentDisplayNumber, up ? "volumeUp" : "volumeDown", active);
                }
            }
        }

        private void HandleHomeButton(ushort tpNumber)
        {
            this.CurrentPageNumber = 0;
            // Prevent stale whole-house zone callback from reopening last subsystem page.
            // Time-boxed: only swallows an echo arriving within SuppressWholeHouseZoneWindowMs of
            // this Home press, so the user's first real room tap (seconds later) is not eaten.
            this.SuppressNextWholeHouseZoneFlip = true;
            this.SuppressWholeHouseZoneArmedAt = DateTime.Now;
            this.CurrentSubsystemNumber = 0;
            this.CurrentSubsystemIsLights = false;
            this.CurrentSubsystemIsShades = false;
            this.CurrentSubsystemIsClimate = false;
            this.CurrentSubsystemIsAudio = false;
            this.CurrentSubsystemIsVideo = false;

            _parent.HomeButtonPress(tpNumber);
            this.UserInterface.BooleanInput[11].BoolValue = true;
            this.UserInterface.BooleanInput[12].BoolValue = false;
            this.UserInterface.BooleanInput[998].BoolValue = false;
            this.UserInterface.BooleanInput[999].BoolValue = false;
            this.UserInterface.BooleanInput[1002].BoolValue = false;
            this.UserInterface.BooleanInput[21].BoolValue = false;
            CloseAllMusicMenus();   // HTML equivalent of the four joins above
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
            CloseAllMusicMenus();   // HTML equivalent of the four joins above
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
            CloseAllMusicMenus();   // HTML equivalent of the four joins above
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
                SendMenuCommand(MenuShareSource, false);
                UnsubscribeFromMusicSharingChanges();
            }
            else
            {
                _sharingMenuTimer = new CTimer(HideSharingMenu, null, 60000, -1);
                // Which LAYOUT the sharing list uses. The scenario > 50 test is the program's
                // to make (it comes from the room's AudioSrcSharingScenario), so it travels as
                // the descriptor's `variant`; whether the list is showing is not.
                bool withFloors = this.CurrentSubsystemIsAudio && asrcSharingScenario > 50;
                this.UserInterface.BooleanInput[998].BoolValue = !withFloors;
                this.UserInterface.BooleanInput[999].BoolValue = withFloors;
                SendMenuCommand(MenuShareSource, true, withFloors ? "floors" : "nofloors");
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

            //actually route the current room's zone off: SwitcherAudioZoneOff sends the analog 0
            //to musicEISC1 (+500) and clears the multicast (+300). Without this the UI shows "Off"
            //but the zone never turns off. Mirrors the subsystem-off path (case 149/150).
            ushort audioID = _parent.manager.RoomZ[this.CurrentRoomNum].AudioID;
            _parent.musicSystemControl.SwitcherAudioZoneOff(audioID);

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
            SendMenuCommand(MenuShareSource, false);
        }

        /// <summary>
        /// "Share to all" — push the current room's music source into every room in the
        /// sharing list.
        ///
        /// ⚠ THE SOURCE IS CAPTURED ONCE, BEFORE THE LOOP, AND MUST STAY THAT WAY.
        ///
        /// It used to be re-read from RoomZ[CurrentRoomNum].CurrentMusicSrc on every
        /// iteration, which is a read of live state that SHARING ITSELF CAN CHANGE. When the
        /// source lives on the same NAX box as the origin room, that room plays it on a plain
        /// switcher input; sharing it to a room on a different box forces the box to start
        /// transmitting it as an AES67 stream, and the switcher then reports the origin room's
        /// input as changed. That feedback lands in updateMusicSourceInUse, which writes
        /// CurrentMusicSrc for the origin room — mid-loop. The next iteration read 0, indexed
        /// MusicSourceZ[0], threw KeyNotFoundException, and the loop died after ONE room.
        ///
        /// Silently: there is no try/catch in this file, so the exception unwound into the CIP
        /// sig-change callback and reached the error log, never the console. The observed
        /// symptom was "Share To All shares to Master Bed and then stops" for a music-server
        /// source, while AirPlay worked — because AirPlay was already streaming everywhere, so
        /// nothing re-routed and no feedback fired.
        ///
        /// Which source is being shared is decided when the button is pressed. It is not a
        /// live value, and re-reading it was the bug.
        /// </summary>
        private void HandleMusicShareToAll()
        {
            ushort audioSrcNum = _parent.manager.RoomZ[this.CurrentRoomNum].CurrentMusicSrc;
            if (audioSrcNum == 0 || !_parent.manager.MusicSourceZ.ContainsKey(audioSrcNum))
            {
                CrestronConsole.PrintLine("TP-{0} ShareToAll: room {1} has no valid music source ({2}) - nothing to share",
                    this.Number, this.CurrentRoomNum, audioSrcNum);
                return;
            }
            string audioSrcName = _parent.manager.MusicSourceZ[audioSrcNum].Name;

            for (ushort i = 0; i < this.MusicRoomsToShareSourceTo.Count; i++)
            {
                ushort roomNumber = this.MusicRoomsToShareSourceTo[i];
                if (!_parent.manager.RoomZ.ContainsKey(roomNumber))
                {
                    // A room in the sharing scenario that no longer exists in the config must
                    // cost that room only, not every room after it in the list.
                    CrestronConsole.PrintLine("TP-{0} ShareToAll: unknown room {1} in sharing list - skipping",
                        this.Number, roomNumber);
                    continue;
                }

                if (this.HTML_UI)
                {
                    this._HTMLContract.MusicRoomControl[i].musicZoneSelected((sig, wh) => sig.BoolValue = true);
                    this._HTMLContract.MusicRoomControl[i].musicVolEnable((sig, wh) => sig.BoolValue = true);
                    this._HTMLContract.MusicRoomControl[i].musicZoneSource((sig, wh) => sig.StringValue = audioSrcName);
                }
                else
                {
                    this.UserInterface.SmartObjects[7].BooleanInput[(ushort)(i * 7 + 4011)].BoolValue = true;
                    this.UserInterface.SmartObjects[7].BooleanInput[(ushort)(i * 7 + 4016)].BoolValue = true;
                    this.UserInterface.SmartObjects[7].StringInput[(ushort)(i * 2 + 12)].StringValue = _parent.BuildHTMLString(this.Number, audioSrcName, "24");
                }
                this.MusicRoomsToShareCheckbox[i] = true;

                // Per-room, because ONE room must never cost the rest of the list.
                //
                // This is not hypothetical caution: a config with more includedSources than
                // receiverInputs threw out of ReceiverOnOffFromDistAudio here, and with no
                // handler anywhere above this loop the exception unwound into the CIP
                // sig-change callback — so "Share To All" turned on the first room and
                // silently abandoned the other eight. Catch narrowly, log loudly, keep going.
                try
                {
                    _parent.musicSystemControl.SwitcherSelectMusicSource(_parent.manager.RoomZ[roomNumber].AudioID, audioSrcNum);
                }
                catch (Exception ex)
                {
                    CrestronConsole.PrintLine("TP-{0} ShareToAll: room {1} ({2}) FAILED - {3}",
                        this.Number, roomNumber, _parent.manager.RoomZ[roomNumber].Name, ex.Message);
                    ErrorLog.Error("TP-{0} ShareToAll room {1} failed: {2}", this.Number, roomNumber, ex);
                }
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
