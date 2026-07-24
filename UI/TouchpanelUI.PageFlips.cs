//-----------------------------------------------------------------------
// <copyright file="TouchpanelUI.PageFlips.cs" company="Crestron">
//     Copyright (c) Crestron Electronics. All rights reserved.
// </copyright>
//-----------------------------------------------------------------------
using System;
using Crestron.SimplSharp;
using Crestron.SimplSharpPro.CrestronThread;

namespace ACS_4Series_Template_V3.UI
{
    /// <summary>
    /// Page flip and menu handling for TouchpanelUI
    /// </summary>
    public partial class TouchpanelUI
    {
        // Track last page flip values to prevent duplicate updates causing blinking
        // Use a combined state: page number + whether video subsystem is active
        private ushort _lastVideoPageFlip = ushort.MaxValue;
        private bool _lastVideoSubsystemState = false;
        private ushort _lastMusicPageFlip = ushort.MaxValue;
        private bool _lastMusicSubsystemState = false;
        private bool _lastMusicHomePageState = false;
        public bool SuppressNextWholeHouseZoneFlip { get; set; }
        // When the suppress flag was armed (set on Home press). The stale whole-house zone echo
        // we want to swallow arrives within a few hundred ms of Home; a real user room press only
        // happens seconds later after navigating to a subsystem's floors list. Time-boxing the
        // suppression to this window keeps eating the echo without eating the user's first tap.
        public DateTime SuppressWholeHouseZoneArmedAt { get; set; }
        public const double SuppressWholeHouseZoneWindowMs = 1000;
        /// <summary>
        /// Reset video page flip tracking (call when changing rooms or turning off)
        /// </summary>
        public void ResetVideoPageFlipTracking()
        {
            _lastVideoPageFlip = ushort.MaxValue;
            _lastVideoSubsystemState = false;
        }

        /// <summary>
        /// Reset music page flip tracking (call when changing rooms)
        /// </summary>
        public void ResetMusicPageFlipTracking()
        {
            _lastMusicPageFlip = ushort.MaxValue;
            _lastMusicSubsystemState = false;
            _lastMusicHomePageState = false;
        }

        public void subsystemPageFlips(ushort pageNumber)
        {
            subsystemPageFlips(pageNumber, this.CurrentSubsystemNumber);
        }

