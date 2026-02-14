using System;
using System.Linq;
using Crestron.SimplSharp;
using ACS_4Series_Template_V3.UI;

namespace ACS_4Series_Template_V3
{
    public partial class ControlSystem
    {
        #region Subsystem Selection

        public void SetTPCurrentSubsystemBools(ushort TPNumber)
        {
            try
            {
                ushort currentRoomNum = manager.touchpanelZ[TPNumber].CurrentRoomNum;
                ushort subsystemNumber = manager.touchpanelZ[TPNumber].CurrentSubsystemNumber;
                if (subsystemNumber == 0 || !manager.SubsystemZ.ContainsKey(subsystemNumber))
                {
                    CrestronConsole.PrintLine("SetCurrentSubsystem: Invalid subsystem number {0} for touchpanel {1}",
                        subsystemNumber, TPNumber);
                    return;
                }

                manager.touchpanelZ[TPNumber].CurrentSubsystemIsLights = false;
                manager.touchpanelZ[TPNumber].CurrentSubsystemIsClimate = false;
                manager.touchpanelZ[TPNumber].CurrentSubsystemIsAudio = false;
                manager.touchpanelZ[TPNumber].CurrentSubsystemIsVideo = false;
                manager.touchpanelZ[TPNumber].CurrentSubsystemIsShades = false;

                for (ushort i = 0; i < manager.SubsystemZ.Count; i++)
                {
                    string subsystemName = manager.SubsystemZ[subsystemNumber].Name.ToUpper();

                    if (subsystemName == "VIDEO")
                    {
                        manager.touchpanelZ[TPNumber].CurrentSubsystemIsVideo = true;
                    }
                    else if (subsystemName == "AUDIO" || subsystemName == "MUSIC")
                    {
                        manager.touchpanelZ[TPNumber].CurrentSubsystemIsAudio = true;
                    }
                    else if (subsystemName == "HVAC" || subsystemName == "CLIMATE")
                    {
                        manager.touchpanelZ[TPNumber].CurrentSubsystemIsClimate = true;
                        manager.touchpanelZ[TPNumber].CurrentClimateID = manager.RoomZ[currentRoomNum].ClimateID;
                    }
                    else if (subsystemName == "LIGHTS" || subsystemName == "LIGHTING")
                    {
                        manager.touchpanelZ[TPNumber].CurrentSubsystemIsLights = true;
                    }
                    else if (subsystemName == "SHADES" || subsystemName == "WINDOWS" || subsystemName == "BLINDS" || subsystemName == "DRAPES")
                    {
                        manager.touchpanelZ[TPNumber].CurrentSubsystemIsShades = true;
                    }

                    break;
                }
            }
            catch (Exception ex)
            {
                ErrorLog.Error("Error in SetCurrentSubsystem: {0}", ex.Message);
            }
        }

