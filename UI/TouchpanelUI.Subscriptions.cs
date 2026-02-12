//-----------------------------------------------------------------------
// <copyright file="TouchpanelUI.Subscriptions.cs" company="Crestron">
//     Copyright (c) Crestron Electronics. All rights reserved.
// </copyright>
//-----------------------------------------------------------------------
using System;
using System.Linq;
using ACS_4Series_Template_V3.Room;
using Crestron.SimplSharp;
using Crestron.SimplSharpPro.CrestronThread;

namespace ACS_4Series_Template_V3.UI
{
    /// <summary>
    /// Event subscription handling for TouchpanelUI
    /// </summary>
    public partial class TouchpanelUI
    {
        // Track the room number that subscriptions are attached to
        private ushort _subscribedRoomNumber = 0;

        #region Volume/Mute Subscriptions
        public void UnsubscribeTouchpanelFromAllVolMuteChanges()
        {
            foreach (var kvp in this.MuteChangeHandlers)
            {
                var room = kvp.Key;
                var handler = kvp.Value;
                room.MusicMutedChanged -= handler;
            }
            this.MuteChangeHandlers.Clear();

            foreach (var kvp in this.VolumeChangeHandlers)
            {
                var room = kvp.Key;
                var handler = kvp.Value;
                room.MusicVolumeChanged -= handler;
            }
            this.VolumeChangeHandlers.Clear();
        }
        #endregion

        #region Room Subsystem Subscriptions
        /// <summary>
        /// Subscribes to status events of the subsystems available for the current room
        /// </summary>
        public void SubscribeToRoomSubsystemEvents(ushort roomNumber, ushort previousRoom)
        {
            // Unsubscribe from all current subscriptions on the SUBSCRIBED room (not just previousRoom)
            if (_roomSubsystemSubscriptions != null && _roomSubsystemSubscriptions.Count > 0)
            {
                // Use _subscribedRoomNumber to ensure we unsubscribe from the correct room
                ushort roomToUnsubscribe = _subscribedRoomNumber > 0 ? _subscribedRoomNumber : previousRoom;
                
                if (roomToUnsubscribe > 0 && _parent.manager.RoomZ.ContainsKey(roomToUnsubscribe))
                {
                    RoomConfig oldRoom = _parent.manager.RoomZ[roomToUnsubscribe];
                    
                    foreach (var subscription in _roomSubsystemSubscriptions)
                    {
                        Action<ushort, string> handler = subscription.Value;
                        oldRoom.HVACStatusChanged -= handler;
                        oldRoom.LightStatusChanged -= handler;
                        oldRoom.MusicStatusTextChanged -= handler;
                        oldRoom.MusicStatusTextOffChanged -= handler;
                        oldRoom.VideoStatusTextChanged -= handler;
                        oldRoom.VideoStatusTextOffChanged -= handler;
                    }
                    
                    if (MusicSourceNameUpdateHandler != null)
                    {
                        oldRoom.MusicSrcStatusChanged -= MusicSourceNameUpdateHandler;
                        MusicSourceNameUpdateHandler = null;
                    }
                    if (_currentRoomVolumeHandler != null)
                    {
                        oldRoom.MusicVolumeChanged -= _currentRoomVolumeHandler;
                        _currentRoomVolumeHandler = null;
                    }
                }
                _roomSubsystemSubscriptions.Clear();
            }

            // Reset tracked room
            _subscribedRoomNumber = 0;

            if (_parent.manager == null)
            {
                CrestronConsole.PrintLine("_parent.manager is null.");
                return;
            }

            if (!_parent.manager.RoomZ.ContainsKey(roomNumber))
            {
                CrestronConsole.PrintLine($"Room {roomNumber} does not exist in RoomZ.");
                return;
            }

            RoomConfig room = _parent.manager.RoomZ[roomNumber];
            ushort subsystemScenario = room.SubSystemScenario;

            if (!_parent.manager.SubsystemScenarioZ.ContainsKey(subsystemScenario))
            {
                CrestronConsole.PrintLine($"Subsystem scenario {subsystemScenario} does not exist for room {roomNumber}. Subscription aborted.");
                return;
            }

            // Track which room we're subscribing to
            _subscribedRoomNumber = roomNumber;

            ushort numSubsystems = (ushort)_parent.manager.SubsystemScenarioZ[subsystemScenario].IncludedSubsystems.Count;

            for (ushort i = 0; i < numSubsystems; i++)
            {
                ushort capturedIndex = i;
                ushort capturedRoomNumber = roomNumber; // Capture room number for closure
                string subName = _parent.manager.SubsystemZ[_parent.manager.SubsystemScenarioZ[subsystemScenario].IncludedSubsystems[i]].Name;

                if (subName.Contains("Climate") || subName.Contains("HVAC"))
                {
                    SubscribeToClimateSubsystem(room, capturedIndex, capturedRoomNumber);
                }
                else if (subName.ToUpper().Contains("LIGHT"))
                {
                    SubscribeToLightSubsystem(room, capturedIndex, capturedRoomNumber);
                }
                else if (subName.ToUpper().Contains("SHADE") || subName.ToUpper().Contains("DRAPE"))
                {
                    SetSubsystemStatus(capturedIndex, "");
                }
                else if (subName.ToUpper().Contains("AUDIO") || subName.ToUpper().Contains("MUSIC"))
                {
                    SubscribeToAudioSubsystem(room, capturedIndex, capturedRoomNumber);
                }
                else if (subName.ToUpper().Contains("VIDEO") || subName.ToUpper().Contains("WATCH"))
                {
                    SubscribeToVideoSubsystem(room, capturedIndex, capturedRoomNumber);
                }
                else
                {
                    SetSubsystemStatus(capturedIndex, "");
                }
            }
        }

