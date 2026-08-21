using System;
using System.Linq;
using Crestron.SimplSharp;

namespace ACS_4Series_Template_V3
{
    public partial class ControlSystem
    {
        #region Video/DM Methods

        public void SetVSRCGroup(ushort TPNumber, ushort group)
        {
            ushort currentRoomNumber = manager.touchpanelZ[TPNumber].CurrentRoomNum;
            ushort numVSrcs = (ushort)manager.VideoSrcScenarioZ[manager.RoomZ[currentRoomNumber].VideoSrcScenario].IncludedSources.Count;
            ushort numVidGroups = (ushort)(numVSrcs / 6);
            ushort modVid = (ushort)(numVSrcs % 6);

            if (modVid > 0) { numVidGroups++; }
            else if (numVidGroups == 0) { numVidGroups++; }
            if (group <= numVidGroups) { manager.touchpanelZ[TPNumber].CurrentVSrcGroupNum = group; }
            else { manager.touchpanelZ[TPNumber].CurrentVSrcGroupNum = 1; }
            if (manager.touchpanelZ[TPNumber].UseAnalogModes)
            {
                if (numVSrcs < 6)
                {
                    for (ushort i = 0; i < 6; i++)
                    {
                        manager.touchpanelZ[TPNumber].UserInterface.BooleanInput[(ushort)(511 + i)].BoolValue = false;
                        if (i < modVid)
                        {
                            manager.touchpanelZ[TPNumber].UserInterface.BooleanInput[(ushort)(511 + i)].BoolValue = true;
                        }
                    }
                }
                else if (manager.touchpanelZ[TPNumber].CurrentVSrcGroupNum == numVidGroups && modVid > 0)
                {
                    for (ushort i = 0; i < 6; i++)
                    {
                        manager.touchpanelZ[TPNumber].UserInterface.BooleanInput[(ushort)(511 + i)].BoolValue = false;
                        if (i < modVid)
                        {
                            manager.touchpanelZ[TPNumber].UserInterface.BooleanInput[(ushort)(511 + i)].BoolValue = true;
                        }
                    }
                }
                else
                {
                    for (ushort i = 0; i < 6; i++)
                    {
                        manager.touchpanelZ[TPNumber].UserInterface.BooleanInput[(ushort)(511 + i)].BoolValue = true;
                    }
                }
            }
            for (ushort i = 0; i < 6; i++)
            {
                manager.touchpanelZ[TPNumber].UserInterface.BooleanInput[(ushort)(501 + i)].BoolValue = false;
                manager.touchpanelZ[TPNumber].UserInterface.BooleanInput[(ushort)(521 + i)].BoolValue = false;
            }
            for (ushort i = 0; i < 6; i++)
            {
                if ((ushort)((manager.touchpanelZ[TPNumber].CurrentVSrcGroupNum - 1) * 6 + i) >= numVSrcs) { break; }
                ushort srcNum = manager.VideoSrcScenarioZ[manager.RoomZ[currentRoomNumber].VideoSrcScenario].IncludedSources[(ushort)((manager.touchpanelZ[TPNumber].CurrentVSrcGroupNum - 1) * 6 + i)];
                if (manager.VideoSourceZ[srcNum].InUse)
                {
                    manager.touchpanelZ[TPNumber].UserInterface.BooleanInput[(ushort)(521 + i)].BoolValue = true;
                }
                else
                {
                    manager.touchpanelZ[TPNumber].UserInterface.BooleanInput[(ushort)(521 + i)].BoolValue = false;
                }

                if (srcNum == manager.RoomZ[currentRoomNumber].CurrentVideoSrc)
                {
                    if (i == 5)
                    {
                        manager.touchpanelZ[TPNumber].UserInterface.BooleanInput[(ushort)(506)].BoolValue = true;
                    }
                    else
                    {
                        manager.touchpanelZ[TPNumber].UserInterface.BooleanInput[(ushort)(500 + ((i + 1) % 6))].BoolValue = true;
                    }
                }
                manager.touchpanelZ[TPNumber].UserInterface.StringInput[(ushort)(201 + i)].StringValue = manager.VideoSourceZ[srcNum].DisplayName;
                manager.touchpanelZ[TPNumber].UserInterface.UShortInput[(ushort)(201 + i)].UShortValue = manager.VideoSourceZ[srcNum].AnalogModeNumber;
            }
        }

        public void SendReceiverInputTurnOffVideo(ushort videoSwitcherOutputNum, ushort asrcScenario, ushort musicSourceNumber)
        {
            // Not implemented - placeholder for future functionality
        }

        public bool is8ZoneBox(ushort roomNumber)
        {
            bool isEightZoneBox = false;
            if (manager.RoomZ[roomNumber].NAXBoxNumber > 0)
            {
                if (manager.NAXBoxZ[manager.RoomZ[roomNumber].NAXBoxNumber].Type.ToUpper().Contains("8"))
                {
                    isEightZoneBox = true;
                }
            }
            return isEightZoneBox;
        }

        // Same test as is8ZoneBox but keyed on a NAX box number directly, so a room's video-only
        // audio zone (which may live on a different box than its music zone) resolves correctly.
        public bool is8ZoneBoxByBox(ushort naxBoxNumber)
        {
            if (naxBoxNumber > 0 && manager.NAXBoxZ.ContainsKey(naxBoxNumber))
            {
                return manager.NAXBoxZ[naxBoxNumber].Type.ToUpper().Contains("8");
            }
            return false;
        }