        public void SelectSubsystem(ushort TPNumber, ushort subsystemButtonNumber)
        {
            ushort audioIsSystemNumber = 0;
            ushort videoIsSystemNumber = 0;
            ushort currentRoomNum = manager.touchpanelZ[TPNumber].CurrentRoomNum;
            ushort currentSubsystemScenario = manager.RoomZ[currentRoomNum].SubSystemScenario;
            ushort subsystemNumber = 0;
            if (subsystemButtonNumber > 0)
            {
                subsystemButtonNumber--;
                if (manager.touchpanelZ[TPNumber].CurrentPageNumber == 0)
                {
                    ushort homePageScenario = manager.touchpanelZ[TPNumber].HomePageScenario;
                    subsystemNumber = this.config.RoomConfig.WholeHouseSubsystemScenarios[homePageScenario - 1].WholeHouseSubsystems[subsystemButtonNumber].SubsystemNumber;
                    var includedFloors = manager.WholeHouseSubsystemScenarioZ[homePageScenario].WholeHouseSubsystems[subsystemButtonNumber].IncludedFloors;
                    ushort currentFloor = manager.touchpanelZ[TPNumber].CurrentFloorNum;
                    ushort floorButtonNumber = 1;
                    if (includedFloors.Count == 0 || (includedFloors.Count == 1 && includedFloors[0] == 0))
                    {
                        floorButtonNumber = 0;
                    }
                    else if (!includedFloors.Contains(currentFloor) && includedFloors.Count > 0)
                    {
                        manager.touchpanelZ[TPNumber].CurrentFloorNum = includedFloors[0];
                        floorButtonNumber = 1;
                    }
                    else
                    {
                        floorButtonNumber = (ushort)(includedFloors.IndexOf(currentFloor) + 1);
                    }
                    manager.touchpanelZ[TPNumber].CurrentSubsystemNumber = subsystemNumber;

                    if (manager.touchpanelZ[TPNumber].HTML_UI)
                    {
                        if (includedFloors.Count > 1 || (includedFloors.Count == 1 && includedFloors[0] != 0))
                        {
                            CrestronConsole.PrintLine("populate floor list HTML UI");
                            manager.touchpanelZ[TPNumber]._HTMLContract.FloorList.NumberOfFloors(
                                    (sig, wh) => sig.UShortValue = (ushort)includedFloors.Count);
                            CrestronConsole.PrintLine("set number of floors to {0}", (ushort)includedFloors.Count);

                            for (int i = 0; i < includedFloors.Count; i++)
                            {
                                ushort floorNum = includedFloors[i];
                                manager.touchpanelZ[TPNumber]._HTMLContract.FloorSelect[i].FloorName(
                                    (sig, wh) => sig.StringValue = manager.Floorz[floorNum].Name);
                            }
                            CrestronConsole.PrintLine("finished populate floor list HTML UI");
                        }
                        else
                        {
                            CrestronConsole.PrintLine("Skipping floor list - only 1 floor or no floor filter [0]");
                        }
                    }
                    else
                    {
                        if (includedFloors.Count > 1 || (includedFloors.Count == 1 && includedFloors[0] != 0))
                        {
                            manager.touchpanelZ[TPNumber].UserInterface.SmartObjects[3].UShortInput[4].UShortValue = (ushort)includedFloors.Count;
                        }
                    }

                    if (floorButtonNumber > 0)
                    {
                        manager.touchpanelZ[TPNumber].floorButtonFB(floorButtonNumber);
                    }
                    SetTPCurrentSubsystemBools(TPNumber);
                    WholeHouseUpdateZoneList(TPNumber);
                    SendSubsystemZonesPageNumber(TPNumber, false);
                }
                else
                {
                    for (ushort i = 1; i <= manager.SubsystemZ.Count; i++)
                    {
                        if (manager.SubsystemZ[i].Name.ToUpper() == "VIDEO")
                        {
                            videoIsSystemNumber = i;
                            manager.touchpanelZ[TPNumber].CurrentSubsystemIsVideo = true;
                            manager.touchpanelZ[TPNumber].UserInterface.BooleanInput[70].BoolValue = manager.RoomZ[currentRoomNum].LiftGoWithOff;
                            UpdateVideoDisplayList(TPNumber);
                        }
                        else if (manager.SubsystemZ[i].Name.ToUpper() == "AUDIO" || manager.SubsystemZ[i].Name.ToUpper() == "MUSIC")
                        {
                            audioIsSystemNumber = i;
                            manager.touchpanelZ[TPNumber].UnsubscribeTouchpanelFromAllVolMuteChanges();
                            manager.touchpanelZ[TPNumber].UserInterface.UShortInput[2].UShortValue = manager.RoomZ[currentRoomNum].MusicVolume;
                            manager.touchpanelZ[TPNumber].UserInterface.BooleanInput[1009].BoolValue = manager.RoomZ[currentRoomNum].MusicMuted;
                            EventHandler volumeHandler = (sender, e) => manager.touchpanelZ[TPNumber].UserInterface.UShortInput[2].UShortValue = manager.RoomZ[currentRoomNum].MusicVolume;
                            manager.RoomZ[currentRoomNum].MusicVolumeChanged += volumeHandler;
                            manager.touchpanelZ[TPNumber].VolumeChangeHandlers[manager.RoomZ[currentRoomNum]] = volumeHandler;
                        }
                        else if (manager.SubsystemZ[i].Name.ToUpper() == "CLIMATE" || manager.SubsystemZ[i].Name.ToUpper() == "HVAC")
                        {
                            new CTimer(o =>
                            {
                                ushort tpNumber = TPNumber;
                                ushort roomNum = currentRoomNum;

                                if (manager.touchpanelZ.ContainsKey(tpNumber) &&
                                    manager.RoomZ.ContainsKey(roomNum) &&
                                    manager.RoomZ[roomNum].CurrentSetpoint > 0)
                                {
                                    manager.touchpanelZ[tpNumber].UserInterface.UShortInput[101].UShortValue =
                                        manager.RoomZ[roomNum].CurrentSetpoint;

                                    CrestronConsole.PrintLine("Timer updated setpoint for TP-{0} to {1}",
                                        tpNumber, manager.RoomZ[roomNum].CurrentSetpoint);
                                }
                            }, null, 300);
                        }
                    }
                    subsystemNumber = manager.SubsystemScenarioZ[currentSubsystemScenario].IncludedSubsystems[subsystemButtonNumber];
                    manager.RoomZ[currentRoomNum].CurrentSubsystem = subsystemNumber;
                    manager.touchpanelZ[TPNumber].CurrentSubsystemNumber = subsystemNumber;
                    SetTPCurrentSubsystemBools(TPNumber);
                    if (subsystemNumber == videoIsSystemNumber)
                    {
                        manager.RoomZ[currentRoomNum].LastSystemVid = true;
                        imageEISC.BooleanInput[TPNumber].BoolValue = true;
                        imageEISC.BooleanInput[(ushort)(TPNumber + 100)].BoolValue = false;
                        videoSystemControl.UpdateTPVideoMenu(TPNumber);
                        if (manager.RoomZ[currentRoomNum].CurrentVideoSrc == 0)
                        {
                            manager.touchpanelZ[TPNumber].UserInterface.BooleanInput[53].BoolValue = true;
                        }
                    }
                    else if (subsystemNumber == audioIsSystemNumber)
                    {
                        manager.RoomZ[currentRoomNum].LastSystemVid = false;
                        imageEISC.BooleanInput[(ushort)(TPNumber + 100)].BoolValue = true;
                        imageEISC.BooleanInput[TPNumber].BoolValue = false;
                        ushort currentMusicSrc = manager.RoomZ[currentRoomNum].CurrentMusicSrc;
                        if (currentMusicSrc > 0)
                        {
                            manager.touchpanelZ[TPNumber].musicPageFlips(manager.MusicSourceZ[currentMusicSrc].FlipsToPageNumber);
                            musicEISC1.UShortInput[(ushort)(TPNumber + 100)].UShortValue = manager.MusicSourceZ[currentMusicSrc].Number;
                            musicEISC1.UShortInput[(ushort)(TPNumber + 300)].UShortValue = manager.MusicSourceZ[currentMusicSrc].EquipID;
                        }
                        else if (manager.RoomZ[currentRoomNum].CurrentVideoSrc == 0)
                        {
                            manager.touchpanelZ[TPNumber].UserInterface.BooleanInput[55].BoolValue = true;// i think 55 is ASRC sub
                        }
                    }
                    else
                    {
                        imageEISC.BooleanInput[(ushort)(TPNumber + 100)].BoolValue = false;
                        imageEISC.BooleanInput[TPNumber].BoolValue = false;
                        manager.touchpanelZ[TPNumber].CurrentSubsystemIsVideo = false;
                    }
                    manager.touchpanelZ[TPNumber].subsystemPageFlips(manager.SubsystemZ[subsystemNumber].FlipsToPageNumber);
                }

                manager.touchpanelZ[TPNumber].UserInterface.StringInput[4].StringValue = manager.SubsystemZ[subsystemNumber].DisplayName;
                if (manager.SubsystemZ[subsystemNumber].EquipID > 99)
                {
                    subsystemEISC.UShortInput[(ushort)(TPNumber + 200)].UShortValue = (ushort)(manager.SubsystemZ[subsystemNumber].EquipID + TPNumber);
                }
                else
                {
                    subsystemEISC.UShortInput[(ushort)(TPNumber + 200)].UShortValue = (ushort)(manager.SubsystemZ[subsystemNumber].EquipID);
                }
            }
            else { manager.RoomZ[currentRoomNum].CurrentSubsystem = 0; }
        }

