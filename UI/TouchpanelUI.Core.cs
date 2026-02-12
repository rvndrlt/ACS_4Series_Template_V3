//-----------------------------------------------------------------------
// <copyright file="TouchpanelUI.Core.cs" company="Crestron">
//     Copyright (c) Crestron Electronics. All rights reserved.
// </copyright>
//-----------------------------------------------------------------------
using System;
using System.Collections.Generic;
using ACS_4Series_Template_V3.Room;
using Ch5_Sample_Contract;
using Crestron.SimplSharp;
using Crestron.SimplSharp.CrestronIO;
using Crestron.SimplSharp.Reflection;
using Crestron.SimplSharpPro;
using Crestron.SimplSharpPro.DeviceSupport;
using Crestron.SimplSharpPro.UI;

namespace ACS_4Series_Template_V3.UI
{
    /// <summary>
    /// Allows us to instantiate and register a touchpanel dynamically
    /// </summary>
    public partial class TouchpanelUI
    {
        #region Private Fields
        private bool _previousBool100State = false;
        private CTimer _sleepFormatLiftTimer;
        private CTimer _connectionStatusCheckTimer;
        private DeviceExtender _ethernetExtender;
        private RoomConfig currentSubscribedRoom;
        private CTimer _sharingMenuTimer;
        private Action<ushort, ushort, ushort, string, ushort> MusicSourceNameUpdateHandler;
        private Action<ushort> _currentSetpointHandler;
        private ControlSystem _parent;
        private const string LogHeader = "[UI] ";
        private readonly Dictionary<ushort, Action<ushort, string>> _roomSubsystemSubscriptions = new Dictionary<ushort, Action<ushort, string>>();
        private readonly Dictionary<ushort, Action<ushort, string>> _roomListStatusSubscriptions = new Dictionary<ushort, Action<ushort, string>>();
        private Dictionary<ushort, Action<ushort, ushort, ushort, string, ushort>> _musicSharingChangeHandlers = new Dictionary<ushort, Action<ushort, ushort, ushort, string, ushort>>();
        private EventHandler _currentRoomVolumeHandler;
        #endregion

        #region Public Fields
        public Contract _HTMLContract;
        public BasicTriListWithSmartObject UserInterface;
        public Assembly TpAssembly;
        public CrestronControlSystem CS;
        public List<ushort> WholeHouseRoomList = new List<ushort>();
        public List<ushort> MusicRoomsToShareSourceTo = new List<ushort>();
        public List<bool> MusicRoomsToShareCheckbox = new List<bool>();
        #endregion

        #region Enums
        public enum CurrentPageType
        {
            Home = 0,
            RoomList = 1,
            RoomSubsystemList = 2,
            SubsystemPage = 3,
        }

        public enum CurrentSubsystemType
        {
            Audio = 1,
            Video = 2,
            Lights = 3,
            Shades = 4,
            Climate = 5,
            Security = 6,
            Spa = 7,
            Pool = 8,
            Camera = 9,
            Other = 10
        }

        public enum SmartObjectIDs
        {
            mediaPlayer = 1,
            subsystemSelect = 2,
            floorSelect = 3,
            zoneSelect = 4,
            videoSources = 5,
            musicSources = 6,
            musicMenu = 7,
            lightingButtons = 8,
            musicFloorSelect = 9,
            wholeHouseZoneList = 10,
            dpad = 11,
            cameraKeypad = 12,
            xmKeypad = 13,
            wholeHouseSubsystems = 14,
            quickActions = 15,
            kscapeKeypad = 16,
            lightingModes = 18,
            securityPartitions = 19,
            securityKeypad = 20,
            securityZoneList = 21,
            spa = 22,
            poolTab = 23,
            TVpresets = 24,
            DVRKeypad = 25,
            DVRTab = 26,
            quickViewSubsystems = 27,
            quickActionViewStatus = 28,
            quickActionSaveCheckbox = 29,
            quickActionMusic = 30,
            quickActionClimate = 31,
            cameraSelect = 33,
            videoDisplays = 34,
            poolLights = 35,
        }
        #endregion

