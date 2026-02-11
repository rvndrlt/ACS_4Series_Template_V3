using ACS_4Series_Template_V3.Music;
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
        public VideoSystemControl(ControlSystem parent)
        {
            _parent = parent;
        }
        public void SelectDisplayVideoSource(ushort displayNumber, ushort sourceButtonNumber)
        {
            if (displayNumber > 0)
            {
                CrestronConsole.PrintLine("display {0} {1} buttonnum {2}", displayNumber, _parent.manager.VideoDisplayZ[displayNumber].DisplayName, sourceButtonNumber);
                ushort videoSwitcherOutputNum = _parent.manager.VideoDisplayZ[displayNumber].VideoOutputNum;
                ushort vidConfigScenario = _parent.manager.VideoDisplayZ[displayNumber].VidConfigurationScenario;
                ushort currentRoomNum = _parent.manager.VideoDisplayZ[displayNumber].AssignedToRoomNum;
                ushort audioSwitcherOutputNum = _parent.manager.RoomZ[currentRoomNum].AudioID;
                ushort vsrcScenario = _parent.manager.VideoDisplayZ[displayNumber].VideoSourceScenario;

                ushort currentVSRC = 0;
                //OFF
                if (sourceButtonNumber == 0)
                {
                    _parent.videoEISC1.UShortInput[(ushort)(videoSwitcherOutputNum + 600)].UShortValue = 0;//display input
                    _parent.videoEISC1.UShortInput[(ushort)(videoSwitcherOutputNum + 700)].UShortValue = 0;//receiver input
                    _parent.videoEISC1.UShortInput[(ushort)(videoSwitcherOutputNum + 800)].UShortValue = 0;//alt switcher input
                    _parent.videoEISC2.StringInput[(ushort)(videoSwitcherOutputNum + 200)].StringValue = "0.0.0.0"; //clear the NVX multicast address
                    _parent.videoEISC2.UShortInput[(ushort)(displayNumber + 400)].UShortValue = 0;
                    _parent.manager.VideoDisplayZ[displayNumber].CurrentVideoSrc = 0;//clear the current source for the display
                    _parent.manager.RoomZ[currentRoomNum].UpdateVideoSrcStatus(0);//from selectDisplayVideoSource
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
                                _parent.musicSystemControl.SwitcherAudioZoneOff(audioSwitcherOutputNum);//turn the audio off
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
                    if (vidConfigScenario > 0 && _parent.manager.VideoConfigScenarioZ[vidConfigScenario].HasReceiver && _parent.manager.VideoConfigScenarioZ[vidConfigScenario].MusicThroughReceiver > 0)
                    {
                        _parent.musicSystemControl.SwitcherAudioZoneOff(audioSwitcherOutputNum);//turn the switcher output off
                    }
                    //this will work for panels that don't use the 6 per page analog modes because srcGroup will always be 1


                    currentVSRC = _parent.manager.VideoSrcScenarioZ[vsrcScenario].IncludedSources[adjustedButtonNum];

                    //set the current video source for the room
                    _parent.manager.RoomZ[currentRoomNum].CurrentVideoSrc = currentVSRC;
                    _parent.manager.VideoDisplayZ[displayNumber].CurrentVideoSrc = currentVSRC;
                    CrestronConsole.PrintLine("vidout{0} to in{1}", videoSwitcherOutputNum, _parent.manager.VideoSourceZ[currentVSRC].VidSwitcherInputNumber);
                    //SEND THE SWITCHING COMMANDS
                    _parent.videoEISC1.UShortInput[(ushort)(videoSwitcherOutputNum + 600)].UShortValue = _parent.manager.VideoSrcScenarioZ[vsrcScenario].DisplayInputs[adjustedButtonNum];
                    _parent.videoEISC1.UShortInput[(ushort)(videoSwitcherOutputNum + 700)].UShortValue = _parent.manager.VideoSrcScenarioZ[vsrcScenario].ReceiverInputs[adjustedButtonNum];
                    _parent.videoEISC1.UShortInput[(ushort)(videoSwitcherOutputNum + 800)].UShortValue = _parent.manager.VideoSrcScenarioZ[vsrcScenario].AltSwitcherInputs[adjustedButtonNum];
                    _parent.videoEISC2.StringInput[(ushort)(videoSwitcherOutputNum + 200)].StringValue = _parent.manager.VideoSourceZ[currentVSRC].StreamLocation;//set the DM NVX Video Source address to subscribe to
                    _parent.videoEISC2.UShortInput[(ushort)(displayNumber + 400)].UShortValue = currentVSRC; //tell the simpl program which source# the display is viewing

                    //send multicast address to audio zone if video sound is through distributed audio
                    //turn on the NAX stream for audio in this zone

                    if (vidConfigScenario > 0 && _parent.manager.VideoConfigScenarioZ[vidConfigScenario].VideoVolThroughDistAudio)
                    {
                        if (ControlSystem.NAXsystem)
                        {
                            if (_parent.is8ZoneBox(currentRoomNum))
                            {
                                _parent.musicEISC1.UShortInput[(ushort)(audioSwitcherOutputNum + 500)].UShortValue = 17;//to switcher
                            }
                            else
                            {
                                _parent.musicEISC1.UShortInput[(ushort)(audioSwitcherOutputNum + 500)].UShortValue = 13;//to switcher
                            }
                            _parent.musicEISC3.StringInput[(ushort)(audioSwitcherOutputNum + 300)].StringValue = _parent.manager.VideoSourceZ[currentVSRC].MultiCastAddress;
                            _parent.musicSystemControl.multis[audioSwitcherOutputNum] = _parent.manager.VideoSourceZ[currentVSRC].MultiCastAddress;
                        }
                        else
                        {
                            _parent.musicEISC1.UShortInput[(ushort)(audioSwitcherOutputNum + 500)].UShortValue = _parent.manager.VideoSourceZ[currentVSRC].AudSwitcherInputNumber;
                        }
                        _parent.manager.RoomZ[currentRoomNum].UpdateMusicSrcStatus(0);//from SelectDisplayVideoSource
                        //manager.RoomZ[currentRoomNum].CurrentMusicSrc = 0;//from SelectDisplayVideoSource
                        //manager.RoomZ[currentRoomNum].MusicStatusText = "";
                    }
                }

                UpdateRoomVideoStatusText(videoSwitcherOutputNum, currentVSRC);

                if (currentVSRC > 0)
                {
                    _parent.videoEISC1.UShortInput[(ushort)(videoSwitcherOutputNum + 500)].UShortValue = _parent.manager.VideoSourceZ[currentVSRC].VidSwitcherInputNumber;//this is for the DM. switcher input # to output
                    _parent.videoEISC1.UShortInput[(ushort)(videoSwitcherOutputNum + 900)].UShortValue = _parent.manager.VideoSourceZ[currentVSRC].VidSwitcherInputNumber;//this is for the room module - this may be redundant 
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
            CrestronConsole.PrintLine("display {0}", displayNumber);
            ushort srcGroup = _parent.manager.touchpanelZ[TPNumber].CurrentVSrcGroupNum;
            _parent.imageEISC.BooleanInput[TPNumber].BoolValue = true;//this tells the program that the current subsystem is video for this panel
            _parent.manager.touchpanelZ[TPNumber].CurrentSubsystemIsVideo = true;
            //this will work for panels that don't use the 6 per page analog modes because srcGroup will always be 1
            if (srcGroup > 0)
            {
                adjustedButtonNum = (ushort)(sourceButtonNumber + (srcGroup - 1) * 6);//this is for a handheld using analog mode buttons 6 per page and shouldn't affect other panels
                CrestronConsole.PrintLine("adjusted {0}", adjustedButtonNum);
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
                if (vidConfigScenario > 0 && _parent.manager.VideoConfigScenarioZ[vidConfigScenario].HasReceiver && _parent.manager.VideoConfigScenarioZ[vidConfigScenario].MusicThroughReceiver > 0)
                {
                    _parent.manager.RoomZ[currentRoomNum].UpdateMusicSrcStatus(0);//from SelectVideoSourceFromTP
                }


                if (vidConfigScenario > 0 && _parent.manager.VideoConfigScenarioZ[vidConfigScenario].VideoVolThroughDistAudio)
                {
                    _parent.manager.RoomZ[currentRoomNum].UpdateMusicSrcStatus(0);//from SelectVideoSourceFromTP
                }
            }
            if (_parent.logging) CrestronConsole.PrintLine("seelctvidesrouce from tp");
            UpdateTPVideoMenu(TPNumber);//from selectVideoSourceFromTP
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
            ushort vidConfigScenario = _parent.manager.VideoDisplayZ[displayNumber].VidConfigurationScenario;
            ushort audioSwitcherOutputNum = _parent.manager.RoomZ[currentRoomNum].AudioID;
            ushort vsrcScenario = _parent.manager.VideoDisplayZ[displayNumber].VideoSourceScenario;
            ushort currentVSRC = 0;
            CrestronConsole.PrintLine("multidisplayvideosource disp{0} btnnum{1}", displayNumber, sourceButtonNumber);
            if (sourceButtonNumber == 0)
            {

                _parent.manager.VideoDisplayZ[displayNumber].CurrentVideoSrc = 0;
                _parent.manager.RoomZ[currentRoomNum].UpdateVideoSrcStatus(0);//from selectmultidisplayvideosource
                if (_parent.manager.VideoConfigScenarioZ[vidConfigScenario].VideoVolThroughDistAudio)
                {
                    _parent.musicSystemControl.SwitcherAudioZoneOff(audioSwitcherOutputNum);//turn the audio off
                }
                for (ushort i = 0; i < numberOfDisplays; i++)
                {
                    ushort currentDisplayNumber = _parent.manager.VideoDisplayZ[displayNumber].TieToDisplayNumbers[i];
                    ushort videoSwitcherOutputNum = _parent.manager.VideoDisplayZ[currentDisplayNumber].VideoOutputNum;

                    _parent.videoEISC1.UShortInput[(ushort)(videoSwitcherOutputNum + 600)].UShortValue = 0;//display input
                    _parent.videoEISC1.UShortInput[(ushort)(videoSwitcherOutputNum + 700)].UShortValue = 0;//receiver input
                    _parent.videoEISC1.UShortInput[(ushort)(videoSwitcherOutputNum + 800)].UShortValue = 0;//alt switcher input
                    _parent.videoEISC2.StringInput[(ushort)(videoSwitcherOutputNum + 200)].StringValue = "0.0.0.0"; //clear the NVX multicast address
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
                if (vidConfigScenario > 0 && _parent.manager.VideoConfigScenarioZ[vidConfigScenario].HasReceiver && _parent.manager.VideoConfigScenarioZ[vidConfigScenario].MusicThroughReceiver > 0)
                {
                    _parent.musicSystemControl.SwitcherAudioZoneOff(audioSwitcherOutputNum);//turn the switcher output off
                }
                //this will work for panels that don't use the 6 per page analog modes because srcGroup will always be 1

                ushort adjustedButtonNum = (ushort)(sourceButtonNumber - 1);//this is for a handheld using analog mode buttons 6 per page and shouldn't affect other panels
                currentVSRC = _parent.manager.VideoSrcScenarioZ[vsrcScenario].IncludedSources[adjustedButtonNum];

                //set the current video source for the room
                _parent.manager.RoomZ[currentRoomNum].CurrentVideoSrc = currentVSRC;
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

                    _parent.videoEISC1.UShortInput[(ushort)(videoSwitcherOutputNum + 500)].UShortValue = _parent.manager.VideoSourceZ[currentVSRC].VidSwitcherInputNumber;//this is for the DM. switcher input # to output
                    _parent.videoEISC1.UShortInput[(ushort)(videoSwitcherOutputNum + 900)].UShortValue = _parent.manager.VideoSourceZ[currentVSRC].VidSwitcherInputNumber;//this is for the room module - this may be redundant 
                    UpdateRoomVideoStatusText(videoSwitcherOutputNum, currentVSRC);

                }
                //send multicast address to audio zone if video sound is through distributed audio
                //turn on the NAX stream for audio in this zone

                if (vidConfigScenario > 0 && _parent.manager.VideoConfigScenarioZ[vidConfigScenario].VideoVolThroughDistAudio)
                {
                    if (_parent.is8ZoneBox(currentRoomNum))
                    {
                        _parent.musicEISC1.UShortInput[(ushort)(audioSwitcherOutputNum + 500)].UShortValue = 17;//to switcher
                    }
                    else
                    {
                        _parent.musicEISC1.UShortInput[(ushort)(audioSwitcherOutputNum + 500)].UShortValue = 13;//to switcher
                    }
                    _parent.musicEISC3.StringInput[(ushort)(audioSwitcherOutputNum + 300)].StringValue = _parent.manager.VideoSourceZ[currentVSRC].MultiCastAddress;
                    _parent.musicSystemControl.multis[audioSwitcherOutputNum] = _parent.manager.VideoSourceZ[currentVSRC].MultiCastAddress;
                    _parent.manager.RoomZ[currentRoomNum].UpdateMusicSrcStatus(0);
                    //manager.RoomZ[currentRoomNum].CurrentMusicSrc = 0;//from SelectMultiDisplayVideoSource
                    //manager.RoomZ[currentRoomNum].MusicStatusText = "";
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
                    ushort audioID = _parent.manager.RoomZ[roomNumber].AudioID;
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
            //CrestronConsole.PrintLine("PopulateTPVideoSourceList {0}", TPNumber);
            ushort currentRoomNumber = _parent.manager.touchpanelZ[TPNumber].CurrentRoomNum;
            ushort numSrcs = (ushort)_parent.manager.VideoSrcScenarioZ[_parent.manager.RoomZ[currentRoomNumber].VideoSrcScenario].IncludedSources.Count;

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
                    //TODO - build HTML contract for video display list
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
                    SelectDisplay(TPNumber, 1);//default to first display
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
                    var room = _parent.manager.RoomZ[roomNum];
                    if (videoSourceNumber == 0 && room.CurrentVideoSrc == 0)
                    {
                        //NOTE TODO - room.CurrentVideoSrc what happens when there are multiple displays?
                        return;
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

        public void UpdateTPVideoMenu(ushort TPNumber)
        {
            ushort currentRoomNumber = _parent.manager.touchpanelZ[TPNumber].CurrentRoomNum;

            if (_parent.manager.RoomZ[currentRoomNumber].VideoSrcScenario > 0)
            {
                ushort numSrcs = (ushort)_parent.manager.VideoSrcScenarioZ[_parent.manager.RoomZ[currentRoomNumber].VideoSrcScenario].IncludedSources.Count;
                ushort currentVSRC = _parent.manager.RoomZ[currentRoomNumber].CurrentVideoSrc;
                ushort vidConfigScenario = _parent.manager.RoomZ[currentRoomNumber].ConfigurationScenario;
                //show or hide the volume feedback indicator guage
                if (_parent.manager.VideoConfigScenarioZ[vidConfigScenario].HasReceiver && _parent.manager.VideoConfigScenarioZ[vidConfigScenario].ReceiverHasVolFB)
                {
                    _parent.manager.touchpanelZ[TPNumber].UserInterface.BooleanInput[153].BoolValue = true; //enable the volume feedback for the receiver
                }
                else { _parent.manager.touchpanelZ[TPNumber].UserInterface.BooleanInput[153].BoolValue = false; }
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

                _parent.manager.touchpanelZ[TPNumber].CurrentVSrcNum = currentVSRC;
                CrestronConsole.PrintLine("UPDATE TP VIDEO MENU TP-{0} room{1} vsrc{2}", TPNumber, currentRoomNumber, currentVSRC);
                //for tsr-310s  enable more sources button
                if (numSrcs > 6) { _parent.manager.touchpanelZ[TPNumber].UserInterface.BooleanInput[509].BoolValue = true; }
                else { _parent.manager.touchpanelZ[TPNumber].UserInterface.BooleanInput[509].BoolValue = false; }
                _parent.subsystemEISC.UShortInput[(ushort)((TPNumber - 1) * 10 + 302)].UShortValue = _parent.manager.RoomZ[currentRoomNumber].VideoOutputNum;//this updates the equipment ID to connect the panel to the room
                if (currentVSRC > 0)
                {
                    _parent.manager.touchpanelZ[TPNumber].videoPageFlips(_parent.manager.VideoSourceZ[currentVSRC].FlipsToPageNumber);//from updateTPVideoMenu
                    _parent.videoEISC1.UShortInput[(ushort)(TPNumber + 300)].UShortValue = _parent.manager.VideoSourceZ[currentVSRC].EquipID;
                    _parent.manager.touchpanelZ[TPNumber].UserInterface.StringInput[2].StringValue = _parent.manager.VideoSourceZ[currentVSRC].DisplayName;
                }
                else//OFF
                {
                    _parent.manager.touchpanelZ[TPNumber].videoPageFlips(0);//from update tpVideoMenu
                    _parent.videoEISC1.UShortInput[(ushort)(TPNumber + 300)].UShortValue = 0;//equip ID
                    _parent.manager.touchpanelZ[TPNumber].UserInterface.StringInput[2].StringValue = "Off";
                    CrestronConsole.PrintLine("VSRC{0} clear button feedback", currentVSRC);
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
            UpdateTPVideoMenu(TPNumber);//from selectDisplay
            CrestronConsole.PrintLine("selected {0} out{1}", _parent.manager.VideoDisplayZ[displayNumber].DisplayName, _parent.manager.VideoDisplayZ[displayNumber].VideoOutputNum);
        }
    }
}
