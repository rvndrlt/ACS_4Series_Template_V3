using System;
using System.Linq;
using Crestron.SimplSharp;
using ACS_4Series_Template_V3.UI;

namespace ACS_4Series_Template_V3
{
    public partial class ControlSystem
    {
        private CTimer _climateSetpointDelayTimer;

        /// <summary>First "Item N Selected" digital on the TSR Dynamic Button List (smart object 2).
        /// The SGD cue for "Item 1 Selected" is 12, but the list's [~BeginGroup~]/[~EndGroup~] markers
        /// are not exposed as sigs, so every cue after a marker shifts down by one at runtime — the
        /// same reason the item text uses 11 (cue 12, one marker ahead) and the item icon uses 2011
        /// (cue 2014, three markers ahead).</summary>
        private const ushort TsrSubsystemSelectedJoinBase = 11;


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

        /// <summary>Refresh ONLY the "selected" highlight in the room subsystem list. The full
        /// updateSubsystemListSmartObject() rewrites every name/icon and is only run on room-level
        /// updates, so selecting a subsystem used to leave the highlight wherever it was last drawn.</summary>
        public void UpdateSubsystemListSelectedFeedback(ushort TPNumber)
        {
            ushort currentRoomNumber = manager.touchpanelZ[TPNumber].CurrentRoomNum;
            ushort selected = manager.RoomZ.ContainsKey(currentRoomNumber)
                ? manager.RoomZ[currentRoomNumber].CurrentSubsystem
                : (ushort)0;
            UpdateSubsystemListSelectedFeedback(TPNumber, selected);
        }

        /// <summary>As above, but for an explicit subsystem number. Pass 0 to clear the highlight
        /// (Home) without disturbing the room's stored CurrentSubsystem.</summary>
        public void UpdateSubsystemListSelectedFeedback(ushort TPNumber, ushort selectedSubsystemNumber)
        {
            try
            {
                ushort currentSubsystemScenario = ResolveSubsystemScenario(TPNumber);
                if (currentSubsystemScenario == 0) { return; }

                var included = manager.SubsystemScenarioZ[currentSubsystemScenario].IncludedSubsystems;
                bool isHtml = manager.touchpanelZ[TPNumber].HTML_UI;
                bool isTsr = manager.touchpanelZ[TPNumber].Type.ToUpper().Contains("TSR");

                for (ushort i = 0; i < included.Count; i++)
                {
                    bool on = included[i] == selectedSubsystemNumber && selectedSubsystemNumber > 0;
                    if (isHtml)
                    {
                        manager.touchpanelZ[TPNumber]._HTMLContract.SubsystemButton[i].SubsystemSelected(
                            (sig, wh) => sig.BoolValue = on);
                    }
                    else if (isTsr)
                    {
                        manager.touchpanelZ[TPNumber].UserInterface.SmartObjects[2]
                            .BooleanInput[(ushort)(i + TsrSubsystemSelectedJoinBase)].BoolValue = on;
                    }
                    else
                    {
                        manager.touchpanelZ[TPNumber].UserInterface.SmartObjects[2]
                            .BooleanInput[(ushort)(i + 4016)].BoolValue = on;
                    }
                }
            }
            catch (Exception ex)
            {
                ErrorLog.Error("Error in UpdateSubsystemListSelectedFeedback: {0}", ex.Message);
            }
        }

