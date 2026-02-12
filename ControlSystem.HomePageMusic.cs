using System;
using System.Collections.Generic;
using Crestron.SimplSharp;
using ACS_4Series_Template_V3.UI;
using Ch5_Sample_Contract;

namespace ACS_4Series_Template_V3
{
    public partial class ControlSystem
    {
        #region Home Page Music Zones

        // Track which rooms we've already subscribed to for home page music events
        private HashSet<ushort> _homePageMusicSubscribedRooms = new HashSet<ushort>();

        /// <summary>
        /// Initializes the Home Page Music Zones for HTML touchpanels
        /// Should be called after HomePageMusicRooms is populated
        /// </summary>
        public void InitializeHomePageMusicZonesForHTML(ushort TPNumber)
        {
            if (!manager.touchpanelZ.ContainsKey(TPNumber) || !manager.touchpanelZ[TPNumber].HTML_UI)
                return;

            var tp = manager.touchpanelZ[TPNumber];
            ushort numberOfZones = (ushort)HomePageMusicRooms.Count;
            
            CrestronConsole.PrintLine("InitializeHomePageMusicZonesForHTML TP-{0}: {1} total zones", TPNumber, numberOfZones);

            // Set the TOTAL number of zones at startup - this doesn't change
            tp._HTMLContract.HomeNumberOfMusicZones.NumberOfMusicZones(
                (sig, wh) => sig.UShortValue = numberOfZones);

            // Initialize each zone and subscribe to button events
            for (int i = 0; i < numberOfZones && i < tp._HTMLContract.HomeMusicZone.Length; i++)
            {
                int capturedIndex = i;
                ushort roomNumber = HomePageMusicRooms[i];
                
                if (!manager.RoomZ.ContainsKey(roomNumber))
                {
                    CrestronConsole.PrintLine("  Zone[{0}]: Room {1} not found", i, roomNumber);
                    continue;
                }
                
                var room = manager.RoomZ[roomNumber];
                ushort capturedRoomNumber = roomNumber;
                ushort audioID = room.AudioID;

                CrestronConsole.PrintLine("  Zone[{0}]: Room {1} ({2}), AudioID={3}", i, roomNumber, room.Name, audioID);

                // Set initial zone data
                bool isPlaying = room.CurrentMusicSrc > 0;
                string sourceName = isPlaying && manager.MusicSourceZ.ContainsKey(room.CurrentMusicSrc) 
                    ? manager.MusicSourceZ[room.CurrentMusicSrc].Name 
                    : "Off";

                tp._HTMLContract.HomeMusicZone[capturedIndex].ZoneName(
                    (sig, wh) => sig.StringValue = room.Name);
                tp._HTMLContract.HomeMusicZone[capturedIndex].isVisible(
                    (sig, wh) => sig.BoolValue = isPlaying);
                tp._HTMLContract.HomeMusicZone[capturedIndex].CurrentSource(
                    (sig, wh) => sig.StringValue = sourceName);
                tp._HTMLContract.HomeMusicZone[capturedIndex].Volume(
                    (sig, wh) => sig.UShortValue = room.MusicVolume);
                tp._HTMLContract.HomeMusicZone[capturedIndex].isMuted(
                    (sig, wh) => sig.BoolValue = room.MusicMuted);

                // Subscribe to room events (only once per room across all touchpanels)
                if (!_homePageMusicSubscribedRooms.Contains(roomNumber))
                {
                    // When music source changes, update the home page list
                    room.MusicSrcStatusChanged += (musicSrc, flipsToPage, equipID, name, buttonNum) =>
                    {
                        CrestronConsole.PrintLine("HomePageMusic: Room {0} ({1}) music changed to src={2}", 
                            capturedRoomNumber, room.Name, musicSrc);
                        musicSystemControl.HomePageMusicStatusText();
                    };

                    // Volume changes - update directly on all TPs
                    room.MusicVolumeChanged += (sender, e) =>
                    {
                        CrestronConsole.PrintLine("HomePageMusic: Room {0} volume changed to {1}", capturedRoomNumber, room.MusicVolume);
                        foreach (var panel in manager.touchpanelZ)
                        {
                            if (panel.Value.HTML_UI && capturedIndex < panel.Value._HTMLContract.HomeMusicZone.Length)
                            {
                                panel.Value._HTMLContract.HomeMusicZone[capturedIndex].Volume(
                                    (sig, wh) => sig.UShortValue = room.MusicVolume);
                            }
                        }
                    };

                    // Mute changes - update directly on all TPs
                    room.MusicMutedChanged += (sender, e) =>
                    {
                        CrestronConsole.PrintLine("HomePageMusic: Room {0} mute changed to {1}", capturedRoomNumber, room.MusicMuted);
                        foreach (var panel in manager.touchpanelZ)
                        {
                            if (panel.Value.HTML_UI && capturedIndex < panel.Value._HTMLContract.HomeMusicZone.Length)
                            {
                                panel.Value._HTMLContract.HomeMusicZone[capturedIndex].isMuted(
                                    (sig, wh) => sig.BoolValue = room.MusicMuted);
                            }
                        }
                    };

                    _homePageMusicSubscribedRooms.Add(roomNumber);
                }

                // Subscribe to HTML contract button events
                // These handlers use the FIXED index which corresponds to the FIXED room
                
                // Volume Up
                tp._HTMLContract.HomeMusicZone[capturedIndex].VolumeUp += (sender, args) =>
                {
                    if (args.SigArgs.Sig.BoolValue && audioID > 0)
                    {
                        CrestronConsole.PrintLine("HomeMusicZone[{0}] VolumeUp pressed, Room={1}, AudioID={2}", 
                            capturedIndex, room.Name, audioID);
                        musicEISC3.BooleanInput[(ushort)(audioID + 200)].BoolValue = true;
                    }
                    else if (audioID > 0)
                    {
                        musicEISC3.BooleanInput[(ushort)(audioID + 200)].BoolValue = false;
                    }
                };

                // Volume Down
                tp._HTMLContract.HomeMusicZone[capturedIndex].VolumeDown += (sender, args) =>
                {
                    if (args.SigArgs.Sig.BoolValue && audioID > 0)
                    {
                        CrestronConsole.PrintLine("HomeMusicZone[{0}] VolumeDown pressed, Room={1}, AudioID={2}", 
                            capturedIndex, room.Name, audioID);
                        musicEISC3.BooleanInput[(ushort)(audioID + 300)].BoolValue = true;
                    }
                    else if (audioID > 0)
                    {
                        musicEISC3.BooleanInput[(ushort)(audioID + 300)].BoolValue = false;
                    }
                };

                // Mute toggle
                tp._HTMLContract.HomeMusicZone[capturedIndex].SendMute += (sender, args) =>
                {
                    if (args.SigArgs.Sig.BoolValue && audioID > 0)
                    {
                        CrestronConsole.PrintLine("HomeMusicZone[{0}] Mute pressed, Room={1}, AudioID={2}, CurrentMute={3}", 
                            capturedIndex, room.Name, audioID, room.MusicMuted);
                        // Toggle mute
                        musicEISC3.BooleanInput[(ushort)(audioID + 400)].BoolValue = !room.MusicMuted;
                    }
                };

                // Power Off - turns off music for THIS zone only
                tp._HTMLContract.HomeMusicZone[capturedIndex].SendPowerOff += (sender, args) =>
                {
                    if (args.SigArgs.Sig.BoolValue && audioID > 0)
                    {
                        CrestronConsole.PrintLine("HomeMusicZone[{0}] PowerOff pressed, Room={1}, AudioID={2}", 
                            capturedIndex, room.Name, audioID);
                        musicSystemControl.SwitcherSelectMusicSource(audioID, 0);
                        // HomePageMusicStatusText will be called via the MusicSrcStatusChanged event
                    }
                };

                // Set Volume (slider)
                tp._HTMLContract.HomeMusicZone[capturedIndex].SetVolume += (sender, args) =>
                {
                    if (audioID > 0)
                    {
                        CrestronConsole.PrintLine("HomeMusicZone[{0}] SetVolume={1}, Room={2}, AudioID={3}", 
                            capturedIndex, args.SigArgs.Sig.UShortValue, room.Name, audioID);
                        musicEISC3.UShortInput[(ushort)(audioID + 100)].UShortValue = args.SigArgs.Sig.UShortValue;
                    }
                };
            }

            CrestronConsole.PrintLine("InitializeHomePageMusicZonesForHTML complete for TP-{0}", TPNumber);
        }