        public void subsystemPageFlips(ushort pageNumber, ushort preferredSubsystemNumber)
        {
            //CrestronConsole.PrintLine("subsystemPageFlips called for TP-{0} pageNumber: {1}", this.Number, pageNumber);
            
            string subsystemName = "";
            ushort selectedSubsystemNumber = preferredSubsystemNumber > 0 ? preferredSubsystemNumber : this.CurrentSubsystemNumber;

            // For special clear/navigation pages, do not infer subsystem from current selection.
            // Otherwise a close (page 0) can accidentally re-open the current subsystem page.
            bool isSpecialPageNumber = (pageNumber == 0 || pageNumber == 1000 || (pageNumber > 90 && pageNumber < 100));

            // Prefer the currently selected subsystem only for real subsystem pages
            // to avoid ambiguity when multiple subsystems share FlipsToPageNumber.
            if (!isSpecialPageNumber
                && selectedSubsystemNumber > 0
                && _parent.manager.SubsystemZ.ContainsKey(selectedSubsystemNumber))
            {
                subsystemName = _parent.manager.SubsystemZ[selectedSubsystemNumber].Name;
            }
            else
            {
                for (ushort i = 1; i <= _parent.manager.SubsystemZ.Count; i++)
                {
                    if (_parent.manager.SubsystemZ[i].FlipsToPageNumber == pageNumber)
                    {
                        subsystemName = _parent.manager.SubsystemZ[i].Name;
                    }
                }
            }

            CrestronConsole.PrintLine("TP-{0} subsystemPageFlips page={1} currentSub={2} subsystemName={3}",
                this.Number, pageNumber, selectedSubsystemNumber, subsystemName);

            // HTML descriptor fork: on HTML panels, real subsystem pages are driven by a single
            // JSON descriptor (serial join 1520) consumed by pageRouter.js, NOT by the per-page
            // boolean choreography below. Special/navigation pages (close=0, 1000, whole-house room
            // list 91-99 -> isSpecialPageNumber) still use the legacy path for now; see
            // PAGE-FLIP-DESCRIPTOR-PLAN.md Phase 2. Dumb panels (HTML_UI == false) never take this
            // fork and are completely unaffected.
            if (this.HTML_UI)
            {
                // Home (10000) and close (0): tell pageRouter.js to hide every managed subsystem
                // page (it maps page "home" -> null -> all managed joins off). Without this the last
                // shown subsystem page lingers, because on the HTML path the router — not C# booleans
                // — owns those joins. 10000 is a pure home and returns; 0 falls through so the legacy
                // room-subsystem-list show (BooleanInput[100]) below still runs.
                if (pageNumber == 10000 || pageNumber == 0)
                {
                    SendHomeDescriptor();
                    if (pageNumber == 10000) { return; }
                }
                // Real subsystem page: route via the JSON descriptor instead of the boolean choreography.
                else if (!isSpecialPageNumber && selectedSubsystemNumber > 0)
                {
                    BuildAndSendSubsystemDescriptor(pageNumber, selectedSubsystemNumber, subsystemName);
                    return;
                }
            }

            for (ushort i = 0; i < 20; i++)
            {
                this.UserInterface.BooleanInput[(ushort)(i + 101)].BoolValue = false;
            }
            for (ushort i = 0; i < 10; i++)
            {
                this.UserInterface.BooleanInput[(ushort)(i + 701)].BoolValue = false;
                this.UserInterface.BooleanInput[(ushort)(i + 711)].BoolValue = false;
                this.UserInterface.BooleanInput[(ushort)(i + 721)].BoolValue = false;
                this.UserInterface.BooleanInput[(ushort)(i + 731)].BoolValue = false;
                this.UserInterface.BooleanInput[(ushort)(i + 741)].BoolValue = false;
            }

            // Hardening: never render a real room-subsystem page for a subsystem the current room
            // does not actually have. This catches any path that leaves a stale subsystem selected
            // (e.g. Shades carried over from a previous room) before it can pop the wrong page. Only
            // applies to real subsystem pages — special/navigation/whole-house pages are exempt
            // (isSpecialPageNumber), and whole-house lights/shades use the 91-99 page range which
            // never reaches the name branches below.
            //
            // Membership is checked against the SELECTED ROOM's own subsystem scenario
            // (CurrentRoomNum) — NOT this.SubSystemScenario. The touchpanel field lingers from the
            // last room-list navigation and is wrong on the whole-house zone path (where a room is
            // picked directly), which previously blocked legitimate whole-house Shades until a room
            // was first opened from the room list. The just-selected room is the authority here.
            // The whole-house path selects the subsystem fresh from the whole-house list and has
            // no per-room scenario to validate against (a whole-house-only subsystem like Pool /
            // Security / Gates legitimately may not appear in the current room's scenario). On that
            // path CurrentPageNumber is still Home, so skip the membership guard there; it only
            // exists to catch a stale subsystem carried over during room-list navigation.
            if (!isSpecialPageNumber && selectedSubsystemNumber > 0
                && this.CurrentPageNumber != (ushort)CurrentPageType.Home
                && _parent.manager.RoomZ.ContainsKey(this.CurrentRoomNum))
            {
                ushort roomScenario = _parent.manager.RoomZ[this.CurrentRoomNum].SubSystemScenario;

                if (_parent.manager.SubsystemScenarioZ.ContainsKey(roomScenario)
                    && !_parent.manager.SubsystemScenarioZ[roomScenario].IncludedSubsystems.Contains(selectedSubsystemNumber))
                {
                    CrestronConsole.PrintLine(
                        "TP-{0} subsystemPageFlips BLOCKED: subsystem {1} ({2}) not in room {3} scenario {4} - not flipping",
                        this.Number, selectedSubsystemNumber, subsystemName, this.CurrentRoomNum, roomScenario);
                    return; // subpages already cleared above; show nothing rather than the wrong subsystem
                }
            }

            if (subsystemName.ToUpper() == "HVAC" || subsystemName.ToUpper() == "CLIMATE")
            {
                if (this.Type == "Tsr310" || this.Type == "HR310")
                {
                    this.UserInterface.BooleanInput[(ushort)(pageNumber + 100)].BoolValue = true;
                }
                ushort scenario = _parent.manager.RoomZ[this.CurrentRoomNum].HVACScenario;
                if (this.CurrentPageNumber == (ushort)CurrentPageType.Home && this.Name.ToUpper().Contains("IPHONE"))
                {
                    this.UserInterface.BooleanInput[(ushort)(710 + scenario)].BoolValue = true;
                }
                else
                {
                    this.UserInterface.BooleanInput[(ushort)(700 + scenario)].BoolValue = true;
                }
            }
            else if (subsystemName.ToUpper().Contains("LIGHT"))
            {
                if (this.CurrentPageNumber == (ushort)CurrentPageType.Home && this.Name.ToUpper().Contains("IPHONE"))
                {
                    this.UserInterface.BooleanInput[723].BoolValue = true;
                }
                else
                {
                    // Use guiScenarioNumber when present (>0).
                    // If absent/0, fall back to legacy flips-to-page mapping.
                    ushort guiScenario = 0;

                    if (selectedSubsystemNumber > 0
                        && _parent.manager.SubsystemZ.ContainsKey(selectedSubsystemNumber)
                        && _parent.manager.SubsystemZ[selectedSubsystemNumber].GuiScenarioNumber > 0)
                    {
                        guiScenario = _parent.manager.SubsystemZ[selectedSubsystemNumber].GuiScenarioNumber;
                    }
                    else
                    {
                        for (ushort i = 1; i <= _parent.manager.SubsystemZ.Count; i++)
                        {
                            if (_parent.manager.SubsystemZ[i].FlipsToPageNumber == pageNumber
                                && _parent.manager.SubsystemZ[i].GuiScenarioNumber > 0)
                            {
                                guiScenario = _parent.manager.SubsystemZ[i].GuiScenarioNumber;
                                break;
                            }
                        }
                    }

                    if (guiScenario > 0)
                    {
                        CrestronConsole.PrintLine("TP-{0} LIGHTS -> join {1} (guiScenario={2})", this.Number, (ushort)(730 + guiScenario), guiScenario);
                        this.UserInterface.BooleanInput[(ushort)(730 + guiScenario)].BoolValue = true;
                    }
                    else
                    {
                        CrestronConsole.PrintLine("TP-{0} LIGHTS -> legacy join {1}", this.Number, (ushort)(pageNumber + 100));
                        this.UserInterface.BooleanInput[(ushort)(pageNumber + 100)].BoolValue = true;
                    }
                }
            }
            else if (subsystemName.ToUpper().Contains("SHADE") || subsystemName.ToUpper().Contains("DRAPE"))
            {
                // Use guiScenarioNumber when present (>0).
                // If absent/0, fall back to legacy flips-to-page mapping.
                ushort guiScenario = 0;

                if (selectedSubsystemNumber > 0
                    && _parent.manager.SubsystemZ.ContainsKey(selectedSubsystemNumber)
                    && _parent.manager.SubsystemZ[selectedSubsystemNumber].GuiScenarioNumber > 0)
                {
                    guiScenario = _parent.manager.SubsystemZ[selectedSubsystemNumber].GuiScenarioNumber;
                }
                else
                {
                    for (ushort i = 1; i <= _parent.manager.SubsystemZ.Count; i++)
                    {
                        if (_parent.manager.SubsystemZ[i].FlipsToPageNumber == pageNumber
                            && _parent.manager.SubsystemZ[i].GuiScenarioNumber > 0)
                        {
                            guiScenario = _parent.manager.SubsystemZ[i].GuiScenarioNumber;
                            break;
                        }
                    }
                }

                if (guiScenario > 0)
                {
                    CrestronConsole.PrintLine("TP-{0} SHADES -> join {1} (guiScenario={2})", this.Number, (ushort)(740 + guiScenario), guiScenario);
                    this.UserInterface.BooleanInput[(ushort)(740 + guiScenario)].BoolValue = true;
                }
                else
                {
                    CrestronConsole.PrintLine("TP-{0} SHADES -> legacy join {1}", this.Number, (ushort)(pageNumber + 100));
                    this.UserInterface.BooleanInput[(ushort)(pageNumber + 100)].BoolValue = true;
                }
            }
            else if (pageNumber == 1000)
            {
                this.UserInterface.BooleanInput[100].BoolValue = false;
                if (_parent.manager.FloorScenarioZ[this.FloorScenario].IncludedFloors.Count < 2)
                {
                    this.UserInterface.BooleanInput[51].BoolValue = true;
                }
                else
                {
                    //CrestronConsole.PrintLine("subsystem page flips TP-{0} FloorScenario {1} has {2} floors", this.Number, this.FloorScenario, _parent.manager.FloorScenarioZ[this.FloorScenario].IncludedFloors.Count);
                    this.UserInterface.BooleanInput[50].BoolValue = true;
                }
            }
            else if (pageNumber == 0)
            {
                if (this.CurrentPageNumber == (ushort)CurrentPageType.RoomSubsystemList)
                {
                    this.UserInterface.BooleanInput[100].BoolValue = true;
                }
            }
            else if (pageNumber > 0 && pageNumber <= 20)
            {
                this.UserInterface.BooleanInput[(ushort)(pageNumber + 100)].BoolValue = true;
            }
            else if (pageNumber > 90 && pageNumber < 100)
            {
                this.UserInterface.BooleanInput[(ushort)(pageNumber)].BoolValue = true;
                this.UserInterface.BooleanInput[100].BoolValue = false;
            }
        }