        private void SetSubsystemStatus(ushort index, string status)
        {
            if (this.HTML_UI)
            {
                this._HTMLContract.SubsystemButton[index].SubsystemStatus((sig, wh) => sig.StringValue = status);
            }
            else
            {
                this.UserInterface.SmartObjects[2].StringInput[(ushort)(3 * index + 12)].StringValue = status;
            }
        }

        private void SubscribeToClimateSubsystem(RoomConfig room, ushort index, ushort expectedRoomNumber)
        {
            SetSubsystemStatus(index, this.HTML_UI ? stripHTMLTags(room.HVACStatusText) : room.HVACStatusText);

            Action<ushort, string> subscription = (rNumber, status) =>
            {
                // Only update if this is still for our current room
                if (rNumber == expectedRoomNumber && this.CurrentRoomNum == expectedRoomNumber)
                {
                    if (this.HTML_UI)
                    {
                        this._HTMLContract.SubsystemButton[index].SubsystemStatus((sig, wh) => sig.StringValue = stripHTMLTags(status));
                    }
                    else
                    {
                        this.UserInterface.SmartObjects[2].StringInput[(ushort)(3 * index + 12)].StringValue = status;
                    }
                }
            };
            room.HVACStatusChanged += subscription;
            _roomSubsystemSubscriptions[index] = subscription;
        }

        private void SubscribeToLightSubsystem(RoomConfig room, ushort index, ushort expectedRoomNumber)
        {
            SetSubsystemStatus(index, room.LightStatusText);

            Action<ushort, string> subscription = (rNumber, status) =>
            {
                // Only update if this is still for our current room
                if (rNumber == expectedRoomNumber && this.CurrentRoomNum == expectedRoomNumber)
                {
                    if (this.HTML_UI)
                    {
                        this._HTMLContract.SubsystemButton[index].SubsystemStatus((sig, wh) => sig.StringValue = status);
                    }
                    else
                    {
                        this.UserInterface.SmartObjects[2].StringInput[(ushort)(3 * index + 12)].StringValue = status;
                    }
                }
            };
            room.LightStatusChanged += subscription;
            _roomSubsystemSubscriptions[index] = subscription;
        }