        #region Properties
        public Action<ushort, string> CurrentClimateSubscription { get; set; }
        public ushort Number { get; set; }
        public uint Ipid { get; set; }
        public string Type { get; set; }
        public string Name { get; set; }
        public bool HTML_UI { get; set; }
        public ushort HomePageScenario { get; set; }
        public ushort SubSystemScenario { get; set; }
        public ushort FloorScenario { get; set; }
        public ushort DefaultRoom { get; set; }
        public ushort DefaultDisplay { get; set; }
        public ushort CurrentFloorNum { get; set; }
        public ushort CurrentClimateID { get; set; }
        public ushort CurrentMusicFloorNum { get; set; }
        public ushort CurrentRoomNum { get; set; }
        public bool ChangeRoomButtonEnable { get; set; }
        public string ChangeRoomButtonText { get; set; }
        public ushort CurrentDisplayNumber { get; set; }
        public bool UseAnalogModes { get; set; }
        public bool DontInheritSubsystemScenario { get; set; }
        public bool IsConnectedRemotely { get; set; }
        public bool SrcSharingButtonFB { get; set; }
        public ushort CurrentVSrcGroupNum { get; set; }
        public ushort CurrentVSrcNum { get; set; }
        public ushort CurrentVideoPageNumber { get; set; }
        public ushort CurrentASrcGroupNum { get; set; }
        public ushort CurrentPageNumber { get; set; }
        public ushort CurrentSubsystemNumber { get; set; }
        public bool CurrentSubsystemIsVideo { get; set; }
        public bool CurrentSubsystemIsAudio { get; set; }
        public bool CurrentSubsystemIsLights { get; set; }
        public bool CurrentSubsystemIsShades { get; set; }
        public bool CurrentSubsystemIsClimate { get; set; }
        public Dictionary<RoomConfig, EventHandler> MuteChangeHandlers { get; } = new Dictionary<RoomConfig, EventHandler>();
        public Dictionary<RoomConfig, EventHandler> VolumeChangeHandlers { get; } = new Dictionary<RoomConfig, EventHandler>();
        #endregion

        #region Constructor
        public TouchpanelUI(ushort number, uint ipid, string type, string name, bool HTML_UI, ushort homePageScenario, ushort subSystemScenario, ushort floorScenario, ushort defaultRoom, ushort defaultDisplay, bool changeRoomButtonEnable, string changeRoomButtonText, bool useAnalogModes, bool dontInheritSubsystemScenario, CrestronControlSystem cs)
        {
            this.Number = number;
            this.Ipid = ipid;
            this.Type = type;
            this.Name = name;
            this.HTML_UI = HTML_UI;
            this.HomePageScenario = homePageScenario;
            this.SubSystemScenario = subSystemScenario;
            this.FloorScenario = floorScenario;
            this.DefaultRoom = defaultRoom;
            this.DefaultDisplay = defaultDisplay;
            this.ChangeRoomButtonEnable = changeRoomButtonEnable;
            this.ChangeRoomButtonText = changeRoomButtonText;
            this.UseAnalogModes = useAnalogModes;
            this.DontInheritSubsystemScenario = dontInheritSubsystemScenario;
            this.CS = cs;
        }
        #endregion

        #region Registration
        public bool Register()
        {
            try
            {
                _parent = this.CS as ControlSystem;
                var uiObject = this.RetrieveUiObject(this.Type, this.Ipid);
                this.UserInterface = uiObject;
                if (this.UserInterface == null)
                {
                    return false;
                }
                this.UserInterface.Description = this.Name;

                if (this.Type.Equals("CrestronApp", StringComparison.OrdinalIgnoreCase))
                {
                    SetupCrestronApp();
                }

                this.UserInterface.SigChange += this.UserInterfaceObject_SigChange;
                this.UserInterface.OnlineStatusChange += this.ConnectionStatusChange;
                
                if (this.HTML_UI)
                {
                    _HTMLContract = new Contract();
                    _HTMLContract.AddDevice(this.UserInterface);
                    _parent.userInterfaceControl.SubscribeToContractEvents(this);
                }
                else
                {
                    string sgdPath = Path.Combine(Directory.GetApplicationDirectory(), "TSW-770-DARK.sgd");
                    this.UserInterface.LoadSmartObjects(sgdPath);
                    foreach (KeyValuePair<uint, SmartObject> smartObject in this.UserInterface.SmartObjects)
                    {
                        smartObject.Value.SigChange += new SmartObjectSigChangeEventHandler(this.SmartObject_SigChange);
                    }
                }

                if (this.UserInterface.Register() != eDeviceRegistrationUnRegistrationResponse.Success)
                {
                    ErrorLog.Error(LogHeader + "Error registring UI {0}", this.Name);
                    return false;
                }
                else
                {
                    this.CurrentPageNumber = 2;
                    this.UserInterface.BooleanInput[12].BoolValue = true;
                    return true;
                }
            }
            catch (Exception e)
            {
                ErrorLog.Error(LogHeader + "Exception when trying to register UI {0}: {1}\nInner Exception: {2}", this.Name, e.Message, e.InnerException != null ? e.InnerException.Message : "No inner exception");
                CrestronConsole.PrintLine(LogHeader + "Exception when trying to register UI {0}: {1}\nInner Exception: {2}", this.Name, e.Message, e.InnerException != null ? e.InnerException.Message : "No inner exception");
                return false;
            }
        }

