using System;
using System.Linq;
using Crestron.SimplSharp;
using ACS_4Series_Template_V3.UI;

namespace ACS_4Series_Template_V3
{
    public partial class ControlSystem
    {
        #region Floor and Zone Navigation

        public ushort FindOutWhichFloorThisRoomIsOn(ushort TPNumber, ushort roomNumber)
        {
            ushort floorNumber = 0;
            ushort floorScenario = manager.touchpanelZ[TPNumber].FloorScenario;
            for (ushort i = 0; i < manager.FloorScenarioZ[floorScenario].IncludedFloors.Count; i++)
            {
                ushort actualFloorNum = manager.FloorScenarioZ[floorScenario].IncludedFloors[i];
                if (manager.Floorz.ContainsKey(actualFloorNum) &&
                    manager.Floorz[actualFloorNum].IncludedRooms.Contains(roomNumber) &&
                    manager.Floorz[actualFloorNum].Name.ToUpper() != "ALL")
                {
                    floorNumber = actualFloorNum;
                    break;
                }
            }
            return floorNumber;
        }

        public ushort FloorButtonNumberToHighLight(ushort TPNumber, ushort floorNumber)
        {
            ushort floorScenario = 0;
            ushort buttonNumber = 0;

            if (TPNumber > 0 && floorNumber > 0)
            {
                floorScenario = manager.touchpanelZ[TPNumber].FloorScenario;
                buttonNumber = (ushort)manager.FloorScenarioZ[floorScenario].IncludedFloors.IndexOf(floorNumber);
            }
            buttonNumber++;
            return buttonNumber;
        }

        public void SelectFloor(ushort TPNumber, ushort floorButtonNumber)
        {
            ushort floorScenarioNum = manager.touchpanelZ[TPNumber].FloorScenario;

            ushort currentFloor = 1;
            if (floorButtonNumber > 0)
            {
                currentFloor = this.manager.FloorScenarioZ[floorScenarioNum].IncludedFloors[floorButtonNumber - 1];
            }
            else if (this.manager.touchpanelZ[TPNumber].CurrentFloorNum > 0)
            {
                currentFloor = this.manager.touchpanelZ[TPNumber].CurrentFloorNum;
                floorButtonNumber = FloorButtonNumberToHighLight(TPNumber, currentFloor);
            }
            if (manager.FloorScenarioZ[floorScenarioNum].IncludedFloors.Count > 1)
            {
                this.manager.touchpanelZ[TPNumber].CurrentFloorNum = currentFloor;
            }
            ushort currentNumberOfZones = (ushort)this.manager.Floorz[currentFloor].IncludedRooms.Count();
            if (manager.touchpanelZ[TPNumber].HTML_UI)
            {
                manager.touchpanelZ[TPNumber]._HTMLContract.roomList.numberOfZones(
                        (sig, wh) => sig.UShortValue = currentNumberOfZones);
            }
            else
            {
                manager.touchpanelZ[TPNumber].UserInterface.SmartObjects[4].UShortInput[3].UShortValue = currentNumberOfZones;
            }
            manager.touchpanelZ[TPNumber].floorButtonFB(floorButtonNumber);
            manager.touchpanelZ[TPNumber].SubscribeToListOfRoomsStatusEvents(currentFloor);
            UpdateRoomListNameAndImage(TPNumber);//from SelectFloor
        }

