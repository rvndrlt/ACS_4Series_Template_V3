//-----------------------------------------------------------------------
// <copyright file="TouchpanelUI.ButtonFeedback.cs" company="Crestron">
//     Copyright (c) Crestron Electronics. All rights reserved.
// </copyright>
//-----------------------------------------------------------------------
using Crestron.SimplSharp;

namespace ACS_4Series_Template_V3.UI
{
    /// <summary>
    /// Button feedback handling for TouchpanelUI
    /// </summary>
    public partial class TouchpanelUI
    {
        // Track last feedback values to prevent duplicate updates
        private ushort _lastMusicButtonFB = 0;
        private ushort _lastVideoButtonFB = 0;

        // Analog-mode (TSR-310) video source buttons: six direct joins per page.
        // 501-506 = selected feedback, 521-526 = "in use" lamp, paged by CurrentVSrcGroupNum.
        private const ushort VideoSourceButtonsPerGroup = 6;
        private const ushort VideoSourceSelectedJoinBase = 501;
        private const ushort VideoSourceInUseJoinBase = 521;

        /// <summary>
        /// Music SOURCE button feedback
        /// </summary>
        /// <param name="buttonNumber">Button number to highlight (0 to clear all)</param>
        public void musicButtonFB(ushort buttonNumber)
        {
            // Skip if same as last update to prevent blinking
            if (_lastMusicButtonFB == buttonNumber)
                return;
            _lastMusicButtonFB = buttonNumber;

            for (ushort i = 0; i < 20; i++)
            {
                if (this.HTML_UI)
                {
                    _HTMLContract.musicSourceSelect[i].musicSourceSelected((sig, source) =>
                    {
                        sig.BoolValue = false;
                    });
                }
                else
                {
                    this.UserInterface.SmartObjects[6].BooleanInput[(ushort)(i + 11)].BoolValue = false;
                }
            }
            this.UserInterface.BooleanInput[1001].BoolValue = false;

            if (buttonNumber > 0)
            {
                if (this.HTML_UI)
                {
                    if (buttonNumber - 1 < _HTMLContract.musicSourceSelect.Length)
                    {
                        _HTMLContract.musicSourceSelect[buttonNumber - 1].musicSourceSelected((sig, source) =>
                        {
                            sig.BoolValue = true;
                        });
                    }
                }
                else
                {
                    this.UserInterface.SmartObjects[6].BooleanInput[(ushort)(buttonNumber + 10)].BoolValue = true;
                }

                ushort asrcSharingScenario = _parent.manager.RoomZ[this.CurrentRoomNum].AudioSrcSharingScenario;
                if (asrcSharingScenario > 0)
                {
                    this.UserInterface.BooleanInput[1001].BoolValue = true;
                }
            }
        }

        /// <summary>
        /// Video SOURCE button feedback
        /// </summary>
        /// <param name="buttonNumber">Button number to highlight (0 to clear all)</param>
        public void videoButtonFB(ushort buttonNumber)
        {
            // In-use lamps first: they are driven by OTHER rooms' activity, so they can change
            // while this panel's own selection does not. Refreshing them before the
            // same-value early-out below means a repeat call still updates them.
            videoSourceInUseFB();

            // Skip if same as last update to prevent blinking
            if (_lastVideoButtonFB == buttonNumber)
                return;
            _lastVideoButtonFB = buttonNumber;

            CrestronConsole.PrintLine("videoButtonFB: {0}", buttonNumber);

            // Analog-mode panels (TSR-310) have no source smart object — the six source buttons
            // are direct joins 501-506, paged six at a time by CurrentVSrcGroupNum. This used to
            // fall through to the SmartObjects[5] writes below, which the TSR panel does not
            // have, so a source selected FROM the panel never lit until something called
            // SetVSRCGroup — i.e. until the user left the video menu and came back.
            if (this.UseAnalogModes)
            {
                ushort group = this.CurrentVSrcGroupNum > 0 ? this.CurrentVSrcGroupNum : (ushort)1;
                ushort firstInGroup = (ushort)((group - 1) * VideoSourceButtonsPerGroup);
                for (ushort i = 0; i < VideoSourceButtonsPerGroup; i++)
                {
                    // buttonNumber is 1-based over the ROOM's whole source list; the join is the
                    // slot within the page currently shown.
                    bool on = buttonNumber > firstInGroup
                              && buttonNumber <= firstInGroup + VideoSourceButtonsPerGroup
                              && buttonNumber - firstInGroup == i + 1;
                    this.UserInterface.BooleanInput[(ushort)(VideoSourceSelectedJoinBase + i)].BoolValue = on;
                }
                return;
            }

            for (ushort i = 0; i < 20; i++)
            {
                if (this.HTML_UI)
                {
                    this._HTMLContract.vsrcButton[i].vidSourceIsSelected((sig, source) =>
                    {
                        sig.BoolValue = false;
                    });
                }
                else
                {
                    this.UserInterface.SmartObjects[5].BooleanInput[(ushort)(i + 11)].BoolValue = false;
                }
            }

            if (buttonNumber > 0)
            {
                if (this.HTML_UI)
                {
                    this._HTMLContract.vsrcButton[buttonNumber - 1].vidSourceIsSelected((sig, source) =>
                    {
                        sig.BoolValue = true;
                    });
                }
                else
                {
                    this.UserInterface.SmartObjects[5].BooleanInput[(ushort)(buttonNumber + 10)].BoolValue = true;
                }
            }
        }

