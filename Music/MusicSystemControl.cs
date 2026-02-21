using ACS_4Series_Template_V3.QuickActions;
using Crestron.SimplSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ACS_4Series_Template_V3.Music
{

    public class MusicSystemControl
    {
        private ControlSystem _parent;

        public MusicSystemControl(ControlSystem parent)
        {
            _parent = parent;
        }
        public string[] multis = new string[100];
        public bool RecallMusicPresetTimerBusy = false;
        public CTimer SendVolumeAfterMusicPresetTimer;
        public void SwitcherSelectMusicSource(ushort switcherOutputNum, ushort ASRCtoSend)
        {
            ushort videoConfigurationScenario = 0;
            ushort currentRoomNum = 0;
            //set the current music source for the room
            foreach (var rm in _parent.manager.RoomZ)
            {
                if (rm.Value.AudioID == switcherOutputNum)
                {
                    rm.Value.CurrentMusicSrc = ASRCtoSend;
                    currentRoomNum = rm.Value.Number;
                    if (ASRCtoSend > 0)
                    {
                        rm.Value.MusicStatusText = _parent.manager.MusicSourceZ[ASRCtoSend].Name + " is playing. ";
                    }
                    else
                    {
                        rm.Value.MusicStatusText = "";
                    }
                }
            }
            if (ASRCtoSend > 0)
            {
                //calculate whether to select AES67 Stream input 17
                //first get the NAXBoxNumber this source is connected to
                ushort sourceBoxNumber = _parent.manager.MusicSourceZ[ASRCtoSend].NaxBoxNumber;
                //then get the current zones box number
                int zoneBoxNumber = ((switcherOutputNum - 1) / 8) + 1;
                //if the source is on a different box than the zone, use the stream
                //CrestronConsole.PrintLine("sourceBoxNumber{0} zoneBoxNumber{1}", sourceBoxNumber, zoneBoxNumber);
                if (sourceBoxNumber > 0 && sourceBoxNumber != zoneBoxNumber)//then this is a streaming source
                {
                    _parent.musicEISC1.UShortInput[(ushort)(switcherOutputNum + 500)].UShortValue = 17;
                    _parent.musicEISC3.StringInput[(ushort)(switcherOutputNum + 300)].StringValue = _parent.manager.MusicSourceZ[ASRCtoSend].MultiCastAddress;
                    multis[switcherOutputNum] = _parent.manager.MusicSourceZ[ASRCtoSend].MultiCastAddress;
                    CrestronConsole.PrintLine("audio in 17 to out {0} srcNum {1} MULTI {2}", switcherOutputNum, ASRCtoSend, _parent.manager.MusicSourceZ[ASRCtoSend].MultiCastAddress);
                }
                //otherwise its on the same box so just use the switcher input number
                else
                {
                    _parent.musicEISC1.UShortInput[(ushort)(switcherOutputNum + 500)].UShortValue = _parent.manager.MusicSourceZ[ASRCtoSend].SwitcherInputNumber;//switcher input # to output
                    _parent.musicEISC3.StringInput[(ushort)(switcherOutputNum + 300)].StringValue = "0.0.0.0";//clear the multicast address, we're not using streaming
                    CrestronConsole.PrintLine("audio in {1} to out {0} srcNum {2}", switcherOutputNum, _parent.manager.MusicSourceZ[ASRCtoSend].SwitcherInputNumber, ASRCtoSend);
                }
                _parent.musicEISC3.StringInput[(ushort)(switcherOutputNum + 500)].StringValue = _parent.manager.MusicSourceZ[ASRCtoSend].Name;//update the current source to the zone module which also updates the sharing page
                if (_parent.manager.MusicSourceZ[ASRCtoSend].StreamingProviderNumber > 0 && _parent.manager.MusicSourceZ[ASRCtoSend].SwitcherInputNumber > 8)//this is a streaming source
                {
                    _parent.musicEISC1.UShortInput[(ushort)(600 + _parent.manager.MusicSourceZ[ASRCtoSend].SwitcherInputNumber - 8)].UShortValue = _parent.manager.MusicSourceZ[ASRCtoSend].StreamingProviderNumber;
                }
                ReceiverOnOffFromDistAudio(currentRoomNum, ASRCtoSend); //turn on receiver from switcherselectmusicsource
                //updateMusicSourceInUse(ASRCtoSend, manager.MusicSourceZ[ASRCtoSend].SwitcherInputNumber, switcherOutputNum);
            }
            else
            {

                videoConfigurationScenario = _parent.manager.RoomZ[currentRoomNum].ConfigurationScenario;
                CrestronConsole.PrintLine("SwitcherSelectMusicSource ASRCtoSend {0} out{1} config{2}", ASRCtoSend, switcherOutputNum, videoConfigurationScenario);
                if (currentRoomNum > 0 && videoConfigurationScenario > 0 && _parent.manager.VideoConfigScenarioZ[videoConfigurationScenario].HasReceiver)
                {
                    //TODO test for current receiver input so you can turn it off only if its listening to music
                    _parent.videoEISC1.UShortInput[(ushort)(_parent.manager.RoomZ[currentRoomNum].VideoOutputNum + 700)].UShortValue = 0;//receiver input
                }
                SwitcherAudioZoneOff(switcherOutputNum);
                //updateMusicSourceInUse(0, 0, switcherOutputNum);
            }
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="actionNumber"></param>
        public void AudioFloorOff(ushort actionNumber)
        {
            CrestronConsole.PrintLine("STARTING ALL Off {0}:{1}", DateTime.Now.Second, DateTime.Now.Millisecond);
            _parent.nax.NAXAllOffBusy = true;
            _parent.nax.NAXoffTimer = new CTimer(_parent.nax.NAXAllOffCallback, 0, 10000);
            //ha all off
            if (actionNumber == 1)
            {
                CrestronConsole.PrintLine("HA ALL Off");
                for (ushort i = 0; i < 100; i++)
                {
                    //musicEISC1.UShortInput[(ushort)(401 + i)].UShortValue = 0; //clear the asrc button fb
                    _parent.musicEISC1.UShortInput[(ushort)(101 + i)].UShortValue = 0; //clear the current arsc number to the Media Player.
                    _parent.imageEISC.BooleanInput[(ushort)(101 + i)].BoolValue = false; //current subsystem is not audio for any panel.
                    _parent.musicEISC3.UShortInput[(ushort)(101 + i)].UShortValue = 0; //set volume to 0
                    _parent.quickActionControl.updateQuickActionMusicSource((ushort)(i + 1), "Off");
                }

                foreach (var tp in _parent.manager.touchpanelZ)
                {
                    tp.Value.CurrentSubsystemIsAudio = false;
                    for (ushort i = 0; i < 20; i++)
                    {
                        if (tp.Value.HTML_UI)
                        {
                            tp.Value._HTMLContract.musicSourceSelect[i].musicSourceSelected(
                                (sig, wh) => sig.BoolValue = false);
                        }
                        else
                        {
                            tp.Value.UserInterface.SmartObjects[6].BooleanInput[(ushort)(i + 3)].BoolValue = false;//clear the music source button fb
                        }
                    }
                }
                foreach (var room in _parent.manager.RoomZ)
                {
                    room.Value.UpdateMusicSrcStatus(0);//from audioflooroff);
                    room.Value.CurrentMusicSrc = 0;
                    room.Value.MusicStatusText = "";
                    _parent.musicEISC3.StringInput[(ushort)(room.Value.AudioID + 500)].StringValue = "Off";
                    _parent.musicEISC3.StringInput[(ushort)(room.Value.AudioID + 300)].StringValue = "0.0.0.0";
                    ushort config = room.Value.ConfigurationScenario;
                    if (config > 0 && _parent.manager.VideoConfigScenarioZ[config].VideoVolThroughDistAudio && room.Value.CurrentVideoSrc > 0)
                    {
                        CrestronConsole.PrintLine("skipping off command for {0}", room.Value.Name);
                    }
                    else
                    {
                        _parent.musicEISC1.UShortInput[(ushort)(room.Value.AudioID + 500)].UShortValue = 0; //current source to switcher
                    }

                    ReceiverOnOffFromDistAudio(room.Value.Number, 0);//from audioflooroff
                }
            }
            else //floor off
            {
                foreach (ushort rmNum in _parent.manager.Floorz[(ushort)(actionNumber - 1)].IncludedRooms)
                {
                    CrestronConsole.PrintLine("AudioFloorOff {0} {1}", rmNum, _parent.manager.RoomZ[rmNum].Name);
                    _parent.manager.RoomZ[rmNum].UpdateMusicSrcStatus(0);
                    _parent.manager.RoomZ[rmNum].CurrentMusicSrc = 0;
                    _parent.manager.RoomZ[rmNum].MusicStatusText = "";
                    _parent.musicEISC3.StringInput[(ushort)(_parent.manager.RoomZ[rmNum].AudioID + 500)].StringValue = "Off";
                    _parent.quickActionControl.updateQuickActionMusicSource(_parent.manager.RoomZ[rmNum].AudioID, "Off");
                    _parent.musicEISC3.StringInput[(ushort)(_parent.manager.RoomZ[rmNum].AudioID + 300)].StringValue = "0.0.0.0";
                    SwitcherAudioZoneOff(_parent.manager.RoomZ[rmNum].AudioID);
                    ReceiverOnOffFromDistAudio(rmNum, 0);//from audioflooroff
                }
            }
            UpdateAllPanelsTextWhenAudioChanges();//called from AudioFloorOff
            CrestronConsole.PrintLine("FINISHED ALL Off {0}:{1}", DateTime.Now.Second, DateTime.Now.Millisecond);
        }
        public void ReceiverOnOffFromDistAudio(ushort roomNumber, ushort musicSourceNumber)
        {
            if (roomNumber > 0)
            {
                ushort configNum = _parent.manager.RoomZ[roomNumber].ConfigurationScenario;
                bool hasRec = false;
                if (configNum > 0)
                {
                    hasRec = _parent.manager.VideoConfigScenarioZ[configNum].HasReceiver;
                }
                ushort videoSwitcherOutputNum = _parent.manager.RoomZ[roomNumber].VideoOutputNum;//this is also the roomNumber in the simpl program. unfortunately. this should change so the dm output can change easily.
                ushort asrcScenario = _parent.manager.RoomZ[roomNumber].AudioSrcScenario;

                if (hasRec)
                {
                    if (musicSourceNumber == 0)
                    {
                        if (_parent.manager.RoomZ[roomNumber].CurrentVideoSrc == 0 && videoSwitcherOutputNum > 0) //make sure video isn't being watched. TODO - change this to check the current receiver input # and turn it off if its on a music input.
                        {
                            _parent.videoEISC1.UShortInput[(ushort)(videoSwitcherOutputNum + 700)].UShortValue = 0;//turn the receiver off
                        }
                    }
                    else if (asrcScenario > 0) // send the input to the receiver
                    {
                        for (ushort j = 0; j < _parent.manager.AudioSrcScenarioZ[asrcScenario].IncludedSources.Count; j++)
                        {
                            if (musicSourceNumber == _parent.manager.AudioSrcScenarioZ[asrcScenario].IncludedSources[j] && videoSwitcherOutputNum > 0)
                            {
                                _parent.videoEISC1.UShortInput[(ushort)(videoSwitcherOutputNum + 700)].UShortValue = _parent.manager.AudioSrcScenarioZ[asrcScenario].ReceiverInputs[j];//receiver input
                                //turn off video for the room
                                _parent.videoEISC1.UShortInput[(ushort)(videoSwitcherOutputNum + 600)].UShortValue = 0;//TV off - TV input = 0
                                _parent.videoEISC1.UShortInput[(ushort)(videoSwitcherOutputNum + 500)].UShortValue = 0; //DM off 
                                _parent.videoEISC2.StringInput[(ushort)(videoSwitcherOutputNum + 200)].StringValue = "0.0.0.0";//DM NVX multicast address off
                                CrestronConsole.PrintLine("Video off from DISTRIBUTED AUDIO");
                            }
                        }
                    }
                }
            }
        }
        public void PanelSelectMusicSource(ushort TPNumber, ushort ASRCtoSend)
        {
            //set the current music source for the room
            ushort currentRoom = _parent.manager.touchpanelZ[TPNumber].CurrentRoomNum;
            ushort musicSrcScenario = _parent.manager.RoomZ[currentRoom].AudioSrcScenario;
            _parent.manager.RoomZ[currentRoom].CurrentMusicSrc = ASRCtoSend;
            //manager.RoomZ[currentRoom].UpdateMusicSrcStatus(ASRCtoSend);
            if (ASRCtoSend > 0)
            {
                _parent.manager.RoomZ[currentRoom].MusicStatusText = _parent.manager.MusicSourceZ[ASRCtoSend].Name + " is playing.";
                _parent.manager.touchpanelZ[TPNumber].UserInterface.StringInput[3].StringValue = _parent.manager.MusicSourceZ[ASRCtoSend].Name;
                _parent.musicEISC1.UShortInput[(ushort)(TPNumber + 100)].UShortValue = _parent.manager.MusicSourceZ[ASRCtoSend].Number; //send source number for media server object router
                _parent.manager.touchpanelZ[TPNumber].musicPageFlips(_parent.manager.MusicSourceZ[ASRCtoSend].FlipsToPageNumber);
                //musicEISC1.UShortInput[(ushort)(TPNumber + 200)].UShortValue = _parent.manager.MusicSourceZ[ASRCtoSend].FlipsToPageNumber;

                //highlight the button feedback for the music source
                for (ushort i = 0; i < _parent.manager.AudioSrcScenarioZ[musicSrcScenario].IncludedSources.Count; i++)
                {
                    if (_parent.manager.AudioSrcScenarioZ[musicSrcScenario].IncludedSources[i] == ASRCtoSend)
                    {
                        _parent.manager.touchpanelZ[TPNumber].musicButtonFB((ushort)(i + 1));
                    }
                }
                _parent.musicEISC1.UShortInput[(ushort)(TPNumber + 300)].UShortValue = _parent.manager.MusicSourceZ[ASRCtoSend].EquipID;//asrc equipid
            }
            else
            {

                _parent.manager.touchpanelZ[TPNumber].UserInterface.StringInput[3].StringValue = "Off";
                _parent.musicEISC1.UShortInput[(ushort)(TPNumber + 100)].UShortValue = 0;//current asrc number
                _parent.manager.touchpanelZ[TPNumber].musicPageFlips(0);//from select music source - off
                _parent.musicEISC1.UShortInput[(ushort)(TPNumber + 300)].UShortValue = 0;//equip ID
                _parent.manager.touchpanelZ[TPNumber].musicButtonFB(0);
            }
        }
        public void SwitcherAudioZoneOff(ushort audioSwitcherOutputNum)
        {
            CrestronConsole.PrintLine("switcheraudiozoneoff {0} ", audioSwitcherOutputNum);
            ushort roomNum = 0;
            if (audioSwitcherOutputNum > 0)
            {
                //get the room number associated with this audio output
                foreach (var room in _parent.manager.RoomZ)
                {
                    if (room.Value.AudioID == audioSwitcherOutputNum)
                    {
                        roomNum = room.Value.Number;
                    }
                }
                ushort vidConfigScenario = _parent.manager.RoomZ[roomNum].ConfigurationScenario;
                bool vidVolThroughDistAudio = false;
                if (vidConfigScenario > 0)
                {
                    vidVolThroughDistAudio = _parent.manager.VideoConfigScenarioZ[vidConfigScenario].VideoVolThroughDistAudio;
                }
                //if vidVolThroughDistAudio then change the current audio source to the current video source
                //this is if TV was on and then they switched to listen to music then turned the music off it should go back to listening to video
                ushort vsrc = _parent.manager.RoomZ[roomNum].CurrentVideoSrc;
                if (vidVolThroughDistAudio && vsrc > 0)
                {
                    _parent.musicEISC1.UShortInput[(ushort)(audioSwitcherOutputNum + 500)].UShortValue = 17; //
                    _parent.musicEISC3.StringInput[(ushort)(audioSwitcherOutputNum + 300)].StringValue = _parent.manager.VideoSourceZ[vsrc].MultiCastAddress;
                    multis[audioSwitcherOutputNum] = _parent.manager.VideoSourceZ[vsrc].MultiCastAddress; //this is to prevent feedback from going to previous audio source.
                }
                else
                {
                    _parent.musicEISC1.UShortInput[(ushort)(audioSwitcherOutputNum + 500)].UShortValue = 0;//to switcher
                    _parent.musicEISC3.StringInput[(ushort)(audioSwitcherOutputNum + 300)].StringValue = "0.0.0.0"; //multicast off
                    _parent.musicEISC3.StringInput[(ushort)(audioSwitcherOutputNum + 500)].StringValue = "Off";
                    _parent.quickActionControl.updateQuickActionMusicSource(audioSwitcherOutputNum, "Off");
                    CrestronConsole.PrintLine("{0} {1} audio off", roomNum, _parent.manager.RoomZ[roomNum].Name);
                }

            }
        }
        public void RecallMusicPreset(ushort presetNumber)
        {
            //TO DO - add timer to block switcher from updating panels. i think this is done. not sure.
            //TO DO - this is causing an infinite loop. this may not be true. maybe dont need it???
            //TO DO !!!! add a lambda to send the preset number to recall and attach it to the callback


            //in the case that multiple zones are changing sources this delay will let the switching go through and then update the panel status later to prevent bogging down the system by calling the update function every time
            if (presetNumber > 0)
            {
                if (!RecallMusicPresetTimerBusy)
                {
                    _parent.nax.NAXoutputChangedTimer = new CTimer(MusicPresetQuickActionCallback, 0, 5000);
                    RecallMusicPresetTimerBusy = true;
                    CrestronConsole.PrintLine("STARTED RECALL MUSIC PRESET {0}:{1}", DateTime.Now.Second, DateTime.Now.Millisecond);
                }
                foreach (var rm in _parent.manager.RoomZ)
                {
                    ushort switcherOutputNum = rm.Value.AudioID;
                    if (switcherOutputNum > 0)
                    {
                        ushort zoneChecked = _parent.quickActionXML.MusicZoneChecked[presetNumber - 1, switcherOutputNum - 1];
                        if (zoneChecked > 0)
                        {
                            ushort musicSrcToSend = _parent.quickActionXML.Sources[presetNumber - 1, switcherOutputNum - 1];
                            SwitcherSelectMusicSource(switcherOutputNum, musicSrcToSend);//from music preset
                            ReceiverOnOffFromDistAudio(rm.Value.Number, musicSrcToSend);//on from music preset
                            CrestronConsole.PrintLine("presetNumber {0} switcherOutput {1} source{2}", presetNumber, switcherOutputNum, musicSrcToSend);
                        }
                    }
                }
                SendVolumeAfterMusicPresetTimer = new CTimer(SendVolumesMusicPresetCallback, 0, 3000);

            }
        }
        private void MusicPresetQuickActionCallback(object obj)
        {
            _parent.nax.NAXoutputChangedTimer.Stop();
            _parent.nax.NAXoutputChangedTimer.Dispose();
            UpdateAllPanelsTextWhenAudioChanges();//called from MusicPresetQuickActionCallback
            CrestronConsole.PrintLine("##############     NAXoutputChangedCallback {0}:{1}", DateTime.Now.Second, DateTime.Now.Millisecond);

            RecallMusicPresetTimerBusy = false;
        }
        private void SendVolumesMusicPresetCallback(object obj)
        {
            foreach (var rm in _parent.manager.RoomZ)
            {
                ushort switcherOutputNum = rm.Value.AudioID;
                ushort zoneChecked = _parent.quickActionXML.MusicZoneChecked[_parent.quickActionXML.quickActionToRecallOrSave - 1, switcherOutputNum - 1];
                ushort src = _parent.quickActionXML.Sources[_parent.quickActionXML.quickActionToRecallOrSave - 1, switcherOutputNum - 1];
                if (switcherOutputNum > 0 && zoneChecked > 0 && src > 0)
                {
                    ushort volumeToSend = _parent.quickActionXML.Volumes[_parent.quickActionXML.quickActionToRecallOrSave - 1, switcherOutputNum - 1];//need to change musicPresetToRecall to lambda
                    _parent.musicEISC3.UShortInput[(ushort)(100 + switcherOutputNum)].UShortValue = volumeToSend;//send the volume
                }
            }
        }
        public void SendShareSource(ushort sharingRoomNumber, ushort ASRCtoSend)
        {
            ushort inputNum = 0;
            string multicastAddress = "0.0.0.0";
            ushort switcherOutputNum = _parent.manager.RoomZ[sharingRoomNumber].AudioID;//switcher outputnumber for the room to be shared to.
            if (ASRCtoSend > 0)
            {
                inputNum = _parent.manager.MusicSourceZ[ASRCtoSend].SwitcherInputNumber;
                //send the name of the source
                _parent.musicEISC3.StringInput[(ushort)(switcherOutputNum + 500)].StringValue = _parent.manager.MusicSourceZ[ASRCtoSend].Name;
                //update the SAVE_MUSIC_QUICK_ACTION source name
                foreach (var tp in _parent.manager.touchpanelZ)
                {
                    if (tp.Value.HTML_UI)
                    {
                        //TODO - build HTML contract for quick action
                    }
                    else
                    {
                        tp.Value.UserInterface.SmartObjects[30].StringInput[(ushort)(2 * switcherOutputNum)].StringValue = _parent.manager.MusicSourceZ[ASRCtoSend].Name;
                    }
                }
            }
            if (ControlSystem.NAXsystem && ASRCtoSend > 0)
            {
                int zoneBoxNumber = ((switcherOutputNum - 1) / 8) + 1;
                int srcBoxNumber = _parent.manager.MusicSourceZ[ASRCtoSend].NaxBoxNumber;
                if (srcBoxNumber != zoneBoxNumber) //this source will be streamed via multicast 
                {
                    inputNum = 17;
                    multicastAddress = _parent.manager.MusicSourceZ[ASRCtoSend].MultiCastAddress;
                }
            }
            _parent.musicEISC1.UShortInput[(ushort)(switcherOutputNum + 500)].UShortValue = inputNum;//send the source to switcher
            if (ASRCtoSend > 0)
            {
                //send the multicast address
                if (inputNum == 17)
                {
                    _parent.musicEISC3.StringInput[(ushort)(switcherOutputNum + 300)].StringValue = multicastAddress;
                }
                //update the room status
                _parent.manager.RoomZ[sharingRoomNumber].UpdateMusicSrcStatus(ASRCtoSend);
                CrestronConsole.PrintLine("sharing switcherOutputNum{0} - {1} {2}", switcherOutputNum, _parent.manager.MusicSourceZ[ASRCtoSend].Name, _parent.manager.RoomZ[sharingRoomNumber].Name);
                ReceiverOnOffFromDistAudio(sharingRoomNumber, ASRCtoSend);//from selectShareSource
            }
        }
        public void SelectShareSource(ushort TPNumber, ushort zoneButtonNumber)
        {
            //zone button number is 0 based
            try
            {
                //get current room number and current source
                ushort currentRoom = _parent.manager.touchpanelZ[TPNumber].CurrentRoomNum;
                if (currentRoom > 0)
                {
                    ushort currentASRC = _parent.manager.RoomZ[currentRoom].CurrentMusicSrc;//this is the number in the list of music sources
                    ushort sharingRoomNumber = 0;
                    ushort numRooms = 0;
                    List<ushort> roomList = new List<ushort>();

                    if (_parent.manager.RoomZ[currentRoom].AudioSrcSharingScenario > 50)//this means we're using the floor room list
                    {
                        ushort currentFloor = _parent.manager.touchpanelZ[TPNumber].CurrentMusicFloorNum;
                        numRooms = (ushort)_parent.manager.Floorz[currentFloor].IncludedRooms.Count();
                        //build the list of rooms that are not the current room and are part of the music system
                        for (ushort i = 0; i < numRooms; i++)
                        {
                            ushort room = _parent.manager.Floorz[currentFloor].IncludedRooms[i];
                            if (room != currentRoom && _parent.manager.RoomZ[room].AudioID > 0)
                            {
                                roomList.Add(room);
                            }
                        }

                        sharingRoomNumber = roomList[zoneButtonNumber];
                        SendShareSource(sharingRoomNumber, currentASRC);
                        //also ALL MUSIC OFF - the current zone volume feedback should go to 0 but it doesn't

                    }
                    else //we're using the audioSrcSharingScenario not the floor room list
                    {
                        ushort sharingScenario = _parent.manager.RoomZ[currentRoom].AudioSrcSharingScenario;
                        numRooms = (ushort)_parent.manager.AudioSrcSharingScenarioZ[sharingScenario].IncludedZones.Count;
                        //if the current room is in the sharing list skip over it
                        for (ushort i = 0; i < numRooms; i++)
                        {
                            if (_parent.manager.AudioSrcSharingScenarioZ[sharingScenario].IncludedZones[i] != currentRoom)
                            {
                                roomList.Add(_parent.manager.AudioSrcSharingScenarioZ[sharingScenario].IncludedZones[i]);
                            }
                        }
                        sharingRoomNumber = roomList[zoneButtonNumber];
                        SendShareSource(sharingRoomNumber, currentASRC);
                    }

                }
            }
            catch (Exception e)
            {
                ErrorLog.Warn("select share source tpnumber {0} zonebuttonnumber {1} {2} ", TPNumber, zoneButtonNumber, e.Message);
            }
        }


        // Track the current list of active rooms for button handling
        // Position in list -> room number
        public List<ushort> ActiveMusicRoomsList { get; private set; } = new List<ushort>();

        /// <summary>
        /// displays the number of zones playing in the format 'Media playing in X rooms' or if only one zone is playing it shows the source and the room name 'X is playing in RoomName'. This is based on the current music source for each room. If no rooms are playing music, it hides the music subpage on the homepage. This is called from any function that changes the music source for a room. It also updates the text on the homepage music status text.
        /// </summary>
        public void HomePageMusicStatusText()
        {
            CrestronConsole.PrintLine("=== HomePageMusicStatusText called ===");
            CrestronConsole.PrintLine("HomePageMusicRooms count: {0}", _parent.HomePageMusicRooms.Count);
            
            // Build list of active rooms (rooms currently playing music)
            ActiveMusicRoomsList.Clear();
            string firstActiveRoomName = "";
            string firstActiveSourceName = "";

            // Build list of active rooms
            for (int i = 0; i < _parent.HomePageMusicRooms.Count; i++)
            {
                ushort roomNumber = _parent.HomePageMusicRooms[i];
                if (!_parent.manager.RoomZ.ContainsKey(roomNumber))
                    continue;
                
                var room = _parent.manager.RoomZ[roomNumber];
                if (room.CurrentMusicSrc > 0)
                {
                    ActiveMusicRoomsList.Add(roomNumber);
                    if (ActiveMusicRoomsList.Count == 1)
                    {
                        firstActiveRoomName = room.Name;
                        if (_parent.manager.MusicSourceZ.ContainsKey(room.CurrentMusicSrc))
                        {
                            firstActiveSourceName = _parent.manager.MusicSourceZ[room.CurrentMusicSrc].Name;
                        }
                    }
                }
            }

            ushort numberActiveRooms = (ushort)ActiveMusicRoomsList.Count;
            CrestronConsole.PrintLine("  Active rooms: {0}", numberActiveRooms);
            for (int i = 0; i < ActiveMusicRoomsList.Count; i++)
            {
                var room = _parent.manager.RoomZ[ActiveMusicRoomsList[i]];
                CrestronConsole.PrintLine("    Position[{0}] = Room {1} ({2}), AudioID={3}", 
                    i, ActiveMusicRoomsList[i], room.Name, room.AudioID);
            }

            // Update each touchpanel
            foreach (var tp in _parent.manager.touchpanelZ)
            {
                ushort tpNumber = tp.Key;
                
                // Update status text and subpage visibility for all panels
                bool showHomePageMusicSubpage = numberActiveRooms > 0;
                string statusText = numberActiveRooms == 1
                    ? $"{firstActiveSourceName} is playing in {firstActiveRoomName}"
                    : numberActiveRooms > 1
                        ? $"Media playing in {numberActiveRooms} rooms"
                        : "";

                tp.Value.UserInterface.BooleanInput[20].BoolValue = showHomePageMusicSubpage;
                if (numberActiveRooms == 0){
                    tp.Value.UserInterface.BooleanInput[21].BoolValue = false;//hide the volume controls page
                    tp.Value.UserInterface.BooleanInput[2021].BoolValue = false;//hide the media player page.
                }
                tp.Value.UserInterface.StringInput[20].StringValue = statusText;
                
                if (!tp.Value.HTML_UI)
                    continue;

                CrestronConsole.PrintLine("  Updating HTML TP-{0}, NumberOfMusicZones={1}", tpNumber, numberActiveRooms);

                // KEY: Set the list size to the number of ACTIVE zones
                // This tells ch5-list how many items to render
                tp.Value._HTMLContract.HomeNumberOfMusicZones.NumberOfMusicZones(
                    (sig, wh) => sig.UShortValue = numberActiveRooms);

                // Populate positions 0 through (numberActiveRooms-1) with active room data
                for (int i = 0; i < ActiveMusicRoomsList.Count && i < tp.Value._HTMLContract.HomeMusicZone.Length; i++)
                {
                    ushort roomNumber = ActiveMusicRoomsList[i];
                    var room = _parent.manager.RoomZ[roomNumber];
                    ushort currentMusicSrc = room.CurrentMusicSrc;
                    
                    int capturedIndex = i;
                    string roomName = room.Name;
                    ushort volume = room.MusicVolume;
                    bool muted = room.MusicMuted;
                    
                    string sourceName = _parent.manager.MusicSourceZ.ContainsKey(currentMusicSrc)
                        ? _parent.manager.MusicSourceZ[currentMusicSrc].Name
                        : "Unknown";

                    //CrestronConsole.PrintLine("    Slot[{0}] = {1}: src={2}, vol={3}", capturedIndex, roomName, sourceName, volume);

                    tp.Value._HTMLContract.HomeMusicZone[capturedIndex].ZoneName(
                        (sig, wh) => sig.StringValue = roomName);
                    tp.Value._HTMLContract.HomeMusicZone[capturedIndex].isVisible(
                        (sig, wh) => sig.BoolValue = true);  // Always true for items in the active list
                    tp.Value._HTMLContract.HomeMusicZone[capturedIndex].CurrentSource(
                        (sig, wh) => sig.StringValue = sourceName);
                    tp.Value._HTMLContract.HomeMusicZone[capturedIndex].Volume(
                        (sig, wh) => sig.UShortValue = volume);
                    tp.Value._HTMLContract.HomeMusicZone[capturedIndex].isMuted(
                        (sig, wh) => sig.BoolValue = muted);
                }
                
                CrestronConsole.PrintLine("    StatusText: {0}, ShowSubpage: {1}", statusText, showHomePageMusicSubpage);
            }
            
            CrestronConsole.PrintLine("=== HomePageMusicStatusText complete ===");
        }

        public void UpdateTPMusicMenu(ushort TPNumber)
        {//updates the source text on the sharing menu
            ushort currentRoomNumber = _parent.manager.touchpanelZ[TPNumber].CurrentRoomNum;
            ushort currentAudioZone = _parent.manager.RoomZ[currentRoomNumber].AudioID;
            if (currentAudioZone > 0)
            {
                ushort asrcScenarioNum = _parent.manager.RoomZ[currentRoomNumber].AudioSrcScenario;
                ushort numSrcs = (ushort)_parent.manager.AudioSrcScenarioZ[asrcScenarioNum].IncludedSources.Count;
                //for tsr-310s  enable more sources button
                if (numSrcs > 6)
                {
                    //musicEISC1.BooleanInput[(ushort)(TPNumber)].BoolValue = true;
                    _parent.manager.touchpanelZ[TPNumber].UserInterface.BooleanInput[529].BoolValue = true;//enable more music sources button
                }
                else
                {
                    //musicEISC1.BooleanInput[(ushort)(TPNumber)].BoolValue = false; }
                    _parent.manager.touchpanelZ[TPNumber].UserInterface.BooleanInput[529].BoolValue = false; //remove more music sources button
                }

                if (_parent.manager.RoomZ[currentRoomNumber].CurrentMusicSrc > 0)
                {
                    _parent.musicEISC1.UShortInput[(ushort)(TPNumber + 100)].UShortValue = _parent.manager.RoomZ[currentRoomNumber].CurrentMusicSrc;//this doesnt route to the panel
                    _parent.manager.touchpanelZ[TPNumber].musicPageFlips(_parent.manager.MusicSourceZ[_parent.manager.RoomZ[currentRoomNumber].CurrentMusicSrc].FlipsToPageNumber);
                    _parent.musicEISC1.UShortInput[(ushort)(TPNumber + 300)].UShortValue = _parent.manager.MusicSourceZ[_parent.manager.RoomZ[currentRoomNumber].CurrentMusicSrc].EquipID;
                    _parent.manager.touchpanelZ[TPNumber].UserInterface.StringInput[3].StringValue = _parent.manager.MusicSourceZ[_parent.manager.RoomZ[currentRoomNumber].CurrentMusicSrc].Name;
                }
                if (_parent.manager.RoomZ[currentRoomNumber].CurrentMusicSrc == 0)
                {
                    for (ushort i = 0; i < 20; i++)
                    {
                        if (_parent.manager.touchpanelZ[TPNumber].HTML_UI)
                        {
                            _parent.manager.touchpanelZ[TPNumber]._HTMLContract.musicSourceSelect[i].musicSourceSelected(
                                (sig, wh) => sig.BoolValue = false);//clear all button feedback
                        }
                        else
                        {
                            _parent.manager.touchpanelZ[TPNumber].UserInterface.SmartObjects[6].BooleanInput[(ushort)(i + 3)].BoolValue = false;//clear all button feedback
                        }
                    }
                }
                //highlight button fb for the source
                for (ushort i = 0; i < numSrcs; i++)//loop through all music sources in this scenario. 'i+1' will be the button number in the list
                {
                    _parent.manager.touchpanelZ[TPNumber].UserInterface.BooleanInput[(ushort)(530 + ((i + 1) % 6))].BoolValue = false;//first clear the button
                    ushort srcNum = _parent.manager.AudioSrcScenarioZ[asrcScenarioNum].IncludedSources[i];
                    //Update the current audio source of this room to the panel and highlight the appropriate button
                    if (srcNum == _parent.manager.RoomZ[currentRoomNumber].CurrentMusicSrc)
                    {
                        //handle the hand helds
                        if (_parent.manager.touchpanelZ[TPNumber].UseAnalogModes)
                        {
                            if (i == 5)
                            {
                                _parent.manager.touchpanelZ[TPNumber].UserInterface.BooleanInput[536].BoolValue = true;
                            }//fb for button 6
                            else
                            {
                                _parent.manager.touchpanelZ[TPNumber].UserInterface.BooleanInput[(ushort)(530 + ((i + 1) % 6))].BoolValue = true;
                            }
                        }
                        else //this is not a handheld
                        {
                            for (ushort j = 0; j < 20; j++)
                            {
                                if (_parent.manager.touchpanelZ[TPNumber].HTML_UI)
                                {
                                    _parent.manager.touchpanelZ[TPNumber]._HTMLContract.musicSourceSelect[j].musicSourceSelected(
                                        (sig, wh) => sig.BoolValue = false);//clear all button feedback
                                }
                                else
                                {
                                    _parent.manager.touchpanelZ[TPNumber].UserInterface.SmartObjects[6].BooleanInput[(ushort)(j + 3)].BoolValue = false;//clear all button feedback
                                }
                            }
                            if (_parent.manager.touchpanelZ[TPNumber].HTML_UI)
                            {
                                _parent.manager.touchpanelZ[TPNumber]._HTMLContract.musicSourceSelect[i].musicSourceSelected(
                                    (sig, wh) => sig.BoolValue = true);//music source list button number to highlight
                            }
                            else
                            {
                                _parent.manager.touchpanelZ[TPNumber].UserInterface.SmartObjects[6].BooleanInput[(ushort)(i + 3)].BoolValue = true;
                            }
                        }//music source list button number to highlight
                    }
                }
                if (_parent.manager.touchpanelZ[TPNumber].UseAnalogModes)
                {
                    _parent.SetASRCGroup(TPNumber, _parent.manager.touchpanelZ[TPNumber].CurrentASrcGroupNum);
                    for (ushort i = 0; i < 6; i++)
                    {
                        if ((ushort)((_parent.manager.touchpanelZ[TPNumber].CurrentASrcGroupNum - 1) * 6 + i) >= numSrcs) { break; }
                        ushort srcNum = _parent.manager.AudioSrcScenarioZ[asrcScenarioNum].IncludedSources[(ushort)((_parent.manager.touchpanelZ[TPNumber].CurrentASrcGroupNum - 1) * 6 + i)];
                        //in use fb
                        if (_parent.manager.MusicSourceZ[srcNum].InUse)
                        {
                            //inUse |= (int)(1 << i);
                            _parent.manager.touchpanelZ[TPNumber].UserInterface.BooleanInput[(ushort)(551 + i)].BoolValue = true;
                        }//set the bit
                        else
                        {
                            //inUse &= (int)(~(1 << i)); 
                            _parent.manager.touchpanelZ[TPNumber].UserInterface.BooleanInput[(ushort)(551 + i)].BoolValue = false;
                        }//clear bit
                    }
                    //in use analog
                    //musicEISC3.UShortInput[(ushort)(TPNumber)].UShortValue = (ushort)inUse;
                }
            }
        }
        public void updateMusicSourceInUse(ushort sourceNumber, ushort switcherInputNumber, ushort switcherOutputNum)
        {
            ushort currentRoomNumber = 0;
            if (sourceNumber > 0)
            {
                _parent.manager.MusicSourceZ[sourceNumber].InUse = true;
            }
            CrestronConsole.PrintLine("sourceNumber{0} switcherInputNumber{1} output{2}", sourceNumber, switcherInputNumber, switcherOutputNum);
            //update room status to indicate the current source playing
            foreach (var room in _parent.manager.RoomZ)
            {
                if (room.Value.AudioID == switcherOutputNum)//audio ID is the equipID as well as the zone output number
                {
                    currentRoomNumber = room.Value.Number;
                    room.Value.CurrentMusicSrc = sourceNumber;//update the room with the current audio source number
                    //update the music status text
                    if (switcherInputNumber > 0 && sourceNumber > 0)
                    {
                        //check if this source is defined in the config file. it could have switched to an input that isn't defined and will cause an out of bounds error
                        if (_parent.manager.MusicSourceZ.Count >= sourceNumber) //source number is not the same as switcher input number. it is the position in the list of sources
                        {
                            room.Value.MusicStatusText = _parent.manager.MusicSourceZ[sourceNumber].Name + " is playing. ";
                        }
                    }
                    else
                    {
                        room.Value.MusicStatusText = "";
                    }
                }
            }
            //loop through all sources and all rooms to find out if any source is no longer in use
            for (ushort i = 1; i <= _parent.manager.MusicSourceZ.Count; i++)
            {
                ushort k = 0;
                //find out if music source i is in use
                foreach (var room in _parent.manager.RoomZ)
                {
                    if (room.Value.CurrentMusicSrc == i) { k++; }
                }
                if (k == 0)//this means its not in use
                {
                    _parent.manager.MusicSourceZ[i].InUse = false;
                    if (_parent.manager.MusicSourceZ[i].SwitcherInputNumber > 8)//this is a streaming player 
                    {
                        _parent.musicEISC1.UShortInput[(ushort)(600 + _parent.manager.MusicSourceZ[i].SwitcherInputNumber - 8)].UShortValue = 0;//streaming provider off
                    }
                }
            }
            foreach (var tp in _parent.manager.touchpanelZ)
            {
                UpdateTPMusicMenu(tp.Key);
            }
        }
        public void UpdateAllPanelsTextWhenAudioChanges()
        {
            foreach (var tp in _parent.manager.touchpanelZ)
            {
                ushort TPNumber = tp.Value.Number;
                ushort currentRoomNumber = tp.Value.CurrentRoomNum;
                ushort currentMusicSource = _parent.manager.RoomZ[currentRoomNumber].CurrentMusicSrc;
                //only update if the panel is currently on the rooms page

                //find which panels are connected to the current room and update the current source text

                CrestronConsole.PrintLine("TP-{0} Room#{1}", TPNumber, _parent.manager.RoomZ[currentRoomNumber].AudioID);
                if (_parent.manager.RoomZ[currentRoomNumber].CurrentMusicSrc == 0)
                {
                    UpdatePanelToMusicZoneOff(TPNumber);
                }
                else
                {
                    //musicEISC2.StringInput[TPNumber].StringValue = manager.MusicSourceZ[currentMusicSource].Name;//current source to TP
                    _parent.manager.touchpanelZ[TPNumber].UserInterface.StringInput[3].StringValue = _parent.manager.MusicSourceZ[currentMusicSource].Name;//current source to TP
                    CrestronConsole.PrintLine("TP-{0} current music src == {1}", TPNumber, _parent.manager.MusicSourceZ[currentMusicSource].Name);
                    _parent.musicEISC1.UShortInput[(ushort)(TPNumber + 100)].UShortValue = _parent.manager.MusicSourceZ[currentMusicSource].Number;//current asrc number to panel media server and sharing objects
                    _parent.manager.touchpanelZ[TPNumber].musicPageFlips(_parent.manager.MusicSourceZ[currentMusicSource].FlipsToPageNumber);
                    //musicEISC1.UShortInput[(ushort)(TPNumber + 200)].UShortValue = manager.MusicSourceZ[currentMusicSource].FlipsToPageNumber;//current asrc page number to panel
                }
            }
        }

        public void UpdatePanelToMusicZoneOff(ushort TPNumber)
        {
            _parent.manager.touchpanelZ[TPNumber].UserInterface.StringInput[3].StringValue = "Off";//current source to TP
            _parent.musicEISC1.UShortInput[(ushort)(TPNumber + 100)].UShortValue = 0;//current asrc number to panel media server and sharing objects
            _parent.manager.touchpanelZ[TPNumber].musicPageFlips(0);//from UpdatePanelToMusicZoneOff clear the music page
            _parent.manager.touchpanelZ[TPNumber].musicButtonFB(0);//clear the button feedback
        }
        public void RequestMusicSources()
        {
            try
            {
                //first clear out any garbage that may be in there
                for (ushort i = 1; i < 50; i++)
                {
                    _parent.musicEISC3.StringInput[i].StringValue = i.ToString();
                }
                for (ushort i = 1; i <= _parent.manager.MusicSourceZ.Count; i++)
                {
                    ushort eiscposition = 0;
                    if (_parent.manager.MusicSourceZ[i].NaxBoxNumber > 0)
                    {
                        eiscposition = (ushort)((_parent.manager.MusicSourceZ[i].NaxBoxNumber - 1) * 16 + _parent.manager.MusicSourceZ[i].SwitcherInputNumber);
                    }
                    else
                    {
                        eiscposition = _parent.manager.MusicSourceZ[i].SwitcherInputNumber;
                    }
                    if (eiscposition > 0)
                    {
                        _parent.musicEISC3.StringInput[eiscposition].StringValue = _parent.manager.MusicSourceZ[i].Name;
                    }

                }
            }
            catch (Exception e)
            {
                ErrorLog.Error("request musicSourceZ err {0}", e.Message);
            }
            try
            {
                for (ushort i = 1; i <= _parent.manager.RoomZ.Count; i++)
                {
                    ushort outputNum = _parent.manager.RoomZ[i].AudioID; //audio ID is the swamp output number
                    _parent.musicEISC3.StringInput[(ushort)(outputNum + 100)].StringValue = _parent.manager.RoomZ[i].Name;
                }
            }
            catch (Exception e)
            {
                ErrorLog.Error("roomZ err {0}", e.Message);
            }
        }
    }
}