        public void SelectWholeHouseFloor(ushort TPNumber, ushort floorButtonNumber)
        {
            ushort wholeHouseSubsystemScenarioNum = manager.touchpanelZ[TPNumber].HomePageScenario;
            ushort currentSubsystem = manager.touchpanelZ[TPNumber].CurrentSubsystemNumber;

            for (ushort i = 0; i < manager.WholeHouseSubsystemScenarioZ[wholeHouseSubsystemScenarioNum].WholeHouseSubsystems.Count; i++)
            {
                if (manager.WholeHouseSubsystemScenarioZ[wholeHouseSubsystemScenarioNum].WholeHouseSubsystems[i].SubsystemNumber == currentSubsystem)
                {
                    ushort floorNumber = manager.WholeHouseSubsystemScenarioZ[wholeHouseSubsystemScenarioNum].WholeHouseSubsystems[i].IncludedFloors[floorButtonNumber - 1];
                    manager.touchpanelZ[TPNumber].CurrentFloorNum = floorNumber;
                    manager.touchpanelZ[TPNumber].WholeHouseRoomList.Clear();
                    string subsystemName = manager.SubsystemZ[currentSubsystem].Name.ToUpper();
                    string subsystemIcon = manager.SubsystemZ[currentSubsystem].IconSerial;
                    string subsystemIconHTML = manager.SubsystemZ[currentSubsystem].IconHTML;

                    foreach (ushort roomNumber in manager.Floorz[floorNumber].IncludedRooms)
                    {
                        var room = manager.RoomZ[roomNumber];
                        bool isValidRoom = false;

                        if (subsystemName.Contains("LIGHT"))
                        {
                            isValidRoom = room.LightsID > 0;
                        }
                        else if (subsystemName.Contains("CLIMATE") || subsystemName.Contains("HVAC"))
                        {
                            isValidRoom = room.ClimateID > 0;
                        }
                        else if (subsystemName.Contains("SHADE") || subsystemName.Contains("DRAPE"))
                        {
                            isValidRoom = room.ShadesID > 0;
                        }
                        else
                        {
                            isValidRoom = true;
                        }

                        if (isValidRoom)
                        {
                            manager.touchpanelZ[TPNumber].WholeHouseRoomList.Add(roomNumber);
                        }
                    }

                    ushort currentNumberOfZones = (ushort)manager.touchpanelZ[TPNumber].WholeHouseRoomList.Count;

                    if (manager.touchpanelZ[TPNumber].HTML_UI)
                    {
                        manager.touchpanelZ[TPNumber]._HTMLContract.WholeHouseZoneList.numberOfWholeHouseZones(
                                (sig, wh) => sig.UShortValue = currentNumberOfZones);
                    }
                    else
                    {
                        manager.touchpanelZ[TPNumber].UserInterface.SmartObjects[10].UShortInput[3].UShortValue = currentNumberOfZones;
                    }

                    for (ushort j = 0; j < currentNumberOfZones; j++)
                    {
                        ushort roomNumber = manager.touchpanelZ[TPNumber].WholeHouseRoomList[j];
                        var room = manager.RoomZ[roomNumber];
                        string roomName = room.Name;
                        string statusText = "";

                        if (subsystemName.Contains("CLIMATE") || subsystemName.Contains("HVAC"))
                        {
                            statusText = room.HVACStatusText ?? "";
                        }
                        else if (subsystemName.Contains("LIGHT"))
                        {
                            statusText = room.LightStatusText ?? "";
                            CrestronConsole.PrintLine("Room {0} LightStatusText: {1}", room.Name, statusText);
                        }

                        ushort index = j;
                        string capturedRoomName = roomName;
                        string capturedStatusText = statusText;
                        string capturedIcon = subsystemIconHTML;

                        if (manager.touchpanelZ[TPNumber].HTML_UI)
                        {
                            manager.touchpanelZ[TPNumber]._HTMLContract.WholeHouseZone[index].HouseZoneName(
                                    (sig, wh) => sig.StringValue = capturedRoomName);
                            manager.touchpanelZ[TPNumber]._HTMLContract.WholeHouseZone[index].HouseZoneStatus(
                                    (sig, wh) => sig.StringValue = capturedStatusText);
                            manager.touchpanelZ[TPNumber]._HTMLContract.WholeHouseZone[index].HouseZoneIcon(
                                    (sig, wh) => sig.StringValue = capturedIcon);
                        }
                        else
                        {
                            manager.touchpanelZ[TPNumber].UserInterface.SmartObjects[10].StringInput[(ushort)(3 * j + 11)].StringValue = roomName;
                            manager.touchpanelZ[TPNumber].UserInterface.SmartObjects[10].StringInput[(ushort)(3 * j + 12)].StringValue = statusText;
                            manager.touchpanelZ[TPNumber].UserInterface.SmartObjects[10].StringInput[(ushort)(3 * j + 13)].StringValue = subsystemIcon;
                        }
                    }

                    CrestronConsole.PrintLine("SelectWholeHouseFloor TP-{0} floor{1} numZones{2}", TPNumber, floorNumber, currentNumberOfZones);
                    manager.touchpanelZ[TPNumber].floorButtonFB(floorButtonNumber);
                    manager.touchpanelZ[TPNumber].SubscribeToWholeHouseListEvents();

                    break;
                }
            }
        }

        public void SelectMusicFloor(ushort TPNumber, ushort floorButtonNumber)
        {
            ushort floorScenarioNum = manager.touchpanelZ[TPNumber].FloorScenario;

            ushort currentFloor = 1;
            if (floorButtonNumber > 0)
            {
                currentFloor = this.manager.FloorScenarioZ[floorScenarioNum].IncludedFloors[floorButtonNumber - 1];
                ushort currentRoomNumber = manager.touchpanelZ[TPNumber].CurrentRoomNum;
                manager.touchpanelZ[TPNumber].CurrentMusicFloorNum = currentFloor;
                manager.touchpanelZ[TPNumber].musicFloorButtonFB(floorButtonNumber);

                foreach (var rm in manager.Floorz[currentFloor].IncludedRooms)
                {
                    var room = manager.RoomZ[rm];
                    room.MusicVolume = room.MusicVolume;
                }

                UpdateMusicSharingPage(TPNumber, currentRoomNumber);
                if (manager.touchpanelZ[TPNumber].SrcSharingButtonFB)
                {
                    manager.touchpanelZ[TPNumber].SubscribeToMusicSharingChanges();
                }
            }
        }