        public void SendSubsystemZonesPageNumber(ushort TPNumber, bool close)
        {
            ushort currentSub = manager.touchpanelZ[TPNumber].CurrentSubsystemNumber;
            ushort floorScenario = manager.touchpanelZ[TPNumber].FloorScenario;
            if (new[] { "LIGHTS", "LIGHTING", "CLIMATE", "HVAC", "SHADES", "DRAPES" }
                .Contains(manager.SubsystemZ[currentSub].DisplayName.ToUpper()))
            {
                if (manager.FloorScenarioZ[floorScenario].IncludedFloors.Count > 1)
                {
                    manager.touchpanelZ[TPNumber].subsystemPageFlips(94);
                }
                else { manager.touchpanelZ[TPNumber].subsystemPageFlips(91); }
            }
            else if (close)
            {
                manager.touchpanelZ[TPNumber].subsystemPageFlips(0);
            }
            else
            {
                manager.touchpanelZ[TPNumber].subsystemPageFlips(manager.SubsystemZ[currentSub].FlipsToPageNumber);
            }
        }

        public void SelectSubsystemPage(ushort TPNumber, ushort SubsystemNumber)
        {
            ushort equipID = manager.SubsystemZ[SubsystemNumber].EquipID;
            ushort currentRoomNumber = manager.touchpanelZ[TPNumber].CurrentRoomNum;
            manager.touchpanelZ[TPNumber].CurrentPageNumber = (ushort)(TouchpanelUI.CurrentPageType.SubsystemPage);
            CrestronConsole.PrintLine("select subsystem page {0} currentpageType{1}", TPNumber, manager.touchpanelZ[TPNumber].CurrentPageNumber);
            manager.touchpanelZ[TPNumber].subsystemPageFlips(manager.SubsystemZ[SubsystemNumber].FlipsToPageNumber);
            if (equipID > 99) { equipID = (ushort)(equipID + TPNumber); }
            subsystemEISC.UShortInput[(ushort)(TPNumber + 200)].UShortValue = equipID;
            manager.RoomZ[currentRoomNumber].CurrentSubsystem = SubsystemNumber;
            SetTPCurrentSubsystemBools(TPNumber);
            if (manager.SubsystemZ[SubsystemNumber].Name.ToUpper() == "AUDIO" || manager.SubsystemZ[SubsystemNumber].Name.ToUpper() == "MUSIC")
            {
                imageEISC.BooleanInput[(ushort)(TPNumber + 100)].BoolValue = true;
            }
            else { imageEISC.BooleanInput[(ushort)(TPNumber + 100)].BoolValue = false; }
        }