        // Serial join carrying the JSON page descriptor to HTML panels (consumed by pageRouter.js).
        // HTML-only reserved range (1500+), alongside MusicSourceCatalogJoin = 1510.
        private const ushort PageDescriptorJoin = 1520;

        /// <summary>
        /// Builds the JSON page descriptor for the selected subsystem and pushes it to this HTML
        /// panel on serial join 1520. pageRouter.js resolves it to a single visible subsystem page.
        ///
        /// The descriptor carries ONLY the semantic routing inputs — a page key + scenario (plus
        /// subsystem/room for context/logging). pageRouter.js maps (page, scenario) to a NAMED page
        /// element and shows it by name (no join number anywhere in the navigation path), the same
        /// way the cameras page is shown. See PAGE-FLIP-DESCRIPTOR-PLAN.md.
        /// </summary>
        private void BuildAndSendSubsystemDescriptor(ushort pageNumber, ushort subsystemNumber, string subsystemName)
        {
            string pageKey = SubsystemPageKey(subsystemName);
            string upper = (subsystemName ?? string.Empty).ToUpper();

            ushort guiScenario = 0;
            if (_parent.manager.SubsystemZ.ContainsKey(subsystemNumber))
            {
                guiScenario = _parent.manager.SubsystemZ[subsystemNumber].GuiScenarioNumber;
            }

            // Scenario is the ONLY routing input the HTML router needs now: pageRouter.js
            // maps (page, scenario) -> a named page element (no join number anywhere in the
            // path). Climate uses the room's HVAC scenario; everything else uses the
            // subsystem's GuiScenarioNumber (0 -> the router falls back to the page default).
            ushort scenario;
            if (upper == "CLIMATE" || upper == "HVAC")
            {
                scenario = _parent.manager.RoomZ.ContainsKey(this.CurrentRoomNum)
                    ? _parent.manager.RoomZ[this.CurrentRoomNum].HVACScenario
                    : (ushort)1;
            }
            else
            {
                scenario = guiScenario;
            }

            string roomName = _parent.manager.RoomZ.ContainsKey(this.CurrentRoomNum)
                ? _parent.manager.RoomZ[this.CurrentRoomNum].Name
                : string.Empty;

            var sb = new System.Text.StringBuilder();
            sb.Append("{\"page\":\"").Append(pageKey).Append("\"");
            sb.Append(",\"scenario\":").Append(scenario);
            sb.Append(",\"subsystem\":").Append(subsystemNumber);
            sb.Append(",\"room\":").Append(this.CurrentRoomNum);
            sb.Append(",\"roomName\":\"").Append(EscapeDescriptorString(roomName)).Append("\"");
            sb.Append("}");
            string json = sb.ToString();

            this.UserInterface.StringInput[PageDescriptorJoin].StringValue = json;
            CrestronConsole.PrintLine("TP-{0} pageDescriptor -> {1}", this.Number, json);

            // Tell the camera auto-retry whether this panel is now on the Cameras page.
            if (this.HTML_UI && _parent.cameraManager != null)
            {
                _parent.cameraManager.SetPageActive(this.Number, pageKey == "cameras");
            }
        }