        public void SelectOnlyFloor(ushort TPNumber)
        {
            ushort floorScenarioNum = manager.touchpanelZ[TPNumber].FloorScenario;
            if (manager.FloorScenarioZ[floorScenarioNum].IncludedFloors.Count == 1)
            {
                SelectFloor((ushort)(TPNumber), 1);
            }
        }

        #endregion

        #region Zone Selection

        public void SelectZone(ushort TPNumber, ushort zoneListButtonNumber, bool selectDefaultSubsystem)
        {
            imageEISC.BooleanInput[(ushort)(TPNumber + 100)].BoolValue = false;
            imageEISC.BooleanInput[TPNumber].BoolValue = false;
            manager.touchpanelZ[TPNumber].CurrentSubsystemIsAudio = false;
            manager.touchpanelZ[TPNumber].CurrentSubsystemIsVideo = false;
            ushort currentRoomNumber = 0;
            ushort previousRoomNumber = 0;
            if (manager.touchpanelZ[TPNumber].CurrentRoomNum > 0)
            {
                previousRoomNumber = manager.touchpanelZ[TPNumber].CurrentRoomNum;
            }
            if (zoneListButtonNumber > 0)
            {
                currentRoomNumber = manager.Floorz[manager.touchpanelZ[TPNumber].CurrentFloorNum].IncludedRooms[zoneListButtonNumber - 1];
                manager.touchpanelZ[TPNumber].CurrentRoomNum = currentRoomNumber;
            }
            if (currentRoomNumber > 0)
            {
                if (manager.RoomZ[currentRoomNumber].CurrentDisplayNumber > 0)
                {
                    manager.touchpanelZ[TPNumber].CurrentDisplayNumber = manager.RoomZ[currentRoomNumber].CurrentDisplayNumber;
                }
                if (!manager.touchpanelZ[TPNumber].DontInheritSubsystemScenario)
                {
                    manager.touchpanelZ[TPNumber].SubSystemScenario = manager.RoomZ[currentRoomNumber].SubSystemScenario;
                }
                ushort currentMusicSource = manager.RoomZ[currentRoomNumber].CurrentMusicSrc;

                if (currentMusicSource > 0)
                {
                    manager.touchpanelZ[TPNumber].UserInterface.StringInput[3].StringValue = manager.MusicSourceZ[currentMusicSource].Name;
                }
                manager.touchpanelZ[TPNumber].UserInterface.BooleanInput[1009].BoolValue = manager.RoomZ[currentRoomNumber].MusicMuted;
                manager.touchpanelZ[TPNumber].UserInterface.UShortInput[2].UShortValue = manager.RoomZ[currentRoomNumber].MusicVolume;
                UpdateSubsystems(TPNumber);
                UpdateEquipIDsForSubsystems(TPNumber, currentRoomNumber);
                climateControl.SyncPanelToClimateZone(TPNumber);
                string imagePath = (manager.touchpanelZ[TPNumber].IsConnectedRemotely) ? string.Format("http://{0}:{1}/{2}", manager.ProjectInfoZ[0].DDNSAdress, httpPort, manager.RoomZ[currentRoomNumber].ImageURL) : string.Format("http://{0}:{1}/{2}", IPaddress, httpPort, manager.RoomZ[currentRoomNumber].ImageURL);
                if (manager.touchpanelZ[TPNumber].HTML_UI)
                {
                    imagePath = (manager.touchpanelZ[TPNumber].IsConnectedRemotely) ? string.Format("http://{0}:{1}/{2}", manager.ProjectInfoZ[0].DDNSAdress, httpsPort, manager.RoomZ[currentRoomNumber].ImageURL) : string.Format("http://{0}:{1}/{2}", IPaddress, httpsPort, manager.RoomZ[currentRoomNumber].ImageURL);
                }
                manager.touchpanelZ[TPNumber].UserInterface.StringInput[5].StringValue = imagePath;
                ushort asrcScenarioNum = manager.RoomZ[currentRoomNumber].AudioSrcScenario;

                if (currentMusicSource == 0)
                {
                    musicSystemControl.UpdatePanelToMusicZoneOff(TPNumber);
                }
                if (asrcScenarioNum > 0)
                {
                    ushort numASrcs = (ushort)manager.AudioSrcScenarioZ[asrcScenarioNum].IncludedSources.Count;
                    if (manager.touchpanelZ[TPNumber].HTML_UI)
                    {
                        manager.touchpanelZ[TPNumber]._HTMLContract.musicSourceList.numberOfMusicSources(
                                (sig, wh) => sig.UShortValue = numASrcs);
                    }
                    else
                    {
                        manager.touchpanelZ[TPNumber].UserInterface.SmartObjects[6].UShortInput[4].UShortValue = numASrcs;
                    }
                    bool useAnalogModes = manager.touchpanelZ[TPNumber].UseAnalogModes;
                    if (useAnalogModes && numASrcs > 6)
                    {
                        for (ushort i = 0; i < 6; i++)
                        {
                            manager.touchpanelZ[TPNumber].UserInterface.BooleanInput[(ushort)(541 + i)].BoolValue = true;
                        }
                    }
                    for (ushort i = 0; i < numASrcs; i++)
                    {
                        ushort srcNum = manager.AudioSrcScenarioZ[asrcScenarioNum].IncludedSources[i];

                        if (manager.touchpanelZ[TPNumber].HTML_UI)
                        {
                            manager.touchpanelZ[TPNumber]._HTMLContract.musicSourceSelect[i].musicSourceName(
                                (sig, wh) => sig.StringValue = manager.MusicSourceZ[srcNum].Name);
                            manager.touchpanelZ[TPNumber]._HTMLContract.musicSourceSelect[i].musicSourceIcon(
                                (sig, wh) => sig.StringValue = manager.MusicSourceZ[srcNum].IconHTML);
                        }
                        else
                        {
                            manager.touchpanelZ[TPNumber].UserInterface.SmartObjects[6].StringInput[(ushort)(i + 11)].StringValue = BuildHTMLString(TPNumber, manager.MusicSourceZ[srcNum].Name, "26");
                            manager.touchpanelZ[TPNumber].UserInterface.SmartObjects[6].StringInput[(ushort)(i + 2011)].StringValue = manager.MusicSourceZ[srcNum].IconSerial;
                        }
                        if (useAnalogModes)
                        {
                            manager.touchpanelZ[TPNumber].UserInterface.UShortInput[(ushort)(i + 211)].UShortValue = manager.MusicSourceZ[srcNum].AnalogModeNumber;
                        }
                        if (srcNum == currentMusicSource)
                        {
                            manager.touchpanelZ[TPNumber].musicButtonFB((ushort)(i + 1));
                            manager.touchpanelZ[TPNumber].musicPageFlips(manager.MusicSourceZ[srcNum].FlipsToPageNumber);
                        }
                    }
                }

                CrestronConsole.PrintLine("current room {0} vsrcscenario {1}", manager.RoomZ[currentRoomNumber].Name, manager.RoomZ[currentRoomNumber].VideoSrcScenario);
                if (manager.RoomZ[currentRoomNumber].VideoSrcScenario > 0)
                {
                    videoSystemControl.UpdateTPVideoMenu(TPNumber);
                }
                UpdateRoomOptions(TPNumber);
                videoSystemControl.UpdateDisplaysAvailableForSelection(TPNumber, currentRoomNumber);

                UpdateMusicSharingPage(TPNumber, currentRoomNumber);
                ushort configScenario = manager.RoomZ[currentRoomNumber].ConfigurationScenario;
                if (configScenario > 0 && manager.VideoConfigScenarioZ[configScenario].HasReceiver && manager.VideoConfigScenarioZ[configScenario].MusicThroughReceiver > 0 && !manager.VideoConfigScenarioZ[configScenario].ReceiverHasVolFB)
                {
                    manager.touchpanelZ[TPNumber].UserInterface.BooleanInput[1000].BoolValue = false;
                }
                else
                {
                    manager.touchpanelZ[TPNumber].UserInterface.BooleanInput[1000].BoolValue = true;
                }
                musicSystemControl.UpdateTPMusicMenu((ushort)(TPNumber));
                try
                {
                    manager.touchpanelZ[TPNumber].SubscribeToMusicMenuEvents(currentRoomNumber);
                }
                catch (System.Collections.Generic.KeyNotFoundException ex)
                {
                    CrestronConsole.PrintLine("Warning: Error in SubscribeToMusicMenuEvents for room {0}: {1}",
                        currentRoomNumber, ex.Message);
                }

                try
                {
                    manager.touchpanelZ[TPNumber].SubscribeToVideoMenuEvents(currentRoomNumber);
                }
                catch (System.Collections.Generic.KeyNotFoundException ex)
                {
                    CrestronConsole.PrintLine("Warning: Error in SubscribeToVideoMenuEvents for room {0}: {1}",
                        currentRoomNumber, ex.Message);
                }
                manager.touchpanelZ[TPNumber].SubscribeToRoomSubsystemEvents(currentRoomNumber, previousRoomNumber);

                ushort flipToSubsysNumOnSelect = manager.RoomZ[currentRoomNumber].OpenSubsysNumOnRmSelect;
                ushort currentSubsystemScenario = manager.RoomZ[currentRoomNumber].SubSystemScenario;
                if (selectDefaultSubsystem && manager.SubsystemScenarioZ[currentSubsystemScenario].IncludedSubsystems.Contains(flipToSubsysNumOnSelect) && manager.touchpanelZ[TPNumber].Type.ToUpper() != "CRESTRONAPP" && manager.touchpanelZ[TPNumber].Type.ToUpper() != "TSR310")
                {
                    SelectSubsystemPage(TPNumber, flipToSubsysNumOnSelect);
                }
            }
        }