        #endregion

        #region Subsystem Updates

        public void UpdateSubsystems(ushort TPNumber)
        {
            ushort currentRoomNumber = manager.touchpanelZ[TPNumber].CurrentRoomNum;
            ushort numberOfSubsystems = (ushort)manager.SubsystemScenarioZ[manager.touchpanelZ[TPNumber].SubSystemScenario].IncludedSubsystems.Count;
            ushort currentSubsystemScenario = manager.touchpanelZ[TPNumber].SubSystemScenario;
            ushort flipToSubsysNumOnSelect = manager.RoomZ[currentRoomNumber].OpenSubsysNumOnRmSelect;

            if (currentSubsystemScenario == 0) { currentSubsystemScenario = manager.RoomZ[currentRoomNumber].SubSystemScenario; }
            ushort homepageScenario = manager.touchpanelZ[TPNumber].HomePageScenario;

            manager.touchpanelZ[TPNumber].UserInterface.StringInput[1].StringValue = manager.RoomZ[currentRoomNumber].Name;
            if (manager.touchpanelZ[TPNumber].HTML_UI)
            {
                manager.touchpanelZ[TPNumber]._HTMLContract.SubsystemList.NumberOfSubsystems(
                    (sig, wh) => sig.UShortValue = numberOfSubsystems);
            }
            else
            {
                manager.touchpanelZ[TPNumber].UserInterface.SmartObjects[2].UShortInput[3].UShortValue = numberOfSubsystems;
            }

            if (homepageScenario == 0 || numberOfSubsystems == 1)
            {
                ushort subsystemNum = manager.SubsystemScenarioZ[currentSubsystemScenario].IncludedSubsystems[0];
                SelectSubsystemPage(TPNumber, subsystemNum);
            }
            else
            {
                manager.touchpanelZ[TPNumber].subsystemPageFlips(0);
            }
            updateSubsystemListSmartObject(TPNumber, false);
        }