        /// <summary>
        /// Sends a "home" descriptor so pageRouter.js hides every managed subsystem page (mutual
        /// exclusion with no target). Used on Home and on close, where there is no subsystem to show.
        /// </summary>
        private void SendHomeDescriptor()
        {
            string roomName = _parent.manager.RoomZ.ContainsKey(this.CurrentRoomNum)
                ? _parent.manager.RoomZ[this.CurrentRoomNum].Name
                : string.Empty;

            var sb = new System.Text.StringBuilder();
            sb.Append("{\"page\":\"home\"");
            sb.Append(",\"scenario\":0");
            sb.Append(",\"subsystem\":0");
            sb.Append(",\"room\":").Append(this.CurrentRoomNum);
            sb.Append(",\"roomName\":\"").Append(EscapeDescriptorString(roomName)).Append("\"");
            sb.Append("}");
            string json = sb.ToString();

            this.UserInterface.StringInput[PageDescriptorJoin].StringValue = json;
            CrestronConsole.PrintLine("TP-{0} pageDescriptor -> {1}", this.Number, json);

            // Home = definitely not on the Cameras page; stop any auto-retry.
            if (this.HTML_UI && _parent.cameraManager != null)
            {
                _parent.cameraManager.SetPageActive(this.Number, false);
            }
        }

