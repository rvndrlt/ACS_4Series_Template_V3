using System;
using Crestron.SimplSharp;
using ACS_4Series_Template_V3.UI;

namespace ACS_4Series_Template_V3
{
    public partial class ControlSystem
    {
        #region Home Page Music Zones

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

            tp._HTMLContract.HomeNumberOfMusicZones.NumberOfMusicZones(
                (sig, wh) => sig.UShortValue = numberOfZones);

            for (int i = 0; i < numberOfZones && i < tp._HTMLContract.HomeMusicZone.Length; i++)
            {
                int capturedIndex = i;
                ushort roomNumber = HomePageMusicRooms[i];
                var room = manager.RoomZ[roomNumber];

                // Set INITIAL visibility based on whether music is currently playing
                bool isPlaying = room.CurrentMusicSrc > 0;
                tp._HTMLContract.HomeMusicZone[capturedIndex].isVisible(
                    (sig, wh) => sig.BoolValue = isPlaying);

                tp._HTMLContract.HomeMusicZone[capturedIndex].ZoneName(
                    (sig, wh) => sig.StringValue = room.Name);

                string sourceName = "Off";
                if (room.CurrentMusicSrc > 0 && manager.MusicSourceZ.ContainsKey(room.CurrentMusicSrc))
                {
                    sourceName = manager.MusicSourceZ[room.CurrentMusicSrc].Name;
                }
                tp._HTMLContract.HomeMusicZone[capturedIndex].CurrentSource(
                    (sig, wh) => sig.StringValue = sourceName);

                tp._HTMLContract.HomeMusicZone[capturedIndex].Volume(
                    (sig, wh) => sig.UShortValue = room.MusicVolume);

                tp._HTMLContract.HomeMusicZone[capturedIndex].isMuted(
                    (sig, wh) => sig.BoolValue = room.MusicMuted);

                // Subscribe to music source changes to update visibility dynamically
                ushort capturedRoomNumber = roomNumber;
                ushort capturedTPNumber = TPNumber;

                room.MusicSrcStatusChanged += (musicSrc, flipsToPage, equipID, name, buttonNum) =>
                {
                    UpdateHomeMusicZoneForRoom(capturedTPNumber, capturedIndex, capturedRoomNumber);
                };

                // Subscribe to volume changes
                room.MusicVolumeChanged += (sender, e) =>
                {
                    if (manager.touchpanelZ.ContainsKey(capturedTPNumber) &&
                        manager.touchpanelZ[capturedTPNumber].HTML_UI &&
                        capturedIndex < manager.touchpanelZ[capturedTPNumber]._HTMLContract.HomeMusicZone.Length)
                    {
                        manager.touchpanelZ[capturedTPNumber]._HTMLContract.HomeMusicZone[capturedIndex].Volume(
                            (sig, wh) => sig.UShortValue = room.MusicVolume);
                    }
                };

                // Subscribe to mute changes
                room.MusicMutedChanged += (sender, e) =>
                {
                    if (manager.touchpanelZ.ContainsKey(capturedTPNumber) &&
                        manager.touchpanelZ[capturedTPNumber].HTML_UI &&
                        capturedIndex < manager.touchpanelZ[capturedTPNumber]._HTMLContract.HomeMusicZone.Length)
                    {
                        manager.touchpanelZ[capturedTPNumber]._HTMLContract.HomeMusicZone[capturedIndex].isMuted(
                            (sig, wh) => sig.BoolValue = room.MusicMuted);
                    }
                };
            }
        }

        /// <summary>
        /// Updates a specific Home Music Zone when its music source changes
        /// </summary>
        private void UpdateHomeMusicZoneForRoom(ushort TPNumber, int zoneIndex, ushort roomNumber)
        {
            if (!manager.touchpanelZ.ContainsKey(TPNumber) || !manager.touchpanelZ[TPNumber].HTML_UI)
                return;

            var tp = manager.touchpanelZ[TPNumber];
            if (zoneIndex >= tp._HTMLContract.HomeMusicZone.Length)
                return;

            if (!manager.RoomZ.ContainsKey(roomNumber))
                return;

            var room = manager.RoomZ[roomNumber];
            bool isPlaying = room.CurrentMusicSrc > 0;

            // Update visibility - show only if music is playing
            tp._HTMLContract.HomeMusicZone[zoneIndex].isVisible(
                (sig, wh) => sig.BoolValue = isPlaying);

            // Update current source name
            string sourceName = "Off";
            if (isPlaying && manager.MusicSourceZ.ContainsKey(room.CurrentMusicSrc))
            {
                sourceName = manager.MusicSourceZ[room.CurrentMusicSrc].Name;
            }
            tp._HTMLContract.HomeMusicZone[zoneIndex].CurrentSource(
                (sig, wh) => sig.StringValue = sourceName);

            // Update volume and mute state
            tp._HTMLContract.HomeMusicZone[zoneIndex].Volume(
                (sig, wh) => sig.UShortValue = room.MusicVolume);
            tp._HTMLContract.HomeMusicZone[zoneIndex].isMuted(
                (sig, wh) => sig.BoolValue = room.MusicMuted);
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