        public void updateSubsystemListSmartObject(ushort TPNumber, bool wholeHouseYes)
        {
            ushort homePageScenario = manager.touchpanelZ[TPNumber].HomePageScenario;
            ushort currentSubsystemScenario = manager.touchpanelZ[TPNumber].SubSystemScenario;
            ushort numberOfSubs = 0;
            ushort subsystemNum = 0;
            if (wholeHouseYes)
            {
                numberOfSubs = (ushort)this.config.RoomConfig.WholeHouseSubsystemScenarios[homePageScenario - 1].WholeHouseSubsystems.Count;
                if (manager.touchpanelZ[TPNumber].HTML_UI)
                {
                    manager.touchpanelZ[TPNumber]._HTMLContract.WholeHouseSubsystemList.numberOfWholeHouseSubsystems(
                        (sig, wh) => sig.UShortValue = numberOfSubs);
                }
                else
                {
                    manager.touchpanelZ[TPNumber].UserInterface.SmartObjects[14].UShortInput[3].UShortValue = numberOfSubs;
                }

                for (ushort i = 0; i < numberOfSubs; i++)
                {
                    subsystemNum = this.config.RoomConfig.WholeHouseSubsystemScenarios[homePageScenario - 1].WholeHouseSubsystems[i].SubsystemNumber;
                    if (manager.touchpanelZ[TPNumber].HTML_UI)
                    {
                        manager.touchpanelZ[TPNumber]._HTMLContract.WholeHouseSubsystem[i].SubsystemIsSelected(
                            (sig, wh) => sig.BoolValue = false);
                        manager.touchpanelZ[TPNumber]._HTMLContract.WholeHouseSubsystem[i].SubsystemName(
                            (sig, wh) => sig.StringValue = manager.SubsystemZ[subsystemNum].Name);
                        manager.touchpanelZ[TPNumber]._HTMLContract.WholeHouseSubsystem[i].SubsystemIcon(
                            (sig, wh) => sig.StringValue = manager.SubsystemZ[subsystemNum].IconHTML);
                    }
                    else
                    {
                        manager.touchpanelZ[TPNumber].UserInterface.SmartObjects[14].BooleanInput[(ushort)(i + 4016)].BoolValue = false;
                        manager.touchpanelZ[TPNumber].UserInterface.SmartObjects[14].StringInput[(ushort)(2 * i + 11)].StringValue = manager.SubsystemZ[subsystemNum].Name;
                        manager.touchpanelZ[TPNumber].UserInterface.SmartObjects[14].StringInput[(ushort)(2 * i + 12)].StringValue = manager.SubsystemZ[subsystemNum].IconSerial;
                    }
                }
            }
            else
            {
                numberOfSubs = (ushort)manager.SubsystemScenarioZ[currentSubsystemScenario].IncludedSubsystems.Count;
                ushort currentRoomNumber = manager.touchpanelZ[TPNumber].CurrentRoomNum;
                for (ushort i = 0; i < numberOfSubs; i++)
                {
                    subsystemNum = manager.SubsystemScenarioZ[currentSubsystemScenario].IncludedSubsystems[i];
                    if (manager.touchpanelZ[TPNumber].HTML_UI)
                    {
                        manager.touchpanelZ[TPNumber]._HTMLContract.SubsystemButton[i].SubsystemSelected(
                            (sig, wh) => sig.BoolValue = false);
                        manager.touchpanelZ[TPNumber]._HTMLContract.SubsystemButton[i].SubsystemName(
                            (sig, wh) => sig.StringValue = manager.SubsystemZ[subsystemNum].Name);
                        manager.touchpanelZ[TPNumber]._HTMLContract.SubsystemButton[i].SubsystemIcon(
                            (sig, wh) => sig.StringValue = manager.SubsystemZ[subsystemNum].IconHTML);
                        if (manager.RoomZ[currentRoomNumber].CurrentSubsystem == subsystemNum)
                        {
                            manager.touchpanelZ[TPNumber]._HTMLContract.SubsystemButton[i].SubsystemSelected(
                                (sig, wh) => sig.BoolValue = true);
                        }
                    }
                    else
                    {
                        manager.touchpanelZ[TPNumber].UserInterface.SmartObjects[2].BooleanInput[(ushort)(i + 4016)].BoolValue = false;
                        manager.touchpanelZ[TPNumber].UserInterface.SmartObjects[2].StringInput[(ushort)(3 * i + 11)].StringValue = manager.SubsystemZ[subsystemNum].Name;
                        manager.touchpanelZ[TPNumber].UserInterface.SmartObjects[2].StringInput[(ushort)(3 * i + 13)].StringValue = manager.SubsystemZ[subsystemNum].IconSerial;
                        if (manager.RoomZ[currentRoomNumber].CurrentSubsystem == subsystemNum)
                        {
                            manager.touchpanelZ[TPNumber].UserInterface.SmartObjects[2].BooleanInput[(ushort)(i + 4016)].BoolValue = true;
                        }
                    }
                }
            }
        }

