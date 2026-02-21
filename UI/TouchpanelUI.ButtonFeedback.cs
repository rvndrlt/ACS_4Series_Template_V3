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
                    _HTMLContract.musicSourceSelect[buttonNumber - 1].musicSourceSelected((sig, source) =>
                    {
                        sig.BoolValue = true;
                    });
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
            // Skip if same as last update to prevent blinking
            if (_lastVideoButtonFB == buttonNumber)
                return;
            _lastVideoButtonFB = buttonNumber;

            CrestronConsole.PrintLine("videoButtonFB: {0}", buttonNumber);

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
                    // TODO - implement Music Floor Contract
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
                    // TODO - implement Music Floor Contract
                }
                else
                {
                    this.UserInterface.SmartObjects[9].BooleanInput[(ushort)(buttonNumber + 10)].BoolValue = true;
                }
            }
        }
    }
}