        private void SubscribeToAudioSubsystem(RoomConfig room, ushort index, ushort expectedRoomNumber)
        {
            SetSubsystemStatus(index, room.MusicStatusTextOff);

            Action<ushort, string> subscription = (rNumber, status) =>
            {
                // Only update if this is still for our current room
                if (rNumber == expectedRoomNumber && this.CurrentRoomNum == expectedRoomNumber)
                {
                    if (this.HTML_UI)
                    {
                        this._HTMLContract.SubsystemButton[index].SubsystemStatus((sig, wh) => sig.StringValue = status);
                    }
                    else
                    {
                        this.UserInterface.SmartObjects[2].StringInput[(ushort)(3 * index + 12)].StringValue = status;
                    }
                }
            };

            Action<ushort, ushort, ushort, string, ushort> musicSourceUpdateHandler =
                (musicSrc, flipsToPage, equipID, name, buttonNum) =>
                {
                    // Only update if this is still for our current room
                    if (this.CurrentRoomNum == expectedRoomNumber)
                    {
                        this.UserInterface.StringInput[3].StringValue = name;
                        this.musicButtonFB(buttonNum);
                        CrestronConsole.PrintLine("Music source name updated to: {0} for room {1}", name, expectedRoomNumber);
                    }
                };

            _currentRoomVolumeHandler = (sender, e) =>
            {
                // Only update if this is still for our current room
                if (this.CurrentRoomNum == expectedRoomNumber)
                {
                    this.UserInterface.UShortInput[2].UShortValue = room.MusicVolume;
                }
            };

            this.UserInterface.UShortInput[2].UShortValue = room.MusicVolume;

            if (room.CurrentMusicSrc > 0 && _parent.manager.MusicSourceZ.ContainsKey(room.CurrentMusicSrc))
            {
                ushort asrcScenarioNum = room.AudioSrcScenario;
                ushort buttonNum = 0;

                if (_parent.manager.AudioSrcScenarioZ.ContainsKey(asrcScenarioNum))
                {
                    ushort numSrcs = (ushort)_parent.manager.AudioSrcScenarioZ[asrcScenarioNum].IncludedSources.Count;
                    for (ushort j = 0; j < numSrcs; j++)
                    {
                        ushort srcNum = _parent.manager.AudioSrcScenarioZ[asrcScenarioNum].IncludedSources[j];
                        if (srcNum == room.CurrentMusicSrc)
                        {
                            buttonNum = (ushort)(j + 1);
                            break;
                        }
                    }
                }

                this.UserInterface.StringInput[3].StringValue = _parent.manager.MusicSourceZ[room.CurrentMusicSrc].Name;
                this.musicButtonFB(buttonNum);
            }
            else
            {
                this.UserInterface.StringInput[3].StringValue = "Off";
                this.musicButtonFB(0);
            }

            room.MusicStatusTextOffChanged += subscription;
            MusicSourceNameUpdateHandler = musicSourceUpdateHandler;
            room.MusicSrcStatusChanged += musicSourceUpdateHandler;
            _roomSubsystemSubscriptions[index] = subscription;
            room.MusicVolumeChanged += _currentRoomVolumeHandler;
        }

        private void SubscribeToVideoSubsystem(RoomConfig room, ushort index, ushort expectedRoomNumber)
        {
            SetSubsystemStatus(index, room.VideoStatusTextOff);

            Action<ushort, string> subscription = (rNumber, status) =>
            {
                // Only update if this is still for our current room
                if (rNumber == expectedRoomNumber && this.CurrentRoomNum == expectedRoomNumber)
                {
                    if (this.HTML_UI)
                    {
                        this._HTMLContract.SubsystemButton[index].SubsystemStatus((sig, wh) => sig.StringValue = status);
                    }
                    else
                    {
                        this.UserInterface.SmartObjects[2].StringInput[(ushort)(3 * index + 12)].StringValue = status;
                    }
                }
            };
            room.VideoStatusTextOffChanged += subscription;
            _roomSubsystemSubscriptions[index] = subscription;
        }
        #endregion