        public void UpdatePanelSubsystemText(ushort TPNumber)
        {
            ushort roomNumber = manager.touchpanelZ[TPNumber].CurrentRoomNum;
            ushort subsystemScenario = manager.RoomZ[roomNumber].SubSystemScenario;
            ushort numSubsystems = (ushort)manager.SubsystemScenarioZ[subsystemScenario].IncludedSubsystems.Count;
            string statusText = "";
            string subName = "";
            for (ushort i = 0; i < numSubsystems; i++)
            {
                subName = manager.SubsystemZ[manager.SubsystemScenarioZ[subsystemScenario].IncludedSubsystems[i]].Name;

                if (subName.ToUpper().Contains("LIGHTS") || subName.ToUpper().Contains("LIGHTING"))
                {
                    if (manager.RoomZ[roomNumber].Name.ToUpper() == "GLOBAL")
                    {
                        statusText = "";
                    }
                    else if (manager.RoomZ[roomNumber].LightsAreOff)
                    {
                        statusText = "Lights are off. ";
                    }
                    else { statusText = "Lights are on. "; }
                }
                else if (subName.ToUpper().Contains("MUSIC") || subName.ToUpper().Contains("AUDIO"))
                {
                    ushort currentASRC = manager.RoomZ[roomNumber].CurrentMusicSrc;
                    if (currentASRC > 0)
                    {
                        statusText = manager.MusicSourceZ[currentASRC].Name + " is playing. ";
                    }
                    else { statusText = "Off"; }
                }
                else if (subName.ToUpper().Contains("VIDEO") || subName.ToUpper().Contains("WATCH") || subName.ToUpper().Contains("TV"))
                {
                    ushort currentVSRC = manager.RoomZ[roomNumber].CurrentVideoSrc;
                }
                else if (subName.ToUpper().Contains("CLIMATE") || subName.ToUpper().Contains("HVAC"))
                {
                }
                else
                {
                    statusText = "";
                }
                if (manager.touchpanelZ[TPNumber].HTML_UI)
                {
                    manager.touchpanelZ[TPNumber]._HTMLContract.SubsystemButton[i].SubsystemStatus(
                        (sig, wh) => sig.StringValue = statusText);
                }
                else
                {
                    manager.touchpanelZ[TPNumber].UserInterface.SmartObjects[2].StringInput[(ushort)(3 * i + 12)].StringValue = statusText;
                }
            }
        }

        public void UpdateEquipIDsForSubsystems(ushort TPNumber, ushort currentRoomNumber)
        {
            subsystemEISC.UShortInput[(ushort)((TPNumber - 1) * 10 + 301)].UShortValue = manager.RoomZ[currentRoomNumber].AudioID;
            subsystemEISC.UShortInput[(ushort)((TPNumber - 1) * 10 + 302)].UShortValue = manager.RoomZ[currentRoomNumber].VideoOutputNum;
            subsystemEISC.UShortInput[(ushort)((TPNumber - 1) * 10 + 303)].UShortValue = manager.RoomZ[currentRoomNumber].LightsID;
            subsystemEISC.UShortInput[(ushort)((TPNumber - 1) * 10 + 304)].UShortValue = manager.RoomZ[currentRoomNumber].ShadesID;
            subsystemEISC.UShortInput[(ushort)((TPNumber - 1) * 10 + 306)].UShortValue = manager.RoomZ[currentRoomNumber].MiscID;
        }

        public void UpdateVideoDisplayList(ushort TPNumber)
        {
            ushort currentRoomNum = manager.touchpanelZ[TPNumber].CurrentRoomNum;
            ushort numDisplays = (ushort)manager.RoomZ[currentRoomNum].ListOfDisplays.Count;
            if (manager.touchpanelZ[TPNumber].HTML_UI)
            {
            }
            else
            {
                manager.touchpanelZ[TPNumber].UserInterface.SmartObjects[34].UShortInput[4].UShortValue = numDisplays;
                for (ushort i = 0; i < numDisplays; i++)
                {
                    manager.touchpanelZ[TPNumber].UserInterface.SmartObjects[34].StringInput[(ushort)(i + 11)].StringValue = manager.VideoDisplayZ[manager.RoomZ[currentRoomNum].ListOfDisplays[i]].DisplayName;
                }
            }
        }