        public void BuildWholeHouseRoomListFromSubsys(ushort TPNumber)
        {
            ushort subsystemNumber = manager.touchpanelZ[TPNumber].CurrentSubsystemNumber;
            ushort floorNumber = manager.touchpanelZ[TPNumber].CurrentFloorNum;

            if (subsystemNumber > 0 && floorNumber > 0)
            {
                manager.touchpanelZ[TPNumber].WholeHouseRoomList.Clear();
                ushort numRooms = (ushort)manager.Floorz[floorNumber].IncludedRooms.Count;
                for (ushort i = 0; i < numRooms; i++)
                {
                    ushort roomNumber = manager.Floorz[floorNumber].IncludedRooms[i];
                    ushort subsysScenarioNum = manager.RoomZ[roomNumber].SubSystemScenario;
                    if (manager.SubsystemScenarioZ[subsysScenarioNum].IncludedSubsystems.Contains(subsystemNumber))
                    {
                        manager.touchpanelZ[TPNumber].WholeHouseRoomList.Add(roomNumber);
                    }
                }
            }
        }

        public void WholeHouseUpdateZoneList(ushort TPNumber)
        {
            if (logging) { CrestronConsole.PrintLine("&&& start WholeHouseUpdateZoneList TP-{0} {1}:{2}", TPNumber, DateTime.Now.Second, DateTime.Now.Millisecond); }
            BuildWholeHouseRoomListFromSubsys(TPNumber);
            manager.touchpanelZ[TPNumber].SubscribeToWholeHouseListEvents();
            if (logging) { CrestronConsole.PrintLine("&&& end WholeHouseUpdateZoneList TP-{0} {1}:{2}", TPNumber, DateTime.Now.Second, DateTime.Now.Millisecond); }
        }