        private void SetupCrestronApp()
        {
            if (this.UserInterface is BasicTriListWithSmartObject uiWithSmartObject)
            {
                CrestronConsole.PrintLine("BasicTriListWithSmartObject detected: {0}", uiWithSmartObject.Name);
                var app = this.UserInterface as CrestronApp;
                if (app != null)
                {
                    _ethernetExtender = app.ExtenderEthernetReservedSigs;
                    if (_ethernetExtender != null)
                    {
                        _ethernetExtender.DeviceExtenderSigChange += this.RemoteAddressConnectionStatusChange;
                        _ethernetExtender.Use();
                        CrestronConsole.PrintLine(LogHeader + "Subscribed to DeviceExtenderSigChange - extender: {0}", _ethernetExtender.GetHashCode());
                        CrestronConsole.PrintLine(LogHeader + "Initial connection states - Address1: {0}, Address2: {1}",
                            app.ExtenderEthernetReservedSigs.ConnectedToAddress1Feedback.BoolValue,
                            app.ExtenderEthernetReservedSigs.ConnectedToAddress2Feedback.BoolValue);
                        _connectionStatusCheckTimer = new CTimer(PollConnectionStatus, null, 2000, 2000);
                    }
                    else
                    {
                        CrestronConsole.PrintLine(LogHeader + "ERROR: ExtenderEthernetReservedSigs is null!");
                    }
                }
            }
            else
            {
                CrestronConsole.PrintLine("ExtenderEthernetReservedSigs is not applicable for this type.");
            }

            try
            {
                CrestronConsole.PrintLine("CrestronApp detected, getting all properties: {0}-{1}", this.UserInterface.Description, this.UserInterface.Name);
                foreach (var prop in this.UserInterface.GetType().GetProperties())
                {
                    try
                    {
                        var value = prop.GetValue(this.UserInterface);
                    }
                    catch
                    {
                        CrestronConsole.PrintLine("- {0} = [error retrieving value]", prop.Name);
                    }
                }

                var paramProjectName = this.UserInterface.GetType().GetProperty("ParameterProjectName");
                if (paramProjectName != null)
                {
                    var projectNameValue = paramProjectName.GetValue(this.UserInterface);
                    CrestronConsole.PrintLine("ParameterProjectName before setting: {0}", projectNameValue);

                    var valueProperty = projectNameValue.GetType().GetProperty("Value");
                    if (valueProperty != null)
                    {
                        if (this.UserInterface.Description.ToUpper().Contains("IPHONE"))
                        {
                            valueProperty.SetValue(projectNameValue, "IPHONE-DARK");
                        }
                        else
                        {
                            valueProperty.SetValue(projectNameValue, "IPAD-DARK");
                        }

                        var afterValue = valueProperty.GetValue(projectNameValue);
                        CrestronConsole.PrintLine("ParameterProjectName.Value after setting: {0}", afterValue);
                    }
                    else
                    {
                        CrestronConsole.PrintLine("Value property not found on {0}", projectNameValue.GetType().Name);
                        foreach (var p in projectNameValue.GetType().GetProperties())
                        {
                            CrestronConsole.PrintLine("Available property: {0}", p.Name);
                        }
                    }
                }
                else
                {
                    CrestronConsole.PrintLine("ParameterProjectName property not found.");
                }
            }
            catch (Exception ex)
            {
                CrestronConsole.PrintLine("Error setting project name: " + ex.Message);
            }
        }