        /// <summary>Maps a subsystem Name to the canonical pageRouter page key.</summary>
        private static string SubsystemPageKey(string subsystemName)
        {
            string n = (subsystemName ?? string.Empty).ToUpper();
            if (n.Contains("LIGHT")) return "lights";
            if (n.Contains("SHADE") || n.Contains("DRAPE")) return "shades";
            if (n == "CLIMATE" || n == "HVAC") return "climate";
            if (n == "AUDIO" || n == "MUSIC") return "audio";
            if (n.Contains("VIDEO")) return "video";
            if (n.Contains("POOL")) return "pool";
            if (n.Contains("GATE")) return "gates";
            if (n.Contains("SECURITY")) return "security";
            if (n.Contains("PANEL") || n.Contains("LIFT")) return "panel";
            return n.ToLower();
        }

        /// <summary>Minimal JSON string escape for descriptor values (quotes + backslashes).</summary>
        private static string EscapeDescriptorString(string s)
        {
            if (string.IsNullOrEmpty(s)) return string.Empty;
            return s.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }

        public void videoPageFlips(ushort pageNumber)
        {
            // Check if this is a duplicate call with same state - skip to prevent blinking
            // Must check both page number AND subsystem state since the same page number
            // behaves differently based on CurrentSubsystemIsVideo
            if (_lastVideoPageFlip == pageNumber && _lastVideoSubsystemState == this.CurrentSubsystemIsVideo)
            {
                //CrestronConsole.PrintLine("TP-{0} videoPageFlips SKIPPED: page={1}, lastPage={2}, isVideo={3}, lastIsVideo={4}", 
                    //this.Number, pageNumber, _lastVideoPageFlip, this.CurrentSubsystemIsVideo, _lastVideoSubsystemState);
                return;
            }
            
            //CrestronConsole.PrintLine("TP-{0} videoPageFlips EXECUTING: page={1}, lastPage={2}, isVideo={3}, lastIsVideo={4}", 
                //this.Number, pageNumber, _lastVideoPageFlip, this.CurrentSubsystemIsVideo, _lastVideoSubsystemState);
            
            _lastVideoPageFlip = pageNumber;
            _lastVideoSubsystemState = this.CurrentSubsystemIsVideo;

            this.CurrentVideoPageNumber = pageNumber;
            for (ushort i = 0; i < 23; i++)
            {
                this.UserInterface.BooleanInput[(ushort)(i + 121)].BoolValue = false;
            }

            this.UserInterface.BooleanInput[53].BoolValue = false;

            if (this.CurrentSubsystemIsVideo)
            {
                this.UserInterface.BooleanInput[(ushort)(pageNumber + 120)].BoolValue = true;
                if (pageNumber == 1 && CurrentVSrcNum > 0 && _parent.manager.VideoSourceZ.ContainsKey(CurrentVSrcNum))
                {
                    ushort subpageScenario = _parent.manager.VideoSourceZ[CurrentVSrcNum].CurrentSubpageScenario;
                    CrestronConsole.PrintLine("TP-{0} Setting DVR subpage: 140 + {1} = {2}", this.Number, subpageScenario, 140 + subpageScenario);
                    this.UserInterface.BooleanInput[(ushort)(140 + (_parent.manager.VideoSourceZ[CurrentVSrcNum].CurrentSubpageScenario))].BoolValue = true;
                    if (this.HTML_UI)
                    {
                        // Maybe do nothing. check the DVR tab contract
                    }
                    else
                    {
                        this.UserInterface.SmartObjects[26].BooleanInput[(ushort)(2)].BoolValue = false;
                        this.UserInterface.SmartObjects[26].BooleanInput[(ushort)(4)].BoolValue = false;
                        this.UserInterface.SmartObjects[26].BooleanInput[(ushort)(2 * subpageScenario)].BoolValue = true;
                    }
                }
            }
        }