        #region Room List Status Subscriptions
        /// <summary>
        /// Subscribe to events for list of rooms status
        /// </summary>
        public void SubscribeToListOfRoomsStatusEvents(ushort newFloorNumber)
        {
            if (_parent.manager == null)
            {
                CrestronConsole.PrintLine("_parent.manager is null.");
                return;
            }
            if (newFloorNumber == 0)
            {
                CrestronConsole.PrintLine("newFloorNumber is 0. Subscription aborted.");
                return;
            }

            ClearCurrentRoomSubscriptions();

            // Clear all room button status text first
            if (this.HTML_UI)
            {
                for (int i = 0; i < _HTMLContract.roomButton.Length; i++)
                {
                    int capturedIndex = i;
                    this._HTMLContract.roomButton[capturedIndex].zoneStatus1((sig, wh) => sig.StringValue = "");
                    this._HTMLContract.roomButton[capturedIndex].zoneStatus2((sig, wh) => sig.StringValue = "");
                }
            }
            else
            {
                for (ushort i = 0; i < 30; i++)
                {
                    this.UserInterface.SmartObjects[4].StringInput[(ushort)(4 * i + 12)].StringValue = "";
                    this.UserInterface.SmartObjects[4].StringInput[(ushort)(4 * i + 13)].StringValue = "";
                }
            }

            ushort subscriptionCounter = 0;
            for (ushort j = 0; j < _parent.manager.Floorz[newFloorNumber].IncludedRooms.Count; j++)
            {
                ushort capturedRoomIndex = j;

                if (this.HTML_UI && capturedRoomIndex >= _HTMLContract.roomButton.Length)
                {
                    CrestronConsole.PrintLine("Room index {0} exceeds roomButton array length {1}", capturedRoomIndex, _HTMLContract.roomButton.Length);
                    continue;
                }

                ushort roomNumber = _parent.manager.Floorz[newFloorNumber].IncludedRooms[j];
                RoomConfig room = _parent.manager.RoomZ[roomNumber];
                ushort subsystemScenario = room.SubSystemScenario;

                if (!_parent.manager.SubsystemScenarioZ.ContainsKey(subsystemScenario))
                {
                    CrestronConsole.PrintLine($"Subsystem scenario {subsystemScenario} does not exist for room {room.Name}. Skipping room.");
                    continue;
                }

                ushort numSubsystems = (ushort)_parent.manager.SubsystemScenarioZ[subsystemScenario].IncludedSubsystems.Count;
                bool hasClimate = false;
                bool hasLightsMusicVideo = false;

                for (ushort i = 0; i < numSubsystems; i++)
                {
                    string subName = _parent.manager.SubsystemZ[_parent.manager.SubsystemScenarioZ[subsystemScenario].IncludedSubsystems[i]].Name;
                    if (subName.ToUpper().Contains("CLIMATE") || subName.ToUpper().Contains("HVAC"))
                    {
                        hasClimate = true;
                    }
                    if (subName.ToUpper().Contains("LIGHT") || subName.ToUpper().Contains("MUSIC") ||
                        subName.ToUpper().Contains("AUDIO") || subName.ToUpper().Contains("VIDEO"))
                    {
                        hasLightsMusicVideo = true;
                    }
                }

                if (hasClimate)
                {
                    SubscribeRoomListToClimate(room, capturedRoomIndex, ref subscriptionCounter);
                }

                if (hasLightsMusicVideo)
                {
                    SubscribeRoomListToStatus(room, capturedRoomIndex, ref subscriptionCounter);
                }
            }
        }

        private void SubscribeRoomListToClimate(RoomConfig room, ushort roomIndex, ref ushort counter)
        {
            ushort capturedRoomNumber = room.Number;
            ushort capturedIndex = roomIndex;
            
            if (this.HTML_UI)
            {
                this._HTMLContract.roomButton[roomIndex].zoneStatus1((sig, wh) => sig.StringValue = stripHTMLTags(room.HVACStatusText));
            }
            else
            {
                this.UserInterface.SmartObjects[4].StringInput[(ushort)(4 * roomIndex + 12)].StringValue = room.HVACStatusText;
            }

            Action<ushort, string> statusSubscription = (rNumber, status) =>
            {
                // Verify this is for the expected room
                if (rNumber == capturedRoomNumber)
                {
                    if (this.HTML_UI)
                    {
                        this._HTMLContract.roomButton[capturedIndex].zoneStatus1((sig, wh) => sig.StringValue = stripHTMLTags(status));
                    }
                    else
                    {
                        this.UserInterface.SmartObjects[4].StringInput[(ushort)(4 * capturedIndex + 12)].StringValue = status;
                    }
                }
            };
            room.HVACStatusChanged += statusSubscription;
            _roomListStatusSubscriptions[counter++] = statusSubscription;
        }

        private void SubscribeRoomListToStatus(RoomConfig room, ushort roomIndex, ref ushort counter)
        {
            ushort capturedRoomNumber = room.Number;
            ushort capturedIndex = roomIndex;
            
            if (this.HTML_UI)
            {
                this._HTMLContract.roomButton[roomIndex].zoneStatus2((sig, wh) => sig.StringValue = room.RoomStatusText);
            }
            else
            {
                this.UserInterface.SmartObjects[4].StringInput[(ushort)(4 * roomIndex + 13)].StringValue = room.RoomStatusText;
            }

            Action<ushort, string> statusSubscription = (rNumber, status) =>
            {
                // Verify this is for the expected room
                if (rNumber == capturedRoomNumber)
                {
                    if (this.HTML_UI)
                    {
                        this._HTMLContract.roomButton[capturedIndex].zoneStatus2((sig, wh) => sig.StringValue = status);
                    }
                    else
                    {
                        this.UserInterface.SmartObjects[4].StringInput[(ushort)(4 * capturedIndex + 13)].StringValue = status;
                    }
                }
            };
            room.RoomStatusTextChanged += statusSubscription;
            _roomListStatusSubscriptions[counter++] = statusSubscription;
        }

