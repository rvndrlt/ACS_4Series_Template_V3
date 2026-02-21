//-----------------------------------------------------------------------
// <copyright file="TouchpanelUI.PageFlips.cs" company="Crestron">
//     Copyright (c) Crestron Electronics. All rights reserved.
// </copyright>
//-----------------------------------------------------------------------
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
        }

        public void subsystemPageFlips(ushort pageNumber)
        {
            CrestronConsole.PrintLine("subsystemPageFlips called for TP-{0} pageNumber: {1}", this.Number, pageNumber);
            
            string subsystemName = "";
            for (ushort i = 1; i <= _parent.manager.SubsystemZ.Count; i++)
            {
                if (_parent.manager.SubsystemZ[i].FlipsToPageNumber == pageNumber)
                {
                    subsystemName = _parent.manager.SubsystemZ[i].Name;
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
            }

            if (subsystemName.ToUpper() == "HVAC" || subsystemName.ToUpper() == "CLIMATE")
            {
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
                    CrestronConsole.PrintLine("subsystem page flips TP-{0} FloorScenario {1} has {2} floors", this.Number, this.FloorScenario, _parent.manager.FloorScenarioZ[this.FloorScenario].IncludedFloors.Count);
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

        public void videoPageFlips(ushort pageNumber)
        {
            // Check if this is a duplicate call with same state - skip to prevent blinking
            // Must check both page number AND subsystem state since the same page number
            // behaves differently based on CurrentSubsystemIsVideo
            if (_lastVideoPageFlip == pageNumber && _lastVideoSubsystemState == this.CurrentSubsystemIsVideo)
            {
                CrestronConsole.PrintLine("TP-{0} videoPageFlips SKIPPED: page={1}, lastPage={2}, isVideo={3}, lastIsVideo={4}", 
                    this.Number, pageNumber, _lastVideoPageFlip, this.CurrentSubsystemIsVideo, _lastVideoSubsystemState);
                return;
            }
            
            CrestronConsole.PrintLine("TP-{0} videoPageFlips EXECUTING: page={1}, lastPage={2}, isVideo={3}, lastIsVideo={4}", 
                this.Number, pageNumber, _lastVideoPageFlip, this.CurrentSubsystemIsVideo, _lastVideoSubsystemState);
            
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
            // Check if this is a duplicate call with same state - skip to prevent blinking
            if (_lastMusicPageFlip == pageNumber && _lastMusicSubsystemState == this.CurrentSubsystemIsAudio)
                return;
            
            _lastMusicPageFlip = pageNumber;
            _lastMusicSubsystemState = this.CurrentSubsystemIsAudio;

            CrestronConsole.PrintLine("TP-{2}, musicPageFlips: {0} currentPageNumber {1} currentSubsystemIsAudio-{3}", pageNumber, this.CurrentPageNumber, this.Number, this.CurrentSubsystemIsAudio);

            for (ushort i = 0; i < 20; i++)
            {
                this.UserInterface.BooleanInput[(ushort)(i + 1011)].BoolValue = false;
            }

            this.UserInterface.BooleanInput[55].BoolValue = false;//this is the music source subpage for iphone.
            if (this.CurrentSubsystemIsAudio)
            {
                if (pageNumber > 0)
                {
                    //show the music source on the rooms music page.
                    this.UserInterface.BooleanInput[(ushort)(pageNumber + 1010)].BoolValue = true;
                }
            }
            else if (this.CurrentPageNumber == (ushort)CurrentPageType.Home)
            {
                //this is to show the music source page on the home page
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