        public void SelectDVRPage()
        {
        }

        public void musicPageFlips(ushort pageNumber)
        {
            //CrestronConsole.PrintLine("TP-{2}, musicPageFlips: {0} currentPageNumber {1} currentSubsystemIsAudio-{3}", pageNumber, this.CurrentPageNumber, this.Number, this.CurrentSubsystemIsAudio);

            bool isHomePage = (this.CurrentPageNumber == (ushort)CurrentPageType.Home);
            
            // Only apply debouncing when NOT on the home page
            // The blinking issue only happens from the music subsystem page, not the home page
            if (!isHomePage)
            {
                if (_lastMusicPageFlip == pageNumber && 
                    _lastMusicSubsystemState == this.CurrentSubsystemIsAudio)
                    return;
                
                _lastMusicPageFlip = pageNumber;
                _lastMusicSubsystemState = this.CurrentSubsystemIsAudio;
            }
            //clear out any subpages first
            for (ushort i = 0; i < 20; i++)
            {
                this.UserInterface.BooleanInput[(ushort)(i + 1011)].BoolValue = false;
            }

            this.UserInterface.BooleanInput[55].BoolValue = false;//this is the music source subpage for iphone.

            // Suppress reactive media-player flips while a music selection menu is in use
            // or a whole-house quick-action recall is running. Each of these puts up a
            // modal room/source list and fires many source-change events (one per room
            // checked / recalled); without this the media player pops up over the menu on
            // every change. The subpage-clear above still runs, so the media player stays
            // hidden until the menu clears (its join goes false) or the recall timer
            // finishes — the next flip then shows the correct page. Explicit user actions
            // (chevron tap via LaunchSource) bypass musicPageFlips and write BooleanInput
            // directly, so those still work while a menu is visible.
            //   b 21   home-music control dialog
            //   b 998  share-source menu (no-floor variant)
            //   b 999  share-source menu (floor variant)
            //   b 1500 "Select rooms for:" / add-to-group list (home-music start flow)
            bool musicMenuOrRecallActive =
                   this.UserInterface.BooleanInput[21].BoolValue
                || this.UserInterface.BooleanInput[998].BoolValue
                || this.UserInterface.BooleanInput[999].BoolValue
                || this.UserInterface.BooleanInput[1500].BoolValue
                || _parent.musicSystemControl.RecallMusicPresetTimerBusy;

            if (this.CurrentSubsystemIsAudio)
            {
                if (pageNumber > 0 && !musicMenuOrRecallActive)
                {
                    //show the music source on the rooms music page.
                    this.UserInterface.BooleanInput[(ushort)(pageNumber + 1010)].BoolValue = true;
                }
            }
            else if (isHomePage)
            {
                if (musicMenuOrRecallActive) return;
                this.UserInterface.BooleanInput[(ushort)(pageNumber + 1020)].BoolValue = true;
            }
        }