        #endregion

        #region Helper Methods

        public bool isThisSubsystemInQuickActionList(string subsystemName)
        {
            bool subsysIsHere = false;
            ushort subsysNumber = 0;
            foreach (var subsys in manager.SubsystemZ)
            {
                if (subsystemName.ToUpper() == subsys.Value.Name.ToUpper())
                {
                    subsysNumber = subsys.Value.Number;
                }
            }
            for (ushort i = 0; i < quickActionXML.NumberOfIncludedSubsystems[quickActionXML.quickActionToRecallOrSave - 1]; i++)
            {
                ushort subnum = quickActionXML.IncludedSubsystems[quickActionXML.quickActionToRecallOrSave - 1, i];
                if (subnum == subsysNumber)
                {
                    subsysIsHere = true;
                }
            }
            if (!subsysIsHere) { CrestronConsole.PrintLine("{0} is not included in this quick action", subsystemName); }
            return subsysIsHere;
        }

        public ushort GetWholeHouseSubsystemIndex(ushort TPNumber)
        {
            ushort index = 0;
            ushort wholeHouseScenarioNum = manager.touchpanelZ[TPNumber].HomePageScenario;
            ushort subsystemNumber = manager.touchpanelZ[TPNumber].CurrentSubsystemNumber;
            ushort numSubsystems = (ushort)this.config.RoomConfig.WholeHouseSubsystemScenarios[wholeHouseScenarioNum - 1].WholeHouseSubsystems.Count;
            for (ushort i = 0; i < numSubsystems; i++)
            {
                if (this.config.RoomConfig.WholeHouseSubsystemScenarios[wholeHouseScenarioNum - 1].WholeHouseSubsystems[i].SubsystemNumber == subsystemNumber)
                { index = i; }
            }

            return index;
        }

        public ushort TranslateButtonNumberToASrc(ushort TPNumber, ushort sourceButtonNumber)
        {
            ushort adjustedButtonNum = sourceButtonNumber;
            ushort currentASRCscenario = manager.RoomZ[manager.touchpanelZ[TPNumber].CurrentRoomNum].AudioSrcScenario;
            ushort srcGroup = manager.touchpanelZ[TPNumber].CurrentASrcGroupNum;
            ushort currentASRC = 0;
            if (srcGroup > 0)
            {
                adjustedButtonNum = (ushort)(sourceButtonNumber + (srcGroup - 1) * 6 - 1);
            }
            if (sourceButtonNumber > 0)
            {
                currentASRC = manager.AudioSrcScenarioZ[currentASRCscenario].IncludedSources[(ushort)(adjustedButtonNum)];
            }
            return currentASRC;
        }

        public bool AreAllDisplaysOffInThisRoom(ushort roomNumber)
        {
            bool allDisplaysAreOff = true;

            for (ushort i = 0; i < manager.RoomZ[roomNumber].NumberOfDisplays; i++)
            {
                ushort displayNum = manager.RoomZ[roomNumber].ListOfDisplays[i];
                if (manager.VideoDisplayZ[displayNum].CurrentVideoSrc > 0)
                {
                    CrestronConsole.PrintLine("{0} is on", manager.VideoDisplayZ[displayNum].DisplayName);
                    allDisplaysAreOff = false;
                }
            }

            return allDisplaysAreOff;
        }

        public string GetVideoSourceStatus(ushort roomNumber)
        {
            string statusText = "Off";

            foreach (var display in manager.VideoDisplayZ)
            {
                if (display.Value.AssignedToRoomNum == roomNumber)
                {
                    if (display.Value.CurrentVideoSrc > 0)
                    { statusText = display.Value.CurrentSourceText + " is on. "; }
                }
            }
            return statusText;
        }

        public string BuildHTMLString(ushort TPNumber, string input, string fontSize)
        {
            string htmlString = "";
            if (manager.touchpanelZ[TPNumber].Name.ToUpper().Contains("IPHONE"))
            {
                htmlString = string.Format(@"<FONT size=""{0}"">{1}</FONT>", fontSize, input);
            }
            else
            {
                htmlString = input;
            }
            return htmlString;
        }

