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

        public void DmOutputChanged(ushort dmOutNumber, ushort switcherInputNumber)
        {
            dmOutNumber = (ushort)(dmOutNumber - 500);
            ushort sourceNumber = 0;
            ushort numberOfVSRCs = (ushort)manager.VideoSourceZ.Count;
            ushort numberOfRooms = (ushort)manager.RoomZ.Count;
            if (switcherInputNumber > 0)
            {
                videoEISC1.BooleanInput[(ushort)(switcherInputNumber + 100)].BoolValue = true;
                ushort key = manager.VideoSourceZ.FirstOrDefault(p => p.Value.VidSwitcherInputNumber == switcherInputNumber).Key;
                manager.VideoSourceZ[key].InUse = true;
                sourceNumber = key;
            }

            videoSystemControl.UpdateRoomVideoStatusText(dmOutNumber, sourceNumber);

            for (ushort i = 1; i <= numberOfVSRCs; i++)
            {
                ushort k = 0;
                for (ushort j = 1; j <= numberOfRooms; j++)
                {
                    if (manager.RoomZ[j].CurrentVideoSrc == i) { k++; }
                }
                if (k == 0)
                {
                    manager.VideoSourceZ[i].InUse = false;
                    videoEISC1.BooleanInput[(ushort)(manager.VideoSourceZ[i].VidSwitcherInputNumber + 100)].BoolValue = false;
                }
            }
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

        #endregion
    }
}