        public void ClearCurrentRoomSubscriptions()
        {
            try
            {
                if (_roomListStatusSubscriptions != null && _roomListStatusSubscriptions.Count > 0)
                {
                    var subscriptions = _roomListStatusSubscriptions.ToList();
                    foreach (var subscription in subscriptions)
                    {
                        Action<ushort, string> handler = subscription.Value;

                        foreach (var room in _parent.manager.RoomZ.Values)
                        {
                            try
                            {
                                room.HVACStatusChanged -= handler;
                                room.LightStatusChanged -= handler;
                                room.RoomStatusTextChanged -= handler;
                            }
                            catch (Exception ex)
                            {
                                ErrorLog.Error("Error unsubscribing handler: {0}", ex.Message);
                                CrestronConsole.PrintLine("Error unsubscribing handler: {0}", ex.Message);
                            }
                        }
                    }
                    _roomListStatusSubscriptions.Clear();
                }

                for (ushort i = 0; i < 30; i++)
                {
                    if (this.HTML_UI)
                    {
                        this._HTMLContract.WholeHouseZone[i].HouseZoneStatus((sig, wh) => sig.StringValue = "");
                    }
                    else
                    {
                        this.UserInterface.SmartObjects[10].StringInput[(ushort)(3 * i + 12)].StringValue = "";
                    }
                }
            }
            catch (Exception ex)
            {
                CrestronConsole.PrintLine("Error in ClearCurrentRoomSubscriptions: {0}", ex.Message);
            }
        }
        #endregion

        #region Music Menu Subscriptions
        public void SubscribeToMusicMenuEvents(ushort roomNumber)
        {
            if (currentSubscribedRoom != null)
            {
                currentSubscribedRoom.MusicSrcStatusChanged -= MusicSrcStatusChangedHandler;
            }

            if (_parent.manager.RoomZ[roomNumber].AudioID > 0)
            {
                RoomConfig room = _parent.manager.RoomZ[roomNumber];
                ushort currentMusicSrc = room.CurrentMusicSrc;

                if (currentMusicSrc > 0)
                {
                    this.musicPageFlips(_parent.manager.MusicSourceZ[currentMusicSrc].FlipsToPageNumber);
                    _parent.musicEISC1.UShortInput[(ushort)(Number + 300)].UShortValue = _parent.manager.MusicSourceZ[currentMusicSrc].EquipID;
                    this.UserInterface.StringInput[3].StringValue = _parent.manager.MusicSourceZ[currentMusicSrc].Name;
                    ushort asrcScenarioNum = _parent.manager.RoomZ[roomNumber].AudioSrcScenario;
                    ushort numSrcs = (ushort)_parent.manager.AudioSrcScenarioZ[asrcScenarioNum].IncludedSources.Count;
                    for (ushort i = 0; i < numSrcs; i++)
                    {
                        ushort srcNum = _parent.manager.AudioSrcScenarioZ[asrcScenarioNum].IncludedSources[i];
                        if (srcNum == _parent.manager.RoomZ[roomNumber].CurrentMusicSrc)
                        {
                            this.musicButtonFB((ushort)(i + 1));
                        }
                    }
                }
                else
                {
                    this.musicPageFlips(0);
                    _parent.musicEISC1.UShortInput[(ushort)(Number + 300)].UShortValue = 0;
                    this.UserInterface.StringInput[3].StringValue = "Off";
                    this.musicButtonFB(0);
                }
                _parent.musicEISC1.UShortInput[(ushort)(Number + 100)].UShortValue = currentMusicSrc;

                room.MusicSrcStatusChanged += MusicSrcStatusChangedHandler;
                currentSubscribedRoom = room;
            }
        }

        private void MusicSrcStatusChangedHandler(ushort musicSrc, ushort flipsToPage, ushort equipID, string name, ushort buttonNum)
        {
            _parent.musicEISC1.UShortInput[(ushort)(Number + 100)].UShortValue = musicSrc;
            this.musicPageFlips(flipsToPage);
            _parent.musicEISC1.UShortInput[(ushort)(Number + 300)].UShortValue = equipID;
            this.UserInterface.StringInput[3].StringValue = name;
            this.musicButtonFB(buttonNum);
        }
        #endregion

