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
        public void InitializeHomePageMusicZones(ushort TPNumber)
        {
            if (!manager.touchpanelZ.ContainsKey(TPNumber))
                return;

            // Only initialize once per touchpanel
            if (_homePageMusicInitializedTPs.Contains(TPNumber))
                return;
            _homePageMusicInitializedTPs.Add(TPNumber);

            var tp = manager.touchpanelZ[TPNumber];
            
            CrestronConsole.PrintLine("InitializeHomePageMusicZones TP-{0}", TPNumber);

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
                                    else if (!panel.Value.HTML_UI) {
                                        panel.Value.UserInterface.SmartObjects[35].UShortInput[(ushort)(1 * capturedSlot + 11)].UShortValue = room.MusicVolume;
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
                                    else if (!panel.Value.HTML_UI) { 
                                        panel.Value.UserInterface.SmartObjects[35].BooleanInput[(ushort)(5 * capturedSlot + 4014)].BoolValue = room.MusicMuted;
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
            if (tp.HTML_UI)
            {
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
                            //CrestronConsole.PrintLine("HomeMusicZone Slot[{0}] Mute, Room={1}, AudioID={2}, CurrentMute={3}", capturedIndex, room.Name, audioID, room.MusicMuted);
                            if (audioID > 0)
                            {
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
                            //.PrintLine("HomeMusicZone Slot[{0}] PowerOff, Room={1}, AudioID={2}", capturedIndex, room.Name, audioID);
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
                            ushort currentSrc = room.CurrentMusicSrc;
                            if (currentSrc == 0 || !manager.MusicSourceZ.ContainsKey(currentSrc)) return;
                            ushort pageNum = manager.MusicSourceZ[currentSrc].FlipsToPageNumber;
                            if (pageNum == 0) return;
                            // Bypass musicPageFlips — it guards against flipping
                            // while b 21 is open. This is intentional navigation
                            // (chevron tap), so write the join directly.
                            for (ushort k = 0; k < 20; k++)
                                tp.UserInterface.BooleanInput[(ushort)(k + 1021)].BoolValue = false;
                            tp.UserInterface.BooleanInput[(ushort)(pageNum + 1020)].BoolValue = true;
                        }
                    };
                    // Set Volume (slider)
                    tp._HTMLContract.HomeMusicZone[capturedIndex].SetVolume += (sender, args) =>
                    {
                        ushort roomNumber = GetRoomAtSlot(capturedIndex);
                        if (roomNumber > 0 && manager.RoomZ.ContainsKey(roomNumber))
                        {
                            ushort audioID = manager.RoomZ[roomNumber].AudioID;
                            //CrestronConsole.PrintLine("HomeMusicZone Slot[{0}] SetVolume={1}, Room={2}, AudioID={3}", capturedIndex, args.SigArgs.Sig.UShortValue, manager.RoomZ[roomNumber].Name, audioID);
                            if (audioID > 0)
                                VOLUMEEISC.UShortInput[audioID].UShortValue = args.SigArgs.Sig.UShortValue;
                        }
                    };
                }
            }

            // Initial population of the list
            musicSystemControl.HomePageMusicStatusText();
            
            CrestronConsole.PrintLine("InitializeHomePageMusicZonesForHTML complete for TP-{0}", TPNumber);
        }

        /// <summary>
        /// Gets the room number at the given slot in the active music list.
        /// Returns 0 if the slot is invalid or empty.
        /// </summary>
        internal ushort GetRoomAtSlot(int slotIndex)
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

        #region Add Room To Group Menu (HomeMusicScenario2)

        /// <summary>
        /// Returns the audio subsystem's IncludedFloors list from the TP's
        /// whole-house scenario, or null if nothing is configured.
        /// The audio subsystem is identified by name (case-insensitive "Audio").
        /// </summary>
        private List<ushort> GetAudioSubsystemIncludedFloors(UI.TouchpanelUI tp)
        {
            ushort audioSubsystemNum = 0;
            foreach (var kvp in manager.SubsystemZ)
            {
                if (kvp.Value != null && string.Equals(kvp.Value.Name, "Audio", StringComparison.OrdinalIgnoreCase))
                {
                    audioSubsystemNum = kvp.Key;
                    break;
                }
            }
            if (audioSubsystemNum == 0) return null;

            ushort whScenario = tp.HomePageScenario;
            if (!manager.WholeHouseSubsystemScenarioZ.ContainsKey(whScenario)) return null;

            var whSubs = manager.WholeHouseSubsystemScenarioZ[whScenario].WholeHouseSubsystems;
            if (whSubs == null) return null;
            for (int i = 0; i < whSubs.Count; i++)
            {
                if (whSubs[i].SubsystemNumber == audioSubsystemNum)
                    return whSubs[i].IncludedFloors;
            }
            return null;
        }


        // Raw join numbers shared with homeMusicAddToGroup HTML binding.
        // Distinct from audio-sharing dialog joins (998/999) so they don't interfere.
        private const ushort AddToGroupShowJoin     = 1500; // b/s: C# -> HTML (visibility + title)
        private const ushort AddToGroupSlotJoin     = 1500; // n:   HTML -> C# (slot index before trigger)
        private const ushort AddToGroupOpenJoin     = 1501; // b:   HTML -> C# (rising edge)
        private const ushort AddToGroupDoneJoin     = 1502; // b:   HTML -> C# (rising edge)

        /// <summary>
        /// Opens the "Add room to this group" menu for a touchpanel.
        /// slotIndex is the position in the scenario-2 list the user tapped; the
        /// room's current music source becomes the target of the add-to-group action.
        /// </summary>
        public void OpenAddToGroupMenu(ushort TPNumber, ushort slotIndex)
        {
            if (!manager.touchpanelZ.ContainsKey(TPNumber)) return;
            var tp = manager.touchpanelZ[TPNumber];

            if (slotIndex >= musicSystemControl.ActiveMusicRoomsList.Count) return;
            ushort roomNum = musicSystemControl.ActiveMusicRoomsList[slotIndex];
            if (!manager.RoomZ.ContainsKey(roomNum)) return;

            ushort srcNum = manager.RoomZ[roomNum].CurrentMusicSrc;
            if (srcNum == 0 || !manager.MusicSourceZ.ContainsKey(srcNum)) return;

            tp.AddToGroupTargetSource = srcNum;

            // Populate the floor list from the audio subsystem's IncludedFloors
            // on the TP's whole-house scenario (NOT the TP's FloorScenario).
            // This lets the integrator exclude floors that have no audio-capable
            // rooms via config rather than runtime logic.
            var audioFloors = GetAudioSubsystemIncludedFloors(tp);
            if (audioFloors != null && audioFloors.Count > 0)
            {
                bool currentFloorInList = false;
                for (int k = 0; k < audioFloors.Count; k++)
                {
                    if (audioFloors[k] == tp.CurrentMusicFloorNum) { currentFloorInList = true; break; }
                }
                if (!currentFloorInList)
                {
                    tp.CurrentMusicFloorNum = audioFloors[0];
                }

                tp._HTMLContract.FloorList.NumberOfFloors(
                    (sig, wh) => sig.UShortValue = (ushort)audioFloors.Count);
                for (int fi = 0; fi < audioFloors.Count && fi < tp._HTMLContract.FloorSelect.Length; fi++)
                {
                    ushort fNum = audioFloors[fi];
                    int capturedFi = fi;
                    string fName = manager.Floorz.ContainsKey(fNum) ? manager.Floorz[fNum].Name : "";
                    tp._HTMLContract.FloorSelect[capturedFi].FloorName(
                        (sig, wh) => sig.StringValue = fName);
                    tp._HTMLContract.FloorSelect[capturedFi].FloorIsSelected(
                        (sig, wh) => sig.BoolValue = (fNum == tp.CurrentMusicFloorNum));
                }
            }

            UpdateAddToGroupPage(TPNumber);

            // Push title and show dialog.
            string srcName = manager.MusicSourceZ[srcNum].Name;
            if (tp.HTML_UI)
            {
                tp.UserInterface.StringInput[AddToGroupShowJoin].StringValue = "Add rooms to: " + srcName;
                tp.UserInterface.BooleanInput[AddToGroupShowJoin].BoolValue = true;
            }
        }

        /// <summary>
        /// Closes the "Add room to this group" menu and resets add-to-group state
        /// so the existing audio-sharing flow is unaffected.
        /// </summary>
        public void CloseAddToGroupMenu(ushort TPNumber)
        {
            if (!manager.touchpanelZ.ContainsKey(TPNumber)) return;
            var tp = manager.touchpanelZ[TPNumber];

            bool wasInitiateMode = tp.InitiateMusicMode;

            tp.AddToGroupTargetSource = 0;
            tp.MusicRoomsToShareSourceTo.Clear();
            tp.MusicRoomsToShareCheckbox.Clear();
            tp.InitiateMusicMode = false;

            if (tp.HTML_UI)
            {
                tp.UserInterface.BooleanInput[AddToGroupShowJoin].BoolValue = false;
                tp._HTMLContract.musicNumberOfRooms.numberOfMusicZones(
                    (sig, wh) => sig.UShortValue = 0);
            }

            if (wasInitiateMode)
            {
                // Initiate mode complete: rebuild groups, then show S2.
                musicSystemControl.HomePageMusicStatusText();
                tp.UserInterface.BooleanInput[20].BoolValue = false;
                tp.UserInterface.BooleanInput[21].BoolValue = true;
            }
        }

        /// <summary>
        /// Opens the "Add room to group" menu during initiate-music flow.
        /// Uses the specified source directly rather than looking up from a slot
        /// in ActiveMusicRoomsList (which is empty during initiation).
        /// </summary>
        public void OpenInitiateAddToGroupMenu(ushort TPNumber, ushort srcNum)
        {
            if (!manager.touchpanelZ.ContainsKey(TPNumber)) return;
            var tp = manager.touchpanelZ[TPNumber];
            if (srcNum == 0 || !manager.MusicSourceZ.ContainsKey(srcNum)) return;

            tp.AddToGroupTargetSource = srcNum;

            var audioFloors = GetAudioSubsystemIncludedFloors(tp);
            if (audioFloors != null && audioFloors.Count > 0)
            {
                bool currentFloorInList = false;
                for (int k = 0; k < audioFloors.Count; k++)
                {
                    if (audioFloors[k] == tp.CurrentMusicFloorNum) { currentFloorInList = true; break; }
                }
                if (!currentFloorInList)
                {
                    tp.CurrentMusicFloorNum = audioFloors[0];
                }

                tp._HTMLContract.FloorList.NumberOfFloors(
                    (sig, wh) => sig.UShortValue = (ushort)audioFloors.Count);
                for (int fi = 0; fi < audioFloors.Count && fi < tp._HTMLContract.FloorSelect.Length; fi++)
                {
                    ushort fNum = audioFloors[fi];
                    int capturedFi = fi;
                    string fName = manager.Floorz.ContainsKey(fNum) ? manager.Floorz[fNum].Name : "";
                    tp._HTMLContract.FloorSelect[capturedFi].FloorName(
                        (sig, wh) => sig.StringValue = fName);
                    tp._HTMLContract.FloorSelect[capturedFi].FloorIsSelected(
                        (sig, wh) => sig.BoolValue = (fNum == tp.CurrentMusicFloorNum));
                }
            }

            UpdateAddToGroupPage(TPNumber);

            string srcName = manager.MusicSourceZ[srcNum].Name;
            if (tp.HTML_UI)
            {
                tp.UserInterface.StringInput[AddToGroupShowJoin].StringValue = "Select rooms for: " + srcName;
                tp.UserInterface.BooleanInput[AddToGroupShowJoin].BoolValue = true;
            }
        }

        /// <summary>
        /// Handles a floor-button press while the add-to-group menu is open.
        /// Resolves the button number against the audio subsystem's IncludedFloors
        /// (the list the menu was populated from), updates feedback, and rebuilds
        /// the room list for the new floor.
        /// </summary>
        public void SelectAddToGroupFloor(ushort TPNumber, ushort floorButtonNumber)
        {
            if (!manager.touchpanelZ.ContainsKey(TPNumber)) return;
            var tp = manager.touchpanelZ[TPNumber];
            if (tp.AddToGroupTargetSource == 0) return;

            var audioFloors = GetAudioSubsystemIncludedFloors(tp);
            if (audioFloors == null || floorButtonNumber == 0) return;
            int idx = floorButtonNumber - 1;
            if (idx < 0 || idx >= audioFloors.Count) return;

            tp.CurrentMusicFloorNum = audioFloors[idx];
            tp.musicFloorButtonFB(floorButtonNumber);
            UpdateAddToGroupPage(TPNumber);
        }

        /// <summary>
        /// Populates MusicRoomControl[] for the add-to-group menu based on the
        /// touchpanel's CurrentMusicFloorNum, filtered by AudioID > 0 and by
        /// whether the target source exists in the room's AudioSrcScenario.
        /// Checkbox state reflects whether the room is already playing the
        /// target source.
        /// </summary>
        public void UpdateAddToGroupPage(ushort TPNumber)
        {
            if (!manager.touchpanelZ.ContainsKey(TPNumber)) return;
            var tp = manager.touchpanelZ[TPNumber];
            if (tp.AddToGroupTargetSource == 0) return;
            if (!tp.HTML_UI) return;

            ushort targetSrc = tp.AddToGroupTargetSource;
            ushort floorNum = tp.CurrentMusicFloorNum;
            if (!manager.Floorz.ContainsKey(floorNum)) return;

            tp.UnsubscribeTouchpanelFromAllVolMuteChanges();
            tp.MusicRoomsToShareSourceTo.Clear();
            tp.MusicRoomsToShareCheckbox.Clear();

            var floorRooms = manager.Floorz[floorNum].IncludedRooms;
            for (int i = 0; i < floorRooms.Count; i++)
            {
                ushort roomNum = floorRooms[i];
                if (!manager.RoomZ.ContainsKey(roomNum)) continue;
                var room = manager.RoomZ[roomNum];
                if (room.AudioID == 0) continue;

                // Skip rooms whose audio-source scenario doesn't include the
                // target source — they can't play it anyway.
                if (!manager.AudioSrcScenarioZ.ContainsKey(room.AudioSrcScenario)) continue;
                if (!manager.AudioSrcScenarioZ[room.AudioSrcScenario].IncludedSources.Contains(targetSrc)) continue;

                bool isOnTargetSrc = room.CurrentMusicSrc == targetSrc;
                tp.MusicRoomsToShareSourceTo.Add(roomNum);
                tp.MusicRoomsToShareCheckbox.Add(isOnTargetSrc);
            }

            int count = tp.MusicRoomsToShareSourceTo.Count;
            tp._HTMLContract.musicNumberOfRooms.numberOfMusicZones(
                (sig, wh) => sig.UShortValue = (ushort)count);

            for (int j = 0; j < count && j < tp._HTMLContract.MusicRoomControl.Length; j++)
            {
                ushort roomNum = tp.MusicRoomsToShareSourceTo[j];
                var room = manager.RoomZ[roomNum];
                bool isChecked = tp.MusicRoomsToShareCheckbox[j];

                int capturedIndex = j;
                string roomName = room.Name;
                string sourceLabel = room.CurrentMusicSrc > 0 && manager.MusicSourceZ.ContainsKey(room.CurrentMusicSrc)
                    ? manager.MusicSourceZ[room.CurrentMusicSrc].Name
                    : "Off";

                tp._HTMLContract.MusicRoomControl[capturedIndex].musicZoneName(
                    (sig, wh) => sig.StringValue = roomName);
                tp._HTMLContract.MusicRoomControl[capturedIndex].musicZoneSource(
                    (sig, wh) => sig.StringValue = sourceLabel);
                tp._HTMLContract.MusicRoomControl[capturedIndex].musicZoneSelected(
                    (sig, wh) => sig.BoolValue = isChecked);
                // No per-room volume in the add-to-group menu.
                tp._HTMLContract.MusicRoomControl[capturedIndex].musicVolEnable(
                    (sig, wh) => sig.BoolValue = false);
            }
        }

        #endregion

        #region Change Group Source Menu (HomeMusicScenario2)

        // Raw joins for the "Change music source for this group" menu.
        private const ushort ChangeGroupSrcShowJoin      = 1503; // b/s: C# -> HTML (visibility + title)
        private const ushort ChangeGroupSrcSlotJoin      = 1501; // n:   HTML -> C#
        private const ushort ChangeGroupSrcOpenJoin      = 1504; // b:   HTML -> C# pulse
        private const ushort ChangeGroupSrcCancelJoin    = 1505; // b:   HTML -> C# pulse
        private const ushort ChangeGroupSrcEmptyJoin     = 1506; // b:   C# -> HTML (shows empty-state message)

        /// <summary>
        /// Opens the "Change music source for this group" menu for the group that
        /// contains the room at slotIndex. Populates musicSourceSelect[] with the
        /// intersection of each group-room's AudioSrcScenario.IncludedSources,
        /// excluding the current group source.
        /// </summary>
        public void OpenChangeGroupSourceMenu(ushort TPNumber, ushort slotIndex)
        {
            if (!manager.touchpanelZ.ContainsKey(TPNumber)) return;
            var tp = manager.touchpanelZ[TPNumber];

            if (slotIndex >= musicSystemControl.ActiveMusicRoomsList.Count) return;
            ushort anchorRoom = musicSystemControl.ActiveMusicRoomsList[slotIndex];
            if (!manager.RoomZ.ContainsKey(anchorRoom)) return;
            ushort groupSrc = manager.RoomZ[anchorRoom].CurrentMusicSrc;
            if (groupSrc == 0 || !manager.MusicSourceZ.ContainsKey(groupSrc)) return;

            tp.ChangeGroupSourceCurrentSrc = groupSrc;

            // Compute intersection of IncludedSources across all rooms in this group.
            List<ushort> commonSrcs = null;
            for (int i = 0; i < musicSystemControl.ActiveMusicRoomsList.Count; i++)
            {
                ushort rn = musicSystemControl.ActiveMusicRoomsList[i];
                if (!manager.RoomZ.ContainsKey(rn)) continue;
                var r = manager.RoomZ[rn];
                if (r.CurrentMusicSrc != groupSrc) continue;
                if (!manager.AudioSrcScenarioZ.ContainsKey(r.AudioSrcScenario)) continue;

                var roomSrcs = manager.AudioSrcScenarioZ[r.AudioSrcScenario].IncludedSources;
                if (commonSrcs == null)
                {
                    commonSrcs = new List<ushort>(roomSrcs);
                }
                else
                {
                    var next = new List<ushort>();
                    for (int j = 0; j < commonSrcs.Count; j++)
                    {
                        if (roomSrcs.Contains(commonSrcs[j])) next.Add(commonSrcs[j]);
                    }
                    commonSrcs = next;
                }
            }
            if (commonSrcs == null) commonSrcs = new List<ushort>();

            // Exclude the current group source — picking it is a no-op.
            commonSrcs.RemoveAll(s => s == groupSrc);

            tp.ChangeGroupSourceCommonSrcs = commonSrcs;

            // Push tile data.
            ushort count = (ushort)commonSrcs.Count;
            tp._HTMLContract.musicSourceList.numberOfMusicSources(
                (sig, wh) => sig.UShortValue = count);
            for (int i = 0; i < commonSrcs.Count && i < tp._HTMLContract.musicSourceSelect.Length; i++)
            {
                int ci = i;
                ushort sNum = commonSrcs[ci];
                var src = manager.MusicSourceZ.ContainsKey(sNum) ? manager.MusicSourceZ[sNum] : null;
                string nm = src != null ? src.Name : "";
                string ic = src != null ? src.IconHTML : "";
                tp._HTMLContract.musicSourceSelect[ci].musicSourceName(
                    (sig, wh) => sig.StringValue = nm);
                tp._HTMLContract.musicSourceSelect[ci].musicSourceIcon(
                    (sig, wh) => sig.StringValue = ic);
                tp._HTMLContract.musicSourceSelect[ci].musicSourceSelected(
                    (sig, wh) => sig.BoolValue = false);
            }

            string groupSrcName = manager.MusicSourceZ[groupSrc].Name;
            tp.UserInterface.StringInput[ChangeGroupSrcShowJoin].StringValue =
                "Change source for: " + groupSrcName;
            tp.UserInterface.BooleanInput[ChangeGroupSrcEmptyJoin].BoolValue = (count == 0);
            tp.UserInterface.BooleanInput[ChangeGroupSrcShowJoin].BoolValue = true;
        }

        /// <summary>
        /// Closes the "Change music source for this group" menu and resets state.
        /// Does not change any room sources.
        /// </summary>
        public void CloseChangeGroupSourceMenu(ushort TPNumber)
        {
            if (!manager.touchpanelZ.ContainsKey(TPNumber)) return;
            var tp = manager.touchpanelZ[TPNumber];

            tp.ChangeGroupSourceCurrentSrc = 0;
            tp.ChangeGroupSourceCommonSrcs.Clear();
            tp.InitiateMusicMode = false;

            tp.UserInterface.BooleanInput[ChangeGroupSrcShowJoin].BoolValue = false;
            tp.UserInterface.BooleanInput[ChangeGroupSrcEmptyJoin].BoolValue = false;
            tp._HTMLContract.musicSourceList.numberOfMusicSources(
                (sig, wh) => sig.UShortValue = 0);
        }

        /// <summary>
        /// Opens the source picker in "initiate music" mode — all sources for the
        /// TP's current room are shown (no intersection, no exclusion). After the
        /// user selects a source, the flow chains to AddToGroup, then to S2.
        /// </summary>
        public void OpenInitiateMusicSourceMenu(ushort TPNumber)
        {
            if (!manager.touchpanelZ.ContainsKey(TPNumber)) return;
            var tp = manager.touchpanelZ[TPNumber];

            ushort roomNum = tp.CurrentRoomNum;
            if (!manager.RoomZ.ContainsKey(roomNum)) return;
            ushort asrcScenario = manager.RoomZ[roomNum].AudioSrcScenario;
            if (!manager.AudioSrcScenarioZ.ContainsKey(asrcScenario)) return;

            var allSrcs = manager.AudioSrcScenarioZ[asrcScenario].IncludedSources;
            if (allSrcs == null || allSrcs.Count == 0) return;

            tp.InitiateMusicMode = true;
            // Use ChangeGroupSourceCommonSrcs to hold the source list so that
            // the selectMusicSource handler can resolve tile index → source number.
            tp.ChangeGroupSourceCommonSrcs = new List<ushort>(allSrcs);
            // Set sentinel so the selectMusicSource handler takes the common-srcs branch.
            tp.ChangeGroupSourceCurrentSrc = ushort.MaxValue;

            // Push tile data.
            ushort count = (ushort)allSrcs.Count;
            tp._HTMLContract.musicSourceList.numberOfMusicSources(
                (sig, wh) => sig.UShortValue = count);
            for (int i = 0; i < allSrcs.Count && i < tp._HTMLContract.musicSourceSelect.Length; i++)
            {
                int ci = i;
                ushort sNum = allSrcs[ci];
                var src = manager.MusicSourceZ.ContainsKey(sNum) ? manager.MusicSourceZ[sNum] : null;
                string nm = src != null ? src.Name : "";
                string ic = src != null ? src.IconHTML : "";
                tp._HTMLContract.musicSourceSelect[ci].musicSourceName(
                    (sig, wh) => sig.StringValue = nm);
                tp._HTMLContract.musicSourceSelect[ci].musicSourceIcon(
                    (sig, wh) => sig.StringValue = ic);
                tp._HTMLContract.musicSourceSelect[ci].musicSourceSelected(
                    (sig, wh) => sig.BoolValue = false);
            }

            tp.UserInterface.StringInput[ChangeGroupSrcShowJoin].StringValue = "Select a music source";
            tp.UserInterface.BooleanInput[ChangeGroupSrcEmptyJoin].BoolValue = false;
            tp.UserInterface.BooleanInput[ChangeGroupSrcShowJoin].BoolValue = true;
        }

        /// <summary>
        /// Applies a newly-selected source to every room in the current group
        /// (rooms whose CurrentMusicSrc equals the stored group source), then
        /// closes the menu.
        /// </summary>
        public void ApplyChangeGroupSource(ushort TPNumber, ushort newSrc)
        {
            if (!manager.touchpanelZ.ContainsKey(TPNumber)) return;
            var tp = manager.touchpanelZ[TPNumber];
            if (tp.ChangeGroupSourceCurrentSrc == 0) return;
            if (newSrc == 0) return;

            ushort oldSrc = tp.ChangeGroupSourceCurrentSrc;
            CrestronConsole.PrintLine("ApplyChangeGroupSource: oldSrc={0} newSrc={1} ActiveListCount={2}",
                oldSrc, newSrc, musicSystemControl.ActiveMusicRoomsList.Count);

            musicSystemControl.BeginSuppressRebuild();
            try
            {
                var roomsToSwitch = new List<ushort>();
                for (int i = 0; i < musicSystemControl.ActiveMusicRoomsList.Count; i++)
                {
                    ushort rn = musicSystemControl.ActiveMusicRoomsList[i];
                    if (!manager.RoomZ.ContainsKey(rn)) continue;
                    ushort roomSrc = manager.RoomZ[rn].CurrentMusicSrc;
                    CrestronConsole.PrintLine("  Scan slot[{0}] room={1} ({2}) audioID={3} src={4} match={5}",
                        i, rn, manager.RoomZ[rn].Name, manager.RoomZ[rn].AudioID, roomSrc, roomSrc == oldSrc);
                    if (roomSrc == oldSrc)
                    {
                        roomsToSwitch.Add(rn);
                    }
                }
                CrestronConsole.PrintLine("  roomsToSwitch count={0}", roomsToSwitch.Count);
                for (int i = 0; i < roomsToSwitch.Count; i++)
                {
                    ushort audioID = manager.RoomZ[roomsToSwitch[i]].AudioID;
                    CrestronConsole.PrintLine("  Switching room={0} ({1}) audioID={2} -> newSrc={3}",
                        roomsToSwitch[i], manager.RoomZ[roomsToSwitch[i]].Name, audioID, newSrc);
                    if (audioID > 0)
                    {
                        try
                        {
                            musicSystemControl.SwitcherSelectMusicSource(audioID, newSrc);
                        }
                        catch (Exception ex)
                        {
                            // Log but continue — don't let one room's handler failure
                            // prevent other rooms from being switched
                            CrestronConsole.PrintLine("  SwitcherSelectMusicSource error for room {0} audioID={1}: {2}\n{3}",
                                roomsToSwitch[i], audioID, ex.Message, ex.StackTrace);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                CrestronConsole.PrintLine("ApplyChangeGroupSource error: {0}\n{1}", e.Message, e.StackTrace);
            }
            finally
            {
                CloseChangeGroupSourceMenu(TPNumber);
                // Single rebuild with all rooms in their final state.
                musicSystemControl.EndSuppressRebuild();
            }
        }

        #endregion

        #region Music Source Catalog (JSON push for HTML panels)

        // Serial join for the music source catalog JSON — pushed once at boot.
        // HTML subscribes and builds a name→icon/flipsToPageNumber map.
        private const ushort MusicSourceCatalogJoin = 1510; // s: C# -> HTML

        /// <summary>
        /// Builds a JSON array of all music sources (number, name, iconHTML,
        /// flipsToPageNumber) and pushes it to every HTML touchpanel on serial
        /// join 1510. Called once at boot — music source definitions do not
        /// change at runtime.
        /// </summary>
        public void PushMusicSourceCatalog()
        {
            var sb = new System.Text.StringBuilder();
            sb.Append('[');
            bool first = true;
            foreach (var kvp in manager.MusicSourceZ)
            {
                if (!first) sb.Append(',');
                first = false;
                var src = kvp.Value;
                sb.Append("{\"n\":");
                sb.Append(src.Number);
                sb.Append(",\"name\":\"");
                sb.Append(EscapeJsonString(src.Name));
                sb.Append("\",\"icon\":\"");
                sb.Append(EscapeJsonString(src.IconHTML));
                sb.Append("\",\"fp\":");
                sb.Append(src.FlipsToPageNumber);
                sb.Append('}');
            }
            sb.Append(']');
            string json = sb.ToString();

            foreach (var tp in manager.touchpanelZ)
            {
                if (tp.Value.HTML_UI)
                {
                    tp.Value.UserInterface.StringInput[MusicSourceCatalogJoin].StringValue = json;
                }
            }
            CrestronConsole.PrintLine("PushMusicSourceCatalog: pushed {0} sources to HTML panels", manager.MusicSourceZ.Count);
        }

        private static string EscapeJsonString(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }

        #endregion
    }
}
