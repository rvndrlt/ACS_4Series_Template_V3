//-----------------------------------------------------------------------
// <copyright file="TouchpanelUI.MusicSharing.cs" company="Crestron">
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
    /// Music sharing functionality for TouchpanelUI
    /// </summary>
    public partial class TouchpanelUI
    {
        private void HideSharingMenu(object userObject)
        {
            this.SrcSharingButtonFB = false;
            this.UserInterface.BooleanInput[1002].BoolValue = false;
            this.UserInterface.BooleanInput[998].BoolValue = false;
            this.UserInterface.BooleanInput[999].BoolValue = false;

            _sharingMenuTimer?.Dispose();
            _sharingMenuTimer = null;

            UnsubscribeFromMusicSharingChanges();
        }

        private void UnsubscribeFromMusicSharingChanges()
        {
            foreach (var kvp in _musicSharingChangeHandlers)
            {
                ushort roomNumber = kvp.Key;
                Action<ushort, ushort, ushort, string, ushort> handler = kvp.Value;

                if (_parent.manager.RoomZ.ContainsKey(roomNumber))
                {
                    RoomConfig room = _parent.manager.RoomZ[roomNumber];
                    room.MusicSrcStatusChanged -= handler;
                }
            }
            _musicSharingChangeHandlers.Clear();

            foreach (var kvp in VolumeChangeHandlers.ToList())
            {
                RoomConfig room = kvp.Key;
                EventHandler handler = kvp.Value;
                room.MusicVolumeChanged -= handler;
            }

            foreach (var kvp in MuteChangeHandlers.ToList())
            {
                RoomConfig room = kvp.Key;
                EventHandler handler = kvp.Value;
                room.MusicMutedChanged -= handler;
            }

            MuteChangeHandlers.Clear();
            VolumeChangeHandlers.Clear();
        }

        public void SubscribeToMusicSharingChanges()
        {
            if (this.MusicRoomsToShareSourceTo == null || this.MusicRoomsToShareSourceTo.Count == 0)
            {
                return;
            }

            UnsubscribeFromMusicSharingChanges();

            for (int i = 0; i < this.MusicRoomsToShareSourceTo.Count; i++)
            {
                ushort roomNumber = this.MusicRoomsToShareSourceTo[i];
                int roomIndex = i;

                if (_parent.manager.RoomZ.ContainsKey(roomNumber))
                {
                    RoomConfig room = _parent.manager.RoomZ[roomNumber];

                    Action<ushort, ushort, ushort, string, ushort> handler =
                        (musicSrc, flipsToPage, equipID, name, buttonNum) =>
                        {
                            if (musicSrc > 0)
                            {
                                if (this.HTML_UI)
                                {
                                    this._HTMLContract.MusicRoomControl[roomIndex].musicZoneSource((sig, wh) => sig.StringValue = name);
                                    this._HTMLContract.MusicRoomControl[roomIndex].musicVolEnable((sig, wh) => sig.BoolValue = true);
                                }
                                else
                                {
                                    this.UserInterface.SmartObjects[7].StringInput[(ushort)(roomIndex * 2 + 12)].StringValue =
                                        _parent.BuildHTMLString(this.Number, name, "24");
                                    this.UserInterface.SmartObjects[7].BooleanInput[(ushort)(roomIndex * 7 + 4016)].BoolValue = true;
                                }
                            }
                            else
                            {
                                if (this.HTML_UI)
                                {
                                    this._HTMLContract.MusicRoomControl[roomIndex].musicZoneSource((sig, wh) => sig.StringValue = "Off");
                                    this._HTMLContract.MusicRoomControl[roomIndex].musicVolEnable((sig, wh) => sig.BoolValue = false);
                                }
                                else
                                {
                                    this.UserInterface.SmartObjects[7].StringInput[(ushort)(roomIndex * 2 + 12)].StringValue =
                                        _parent.BuildHTMLString(this.Number, "Off", "24");
                                    this.UserInterface.SmartObjects[7].BooleanInput[(ushort)(roomIndex * 7 + 4016)].BoolValue = false;
                                }
                            }
                        };

                    EventHandler volumeHandler = (sender, e) =>
                    {
                        if (this.HTML_UI)
                        {
                            this._HTMLContract.MusicRoomControl[roomIndex].musicVolume((sig, wh) => sig.UShortValue = room.MusicVolume);
                        }
                        else
                        {
                            this.UserInterface.SmartObjects[7].UShortInput[(ushort)(roomIndex + 11)].UShortValue = room.MusicVolume;
                        }

                        if (_sharingMenuTimer != null)
                        {
                            _sharingMenuTimer.Stop();
                            _sharingMenuTimer.Dispose();
                            _sharingMenuTimer = new CTimer(HideSharingMenu, null, 60000, -1);
                        }
                    };

                    EventHandler muteHandler = (sender, e) =>
                    {
                        CrestronConsole.PrintLine("muteHandler fired for roomIndex {0}, room {1}, MusicMuted={2}", roomIndex, room.Name, room.MusicMuted);
                        if (this.HTML_UI)
                        {
                            this._HTMLContract.MusicRoomControl[roomIndex].musicZoneMuted((sig, wh) => sig.BoolValue = room.MusicMuted);
                        }
                        else
                        {
                            this.UserInterface.SmartObjects[7].BooleanInput[(ushort)(roomIndex * 7 + 4014)].BoolValue = room.MusicMuted;
                        }

                        if (_sharingMenuTimer != null)
                        {
                            _sharingMenuTimer.Stop();
                            _sharingMenuTimer.Dispose();
                            _sharingMenuTimer = new CTimer(HideSharingMenu, null, 60000, -1);
                        }
                    };

                    _musicSharingChangeHandlers[roomNumber] = handler;
                    MuteChangeHandlers[room] = muteHandler;
                    VolumeChangeHandlers[room] = volumeHandler;

                    room.MusicSrcStatusChanged += handler;
                    room.MusicVolumeChanged += volumeHandler;
                    room.MusicMutedChanged += muteHandler;

                    ushort currentMusicSource = room.CurrentMusicSrc;
                    if (currentMusicSource > 0 && _parent.manager.MusicSourceZ.ContainsKey(currentMusicSource))
                    {
                        if (this.HTML_UI)
                        {
                            this._HTMLContract.MusicRoomControl[roomIndex].musicZoneSource((sig, wh) => sig.StringValue = _parent.manager.MusicSourceZ[currentMusicSource].Name);
                            this._HTMLContract.MusicRoomControl[roomIndex].musicVolEnable((sig, wh) => sig.BoolValue = true);
                            this._HTMLContract.MusicRoomControl[roomIndex].musicVolume((sig, wh) => sig.UShortValue = room.MusicVolume);
                            this._HTMLContract.MusicRoomControl[roomIndex].musicZoneMuted((sig, wh) => sig.BoolValue = room.MusicMuted);
                        }
                        else
                        {
                            this.UserInterface.SmartObjects[7].StringInput[(ushort)(roomIndex * 2 + 12)].StringValue =
                                _parent.BuildHTMLString(this.Number, _parent.manager.MusicSourceZ[currentMusicSource].Name, "24");
                            this.UserInterface.SmartObjects[7].BooleanInput[(ushort)(roomIndex * 7 + 4016)].BoolValue = true;
                            this.UserInterface.SmartObjects[7].UShortInput[(ushort)(roomIndex + 11)].UShortValue = room.MusicVolume;
                            this.UserInterface.SmartObjects[7].BooleanInput[(ushort)(roomIndex * 7 + 4014)].BoolValue = room.MusicMuted;
                        }
                    }
                    else
                    {
                        if (this.HTML_UI)
                        {
                            this._HTMLContract.MusicRoomControl[roomIndex].musicZoneSource((sig, wh) => sig.StringValue = "Off");
                            this._HTMLContract.MusicRoomControl[roomIndex].musicVolEnable((sig, wh) => sig.BoolValue = false);
                        }
                        else
                        {
                            this.UserInterface.SmartObjects[7].StringInput[(ushort)(roomIndex * 2 + 12)].StringValue =
                                _parent.BuildHTMLString(this.Number, "Off", "24");
                            this.UserInterface.SmartObjects[7].BooleanInput[(ushort)(roomIndex * 7 + 4016)].BoolValue = false;
                        }
                    }
                }
            }
        }
    }
}