        #region Video Menu Subscriptions
        public void SubscribeToVideoMenuEvents(ushort roomNumber)
        {
            if (currentSubscribedRoom != null)
            {
                currentSubscribedRoom.VideoSrcStatusChanged -= VideoSrcStatusChangedHandler;
                currentSubscribedRoom.DisplayChanged -= UpdateTouchpanelDisplayName;
            }

            ushort displayAssignedToRoomNum = _parent.manager.VideoDisplayZ.Values.FirstOrDefault(display => display.AssignedToRoomNum == roomNumber)?.Number ?? 0;
            if (_parent.logging) CrestronConsole.PrintLine("SubscribeToVideoMenuEvents called for room {0} tp-{1} displayAssignedToRoomNum: {2}", roomNumber, Number, displayAssignedToRoomNum);

            if (displayAssignedToRoomNum > 0)
            {
                RoomConfig room = _parent.manager.RoomZ[roomNumber];
                ushort currentVidSrc = room.CurrentVideoSrc;

                if (currentVidSrc > 0)
                {
                    this.videoPageFlips(_parent.manager.VideoSourceZ[currentVidSrc].FlipsToPageNumber);
                    _parent.videoEISC1.UShortInput[(ushort)(Number + 300)].UShortValue = _parent.manager.VideoSourceZ[currentVidSrc].EquipID;
                    this.UserInterface.StringInput[2].StringValue = _parent.manager.VideoSourceZ[currentVidSrc].DisplayName;
                    ushort vsrcScenarioNum = _parent.manager.RoomZ[roomNumber].VideoSrcScenario;
                    ushort numSrcs = (ushort)_parent.manager.VideoSrcScenarioZ[vsrcScenarioNum].IncludedSources.Count;

                    for (ushort i = 0; i < numSrcs; i++)
                    {
                        ushort srcNum = _parent.manager.VideoSrcScenarioZ[vsrcScenarioNum].IncludedSources[i];
                        if (srcNum == _parent.manager.RoomZ[roomNumber].CurrentVideoSrc)
                        {
                            this.videoButtonFB((ushort)(i + 1));
                        }
                    }
                }
                else
                {
                    this.videoPageFlips(0);
                    _parent.videoEISC1.UShortInput[(ushort)(Number + 300)].UShortValue = 0;
                    this.UserInterface.StringInput[2].StringValue = "Off";
                    this.videoButtonFB(0);
                }

                room.VideoSrcStatusChanged += VideoSrcStatusChangedHandler;
                room.DisplayChanged += UpdateTouchpanelDisplayName;
                if (room.CurrentDisplayNumber > 0 && _parent.manager.VideoDisplayZ.ContainsKey(room.CurrentDisplayNumber))
                {
                    UpdateTouchpanelDisplayName(room.Number, _parent.manager.VideoDisplayZ[room.CurrentDisplayNumber].DisplayName);
                }
                currentSubscribedRoom = room;
                if (_parent.logging) CrestronConsole.PrintLine("FINISHED SubscribeToVideoMenuEvents called for room {0} tp-{1} currentVidSrc: {2}", room.Name, Number, currentVidSrc);
            }
        }

        private void VideoSrcStatusChangedHandler(ushort flipsToPage, ushort equipID, string name, ushort buttonNum)
        {
            CrestronConsole.PrintLine("VideoSrcStatusChangedHandler called for tp-{0} flipsToPage: {1}, equipID: {2}, name: {3}, buttonNum: {4}", Number, flipsToPage, equipID, name, buttonNum);
            this.videoPageFlips(flipsToPage);
            _parent.videoEISC1.UShortInput[(ushort)(Number + 300)].UShortValue = equipID;
            this.UserInterface.StringInput[2].StringValue = name;
            this.videoButtonFB(buttonNum);
        }

        private void UpdateTouchpanelDisplayName(ushort roomNumber, string displayName)
        {
            this.UserInterface.StringInput[10].StringValue = displayName;
            ushort displayNumber = _parent.manager.RoomZ[roomNumber].CurrentDisplayNumber;
            ushort videoOutputNumber = _parent.manager.VideoDisplayZ[displayNumber].VideoOutputNum;
            _parent.subsystemEISC.UShortInput[(ushort)((Number - 1) * 10 + 302)].UShortValue = videoOutputNumber;
        }
        #endregion