        public void SleepFormatLiftMenu(string button, ushort timer)
        {
            if (_sleepFormatLiftTimer != null)
            {
                _sleepFormatLiftTimer.Stop();
                _sleepFormatLiftTimer.Dispose();
                _sleepFormatLiftTimer = null;
            }

            if (timer > 0)
            {
                _sleepFormatLiftTimer = new CTimer(_ =>
                {
                    ClearSleepFormatLiftMenus();
                }, timer * 1000);
            }

            for (ushort i = 0; i < 5; i++)
            {
                this.UserInterface.BooleanInput[(ushort)(171 + i)].BoolValue = false;
            }
            for (ushort i = 0; i < 10; i++)
            {
                this.UserInterface.BooleanInput[(ushort)(191 + i)].BoolValue = false;
                this.UserInterface.BooleanInput[(ushort)(71 + i)].BoolValue = false;
            }

            if (button.ToUpper().Contains("SLEEP"))
            {
                this.UserInterface.BooleanInput[160].BoolValue = !this.UserInterface.BooleanInput[160].BoolValue;
                ushort scenario = _parent.manager.RoomZ[this.CurrentRoomNum].SleepScenario;
                if (this.UserInterface.BooleanInput[160].BoolValue)
                {
                    // Validate scenario is in valid range (1-5) to prevent UI conflicts
                    if (scenario > 0 && scenario <= 5)
                    {
                        this.UserInterface.BooleanInput[(ushort)(170 + scenario)].BoolValue = true;
                    }
                    else if (scenario > 5)
                    {
                        CrestronConsole.PrintLine("WARNING: SleepScenario {0} for room {1} is out of range (1-5). Sleep menu disabled.", 
                            scenario, _parent.manager.RoomZ[this.CurrentRoomNum].Name);
                    }
                    this.UserInterface.BooleanInput[180].BoolValue = false;
                    this.UserInterface.BooleanInput[60].BoolValue = false;
                }
            }
            else if (button.ToUpper().Contains("FORMAT"))
            {
                this.UserInterface.BooleanInput[180].BoolValue = !this.UserInterface.BooleanInput[180].BoolValue;
                ushort scenario = _parent.manager.RoomZ[this.CurrentRoomNum].FormatScenario;
                if (this.UserInterface.BooleanInput[180].BoolValue)
                {
                    // Validate scenario is in valid range (1-10) to prevent UI conflicts
                    if (scenario > 0 && scenario <= 10)
                    {
                        this.UserInterface.BooleanInput[(ushort)(190 + scenario)].BoolValue = true;
                    }
                    else if (scenario > 10)
                    {
                        CrestronConsole.PrintLine("WARNING: FormatScenario {0} for room {1} is out of range (1-10). Format menu disabled.", 
                            scenario, _parent.manager.RoomZ[this.CurrentRoomNum].Name);
                    }
                    this.UserInterface.BooleanInput[160].BoolValue = false;
                    this.UserInterface.BooleanInput[60].BoolValue = false;
                }
            }
            else if (button.ToUpper().Contains("LIFT"))
            {
                this.UserInterface.BooleanInput[60].BoolValue = !this.UserInterface.BooleanInput[60].BoolValue;
                ushort scenario = _parent.manager.RoomZ[this.CurrentRoomNum].LiftScenario;
                if (this.UserInterface.BooleanInput[60].BoolValue)
                {
                    // Validate scenario is in valid range (1-10) to prevent UI conflicts
                    if (scenario > 0 && scenario <= 10)
                    {
                        this.UserInterface.BooleanInput[(ushort)(70 + scenario)].BoolValue = true;
                    }
                    else if (scenario > 10)
                    {
                        CrestronConsole.PrintLine("WARNING: LiftScenario {0} for room {1} is out of range (1-10). Lift menu disabled.", 
                            scenario, _parent.manager.RoomZ[this.CurrentRoomNum].Name);
                    }
                    this.UserInterface.BooleanInput[180].BoolValue = false;
                    this.UserInterface.BooleanInput[160].BoolValue = false;
                }
            }
            else
            {
                this.UserInterface.BooleanInput[60].BoolValue = false;
                this.UserInterface.BooleanInput[160].BoolValue = false;
                this.UserInterface.BooleanInput[180].BoolValue = false;
            }
        }

        private void ClearSleepFormatLiftMenus()
        {
            for (ushort i = 0; i < 5; i++)
            {
                this.UserInterface.BooleanInput[(ushort)(171 + i)].BoolValue = false;
            }
            for (ushort i = 0; i < 10; i++)
            {
                this.UserInterface.BooleanInput[(ushort)(191 + i)].BoolValue = false;
                this.UserInterface.BooleanInput[(ushort)(71 + i)].BoolValue = false;
            }
            this.UserInterface.BooleanInput[60].BoolValue = false;
            this.UserInterface.BooleanInput[160].BoolValue = false;
            this.UserInterface.BooleanInput[180].BoolValue = false;
        }
    }
}