        /// <summary>
        /// Refresh the "in use" lamps (joins 521-526) next to the video source buttons on an
        /// analog-mode panel (TSR-310). A source goes in/out of use when ANY room selects or
        /// drops it, which is why this is separate from videoButtonFB's selected feedback and
        /// is not gated on this panel's own selection changing. No-op on other panel types,
        /// whose source lists do not carry an in-use indicator.
        /// </summary>
        public void videoSourceInUseFB()
        {
            if (!this.UseAnalogModes) { return; }
            try
            {
                if (!_parent.manager.RoomZ.ContainsKey(this.CurrentRoomNum)) { return; }
                ushort scenario = _parent.manager.RoomZ[this.CurrentRoomNum].VideoSrcScenario;
                if (scenario == 0 || !_parent.manager.VideoSrcScenarioZ.ContainsKey(scenario)) { return; }

                var sources = _parent.manager.VideoSrcScenarioZ[scenario].IncludedSources;
                ushort group = this.CurrentVSrcGroupNum > 0 ? this.CurrentVSrcGroupNum : (ushort)1;
                ushort firstInGroup = (ushort)((group - 1) * VideoSourceButtonsPerGroup);
                for (ushort i = 0; i < VideoSourceButtonsPerGroup; i++)
                {
                    ushort listIndex = (ushort)(firstInGroup + i);
                    bool inUse = listIndex < sources.Count
                                 && _parent.manager.VideoSourceZ.ContainsKey(sources[listIndex])
                                 // A local source (no distributed switcher input) is never shared,
                                 // so it can never be "in use" by another room no matter what the
                                 // flag says. Same rule as RecalculateVideoSourceInUse.
                                 && _parent.manager.VideoSourceZ[sources[listIndex]].VidSwitcherInputNumber > 0
                                 && _parent.manager.VideoSourceZ[sources[listIndex]].InUse;
                    this.UserInterface.BooleanInput[(ushort)(VideoSourceInUseJoinBase + i)].BoolValue = inUse;
                }
            }
            catch (System.Exception ex)
            {
                ErrorLog.Error("Error in videoSourceInUseFB for TP-{0}: {1}", this.Number, ex.Message);
            }
        }

        /// <summary>
        /// Reset video button feedback tracking (call when changing rooms)
        /// </summary>
        public void ResetVideoButtonFBTracking()
        {
            _lastVideoButtonFB = ushort.MaxValue;
        }

        /// <summary>
        /// Reset music button feedback tracking (call when changing rooms)
        /// </summary>
        public void ResetMusicButtonFBTracking()
        {
            _lastMusicButtonFB = ushort.MaxValue;
        }

        /// <summary>
        /// Floor selection button feedback
        /// </summary>
        /// <param name="buttonNumber">Button number to highlight (0 to clear all)</param>
        public void floorButtonFB(ushort buttonNumber)
        {
            for (ushort i = 0; i < 10; i++)
            {
                if (this.HTML_UI)
                {
                    this._HTMLContract.FloorSelect[i].FloorIsSelected((sig, source) =>
                    {
                        sig.BoolValue = false;
                    });
                }
                else
                {
                    this.UserInterface.SmartObjects[3].BooleanInput[(ushort)(i + 11)].BoolValue = false;
                }
            }

            if (buttonNumber > 0)
            {
                if (this.HTML_UI)
                {
                    this._HTMLContract.FloorSelect[buttonNumber - 1].FloorIsSelected((sig, source) =>
                    {
                        sig.BoolValue = true;
                    });
                }
                else
                {
                    this.UserInterface.SmartObjects[3].BooleanInput[(ushort)(buttonNumber + 10)].BoolValue = true;
                }
            }
        }

        /// <summary>
        /// Music floor selection button feedback
        /// </summary>
        /// <param name="buttonNumber">Button number to highlight (0 to clear all)</param>
        public void musicFloorButtonFB(ushort buttonNumber)
        {
            for (ushort i = 0; i < 10; i++)
            {
                if (this.HTML_UI)
                {
                    if (i < this._HTMLContract.FloorSelect.Length)
                    {
                        int ci = i;
                        this._HTMLContract.FloorSelect[ci].FloorIsSelected(
                            (sig, wh) => sig.BoolValue = false);
                    }
                }
                else
                {
                    this.UserInterface.SmartObjects[9].BooleanInput[(ushort)(i + 11)].BoolValue = false;
                }
            }

            if (buttonNumber > 0)
            {
                if (this.HTML_UI)
                {
                    int idx = buttonNumber - 1;
                    if (idx >= 0 && idx < this._HTMLContract.FloorSelect.Length)
                    {
                        this._HTMLContract.FloorSelect[idx].FloorIsSelected(
                            (sig, wh) => sig.BoolValue = true);
                    }
                }
                else
                {
                    this.UserInterface.SmartObjects[9].BooleanInput[(ushort)(buttonNumber + 10)].BoolValue = true;
                }
            }
        }
    }
}
