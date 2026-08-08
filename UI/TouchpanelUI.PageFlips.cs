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

            // Same for the intercom, and here it is load-bearing rather than an
            // optimisation: the door video outlives the call on purpose, so leaving the
            // page is the only thing that stops the stream and frees its RTSP session.
            if (this.HTML_UI && _parent.intercomManager != null)
            {
                _parent.intercomManager.SetPageActive(this.Number, pageKey == "intercom");
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

            // Home is also how the intercom page ends in practice — the close button, the
            // Home key and the IDLE TIMEOUT all arrive here (timeout → GoToDefaultPage →
            // HomeButtonPress → subsystemPageFlips(10000) → here). This is the line that
            // stops the door stream when a panel is left alone on the page.
            if (this.HTML_UI && _parent.intercomManager != null)
            {
                _parent.intercomManager.SetPageActive(this.Number, false);
            }
        }

        /// <summary>
        /// Forces this HTML panel onto the Intercom page, whatever it was showing.
        ///
        /// Deliberately NOT routed through subsystemPageFlips(): an incoming door call is
        /// not a subsystem selection. Going through that path would require a real
        /// subsystem number and would be subject to the room-membership guard, and the
        /// intercom is not room-scoped at all — it belongs to the panel. Writing the
        /// descriptor directly is both simpler and immune to whatever room/subsystem
        /// state the panel happened to be in.
        ///
        /// The page key must match the DOM_PAGES entry in pageRouter.js ("intercom"),
        /// which shows it with mutual exclusion against every other managed page — so
        /// this alone is enough to get off the rooms page, a subsystem page, or home.
        /// </summary>
        /// <param name="scenario">
        /// Which intercom LAYOUT to show. 1 = SIP door station (full call control),
        /// 2 = view-only RTSP door station (no SIP session, so no answer/reject/hangup/mic).
        ///
        /// ⚠ The scenario comes from the STATION THAT RANG, not from the subsystem's
        /// guiScenarioNumber and not from the panel. A house can have both a 2N and a
        /// UniFi door station, so the layout is a property of the caller, not of the
        /// config or the screen. The subsystem's guiScenarioNumber is only the default for
        /// a user opening the page manually with no call in progress.
        /// </param>
        public void ShowIntercomPage(ushort scenario)
        {
            if (!this.HTML_UI || this.UserInterface == null) { return; }

            string roomName = _parent.manager.RoomZ.ContainsKey(this.CurrentRoomNum)
                ? _parent.manager.RoomZ[this.CurrentRoomNum].Name
                : string.Empty;

            var sb = new System.Text.StringBuilder();
            sb.Append("{\"page\":\"intercom\"");
            sb.Append(",\"scenario\":").Append(scenario < 1 ? 1 : scenario);
            sb.Append(",\"subsystem\":0");
            sb.Append(",\"room\":").Append(this.CurrentRoomNum);
            sb.Append(",\"roomName\":\"").Append(EscapeDescriptorString(roomName)).Append("\"");
            sb.Append("}");
            string json = sb.ToString();

            this.UserInterface.StringInput[PageDescriptorJoin].StringValue = json;
            CrestronConsole.PrintLine("TP-{0} pageDescriptor (forced by call) -> {1}", this.Number, json);

            // Leaving the Cameras page: stop its stream auto-retry, exactly as the normal
            // descriptor path does. Both pages own a ch5-video, and a camera retry firing
            // while the intercom stream is up would fight over the panel's RTSP sessions.
            if (_parent.cameraManager != null)
            {
                _parent.cameraManager.SetPageActive(this.Number, false);
            }

            // Arm the idle timer for the same reason as ShowCamerasPage: this is a programmatic
            // flip, so nothing else starts it. During a call IntercomCallActive re-arms instead of
            // navigating, and once the call ends the next expiry sends the panel home normally —
            // but only if a timer is actually running.
            ResetIdleTimer();
        }

        /// <summary>
        /// Forces this HTML panel onto the Cameras page, whatever it was showing.
        ///
        /// Used by the external camera-popup trigger (an event on another program says
        /// "show camera X now" — see CameraManager.PopupCameraOnAllPanels). Same reasoning
        /// as ShowIntercomPage for writing the descriptor directly instead of going through
        /// subsystemPageFlips: this is not a user subsystem selection, it needs no
        /// subsystem number, and it must not be subject to the room-membership guard —
        /// cameras are whole-house, not room-scoped.
        ///
        /// The page key must match the DOM_PAGES entry in pageRouter.js ("cameras"), which
        /// shows it with mutual exclusion against every other managed page, so this alone
        /// is enough to get off home, a rooms page, or any subsystem page.
        /// </summary>
        public void ShowCamerasPage()
        {
            if (!this.HTML_UI || this.UserInterface == null) { return; }

            string roomName = _parent.manager.RoomZ.ContainsKey(this.CurrentRoomNum)
                ? _parent.manager.RoomZ[this.CurrentRoomNum].Name
                : string.Empty;

            var sb = new System.Text.StringBuilder();
            sb.Append("{\"page\":\"cameras\"");
            sb.Append(",\"scenario\":0");
            sb.Append(",\"subsystem\":0");
            sb.Append(",\"room\":").Append(this.CurrentRoomNum);
            sb.Append(",\"roomName\":\"").Append(EscapeDescriptorString(roomName)).Append("\"");
            sb.Append("}");
            string json = sb.ToString();

            this.UserInterface.StringInput[PageDescriptorJoin].StringValue = json;
            CrestronConsole.PrintLine("TP-{0} pageDescriptor (camera popup) -> {1}", this.Number, json);

            // Arm the stream auto-retry for this panel, exactly as the normal descriptor path
            // does when it lands on Cameras. Without this a failed stream on a popup would
            // never be retried, because the retry logic is gated on the panel being on the page.
            if (_parent.cameraManager != null)
            {
                _parent.cameraManager.SetPageActive(this.Number, true);
            }

            // Leaving the intercom page: drop its stream. Two ch5-video streams open at once
            // would compete for the panel's small RTSP session pool, and a camera popup can
            // land on a panel still showing door video from an earlier call.
            if (_parent.intercomManager != null)
            {
                _parent.intercomManager.SetPageActive(this.Number, false);
            }

            // ⚠ ARM THE IDLE TIMER, or the panel never leaves this page.
            //
            // This is a PROGRAMMATIC page flip: nobody touched the panel, so none of the normal
            // sig-change paths run and nothing starts the timer. On a panel that had already gone
            // idle the timer has fired and is dormant, so without this the popup parks it on the
            // Cameras page indefinitely — observed sitting there for several minutes rather than
            // returning to its configured default page.
            //
            // Exactly the failure ResetIdleTimer's own remarks describe: a navigation path that
            // does not reset the timer. The fix belongs here, not in the timeout value.
            ResetIdleTimer();
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

        // Serial join carrying the A/V SOURCE descriptor to HTML panels (consumed by
        // pageRouter.js). Separate from PageDescriptorJoin because a source page renders
        // INSIDE the video subsystem page, not instead of it — the two have independent
        // mutual-exclusion sets and must not clobber each other.
        private const ushort SourceDescriptorJoin = 1522;

        // Serial join carrying the MUSIC source descriptor. Deliberately NOT the same join as
        // the video source descriptor above, even though the payload shape is nearly identical
        // and `kind` would let the router tell them apart.
        //
        // A serial join latches exactly one value. Video and music are cleared and set on
        // independent schedules — the media player can be centred over the HOME page while the
        // video descriptor is being cleared by room-list navigation — so sharing one join means
        // the last writer wins and the other kind's state is gone. That is invisible in normal
        // operation (both sides are re-sent on the next flip) but not on the page-ready pull
        // after an HTML reload, where the single latched value is the ONLY thing the panel gets
        // back. Two joins, two independently replayable states.
        private const ushort MusicSourceDescriptorJoin = 1523;

        // Serial join carrying the ROOM OPTIONS descriptor: which video dropdowns this room
        // has, and how many buttons each holds. Latched capability, not a command — it changes
        // only when the panel's room changes.
        private const ushort RoomOptionsDescriptorJoin = 1524;

        // Last room-options payload sent, so an unchanged room does not re-send. Room changes
        // are rare but UpdateTPVideoMenu is not — it runs on every source change.
        private string _lastRoomOptionsJson = string.Empty;

        /// <summary>
        /// Publishes the video dropdown capability for the panel's current room:
        /// {"room":5,"lift":3,"sleep":4,"format":4,"display":2} — button counts, 0 meaning the
        /// room does not have that menu.
        ///
        /// Carries NO labels. Every label already has its own serial join (60/61-65 lift,
        /// 160/161-165 sleep, 180/181-190 format, 372-381 display) written a few lines before
        /// this is called, and duplicating them here would bloat a join that is always latched
        /// — long serial payloads are silently truncated on hardware panels. Counts are all
        /// the panel is missing.
        ///
        /// HTML panels only. Dumb panels read the same facts from the availability booleans
        /// and their smart objects, which are untouched.
        /// </summary>
        public void SendRoomOptionsDescriptor(ushort liftCount, ushort sleepCount,
                                              ushort formatCount, ushort displayCount)
        {
            if (!this.HTML_UI) return;

            var sb = new System.Text.StringBuilder();
            sb.Append("{\"room\":").Append(this.CurrentRoomNum);
            sb.Append(",\"lift\":").Append(liftCount);
            sb.Append(",\"sleep\":").Append(sleepCount);
            sb.Append(",\"format\":").Append(formatCount);
            sb.Append(",\"display\":").Append(displayCount);
            sb.Append("}");
            string json = sb.ToString();

            if (json == _lastRoomOptionsJson) return;
            _lastRoomOptionsJson = json;

            this.UserInterface.StringInput[RoomOptionsDescriptorJoin].StringValue = json;
            CrestronConsole.PrintLine("TP-{0} roomOptions -> {1}", this.Number, json);
        }

        /// <summary>
        /// Builds the JSON source descriptor for a video source and pushes it to this HTML
        /// panel on serial join 1522. pageRouter.js resolves (source, view, scenario) to a
        /// named element, e.g. "apple tv scenario 3" -> appletvMainScenario3.
        ///
        /// Carries NO view: the router always opens a new source on `main` and owns
        /// main/keypad/favorites switching locally. The program does not track, and does not
        /// need to know, which tab the user is looking at.
        /// </summary>
        private void BuildAndSendSourceDescriptor(ushort srcNum)
        {
            if (!_parent.manager.VideoSourceZ.ContainsKey(srcNum))
            {
                CrestronConsole.PrintLine("TP-{0} sourceDescriptor: unknown video source {1}", this.Number, srcNum);
                SendClearSourceDescriptor();
                return;
            }

            var src = _parent.manager.VideoSourceZ[srcNum];
            string sourceKey = VideoSourcePageKey(src.Name);

            if (sourceKey.Length == 0)
            {
                // Name matched no known device type. Clearing (rather than sending an empty key)
                // keeps the panel in a defined state; fix the source Name in the config.
                CrestronConsole.PrintLine(
                    "TP-{0} sourceDescriptor: video source {1} name '{2}' matches no known source type - no page will show",
                    this.Number, srcNum, src.Name);
                SendClearSourceDescriptor();
                return;
            }

            var sb = new System.Text.StringBuilder();
            sb.Append("{\"kind\":\"video\"");
            sb.Append(",\"source\":\"").Append(sourceKey).Append("\"");
            sb.Append(",\"scenario\":").Append(src.EffectiveGuiScenario);
            sb.Append(",\"srcNum\":").Append(srcNum);
            sb.Append(",\"srcName\":\"").Append(EscapeDescriptorString(src.DisplayName)).Append("\"");
            sb.Append(",\"room\":").Append(this.CurrentRoomNum);
            sb.Append("}");
            string json = sb.ToString();

            this.UserInterface.StringInput[SourceDescriptorJoin].StringValue = json;
            CrestronConsole.PrintLine("TP-{0} sourceDescriptor -> {1}", this.Number, json);
        }

        /// <summary>
        /// Sends an empty-source descriptor so pageRouter.js hides every source page and
        /// forgets its per-source view memory (next selection opens on `main`). Used on
        /// source-off, power-off and close.
        /// </summary>
        private void SendClearSourceDescriptor()
        {
            string json = "{\"kind\":\"video\",\"source\":\"\",\"scenario\":0,\"srcNum\":0,\"srcName\":\"\",\"room\":"
                + this.CurrentRoomNum + "}";
            this.UserInterface.StringInput[SourceDescriptorJoin].StringValue = json;
            CrestronConsole.PrintLine("TP-{0} sourceDescriptor -> {1}", this.Number, json);
        }

        /// <summary>
        /// Maps a video source's configured Name to the canonical pageRouter source key.
        ///
        /// Matches on Name, never DisplayName: Name is not shown to the user, so it stays
        /// descriptive ("DVR 10 Tree Guest House", "Apple TV 5 Guest 2") while DisplayName is
        /// whatever the client wants to read on screen ("His DVR"). Returns "" when nothing
        /// matches — the caller logs it and shows no page.
        /// </summary>
        private static string VideoSourcePageKey(string sourceName)
        {
            string n = (sourceName ?? string.Empty).ToUpper();

            // Apple TV MUST be tested before anything that could match a trailing "TV".
            if (n.Contains("APPLE TV") || n.Contains("APPLETV") || ContainsWord(n, "ATV")) return "appletv";
            if (n.Contains("KALEIDESCAPE") || n.Contains("KSCAPE")) return "kaleidescape";
            if (n.Contains("BLURAY") || n.Contains("BLU-RAY") || n.Contains("BLU RAY")) return "bluray";
            if (n.Contains("DVR") || n.Contains("DIRECTV") || n.Contains("DIRECT TV")) return "dvr";
            if (n.Contains("CABLE") || ContainsWord(n, "CATV")) return "cabletv";
            if (n.Contains("CAMERA")) return "cameras";
            return string.Empty;
        }

        /// <summary>
        /// Whole-word Contains. Short source-type tokens ("ATV", "CATV") must not match inside
        /// unrelated words — a plain Contains("ATV") hits names like "GREATVIEW".
        /// </summary>
        private static bool ContainsWord(string haystack, string word)
        {
            if (string.IsNullOrEmpty(haystack) || string.IsNullOrEmpty(word)) return false;

            int i = haystack.IndexOf(word, StringComparison.Ordinal);
            while (i >= 0)
            {
                bool leftOk = (i == 0) || !char.IsLetterOrDigit(haystack[i - 1]);
                int after = i + word.Length;
                bool rightOk = (after >= haystack.Length) || !char.IsLetterOrDigit(haystack[after]);
                if (leftOk && rightOk) return true;
                i = haystack.IndexOf(word, i + 1, StringComparison.Ordinal);
            }
            return false;
        }

        public void videoPageFlips(ushort pageNumber)
        {
            videoPageFlips(pageNumber, this.CurrentVSrcNum);
        }

        /// <summary>
        /// Flip to a video source's control page.
        ///
        /// <paramref name="preferredSourceNumber"/> exists because CurrentVSrcNum is NOT
        /// reliably current at every call site — SubscribeToVideoMenuEvents flips the page
        /// while the field still holds the previous room's source (it is assigned later).
        /// That was harmless when the page was chosen by FlipsToPageNumber arithmetic, but
        /// the HTML descriptor names the SOURCE, so it must be told which one explicitly
        /// rather than reading lingering state. Same reasoning as
        /// subsystemPageFlips(pageNumber, preferredSubsystemNumber).
        /// </summary>
        public void videoPageFlips(ushort pageNumber, ushort preferredSourceNumber)
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

            // HTML descriptor fork: on HTML panels the source control page is named by a JSON
            // descriptor on serial 1522 (consumed by pageRouter.js), NOT by the
            // "FlipsToPageNumber + 120" arithmetic below. The main/keypad sub-page (140 + n) is
            // gone too — that is a VIEW, owned entirely by the HTML side. Dumb panels
            // (HTML_UI == false) never take this fork and are completely unaffected.
            if (this.HTML_UI)
            {
                ushort srcNum = preferredSourceNumber > 0 ? preferredSourceNumber : this.CurrentVSrcNum;
                if (this.CurrentSubsystemIsVideo && pageNumber > 0 && srcNum > 0)
                {
                    BuildAndSendSourceDescriptor(srcNum);
                }
                else
                {
                    // Source off / not on video: clear the source page. The router also drops its
                    // per-source "last view" memory here, so the next selection opens on `main`.
                    SendClearSourceDescriptor();
                }
                return;
            }

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
                    // Dumb panels only — HTML_UI returned at the fork above, so no HTML_UI
                    // branch is needed here any more.
                    ushort subpageScenario = _parent.manager.VideoSourceZ[CurrentVSrcNum].CurrentSubpageScenario;
                    CrestronConsole.PrintLine("TP-{0} Setting DVR subpage: 140 + {1} = {2}", this.Number, subpageScenario, 140 + subpageScenario);
                    this.UserInterface.BooleanInput[(ushort)(140 + subpageScenario)].BoolValue = true;
                    this.UserInterface.SmartObjects[26].BooleanInput[(ushort)(2)].BoolValue = false;
                    this.UserInterface.SmartObjects[26].BooleanInput[(ushort)(4)].BoolValue = false;
                    this.UserInterface.SmartObjects[26].BooleanInput[(ushort)(2 * subpageScenario)].BoolValue = true;
                }
            }
        }

        public void SelectDVRPage()
        {
        }

        /// <summary>
        /// Builds the JSON source descriptor for a MUSIC source and pushes it to this HTML
        /// panel on serial join 1523. `kind` is still carried so the router (and the console
        /// log) never has to infer which map a payload belongs to.
        ///
        /// An empty <paramref name="placement"/> means "no music page visible" and is the
        /// audio analogue of the empty-source video descriptor: it covers source-off,
        /// power-off, close, and the menu/recall suppression above. It is deliberately a
        /// separate field from `source` rather than blanking the source, because the panel
        /// still wants to know WHICH source it would show — that is what lets the media
        /// player keep its identity across a suppression window instead of resetting.
        /// </summary>
        private void BuildAndSendMusicSourceDescriptor(ushort srcNum, string placement)
        {
            string sourceKey = string.Empty;
            ushort scenario = 1;
            string srcName = string.Empty;

            if (srcNum > 0 && _parent.manager.MusicSourceZ.ContainsKey(srcNum))
            {
                var src = _parent.manager.MusicSourceZ[srcNum];
                sourceKey = MusicSourcePageKey(src.Name);
                scenario = src.EffectiveGuiScenario;
                srcName = src.Name;

                if (sourceKey.Length == 0)
                {
                    CrestronConsole.PrintLine(
                        "TP-{0} musicDescriptor: music source {1} name '{2}' matches no known source type - no page will show",
                        this.Number, srcNum, src.Name);
                }
            }

            // No resolvable source ⇒ nothing to place, whatever the caller asked for.
            if (sourceKey.Length == 0) { placement = string.Empty; }

            var sb = new System.Text.StringBuilder();
            sb.Append("{\"kind\":\"audio\"");
            sb.Append(",\"source\":\"").Append(sourceKey).Append("\"");
            sb.Append(",\"scenario\":").Append(scenario);
            sb.Append(",\"placement\":\"").Append(placement).Append("\"");
            sb.Append(",\"srcNum\":").Append(srcNum);
            sb.Append(",\"srcName\":\"").Append(EscapeDescriptorString(srcName)).Append("\"");
            sb.Append(",\"room\":").Append(this.CurrentRoomNum);
            sb.Append("}");
            string json = sb.ToString();

            this.UserInterface.StringInput[MusicSourceDescriptorJoin].StringValue = json;
            CrestronConsole.PrintLine("TP-{0} musicDescriptor -> {1}", this.Number, json);
        }

        /// <summary>
        /// Hides the music source page (media player) in BOTH placements, on either panel
        /// type. The single chokepoint for "close the media player" so callers no longer
        /// clear a range of joins by hand — the HTML path has no such range.
        /// </summary>
        public void ClearMusicSourcePage()
        {
            if (this.HTML_UI)
            {
                BuildAndSendMusicSourceDescriptor(0, string.Empty);
                return;
            }
            for (ushort i = 0; i < 20; i++)
            {
                this.UserInterface.BooleanInput[(ushort)(i + 1011)].BoolValue = false;
            }
        }

        /// <summary>
        /// Shows a music source's page at an explicit placement, bypassing the suppression
        /// guard in musicPageFlips. For DELIBERATE navigation only — the home-music chevron
        /// tap, which happens while the very dialog that guard watches (b 21) is open.
        /// HTML panels only; dumb panels have no descriptor to send.
        /// </summary>
        public void ShowMusicSourceAt(ushort srcNum, string placement)
        {
            if (!this.HTML_UI) return;
            BuildAndSendMusicSourceDescriptor(srcNum, placement);
        }

        /// <summary>
        /// Maps a music source's configured Name to the canonical pageRouter source key.
        ///
        /// Unlike video — where each device type has its own artwork — nearly every music
        /// source renders the SAME ch5-media-player, because the transport controls, artwork
        /// and metadata all arrive on the same joins whether it is Spotify, Pandora, AirPlay
        /// or a music server. So the default is "mediaplayer" and only genuinely different
        /// UIs get their own key. That is why this returns a usable key for unmatched names
        /// while VideoSourcePageKey returns "" — an unknown video device has no page to show,
        /// an unknown music source almost certainly wants the standard player.
        /// </summary>
        private static string MusicSourcePageKey(string sourceName)
        {
            string n = (sourceName ?? string.Empty).ToUpper();
            if (n.Contains("JUKEBOX")) return "jukebox";
            return "mediaplayer";
        }

        public void musicPageFlips(ushort pageNumber)
        {
            musicPageFlips(pageNumber, 0);
        }

        /// <summary>
        /// Flip to a music source's control page (the media player).
        ///
        /// <paramref name="preferredSourceNumber"/> exists for the same reason
        /// videoPageFlips has one: the HTML descriptor NAMES the source, so it must be told
        /// which one rather than re-deriving it from lingering state. Every call site already
        /// has the source in hand — it looked up FlipsToPageNumber from it — so passing it
        /// costs nothing and removes a whole class of "wrong source named" bug. 0 means
        /// "use the current room's source", which is right for the off/close paths.
        /// </summary>
        public void musicPageFlips(ushort pageNumber, ushort preferredSourceNumber)
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

            // Suppress reactive media-player flips while a music selection menu is in use
            // or a whole-house quick-action recall is running. Each of these puts up a
            // modal room/source list and fires many source-change events (one per room
            // checked / recalled); without this the media player pops up over the menu on
            // every change. The subpage-clear above still runs, so the media player stays
            // hidden until the menu clears (its join goes false) or the recall timer
            // finishes — the next flip then shows the correct page. Explicit user actions
            // (chevron tap via LaunchSource) bypass musicPageFlips and drive the placement
            // directly, so those still work while a menu is visible.
            //
            // HTML panels answer this from _openMenus, which is built from what the PANEL
            // reports on serial 1527 (see TouchpanelUI.Menus.cs). That is strictly better than
            // the boolean read it replaces: the old test read back joins this program had
            // written, so a menu the user had closed locally still counted as open and kept
            // suppressing the media player. Dumb panels keep reading the joins.
            //   b 21   home-music control dialog
            //   b 998  share-source menu (no-floor variant)
            //   b 999  share-source menu (floor variant)
            //   b 1500 "Select rooms for:" / add-to-group list (home-music start flow)
            bool menusOpen = this.HTML_UI
                ? AnyMusicMenuOpen()
                : (this.UserInterface.BooleanInput[21].BoolValue
                    || this.UserInterface.BooleanInput[998].BoolValue
                    || this.UserInterface.BooleanInput[999].BoolValue
                    || this.UserInterface.BooleanInput[1500].BoolValue);

            bool musicMenuOrRecallActive =
                   menusOpen
                || _parent.musicSystemControl.RecallMusicPresetTimerBusy;

            // HTML descriptor fork: on HTML panels the music source page is named by a JSON
            // descriptor on serial 1523 (consumed by pageRouter.js), NOT by the
            // "FlipsToPageNumber + 1010/+1020" arithmetic below. Dumb panels (HTML_UI == false)
            // never take this fork and are completely unaffected.
            //
            // Audio needs one field video did not: PLACEMENT. Video's sources each have their
            // own page, so naming the source is enough. Every music source renders the SAME
            // media player, in one of two positions — docked inside the audio subsystem page,
            // or centred over the home page — and that choice is the program's (it follows from
            // which page the panel is on), not the panel's. Hence {source, scenario, placement}
            // rather than the video descriptor's {source, scenario}.
            if (this.HTML_UI)
            {
                bool visible = pageNumber > 0 && !musicMenuOrRecallActive;
                string placement =
                      (visible && this.CurrentSubsystemIsAudio) ? "audiosub"
                    : (visible && isHomePage) ? "home"
                    : string.Empty;

                ushort srcNum = preferredSourceNumber;
                if (srcNum == 0
                    && _parent.manager.RoomZ.ContainsKey(this.CurrentRoomNum))
                {
                    srcNum = _parent.manager.RoomZ[this.CurrentRoomNum].CurrentMusicSrc;
                }
                BuildAndSendMusicSourceDescriptor(srcNum, placement);
                return;
            }

            //clear out any subpages first
            for (ushort i = 0; i < 20; i++)
            {
                this.UserInterface.BooleanInput[(ushort)(i + 1011)].BoolValue = false;
            }

            this.UserInterface.BooleanInput[55].BoolValue = false;//this is the music source subpage for iphone.

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
            // HTML panels own these dropdowns entirely — open/close, the 30-second auto-close,
            // and which scenario's button set to draw (from the room-options descriptor on
            // 1524). Nothing below this line is anything the program acts on; it is all
            // visibility. Returning here is what severs the round trip that previously made
            // closing a menu a press to the processor and a boolean back.
            if (this.HTML_UI) return;

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