        #endregion

        #region Music Sharing

        public void UpdateMusicSharingPage(ushort TPNumber, ushort currentRoomNumber)
        {
            ushort numRooms = 0;
            ushort roomNumber = 0;
            ushort flag = 0;
            manager.touchpanelZ[TPNumber].MusicRoomsToShareSourceTo.Clear();
            manager.touchpanelZ[TPNumber].MusicRoomsToShareCheckbox.Clear();

            if (manager.RoomZ[currentRoomNumber].AudioSrcSharingScenario > 0)
            {
                if (manager.RoomZ[currentRoomNumber].CurrentMusicSrc > 0)
                {
                    manager.touchpanelZ[TPNumber].UserInterface.BooleanInput[1001].BoolValue = true;
                }
                else
                {
                    manager.touchpanelZ[TPNumber].UserInterface.BooleanInput[1001].BoolValue = false;
                }

                if (manager.RoomZ[currentRoomNumber].AudioSrcSharingScenario > 50)
                {
                    ushort currentFloor = manager.touchpanelZ[TPNumber].CurrentMusicFloorNum;
                    numRooms = (ushort)this.manager.Floorz[currentFloor].IncludedRooms.Count;
                    if (manager.touchpanelZ[TPNumber].CurrentSubsystemIsAudio && manager.touchpanelZ[TPNumber].SrcSharingButtonFB)
                    {
                        manager.touchpanelZ[TPNumber].UserInterface.BooleanInput[998].BoolValue = false;
                        manager.touchpanelZ[TPNumber].UserInterface.BooleanInput[999].BoolValue = true;
                    }
                    else
                    {
                        manager.touchpanelZ[TPNumber].UserInterface.BooleanInput[999].BoolValue = false;
                    }

                    for (ushort i = 0; i < numRooms; i++)
                    {
                        if (manager.touchpanelZ[TPNumber].HTML_UI)
                        {
                            manager.touchpanelZ[TPNumber]._HTMLContract.MusicRoomControl[i].musicZoneSelected(
                                (sig, wh) => sig.BoolValue = false);
                        }
                        else
                        {
                            manager.touchpanelZ[TPNumber].UserInterface.SmartObjects[7].BooleanInput[(ushort)(i * 7 + 4011)].BoolValue = false;
                        }
                        roomNumber = manager.Floorz[currentFloor].IncludedRooms[i];
                        if (roomNumber == currentRoomNumber || manager.RoomZ[roomNumber].AudioID == 0)
                        {
                            flag++;
                        }
                        else
                        {
                            manager.touchpanelZ[TPNumber].MusicRoomsToShareSourceTo.Add(roomNumber);
                            manager.touchpanelZ[TPNumber].MusicRoomsToShareCheckbox.Add(false);
                        }
                    }
                }
                else
                {
                    numRooms = (ushort)manager.AudioSrcSharingScenarioZ[manager.RoomZ[currentRoomNumber].AudioSrcSharingScenario].IncludedZones.Count;
                    manager.touchpanelZ[TPNumber].UserInterface.BooleanInput[999].BoolValue = false;
                    for (ushort i = 0; i < numRooms; i++)
                    {
                        if (manager.touchpanelZ[TPNumber].HTML_UI)
                        {
                            manager.touchpanelZ[TPNumber]._HTMLContract.MusicRoomControl[i].musicZoneSelected(
                                (sig, wh) => sig.BoolValue = false);
                        }
                        else
                        {
                            manager.touchpanelZ[TPNumber].UserInterface.SmartObjects[7].BooleanInput[(ushort)(i * 7 + 4011)].BoolValue = false;
                        }
                        roomNumber = manager.AudioSrcSharingScenarioZ[manager.RoomZ[currentRoomNumber].AudioSrcSharingScenario].IncludedZones[i];
                        if (roomNumber == currentRoomNumber) { flag = 1; }
                        else
                        {
                            manager.touchpanelZ[TPNumber].MusicRoomsToShareSourceTo.Add(roomNumber);
                            manager.touchpanelZ[TPNumber].MusicRoomsToShareCheckbox.Add(false);
                        }
                    }
                }
                manager.touchpanelZ[TPNumber].UnsubscribeTouchpanelFromAllVolMuteChanges();
                manager.touchpanelZ[TPNumber].UserInterface.UShortInput[2].UShortValue = manager.RoomZ[currentRoomNumber].MusicVolume;
                manager.touchpanelZ[TPNumber].UserInterface.BooleanInput[1009].BoolValue = manager.RoomZ[currentRoomNumber].MusicMuted;
                EventHandler volumeHandler = (sender, e) => manager.touchpanelZ[TPNumber].UserInterface.UShortInput[2].UShortValue = manager.RoomZ[currentRoomNumber].MusicVolume;
                manager.RoomZ[currentRoomNumber].MusicVolumeChanged += volumeHandler;
                manager.touchpanelZ[TPNumber].VolumeChangeHandlers[manager.RoomZ[currentRoomNumber]] = volumeHandler;
                for (ushort j = 0; j < manager.touchpanelZ[TPNumber].MusicRoomsToShareSourceTo.Count; j++)
                {
                    ushort roomNum = manager.touchpanelZ[TPNumber].MusicRoomsToShareSourceTo[j];
                    var rm = manager.RoomZ[roomNum];
                    ushort currentMusicSource = manager.RoomZ[manager.touchpanelZ[TPNumber].MusicRoomsToShareSourceTo[j]].CurrentMusicSrc;
                    if (manager.touchpanelZ[TPNumber].HTML_UI)
                    {
                        manager.touchpanelZ[TPNumber]._HTMLContract.MusicRoomControl[j].musicZoneName(
                            (sig, wh) => sig.StringValue = manager.RoomZ[manager.touchpanelZ[TPNumber].MusicRoomsToShareSourceTo[j]].Name);
                        if (currentMusicSource > 0)
                        {
                            manager.touchpanelZ[TPNumber]._HTMLContract.MusicRoomControl[j].musicZoneSource(
                                (sig, wh) => sig.StringValue = manager.MusicSourceZ[currentMusicSource].Name);
                            manager.touchpanelZ[TPNumber]._HTMLContract.MusicRoomControl[j].musicVolEnable(
                                (sig, wh) => sig.BoolValue = true);
                            manager.touchpanelZ[TPNumber]._HTMLContract.MusicRoomControl[j].musicVolume(
                                (sig, wh) => sig.UShortValue = rm.MusicVolume);
                            manager.touchpanelZ[TPNumber]._HTMLContract.MusicRoomControl[j].musicZoneMuted(
                                (sig, wh) => sig.BoolValue = rm.MusicMuted);
                        }
                        else
                        {
                            manager.touchpanelZ[TPNumber]._HTMLContract.MusicRoomControl[j].musicZoneSource(
                                (sig, wh) => sig.StringValue = "Off");
                            manager.touchpanelZ[TPNumber]._HTMLContract.MusicRoomControl[j].musicVolEnable(
                                (sig, wh) => sig.BoolValue = false);
                        }
                    }
                    else
                    {
                        manager.touchpanelZ[TPNumber].UserInterface.SmartObjects[7].StringInput[(ushort)(2 * j + 11)].StringValue = BuildHTMLString(TPNumber, manager.RoomZ[manager.touchpanelZ[TPNumber].MusicRoomsToShareSourceTo[j]].Name, "24");
                        if (currentMusicSource > 0)
                        {
                            manager.touchpanelZ[TPNumber].UserInterface.SmartObjects[7].StringInput[(ushort)(2 * j + 12)].StringValue = BuildHTMLString(TPNumber, manager.MusicSourceZ[currentMusicSource].Name, "24");
                            manager.touchpanelZ[TPNumber].UserInterface.SmartObjects[7].BooleanInput[(ushort)(7 * j + 4016)].BoolValue = true;
                            manager.touchpanelZ[TPNumber].UserInterface.SmartObjects[7].UShortInput[(ushort)(1 * j + 11)].UShortValue = rm.MusicVolume;
                            manager.touchpanelZ[TPNumber].UserInterface.SmartObjects[7].BooleanInput[(ushort)(7 * j + 4014)].BoolValue = manager.RoomZ[manager.touchpanelZ[TPNumber].MusicRoomsToShareSourceTo[j]].MusicMuted;
                        }
                        else
                        {
                            manager.touchpanelZ[TPNumber].UserInterface.SmartObjects[7].StringInput[(ushort)(2 * j + 12)].StringValue = BuildHTMLString(TPNumber, "Off", "24");
                            manager.touchpanelZ[TPNumber].UserInterface.SmartObjects[7].BooleanInput[(ushort)(7 * j + 4016)].BoolValue = false;
                        }
                    }

                    SubscribeToVolMuteChange(rm, manager.touchpanelZ[TPNumber], j);
                }
                if (manager.touchpanelZ[TPNumber].HTML_UI)
                {
                    manager.touchpanelZ[TPNumber]._HTMLContract.musicNumberOfRooms.numberOfMusicZones(
                            (sig, wh) => sig.UShortValue = (ushort)manager.touchpanelZ[TPNumber].MusicRoomsToShareSourceTo.Count);
                }
                else
                {
                    manager.touchpanelZ[TPNumber].UserInterface.SmartObjects[7].UShortInput[3].UShortValue = (ushort)manager.touchpanelZ[TPNumber].MusicRoomsToShareSourceTo.Count;
                }
            }
            else
            {
                manager.touchpanelZ[TPNumber].UserInterface.BooleanInput[1001].BoolValue = false;
            }
        }