        public void SelectSubsystem(ushort TPNumber, ushort subsystemButtonNumber)
        {
            CrestronConsole.PrintLine("SelectSubsystem called for TP-{0} subsystemButtonNumber {1}", TPNumber, subsystemButtonNumber);
            manager.touchpanelZ[TPNumber].ResetIdleTimer();   // subsystem selection counts as activity
            ushort currentRoomNum = manager.touchpanelZ[TPNumber].CurrentRoomNum;
            ushort currentSubsystemScenario = manager.RoomZ[currentRoomNum].SubSystemScenario;
            ushort subsystemNumber = 0;
            if (subsystemButtonNumber > 0)
            {
                subsystemButtonNumber--;
                if (manager.touchpanelZ[TPNumber].CurrentPageNumber == 0 && !manager.touchpanelZ[TPNumber].Type.ToUpper().Contains("TSR"))
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
                    string subName = manager.SubsystemZ[subsystemNumber].Name.ToUpper();
                    if ((subName == "AUDIO" || subName == "MUSIC") && manager.touchpanelZ[TPNumber].HTML_UI)
                    {
                        if (musicSystemControl.ActiveMusicRoomsList.Count == 0)
                        {
                            // No zones playing — initiate music flow (source picker → room picker → S2)
                            OpenInitiateMusicSourceMenu(TPNumber);
                        }
                        else
                        {
                            // Zones already playing — open homeMusicControlScenario2 directly
                            manager.touchpanelZ[TPNumber].UserInterface.BooleanInput[20].BoolValue = false;
                            manager.touchpanelZ[TPNumber].UserInterface.BooleanInput[21].BoolValue = true;
                            manager.touchpanelZ[TPNumber].SendMenuCommand(TouchpanelUI.MenuHomeMusicControl, true);
                        }
                    }
                    else
                    {
                        SetTPCurrentSubsystemBools(TPNumber);
                        WholeHouseUpdateZoneList(TPNumber);
                        SendSubsystemZonesPageNumber(TPNumber, false);
                    }
                }
                else
                {
                    for (ushort i = 1; i <= manager.SubsystemZ.Count; i++)
                    {
                        if (manager.SubsystemZ[i].Name.ToUpper() == "VIDEO")
                        {
                            manager.touchpanelZ[TPNumber].CurrentSubsystemIsVideo = true;
                            manager.touchpanelZ[TPNumber].UserInterface.BooleanInput[70].BoolValue = manager.RoomZ[currentRoomNum].LiftGoWithOff;
                            UpdateVideoDisplayList(TPNumber);
                        }
                        else if (manager.SubsystemZ[i].Name.ToUpper() == "AUDIO" || manager.SubsystemZ[i].Name.ToUpper() == "MUSIC")
                        {
                            manager.touchpanelZ[TPNumber].UnsubscribeTouchpanelFromAllVolMuteChanges();
                            manager.touchpanelZ[TPNumber].UserInterface.UShortInput[2].UShortValue = manager.RoomZ[currentRoomNum].MusicVolume;
                            manager.touchpanelZ[TPNumber].UserInterface.BooleanInput[1009].BoolValue = manager.RoomZ[currentRoomNum].MusicMuted;
                            EventHandler volumeHandler = (sender, e) => manager.touchpanelZ[TPNumber].UserInterface.UShortInput[2].UShortValue = manager.RoomZ[currentRoomNum].MusicVolume;
                            manager.RoomZ[currentRoomNum].MusicVolumeChanged += volumeHandler;
                            manager.touchpanelZ[TPNumber].VolumeChangeHandlers[manager.RoomZ[currentRoomNum]] = volumeHandler;
                        }
                        else if (manager.SubsystemZ[i].Name.ToUpper() == "CLIMATE" || manager.SubsystemZ[i].Name.ToUpper() == "HVAC")
                        {
                            if (_climateSetpointDelayTimer != null)
                            {
                                _climateSetpointDelayTimer.Stop();
                                _climateSetpointDelayTimer.Dispose();
                            }
                            _climateSetpointDelayTimer = new CTimer(o =>
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
                    SetTPCurrentSubsystemBools(TPNumber);//from select subsystem
                    UpdateSubsystemListSelectedFeedback(TPNumber);//highlight the button that was just pressed
                    // Entering the Climate page must re-attach this panel's climate feedback
                    // handlers (scope "climate": HVACStatusChanged -> UpdateClimateUI, and
                    // CurrentSetpointChanged -> analog 101). Home calls
                    // ReleaseTransientSubscriptions(), which clears that scope, and the ONLY
                    // callers of SyncPanelToClimateZone are room-selection paths. Without this
                    // line, Home -> Climate leaves the setpoint analog fed by nothing but the
                    // 300ms _climateSetpointDelayTimer one-shot below: one correct value, then
                    // frozen forever while the model keeps updating. Most visible on a TSR-310,
                    // whose Home lands on the room-subsystem list, so the room step that used to
                    // re-subscribe as a side effect is skipped entirely.
                    // SubscribeToClimateEvents opens with ClearScope("climate"), so re-entering
                    // the page repeatedly does not stack handlers.
                    if (manager.touchpanelZ[TPNumber].CurrentSubsystemIsClimate)
                    {
                        climateControl.SyncPanelToClimateZone(TPNumber);
                    }
                    // Same gap on the shades side: SmartObject 19's item count, names and button
                    // feedback are written ONLY from subysystemControl_SigChange, i.e. only when a
                    // live EISC change arrives WHILE CurrentSubsystemIsShades is true. An EISC
                    // raises SigChange on change, so a count that SIMPL already drove (at startup,
                    // or while the panel was on another subsystem) never re-fires on entry and the
                    // Subpage Reference List renders zero items — a blank shades page. Pull the
                    // retained values out of the EISC outputs instead of waiting for a change.
                    if (manager.touchpanelZ[TPNumber].CurrentSubsystemIsShades)
                    {
                        // Two back ends, same pull-on-entry need. ShadesScenario2 (0xB4,
                        // Lighting4Series) owns this site; SyncPanelToShades covers legacy
                        // SIMPL-bridge sites on the subsystem control EISC.
                        if (shadesScenario2Control != null && shadesScenario2Control.IsConfigured
                            && manager.touchpanelZ[TPNumber].TSR310 != null)
                        {
                            shadesScenario2Control.PushShadesToSmartObject(TPNumber);
                        }
                        else
                        {
                            SyncPanelToShades(TPNumber);
                        }
                    }
                    // Dispatch on the selected subsystem's NAME, taken directly from the button
                    // that was pressed. (Previously this compared subsystemNumber to a local
                    // videoIsSystemNumber/audioIsSystemNumber that was only assigned inside the
                    // discovery loop above; on any path where that loop didn't set it, the value
                    // stayed 0 and the video population — UpdateTPVideoMenu — was silently skipped.)
                    string selectedSubsystemName = manager.SubsystemZ[subsystemNumber].Name.ToUpper();
                    if (selectedSubsystemName == "VIDEO")
                    {
                        manager.RoomZ[currentRoomNum].LastSystemVid = true;
                        //the current subsystem is video bool lets the video EQUIPID pass / it also enables the video module for volume
                        imageEISC.BooleanInput[TPNumber].BoolValue = true;//current subsystem is video
                        imageEISC.BooleanInput[(ushort)(TPNumber + 100)].BoolValue = false;
                        videoSystemControl.UpdateTPVideoMenu(TPNumber);
                        if (manager.RoomZ[currentRoomNum].CurrentVideoSrc == 0)
                        {
                            manager.touchpanelZ[TPNumber].UserInterface.BooleanInput[53].BoolValue = true;
                        }
                    }
                    else if (selectedSubsystemName == "AUDIO" || selectedSubsystemName == "MUSIC")
                    {
                        manager.RoomZ[currentRoomNum].LastSystemVid = false;
                        //the current subsystem is audio bool lets the audio EQUIPID pass
                        imageEISC.BooleanInput[(ushort)(TPNumber + 100)].BoolValue = true;//current subsystem is audio
                        imageEISC.BooleanInput[TPNumber].BoolValue = false;//current subsystem is NOT video

                        ushort currentMusicSrc = manager.RoomZ[currentRoomNum].CurrentMusicSrc;
                        if (currentMusicSrc > 0)
                        {
                            manager.touchpanelZ[TPNumber].musicPageFlips(manager.MusicSourceZ[currentMusicSrc].FlipsToPageNumber, currentMusicSrc);//from select subsystem
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
                        imageEISC.BooleanInput[(ushort)(TPNumber + 100)].BoolValue = false;//current subsystem is NOT audio
                        imageEISC.BooleanInput[TPNumber].BoolValue = false;//current subsystem is NOT video
                        manager.touchpanelZ[TPNumber].CurrentSubsystemIsVideo = false;
                    }
                    CrestronConsole.PrintLine(
                        "TP-{0} SelectSubsystem resolved subsystemNumber={1} name={2} flipsToPage={3} guiScenario={4} room={5}",
                        TPNumber,
                        subsystemNumber,
                        manager.SubsystemZ[subsystemNumber].Name,
                        manager.SubsystemZ[subsystemNumber].FlipsToPageNumber,
                        manager.SubsystemZ[subsystemNumber].GuiScenarioNumber,
                        currentRoomNum);

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
            else
            {
                manager.RoomZ[currentRoomNum].CurrentSubsystem = 0;
                UpdateSubsystemListSelectedFeedback(TPNumber, 0);//nothing selected - clear the list highlight
            }
        }

        public void SendSubsystemZonesPageNumber(ushort TPNumber, bool close)
        {
            ushort currentSub = manager.touchpanelZ[TPNumber].CurrentSubsystemNumber;
            if (new[] { "LIGHTS", "LIGHTING", "CLIMATE", "HVAC", "SHADES", "DRAPES" }
                .Contains(manager.SubsystemZ[currentSub].DisplayName.ToUpper()))
            {
                int floorCount = 0;
                ushort homePageScenario = manager.touchpanelZ[TPNumber].HomePageScenario;
                if (homePageScenario > 0 && manager.WholeHouseSubsystemScenarioZ.ContainsKey(homePageScenario))
                {
                    ushort whIdx = GetWholeHouseSubsystemIndex(TPNumber);
                    var includedFloors = manager.WholeHouseSubsystemScenarioZ[homePageScenario].WholeHouseSubsystems[whIdx].IncludedFloors;
                    if (includedFloors.Count == 1 && includedFloors[0] == 0)
                        floorCount = 0;
                    else
                        floorCount = includedFloors.Count;
                }
                else
                {
                    ushort floorScenario = manager.touchpanelZ[TPNumber].FloorScenario;
                    floorCount = manager.FloorScenarioZ[floorScenario].IncludedFloors.Count;
                }

                if (floorCount > 1)
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
            // Keep CurrentSubsystemNumber in sync BEFORE the page flip. The auto-flip path
            // (UpdateSubsystems / SelectZone) previously left this value stale from the prior
            // room, so both subsystemPageFlips() and SetTPCurrentSubsystemBools() below read the
            // wrong subsystem (e.g. Shades) and opened a page the new room doesn't even have.
            manager.touchpanelZ[TPNumber].CurrentSubsystemNumber = SubsystemNumber;
            CrestronConsole.PrintLine("select subsystem page {0} currentpageType{1}", TPNumber, manager.touchpanelZ[TPNumber].CurrentPageNumber);
            // Pass the subsystem explicitly so the flip renders THIS subsystem, not whatever
            // happened to be selected last.
            manager.touchpanelZ[TPNumber].subsystemPageFlips(manager.SubsystemZ[SubsystemNumber].FlipsToPageNumber, SubsystemNumber);
            if (equipID > 99) { equipID = (ushort)(equipID + TPNumber); }
            subsystemEISC.UShortInput[(ushort)(TPNumber + 200)].UShortValue = equipID;
            manager.RoomZ[currentRoomNumber].CurrentSubsystem = SubsystemNumber;
            SetTPCurrentSubsystemBools(TPNumber);//from select subsystem page
            UpdateSubsystemListSelectedFeedback(TPNumber);//keep the list highlight on the page we just opened
            if (manager.SubsystemZ[SubsystemNumber].Name.ToUpper() == "AUDIO" || manager.SubsystemZ[SubsystemNumber].Name.ToUpper() == "MUSIC")
            {
                imageEISC.BooleanInput[(ushort)(TPNumber + 100)].BoolValue = true;
            }
            else { imageEISC.BooleanInput[(ushort)(TPNumber + 100)].BoolValue = false; }
        }

        // ─── Physical hard keys (Power / Lights) ────────────────────────────
        // Serial C#→HTML: tells powerOff.js which power-off dialog to show. Carries a seq so a
        // repeated press always changes the value (a serial subscribe fires only on change).
        public const ushort PowerOffDialogJoin = 1522;
        private int powerOffDialogSeq;

        /// <summary>
        /// The subsystem scenario a panel's room menu should be built from, or 0 if there is
        /// none usable. The panel's live SubSystemScenario wins; 0 falls back to the selected
        /// room's. A number matching no scenario returns 0 rather than letting callers throw
        /// KeyNotFoundException on SubsystemScenarioZ — that dictionary is keyed by the scenario
        /// numbers present in the config, so it has no key 0 and none for a typo.
        /// </summary>
        private ushort ResolveSubsystemScenario(ushort tpNumber)
        {
            var tp = manager.touchpanelZ[tpNumber];
            ushort scenario = tp.SubSystemScenario;
            if (scenario == 0 && manager.RoomZ.ContainsKey(tp.CurrentRoomNum))
                scenario = manager.RoomZ[tp.CurrentRoomNum].SubSystemScenario;
            return manager.SubsystemScenarioZ.ContainsKey(scenario) ? scenario : (ushort)0;
        }

        /// <summary>Find the subsystem NUMBER for a category (by name) within the panel's current
        /// room subsystem scenario. Returns 0 if the room's menu has no such subsystem.</summary>
        private ushort FindRoomSubsystemNumber(ushort tpNumber, params string[] upperNames)
        {
            ushort scenario = ResolveSubsystemScenario(tpNumber);
            if (scenario == 0) return 0;
            foreach (ushort num in manager.SubsystemScenarioZ[scenario].IncludedSubsystems)
            {
                if (!manager.SubsystemZ.ContainsKey(num)) continue;
                string n = manager.SubsystemZ[num].Name.ToUpper();
                foreach (var want in upperNames) { if (n == want) return num; }
            }
            return 0;
        }

        /// <summary>Physical Lights hard key: flip the panel to the current room's lighting page.</summary>
        public void HardKeyLights(ushort tpNumber)
        {
            if (!manager.touchpanelZ.ContainsKey(tpNumber)) return;
            manager.touchpanelZ[tpNumber].ResetIdleTimer();
            ushort num = FindRoomSubsystemNumber(tpNumber, "LIGHTS", "LIGHTING");
            if (num > 0) { SelectSubsystemPage(tpNumber, num); }
            else { CrestronConsole.PrintLine("HardKeyLights: no lighting subsystem for TP-{0} current room", tpNumber); }
        }

        /// <summary>
        /// Physical Power hard key. If the panel is already viewing the Video or Audio menu, show
        /// that menu's power-off dialog. Otherwise flip to whichever subsystem is ON in the current
        /// room — VIDEO priority — and show its power-off dialog. On = a source is selected
        /// (CurrentVideoSrc / CurrentMusicSrc). Neither on → do nothing.
        /// </summary>
        public void HardKeyPower(ushort tpNumber)
        {
            if (!manager.touchpanelZ.ContainsKey(tpNumber)) return;
            var tp = manager.touchpanelZ[tpNumber];
            tp.ResetIdleTimer();

            // Current menu wins.
            if (tp.CurrentSubsystemIsVideo) { ShowPowerOffDialog(tpNumber, "video"); return; }
            if (tp.CurrentSubsystemIsAudio) { ShowPowerOffDialog(tpNumber, "audio"); return; }

            // Neither menu open: flip to whichever is on in the room (video priority) + its dialog.
            if (!manager.RoomZ.ContainsKey(tp.CurrentRoomNum)) return;
            var room = manager.RoomZ[tp.CurrentRoomNum];
            if (room.CurrentVideoSrc > 0)
            {
                ushort num = FindRoomSubsystemNumber(tpNumber, "VIDEO");
                if (num > 0) { SelectSubsystemPage(tpNumber, num); }
                ShowPowerOffDialog(tpNumber, "video");
            }
            else if (room.CurrentMusicSrc > 0)
            {
                ushort num = FindRoomSubsystemNumber(tpNumber, "AUDIO", "MUSIC");
                if (num > 0) { SelectSubsystemPage(tpNumber, num); }
                ShowPowerOffDialog(tpNumber, "audio");
            }
            // else: neither on -> nothing
        }

        /// <summary>Trigger the HTML power-off dialog ("video" or "audio") on the panel via join
        /// 1522. seq makes each write distinct so repeated presses re-fire the panel's subscribe.</summary>
        private void ShowPowerOffDialog(ushort tpNumber, string which)
        {
            var tp = manager.touchpanelZ[tpNumber];
            if (!tp.HTML_UI || tp.UserInterface == null) return;
            string json = "{\"seq\":" + (++powerOffDialogSeq) + ",\"dialog\":\"" + which + "\"}";
            try { tp.UserInterface.StringInput[PowerOffDialogJoin].StringValue = json; }
            catch (Exception ex) { ErrorLog.Error("HardKey power-off dialog TP-{0} error: {1}", tpNumber, ex.Message); }
        }

        #endregion

        #region Subsystem Updates

        public void UpdateSubsystems(ushort TPNumber)
        {
            ushort currentRoomNumber = manager.touchpanelZ[TPNumber].CurrentRoomNum;
            ushort flipToSubsysNumOnSelect = manager.RoomZ[currentRoomNumber].OpenSubsysNumOnRmSelect;

            // Resolve BEFORE indexing SubsystemScenarioZ. This used to index the dictionary with
            // the panel's raw value on the first line of the method and only fall back to the
            // room's afterwards, so an unusable number threw KeyNotFoundException and aborted the
            // whole panel update — the symptom being a subsystem list that never drew.
            ushort currentSubsystemScenario = ResolveSubsystemScenario(TPNumber);
            if (currentSubsystemScenario == 0)
            {
                CrestronConsole.PrintLine("UpdateSubsystems: TP-{0} room {1} has no usable subSystemScenario (panel {2}, room {3}) - skipping",
                    TPNumber, currentRoomNumber, manager.touchpanelZ[TPNumber].SubSystemScenario,
                    manager.RoomZ[currentRoomNumber].SubSystemScenario);
                return;
            }
            ushort numberOfSubsystems = (ushort)manager.SubsystemScenarioZ[currentSubsystemScenario].IncludedSubsystems.Count;
            ushort homepageScenario = manager.touchpanelZ[TPNumber].HomePageScenario;

            manager.touchpanelZ[TPNumber].UserInterface.StringInput[1].StringValue = manager.RoomZ[currentRoomNumber].Name;
            if (manager.touchpanelZ[TPNumber].HTML_UI)
            {
                manager.touchpanelZ[TPNumber]._HTMLContract.SubsystemList.NumberOfSubsystems(
                    (sig, wh) => sig.UShortValue = numberOfSubsystems);
            }
            else if (manager.touchpanelZ[TPNumber].Type.ToUpper().Contains("TSR"))
            {
                CrestronConsole.PrintLine("Updating subsystem list smart object for TSR - number of subs {0}", numberOfSubsystems);
                manager.touchpanelZ[TPNumber].UserInterface.SmartObjects[2].UShortInput[4].UShortValue = numberOfSubsystems;
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
            updateSubsystemListSmartObject(TPNumber, false);//from update subsystems
        }

        public void updateSubsystemListSmartObject(ushort TPNumber, bool wholeHouseYes)
        {
            ushort homePageScenario = manager.touchpanelZ[TPNumber].HomePageScenario;
            // Same resolve+guard as UpdateSubsystems: this path also indexed SubsystemScenarioZ
            // with the panel's raw value (below, in the non-whole-house branch) and would throw
            // on a panel whose scenario is 0 or unknown.
            ushort currentSubsystemScenario = ResolveSubsystemScenario(TPNumber);
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
                if (currentSubsystemScenario == 0)
                {
                    CrestronConsole.PrintLine("updateSubsystemListSmartObject: TP-{0} has no usable subSystemScenario - skipping", TPNumber);
                    return;
                }
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
                    else if (manager.touchpanelZ[TPNumber].Type.ToUpper().Contains("TSR")) { 
                        manager.touchpanelZ[TPNumber].UserInterface.SmartObjects[2].BooleanInput[(ushort)(i + TsrSubsystemSelectedJoinBase)].BoolValue = false;//clear selected feedback
                        manager.touchpanelZ[TPNumber].UserInterface.SmartObjects[2].StringInput[(ushort)(i + 11)].StringValue = manager.SubsystemZ[subsystemNum].Name;
                        manager.touchpanelZ[TPNumber].UserInterface.SmartObjects[2].StringInput[(ushort)(i + 2011)].StringValue = manager.SubsystemZ[subsystemNum].IconSerial;
                        if (manager.RoomZ[currentRoomNumber].CurrentSubsystem == subsystemNum)
                        {
                            manager.touchpanelZ[TPNumber].UserInterface.SmartObjects[2].BooleanInput[(ushort)(i + TsrSubsystemSelectedJoinBase)].BoolValue = true;
                        }
                    }
                    else
                    {
                        manager.touchpanelZ[TPNumber].UserInterface.SmartObjects[2].BooleanInput[(ushort)(i + 4016)].BoolValue = false;//clear feedback
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

        /// <summary>
        /// Push the CURRENT shade state from the subsystem-control EISC into SmartObject 19 on a
        /// dumb panel, instead of waiting for a live SigChange.
        ///
        /// The normal path (subysystemControl_SigChange) only writes the smart object when the
        /// EISC raises a change event AND CurrentSubsystemIsShades is already true. An EISC fires
        /// on CHANGE, so any value SIMPL drove earlier — at startup, or while this panel sat on a
        /// different subsystem — is retained on the EISC output but never re-announced. Entering
        /// the shades page therefore left "Set Number of Items" at 0 and the Subpage Reference
        /// List drew nothing at all. Reading the retained outputs makes page entry deterministic.
        ///
        /// Join map (mirrors the live handler exactly, so both paths agree):
        ///   count   analog  (slot-1)*100 + 1        -> SO19 UShortInput[3]
        ///   names   serial  (slot-1)*100 + n        -> SO19 StringInput[(n-1)*2 + 1 + 4010]
        ///                   (stride 2: each subpage item owns a name AND a level serial)
        ///   button  digital (slot-1)*200 + n        -> SO19 BooleanInput[n + 4010]
        ///                   (3 per shade: open / stop / close)
        /// HTML panels use the contract instead and are skipped.
        /// </summary>
        public void SyncPanelToShades(ushort TPNumber)
        {
            try
            {
                if (!manager.touchpanelZ.ContainsKey(TPNumber)) return;
                var tp = manager.touchpanelZ[TPNumber];
                if (tp.UserInterface == null || tp.HTML_UI) return;

                // TP 21+ live on EISC2 with the offset recalculated from TP 21 as "TP 1",
                // exactly like SendToSubsystemEISC does on the outbound side.
                var eisc = (TPNumber <= 20) ? subsystemControlEISC : subsystemControlEISC2;
                if (eisc == null) return;
                ushort slot = (ushort)((TPNumber <= 20) ? TPNumber : TPNumber - 20);
                uint aBase = (uint)((slot - 1) * 100);
                uint bBase = (uint)((slot - 1) * 200);

                ushort count = eisc.UShortOutput[aBase + 1].UShortValue;
                var so = tp.UserInterface.SmartObjects[SHADES_SMART_OBJECT_ID];
                so.UShortInput[3].UShortValue = count;

                for (int n = 1; n <= count && n <= MAX_SHADES; n++)
                {
                    so.StringInput[(uint)((n - 1) * 2 + 1 + 4010)].StringValue =
                        eisc.StringOutput[aBase + (uint)n].StringValue;
                }
                for (int n = 1; n <= count * 3 && n <= MAX_SHADES * 3; n++)
                {
                    so.BooleanInput[(uint)(n + 4010)].BoolValue =
                        eisc.BooleanOutput[bBase + (uint)n].BoolValue;
                }

                CrestronConsole.PrintLine("SHADESYNC: TP-{0} slot {1} count={2} (analog {3}) pushed to SO{4}",
                    TPNumber, slot, count, aBase + 1, SHADES_SMART_OBJECT_ID);
            }
            catch (Exception ex)
            {
                ErrorLog.Error("SyncPanelToShades TP-{0} error: {1}", TPNumber, ex.Message);
                CrestronConsole.PrintLine("SHADESYNC: TP-{0} FAILED: {1}", TPNumber, ex.Message);
            }
        }

        // SmartObject 19 on both TSW-770-DARK and TSR-310 is a Subpage Reference List Horizontal
        // holding up to 20 shades: 3 digitals (open/stop/close), 1 analog and 2 serials per item.
        private const uint SHADES_SMART_OBJECT_ID = 19;
        private const int MAX_SHADES = 20;

        public void UpdateEquipIDsForSubsystems(ushort TPNumber, ushort currentRoomNumber)
        {
            subsystemEISC.UShortInput[(ushort)((TPNumber - 1) * 10 + 301)].UShortValue = manager.RoomZ[currentRoomNumber].AudioID;
            subsystemEISC.UShortInput[(ushort)((TPNumber - 1) * 10 + 302)].UShortValue = manager.RoomZ[currentRoomNumber].VideoOutputNum;
            subsystemEISC.UShortInput[(ushort)((TPNumber - 1) * 10 + 303)].UShortValue = manager.RoomZ[currentRoomNumber].LightsID;
            subsystemEISC.UShortInput[(ushort)((TPNumber - 1) * 10 + 304)].UShortValue = manager.RoomZ[currentRoomNumber].ShadesID;
            subsystemEISC.UShortInput[(ushort)((TPNumber - 1) * 10 + 306)].UShortValue = manager.RoomZ[currentRoomNumber].MiscID;

            // Send lightsID to LightsScenario2 EISC for room-based scene/load control
            if (lightingScenario2Control != null && manager.RoomZ[currentRoomNumber].LightsID > 0)
            {
                lightingScenario2Control.SendLightsID(TPNumber, manager.RoomZ[currentRoomNumber].LightsID);
            }

            // Send shadesID to ShadesScenario2 EISC for room-based shade control
            if (shadesScenario2Control != null && manager.RoomZ[currentRoomNumber].ShadesID > 0)
            {
                shadesScenario2Control.SendShadesID(TPNumber, manager.RoomZ[currentRoomNumber].ShadesID);
            }
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
        /// <summary>
        /// 
        /// </summary>
        /// <param name="TPNumber"></param>
        /// <param name="sourceButtonNumber"></param>
        /// <returns></returns>
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

        /// <summary>
        /// Called from LightingScenario2Control when the remote Lighting4Series program reports
        /// room-level on/off status via EISC3 signal 1000+lightsID.
        /// </summary>
        public void UpdateLightingStatusFromScenario2(ushort lightsID, bool lightsAreOff)
        {
            if (manager == null || manager.RoomZ == null) return;

            foreach (var room in manager.RoomZ)
            {
                if (room.Value.LightsID == lightsID)
                {
                    room.Value.LightsAreOff = lightsAreOff;
                    CrestronConsole.PrintLine("S2 room {0} lightsid {1} lightsareoff {2}", room.Value.Name, room.Value.LightsID, room.Value.LightsAreOff);
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
