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
        
        // Track which touchpanels have been initialized for home page music
        private HashSet<ushort> _homePageMusicInitializedTPs = new HashSet<ushort>();

        /// <summary>
        /// Initializes the Home Page Music Zones for HTML touchpanels
        /// Should be called after HomePageMusicRooms is populated
        /// </summary>
        public void InitializeHomePageMusicZonesForHTML(ushort TPNumber)
        {
            if (!manager.touchpanelZ.ContainsKey(TPNumber) || !manager.touchpanelZ[TPNumber].HTML_UI)
                return;

            // Only initialize once per touchpanel
            if (_homePageMusicInitializedTPs.Contains(TPNumber))
                return;
            _homePageMusicInitializedTPs.Add(TPNumber);

            var tp = manager.touchpanelZ[TPNumber];
            
            CrestronConsole.PrintLine("InitializeHomePageMusicZonesForHTML TP-{0}", TPNumber);

            // Subscribe to music source changes for all rooms with AudioID (only once per room)
            // Use manager.RoomZ directly instead of HomePageMusicRooms since HomePageMusicRooms 
            // may not be populated yet at this point in startup
            foreach (var roomKvp in manager.RoomZ)
            {
                ushort roomNumber = roomKvp.Key;
                var room = roomKvp.Value;
                
                // Skip rooms without audio
                if (room.AudioID == 0)
                    continue;

                // Only subscribe once per room (across all touchpanels)
                if (!_homePageMusicSubscribedRooms.Contains(roomNumber))
                {
                    ushort capturedRoomNumber = roomNumber;

                    // When music source changes, rebuild the entire home page music list
                    room.MusicSrcStatusChanged += (musicSrc, flipsToPage, equipID, name, buttonNum) =>
                    {
                        CrestronConsole.PrintLine("HomePageMusic: Room {0} ({1}) music changed to src={2}", 
                            capturedRoomNumber, room.Name, musicSrc);
                        musicSystemControl.HomePageMusicStatusText();
                    };

                    // Volume changes - need to update the correct slot in the list
                    room.MusicVolumeChanged += (sender, e) =>
                    {
                        // Find which slot this room is in (if any)
                        for (int slotIndex = 0; slotIndex < musicSystemControl.ActiveMusicRoomsList.Count; slotIndex++)
                        {
                            if (musicSystemControl.ActiveMusicRoomsList[slotIndex] == capturedRoomNumber)
                            {
                                int capturedSlot = slotIndex;
                                foreach (var panel in manager.touchpanelZ)
                                {
                                    if (panel.Value.HTML_UI && capturedSlot < panel.Value._HTMLContract.HomeMusicZone.Length)
                                    {
                                        panel.Value._HTMLContract.HomeMusicZone[capturedSlot].Volume(
                                            (sig, wh) => sig.UShortValue = room.MusicVolume);
                                    }
                                }
                                break;
                            }
                        }
                    };

                    // Mute changes - need to update the correct slot in the list
                    room.MusicMutedChanged += (sender, e) =>
                    {
                        CrestronConsole.PrintLine("HomePageMusic MutedChanged: Room {0} ({1}) mute={2}", 
                            capturedRoomNumber, room.Name, room.MusicMuted);
                        // Find which slot this room is in (if any)
                        for (int slotIndex = 0; slotIndex < musicSystemControl.ActiveMusicRoomsList.Count; slotIndex++)
                        {
                            if (musicSystemControl.ActiveMusicRoomsList[slotIndex] == capturedRoomNumber)
                            {
                                int capturedSlot = slotIndex;
                                CrestronConsole.PrintLine("HomePageMusic: Room {0} mute changed to {1}, slot={2}", 
                                    capturedRoomNumber, room.MusicMuted, capturedSlot);
                                foreach (var panel in manager.touchpanelZ)
                                {
                                    if (panel.Value.HTML_UI && capturedSlot < panel.Value._HTMLContract.HomeMusicZone.Length)
                                    {
                                        panel.Value._HTMLContract.HomeMusicZone[capturedSlot].isMuted(
                                            (sig, wh) => sig.BoolValue = room.MusicMuted);
                                    }
                                }
                                break;
                            }
                        }
                    };

                    _homePageMusicSubscribedRooms.Add(roomNumber);
                    CrestronConsole.PrintLine("  Subscribed to room {0} ({1}) AudioID={2}", roomNumber, room.Name, room.AudioID);
                }
            }

            // Subscribe to HTML contract button events for all possible zone slots
            // These handlers look up which room is at that position in ActiveMusicRoomsList
            for (int i = 0; i < tp._HTMLContract.HomeMusicZone.Length; i++)
            {
                int capturedIndex = i;

                // Volume Up
                tp._HTMLContract.HomeMusicZone[capturedIndex].VolumeUp += (sender, args) =>
                {
                    ushort roomNumber = GetRoomAtSlot(capturedIndex);
                    ushort audioID = manager.RoomZ[roomNumber].AudioID;
                    musicEISC1.BooleanInput[(ushort)(audioID)].BoolValue = args.SigArgs.Sig.BoolValue;

                };

                // Volume Down
                tp._HTMLContract.HomeMusicZone[capturedIndex].VolumeDown += (sender, args) =>
                {
                    ushort roomNumber = GetRoomAtSlot(capturedIndex);
                    ushort audioID = manager.RoomZ[roomNumber].AudioID;
                    musicEISC1.BooleanInput[(ushort)(audioID + 100)].BoolValue = args.SigArgs.Sig.BoolValue;
                };

                // Mute toggle
                tp._HTMLContract.HomeMusicZone[capturedIndex].SendMute += (sender, args) =>
                {
                    if (!args.SigArgs.Sig.BoolValue) return;
                    
                    ushort roomNumber = GetRoomAtSlot(capturedIndex);
                    if (roomNumber > 0 && manager.RoomZ.ContainsKey(roomNumber))
                    {
                        var room = manager.RoomZ[roomNumber];
                        ushort audioID = room.AudioID;
                        CrestronConsole.PrintLine("HomeMusicZone Slot[{0}] Mute, Room={1}, AudioID={2}, CurrentMute={3}", 
                            capturedIndex, room.Name, audioID, room.MusicMuted);
                        if (audioID > 0) { 
                            musicEISC1.BooleanInput[(ushort)(audioID + 200)].BoolValue = true;
                            musicEISC1.BooleanInput[(ushort)(audioID + 200)].BoolValue = false;
                        }
                    }
                };

                // Power Off - turns off music for the room at this slot
                tp._HTMLContract.HomeMusicZone[capturedIndex].SendPowerOff += (sender, args) =>
                {
                    if (!args.SigArgs.Sig.BoolValue) return;
                    
                    ushort roomNumber = GetRoomAtSlot(capturedIndex);
                    if (roomNumber > 0 && manager.RoomZ.ContainsKey(roomNumber))
                    {
                        var room = manager.RoomZ[roomNumber];
                        ushort audioID = room.AudioID;
                        CrestronConsole.PrintLine("HomeMusicZone Slot[{0}] PowerOff, Room={1}, AudioID={2}", 
                            capturedIndex, room.Name, audioID);
                        if (audioID > 0)
                            musicSystemControl.SwitcherSelectMusicSource(audioID, 0);
                        // HomePageMusicStatusText will be called via MusicSrcStatusChanged event
                    }
                };
                tp._HTMLContract.HomeMusicZone[capturedIndex].LaunchSource += (sender, args) =>
                {
                    if (!args.SigArgs.Sig.BoolValue) return;
                    CrestronConsole.PrintLine("HomeMusicZone Slot[{0}] LaunchSource", capturedIndex);
                    ushort roomNumber = GetRoomAtSlot(capturedIndex);
                    if (roomNumber > 0 && manager.RoomZ.ContainsKey(roomNumber))
                    {
                        var room = manager.RoomZ[roomNumber];
                        ushort audioID = room.AudioID;
                        ushort currentSrc = room.CurrentMusicSrc;
                        ushort pageNum = manager.MusicSourceZ[currentSrc].FlipsToPageNumber;
                        tp.musicPageFlips(pageNum);

                    }
                };
                // Set Volume (slider)
                tp._HTMLContract.HomeMusicZone[capturedIndex].SetVolume += (sender, args) =>
                {
                    ushort roomNumber = GetRoomAtSlot(capturedIndex);
                    if (roomNumber > 0 && manager.RoomZ.ContainsKey(roomNumber))
                    {
                        ushort audioID = manager.RoomZ[roomNumber].AudioID;
                        CrestronConsole.PrintLine("HomeMusicZone Slot[{0}] SetVolume={1}, Room={2}, AudioID={3}", 
                            capturedIndex, args.SigArgs.Sig.UShortValue, manager.RoomZ[roomNumber].Name, audioID);
                        if (audioID > 0)
                            musicEISC3.UShortInput[(ushort)(audioID + 100)].UShortValue = args.SigArgs.Sig.UShortValue;
                    }
                };
            }

            // Initial population of the list
            musicSystemControl.HomePageMusicStatusText();
            
            CrestronConsole.PrintLine("InitializeHomePageMusicZonesForHTML complete for TP-{0}", TPNumber);
        }

        /// <summary>
        /// Gets the room number at the given slot in the active music list.
        /// Returns 0 if the slot is invalid or empty.
        /// </summary>
        private ushort GetRoomAtSlot(int slotIndex)
        {
            if (slotIndex >= 0 && slotIndex < musicSystemControl.ActiveMusicRoomsList.Count)
            {
                return musicSystemControl.ActiveMusicRoomsList[slotIndex];
            }
            return 0;
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