        private void SubscribeToVolMuteChange(Room.RoomConfig room, UI.TouchpanelUI touchpanel, int smartGraphicIndex)
        {
            if (touchpanel.HTML_UI)
            {
                EventHandler muteHandler = (sender, e) => manager.touchpanelZ[touchpanel.Number]._HTMLContract.MusicRoomControl[smartGraphicIndex].musicZoneMuted(
                                (sig, wh) => sig.BoolValue = room.MusicMuted);
                EventHandler volumeHandler = (sender, e) => manager.touchpanelZ[touchpanel.Number]._HTMLContract.MusicRoomControl[smartGraphicIndex].musicVolume(
                                (sig, wh) => sig.UShortValue = room.MusicVolume);
                room.MusicMutedChanged += muteHandler;
                room.MusicVolumeChanged += volumeHandler;
                touchpanel.MuteChangeHandlers[room] = muteHandler;
                touchpanel.VolumeChangeHandlers[room] = volumeHandler;
            }
            else
            {
                EventHandler muteHandler = (sender, e) => touchpanel.UserInterface.SmartObjects[7].BooleanInput[(ushort)(7 * smartGraphicIndex + 4014)].BoolValue = room.MusicMuted;
                EventHandler volumeHandler = (sender, e) => touchpanel.UserInterface.SmartObjects[7].UShortInput[(ushort)(1 * smartGraphicIndex + 11)].UShortValue = room.MusicVolume;
                room.MusicMutedChanged += muteHandler;
                room.MusicVolumeChanged += volumeHandler;
                touchpanel.MuteChangeHandlers[room] = muteHandler;
                touchpanel.VolumeChangeHandlers[room] = volumeHandler;
            }
        }

        #endregion
    }
}