        public void UpdateLightingStatus(ushort KeypadNumber, bool LightsAreOff)
        {
            foreach (var room in manager.RoomZ)
            {
                if (room.Value.LightsID == KeypadNumber)
                {
                    room.Value.LightsAreOff = LightsAreOff;
                    CrestronConsole.PrintLine("room {0} lightsid {1} lightsareoff {2}", room.Value.Name, room.Value.LightsID, room.Value.LightsAreOff);
                }
            }
        }

        public void UpdateRoomOptions(ushort TPNumber)
        {
            ushort currentLiftScenario, currentSleepScenario, currentFormatScenario, numLiftButtons, numSleepButtons, numFormatButtons, currentRoomNumber;
            currentRoomNumber = manager.touchpanelZ[TPNumber].CurrentRoomNum;
            currentLiftScenario = manager.RoomZ[currentRoomNumber].LiftScenario;
            currentSleepScenario = manager.RoomZ[currentRoomNumber].SleepScenario;
            currentFormatScenario = manager.RoomZ[currentRoomNumber].FormatScenario;
            videoEISC3.UShortInput[(ushort)(TPNumber)].UShortValue = currentLiftScenario;
            videoEISC3.UShortInput[(ushort)(TPNumber + 100)].UShortValue = currentSleepScenario;
            videoEISC3.UShortInput[(ushort)(TPNumber + 200)].UShortValue = currentFormatScenario;

            if (currentSleepScenario > 0)
            {
                videoEISC2.UShortInput[(ushort)(TPNumber + 200)].UShortValue = (ushort)manager.SleepScenarioZ[currentSleepScenario].SleepCmds.Count;
            }
            if (currentLiftScenario > 0)
            {
                videoEISC2.UShortInput[(ushort)(TPNumber + 300)].UShortValue = (ushort)manager.LiftScenarioZ[currentLiftScenario].LiftCmds.Count;
            }
            if (currentFormatScenario > 0)
            {
                videoEISC2.UShortInput[(ushort)(TPNumber + 400)].UShortValue = (ushort)manager.FormatScenarioZ[currentFormatScenario].FormatCmds.Count;
            }
            if (currentLiftScenario > 0)
            {
                videoEISC3.StringInput[(ushort)(TPNumber)].StringValue = manager.LiftScenarioZ[(ushort)(currentLiftScenario)].ButtonLabel;
                numLiftButtons = (ushort)manager.LiftScenarioZ[currentLiftScenario].LiftCmds.Count;

                for (ushort i = 0; i < numLiftButtons; i++)
                {
                    videoEISC3.StringInput[(ushort)((TPNumber - 1) * 5 + i + 301)].StringValue = manager.LiftCmdZ[manager.LiftScenarioZ[currentLiftScenario].LiftCmds[i]].Name;
                }
            }
            if (currentSleepScenario > 0)
            {
                videoEISC3.StringInput[(ushort)(TPNumber + 100)].StringValue = manager.SleepScenarioZ[(ushort)(currentSleepScenario)].ButtonLabel;
                numSleepButtons = (ushort)manager.SleepScenarioZ[currentSleepScenario].SleepCmds.Count;
                for (ushort i = 0; i < numSleepButtons; i++)
                {
                    videoEISC3.StringInput[(ushort)((TPNumber - 1) * 5 + i + 801)].StringValue = manager.SleepCmdZ[manager.SleepScenarioZ[currentSleepScenario].SleepCmds[i]].Name;
                }
            }
            if (currentFormatScenario > 0)
            {
                videoEISC3.StringInput[(ushort)(TPNumber + 200)].StringValue = manager.FormatScenarioZ[(ushort)(currentFormatScenario)].ButtonLabel;
                numFormatButtons = (ushort)manager.FormatScenarioZ[currentFormatScenario].FormatCmds.Count;
                for (ushort i = 0; i < numFormatButtons; i++)
                {
                    videoEISC3.StringInput[(ushort)((TPNumber - 1) * 10 + i + 1301)].StringValue = manager.FormatCmdZ[manager.FormatScenarioZ[currentFormatScenario].FormatCmds[i]].Name;
                }
            }
        }

        #endregion
    }
}