        #region Climate Subscriptions
        public void SubscribeToClimateEvents(ushort roomNumber)
        {
            if (CurrentClimateSubscription != null)
            {
                foreach (var rm in _parent.manager.RoomZ.Values)
                {
                    rm.HVACStatusChanged -= CurrentClimateSubscription;
                    if (_currentSetpointHandler != null)
                    {
                        rm.CurrentSetpointChanged -= _currentSetpointHandler;
                    }
                }
                CurrentClimateSubscription = null;
                _currentSetpointHandler = null;
            }

            if (!_parent.manager.RoomZ.TryGetValue(roomNumber, out RoomConfig room) || room.ClimateID <= 0)
            {
                return;
            }

            this.CurrentSubsystemIsClimate = true;

            CurrentClimateSubscription = (rNumber, status) =>
            {
                if (rNumber == this.CurrentRoomNum && this.CurrentSubsystemIsClimate)
                {
                    UpdateClimateUI(room);
                }
            };

            _currentSetpointHandler = (newSetpoint) =>
            {
                if (this.CurrentSubsystemIsClimate)
                {
                    this.UserInterface.UShortInput[101].UShortValue = newSetpoint;
                }
            };

            room.HVACStatusChanged += CurrentClimateSubscription;
            room.CurrentSetpointChanged += _currentSetpointHandler;

            new CTimer(_ =>
            {
                if (this.CurrentSubsystemIsClimate && this.CurrentRoomNum == roomNumber)
                {
                    UpdateClimateUI(room);
                }
            }, 200);
        }

        private void UpdateClimateUI(RoomConfig room)
        {
            this.UserInterface.UShortInput[101].UShortValue = room.CurrentSetpoint;
            this.UserInterface.UShortInput[102].UShortValue = room.CurrentTemperature;
            this.UserInterface.UShortInput[103].UShortValue = room.CurrentHeatSetpoint;
            this.UserInterface.UShortInput[104].UShortValue = room.CurrentCoolSetpoint;
        }
        #endregion

        #region Whole House List Subscriptions
        public void SubscribeToWholeHouseListEvents()
        {
            if (_parent.manager == null)
            {
                CrestronConsole.PrintLine("_parent.manager is null.");
                return;
            }
            ClearCurrentRoomSubscriptions();

            string subName = _parent.manager.SubsystemZ[CurrentSubsystemNumber].Name;
            if (subName.ToUpper().Contains("HVAC") || subName.ToUpper().Contains("CLIMATE"))
            {
                SubscribeWholeHouseToClimate();
            }
            else if (subName.ToUpper().Contains("LIGHT"))
            {
                SubscribeWholeHouseToLights();
            }
            else if (subName.ToUpper().Contains("SHADE") || subName.ToUpper().Contains("DRAPE"))
            {
                SubscribeWholeHouseToShades();
            }

            if (this.HTML_UI)
            {
                this._HTMLContract.WholeHouseZoneList.numberOfWholeHouseZones((sig, wh) => sig.UShortValue = (ushort)WholeHouseRoomList.Count);
            }
            else
            {
                this.UserInterface.SmartObjects[10].UShortInput[3].UShortValue = (ushort)WholeHouseRoomList.Count;
            }
        }

        private void SubscribeWholeHouseToClimate()
        {
            for (ushort j = 0; j < WholeHouseRoomList.Count; j++)
            {
                RoomConfig room = _parent.manager.RoomZ[WholeHouseRoomList[j]];
                ushort capturedJ = j;
                ushort capturedRoomNumber = room.Number;

                if (this.HTML_UI)
                {
                    this._HTMLContract.WholeHouseZone[j].HouseZoneName((sig, wh) => sig.StringValue = room.Name);
                    this._HTMLContract.WholeHouseZone[j].HouseZoneStatus((sig, wh) => sig.StringValue = stripHTMLTags(room.HVACStatusText));
                    this._HTMLContract.WholeHouseZone[j].HouseZoneIcon((sig, wh) => sig.StringValue = _parent.manager.SubsystemZ[CurrentSubsystemNumber].IconHTML);
                }
                else
                {
                    this.UserInterface.SmartObjects[10].StringInput[(ushort)(3 * j + 11)].StringValue = room.Name;
                    this.UserInterface.SmartObjects[10].StringInput[(ushort)(3 * j + 12)].StringValue = room.HVACStatusText;
                    this.UserInterface.SmartObjects[10].StringInput[(ushort)(3 * j + 13)].StringValue = _parent.manager.SubsystemZ[CurrentSubsystemNumber].IconSerial;
                }

                Action<ushort, string> statusSubscription = (rNumber, status) =>
                {
                    if (rNumber == capturedRoomNumber)
                    {
                        if (this.HTML_UI)
                        {
                            this._HTMLContract.WholeHouseZone[capturedJ].HouseZoneStatus((sig, wh) => sig.StringValue = status);
                        }
                        else
                        {
                            this.UserInterface.SmartObjects[10].StringInput[(ushort)(3 * capturedJ + 12)].StringValue = status;
                        }
                    }
                };
                room.HVACStatusChanged += statusSubscription;
                _roomListStatusSubscriptions[room.Number] = statusSubscription;
            }
        }

