using ACS_4Series_Template_V3.Music;
using ACS_4Series_Template_V3.DmReceiver;
using ACS_4Series_Template_V3.UI;
using Ch5_Sample_Contract.Subsystem;
using Crestron.SimplSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ACS_4Series_Template_V3.Video
{
    public class VideoSystemControl
    {
        private ControlSystem _parent;
        // Track which displays are currently powered on (keyed by displayNumber)
        private Dictionary<ushort, bool> _displayPowerState = new Dictionary<ushort, bool>();
        // Track active display power-on timers to prevent leaks (keyed by displayNumber)
        private Dictionary<ushort, List<CTimer>> _displayTimers = new Dictionary<ushort, List<CTimer>>();

        public VideoSystemControl(ControlSystem parent)
        {
            _parent = parent;
        }

        /// <summary>
        /// Finds the DmNVXreceiver whose DmOutputNumber matches the given videoOutputNum.
        /// Returns null if no match or if dmDestinationZ is empty.
        /// </summary>
        private DmNVXreceiver FindReceiverByOutputNum(ushort videoOutputNum)
        {
            if (_parent.manager.dmDestinationZ == null) return null;
            foreach (var kvp in _parent.manager.dmDestinationZ)
            {
                if (kvp.Value.DmOutputNumber == videoOutputNum)
                    return kvp.Value;
            }
            return null;
        }

        /// <summary>
        /// Handles display power on + input switching logic.
        /// If display is off: sends power on, waits displayInputDelay seconds, then sends input command.
        /// If display is already on: sends input command immediately.
        /// </summary>
        private void RouteDisplayInput(ushort displayNumber, ushort videoOutputNum, ushort displayInput, ushort displayInputDelay)
        {
            var receiver = FindReceiverByOutputNum(videoOutputNum);
            if (receiver == null || receiver.DisplayControl == null) return;

            string inputCommandKey = "inputHdmi" + displayInput;
            bool isCurrentlyOn = _displayPowerState.ContainsKey(displayNumber) && _displayPowerState[displayNumber];

            if (isCurrentlyOn)
            {
                // Already on — send input command immediately
                CrestronConsole.PrintLine("[DisplayControl] {0} already on, sending {1}", receiver.Name, inputCommandKey);
                receiver.SendDisplayCommand(inputCommandKey);
            }
            else
            {
                // Power on first, then send input after delay
                CrestronConsole.PrintLine("[DisplayControl] {0} powering on (3x), input {1} in {2}s", receiver.Name, inputCommandKey, displayInputDelay);
                receiver.SendDisplayCommand("powerOn");
                _displayPowerState[displayNumber] = true;

                // Dispose any previous timers for this display
                if (_displayTimers.ContainsKey(displayNumber))
                {
                    foreach (var old in _displayTimers[displayNumber])
                    {
                        old.Stop();
                        old.Dispose();
                    }
                }
                var timers = new List<CTimer>();
                _displayTimers[displayNumber] = timers;

                // Send powerOn 2 more times, spaced 2 seconds apart
                timers.Add(new CTimer(o => receiver.SendDisplayCommand("powerOn"), 2000));
                timers.Add(new CTimer(o => receiver.SendDisplayCommand("powerOn"), 4000));

                // Send input command after the configured delay
                timers.Add(new CTimer(o =>
                {
                    CrestronConsole.PrintLine("[DisplayControl] {0} sending delayed {1}", receiver.Name, inputCommandKey);
                    receiver.SendDisplayCommand(inputCommandKey);
                    // Clean up all timers for this display once the last one fires
                    if (_displayTimers.ContainsKey(displayNumber))
                    {
                        foreach (var t in _displayTimers[displayNumber])
                        {
                            t.Stop();
                            t.Dispose();
                        }
                        _displayTimers.Remove(displayNumber);
                    }
                }, (long)displayInputDelay * 1000));
            }
        }

        /// <summary>
        /// Sends power off to the display via the NVX receiver.
        /// </summary>
        private void PowerOffDisplay(ushort displayNumber, ushort videoOutputNum)
        {
            var receiver = FindReceiverByOutputNum(videoOutputNum);
            if (receiver == null || receiver.DisplayControl == null) return;

            CrestronConsole.PrintLine("[DisplayControl] {0} powering off", receiver.Name);
            receiver.SendDisplayCommand("powerOff");
            _displayPowerState[displayNumber] = false;
        }

        /// <summary>
        /// Routes a video volume command to the NVX receiver's IR/serial if the display has volume control.
        /// Always sends to EISC regardless. Additionally sends to the NVX IR only when the display itself is
        /// the device making sound: no external receiver, and video audio is not on the distributed audio system.
        /// Supports press-and-hold ramping: press (value=true) starts repeating the command, release (value=false) stops it.
        /// </summary>
        public void RouteVideoVolumeCommand(ushort displayNumber, string commandKey, bool value)
        {
            if (displayNumber == 0 || !_parent.manager.VideoDisplayZ.ContainsKey(displayNumber)) return;

            ushort vidConfigScenario = _parent.manager.VideoDisplayZ[displayNumber].VidConfigurationScenario;
            if (vidConfigScenario == 0 || !_parent.manager.VideoConfigScenarioZ.ContainsKey(vidConfigScenario)) return;

            // Only route to display IR when there's no external receiver handling volume
            if (_parent.manager.VideoConfigScenarioZ[vidConfigScenario].HasReceiver) return;

            // When video audio runs through distributed audio, the music/video zone is the volume target,
            // not the TV. Sending IR here fights the zone ramp (making it jumpy) and floods the console with
            // missing-command warnings for displays whose volume commands were deliberately removed.
            if (_parent.manager.VideoConfigScenarioZ[vidConfigScenario].VideoVolThroughDistAudio) return;

            ushort videoOutputNum = _parent.manager.VideoDisplayZ[displayNumber].VideoOutputNum;
            var receiver = FindReceiverByOutputNum(videoOutputNum);
            if (receiver == null || !receiver.HasVolumeControl) return;

            if (value)
            {
                receiver.StartVolumeCommand(commandKey);
            }
            else
            {
                receiver.StopVolumeCommand();
            }
        }

        public void SelectDisplayVideoSource(ushort displayNumber, ushort sourceButtonNumber)
        {
            if (displayNumber > 0)
            {
                //CrestronConsole.PrintLine("display {0} {1} buttonnum {2}", displayNumber, _parent.manager.VideoDisplayZ[displayNumber].DisplayName, sourceButtonNumber);
                ushort videoSwitcherOutputNum = _parent.manager.VideoDisplayZ[displayNumber].VideoOutputNum;
                ushort vidConfigScenario = _parent.manager.VideoDisplayZ[displayNumber].VidConfigurationScenario;
                ushort currentRoomNum = _parent.manager.VideoDisplayZ[displayNumber].AssignedToRoomNum;
                // A display may be assigned to a room number that does not exist in RoomZ on purpose
                // — that is how AVRs and tied DM outputs are modelled (see PlaceholderRoom note in
                // ControlSystem.Video.cs). Everything below is room-level state, so there is nothing
                // meaningful to do for such a display. Bail instead of throwing.
                if (!_parent.manager.RoomZ.ContainsKey(currentRoomNum))
                {
                    CrestronConsole.PrintLine("SelectDisplayVideoSource: display {0} is assigned to placeholder room {1} - skipping room updates", displayNumber, currentRoomNum);
                    return;
                }
                ushort audioSwitcherOutputNum = _parent.manager.RoomZ[currentRoomNum].AudioID;//music zone
                ushort videoAudioID = _parent.GetVideoAudioID(currentRoomNum);//video zone (== music zone when no separate TV output)
                bool independentAudio = _parent.HasIndependentVideoAudio(currentRoomNum);
                ushort vsrcScenario = _parent.manager.VideoDisplayZ[displayNumber].VideoSourceScenario;

                ushort currentVSRC = 0;
                //OFF
                if (sourceButtonNumber == 0)
                {
                    _parent.videoEISC1.UShortInput[(ushort)(videoSwitcherOutputNum + 600)].UShortValue = 0;//display input
                    _parent.videoEISC1.UShortInput[(ushort)(videoSwitcherOutputNum + 700)].UShortValue = 0;//receiver input
                    _parent.videoEISC1.UShortInput[(ushort)(videoSwitcherOutputNum + 800)].UShortValue = 0;//alt switcher input
                    // Deliberately NOT clearing the NVX stream location on off. Dropping the
                    // subscription is what black-screens any other decoder still watching this
                    // same source; powering the display off is enough to blank this TV.
                    //_parent.videoEISC2.StringInput[(ushort)(videoSwitcherOutputNum + 200)].StringValue = "0.0.0.0"; //clear the NVX multicast address
                    _parent.videoEISC2.UShortInput[(ushort)(displayNumber + 400)].UShortValue = 0;
                    _parent.manager.VideoDisplayZ[displayNumber].CurrentVideoSrc = 0;//clear the current source for the display
                    _parent.manager.RoomZ[currentRoomNum].UpdateVideoSrcStatus(0);//from selectDisplayVideoSource

                    // Stream deliberately left subscribed on off (see the note above); only the
                    // display is powered down.
                    //var offReceiver = FindReceiverByOutputNum(videoSwitcherOutputNum);
                    //if (offReceiver != null) offReceiver.SetStreamLocation("0.0.0.0");

                    // Send power off to display via NVX receiver
                    PowerOffDisplay(displayNumber, videoSwitcherOutputNum);
                    //in this case since 1 display is turning off the multi display should no longer be 'ON'
                    if (_parent.manager.RoomZ[currentRoomNum].NumberOfDisplays > 1)
                    {
                        //find the multi-display
                        foreach (var disp in _parent.manager.VideoDisplayZ)
                        {
                            if (disp.Value.AssignedToRoomNum == currentRoomNum && disp.Value.TieToDisplayNumbers[0] > 0)//found a multi display in this room
                            {
                                disp.Value.CurrentVideoSrc = 0;
                            }
                        }
                    }
                    //turn off the audio or update the current source in the room
                    if (vidConfigScenario > 0)
                    {
                        if (_parent.manager.RoomZ[currentRoomNum].NumberOfDisplays == 1 || _parent.AreAllDisplaysOffInThisRoom(currentRoomNum))
                        {

                            if (_parent.manager.VideoConfigScenarioZ[vidConfigScenario].VideoVolThroughDistAudio)
                            {
                                _parent.musicSystemControl.SwitcherAudioZoneOff(videoAudioID);//turn the video audio zone off (music zone untouched when independent)
                            }
                        }
                        else
                        {
                            ChangeCurrentSourceWhenAMultiDisplayGoesOff(displayNumber);
                        }
                    }

                }
                //select the source
                else
                {
                    ushort adjustedButtonNum = (ushort)(sourceButtonNumber - 1);//this is for a handheld using analog mode buttons 6 per page and shouldn't affect other panels
                                                                                //if this room has a receiver and the music is through the receiver then turn the music off
                    if (!independentAudio && vidConfigScenario > 0 && _parent.manager.VideoConfigScenarioZ[vidConfigScenario].HasReceiver && _parent.manager.VideoConfigScenarioZ[vidConfigScenario].MusicThroughReceiver > 0)
                    {
                        _parent.musicSystemControl.SwitcherAudioZoneOff(audioSwitcherOutputNum);//turn the switcher output off
                    }
                    //this will work for panels that don't use the 6 per page analog modes because srcGroup will always be 1


                    currentVSRC = _parent.manager.VideoSrcScenarioZ[vsrcScenario].IncludedSources[adjustedButtonNum];

                    // Set the current video source for the room via UpdateVideoSrcStatus, NOT by
                    // assigning CurrentVideoSrc directly: that is a plain auto-property, so a direct
                    // write updates the number and nothing else — no VideoSrcStatusText, no
                    // VideoStatusTextOff, no events. The Video subsystem button's text therefore
                    // never refreshed on a source change. UpdateVideoSrcStatus sets the same field
                    // and then recomputes the status text and raises the events the panels listen to.
                    _parent.manager.RoomZ[currentRoomNum].UpdateVideoSrcStatus(currentVSRC);
                    _parent.manager.VideoDisplayZ[displayNumber].CurrentVideoSrc = currentVSRC;
                    CrestronConsole.PrintLine("vidout{0} to in{1}", videoSwitcherOutputNum, _parent.manager.VideoSourceZ[currentVSRC].VidSwitcherInputNumber);
                    //SEND THE SWITCHING COMMANDS
                    _parent.videoEISC1.UShortInput[(ushort)(videoSwitcherOutputNum + 600)].UShortValue = _parent.manager.VideoSrcScenarioZ[vsrcScenario].DisplayInputs[adjustedButtonNum];
                    _parent.videoEISC1.UShortInput[(ushort)(videoSwitcherOutputNum + 700)].UShortValue = _parent.manager.VideoSrcScenarioZ[vsrcScenario].ReceiverInputs[adjustedButtonNum];
                    _parent.videoEISC1.UShortInput[(ushort)(videoSwitcherOutputNum + 800)].UShortValue = _parent.manager.VideoSrcScenarioZ[vsrcScenario].AltSwitcherInputs[adjustedButtonNum];
                    _parent.videoEISC2.StringInput[(ushort)(videoSwitcherOutputNum + 200)].StringValue = _parent.manager.VideoSourceZ[currentVSRC].StreamLocation;//set the DM NVX Video Source address to subscribe to
                    _parent.videoEISC2.UShortInput[(ushort)(displayNumber + 400)].UShortValue = currentVSRC; //tell the simpl program which source# the display is viewing

                    // Set stream on NVX receiver directly
                    var onReceiver = FindReceiverByOutputNum(videoSwitcherOutputNum);
                    if (onReceiver != null) onReceiver.SetStreamLocation(_parent.manager.VideoSourceZ[currentVSRC].StreamLocation);

                    // Route display input (power on + input) via NVX receiver
                    ushort displayInputValue = _parent.manager.VideoSrcScenarioZ[vsrcScenario].DisplayInputs[adjustedButtonNum];
                    if (displayInputValue > 0)
                    {
                        ushort inputDelay = (vidConfigScenario > 0) ? _parent.manager.VideoConfigScenarioZ[vidConfigScenario].DisplayInputDelay : (ushort)6;
                        RouteDisplayInput(displayNumber, videoSwitcherOutputNum, displayInputValue, inputDelay);
                    }

                    //send multicast address to audio zone if video sound is through distributed audio
                    //turn on the NAX stream for audio in this zone

                    if (vidConfigScenario > 0 && _parent.manager.VideoConfigScenarioZ[vidConfigScenario].VideoVolThroughDistAudio)
                    {
                        if (ControlSystem.NAXsystem)
                        {
                            if (_parent.is8ZoneBoxByBox(_parent.GetVideoNAXBox(currentRoomNum)))
                            {
                                _parent.musicEISC1.UShortInput[(ushort)(videoAudioID + 500)].UShortValue = 17;//to switcher
                            }
                            else
                            {
                                _parent.musicEISC1.UShortInput[(ushort)(videoAudioID + 500)].UShortValue = 13;//to switcher
                            }
                            _parent.musicEISC3.StringInput[(ushort)(videoAudioID + 300)].StringValue = _parent.manager.VideoSourceZ[currentVSRC].MultiCastAddress;
                            _parent.musicSystemControl.multis[videoAudioID] = _parent.manager.VideoSourceZ[currentVSRC].MultiCastAddress;
                        }
                        else
                        {
                            _parent.musicEISC1.UShortInput[(ushort)(videoAudioID + 500)].UShortValue = _parent.manager.VideoSourceZ[currentVSRC].AudSwitcherInputNumber;
                        }
                        // Only clear the music now-playing when video shares the room's single audio zone.
                        // With an independent video zone, music keeps playing on its own zone.
                        if (!independentAudio)
                        {
                            _parent.manager.RoomZ[currentRoomNum].UpdateMusicSrcStatus(0);//from SelectDisplayVideoSource
                            //manager.RoomZ[currentRoomNum].CurrentMusicSrc = 0;//from SelectDisplayVideoSource
                            //manager.RoomZ[currentRoomNum].MusicStatusText = "";
                        }
                    }
                }

                UpdateRoomVideoStatusText(videoSwitcherOutputNum, currentVSRC);

                if (currentVSRC > 0)
                {
                    ushort vidSwitcherIn = _parent.manager.VideoSourceZ[currentVSRC].VidSwitcherInputNumber;
                    if (vidSwitcherIn > 0) // Only route DM for non-NVX sources; NVX sources use streaming, sending 0 triggers off logic
                    {
                        _parent.videoEISC1.UShortInput[(ushort)(videoSwitcherOutputNum + 500)].UShortValue = vidSwitcherIn;//this is for the DM. switcher input # to output
                        _parent.videoEISC1.UShortInput[(ushort)(videoSwitcherOutputNum + 900)].UShortValue = vidSwitcherIn;//this is for the room module - this may be redundant 
                    }
                    else
                    {
                        // NVX source selected — DmOutputChanged won't fire, so recalculate InUse here
                        RecalculateVideoSourceInUse();
                    }
                }
                else
                {
                    _parent.videoEISC1.UShortInput[(ushort)(videoSwitcherOutputNum + 500)].UShortValue = 0;//this is for the DM. switcher input # to output
                    _parent.videoEISC1.UShortInput[(ushort)(videoSwitcherOutputNum + 900)].UShortValue = 0;//this is for the room module - this may be redundant
                }
                foreach (var tp in _parent.manager.touchpanelZ)
                {
                    if (tp.Value.CurrentDisplayNumber == displayNumber)
                    {
                        UpdateTPVideoMenu(tp.Value.Number);//from selectDisplayVideoSource
                    }
                }
            }
        }
        public void SelectVideoSourceFromTP(ushort TPNumber, ushort sourceButtonNumber)
        {
            //calculate the source # because source button # isn't the source #
            ushort currentRoomNum = _parent.manager.touchpanelZ[TPNumber].CurrentRoomNum;
            ushort vidConfigScenario;
            ushort audioSwitcherOutputNum = _parent.manager.RoomZ[currentRoomNum].AudioID;
            ushort adjustedButtonNum = 0;
            ushort displayNumber = 0;

            //default display is for handheld remotes. doesn't apply to ipads etc.
            //this forces a remote to a room. otherwise it's a panel or ipad that could be on any room
            //get the display number and config scenario
            if (_parent.manager.touchpanelZ[TPNumber].DefaultDisplay > 0)
            {

                displayNumber = _parent.manager.touchpanelZ[TPNumber].DefaultDisplay;
                vidConfigScenario = _parent.manager.VideoDisplayZ[displayNumber].VidConfigurationScenario;
            }
            else
            {
                displayNumber = _parent.manager.RoomZ[currentRoomNum].CurrentDisplayNumber;
                vidConfigScenario = _parent.manager.RoomZ[currentRoomNum].ConfigurationScenario;
            }
            //CrestronConsole.PrintLine("display {0}", displayNumber);
            ushort srcGroup = _parent.manager.touchpanelZ[TPNumber].CurrentVSrcGroupNum;
            _parent.imageEISC.BooleanInput[TPNumber].BoolValue = true;//this tells the program that the current subsystem is video for this panel
            _parent.manager.touchpanelZ[TPNumber].CurrentSubsystemIsVideo = true;
            //this will work for panels that don't use the 6 per page analog modes because srcGroup will always be 1
            if (srcGroup > 0 && sourceButtonNumber > 0)
            {
                adjustedButtonNum = (ushort)(sourceButtonNumber + (srcGroup - 1) * 6);//this is for a handheld using analog mode buttons 6 per page and shouldn't affect other panels
                //CrestronConsole.PrintLine("adjusted {0}", adjustedButtonNum);
            }
            //check if there's a display to track this one.
            if (_parent.manager.VideoDisplayZ[displayNumber].TieToDisplayNumbers[0] > 0)
            {
                SelectMultiDisplayVideoSource(displayNumber, adjustedButtonNum);
            }
            else
            {
                SelectDisplayVideoSource(displayNumber, adjustedButtonNum);
            }
            //OFF
            if (sourceButtonNumber == 0)
            {
                _parent.manager.touchpanelZ[TPNumber].UserInterface.StringInput[2].StringValue = "Off";

                if (_parent.manager.RoomZ[currentRoomNum].NumberOfDisplays == 1)
                {
                    _parent.PressCloseXButton(TPNumber);//close the menu when turning off
                }
                else if (_parent.AreAllDisplaysOffInThisRoom(currentRoomNum))
                {
                    _parent.PressCloseXButton(TPNumber);//close the menu when turning off    
                }
            }

            //select the source
            else
            {
                //if this room has a receiver and the music is through the receiver then turn the music off
                //(skip when the room has an independent video audio zone - music keeps playing)
                if (!_parent.HasIndependentVideoAudio(currentRoomNum) && vidConfigScenario > 0 && _parent.manager.VideoConfigScenarioZ[vidConfigScenario].HasReceiver && _parent.manager.VideoConfigScenarioZ[vidConfigScenario].MusicThroughReceiver > 0)
                {
                    _parent.manager.RoomZ[currentRoomNum].UpdateMusicSrcStatus(0);//from SelectVideoSourceFromTP
                }


                if (!_parent.HasIndependentVideoAudio(currentRoomNum) && vidConfigScenario > 0 && _parent.manager.VideoConfigScenarioZ[vidConfigScenario].VideoVolThroughDistAudio)
                {
                    _parent.manager.RoomZ[currentRoomNum].UpdateMusicSrcStatus(0);//from SelectVideoSourceFromTP
                }
            }
            if (_parent.logging) CrestronConsole.PrintLine("seelctvidesrouce from tp");
            UpdateTPVideoMenu(TPNumber);//from selectVideoSourceFromTP

            if (_parent.manager.touchpanelZ[TPNumber].TSR310 != null)
            {
                ushort currentRm = _parent.manager.touchpanelZ[TPNumber].CurrentRoomNum;
                ushort currentVSrc = _parent.manager.RoomZ[currentRm].CurrentVideoSrc;
                ushort favScenario = 0;
                if (currentVSrc > 0 && _parent.manager.VideoSourceZ.ContainsKey(currentVSrc))
                    favScenario = _parent.manager.VideoSourceZ[currentVSrc].FavoriteScenario;

                _parent.manager.touchpanelZ[TPNumber].UserInterface.BooleanInput[360].BoolValue = favScenario > 0;

                if (_parent.channelSettings != null)
                {
                    _parent.manager.touchpanelZ[TPNumber].CurrentChannelGroupNum = 1;
                    _parent.channelSettings.UpdateChannelButtons(TPNumber);
                }
            }
        }
        public void TurnOffAllDisplays(ushort TPNumber)
        {
            ushort currentRoomNumber = _parent.manager.touchpanelZ[TPNumber].CurrentRoomNum;
            _parent.manager.RoomZ[currentRoomNumber].UpdateVideoSrcStatus(0);//from TurnOffAllDisplays
            foreach (var display in _parent.manager.VideoDisplayZ)
            {
                if (display.Value.AssignedToRoomNum == currentRoomNumber)
                {
                    display.Value.CurrentSourceText = "";
                    display.Value.CurrentVideoSrc = 0;
                    _parent.manager.RoomZ[currentRoomNumber].VideoOutputNum = display.Value.VideoOutputNum;
                    SelectVideoSourceFromTP(TPNumber, 0);
                }
            }
        }
        public void SelectMultiDisplayVideoSource(ushort displayNumber, ushort sourceButtonNumber)
        {
            ushort numberOfDisplays = (ushort)_parent.manager.VideoDisplayZ[displayNumber].TieToDisplayNumbers.Count;
            ushort currentRoomNum = _parent.manager.VideoDisplayZ[displayNumber].AssignedToRoomNum;
            if (!_parent.manager.RoomZ.ContainsKey(currentRoomNum))
            {
                CrestronConsole.PrintLine("SelectMultiDisplayVideoSource: display {0} is assigned to placeholder room {1} - skipping room updates", displayNumber, currentRoomNum);
                return;
            }
            ushort vidConfigScenario = _parent.manager.VideoDisplayZ[displayNumber].VidConfigurationScenario;
            ushort audioSwitcherOutputNum = _parent.manager.RoomZ[currentRoomNum].AudioID;//music zone
            ushort videoAudioID = _parent.GetVideoAudioID(currentRoomNum);//video zone (== music zone when no separate TV output)
            bool independentAudio = _parent.HasIndependentVideoAudio(currentRoomNum);
            ushort vsrcScenario = _parent.manager.VideoDisplayZ[displayNumber].VideoSourceScenario;
            ushort currentVSRC = 0;
            CrestronConsole.PrintLine("multidisplayvideosource disp{0} btnnum{1}", displayNumber, sourceButtonNumber);
            if (sourceButtonNumber == 0)
            {

                _parent.manager.VideoDisplayZ[displayNumber].CurrentVideoSrc = 0;
                _parent.manager.RoomZ[currentRoomNum].UpdateVideoSrcStatus(0);//from selectmultidisplayvideosource
                if (_parent.manager.VideoConfigScenarioZ[vidConfigScenario].VideoVolThroughDistAudio)
                {
                    _parent.musicSystemControl.SwitcherAudioZoneOff(videoAudioID);//turn the video audio zone off (music zone untouched when independent)
                }
                for (ushort i = 0; i < numberOfDisplays; i++)
                {
                    ushort currentDisplayNumber = _parent.manager.VideoDisplayZ[displayNumber].TieToDisplayNumbers[i];
                    ushort videoSwitcherOutputNum = _parent.manager.VideoDisplayZ[currentDisplayNumber].VideoOutputNum;

                    _parent.videoEISC1.UShortInput[(ushort)(videoSwitcherOutputNum + 600)].UShortValue = 0;//display input
                    _parent.videoEISC1.UShortInput[(ushort)(videoSwitcherOutputNum + 700)].UShortValue = 0;//receiver input
                    _parent.videoEISC1.UShortInput[(ushort)(videoSwitcherOutputNum + 800)].UShortValue = 0;//alt switcher input
                    // Deliberately NOT clearing the NVX stream location on off. Dropping the
                    // subscription is what black-screens any other decoder still watching this
                    // same source; powering the display off is enough to blank this TV.
                    //_parent.videoEISC2.StringInput[(ushort)(videoSwitcherOutputNum + 200)].StringValue = "0.0.0.0"; //clear the NVX multicast address
                    _parent.videoEISC2.UShortInput[(ushort)(currentDisplayNumber + 400)].UShortValue = 0;
                    _parent.manager.VideoDisplayZ[currentDisplayNumber].CurrentVideoSrc = 0;//clear the current source for the display
                    _parent.videoEISC1.UShortInput[(ushort)(videoSwitcherOutputNum + 500)].UShortValue = 0;//this is for the DM. switcher input # to output
                    _parent.videoEISC1.UShortInput[(ushort)(videoSwitcherOutputNum + 900)].UShortValue = 0;//this is for the room module - this may be redundant

                }
            }
            //select the source
            else
            {
                //if this room has a receiver and the music is through the receiver then turn the music off
                if (!independentAudio && vidConfigScenario > 0 && _parent.manager.VideoConfigScenarioZ[vidConfigScenario].HasReceiver && _parent.manager.VideoConfigScenarioZ[vidConfigScenario].MusicThroughReceiver > 0)
                {
                    _parent.musicSystemControl.SwitcherAudioZoneOff(audioSwitcherOutputNum);//turn the switcher output off
                }
                //this will work for panels that don't use the 6 per page analog modes because srcGroup will always be 1

                ushort adjustedButtonNum = (ushort)(sourceButtonNumber - 1);//this is for a handheld using analog mode buttons 6 per page and shouldn't affect other panels
                currentVSRC = _parent.manager.VideoSrcScenarioZ[vsrcScenario].IncludedSources[adjustedButtonNum];

                // See the note on the identical call in SelectVideoSourceFromTP: assigning
                // CurrentVideoSrc directly skips the status-text recompute and the events.
                _parent.manager.RoomZ[currentRoomNum].UpdateVideoSrcStatus(currentVSRC);
                _parent.manager.VideoDisplayZ[displayNumber].CurrentVideoSrc = currentVSRC;
                for (ushort i = 0; i < numberOfDisplays; i++)
                {
                    ushort currentDisplayNumber = _parent.manager.VideoDisplayZ[displayNumber].TieToDisplayNumbers[i];
                    ushort videoSwitcherOutputNum = _parent.manager.VideoDisplayZ[currentDisplayNumber].VideoOutputNum;
                    CrestronConsole.PrintLine("vidout{0} to in{1}", videoSwitcherOutputNum, _parent.manager.VideoSourceZ[currentVSRC].VidSwitcherInputNumber);
                    //SEND THE SWITCHING COMMANDS
                    _parent.videoEISC1.UShortInput[(ushort)(videoSwitcherOutputNum + 600)].UShortValue = _parent.manager.VideoSrcScenarioZ[vsrcScenario].DisplayInputs[adjustedButtonNum];
                    _parent.videoEISC1.UShortInput[(ushort)(videoSwitcherOutputNum + 700)].UShortValue = _parent.manager.VideoSrcScenarioZ[vsrcScenario].ReceiverInputs[adjustedButtonNum];
                    _parent.videoEISC1.UShortInput[(ushort)(videoSwitcherOutputNum + 800)].UShortValue = _parent.manager.VideoSrcScenarioZ[vsrcScenario].AltSwitcherInputs[adjustedButtonNum];
                    _parent.videoEISC2.StringInput[(ushort)(videoSwitcherOutputNum + 200)].StringValue = _parent.manager.VideoSourceZ[currentVSRC].StreamLocation;//set the DM NVX Video Source address to subscribe to
                    _parent.videoEISC2.UShortInput[(ushort)(currentDisplayNumber + 400)].UShortValue = currentVSRC; //tell the simpl program which source# the display is viewing

                    ushort vidSwitcherIn = _parent.manager.VideoSourceZ[currentVSRC].VidSwitcherInputNumber;
                    if (vidSwitcherIn > 0) // Only route DM for non-NVX sources; NVX sources use streaming, sending 0 triggers off logic
                    {
                        _parent.videoEISC1.UShortInput[(ushort)(videoSwitcherOutputNum + 500)].UShortValue = vidSwitcherIn;//this is for the DM. switcher input # to output
                        _parent.videoEISC1.UShortInput[(ushort)(videoSwitcherOutputNum + 900)].UShortValue = vidSwitcherIn;//this is for the room module - this may be redundant 
                    }
                    else
                    {
                        // NVX source selected — DmOutputChanged won't fire, so recalculate InUse here
                        RecalculateVideoSourceInUse();
                    }
                    UpdateRoomVideoStatusText(videoSwitcherOutputNum, currentVSRC);

                }
                //send multicast address to audio zone if video sound is through distributed audio
                //turn on the NAX stream for audio in this zone

                if (vidConfigScenario > 0 && _parent.manager.VideoConfigScenarioZ[vidConfigScenario].VideoVolThroughDistAudio)
                {
                    if (_parent.is8ZoneBoxByBox(_parent.GetVideoNAXBox(currentRoomNum)))
                    {
                        _parent.musicEISC1.UShortInput[(ushort)(videoAudioID + 500)].UShortValue = 17;//to switcher
                    }
                    else
                    {
                        _parent.musicEISC1.UShortInput[(ushort)(videoAudioID + 500)].UShortValue = 13;//to switcher
                    }
                    _parent.musicEISC3.StringInput[(ushort)(videoAudioID + 300)].StringValue = _parent.manager.VideoSourceZ[currentVSRC].MultiCastAddress;
                    _parent.musicSystemControl.multis[videoAudioID] = _parent.manager.VideoSourceZ[currentVSRC].MultiCastAddress;
                    // Only clear the music now-playing when video shares the room's single audio zone.
                    if (!independentAudio)
                    {
                        _parent.manager.RoomZ[currentRoomNum].UpdateMusicSrcStatus(0);
                        //manager.RoomZ[currentRoomNum].CurrentMusicSrc = 0;//from SelectMultiDisplayVideoSource
                        //manager.RoomZ[currentRoomNum].MusicStatusText = "";
                    }
                }
            }

            foreach (var tp in _parent.manager.touchpanelZ)
            {
                if (tp.Value.CurrentDisplayNumber == displayNumber)
                {
                    UpdateTPVideoMenu(tp.Value.Number);//from SelectMultiDisplayVideoSource
                }
            }
        }
        public void ChangeCurrentSourceWhenAMultiDisplayGoesOff(ushort displayNumber)
        {
            ushort roomNumber = _parent.manager.VideoDisplayZ[displayNumber].AssignedToRoomNum;
            if (!_parent.manager.RoomZ.ContainsKey(roomNumber))
            {
                CrestronConsole.PrintLine("ChangeCurrentSourceWhenAMultiDisplayGoesOff: display {0} is assigned to placeholder room {1} - nothing to update", displayNumber, roomNumber);
                return;
            }
            var room = _parent.manager.RoomZ[roomNumber];
            ushort newVsrc = 0;
            ushort configScen = 0;
            ushort newDisplay = 0;
            ushort buttonNum = 0; // this is to send to the selectDisplay function
            CrestronConsole.PrintLine("display {0} turned off, update the current source for this room", displayNumber);
            var allDisplays = room.ListOfDisplays;
            for (ushort i = 0; i < allDisplays.Count; i++)
            {
                var d = allDisplays[i];
                if (d == displayNumber)
                    continue;             // skip the one that went off

                var src = _parent.manager.VideoDisplayZ[d].CurrentVideoSrc;
                if (src > 0)
                {
                    newVsrc = src;
                    newDisplay = d;
                    configScen = _parent.manager.VideoDisplayZ[d].VidConfigurationScenario;
                    buttonNum = (ushort)(i + 1);
                    break;        // we only need the first still-on display
                }
            }
            if (newVsrc > 0)
            {
                room.CurrentDisplayNumber = newDisplay;
                room.CurrentVideoSrc = newVsrc;
                room.UpdateVideoSrcStatus(newVsrc); //from ChangeCurrentSourceWhenAMultiDisplayGoesOff

                CrestronConsole.PrintLine("starting to update displays - {0}:{1}", DateTime.Now.Second, DateTime.Now.Millisecond);
                foreach (var tp in _parent.manager.touchpanelZ)
                {

                    if (tp.Value.CurrentRoomNum == roomNumber)
                    {
                        SelectDisplay(tp.Value.Number, buttonNum);
                    }
                }
                CrestronConsole.PrintLine("ended to update displays - {0}:{1}", DateTime.Now.Second, DateTime.Now.Millisecond);

                //update the audio 
                if (_parent.manager.VideoConfigScenarioZ[configScen].VideoVolThroughDistAudio)
                {
                    ushort audioID = _parent.GetVideoAudioID(roomNumber);//video zone
                    _parent.musicEISC3.StringInput[(ushort)(audioID + 300)].StringValue = _parent.manager.VideoSourceZ[newVsrc].MultiCastAddress;
                }
            }
            else
            {
                room.UpdateVideoSrcStatus(0);
            }
        }
        public void PopulateTPVideoSourceList(ushort TPNumber)
        {
            
            ushort currentRoomNumber = _parent.manager.touchpanelZ[TPNumber].CurrentRoomNum;
            ushort numSrcs = (ushort)_parent.manager.VideoSrcScenarioZ[_parent.manager.RoomZ[currentRoomNumber].VideoSrcScenario].IncludedSources.Count;
            CrestronConsole.PrintLine("PopulateTPVideoSourceList {0} room{1} numSrcs{2}", TPNumber , currentRoomNumber, numSrcs);
            if (_parent.manager.touchpanelZ[TPNumber].HTML_UI)
            {
                _parent.manager.touchpanelZ[TPNumber]._HTMLContract.vsrcList.numberOfSources(
                        (sig, wh) => sig.UShortValue = numSrcs);//disable the volume buttons
            }
            else
            {
                _parent.manager.touchpanelZ[TPNumber].UserInterface.SmartObjects[5].UShortInput[4].UShortValue = numSrcs;//number of sources
            }

            for (ushort i = 0; i < numSrcs; i++)//loop through all video sources in this scenario
            {
                ushort srcNum = _parent.manager.VideoSrcScenarioZ[_parent.manager.RoomZ[currentRoomNumber].VideoSrcScenario].IncludedSources[i];
                if (_parent.manager.touchpanelZ[TPNumber].HTML_UI)
                {
                    _parent.manager.touchpanelZ[TPNumber]._HTMLContract.vsrcButton[i].vidSourceName(
                        (sig, wh) => sig.StringValue = _parent.manager.VideoSourceZ[srcNum].DisplayName);//populate source names
                    _parent.manager.touchpanelZ[TPNumber]._HTMLContract.vsrcButton[i].vidSourceIcon(
                        (sig, wh) => sig.StringValue = _parent.manager.VideoSourceZ[srcNum].IconHTML);//populate source icons
                }
                else
                {
                    _parent.manager.touchpanelZ[TPNumber].UserInterface.SmartObjects[5].StringInput[(ushort)(i + 11)].StringValue = _parent.BuildHTMLString(TPNumber, _parent.manager.VideoSourceZ[srcNum].DisplayName, "26");//update the names
                    _parent.manager.touchpanelZ[TPNumber].UserInterface.SmartObjects[5].StringInput[(ushort)(i + 2011)].StringValue = _parent.manager.VideoSourceZ[srcNum].IconSerial;//update the icons
                }
                //Update the current video source of this room to the panel and highlight the appropriate button
                if (srcNum == _parent.manager.RoomZ[currentRoomNumber].CurrentVideoSrc)
                {
                    _parent.manager.touchpanelZ[TPNumber].videoButtonFB((ushort)(i + 1));
                }
            }

        }

        public void UpdateDisplaysAvailableForSelection(ushort TPNumber, ushort currentRoomNumber)
        {
            //if the room has multiple displays enable the change display button
            if (_parent.manager.RoomZ[currentRoomNumber].NumberOfDisplays > 1)
            {
                _parent.manager.touchpanelZ[TPNumber].UserInterface.BooleanInput[350].BoolValue = true;//enable the change display button
                if (_parent.manager.touchpanelZ[TPNumber].HTML_UI)
                {
                    // Populate HTML display selection buttons (joins 352-361 for labels, 362-371 for visibility, 372-381 for source text)
                    // First, clear all slots
                    for (ushort slot = 0; slot < 10; slot++)
                    {
                        _parent.manager.touchpanelZ[TPNumber].UserInterface.BooleanInput[(ushort)(362 + slot)].BoolValue = false;
                        _parent.manager.touchpanelZ[TPNumber].UserInterface.StringInput[(ushort)(352 + slot)].StringValue = "";
                        _parent.manager.touchpanelZ[TPNumber].UserInterface.StringInput[(ushort)(372 + slot)].StringValue = "";
                        _parent.manager.touchpanelZ[TPNumber].UserInterface.BooleanInput[(ushort)(352 + slot)].BoolValue = false;
                    }
                    // Fill in available displays using ListOfDisplays (ordered) to match SelectDisplay button indexing
                    var displayList = _parent.manager.RoomZ[currentRoomNumber].ListOfDisplays;
                    for (ushort htmlIdx = 0; htmlIdx < displayList.Count && htmlIdx < 10; htmlIdx++)
                    {
                        ushort dispNum = displayList[htmlIdx];
                        _parent.manager.touchpanelZ[TPNumber].UserInterface.StringInput[(ushort)(352 + htmlIdx)].StringValue = _parent.manager.VideoDisplayZ[dispNum].DisplayName;
                        _parent.manager.touchpanelZ[TPNumber].UserInterface.BooleanInput[(ushort)(362 + htmlIdx)].BoolValue = true; // show button
                        // Source name label
                        ushort srcNum = _parent.manager.VideoDisplayZ[dispNum].CurrentVideoSrc;
                        string srcName = (srcNum > 0 && _parent.manager.VideoSourceZ.ContainsKey(srcNum)) ? _parent.manager.VideoSourceZ[srcNum].DisplayName : "Off";
                        _parent.manager.touchpanelZ[TPNumber].UserInterface.StringInput[(ushort)(372 + htmlIdx)].StringValue = srcName;
                        // Highlight the currently selected display
                        bool isSelected = (dispNum == _parent.manager.RoomZ[currentRoomNumber].CurrentDisplayNumber);
                        _parent.manager.touchpanelZ[TPNumber].UserInterface.BooleanInput[(ushort)(352 + htmlIdx)].BoolValue = isSelected;
                    }
                }
                else
                {
                    _parent.manager.touchpanelZ[TPNumber].UserInterface.SmartObjects[34].UShortInput[4].UShortValue = _parent.manager.RoomZ[currentRoomNumber].NumberOfDisplays;
                    ushort i = 1;
                    foreach (var display in _parent.manager.VideoDisplayZ)
                    {
                        if (display.Value.AssignedToRoomNum == currentRoomNumber)
                        {
                            ushort eiscPosition = (ushort)((TPNumber - 1) * 10 + 2400 + i);
                            _parent.manager.touchpanelZ[TPNumber].UserInterface.SmartObjects[34].StringInput[i].StringValue = display.Value.DisplayName;
                            i++;
                        }
                    }
                }

                if (_parent.manager.RoomZ[currentRoomNumber].CurrentDisplayNumber == 0)
                {
                    SelectDisplay(TPNumber, 1);//from updateDisplaysAvailableForSelection default to first display
                }
                else
                {
                    _parent.manager.touchpanelZ[TPNumber].UserInterface.StringInput[10].StringValue = _parent.manager.VideoDisplayZ[_parent.manager.RoomZ[currentRoomNumber].CurrentDisplayNumber].DisplayName;
                }

            }
            else
            { 
                _parent.manager.touchpanelZ[TPNumber].UserInterface.BooleanInput[350].BoolValue = false;//remove the change display button
            }//remove the change display button
        }
        public void UpdateRoomVideoStatusText(ushort switcherOutputNumber, ushort videoSourceNumber)
        {
            CrestronConsole.PrintLine("UpdateRoomVideoStatusText {0} {1}", switcherOutputNumber, videoSourceNumber);

            foreach (var kv in _parent.manager.VideoDisplayZ)
            {
                var displayNum = kv.Key;
                var display = kv.Value;

                if (display.VideoOutputNum == switcherOutputNumber)
                {
                    var roomNum = display.AssignedToRoomNum;
                    // continue, not return — a switcher output can feed several displays and the
                    // others may well be assigned to real rooms.
                    if (!_parent.manager.RoomZ.ContainsKey(roomNum)) continue;
                    var room = _parent.manager.RoomZ[roomNum];
                    if (videoSourceNumber == 0 && room.CurrentVideoSrc == 0)
                    {
                        // This room is already off — nothing to update for THIS display, but keep
                        // iterating. Was `return`, which abandoned the whole loop: one already-off
                        // display meant every display after it in VideoDisplayZ order silently
                        // missed its update. Same reasoning as the guard 5 lines above.
                        continue;
                    }
                    // Update the display itself
                    display.CurrentVideoSrc = videoSourceNumber;

                    if (videoSourceNumber > 0)
                    {
                        // New source push up to room
                        display.CurrentSourceText = _parent.manager.VideoSourceZ[videoSourceNumber].DisplayName;
                        room.UpdateVideoSrcStatus(videoSourceNumber);
                    }
                    else
                    {
                        // That one display just went dark...
                        display.CurrentSourceText = "";

                        // ...but only clear the room if it was *the* active display
                        if (room.CurrentDisplayNumber == displayNum)
                        {
                            CrestronConsole.PrintLine(
                              "@turning off room {0} because display {1} was current",
                              roomNum,
                              displayNum
                            );
                            room.UpdateVideoSrcStatus(0);
                        }
                        else
                        {
                            CrestronConsole.PrintLine(
                              "@ignoring off for display {0} (room still on display {1})",
                              displayNum,
                              room.CurrentDisplayNumber
                            );
                        }
                    }
                }
            }
        }

        /// <summary>
        /// True when the video path described by this configuration scenario can report a
        /// volume LEVEL back to the panel — i.e. when there is a gauge worth drawing.
        ///
        /// Three paths produce a level, and each has its OWN "does it report?" flag. The flag
        /// names do not line up with the paths, so read this before changing the expression:
        ///
        ///   receiver         HasReceiver              -> ReceiverHasVolFB
        ///   distributed audio  VideoVolThroughDistAudio -> MusicHasVolFB   (NOT ReceiverHasVolFB)
        ///   TV direct        (neither of the above)   -> TvHasVolFB
        ///
        /// MusicHasVolFB is the distributed-audio path's flag — it covers music AND, by
        /// extension, video audio routed through the NAX / audio switcher. ReceiverHasVolFB is
        /// the music-through-receiver path's. A dist-audio system that cannot report a level is
        /// vanishingly rare, but pairing each path with its own flag is the point: it is the
        /// difference between correct and correct-by-luck.
        ///
        /// Nothing else produces a level. No receiver and no TV volume feedback means IR volume
        /// — fire and forget — and the gauge would sit at whatever the last room left behind,
        /// which is worse than no gauge at all.
        ///
        /// This is the ONE definition of that rule. It drives the gauge join 153 on every panel
        /// and the TSR-310 volume popup (join 44) in TouchpanelUI.ShowVolumePopup. Those two
        /// used to disagree — 153 tested only the receiver — which is part of why the bar could
        /// stay up in a room that has nothing to put in it.
        ///
        /// An unknown or zero scenario reports false rather than throwing: config numbers are
        /// hand-entered and must never take the program down.
        /// </summary>
        public bool VideoVolumeHasFeedback(ushort vidConfigScenario)
        {
            if (vidConfigScenario == 0) return false;
            if (!_parent.manager.VideoConfigScenarioZ.ContainsKey(vidConfigScenario)) return false;

            var scenario = _parent.manager.VideoConfigScenarioZ[vidConfigScenario];
            return (scenario.HasReceiver && scenario.ReceiverHasVolFB)
                || (scenario.VideoVolThroughDistAudio && scenario.MusicHasVolFB)
                || scenario.TvHasVolFB;
        }

        /// <summary>
        /// Mirror a distributed-audio zone level onto the VIDEO volume gauge (panel analog 1).
        ///
        /// When a room's video audio runs through the distributed audio system
        /// (VideoVolThroughDistAudio), the level does NOT arrive on the per-panel subsystem EISC
        /// analog that normally feeds analog 1 — it arrives on VOLUMEEISC (0x9C) indexed by audio
        /// switcher output, which Volume_Sigchange handles. That path only ever wrote analog 2
        /// (the MUSIC gauge) and only matched on room.AudioID, so:
        ///   - the video gauge (analog 1) sat at whatever it last held -> TSR-310 popup read 0
        ///   - rooms with a dedicated TV zone (VideoAudioID > 0) were dropped entirely, since
        ///     their level arrives on VideoAudioID, which no room's AudioID matches
        ///
        /// This feeds analog 1 for every panel currently on a matching room, so the native gauge
        /// and the HTML bar (data-analog-bar="1") read the same join.
        ///
        /// Matched on GetVideoAudioID so it covers the shared zone AND the dedicated TV zone.
        /// Deliberately NOT gated on CurrentSubsystemIsVideo — navigation clears that flag, which
        /// is exactly what kept the home page gauge dead (see subysystemControl_SigChange).
        /// </summary>
        public void PushDistAudioVideoVolume(ushort audioSwitcherOutputNum, ushort level)
        {
            if (audioSwitcherOutputNum == 0) return;

            foreach (var tp in _parent.manager.touchpanelZ)
            {
                if (!PanelFollowsDistAudioVideoZone(tp.Value, audioSwitcherOutputNum)) continue;
                tp.Value.UserInterface.UShortInput[1].UShortValue = level;
            }
        }

        /// <summary>
        /// Mirror a distributed-audio zone MUTE state onto the VIDEO mute feedback (panel digital
        /// 156) — the digital counterpart of PushDistAudioVideoVolume, and broken the same way.
        ///
        /// Mute state for these rooms arrives on musicEISC1 digitals 201-300 (zone = join - 200),
        /// handled in Music1SigChangeHandler. That path only drove digital 1009 (the AUDIO mute
        /// indicator) and only matched room.AudioID, so the video mute button on 156 — which is
        /// what both the TSR-310 popup and the HTML page bind their feedback to
        /// (VIDEO_SUB.html: data-digital="156" data-fb="156") — was never driven at all for
        /// dist-audio rooms, and rooms with a dedicated TV zone were missed entirely.
        ///
        /// Not gated on CurrentSubsystemIsVideo, for the same reason as the volume path.
        /// </summary>
        public void PushDistAudioVideoMute(ushort audioSwitcherOutputNum, bool muted)
        {
            if (audioSwitcherOutputNum == 0) return;

            foreach (var tp in _parent.manager.touchpanelZ)
            {
                if (!PanelFollowsDistAudioVideoZone(tp.Value, audioSwitcherOutputNum)) continue;
                tp.Value.UserInterface.BooleanInput[156].BoolValue = muted;
            }
        }

        /// <summary>
        /// True when this panel's current room takes its VIDEO audio from the given distributed
        /// audio zone. Shared by the volume and mute mirrors so they can never disagree about
        /// which panels to update.
        ///
        /// Matched on GetVideoAudioID, so it covers the shared zone (VideoAudioID == 0) and the
        /// dedicated TV zone (VideoAudioID > 0) alike.
        /// </summary>
        private bool PanelFollowsDistAudioVideoZone(TouchpanelUI tp, ushort audioSwitcherOutputNum)
        {
            if (tp == null || tp.UserInterface == null) return false;

            ushort roomNum = tp.CurrentRoomNum;
            if (!_parent.manager.RoomZ.ContainsKey(roomNum)) return false;

            // room.ConfigurationScenario tracks the room's CURRENT display, so this follows a
            // display change the same way UpdateTPVideoMenu's gauge visibility does.
            ushort vidConfigScenario = _parent.manager.RoomZ[roomNum].ConfigurationScenario;
            if (vidConfigScenario == 0) return false;
            if (!_parent.manager.VideoConfigScenarioZ.ContainsKey(vidConfigScenario)) return false;
            if (!_parent.manager.VideoConfigScenarioZ[vidConfigScenario].VideoVolThroughDistAudio) return false;

            return _parent.GetVideoAudioID(roomNum) == audioSwitcherOutputNum;
        }

        public void UpdateTPVideoMenu(ushort TPNumber)
        {
            ushort currentRoomNumber = _parent.manager.touchpanelZ[TPNumber].CurrentRoomNum;

            if (_parent.manager.RoomZ[currentRoomNumber].VideoSrcScenario > 0)
            {
                ushort numSrcs = (ushort)_parent.manager.VideoSrcScenarioZ[_parent.manager.RoomZ[currentRoomNumber].VideoSrcScenario].IncludedSources.Count;
                ushort currentVSRC = _parent.manager.RoomZ[currentRoomNumber].CurrentVideoSrc;
                ushort vidConfigScenario = _parent.manager.RoomZ[currentRoomNumber].ConfigurationScenario;
                //show or hide the volume feedback indicator guage. room.ConfigurationScenario
                //tracks the room's CURRENT display (SelectDisplay reassigns it), so this follows
                //a display change without any extra plumbing.
                _parent.manager.touchpanelZ[TPNumber].UserInterface.BooleanInput[153].BoolValue =
                    VideoVolumeHasFeedback(vidConfigScenario);
                //and seed the gauge and mute indicator themselves, so they open on the live state
                //instead of whatever analog 1 / digital 156 last held (both feeds relay on change).
                _parent.SyncPanelToVideoVolume(TPNumber);
                _parent.SyncPanelToVideoMute(TPNumber);
                //show or hide the format button
                if (_parent.manager.RoomZ[currentRoomNumber].FormatScenario > 0)
                {
                    _parent.manager.touchpanelZ[TPNumber].UserInterface.BooleanInput[179].BoolValue = true;
                    _parent.manager.touchpanelZ[TPNumber].UserInterface.StringInput[180].StringValue = _parent.manager.FormatScenarioZ[_parent.manager.RoomZ[currentRoomNumber].FormatScenario].ButtonLabel; //button label
                    //populate the format button text
                    for (ushort i = 0; i < 10; i++)
                    {
                        if (i < _parent.manager.FormatScenarioZ[_parent.manager.RoomZ[currentRoomNumber].FormatScenario].FormatCmds.Count)
                        {
                            ushort cmd = _parent.manager.FormatScenarioZ[_parent.manager.RoomZ[currentRoomNumber].FormatScenario].FormatCmds[i];
                            _parent.manager.touchpanelZ[TPNumber].UserInterface.StringInput[(ushort)(181 + i)].StringValue = _parent.manager.FormatCmdZ[cmd].Name;
                        }
                        else { _parent.manager.touchpanelZ[TPNumber].UserInterface.StringInput[(ushort)(181 + i)].StringValue = ""; } //clear the rest of the buttons
                    }
                }
                else { _parent.manager.touchpanelZ[TPNumber].UserInterface.BooleanInput[179].BoolValue = false; }
                //show or hide the sleep button
                if (_parent.manager.RoomZ[currentRoomNumber].SleepScenario > 0)
                {
                    _parent.manager.touchpanelZ[TPNumber].UserInterface.BooleanInput[159].BoolValue = true;
                    _parent.manager.touchpanelZ[TPNumber].UserInterface.StringInput[160].StringValue = _parent.manager.SleepScenarioZ[_parent.manager.RoomZ[currentRoomNumber].SleepScenario].ButtonLabel; //button label
                    //populate the sleep buttons text
                    for (ushort i = 0; i < 5; i++)
                    {
                        if (i < _parent.manager.SleepScenarioZ[_parent.manager.RoomZ[currentRoomNumber].SleepScenario].SleepCmds.Count)
                        {
                            ushort cmd = _parent.manager.SleepScenarioZ[_parent.manager.RoomZ[currentRoomNumber].SleepScenario].SleepCmds[i];
                            _parent.manager.touchpanelZ[TPNumber].UserInterface.StringInput[(ushort)(161 + i)].StringValue = _parent.manager.SleepCmdZ[cmd].Name;
                        }
                        else { _parent.manager.touchpanelZ[TPNumber].UserInterface.StringInput[(ushort)(161 + i)].StringValue = ""; } //clear the rest of the buttons
                    }
                }
                else { _parent.manager.touchpanelZ[TPNumber].UserInterface.BooleanInput[159].BoolValue = false; }
                //show or hide the lift button
                if (_parent.manager.RoomZ[currentRoomNumber].LiftScenario > 0)
                {
                    _parent.manager.touchpanelZ[TPNumber].UserInterface.BooleanInput[59].BoolValue = true;
                    _parent.manager.touchpanelZ[TPNumber].UserInterface.StringInput[60].StringValue = _parent.manager.LiftScenarioZ[_parent.manager.RoomZ[currentRoomNumber].LiftScenario].ButtonLabel; //button label                                                                                                                                                        //populate the lift buttons text
                    for (ushort i = 0; i < 5; i++)
                    {
                        if (i < _parent.manager.LiftScenarioZ[_parent.manager.RoomZ[currentRoomNumber].LiftScenario].LiftCmds.Count)
                        {
                            ushort cmd = _parent.manager.LiftScenarioZ[_parent.manager.RoomZ[currentRoomNumber].LiftScenario].LiftCmds[i];
                            _parent.manager.touchpanelZ[TPNumber].UserInterface.StringInput[(ushort)(61 + i)].StringValue = _parent.manager.LiftCmdZ[cmd].Name;
                        }
                        else { _parent.manager.touchpanelZ[TPNumber].UserInterface.StringInput[(ushort)(61 + i)].StringValue = ""; } //clear the rest of the buttons
                    }
                }
                else { _parent.manager.touchpanelZ[TPNumber].UserInterface.BooleanInput[59].BoolValue = false; }
                //show or hide the change display button
                if (_parent.manager.RoomZ[currentRoomNumber].NumberOfDisplays > 1) { _parent.manager.touchpanelZ[TPNumber].UserInterface.BooleanInput[350].BoolValue = true; }
                else { _parent.manager.touchpanelZ[TPNumber].UserInterface.BooleanInput[350].BoolValue = false; }
                // Refresh the display-select contents (labels 352-361, per-slot show 362-371,
                // source text 372-381, selected feedback).
                //
                // This USED to be a partial copy of UpdateDisplaysAvailableForSelection that
                // refreshed only the source text and selected state, with the full refresh
                // deferred until the panel opened the menu (SigChange join 351). The HTML panel
                // now opens that menu locally and never tells the program, so "refresh it when
                // they open it" is no longer available — and it was always the weaker option,
                // since the data is equally stale on a dumb panel between opens. Refresh it
                // whenever the room or source changes instead, which is when it can go stale.
                UpdateDisplaysAvailableForSelection(TPNumber, currentRoomNumber);

                // HTML room-options descriptor. Everything above this point that concerns the
                // lift / sleep / format / display dropdowns answers exactly two questions —
                // does this room HAVE the menu, and how many buttons does it hold — and this
                // is the only place in the program that knows both. So it is the right place
                // to publish them, as one JSON line, instead of a dozen booleans.
                //
                // The panel then owns whether the dropdown is OPEN. That is not information
                // the program has ever used, and mirroring it through joins 71-73 / 171-172 /
                // 191-193 / 351 forced a press to round-trip to the processor and back just to
                // close a menu. See PAGE-FLIP-DESCRIPTOR-PLAN.md, Phase 5.
                _parent.manager.touchpanelZ[TPNumber].SendRoomOptionsDescriptor(
                    LiftButtonCount(currentRoomNumber),
                    SleepButtonCount(currentRoomNumber),
                    FormatButtonCount(currentRoomNumber),
                    DisplayButtonCount(currentRoomNumber));

                _parent.manager.touchpanelZ[TPNumber].CurrentVSrcNum = currentVSRC;
                //CrestronConsole.PrintLine("UPDATE TP VIDEO MENU TP-{0} room{1} vsrc{2}", TPNumber, currentRoomNumber, currentVSRC);
                //for tsr-310s  enable more sources button
                if (numSrcs > 6) { _parent.manager.touchpanelZ[TPNumber].UserInterface.BooleanInput[509].BoolValue = true; }
                else { _parent.manager.touchpanelZ[TPNumber].UserInterface.BooleanInput[509].BoolValue = false; }
                _parent.subsystemEISC.UShortInput[(ushort)((TPNumber - 1) * 10 + 302)].UShortValue = _parent.manager.RoomZ[currentRoomNumber].VideoOutputNum;//this updates the equipment ID to connect the panel to the room
                if (currentVSRC > 0)
                {
                    _parent.manager.touchpanelZ[TPNumber].videoPageFlips(_parent.manager.VideoSourceZ[currentVSRC].FlipsToPageNumber, currentVSRC);//from updateTPVideoMenu
                    _parent.videoEISC1.UShortInput[(ushort)(TPNumber + 300)].UShortValue = _parent.manager.VideoSourceZ[currentVSRC].EquipID;
                    _parent.manager.touchpanelZ[TPNumber].UserInterface.StringInput[2].StringValue = _parent.manager.VideoSourceZ[currentVSRC].DisplayName;
                }
                else//OFF
                {
                    _parent.manager.touchpanelZ[TPNumber].videoPageFlips(0);//from update tpVideoMenu
                    _parent.videoEISC1.UShortInput[(ushort)(TPNumber + 300)].UShortValue = 0;//equip ID
                    _parent.manager.touchpanelZ[TPNumber].UserInterface.StringInput[2].StringValue = "Off";
                    //CrestronConsole.PrintLine("VSRC{0} clear button feedback", currentVSRC);
                    _parent.manager.touchpanelZ[TPNumber].videoButtonFB(0);
                }

                //update the video source list and highlight the appropriate button
                //ushort inUse = 0;
                if (_parent.manager.touchpanelZ[TPNumber].UseAnalogModes)
                {
                    ushort group = _parent.manager.touchpanelZ[TPNumber].CurrentVSrcGroupNum;
                    _parent.SetVSRCGroup(TPNumber, group);
                    for (ushort i = 0; i < 6; i++)
                    {
                        if ((ushort)((_parent.manager.touchpanelZ[TPNumber].CurrentVSrcGroupNum - 1) * 6 + i) >= numSrcs) { break; }
                        ushort srcNum = _parent.manager.VideoSrcScenarioZ[_parent.manager.RoomZ[currentRoomNumber].VideoSrcScenario].IncludedSources[(ushort)((group - 1) * 6 + i)];
                        if (_parent.manager.VideoSourceZ[srcNum].InUse)
                        {
                            _parent.manager.touchpanelZ[TPNumber].UserInterface.BooleanInput[(ushort)(521 + i)].BoolValue = true;
                            //inUse |= (ushort)(1 << i);
                        }//set the bit
                        else
                        {
                            _parent.manager.touchpanelZ[TPNumber].UserInterface.BooleanInput[(ushort)(521 + i)].BoolValue = false;
                            //inUse &= (ushort)(~(1 << i));
                        }
                    }

                }
                else
                {
                    PopulateTPVideoSourceList(TPNumber);//update tp video menu
                }
            }
        }

        // ── Room-options button counts ────────────────────────────────────────────────
        // How many buttons each video dropdown holds for a given room; 0 means the room does
        // not have that menu at all, which is the same test the availability joins (59 / 159 /
        // 179 / 350) use above. These are counts of the CONFIGURED COMMANDS only — the fixed
        // trailing button each menu carries (lift's "Close Lift With Off", sleep's "Cancel")
        // is part of the menu's markup, not the config, so it is not counted here and the
        // panel always renders it.
        //
        // The caps mirror the label loops above (lift 5, sleep 5, format 10, display 10):
        // those loops write serials 61-65 / 161-165 / 181-190 / 372-381, so a count larger
        // than the cap would tell the panel to draw a button whose label never arrives.

        private ushort LiftButtonCount(ushort roomNumber)
        {
            ushort scenario = _parent.manager.RoomZ[roomNumber].LiftScenario;
            if (scenario == 0 || !_parent.manager.LiftScenarioZ.ContainsKey(scenario)) return 0;
            int count = _parent.manager.LiftScenarioZ[scenario].LiftCmds.Count;
            return (ushort)(count > 5 ? 5 : count);
        }

        private ushort SleepButtonCount(ushort roomNumber)
        {
            ushort scenario = _parent.manager.RoomZ[roomNumber].SleepScenario;
            if (scenario == 0 || !_parent.manager.SleepScenarioZ.ContainsKey(scenario)) return 0;
            int count = _parent.manager.SleepScenarioZ[scenario].SleepCmds.Count;
            return (ushort)(count > 5 ? 5 : count);
        }

        private ushort FormatButtonCount(ushort roomNumber)
        {
            ushort scenario = _parent.manager.RoomZ[roomNumber].FormatScenario;
            if (scenario == 0 || !_parent.manager.FormatScenarioZ.ContainsKey(scenario)) return 0;
            int count = _parent.manager.FormatScenarioZ[scenario].FormatCmds.Count;
            return (ushort)(count > 10 ? 10 : count);
        }

        /// <summary>
        /// Display-select is available only with more than one display — with a single display
        /// there is nothing to choose between — so one display reports 0, not 1.
        /// </summary>
        private ushort DisplayButtonCount(ushort roomNumber)
        {
            ushort displays = _parent.manager.RoomZ[roomNumber].NumberOfDisplays;
            if (displays < 2) return 0;
            return (ushort)(displays > 10 ? 10 : displays);
        }

        public void SelectDisplay(ushort TPNumber, ushort ButtonNumber)
        {
            var tp = _parent.manager.touchpanelZ[TPNumber];
            var room = _parent.manager.RoomZ[tp.CurrentRoomNum];
            ushort currentRoomNumber = _parent.manager.touchpanelZ[TPNumber].CurrentRoomNum;
            // Check if ButtonNumber-1 is valid before accessing the list
            if (ButtonNumber < 1 || ButtonNumber > room.ListOfDisplays.Count)
            {
                CrestronConsole.PrintLine("Warning: Invalid display button number {0} for room {1}",
                    ButtonNumber, room.Name);
                return;
            }
            ushort displayNumber = _parent.manager.RoomZ[currentRoomNumber].ListOfDisplays[(ButtonNumber - 1)];
            ushort videoOutputNumber = _parent.manager.VideoDisplayZ[displayNumber].VideoOutputNum;
            room.CurrentDisplayNumber = displayNumber;
            tp.CurrentDisplayNumber = displayNumber;

            _parent.manager.RoomZ[currentRoomNumber].VideoOutputNum = videoOutputNumber;
            _parent.manager.RoomZ[currentRoomNumber].VideoSrcScenario = _parent.manager.VideoDisplayZ[displayNumber].VideoSourceScenario;
            _parent.manager.RoomZ[currentRoomNumber].ConfigurationScenario = _parent.manager.VideoDisplayZ[displayNumber].VidConfigurationScenario;
            _parent.manager.RoomZ[currentRoomNumber].LiftScenario = _parent.manager.VideoDisplayZ[displayNumber].LiftScenario;
            _parent.manager.RoomZ[currentRoomNumber].FormatScenario = _parent.manager.VideoDisplayZ[displayNumber].FormatScenario;
            _parent.subsystemEISC.UShortInput[(ushort)((TPNumber - 1) * 10 + 302)].UShortValue = videoOutputNumber; //this changes the equipment crosspoint for the TP to connect to the room
            _parent.videoEISC3.StringInput[(ushort)(TPNumber + 2300)].StringValue = _parent.manager.VideoDisplayZ[displayNumber].DisplayName;
            _parent.manager.RoomZ[currentRoomNumber].CurrentVideoSrc = _parent.manager.VideoDisplayZ[displayNumber].CurrentVideoSrc;

            room.BindToCurrentDisplay();           // <<< bind RoomConfig to that display
            tp.SubscribeToVideoMenuEvents(tp.CurrentRoomNum);
            UpdateDisplaysAvailableForSelection(TPNumber, currentRoomNumber); // refresh selected feedback
            UpdateTPVideoMenu(TPNumber);//from selectDisplay
            CrestronConsole.PrintLine("selected {0} out{1}", _parent.manager.VideoDisplayZ[displayNumber].DisplayName, _parent.manager.VideoDisplayZ[displayNumber].VideoOutputNum);
        }

        /// <summary>
        /// Recalculates the InUse flag for every video source from the rooms' current selections,
        /// and pushes the resulting "in use" lamps to the panels. This is the ONLY place InUse is
        /// derived — call it after anything that changes what a room is watching.
        ///
        /// A source with VidSwitcherInputNumber == 0 is LOCAL: it is not fed through the
        /// distributed switcher, so no other room can be watching it and it is never "in use".
        /// That is the same test the routing code uses to decide not to route DM for a source.
        /// </summary>
        public void RecalculateVideoSourceInUse()
        {
            // Iterate the dictionaries, not 1..Count — source and room numbers come from the
            // config and are not guaranteed to be contiguous or to start at 1.
            foreach (var kv in _parent.manager.VideoSourceZ)
            {
                var source = kv.Value;
                if (source.VidSwitcherInputNumber == 0)
                {
                    source.InUse = false;
                    continue;
                }

                bool anyRoomWatching = false;
                foreach (var room in _parent.manager.RoomZ.Values)
                {
                    if (room.CurrentVideoSrc == kv.Key) { anyRoomWatching = true; break; }
                }

                source.InUse = anyRoomWatching;
                if (!anyRoomWatching)
                {
                    _parent.videoEISC1.BooleanInput[(ushort)(source.VidSwitcherInputNumber + 100)].BoolValue = false;
                }
            }

            _parent.RefreshVideoSourceInUseFeedback();
        }
    }
}