        #endregion

        #region Room Page Updates

        public void UpdateRoomListNameAndImage(ushort TPNumber)
        {
            if (!manager.touchpanelZ.ContainsKey(TPNumber))
            {
                ErrorLog.Error("Error: Invalid TPNumber {0} in UpdateRoomListNameAndImage", TPNumber);
                CrestronConsole.PrintLine("Error: Invalid TPNumber {0} in UpdateRoomListNameAndImage", TPNumber);
                return;
            }

            ushort currentFloorNum = manager.touchpanelZ[TPNumber].CurrentFloorNum;
            if (currentFloorNum == 0 || !manager.Floorz.ContainsKey(currentFloorNum))
            {
                CrestronConsole.PrintLine("Error: Invalid floor {0} for TP-{1}", currentFloorNum, TPNumber);
                return;
            }

            ushort currentNumberOfZones = (ushort)this.manager.Floorz[currentFloorNum].IncludedRooms.Count();
            for (ushort i = 0; i < currentNumberOfZones; i++)
            {
                ushort zoneTemp = this.manager.Floorz[currentFloorNum].IncludedRooms[i];
                if (!manager.RoomZ.ContainsKey(zoneTemp))
                {
                    ErrorLog.Error("UpdateRoomsPageStatusText: Room {0} doesn't exist in RoomZ from TP-{1}", zoneTemp, TPNumber);
                    CrestronConsole.PrintLine("UpdateRoomsPageStatusText: Room {0} doesn't exist in RoomZ from TP-{1}", zoneTemp, TPNumber);
                    continue;
                }

                string imageUrl = manager.RoomZ[zoneTemp].ImageURL ?? "";
                string imagePath = "";

                if (!string.IsNullOrEmpty(imageUrl))
                {
                    imagePath = (manager.touchpanelZ[TPNumber].IsConnectedRemotely)
                        ? string.Format("http://{0}:{1}/{2}", manager.ProjectInfoZ[0].DDNSAdress ?? "", httpsPort ?? "", imageUrl)
                        : string.Format("http://{0}:{1}/{2}", IPaddress ?? "", httpsPort ?? "", imageUrl);
                }

                if (manager.touchpanelZ[TPNumber].HTML_UI)
                {
                    manager.touchpanelZ[TPNumber]._HTMLContract.roomButton[i].zoneName(
                            (sig, wh) => sig.StringValue = this.manager.RoomZ[zoneTemp].Name ?? "");
                    manager.touchpanelZ[TPNumber]._HTMLContract.roomButton[i].zoneImage(
                            (sig, wh) => sig.StringValue = imagePath);
                }
                else
                {
                    if (!string.IsNullOrEmpty(imageUrl))
                    {
                        imagePath = (manager.touchpanelZ[TPNumber].IsConnectedRemotely)
                            ? string.Format("http://{0}:{1}/{2}", manager.ProjectInfoZ[0].DDNSAdress ?? "", httpPort ?? "", imageUrl)
                            : string.Format("http://{0}:{1}/{2}", IPaddress ?? "", httpPort ?? "", imageUrl);
                    }

                    manager.touchpanelZ[TPNumber].UserInterface.SmartObjects[4].StringInput[(ushort)(4 * i + 11)].StringValue = this.manager.RoomZ[zoneTemp].Name ?? "";
                    manager.touchpanelZ[TPNumber].UserInterface.SmartObjects[4].StringInput[(ushort)(4 * i + 14)].StringValue = imagePath;
                }
            }
        }