        private void SubscribeWholeHouseToLights()
        {
            for (ushort j = 0; j < WholeHouseRoomList.Count; j++)
            {
                RoomConfig room = _parent.manager.RoomZ[WholeHouseRoomList[j]];
                ushort capturedIndex = j;
                ushort roomNumber = room.Number;

                if (this.HTML_UI)
                {
                    this._HTMLContract.WholeHouseZone[j].HouseZoneName((sig, wh) => sig.StringValue = room.Name);
                    this._HTMLContract.WholeHouseZone[j].HouseZoneStatus((sig, wh) => sig.StringValue = room.LightStatusText);
                    this._HTMLContract.WholeHouseZone[j].HouseZoneIcon((sig, wh) => sig.StringValue = _parent.manager.SubsystemZ[CurrentSubsystemNumber].IconHTML);
                }
                else
                {
                    this.UserInterface.SmartObjects[10].StringInput[(ushort)(3 * j + 11)].StringValue = room.Name;
                    this.UserInterface.SmartObjects[10].StringInput[(ushort)(3 * capturedIndex + 12)].StringValue = room.LightStatusText;
                    this.UserInterface.SmartObjects[10].StringInput[(ushort)(3 * j + 13)].StringValue = _parent.manager.SubsystemZ[CurrentSubsystemNumber].IconSerial;
                }

                Action<ushort, string> statusSubscription = (rNumber, status) =>
                {
                    if (rNumber == roomNumber)
                    {
                        if (this.HTML_UI)
                        {
                            this._HTMLContract.WholeHouseZone[capturedIndex].HouseZoneStatus((sig, wh) => sig.StringValue = status);
                        }
                        else
                        {
                            this.UserInterface.SmartObjects[10].StringInput[(ushort)(3 * capturedIndex + 12)].StringValue = status;
                        }
                        CrestronConsole.PrintLine("Light status updated for room {0}: {1}", rNumber, status);
                    }
                };
                room.LightStatusChanged += statusSubscription;
                _roomListStatusSubscriptions[room.Number] = statusSubscription;
            }
        }

        private void SubscribeWholeHouseToShades()
        {
            for (ushort j = 0; j < WholeHouseRoomList.Count; j++)
            {
                RoomConfig room = _parent.manager.RoomZ[WholeHouseRoomList[j]];
                ushort capturedIndex = j;
                ushort roomNumber = room.Number;

                if (this.HTML_UI)
                {
                    this._HTMLContract.WholeHouseZone[j].HouseZoneName((sig, wh) => sig.StringValue = room.Name);
                    this._HTMLContract.WholeHouseZone[j].HouseZoneStatus((sig, wh) => sig.StringValue = room.ShadeStatusText);
                    this._HTMLContract.WholeHouseZone[j].HouseZoneIcon((sig, wh) => sig.StringValue = _parent.manager.SubsystemZ[CurrentSubsystemNumber].IconHTML);
                }
                else
                {
                    this.UserInterface.SmartObjects[10].StringInput[(ushort)(3 * j + 11)].StringValue = room.Name;
                    this.UserInterface.SmartObjects[10].StringInput[(ushort)(3 * capturedIndex + 12)].StringValue = room.ShadeStatusText;
                    this.UserInterface.SmartObjects[10].StringInput[(ushort)(3 * j + 13)].StringValue = _parent.manager.SubsystemZ[CurrentSubsystemNumber].IconSerial;
                }

                Action<ushort, string> statusSubscription = (rNumber, status) =>
                {
                    if (rNumber == roomNumber)
                    {
                        if (this.HTML_UI)
                        {
                            this._HTMLContract.WholeHouseZone[capturedIndex].HouseZoneStatus((sig, wh) => sig.StringValue = status);
                        }
                        else
                        {
                            this.UserInterface.SmartObjects[10].StringInput[(ushort)(3 * capturedIndex + 12)].StringValue = status;
                        }
                        CrestronConsole.PrintLine("Shade status updated for room {0}: {1}", rNumber, status);
                    }
                };
                room.ShadeStatusChanged += statusSubscription;
                _roomListStatusSubscriptions[room.Number] = statusSubscription;
            }
        }
        #endregion
    }
}