        public BasicTriListWithSmartObject RetrieveUiObject(string touchpanelType, uint deviceId)
        {
            try
            {
                string assemblyPath = Path.Combine(Directory.GetApplicationDirectory(), "Crestron.SimplSharpPro.UI.dll");
                this.TpAssembly = Assembly.LoadFrom(assemblyPath);
                string assembly = string.Format("Crestron.SimplSharpPro.UI.{0}", touchpanelType);
                CType cswitcher = this.TpAssembly.GetType(assembly);
                if (cswitcher == null)
                {
                    CrestronConsole.PrintLine(LogHeader + "Unable to find type: {0}", assembly);
                    return null;
                }

                CType[] constructorTypes = new CType[] { typeof(uint), typeof(CrestronControlSystem) };
                ConstructorInfo cinfo = cswitcher.GetConstructor(constructorTypes);
                if (cinfo != null)
                    CrestronConsole.PrintLine("---cinfo.Attributes:{0} name:{1} reflected:{2} || ID{3}", cinfo.Attributes, cinfo.Name, cinfo.ReflectedType, deviceId);
                else
                    CrestronConsole.PrintLine("cinfo NULL");

                if (this.CS == null)
                {
                    CrestronConsole.PrintLine(LogHeader + "CrestronControlSystem (this.CS) is null");
                    return null;
                }

                var instance = (BasicTriListWithSmartObject)cinfo.Invoke(new object[] { deviceId, this.CS });
                return instance;
            }
            catch (Exception e)
            {
                ErrorLog.Error(LogHeader + "Unable to create TP: {0}\nInner Exception: {1}", e.Message, e.InnerException != null ? e.InnerException.Message : "No inner exception");
                CrestronConsole.PrintLine(LogHeader + "Unable to create TP: {0}\nInner Exception: {1}", e.Message, e.InnerException != null ? e.InnerException.Message : "No inner exception");
                return null;
            }
        }
        #endregion

        #region Connection Status
        private void RemoteAddressConnectionStatusChange(DeviceExtender currentDevice, SigEventArgs args)
        {
            CrestronConsole.PrintLine("~~~~Remote Address Connection Status Changed: {0} {1}", currentDevice, args.Sig.Number);

            var app = this.UserInterface as CrestronApp;
            if (app != null && _ethernetExtender != null)
            {
                CrestronConsole.PrintLine(LogHeader + "Connection Status - Address1: {0}, Address2: {1}",
                    app.ExtenderEthernetReservedSigs.ConnectedToAddress1Feedback?.BoolValue,
                    app.ExtenderEthernetReservedSigs.ConnectedToAddress2Feedback?.BoolValue);

                this.IsConnectedRemotely = app.ExtenderEthernetReservedSigs.ConnectedToAddress2Feedback?.BoolValue ?? false;
                CrestronConsole.PrintLine(LogHeader + "App {0} is {1} connected remotely",
                    this.Name, this.IsConnectedRemotely ? "now" : "NOT");
            }
        }

        private void ConnectionStatusChange(GenericBase currentDevice, OnlineOfflineEventArgs args)
        {
            CrestronConsole.PrintLine(LogHeader + "Connection Status Changed: {0} {1}", currentDevice.Name, args.DeviceOnLine);
        }

        private void PollConnectionStatus(object userObject)
        {
            try
            {
                var app = this.UserInterface as CrestronApp;
                if (app == null)
                {
                    CrestronConsole.PrintLine("app null {0}", this.Name);
                }
                else if (_ethernetExtender == null)
                {
                    CrestronConsole.PrintLine("extender null {0}", this.Name);
                }
                else
                {
                    bool isRemote = app.ExtenderEthernetReservedSigs.ConnectedToAddress2Feedback?.BoolValue ?? false;
                    if (this.IsConnectedRemotely != isRemote)
                    {
                        this.IsConnectedRemotely = isRemote;
                        CrestronConsole.PrintLine(LogHeader + "Connection status changed detected by polling: {0} is {1} connected remotely",
                            this.Name, isRemote ? "now" : "NOT");
                    }
                }
            }
            catch (Exception ex)
            {
                ErrorLog.Error(LogHeader + "Error in connection polling: {0}", ex.Message);
            }
        }
        #endregion

        #region Helper Classes
        public class TrackedValue<T>
        {
            public T Value { get; set; }

            public TrackedValue(T initialValue)
            {
                Value = initialValue;
            }
        }
        #endregion

        #region Utility Methods
        private string stripHTMLTags(string input)
        {
            return System.Text.RegularExpressions.Regex.Replace(input, "<.*?>", string.Empty);
        }

        public ushort calculateRampTime(ushort startValue, ushort endValue, ushort time)
        {
            ushort rampTime = 0;
            if (startValue > endValue)
            {
                rampTime = (ushort)((startValue - endValue) * time / 65535);
            }
            else
            {
                rampTime = (ushort)((endValue - startValue) * time / 65535);
            }
            return rampTime;
        }
        #endregion
    }
}