        public void UpdateTPFloorNames(ushort TPNumber)
        {
            ushort floorScenarioNum = manager.touchpanelZ[TPNumber].FloorScenario;
            if (manager.touchpanelZ[TPNumber].HTML_UI)
            {
                manager.touchpanelZ[TPNumber]._HTMLContract.FloorList.NumberOfFloors((sig, wh) => sig.UShortValue = (ushort)manager.FloorScenarioZ[floorScenarioNum].IncludedFloors.Count);
            }
            else
            {
                manager.touchpanelZ[TPNumber].UserInterface.SmartObjects[3].UShortInput[4].UShortValue = (ushort)manager.FloorScenarioZ[floorScenarioNum].IncludedFloors.Count;
            }
            for (ushort i = 0; i < manager.FloorScenarioZ[floorScenarioNum].IncludedFloors.Count; i++)
            {
                if (manager.touchpanelZ[TPNumber].HTML_UI)
                {
                    manager.touchpanelZ[TPNumber]._HTMLContract.FloorSelect[i].FloorName(
                            (sig, wh) => sig.StringValue = manager.Floorz[manager.FloorScenarioZ[floorScenarioNum].IncludedFloors[i]].Name);
                }
                else
                {
                    string floorName = string.Format(@"<FONT size=""26"">{0}</FONT>", manager.Floorz[manager.FloorScenarioZ[floorScenarioNum].IncludedFloors[i]].Name);
                    manager.touchpanelZ[TPNumber].UserInterface.SmartObjects[3].StringInput[(ushort)(i + 11)].StringValue = floorName;
                    manager.touchpanelZ[TPNumber].UserInterface.SmartObjects[9].StringInput[(ushort)(i + 11)].StringValue = floorName;
                }
            }
        }

        #endregion

        #region Button Press Handlers

        public void PressCloseXButton(ushort TPNumber)
        {
            CrestronConsole.PrintLine("TP-{0} closeX page{1}", TPNumber, manager.touchpanelZ[TPNumber].CurrentPageNumber);
            ushort currentRoomNum = manager.touchpanelZ[TPNumber].CurrentRoomNum;

            manager.touchpanelZ[TPNumber].CurrentSubsystemIsAudio = false;
            manager.touchpanelZ[TPNumber].musicPageFlips(0);
            manager.touchpanelZ[TPNumber].videoPageFlips(0);
            manager.touchpanelZ[TPNumber].SleepFormatLiftMenu("CLOSE", 0);
            manager.touchpanelZ[TPNumber].UserInterface.BooleanInput[351].BoolValue = false;
            manager.touchpanelZ[TPNumber].UserInterface.BooleanInput[998].BoolValue = false;
            manager.touchpanelZ[TPNumber].UserInterface.BooleanInput[999].BoolValue = false;
            manager.touchpanelZ[TPNumber].UserInterface.BooleanInput[1002].BoolValue = false;
            manager.touchpanelZ[TPNumber].SrcSharingButtonFB = false;
            manager.touchpanelZ[TPNumber].subsystemPageFlips(0);
            if (manager.touchpanelZ[TPNumber].CurrentPageNumber == (ushort)TouchpanelUI.CurrentPageType.Home)
            {
                subsystemEISC.UShortInput[(ushort)(TPNumber + 200)].UShortValue = (ushort)(300 + TPNumber);
            }
            else
            {
                subsystemEISC.UShortInput[(ushort)(TPNumber + 200)].UShortValue = 0;
            }
            if (manager.touchpanelZ[TPNumber].CurrentPageNumber > 0)
            {
                manager.touchpanelZ[TPNumber].CurrentPageNumber = 2;
                imageEISC.BooleanInput[TPNumber].BoolValue = false;
                manager.touchpanelZ[TPNumber].CurrentSubsystemIsVideo = false;
                imageEISC.BooleanInput[(ushort)(TPNumber + 100)].BoolValue = false;
            }
            else
            {
                WholeHouseUpdateZoneList(TPNumber);
                SendSubsystemZonesPageNumber(TPNumber, true);
            }
        }