        // These three are called with a display's AssignedToRoomNum, and AVR displays are
        // deliberately assigned to placeholder room numbers that do NOT exist in RoomZ (88 =
        // "Family Room AVR", 89 = "Master Bedroom AVR" in the current config). Indexing RoomZ
        // directly therefore threw KeyNotFoundException and aborted UpdateRoomAVConfig partway
        // through its display loop — which took the whole of InitializeSystem down with it on every
        // single boot ("Error in InitializeSystem: The given key '88' was not present"). Everything
        // after that call never ran: the remaining displays' EISC config, UpdateRoomOptions, the
        // quick-action preset names, PushMusicSourceCatalog, and InitCompleteTimer — so
        // ControlSystem.initComplete stayed false and the panel-reconnect re-init path was dead.
        // StartupRooms already guards this exact case; these did not. Returning the zero/false
        // default for an unknown room is correct: a placeholder room has no audio zone.

        // True when a room has a dedicated TV audio zone separate from its music zone. When true,
        // video and music play on independent NAX outputs and neither turns the other off.
        public bool HasIndependentVideoAudio(ushort roomNumber)
        {
            if (!manager.RoomZ.ContainsKey(roomNumber)) return false;
            return manager.RoomZ[roomNumber].VideoAudioID > 0;
        }

        // NAX output the room's video audio routes to: the dedicated video zone when configured,
        // otherwise the room's single (shared) audio zone. Byte-identical to today when VideoAudioID == 0.
        public ushort GetVideoAudioID(ushort roomNumber)
        {
            if (!manager.RoomZ.ContainsKey(roomNumber)) return 0;
            ushort videoAudioID = manager.RoomZ[roomNumber].VideoAudioID;
            return videoAudioID > 0 ? videoAudioID : manager.RoomZ[roomNumber].AudioID;
        }

        // NAX box that owns the room's video audio zone (falls back to the room's music box).
        public ushort GetVideoNAXBox(ushort roomNumber)
        {
            if (!manager.RoomZ.ContainsKey(roomNumber)) return 0;
            ushort videoBox = manager.RoomZ[roomNumber].VideoNAXBoxNumber;
            return videoBox > 0 ? videoBox : manager.RoomZ[roomNumber].NAXBoxNumber;
        }

        public void DmOutputChanged(ushort dmOutNumber, ushort switcherInputNumber)
        {
            dmOutNumber = (ushort)(dmOutNumber - 500);
            ushort sourceNumber = 0;
            if (switcherInputNumber > 0)
            {
                videoEISC1.BooleanInput[(ushort)(switcherInputNumber + 100)].BoolValue = true;
                // FirstOrDefault returns default(KeyValuePair) — Key 0 — when no source is fed
                // from this switcher input, and VideoSourceZ[0] then throws, aborting the whole
                // handler before the recalculation below. The in-use lamps would stay frozen at
                // whatever they last were, which is exactly how a source gets stuck lit.
                ushort key = manager.VideoSourceZ.FirstOrDefault(p => p.Value.VidSwitcherInputNumber == switcherInputNumber).Key;
                if (manager.VideoSourceZ.ContainsKey(key))
                {
                    manager.VideoSourceZ[key].InUse = true;
                    sourceNumber = key;
                }
            }

            // Only update room/panel UI if the source actually changed (avoid redundant updates
            // since SelectDisplayVideoSource already sent panel feedback before the DM switch)
            bool sourceChanged = false;
            foreach (var kv in manager.VideoDisplayZ)
            {
                if (kv.Value.VideoOutputNum == dmOutNumber)
                {
                    // Same placeholder-room hazard as GetVideoAudioID above — AVR displays point at
                    // room numbers that aren't in RoomZ, and this runs on every DM route change.
                    if (!manager.RoomZ.ContainsKey(kv.Value.AssignedToRoomNum)) continue;
                    var room = manager.RoomZ[kv.Value.AssignedToRoomNum];
                    if (room.CurrentVideoSrc != sourceNumber)
                    {
                        sourceChanged = true;
                        break;
                    }
                }
            }

            if (sourceChanged)
            {
                videoSystemControl.UpdateRoomVideoStatusText(dmOutNumber, sourceNumber);
            }

            // Always recalculate InUse status. This used to be a second, subtly different copy
            // of RecalculateVideoSourceInUse: it had no local-source rule, and it only ever
            // CLEARED the flag (k == 0) — so once a source was marked in use it could never be
            // corrected here, only extinguished. It also walked VideoSourceZ/RoomZ as 1..Count,
            // which throws the moment either dictionary is not numbered contiguously from 1.
            videoSystemControl.RecalculateVideoSourceInUse();

            // Only update panel menus if source actually changed
            if (sourceChanged)
            {
                foreach (var tp in manager.touchpanelZ)
                {
                    ushort j = tp.Key;
                    ushort currentRoomNumber = manager.touchpanelZ[j].CurrentRoomNum;
                    ushort panelVideoOutputNumber = manager.RoomZ[currentRoomNumber].VideoOutputNum;
                    if (panelVideoOutputNumber == dmOutNumber)
                    {
                        videoSystemControl.UpdateTPVideoMenu(j);
                    }
                }
            }
        }

        /// <summary>Push the video source "in use" lamps to every panel that shows them.
        /// InUse is global state — one room selecting a source lights it on all the others —
        /// so it cannot be refreshed from the changed room's panels alone.</summary>
        public void RefreshVideoSourceInUseFeedback()
        {
            foreach (var tp in manager.touchpanelZ)
            {
                tp.Value.videoSourceInUseFB();
            }
        }

        #endregion
    }
}