        public void RoomButtonPress(ushort TPNumber, bool TimedOut)
        {
            CrestronConsole.PrintLine("TP-{0} roomButtonPress", TPNumber);
            if (!manager.touchpanelZ.ContainsKey(TPNumber))
            {
                CrestronConsole.PrintLine("Error: touchpanelZ does not contain key: {0}", TPNumber);
                return;
            }
            manager.touchpanelZ[TPNumber].UserInterface.BooleanInput[91].BoolValue = false;
            manager.touchpanelZ[TPNumber].UserInterface.BooleanInput[94].BoolValue = false;
            manager.touchpanelZ[TPNumber].UserInterface.BooleanInput[50].BoolValue = false;
            manager.touchpanelZ[TPNumber].UserInterface.BooleanInput[51].BoolValue = false;

            manager.touchpanelZ[TPNumber].videoPageFlips(0);
            ushort currentRoom = 0;
            if (TimedOut) { currentRoom = manager.touchpanelZ[TPNumber].DefaultRoom; }
            else { currentRoom = manager.touchpanelZ[TPNumber].CurrentRoomNum; }
            if (currentRoom == 0)
            {
                CrestronConsole.PrintLine("Error: TP-{0} has no valid room assigned (currentRoom=0)", TPNumber);
                HomeButtonPress(TPNumber);
                return;
            }

            if (!manager.RoomZ.ContainsKey(currentRoom))
            {
                CrestronConsole.PrintLine("Error: Room {0} does not exist in RoomZ for TP-{1}", currentRoom, TPNumber);
                HomeButtonPress(TPNumber);
                return;
            }
            ushort floorNumber = FindOutWhichFloorThisRoomIsOn(TPNumber, currentRoom);
            CrestronConsole.PrintLine("TP-{0} room={1} floorNumber={2} floorScenario={3}",
                TPNumber, currentRoom, floorNumber, manager.touchpanelZ[TPNumber].FloorScenario);

            if (floorNumber == 0)
            {
                CrestronConsole.PrintLine("Error: Could not find floor for room {0} in floorScenario {1} for TP-{2}",
                    currentRoom, manager.touchpanelZ[TPNumber].FloorScenario, TPNumber);
                HomeButtonPress(TPNumber);
                return;
            }

            if (!manager.Floorz.ContainsKey(floorNumber))
            {
                CrestronConsole.PrintLine("Error: Floorz does not contain key: {0}", floorNumber);
                HomeButtonPress(TPNumber);
                return;
            }

            if (!manager.Floorz[floorNumber].IncludedRooms.Contains(currentRoom))
            {
                CrestronConsole.PrintLine("Error: Floor {0} does not include room {1}", floorNumber, currentRoom);
                HomeButtonPress(TPNumber);
                return;
            }
            ushort zoneButtonNumber = (ushort)(manager.Floorz[floorNumber].IncludedRooms.IndexOf(currentRoom) + 1);
            manager.touchpanelZ[TPNumber].CurrentPageNumber = (ushort)TouchpanelUI.CurrentPageType.RoomSubsystemList;
            imageEISC.BooleanInput[(ushort)(TPNumber + 100)].BoolValue = false;
            imageEISC.BooleanInput[TPNumber].BoolValue = false;
            manager.touchpanelZ[TPNumber].CurrentSubsystemIsVideo = false;
            manager.touchpanelZ[TPNumber].CurrentFloorNum = floorNumber;

            CrestronConsole.PrintLine("TP-{0} calling SelectFloor", TPNumber);
            try
            {
                SelectFloor(TPNumber, 0);
                CrestronConsole.PrintLine("TP-{0} SelectFloor completed", TPNumber);
            }
            catch (Exception ex)
            {
                CrestronConsole.PrintLine("TP-{0} SelectFloor failed: {1}", TPNumber, ex.Message);
                return;
            }

            CrestronConsole.PrintLine("TP-{0} calling SelectZone", TPNumber);
            try
            {
                SelectZone(TPNumber, zoneButtonNumber, TimedOut);
                CrestronConsole.PrintLine("TP-{0} SelectZone completed", TPNumber);
            }
            catch (Exception ex)
            {
                CrestronConsole.PrintLine("TP-{0} SelectZone failed: {1}", TPNumber, ex.Message);
                return;
            }

            CrestronConsole.PrintLine("TP-{0} setting BooleanInput[100] to TRUE", TPNumber);
            manager.touchpanelZ[TPNumber].UserInterface.BooleanInput[100].BoolValue = true;
            CrestronConsole.PrintLine("TP-{0} BooleanInput[100] is now: {1}", TPNumber, manager.touchpanelZ[TPNumber].UserInterface.BooleanInput[100].BoolValue);
        }

        public void RoomListButtonPress(ushort TPNumber)
        {
            manager.touchpanelZ[TPNumber].UserInterface.BooleanInput[91].BoolValue = false;
            manager.touchpanelZ[TPNumber].UserInterface.BooleanInput[94].BoolValue = false;
            manager.touchpanelZ[TPNumber].CurrentPageNumber = (ushort)TouchpanelUI.CurrentPageType.RoomList;
            imageEISC.BooleanInput[(ushort)(TPNumber + 100)].BoolValue = false;
            manager.touchpanelZ[TPNumber].CurrentSubsystemIsAudio = false;
            manager.touchpanelZ[TPNumber].videoPageFlips(0);
            if (manager.FloorScenarioZ[manager.touchpanelZ[TPNumber].FloorScenario].IncludedFloors.Count > 1)
            {
                UpdateTPFloorNames(TPNumber);
            }

            imageEISC.BooleanInput[TPNumber].BoolValue = false;
            manager.touchpanelZ[TPNumber].CurrentSubsystemIsVideo = false;
            manager.touchpanelZ[TPNumber].subsystemPageFlips(1000);

            SelectFloor(TPNumber, 0);
            subsystemEISC.BooleanInput[(ushort)(TPNumber + 200)].BoolValue = false;
        }

        public void HomeButtonPress(ushort TPNumber)
        {
            CrestronConsole.PrintLine("TP-{0} homebuttonpress", TPNumber);
            if (manager.touchpanelZ[TPNumber].Type != "Tsr310" && manager.touchpanelZ[TPNumber].Type != "HR310")
            {
                ushort homePageScenario = manager.touchpanelZ[TPNumber].HomePageScenario;
                string homeImagePath = (manager.touchpanelZ[TPNumber].IsConnectedRemotely) ? string.Format("http://{0}:{1}/HOME.JPG", manager.ProjectInfoZ[0].DDNSAdress, httpPort) : string.Format("http://{0}:{1}/HOME.JPG", IPaddress, httpPort);
                if (manager.touchpanelZ[TPNumber].HTML_UI)
                {
                    homeImagePath = (manager.touchpanelZ[TPNumber].IsConnectedRemotely) ? string.Format("http://{0}:{1}/HOME.JPG", manager.ProjectInfoZ[0].DDNSAdress, httpsPort) : string.Format("http://{0}:{1}/HOME.JPG", IPaddress, httpsPort);
                }
                subsystemEISC.UShortInput[(ushort)(TPNumber + 200)].UShortValue = (ushort)(300 + TPNumber);
                quickActionControl.RefreshQuickAction(TPNumber);
                manager.touchpanelZ[TPNumber].UserInterface.StringInput[5].StringValue = homeImagePath;
                for (ushort i = 0; i < 10; i++)
                {
                    manager.touchpanelZ[TPNumber].UserInterface.BooleanInput[(ushort)(i + 91)].BoolValue = false;
                }
                manager.touchpanelZ[TPNumber].UserInterface.BooleanInput[50].BoolValue = false;
                manager.touchpanelZ[TPNumber].UserInterface.BooleanInput[51].BoolValue = false;
                manager.touchpanelZ[TPNumber].subsystemPageFlips(10000);
                imageEISC.BooleanInput[TPNumber].BoolValue = false;
                manager.touchpanelZ[TPNumber].CurrentSubsystemIsVideo = false;
                manager.touchpanelZ[TPNumber].CurrentPageNumber = 0;
                if (homePageScenario > 0 && homePageScenario <= this.config.RoomConfig.WholeHouseSubsystemScenarios.Length)
                {
                    updateSubsystemListSmartObject(TPNumber, true);
                }
            }
        }

        #endregion
    }
}
