//-----------------------------------------------------------------------
// <copyright file="TouchpanelUI.cs" company="Crestron">
//     Copyright (c) Crestron Electronics. All rights reserved.
// </copyright>
//-----------------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using ACS_4Series_Template_V3.Room;


//using System.Threading.Tasks;
using Crestron.SimplSharp;                       // For Basic SIMPL# Classes
using Crestron.SimplSharp.CrestronIO;            // For Directory
using Crestron.SimplSharp.Reflection;
using Crestron.SimplSharpPro;                    // For Basic SIMPL#Pro classes
using Crestron.SimplSharpPro.CrestronThread;     // For Threading
using Crestron.SimplSharpPro.DeviceSupport;      // For Generic Device Support
using Crestron.SimplSharpPro.Diagnostics;
using Crestron.SimplSharpPro.UI;        // For System Monitor Access

namespace ACS_4Series_Template_V3.UI
{
    /// <summary>
    /// Allows us to instantiate and register a touchpanel dynamically
    /// </summary>
    public class TouchpanelUI
    {
        private CTimer _sleepFormatLiftTimer;
        private CTimer _connectionStatusCheckTimer;
        private DeviceExtender _ethernetExtender;
        private RoomConfig currentSubscribedRoom;
        public enum CurrentPageType
        {
            Home = 0, // 0 = HOME
            RoomList = 1, // 1 = RoomListPage
            RoomSubsystemList = 2, // 2 = RoomSubsystemListPage
            SubsystemPage = 3, // 3 = SubsystemPage
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
            securityPartitions = 19,//shades
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


        /// <summary>
        /// Hardware interface for touchpanel
        /// </summary>
        public BasicTriListWithSmartObject UserInterface;

        /// <summary>
        /// Used by reflection to load Crestron.SimplSharpPro.UI.dll
        /// </summary>
        public Assembly TpAssembly;

        /// <summary>
        /// CrestronControlSystem
        /// </summary>
        public CrestronControlSystem CS;

        /// <summary>
        /// Used for logging information to error log
        /// </summary>
        private const string LogHeader = "[UI] ";

        private readonly Dictionary<ushort, Action<ushort, string>> _roomSubsystemSubscriptions = new Dictionary<ushort, Action<ushort, string>>();
        private readonly Dictionary<ushort, Action<ushort, string>> _roomListStatusSubscriptions = new Dictionary<ushort, Action<ushort, string>>();

        /// <summary>
        /// Initializes a new instance of the TouchpanelUI class
        /// </summary>
        /// <param name="type">Touchpanel type (ie. Tsw760)</param>
        /// <param name="id">IPID</param>
        /// <param name="label">Label you want to show up in the IPTable</param>
        /// <param name="cs">CrestronControlSystem</param>
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

        private ControlSystem _parent;
        
        public ushort Number { get; set; }
        /// <summary>
        /// Gets or sets IPID of the touchpanel
        /// </summary>
        public uint Ipid { get; set; }
        /// <summary>
        /// Gets or sets type of touchpanel. Can be "Tsw760", "Tsw1060", etc
        /// Keep in mind that this will not be checked before trying to load
        /// Yes, that can be improved ;)
        /// </summary>
        public string Type { get; set; }
        


        /// <summary>
        /// Gets or sets label you want to show in IPTable
        /// </summary>
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

        public ushort CurrentPageNumber { get; set; }// 0 = HOME, 1 = RoomList, 2 = RoomSubsystemList
        public ushort CurrentSubsystemNumber { get; set; }
        public bool CurrentSubsystemIsVideo { get; set; }

        public bool CurrentSubsystemIsAudio { get; set; }
        public bool CurrentSubsystemIsLights { get; set; }
        public bool CurrentSubsystemIsShades { get; set; }
        public bool CurrentSubsystemIsClimate { get; set; }

        public List<ushort> WholeHouseRoomList = new List<ushort>();
        public List<ushort> MusicRoomsToShareSourceTo = new List<ushort>();
        public List<bool> MusicRoomsToShareCheckbox = new List<bool>();

            /// <summary>
            /// Dictionary to manage event subscriptions
            /// </summary>

        public Dictionary<Room.RoomConfig, EventHandler> MuteChangeHandlers { get; } = new Dictionary<Room.RoomConfig, EventHandler>();
        public Dictionary<Room.RoomConfig, EventHandler> VolumeChangeHandlers { get; } = new Dictionary<Room.RoomConfig, EventHandler>();

        /// <summary>
        /// Register the touchpanel using the proper information
        /// </summary>
        /// <returns>true or false, depending on if the registration succeeded</returns>
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
                    if (this.UserInterface is BasicTriListWithSmartObject uiWithSmartObject)
                    {
                        // Perform operations specific to BasicTriListWithSmartObject
                        CrestronConsole.PrintLine("BasicTriListWithSmartObject detected: {0}", uiWithSmartObject.Name);
                        var app = this.UserInterface as CrestronApp;
                        if (app != null) {
                            _ethernetExtender = app.ExtenderEthernetReservedSigs;
                            if (_ethernetExtender != null)
                            {
                                _ethernetExtender.DeviceExtenderSigChange += this.RemoteAddressConnectionStatusChange;
                                _ethernetExtender.Use();
                                CrestronConsole.PrintLine(LogHeader + "Subscribed to DeviceExtenderSigChange - extender: {0}", _ethernetExtender.GetHashCode());

                                // Check initial connection state
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
                                //CrestronConsole.PrintLine("- {0} = {1}", prop.Name, value);
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

                            // Get the Value property of the StringParameterValue object
                            var valueProperty = projectNameValue.GetType().GetProperty("Value");
                            if (valueProperty != null)
                            {
                                // Set the Value property
                                if (this.UserInterface.Description.ToUpper().Contains("IPHONE")) {
                                    valueProperty.SetValue(projectNameValue, "IPHONE-DARK");
                                }
                                else { 
                                    valueProperty.SetValue(projectNameValue, "IPAD-DARK");
                                }
                                //CrestronConsole.PrintLine("Set project name to IPAD-DARK via ParameterProjectName.Value");

                                // Verify the value was set
                                var afterValue = valueProperty.GetValue(projectNameValue);
                                CrestronConsole.PrintLine("ParameterProjectName.Value after setting: {0}", afterValue);
                            }
                            else
                            {
                                CrestronConsole.PrintLine("Value property not found on {0}", projectNameValue.GetType().Name);

                                // Print all properties of the projectNameValue object
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

                this.UserInterface.SigChange += this.UserInterfaceObject_SigChange;
                this.UserInterface.OnlineStatusChange += this.ConnectionStatusChange;

                // load smart objects
                string sgdPath = Path.Combine(Directory.GetApplicationDirectory(), "TSW-770-DARK.sgd");

                this.UserInterface.LoadSmartObjects(sgdPath);

                //ErrorLog.Notice(string.Format(LogHeader + "Loaded SmartObjects: {0}", this.UserInterface.SmartObjects.Count));
                foreach (KeyValuePair<uint, SmartObject> smartObject in this.UserInterface.SmartObjects)
                {
                    smartObject.Value.SigChange += new Crestron.SimplSharpPro.SmartObjectSigChangeEventHandler(this.SmartObject_SigChange);
                }
                //testFunction(this.UserInterface);
                if (this.UserInterface.Register() != Crestron.SimplSharpPro.eDeviceRegistrationUnRegistrationResponse.Success)
                {
                    ErrorLog.Error(LogHeader + "Error registring UI {0}", this.Name);
                    return false;
                }
                else
                {

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
        private void RemoteAddressConnectionStatusChange(DeviceExtender currentDevice, SigEventArgs args)
        {
            CrestronConsole.PrintLine("~~~~Remote Address Connection Status Changed: {0} {1}", currentDevice, args.Sig.Number);

            // Use the app directly to check connection status
            var app = this.UserInterface as CrestronApp;
            if (app != null && _ethernetExtender != null)
            {
                // Log the status of all connection feedback signals
                CrestronConsole.PrintLine(LogHeader + "Connection Status - Address1: {0}, Address2: {1}",
                    app.ExtenderEthernetReservedSigs.ConnectedToAddress1Feedback?.BoolValue,
                    app.ExtenderEthernetReservedSigs.ConnectedToAddress2Feedback?.BoolValue);

                // Update the remote connection status based on the current feedback, not the signal number
                this.IsConnectedRemotely = app.ExtenderEthernetReservedSigs.ConnectedToAddress2Feedback?.BoolValue ?? false;
                CrestronConsole.PrintLine(LogHeader + "App {0} is {1} connected remotely",
                    this.Name, this.IsConnectedRemotely ? "now" : "NOT");
            }
        }


        private void ConnectionStatusChange(GenericBase currentDevice, OnlineOfflineEventArgs args)
        {
            CrestronConsole.PrintLine(LogHeader + "Connection Status Changed: {0} {1}", currentDevice.Name, args.DeviceOnLine);

        }

        /// <summary>
        /// Instantiates the touchpanel properly
        /// </summary>
        /// <param name="touchpanelType">Type of touchpanel (Tsw760, Tsw1060,etc)</param>
        /// <param name="deviceId">IPID of touchpanel</param>
        /// <returns>BasicTriListWithSmartObject to be used genericly</returns>
        public BasicTriListWithSmartObject RetrieveUiObject(string touchpanelType, uint deviceId)
        {
            try
            {
                string assemblyPath = Path.Combine(Directory.GetApplicationDirectory(), "Crestron.SimplSharpPro.UI.dll");
                this.TpAssembly = Assembly.LoadFrom(assemblyPath);
                // add the correct device type that we want to reflect into
                string assembly = string.Format("Crestron.SimplSharpPro.UI.{0}", touchpanelType);
                CType cswitcher = this.TpAssembly.GetType(assembly);
                if (cswitcher == null)
                {
                    CrestronConsole.PrintLine(LogHeader + "Unable to find type: {0}",assembly);
                    return null;
                }
                // get the correct constructor for this type
                CType[] constructorTypes = new CType[] { typeof(uint), typeof(CrestronControlSystem) };
                // get info for the previously found constructor
                ConstructorInfo cinfo = cswitcher.GetConstructor(constructorTypes);
                if (cinfo != null) CrestronConsole.PrintLine("---cinfo.Attributes:{0} name:{1} reflected:{2} || ID{3}", cinfo.Attributes, cinfo.Name, cinfo.ReflectedType, deviceId);
                else CrestronConsole.PrintLine("cinfo NULL");
                // create the object with all the information
                if (this.CS == null)
                {
                    CrestronConsole.PrintLine(LogHeader + "CrestronControlSystem (this.CS) is null");
                    return null;
                }
                
                var instance = (BasicTriListWithSmartObject)cinfo.Invoke(new object[] { deviceId, this.CS });
                //instance.Name = this.Name;
                return instance;
            }
            catch (Exception e)
            {
                ErrorLog.Error(LogHeader + "Unable to create TP: {0}\nInner Exception: {1}", e.Message, e.InnerException != null ? e.InnerException.Message : "No inner exception");
                CrestronConsole.PrintLine(LogHeader + "Unable to create TP: {0}\nInner Exception: {1}", e.Message, e.InnerException != null ? e.InnerException.Message : "No inner exception");
                return null;
            }

        }

        /// <summary>
        /// SigChange for SmartObjects
        /// There are some other / better ways to do this potentially
        /// </summary>
        /// <param name="currentDevice">The device that triggered the event</param>
        /// <param name="args">Contains the SigType, Sig.Number and Sig.Value and more</param>
        private void SmartObject_SigChange(GenericBase currentDevice, SmartObjectEventArgs args)
        {
            ushort TPNumber = this.Number;
            CrestronConsole.PrintLine("smartobject--TP-{0}-SmrtID{1} smartObjectButton#{2} type{3} ", TPNumber, args.SmartObjectArgs.ID, args.Sig.Number, args.Sig.Type);
            
            switch ((SmartObjectIDs)args.SmartObjectArgs.ID)
            {
                case SmartObjectIDs.cameraKeypad:
                    break;
                case SmartObjectIDs.securityPartitions:
                    if (args.Event == eSigEvent.BoolChange)
                    {
                        //send button press to securityEISC
                        ushort buttonNumber = (ushort)(args.Sig.Number);
                        _parent.securityEISC.BooleanInput[(ushort)(buttonNumber + 50)].BoolValue = args.Sig.BoolValue;
                    }
                    break;
                case SmartObjectIDs.quickActions:
                    if (args.Event == eSigEvent.BoolChange)
                    {
                        CrestronConsole.PrintLine("quickActions: {0} {1}", args.Sig.Number, args.Sig.BoolValue);
                        //send button press to subsystemControlEISC
                        ushort buttonNumber = (ushort)(args.Sig.Number - 15);
                        _parent.subsystemControlEISC.BooleanInput[(ushort)((TPNumber * 100) - 100 + buttonNumber)].BoolValue = args.Sig.BoolValue;
                    }
                    break;
                case SmartObjectIDs.securityKeypad:
                    if (args.Event == eSigEvent.BoolChange)
                    {
                        //send button press to securityEISC
                        ushort buttonNumber = (ushort)(args.Sig.Number);
                        _parent.securityEISC.BooleanInput[(ushort)(buttonNumber + 60)].BoolValue = args.Sig.BoolValue;
                    }
                    break;
                case SmartObjectIDs.securityZoneList:
                    if (args.Event == eSigEvent.BoolChange)
                    {
                        //send button press to securityEISC
                        ushort buttonNumber = (ushort)(args.Sig.Number);
                        _parent.securityEISC.BooleanInput[(ushort)(buttonNumber + 85)].BoolValue = args.Sig.BoolValue;
                    }
                    else if (args.Event == eSigEvent.UShortChange) { 
                        
                    }
                        break;
                case SmartObjectIDs.spa:
                    break;
                case SmartObjectIDs.poolTab:
                    break;
                case SmartObjectIDs.lightingButtons:
                    { 
                        CrestronConsole.PrintLine("lightingButtons: {0} {1}", args.Sig.Number, args.Sig.BoolValue);
                        //send button press to subsystemControlEISC
                        ushort buttonNumber = (ushort)(args.Sig.Number - 10);
                        if (args.Sig.Type == eSigType.Bool) { 
                            _parent.subsystemControlEISC.BooleanInput[(ushort)((TPNumber - 1) * 200 + buttonNumber)].BoolValue = args.Sig.BoolValue;
                        }

                    }
                    break;
                case SmartObjectIDs.quickViewSubsystems: break;
                case SmartObjectIDs.quickActionViewStatus: break;
                case SmartObjectIDs.quickActionSaveCheckbox: break;
                case SmartObjectIDs.quickActionMusic: break;
                case SmartObjectIDs.quickActionClimate: break;
                case SmartObjectIDs.subsystemSelect: {
                        if (args.Event == eSigEvent.UShortChange)
                        {
                            if (args.Sig.Number == 1)//select a subsystem#
                            {
                                ushort subsystemButtonNumber = (ushort)args.Sig.UShortValue;
                                _parent.SelectSubsystem(TPNumber, subsystemButtonNumber);//from room subsystem
                            }
                        }
                        break; }
                case SmartObjectIDs.DVRTab:
                    {
                        if (args.Event == eSigEvent.BoolChange && args.Sig.BoolValue == true)
                        {
                            CrestronConsole.PrintLine("DVRTab: {0} {1}", args.Sig.Number, args.Sig.BoolValue);
                            ushort buttonNumber = (ushort)args.Sig.Number;
                            if (buttonNumber == 1)
                            {
                                _parent.manager.VideoSourceZ[this.CurrentVSrcNum].CurrentSubpageScenario = 1;
                                this.UserInterface.BooleanInput[141].BoolValue = true;
                                this.UserInterface.SmartObjects[26].BooleanInput[2].BoolValue = true;
                                this.UserInterface.BooleanInput[142].BoolValue = false;
                                this.UserInterface.SmartObjects[26].BooleanInput[4].BoolValue = false;
                            }
                            else if (buttonNumber == 3) {
                                _parent.manager.VideoSourceZ[this.CurrentVSrcNum].CurrentSubpageScenario = 2;
                                this.UserInterface.BooleanInput[141].BoolValue = false;
                                this.UserInterface.SmartObjects[26].BooleanInput[2].BoolValue = false;
                                this.UserInterface.BooleanInput[142].BoolValue = true;
                                this.UserInterface.SmartObjects[26].BooleanInput[4].BoolValue = true;
                            }
                        }
                        break;
                    }
                case SmartObjectIDs.dpad:
                    {
                        if (args.Event == eSigEvent.BoolChange)
                        {
                            ushort buttonNumber = (ushort)(args.Sig.Number);
                            _parent.subsystemControlEISC.BooleanInput[(ushort)(((TPNumber - 1) * 200) + buttonNumber + 21)].BoolValue = args.Sig.BoolValue;
                        }
                        break;
                    }
                case SmartObjectIDs.DVRKeypad:
                    {
                        if (args.Event == eSigEvent.BoolChange)
                        {
                            ushort buttonNumber = (ushort)(args.Sig.Number);
                            _parent.subsystemControlEISC.BooleanInput[(ushort)(((TPNumber - 1) * 200) + buttonNumber + 30)].BoolValue = args.Sig.BoolValue;
                        }
                        break;
                    }
                case SmartObjectIDs.floorSelect: {
                        if (args.Event == eSigEvent.UShortChange) {
                            if (args.Sig.Number == 1)//select a floor#
                            {
                                ushort floorButtonNumber = (ushort)args.Sig.UShortValue;
                                _parent.SelectFloor(TPNumber, floorButtonNumber);
                            }
                        }
                        break; }
                case SmartObjectIDs.zoneSelect: {
                        if (args.Event == eSigEvent.UShortChange)
                        {
                            if (args.Sig.Number == 1)//select a zone#
                            {
                                this.CurrentPageNumber = 2; // 2 = roomSubsystemList
                                _parent.SelectZone((TPNumber), (ushort)args.Sig.UShortValue, true);//from select zone
                            }
                        }
                        break; }
                case SmartObjectIDs.musicMenu: {
                        if (args.Event == eSigEvent.UShortChange)
                        {
                            CrestronConsole.PrintLine("musicMenu: {0} {1}", args.Sig.Number, args.Sig.UShortValue);
                        }
                        else if (args.Event == eSigEvent.BoolChange)
                        { 
                            ushort buttonNumber = (ushort)(args.Sig.Number - 4010);
                            ushort command = (ushort)(buttonNumber % 7);//checkbox toggle, vol up, vol dn, mute, save vol
                            ushort roomListPosition = (ushort)(buttonNumber / 7 + 1);
                            ushort roomNumber = this.MusicRoomsToShareSourceTo[roomListPosition - 1];
                            string tpCurrentRoom = _parent.manager.RoomZ[this.CurrentRoomNum].Name;
                            ushort audioID = _parent.manager.RoomZ[roomNumber].AudioID;
                            string roomname = _parent.manager.RoomZ[roomNumber].Name;
                            ushort audioSrcNum = _parent.manager.RoomZ[this.CurrentRoomNum].CurrentMusicSrc;
                            string audioSrcName = _parent.manager.MusicSourceZ[audioSrcNum].Name;
                            //CrestronConsole.PrintLine("TPRoom {0} tpCurrentSrc {1} ", tpCurrentRoom, audioSrcName);
                            //CrestronConsole.PrintLine("command {0} slot{1} {2}", command, roomListPosition, roomname);
                            if (audioID > 0) { 
                                switch (command) {
                                    case 0://save volume
                                        break;
                                    case 1://checkbox toggle
                                        if (args.Sig.BoolValue == true) { this.MusicRoomsToShareCheckbox[roomListPosition - 1] = !this.MusicRoomsToShareCheckbox[roomListPosition - 1]; }
                                        this.UserInterface.SmartObjects[7].BooleanInput[(ushort)(roomListPosition * 7 + 4004)].BoolValue = this.MusicRoomsToShareCheckbox[roomListPosition - 1];//checkbox fb
                                        this.UserInterface.SmartObjects[7].BooleanInput[(ushort)(roomListPosition * 7 + 4009)].BoolValue = this.MusicRoomsToShareCheckbox[roomListPosition - 1];//show/hide vol buttons
                                        //if the checkbox is selected, then send the source to the room
                                        if (this.MusicRoomsToShareCheckbox[roomListPosition - 1]) {
                                            this.UserInterface.SmartObjects[7].StringInput[(ushort)(roomListPosition * 2 + 10)].StringValue = _parent.BuildHTMLString(TPNumber, audioSrcName, "24");
                                            this.UserInterface.SmartObjects[7].UShortInput[(ushort)(roomListPosition + 10)].UShortValue = _parent.manager.RoomZ[roomNumber].MusicVolume;
                                            //convert volume to percentage
                                            uint vol = _parent.manager.RoomZ[roomNumber].MusicVolume;
                                            ushort volPercent = (ushort)(vol * 100 / 65535);
                                            CrestronConsole.PrintLine("rm {0} vol{1} pos{2}", _parent.manager.RoomZ[roomNumber].Name, volPercent, roomListPosition);
                                            _parent.SwitcherSelectMusicSource(audioID, audioSrcNum);
                                        }
                                        else {
                                            this.UserInterface.SmartObjects[7].StringInput[(ushort)(roomListPosition * 2 + 10)].StringValue = _parent.BuildHTMLString(TPNumber, "Off", "24");
                                            _parent.SwitcherSelectMusicSource(audioID, 0);
                                        }
                                        
                                        break;
                                    case 2://vol up
                                        //CrestronConsole.PrintLine("vol up {0}", _parent.manager.RoomZ[roomNumber].Name);
                                        _parent.musicEISC1.BooleanInput[(ushort)(audioID)].BoolValue = args.Sig.BoolValue;

                                        break;
                                    case 3://vol dn
                                        //CrestronConsole.PrintLine("vol down {0}", _parent.manager.RoomZ[roomNumber].Name);
                                        _parent.musicEISC1.BooleanInput[(ushort)(audioID +100)].BoolValue = args.Sig.BoolValue;
                                        break;
                                    case 4://mute
                                        _parent.musicEISC1.BooleanInput[(ushort)(audioID + 200)].BoolValue = args.Sig.BoolValue;
                                        break;
                                    default: break;
                                }
                            }

                            //CONTINUE HERE 7-9/24
                            //TO DO make volume up and down buttons work on sharing page


                            //on program startup the default room shows for example 'bar' and the subsystem page looks ok but selecting a subsystem clears the page.

                            //when selecting a source or pressing power off - check for the sharing menu feedback. then any zone with a checkbox on should be updated to the new source
                        }
                        break; 
                    }
                case SmartObjectIDs.musicFloorSelect: {
                        if (args.Event == eSigEvent.UShortChange)
                        {
                            if (args.Sig.Number == 1)//select a floor#
                            {
                                ushort floorButtonNumber = (ushort)args.Sig.UShortValue;
                               
                                _parent.SelectMusicFloor(TPNumber, floorButtonNumber);
                            }
                        }
                        break; }
                case SmartObjectIDs.musicSources: {
                    if (args.Event == eSigEvent.UShortChange)
                    {
                        if (args.Sig.Number == 1 && args.Sig.UShortValue > 0)//select a music source
                        {
                            ushort asrcButtonNumber = (ushort)args.Sig.UShortValue;
                            //translate button number to music source number
                            ushort asrcScenario = _parent.manager.RoomZ[this.CurrentRoomNum].AudioSrcScenario;
                            ushort asrcNumberToSend = _parent.manager.AudioSrcScenarioZ[asrcScenario].IncludedSources[asrcButtonNumber - 1];
                            _parent.PanelSelectMusicSource(TPNumber, asrcNumberToSend);
                            //if the music source sharing page is visible and there are zones checked, then update the zones with the new source
                            if (this.UserInterface.BooleanInput[1002].BoolValue == true)
                            {
                                for (int i = 0; i < this.MusicRoomsToShareSourceTo.Count; i++)
                                {
                                    if (this.MusicRoomsToShareCheckbox[i] == true)
                                    {
                                        _parent.SwitcherSelectMusicSource(_parent.manager.RoomZ[this.MusicRoomsToShareSourceTo[i]].AudioID, asrcNumberToSend);
                                        this.UserInterface.SmartObjects[7].StringInput[(ushort)(i * 2 + 12)].StringValue = _parent.BuildHTMLString(TPNumber, _parent.manager.MusicSourceZ[asrcNumberToSend].Name, "24"); ;
                                    }
                                }
                            }
                        }
                    }
                        break; }
                case SmartObjectIDs.wholeHouseSubsystems:
                { 
                    if (args.Event == eSigEvent.UShortChange)
                    {
                        if (args.Sig.Number == 1)//select a subsystem#
                        {
                            ushort subsystemButtonNumber = (ushort)args.Sig.UShortValue;
                            _parent.SelectSubsystem(TPNumber, subsystemButtonNumber);//from whole house subsystem list
                            }
                    }
                    break;
                }
                case SmartObjectIDs.wholeHouseZoneList:
                    {
                        if (args.Event == eSigEvent.UShortChange)
                        {
                            if (args.Sig.Number == 1)//select a zone#
                            {
                                this.CurrentPageNumber = 0; // 0 = HOME
                                ushort subsystemNumber = this.CurrentSubsystemNumber;
                                ushort currentRoomNumber = 0;
                                if (this.WholeHouseRoomList.Count > 0 && args.Sig.UShortValue > 0)
                                {
                                    currentRoomNumber = this.WholeHouseRoomList[args.Sig.UShortValue - 1];
                                    this.CurrentRoomNum = currentRoomNumber;
                                    this.UserInterface.StringInput[1].StringValue = _parent.manager.RoomZ[currentRoomNumber].Name;
                                }
                                CrestronConsole.PrintLine("wholeHouseZoneList: {0} {1} subsystem{2} room{3}", args.Sig.Number, args.Sig.UShortValue, subsystemNumber, _parent.manager.RoomZ[currentRoomNumber].Name);
                                if (subsystemNumber > 0)
                                {
                                    this.subsystemPageFlips(_parent.manager.SubsystemZ[subsystemNumber].FlipsToPageNumber);
                                    
                                    if (_parent.manager.SubsystemZ[subsystemNumber].EquipID > 99)
                                    {
                                        _parent.subsystemEISC.UShortInput[(ushort)(TPNumber + 200)].UShortValue = (ushort)(_parent.manager.SubsystemZ[subsystemNumber].EquipID + TPNumber); //get the equipID for the subsystem
                                    }
                                    else
                                    {
                                        _parent.subsystemEISC.UShortInput[(ushort)(TPNumber + 200)].UShortValue = (ushort)(_parent.manager.SubsystemZ[subsystemNumber].EquipID);
                                    }
                                }
                                if (currentRoomNumber > 0)
                                {
                                    _parent.subsystemEISC.UShortInput[(ushort)((TPNumber - 1) * 10 + 303)].UShortValue = _parent.manager.RoomZ[currentRoomNumber].LightsID;
                                    _parent.subsystemEISC.UShortInput[(ushort)((TPNumber - 1) * 10 + 304)].UShortValue = _parent.manager.RoomZ[currentRoomNumber].ShadesID;

                                    this.CurrentClimateID = _parent.manager.RoomZ[currentRoomNumber].ClimateID;
                                    _parent.SyncPanelToClimateZone(TPNumber);//from whole house select zone
                                }
                            }

                        }
                        break;
                    }
                case SmartObjectIDs.videoSources:
                    {
                        if (args.Event == eSigEvent.UShortChange)
                        {
                            if (args.Sig.Number == 1 && args.Sig.UShortValue > 0)//select a video source
                            {
                                ushort vsrcButtonNumber = (ushort)args.Sig.UShortValue;
                                _parent.SelectVideoSourceFromTP(TPNumber, vsrcButtonNumber);
                            }
                        }
                        break;
                    }
                case SmartObjectIDs.videoDisplays:
                    {
                        if (args.Event == eSigEvent.UShortChange)
                        {
                            if (args.Sig.Number == 1 && args.Sig.UShortValue > 0)//select a video display
                            {
                                ushort vdisplayButtonNumber = (ushort)args.Sig.UShortValue;
                                this.UserInterface.BooleanInput[351].BoolValue =false; //clear the button and sub.
                                _parent.SelectDisplay(TPNumber, vdisplayButtonNumber);
                            }
                        }
                        break;
                    }
                default:
                    break;
            }
        }

        private void onAnalogChangeEvent(uint deviceID, SigEventArgs args)
        { 
        
        }
        private void PollConnectionStatus(object userObject)
        {
            try
            {
                
                var app = this.UserInterface as CrestronApp;
                if (app == null) {
                    CrestronConsole.PrintLine("app null {0}", this.Name);
                }
                else if (_ethernetExtender == null)
                {
                    CrestronConsole.PrintLine("extender null {0}", this.Name);
                }
                else
                {
                    bool isRemote = app.ExtenderEthernetReservedSigs.ConnectedToAddress2Feedback?.BoolValue ?? false;
                    // Only log and update if the state has changed
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
        /// <summary>
        /// Eventhandler for boolean/ushort/string sigs
        /// </summary>
        /// <param name="currentDevice">The device that triggered the event</param>
        /// <param name="args">Contains the SigType, Sig.Number and Sig.Value and more</param>
        private void UserInterfaceObject_SigChange(BasicTriList currentDevice, SigEventArgs args)
        {

            if (args.Sig.Type == eSigType.Bool)
            {
                //CrestronConsole.PrintLine("Sig Change Event: {0}, Value: {1}", args.Sig.Number, args.Sig.BoolValue);
                //video volume buttons
                if (args.Sig.Number > 150 && args.Sig.Number < 160) {
                    _parent.subsystemControlEISC.BooleanInput[(ushort)(((Number - 1) * 200) + args.Sig.Number)].BoolValue = args.Sig.BoolValue;
                }
                //160 is the sleep button 180 is the format button
                else if ( args.Sig.Number > 180 && args.Sig.Number <= 200)
                {
                    //CrestronConsole.PrintLine("TP-{0} SigChange: {1} {2}", this.Number, args.Sig.Number, args.Sig.BoolValue);
                    //Video buttons: volume, sleep, format etc.
                    _parent.subsystemControlEISC.BooleanInput[(ushort)(((Number - 1) * 200) + args.Sig.Number)].BoolValue = args.Sig.BoolValue;
                }
                else if (args.Sig.Number > 200 && args.Sig.Number <= 350)
                {
                    //CrestronConsole.PrintLine("TP-{0} SigChange: {1} {2}", this.Number, args.Sig.Number, args.Sig.BoolValue);
                    //source control buttons
                    _parent.subsystemControlEISC.BooleanInput[(ushort)(((Number - 1) * 200) + args.Sig.Number - 200)].BoolValue = args.Sig.BoolValue;
                    //CrestronConsole.PrintLine("source control button press: {0} {1}", (ushort)(((Number - 1) * 200) + args.Sig.Number - 200), args.Sig.BoolValue);
                }
                else if (args.Sig.Number > 600 && args.Sig.Number < 701)
                {
                    if (this.CurrentSubsystemIsClimate)
                    {
                        CrestronConsole.PrintLine("climate button press: {0} {1}", args.Sig.Number, args.Sig.BoolValue);
                        //get button number - TPNumber * 30 + args.Sig.Number - 100
                        //send button press to HVACeisc
                        ushort climateID = _parent.manager.RoomZ[this.CurrentRoomNum].ClimateID;
                        ushort buttonNumber = (ushort)(climateID * 30 + args.Sig.Number - 130);
                        _parent.HVACEISC.BooleanInput[buttonNumber].BoolValue = args.Sig.BoolValue;
                    }
                    else
                    {
                        //subsystem buttons - these get routed through xpoints to whatever wacky subystem
                        ushort buttonNumber = (ushort)(args.Sig.Number - 600);
                        ushort eiscPos = (ushort)(((this.Number - 1) * 200) + buttonNumber);
                        _parent.subsystemControlEISC.BooleanInput[(ushort)(eiscPos)].BoolValue = args.Sig.BoolValue;
                    }
                }
                else if (args.Sig.Number > 750 && args.Sig.Number < 800)
                {
                    _parent.securityEISC.BooleanInput[(ushort)(args.Sig.Number - 750)].BoolValue = args.Sig.BoolValue;
                }
                else if (args.Sig.Number == 1007)
                {
                    //main volume up
                    _parent.musicEISC1.BooleanInput[(ushort)(_parent.manager.RoomZ[this.CurrentRoomNum].AudioID)].BoolValue = args.Sig.BoolValue;
                    /*if (args.Sig.BoolValue)
                    {
                        ushort time = calculateRampTime(_parent.manager.RoomZ[this.CurrentRoomNum].MusicVolume, 65535, 500);
                        this.UserInterface.UShortInput[2].CreateRamp(65535, time);
                        _parent.musicEISC3.UShortInput[(ushort)(_parent.manager.RoomZ[this.CurrentRoomNum].AudioID + 100)].CreateRamp(65535, time);
                    }
                    else
                    {
                        this.UserInterface.UShortInput[2].StopRamp();
                        _parent.musicEISC3.UShortInput[(ushort)(_parent.manager.RoomZ[this.CurrentRoomNum].AudioID + 100)].StopRamp();
                        _parent.manager.RoomZ[this.CurrentRoomNum].MusicVolume = this.UserInterface.UShortInput[2].UShortValue;
                    }*/
                }
                else if (args.Sig.Number == 1008)
                {
                    //main volume down
                    _parent.musicEISC1.BooleanInput[(ushort)(_parent.manager.RoomZ[this.CurrentRoomNum].AudioID + 100)].BoolValue = args.Sig.BoolValue;
                    /*if (args.Sig.BoolValue)
                    {
                        ushort time = calculateRampTime(_parent.manager.RoomZ[this.CurrentRoomNum].MusicVolume, 0, 500);
                        this.UserInterface.UShortInput[2].CreateRamp(0, time);
                        _parent.musicEISC3.UShortInput[(ushort)(_parent.manager.RoomZ[this.CurrentRoomNum].AudioID + 100)].CreateRamp(0, time);
                    }
                    else
                    {
                        this.UserInterface.UShortInput[2].StopRamp();
                        _parent.musicEISC3.UShortInput[(ushort)(_parent.manager.RoomZ[this.CurrentRoomNum].AudioID + 100)].StopRamp();
                        _parent.manager.RoomZ[this.CurrentRoomNum].MusicVolume = this.UserInterface.UShortInput[2].UShortValue;
                    }*/
                }


                else if (args.Sig.BoolValue == true)
                {
                    _parent.manager.ipidToNumberMap.TryGetValue(currentDevice.ID, out ushort tpNumber);
                    if (args.Sig.Number == 14)
                    {
                        //TODO - also make the hard home button do this
                        this.CurrentPageNumber = 0; // 0 = HOME
                        _parent.HomeButtonPress(tpNumber);//
                        this.UserInterface.BooleanInput[11].BoolValue = true;//show home page
                        this.UserInterface.BooleanInput[12].BoolValue = false;//hide rooms page
                        this.UserInterface.BooleanInput[998].BoolValue = false;//clear the sharing sub
                        this.UserInterface.BooleanInput[999].BoolValue = false;//clear the sharing sub with floors
                        this.UserInterface.BooleanInput[1002].BoolValue = false;//clear the sharing button
                    }
                    else if (args.Sig.Number == 15)
                    {
                        this.CurrentPageNumber = 2; // 2 = roomSubsystemList
                        _parent.RoomButtonPress(tpNumber, false);//room controls page select - go straight to the current room subsystems list
                        this.musicPageFlips(0);
                        CrestronConsole.PrintLine("RoomButtonPress: CurrentPageNumber {0}", this.CurrentPageNumber);
                        this.UserInterface.BooleanInput[11].BoolValue = false;//hide home page
                        this.UserInterface.BooleanInput[12].BoolValue = true;//show rooms page
                        this.UserInterface.BooleanInput[998].BoolValue = false;//clear the sharing sub
                        this.UserInterface.BooleanInput[999].BoolValue = false;//clear the sharing sub with floors
                        this.UserInterface.BooleanInput[1002].BoolValue = false;//clear the sharing button
                    }
                    else if (args.Sig.Number == 16)
                    {
                        _parent.RoomListButtonPress(tpNumber);//list of rooms page
                        this.CurrentPageNumber = 1; // 1 = roomListPage
                        this.musicPageFlips(0);
                        CrestronConsole.PrintLine("RoomListButtonPress: CurrentPageNumber {0}", this.CurrentPageNumber);
                        this.UserInterface.BooleanInput[11].BoolValue = false;//hide home page
                        this.UserInterface.BooleanInput[12].BoolValue = true;//show rooms page
                        this.UserInterface.BooleanInput[998].BoolValue = false;//clear the sharing sub
                        this.UserInterface.BooleanInput[999].BoolValue = false;//clear the sharing sub with floors
                        this.UserInterface.BooleanInput[1002].BoolValue = false;//clear the sharing button
                    }
                    else if (args.Sig.Number == 31)
                    {
                        //eisc to lighting program - home lights on
                    }
                    else if (args.Sig.Number == 32)
                    {
                        //eisc to lighting program - home lights off
                    }
                    else if (args.Sig.Number == 33)
                    {
                        //eisc to lighting program - home lights entertain
                    }
                    else if (args.Sig.Number == 50)
                    {
                        //change room button
                        if (!_parent.manager.touchpanelZ[tpNumber].Name.ToUpper().Contains("IPHONE"))
                        {
                            //these currently go to logic that then goes to the touchpanel.
                            //this will need to change to go directly to the touchpanel
                            _parent.imageEISC.BooleanInput[tpNumber].BoolValue = false;//clear "current subsystem is video"
                            this.CurrentSubsystemIsVideo = false;
                            subsystemPageFlips(1000);//this shows the list of rooms sub for NON iphone projects
                        }
                        _parent.SelectOnlyFloor(tpNumber); //change room button pressed - this is the "<" left arrow button
                        _parent.manager.touchpanelZ[tpNumber].CurrentPageNumber = 1;// 1 = roomListPage
                        //update the rooms now playing status text
                        _parent.UpdateRoomsPageStatusText(tpNumber);
                    }
                    else if (args.Sig.Number == 53)
                    {
                        //toggle video source sub. iphone only.
                        this.UserInterface.BooleanInput[53].BoolValue = !this.UserInterface.BooleanInput[53].BoolValue;//toggle the video source sub
                    }
                    else if (args.Sig.Number == 55)
                    {
                        // toggle music source sub. iphone only.
                        this.UserInterface.BooleanInput[55].BoolValue = !this.UserInterface.BooleanInput[55].BoolValue;//toggle the music source sub
                    }
                    else if (args.Sig.Number == 60)
                    {
                        //toggle lift menu
                        SleepFormatLiftMenu("LIFT", 30);
                    }

                    else if (args.Sig.Number > 60 && args.Sig.Number < 66)
                    {
                        //TODO - lift buttons
                    }
                    else if (args.Sig.Number == 70)
                    {
                        //lift go with off toggle
                        _parent.manager.RoomZ[this.CurrentRoomNum].LiftGoWithOff = !_parent.manager.RoomZ[this.CurrentRoomNum].LiftGoWithOff;
                        this.UserInterface.BooleanInput[70].BoolValue = _parent.manager.RoomZ[this.CurrentRoomNum].LiftGoWithOff;
                    }
                    else if (args.Sig.Number > 80 && args.Sig.Number < 86)
                    {
                        //TODO - misc off buttons
                    }
                    else if (args.Sig.Number == 99)//this is the back arrow
                    {
                        //home menu
                        subsystemPageFlips(0);
                        this.CurrentSubsystemIsLights = false;//
                        _parent.subsystemEISC.UShortInput[(ushort)(tpNumber + 200)].UShortValue = (ushort)(tpNumber + 300);//subsystem equipID 300 is quick actions
                    }
                    else if (args.Sig.Number == 100)//this is the X close subsystem button
                    {
                        _parent.PressCloseXButton(tpNumber);
                    }
                    else if (args.Sig.Number == 144)
                    {
                        //TODO - exit warming sub
                    }
                    else if (args.Sig.Number == 145)
                    {
                        //TODO - exit cooling sub
                    }
                    else if (args.Sig.Number == 148)
                    {
                        //TODO - Music Off
                    }
                    else if (args.Sig.Number == 149)
                    {
                        //TODO - Video Off
                        this.videoPageFlips(0);//from off
                        this.videoButtonFB(0);
                        _parent.SelectVideoSourceFromTP(tpNumber, 0);
                    }
                    else if (args.Sig.Number == 150)
                    {
                        //ROOM power off
                        this.videoPageFlips(0);//from off
                        this.videoButtonFB(0);
                        _parent.SelectVideoSourceFromTP(tpNumber, 0);
                    }
                    else if (args.Sig.Number == 160)
                    {
                        SleepFormatLiftMenu("SLEEP", 30);
                    }
                    else if (args.Sig.Number > 160 && args.Sig.Number < 167)
                    {
                        for (ushort i = 0; i < 5; i++)
                        { 
                            this.UserInterface.BooleanInput[(ushort)(161 + i)].BoolValue = false;//clear the sleep buttons
                        }
                        if (args.Sig.Number == 166)
                        {
                            _parent.manager.RoomZ[this.CurrentRoomNum].StartSleepTimer(0, _parent, Number);//cancel the sleep timer
                        }
                        else { 
                            ushort button = (ushort)(args.Sig.Number - 160);
                            ushort sleepCmd = _parent.manager.SleepScenarioZ[_parent.manager.RoomZ[this.CurrentRoomNum].SleepScenario].SleepCmds[button-1];
                            ushort time = _parent.manager.SleepCmdZ[sleepCmd].Length;
                            CrestronConsole.PrintLine("Sleep button {0} cmd {1} time {2}", button, sleepCmd, time);
                            this.UserInterface.BooleanInput[(ushort)(160 + button)].BoolValue = true;
                            _parent.manager.RoomZ[this.CurrentRoomNum].StartSleepTimer(time, _parent, Number);//start the sleep timer for the room
                        }
                    }
                    else if (args.Sig.Number == 180)
                    {
                        SleepFormatLiftMenu("FORMAT", 30);
                    }


                    else if (args.Sig.Number > 200 && args.Sig.Number < 351)
                    {
                        //Source Buttons
                        //Already routed above.
                    }
                    else if (args.Sig.Number == 351)
                    {
                        //change TV
                        this.UserInterface.BooleanInput[351].BoolValue = !this.UserInterface.BooleanInput[351].BoolValue;//toggle the tv button
                    }
                    else if (args.Sig.Number == 352)
                    {
                        //TODO - all tvs off
                    }
                    else if (args.Sig.Number > 400 && args.Sig.Number < 501)
                    {
                        //TODO - favorite channel buttons
                    }
                    else if (args.Sig.Number > 600 && args.Sig.Number < 701)
                    {

                        //this is already routed above. do nothing here.
                    }
                    else if (args.Sig.Number > 750 && args.Sig.Number < 801)
                    {
                        //this is already routed above. do nothing here.
                    }
                    else if (args.Sig.Number == 1002) //toggle the sharing button
                    {
                        //TODO - extend the timer when any volume / mute or zone select button is pressed / use the item clicked smart object7 event
                        //TODO - clear the button after 15 seconds
                        //TODO - clear the button on music source select / music off / all off / zones select
                        //TODO - if no floor is selected select the default floor
                        this.SrcSharingButtonFB = !this.SrcSharingButtonFB;
                        this.UserInterface.BooleanInput[1002].BoolValue = this.SrcSharingButtonFB;
                        ushort rm = this.CurrentRoomNum;
                        ushort asrcSharingScenario = _parent.manager.RoomZ[rm].AudioSrcSharingScenario;
                        if (!this.SrcSharingButtonFB)//no sharing sub
                        {
                            this.UserInterface.BooleanInput[998].BoolValue = false;
                            this.UserInterface.BooleanInput[999].BoolValue = false;
                        }
                        else if (this.CurrentSubsystemIsAudio && asrcSharingScenario > 50)
                        {
                            this.UserInterface.BooleanInput[998].BoolValue = false;
                            this.UserInterface.BooleanInput[999].BoolValue = true;//sharing sub with floors
                        }
                        else
                        {
                            this.UserInterface.BooleanInput[998].BoolValue = true;//regular sharing sub
                            this.UserInterface.BooleanInput[999].BoolValue = false;//sharing sub with floors
                        }
                    }

                    else if (args.Sig.Number == 1003)//POWER OFF
                    {
                        //music off button
                        this.musicButtonFB(0);
                        this.musicPageFlips(0);
                        _parent.manager.RoomZ[this.CurrentRoomNum].CurrentMusicSrc = 0;
                        this.UserInterface.StringInput[3].StringValue = "Off";
                        _parent.manager.RoomZ[this.CurrentRoomNum].MusicStatusText = "";

                        //if the music source sharing page is visible and there are zones checked, then turn off the selected zones
                        if (this.UserInterface.BooleanInput[1002].BoolValue == true)
                        {
                            for (int i = 0; i < this.MusicRoomsToShareSourceTo.Count; i++)
                            {
                                if (this.MusicRoomsToShareCheckbox[i] == true)
                                {
                                    _parent.SwitcherSelectMusicSource(_parent.manager.RoomZ[this.MusicRoomsToShareSourceTo[i]].AudioID, 0);
                                    this.UserInterface.SmartObjects[7].BooleanInput[(ushort)(i * 7 + 4011)].BoolValue = false;//checkbox checked
                                    this.UserInterface.SmartObjects[7].BooleanInput[(ushort)(i * 7 + 4016)].BoolValue = false;//hide music volume
                                    this.UserInterface.SmartObjects[7].StringInput[(ushort)(i * 2 + 12)].StringValue = _parent.BuildHTMLString(this.Number, "Off", "24");
                                }
                                this.MusicRoomsToShareCheckbox[i] = false;//clear the checkboxes
                            }
                        }
                        this.SrcSharingButtonFB = false;
                        this.UserInterface.BooleanInput[1001].BoolValue = false;//hide sharing button
                        this.UserInterface.BooleanInput[1002].BoolValue = false;//clear the sharing button
                        this.UserInterface.BooleanInput[998].BoolValue = false;//clear the sharing sub
                        this.UserInterface.BooleanInput[999].BoolValue = false;//clear the sharing sub with floors
                    }
                    else if (args.Sig.Number == 1004)
                    {
                        //music share to all
                        for (ushort i = 0; i < this.MusicRoomsToShareSourceTo.Count; i++)
                        {
                            this.UserInterface.SmartObjects[7].BooleanInput[(ushort)(i * 7 + 4011)].BoolValue = true;//checkbox checked
                            this.UserInterface.SmartObjects[7].BooleanInput[(ushort)(i * 7 + 4016)].BoolValue = true;//music volume visible
                            this.MusicRoomsToShareCheckbox[i] = true;
                            ushort roomNumber = this.MusicRoomsToShareSourceTo[i];
                            ushort audioSrcNum = _parent.manager.RoomZ[this.CurrentRoomNum].CurrentMusicSrc;
                            _parent.SwitcherSelectMusicSource(_parent.manager.RoomZ[roomNumber].AudioID, audioSrcNum);//send the music source to the room
                            this.UserInterface.SmartObjects[7].StringInput[(ushort)(i * 2 + 12)].StringValue = _parent.BuildHTMLString(this.Number, _parent.manager.MusicSourceZ[audioSrcNum].Name, "24");
                        }

                    }
                    else if (args.Sig.Number == 1005)
                    {
                        //music unshare to all
                        for (ushort i = 0; i < this.MusicRoomsToShareSourceTo.Count; i++)
                        {
                            ushort roomNumber = this.MusicRoomsToShareSourceTo[i];
                            //if the checkbox is checked, then turn off the room. otherwise leave it alone
                            if (this.MusicRoomsToShareCheckbox[i])
                            {
                                _parent.SwitcherSelectMusicSource(_parent.manager.RoomZ[roomNumber].AudioID, 0);//turn off the room
                                this.UserInterface.SmartObjects[7].StringInput[(ushort)(i * 2 + 12)].StringValue = _parent.BuildHTMLString(this.Number, "Off", "24");
                                this.UserInterface.SmartObjects[7].BooleanInput[(ushort)(i * 7 + 4011)].BoolValue = false;//clear the checkbox
                                this.UserInterface.SmartObjects[7].BooleanInput[(ushort)(i * 7 + 4016)].BoolValue = false;//hide the volume buttons
                            }
                            this.MusicRoomsToShareCheckbox[i] = false;
                        }
                    }
                    else if (args.Sig.Number == 1006)
                    {
                        //music ALL off button
                        this.musicButtonFB(0);
                        this.musicPageFlips(0);
                        this.UserInterface.StringInput[3].StringValue = "Off";
                        foreach (var room in _parent.manager.RoomZ)
                        {
                            if (room.Value.AudioID > 0)
                            {
                                _parent.SwitcherSelectMusicSource(room.Value.AudioID, 0);
                            }
                        }
                    }
                    else if (args.Sig.Number == 1009)
                    {
                        //main volume mute
                        _parent.musicEISC1.BooleanInput[(ushort)(_parent.manager.RoomZ[this.CurrentRoomNum].AudioID + 200)].BoolValue = true;
                        _parent.musicEISC1.BooleanInput[(ushort)(_parent.manager.RoomZ[this.CurrentRoomNum].AudioID + 200)].BoolValue = false;

                    }
                }
            }
            else if (args.Sig.Type == eSigType.UShort)
            { 
                //CrestronConsole.PrintLine("Sig Change Event: {0}, Value: {1}", args.Sig.Number, args.Sig.UShortValue);
            }
        }
        public void subsystemPageFlips(ushort pageNumber) 
        {
            //get the subsystem name
            string subsystemName = "";
            for (ushort i = 1; i <= _parent.manager.SubsystemZ.Count; i++)
            {
                if (_parent.manager.SubsystemZ[i].FlipsToPageNumber == pageNumber)
                { 
                    subsystemName = _parent.manager.SubsystemZ[i].Name;
                }
            }
            //clear the current subsystem page
            this.UserInterface.BooleanInput[50].BoolValue = false;//clear the list of rooms page
            this.UserInterface.BooleanInput[51].BoolValue = false;//clear the room list sub w/no floors 
            
            for (ushort i = 0; i < 20; i++)
            {
                this.UserInterface.BooleanInput[(ushort)(i + 101)].BoolValue = false;
            }
            for (ushort i = 0; i < 10; i++)
            {
                this.UserInterface.BooleanInput[(ushort)(i + 91)].BoolValue = false;//clear the whole house subsystems.
                this.UserInterface.BooleanInput[(ushort)(i + 701)].BoolValue = false;//clear the Rooms hvac scenario menus
                //this applies to iphone only
                this.UserInterface.BooleanInput[(ushort)(i + 711)].BoolValue = false;//clear the HOME hvac scenario menus
                this.UserInterface.BooleanInput[(ushort)(i + 721)].BoolValue = false;//clear the HOME light scenario menus
            }
            //show the right HVAC subsystem scenario page
            if (subsystemName.ToUpper() == "HVAC" || subsystemName.ToUpper() == "CLIMATE")
            {
                ushort scenario = _parent.manager.RoomZ[this.CurrentRoomNum].HVACScenario;
                if (this.CurrentPageNumber == (ushort)TouchpanelUI.CurrentPageType.Home && this.Name.ToUpper().Contains("IPHONE"))
                {
                    this.UserInterface.BooleanInput[(ushort)(710 + scenario)].BoolValue = true;
                }
                else
                {
                    this.UserInterface.BooleanInput[(ushort)(700 + scenario)].BoolValue = true;
                }
                this.UserInterface.BooleanInput[100].BoolValue = false;//clear the room subsystems page
            }
            else if (subsystemName.ToUpper().Contains("LIGHT"))
            {
                //check to see if we are on the home menu or the room subsystems page
                //but only for iphone
                if (this.CurrentPageNumber == (ushort)TouchpanelUI.CurrentPageType.Home && this.Name.ToUpper().Contains("IPHONE"))
                {
                   this.UserInterface.BooleanInput[723].BoolValue = true; //show HOME Lighting page
                }
                else
                {
                    this.UserInterface.BooleanInput[(ushort)(pageNumber + 100)].BoolValue = true;// show the regular lighting page
                }
            }
            else if (pageNumber == 1000)//room list
            {
                this.UserInterface.BooleanInput[100].BoolValue = false;//clear the room subsystems page
                if (_parent.manager.FloorScenarioZ[this.FloorScenario].IncludedFloors.Count < 2)
                {
                    this.UserInterface.BooleanInput[51].BoolValue = true;//list of rooms page no floors
                }
                else
                {
                    this.UserInterface.BooleanInput[50].BoolValue = true;//list of rooms page WITH floors
                }
            }
            else if (pageNumber == 0 && this.CurrentPageNumber == (ushort)TouchpanelUI.CurrentPageType.RoomSubsystemList)
            {
                this.UserInterface.BooleanInput[100].BoolValue = true; //room subsystems page
            }
            else if (pageNumber > 0 && pageNumber <= 20)
            {
                this.UserInterface.BooleanInput[(ushort)(pageNumber + 100)].BoolValue = true;//subsystem to show
            }
            else if (pageNumber > 90 && pageNumber < 100)
            {
                this.UserInterface.BooleanInput[(ushort)(pageNumber)].BoolValue = true;//these are the 'wholehousezonelist' subpages
                this.UserInterface.BooleanInput[100].BoolValue = false;//clear the room subsystems page
            }

        }
        public void videoPageFlips(ushort pageNumber) 
        {
            this.CurrentVideoPageNumber = pageNumber;
            for (ushort i = 0; i < 23; i++)
            { //23 is to clear out the DVR subpages
                this.UserInterface.BooleanInput[(ushort)(i + 121)].BoolValue = false;//clear any video subpages
            }

            this.UserInterface.BooleanInput[53].BoolValue = false;//hide source page for iphone.
            
            if (this.CurrentSubsystemIsVideo)
            {
                this.UserInterface.BooleanInput[(ushort)(pageNumber + 120)].BoolValue = true;//show the video subpage
                if (pageNumber == 1)
                {
                    this.UserInterface.BooleanInput[(ushort)(140 + (_parent.manager.VideoSourceZ[CurrentVSrcNum].CurrentSubpageScenario))].BoolValue = true;
                    //clear the tab fb
                    this.UserInterface.SmartObjects[26].BooleanInput[(ushort)(2)].BoolValue = false;
                    this.UserInterface.SmartObjects[26].BooleanInput[(ushort)(4)].BoolValue = false;
                    //set the tab fb
                    this.UserInterface.SmartObjects[26].BooleanInput[(ushort)(2 * _parent.manager.VideoSourceZ[CurrentVSrcNum].CurrentSubpageScenario)].BoolValue = true;
                }
            }

        }
        public void SelectDVRPage() { 
            
        }
        public void SleepFormatLiftMenu(string button, ushort timer)
        {
            // Stop and dispose any existing timer
            if (_sleepFormatLiftTimer != null)
            {
                _sleepFormatLiftTimer.Stop();
                _sleepFormatLiftTimer.Dispose();
                _sleepFormatLiftTimer = null;
            }

            // Start a new timer (timer is in seconds, CTimer expects ms)
            if (timer > 0)
            {
                _sleepFormatLiftTimer = new CTimer(_ =>
                {
                    // Clear all menus and buttons when timer expires
                    ClearSleepFormatLiftMenus();
                }, timer * 1000);
            }
            //clear all menus first
            for (ushort i = 0; i < 5; i++)
            {
                this.UserInterface.BooleanInput[(ushort)(171 + i)].BoolValue = false; //clear all sleep sub scenarios
            }
            for (ushort i = 0; i < 10; i++)
            {
                this.UserInterface.BooleanInput[(ushort)(191 + i)].BoolValue = false; //clear all format sub
                this.UserInterface.BooleanInput[(ushort)(71 + i)].BoolValue = false; //clear all lift sub scenarios                                                                      
            }
            if (button.ToUpper().Contains("SLEEP"))
            {
                this.UserInterface.BooleanInput[160].BoolValue = !this.UserInterface.BooleanInput[160].BoolValue; //toggle the sleep button
                ushort scenario = _parent.manager.RoomZ[this.CurrentRoomNum].SleepScenario;
                if (this.UserInterface.BooleanInput[160].BoolValue)
                {
                    this.UserInterface.BooleanInput[(ushort)(170 + scenario)].BoolValue = true; //show sleep menu
                    //clear the other buttons
                    this.UserInterface.BooleanInput[180].BoolValue = false; //clear the format button
                    this.UserInterface.BooleanInput[60].BoolValue = false; //clear the lift button
                }
            }
            else if (button.ToUpper().Contains("FORMAT"))
            {
                this.UserInterface.BooleanInput[180].BoolValue = !this.UserInterface.BooleanInput[180].BoolValue; //toggle the format button
                ushort scenario = _parent.manager.RoomZ[this.CurrentRoomNum].FormatScenario;
                if (this.UserInterface.BooleanInput[180].BoolValue)
                {
                    this.UserInterface.BooleanInput[(ushort)(190 + scenario)].BoolValue = true; //show format menu
                    //clear the other buttons
                    this.UserInterface.BooleanInput[160].BoolValue = false; //clear the sleep button
                    this.UserInterface.BooleanInput[60].BoolValue = false; //clear the lift button
                }
            }
            else if (button.ToUpper().Contains("LIFT"))
            {
                this.UserInterface.BooleanInput[60].BoolValue = !this.UserInterface.BooleanInput[60].BoolValue;
                ushort scenario = _parent.manager.RoomZ[this.CurrentRoomNum].LiftScenario;
                if (this.UserInterface.BooleanInput[60].BoolValue)
                {
                    this.UserInterface.BooleanInput[(ushort)(70 + scenario)].BoolValue = true; //show lift menu
                    //clear the other buttons
                    this.UserInterface.BooleanInput[180].BoolValue = false; //clear the format button
                    this.UserInterface.BooleanInput[160].BoolValue = false; //clear the sleep button
                }
            }
            else {
                this.UserInterface.BooleanInput[60].BoolValue = false;
                this.UserInterface.BooleanInput[160].BoolValue = false; //clear the sleep button
                this.UserInterface.BooleanInput[180].BoolValue = false; //clear the format button
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
        public ushort calculateRampTime(ushort startValue, ushort endValue, ushort time)
        {
            ushort rampTime = 0;

            if (startValue > endValue)
            {
                rampTime = (ushort)((startValue - endValue) * time / 65535);//ramp down
            }
            else
            {
                rampTime = (ushort)((endValue - startValue) * time / 65535);//ramp up
            }
            return rampTime;
        }
        public void musicPageFlips(ushort pageNumber)
        {
            CrestronConsole.PrintLine("musicPageFlips: {0}", pageNumber);
            for (ushort i = 0; i < 20; i++)
            {
                this.UserInterface.BooleanInput[(ushort)(i + 1011)].BoolValue = false;//clear any music subpages
            }

            this.UserInterface.BooleanInput[55].BoolValue = false;//hide source page for iphone.
            if (this.CurrentSubsystemIsAudio)
            {
                if (pageNumber > 0)
                {
                    this.UserInterface.BooleanInput[(ushort)(pageNumber + 1010)].BoolValue = true;//show the music sources subpage
                }
            }
        }
        /// <summary>
        /// music SOURCE button feedback
        /// </summary>
        /// <param name="buttonNumber"></param>
        public void musicButtonFB(ushort buttonNumber)
        {
            for (ushort i = 0; i < 20; i++)
            {
                this.UserInterface.SmartObjects[6].BooleanInput[(ushort)(i + 11)].BoolValue = false;//clear all button feedback
            }
            this.UserInterface.BooleanInput[1001].BoolValue = false;//hide the sharing button
            if (buttonNumber > 0) {
                this.UserInterface.SmartObjects[6].BooleanInput[(ushort)(buttonNumber+10)].BoolValue = true;
                ushort asrcSharingScenario = _parent.manager.RoomZ[this.CurrentRoomNum].AudioSrcSharingScenario;
                if (asrcSharingScenario > 0)
                { 
                    this.UserInterface.BooleanInput[1001].BoolValue = true;//show the sharing button
                }
            }
        }
        public void videoButtonFB(ushort buttonNumber)
        {
            
            for (ushort i = 0; i < 20; i++)
            {
                this.UserInterface.SmartObjects[5].BooleanInput[(ushort)(i + 11)].BoolValue = false;//clear all button feedback
            }
            if (buttonNumber > 0)
            {
                CrestronConsole.PrintLine("videoButtonFB: {0}", buttonNumber);
                this.UserInterface.SmartObjects[5].BooleanInput[(ushort)(buttonNumber + 10)].BoolValue = true;
            }
        }
        public void floorButtonFB(ushort buttonNumber) {
            for (ushort i = 0; i < 10; i++)
            {
                this.UserInterface.SmartObjects[3].BooleanInput[(ushort)(i + 11)].BoolValue = false;//clear all button feedback
            }
            if (buttonNumber > 0)
            {
                this.UserInterface.SmartObjects[3].BooleanInput[(ushort)(buttonNumber + 10)].BoolValue = true;
            }
        }
        public void musicFloorButtonFB(ushort buttonNumber)
        {
            for (ushort i = 0; i < 10; i++)
            {
                this.UserInterface.SmartObjects[9].BooleanInput[(ushort)(i + 11)].BoolValue = false;//clear all button feedback
            }
            if (buttonNumber > 0)
            {
                this.UserInterface.SmartObjects[9].BooleanInput[(ushort)(buttonNumber + 10)].BoolValue = true;
            }
        }
        
        //SUBSCRIPTIONS
        public void UnsubscribeTouchpanelFromAllVolMuteChanges()
        {
            
            foreach (var kvp in this.MuteChangeHandlers)
            {
                
                var room = kvp.Key;
                var handler = kvp.Value;
                room.MusicMutedChanged -= handler;
            }
            this.MuteChangeHandlers.Clear();
            foreach (var kvp in this.VolumeChangeHandlers)
            {
                var room = kvp.Key;
                var handler = kvp.Value;
                room.MusicVolumeChanged -= handler;
            }
            this.VolumeChangeHandlers.Clear();
        }

        /// <summary>
        /// Subscribes to status events of the subsystems available for the current room
        /// </summary>
        public void SubscribeToRoomSubsystemEvents(ushort roomNumber, ushort previousRoom)
        {

            // Unsubscribe from all current subscriptions
            if (_roomSubsystemSubscriptions != null && _roomSubsystemSubscriptions.Count > 0)
            {
                foreach (var subscription in _roomSubsystemSubscriptions)
                {
                    //ushort eiscPosition = subscription.Key;
                    Action<ushort, string> handler = subscription.Value;

                    // Find the room associated with this touchpanel's current room
                    if (_parent.manager.RoomZ.ContainsKey(previousRoom))
                    {
                        RoomConfig oldRoom = _parent.manager.RoomZ[previousRoom];

                        // Unsubscribe from all possible events
                        oldRoom.HVACStatusChanged -= handler;
                        oldRoom.LightStatusChanged -= handler;
                        oldRoom.MusicStatusTextChanged -= handler;
                        oldRoom.MusicStatusTextOffChanged -= handler;
                        oldRoom.VideoStatusTextChanged -= handler;
                        oldRoom.VideoStatusTextOffChanged -= handler;

                        //TODO - not sure if the roomstatuschanged event is needed here
                        //oldRoom.RoomStatusChanged -= handler;
                    }
                }

                // Clear the subscriptions dictionary
                _roomSubsystemSubscriptions.Clear();
            }

            // Ensure the room exists before proceeding


            // Check if the manager is valid
            if (_parent.manager == null)
            {
                CrestronConsole.PrintLine("_parent.manager is null.");
                return;
            }


            // Check if the room exists
            if (!_parent.manager.RoomZ.ContainsKey(roomNumber))
            {
                CrestronConsole.PrintLine($"Room {roomNumber} does not exist in RoomZ.");
                return;
            }

            // Get the subsystem scenario for the specified room
            RoomConfig room = _parent.manager.RoomZ[roomNumber];
            ushort subsystemScenario = room.SubSystemScenario;

            // Ensure the subsystem scenario exists
            if (!_parent.manager.SubsystemScenarioZ.ContainsKey(subsystemScenario))
            {
                CrestronConsole.PrintLine($"Subsystem scenario {subsystemScenario} does not exist for room {roomNumber}. Subscription aborted.");
                return;
            }

            ushort numSubsystems = (ushort)_parent.manager.SubsystemScenarioZ[subsystemScenario].IncludedSubsystems.Count;

            for (ushort i = 0; i < numSubsystems; i++)
            {
                ushort capturedIndex = i;  // Create a local copy of i for the closure
                string subName = _parent.manager.SubsystemZ[_parent.manager.SubsystemScenarioZ[subsystemScenario].IncludedSubsystems[i]].Name;
                if (subName.Contains("Climate") || subName.Contains("HVAC"))
                {
                    this.UserInterface.SmartObjects[2].StringInput[(ushort)(3 * capturedIndex + 12)].StringValue = room.HVACStatusText;
                    // Define the subscription
                    Action<ushort, string> subscription = (rNumber, status) =>
                    {
                        this.UserInterface.SmartObjects[2].StringInput[(ushort)(3 * capturedIndex + 12)].StringValue = room.HVACStatusText;
                    };

                    // Subscribe to the HVACStatusChanged event
                    room.HVACStatusChanged += subscription;

                    // Add to the subscriptions dictionary
                    _roomSubsystemSubscriptions[i] = subscription;
                }
                else if (subName.ToUpper().Contains("LIGHT"))
                {

                    this.UserInterface.SmartObjects[2].StringInput[(ushort)(3 * capturedIndex + 12)].StringValue = room.LightStatusText;
                    // Define the subscription
                    
                    Action<ushort, string> subscription = (rNumber, status) =>
                    {
                        this.UserInterface.SmartObjects[2].StringInput[(ushort)(3 * capturedIndex + 12)].StringValue = status;
                    };

                    // Subscribe to the LightStatusChanged event
                    room.LightStatusChanged += subscription;

                    // Add to the subscriptions dictionary
                    _roomSubsystemSubscriptions[i] = subscription;
                }
                else if (subName.ToUpper().Contains("SHADE") || subName.ToUpper().Contains("DRAPE"))
                {
                    this.UserInterface.SmartObjects[2].StringInput[(ushort)(3 * i + 12)].StringValue = "";//currently shades to don't get status text
                }
                else if (subName.ToUpper().Contains("AUDIO") || subName.ToUpper().Contains("MUSIC"))
                {
                    this.UserInterface.SmartObjects[2].StringInput[(ushort)(3 * capturedIndex + 12)].StringValue = room.MusicStatusTextOff;
                    Action<ushort, string> subscription = (rNumber, status) =>
                    {
                        this.UserInterface.SmartObjects[2].StringInput[(ushort)(3 * capturedIndex + 12)].StringValue = status;
                    };
                    room.MusicStatusTextOffChanged += subscription;
                    _roomSubsystemSubscriptions[i] = subscription;
                }
                else if (subName.ToUpper().Contains("VIDEO") || subName.ToUpper().Contains("WATCH"))
                {

                    this.UserInterface.SmartObjects[2].StringInput[(ushort)(3 * capturedIndex + 12)].StringValue = room.VideoStatusTextOff;
                    Action<ushort, string> subscription = (rNumber, status) =>
                    {
                        this.UserInterface.SmartObjects[2].StringInput[(ushort)(3 * capturedIndex + 12)].StringValue = status;
                    };
                    room.VideoStatusTextOffChanged += subscription;
                    _roomSubsystemSubscriptions[i] = subscription;
                }
                else
                {
                    this.UserInterface.SmartObjects[2].StringInput[(ushort)(3 * capturedIndex + 12)].StringValue = "";
                }
            }
        }

        /// <summary>
        /// subscribe to events for list of rooms status
        /// </summary>
        public void SubscribeToListOfRoomsStatusEvents(ushort newFloorNumber)
        {
            // Ensure the parent exists before proceeding
            if (_parent.manager == null)
            {
                CrestronConsole.PrintLine("_parent.manager is null.");
                return;
            }
            if (newFloorNumber == 0)
            {
                CrestronConsole.PrintLine("newFloorNumber is 0. Subscription aborted.");
                return;
            }

            //CrestronConsole.PrintLine("SubscribeToListOfRoomsStatusEvents called for floor {0} tp-{1}", newFloorNumber, Number);

            ClearCurrentRoomSubscriptions();//from SubscribeToListOfRoomsStatusEvents
            ushort subscriptionCounter = 0;
            // Subscribe to the new floor
            for (ushort j = 0; j < _parent.manager.Floorz[newFloorNumber].IncludedRooms.Count; j++)
            {
                ushort capturedRoomIndex = j;
                ushort roomNumber = _parent.manager.Floorz[newFloorNumber].IncludedRooms[j];
                // Get the subsystem scenario for the specified room
                RoomConfig room = _parent.manager.RoomZ[_parent.manager.Floorz[newFloorNumber].IncludedRooms[j]];
                ushort subsystemScenario = room.SubSystemScenario;

                // Ensure the subsystem scenario exists
                if (!_parent.manager.SubsystemScenarioZ.ContainsKey(subsystemScenario))
                {
                    CrestronConsole.PrintLine($"Subsystem scenario {subsystemScenario} does not exist for room {room.Name}. Subscription aborted.");
                    return;
                }

                ushort numSubsystems = (ushort)_parent.manager.SubsystemScenarioZ[subsystemScenario].IncludedSubsystems.Count;
                for (ushort i = 0; i < numSubsystems; i++)
                {
                    ushort capturedSubsystemIndex = i;
                    string subName = _parent.manager.SubsystemZ[_parent.manager.SubsystemScenarioZ[subsystemScenario].IncludedSubsystems[i]].Name;
                    if (subName.ToUpper().Contains("CLIMATE") || subName.ToUpper().Contains("HVAC"))
                    {
                        // Define the subscription
                        this.UserInterface.SmartObjects[4].StringInput[(ushort)(4 * capturedRoomIndex + 12)].StringValue = room.HVACStatusText;
                        Action<ushort, string> statusSubscription = (rNumber, status) =>
                        {
                            this.UserInterface.SmartObjects[4].StringInput[(ushort)(4 * capturedRoomIndex + 12)].StringValue = status;//room status line 1
                        };
                        // Subscribe to the HVACStatusChanged event
                        room.HVACStatusChanged += statusSubscription;
                        // Add to the subscriptions dictionary
                        _roomListStatusSubscriptions[i] = statusSubscription;

                    }
                    if (subName.ToUpper().Contains("LIGHT") || subName.ToUpper().Contains("MUSIC") || subName.ToUpper().Contains("AUDIO") || subName.ToUpper().Contains("VIDEO"))
                    {
                        // Define the subscription
                        this.UserInterface.SmartObjects[4].StringInput[(ushort)(4 * capturedRoomIndex + 13)].StringValue = room.RoomStatusText;
                        Action<ushort, string> statusSubscription = (rNumber, status) =>
                        {
                            this.UserInterface.SmartObjects[4].StringInput[(ushort)(4 * capturedRoomIndex + 13)].StringValue = status;//room status line 2
                        };
                        // Subscribe to the RoomStatusTextChanged event
                        room.RoomStatusTextChanged += statusSubscription;
                        // Add to the subscriptions dictionary
                        _roomListStatusSubscriptions[subscriptionCounter++] = statusSubscription;

                    }
                }
            }
        }

        public void SubscribeToMusicMenuEvents(ushort roomNumber)
        {
            if (currentSubscribedRoom != null)
            {
                currentSubscribedRoom.MusicSrcStatusChanged -= MusicSrcStatusChangedHandler;
            }
            if (_parent.manager.RoomZ[roomNumber].AudioID > 0)
            {
                RoomConfig room = _parent.manager.RoomZ[roomNumber];

                // Initialize values before subscribing
                ushort currentMusicSrc = room.CurrentMusicSrc;
                if (currentMusicSrc > 0)
                {
                    this.musicPageFlips(_parent.manager.MusicSourceZ[currentMusicSrc].FlipsToPageNumber);//set the music page flips
                    _parent.musicEISC1.UShortInput[(ushort)(Number + 300)].UShortValue = _parent.manager.MusicSourceZ[currentMusicSrc].EquipID;
                    this.UserInterface.StringInput[3].StringValue = _parent.manager.MusicSourceZ[currentMusicSrc].Name;//set the music source name
                    ushort asrcScenarioNum = _parent.manager.RoomZ[roomNumber].AudioSrcScenario;
                    ushort numSrcs = (ushort)_parent.manager.AudioSrcScenarioZ[asrcScenarioNum].IncludedSources.Count;
                    for (ushort i = 0; i < numSrcs; i++)//loop through all music sources in this scenario
                    {
                        ushort srcNum = _parent.manager.AudioSrcScenarioZ[asrcScenarioNum].IncludedSources[i];
                        if (srcNum == _parent.manager.RoomZ[roomNumber].CurrentMusicSrc)
                        {
                            this.musicButtonFB((ushort)(i + 1));//set the music button feedback
                            
                        }
                    }
                }
                else
                {
                    this.musicPageFlips(0);//set the music page flips to 0
                    _parent.musicEISC1.UShortInput[(ushort)(Number + 300)].UShortValue = 0;//equipID
                    this.UserInterface.StringInput[3].StringValue = "Off";//src textings
                    this.musicButtonFB(0);//clear music button fb
                }
                _parent.musicEISC1.UShortInput[(ushort)(Number + 100)].UShortValue = currentMusicSrc;


                // Subscribe to changes in CurrentMusicSrc
                room.MusicSrcStatusChanged += MusicSrcStatusChangedHandler;

                // Store reference to currently subscribed room
                currentSubscribedRoom = room;
            }
        }

        public void SubscribeToVideoMenuEvents(ushort roomNumber)
        {
            if (currentSubscribedRoom != null)
            {
                currentSubscribedRoom.VideoSrcStatusChanged -= VideoSrcStatusChangedHandler;
                currentSubscribedRoom.DisplayChanged -= UpdateTouchpanelDisplayName;
            }
            //this just checks to make sure at least 1 display is assigned to the room
            ushort displayAssignedToRoomNum = _parent.manager.VideoDisplayZ.Values.FirstOrDefault(display => display.AssignedToRoomNum == roomNumber)?.Number ?? 0;
            if (_parent.logging) CrestronConsole.PrintLine("SubscribeToVideoMenuEvents called for room {0} tp-{1} displayAssignedToRoomNum: {2}", roomNumber, Number, displayAssignedToRoomNum);
            if (displayAssignedToRoomNum > 0)
            {
                RoomConfig room = _parent.manager.RoomZ[roomNumber];

                // Initialize values before subscribing
                ushort currentVidSrc = room.CurrentVideoSrc;
                if (currentVidSrc > 0)
                {
                    this.videoPageFlips(_parent.manager.VideoSourceZ[currentVidSrc].FlipsToPageNumber);//from updateTPVideoMenu
                    _parent.videoEISC1.UShortInput[(ushort)(Number + 300)].UShortValue = _parent.manager.VideoSourceZ[currentVidSrc].EquipID;
                    this.UserInterface.StringInput[2].StringValue = _parent.manager.VideoSourceZ[currentVidSrc].DisplayName;
                    ushort vsrcScenarioNum = _parent.manager.RoomZ[roomNumber].VideoSrcScenario;

                    ushort numSrcs = (ushort)_parent.manager.VideoSrcScenarioZ[vsrcScenarioNum].IncludedSources.Count;

                    for (ushort i = 0; i < numSrcs; i++)//loop through all music sources in this scenario
                    {
                        ushort srcNum = _parent.manager.VideoSrcScenarioZ[vsrcScenarioNum].IncludedSources[i];
                        if (srcNum == _parent.manager.RoomZ[roomNumber].CurrentVideoSrc)
                        {
                            this.videoButtonFB((ushort)(i + 1));
                        }
                    }
                }
                else
                {
                    this.videoPageFlips(0);
                    _parent.videoEISC1.UShortInput[(ushort)(Number + 300)].UShortValue = 0;//equipID
                    this.UserInterface.StringInput[2].StringValue = "Off";
                    this.videoButtonFB(0);

                }


                // Subscribe to changes in CurrentVideoSrc
                room.VideoSrcStatusChanged += VideoSrcStatusChangedHandler;
                room.DisplayChanged += UpdateTouchpanelDisplayName;
                if (room.CurrentDisplayNumber > 0 && _parent.manager.VideoDisplayZ.ContainsKey(room.CurrentDisplayNumber))
                {
                    UpdateTouchpanelDisplayName(room.Number, _parent.manager.VideoDisplayZ[room.CurrentDisplayNumber].DisplayName);
                }
                // Store reference to currently subscribed room
                currentSubscribedRoom = room;
                if (_parent.logging) CrestronConsole.PrintLine("FINISHED SubscribeToVideoMenuEvents called for room {0} tp-{1} currentVidSrc: {2}", room.Name, Number, currentVidSrc);
            }
        }

        private void MusicSrcStatusChangedHandler(ushort musicSrc, ushort flipsToPage, ushort equipID, string name, ushort buttonNum)
        {
            _parent.musicEISC1.UShortInput[(ushort)(Number + 100)].UShortValue = musicSrc;//for Media Server and sharing module
            this.musicPageFlips(flipsToPage);
            _parent.musicEISC1.UShortInput[(ushort)(Number + 300)].UShortValue = equipID;
            this.UserInterface.StringInput[3].StringValue = name;
            this.musicButtonFB(buttonNum);
        }
        private void VideoSrcStatusChangedHandler(ushort flipsToPage, ushort equipID, string name, ushort buttonNum)
        {
            this.videoPageFlips(flipsToPage);//from updateTPVideoMenu
            _parent.videoEISC1.UShortInput[(ushort)(Number + 300)].UShortValue = equipID;
            this.UserInterface.StringInput[2].StringValue = name;
            this.videoButtonFB(buttonNum);
        }
        private void UpdateTouchpanelDisplayName(ushort roomNumber, string displayName)
        {
            this.UserInterface.StringInput[10].StringValue = displayName;//current display name
            ushort displayNumber = _parent.manager.RoomZ[roomNumber].CurrentDisplayNumber;
            ushort videoOutputNumber = _parent.manager.VideoDisplayZ[displayNumber].VideoOutputNum;
            _parent.subsystemEISC.UShortInput[(ushort)((Number - 1) * 10 + 302)].UShortValue = videoOutputNumber;
        }
        public void ClearCurrentRoomSubscriptions()
        {
            try
            {
                // Unsubscribe from all current subscriptions and clear the StringInputs
                if (_roomListStatusSubscriptions != null && _roomListStatusSubscriptions.Count > 0)
                {
                    var subscriptions = _roomListStatusSubscriptions.ToList();
                    foreach (var subscription in subscriptions)
                    {
                        ushort eiscPosition = subscription.Key;

                        Action<ushort, string> handler = subscription.Value;

                        // Unsubscribe the handler from any events
                        foreach (var room in _parent.manager.RoomZ.Values)
                        {
                            try
                            {
                                room.HVACStatusChanged -= handler;
                                room.LightStatusChanged -= handler;
                                room.RoomStatusTextChanged -= handler;
                            }
                            catch (Exception ex)
                            {
                                ErrorLog.Error("Error unsubscribing handler: {0}", ex.Message);
                                CrestronConsole.PrintLine("Error unsubscribing handler: {0}", ex.Message);
                            }

                        }
                    }

                    // Clear the subscriptions dictionary
                    _roomListStatusSubscriptions.Clear();
                }

                // Clear all 30 possible StringInput slots
                for (ushort i = 0; i < 30; i++)
                {
                    this.UserInterface.SmartObjects[10].StringInput[(ushort)(3 * i + 12)].StringValue = "";
                }
            }
            catch (Exception ex)
            {
                CrestronConsole.PrintLine("Error in ClearCurrentRoomSubscriptions: {0}", ex.Message);
            }
        }
        public void SubscribeToWholeHouseListEvents()
        {
            if (_parent.manager == null)
            {
                CrestronConsole.PrintLine("_parent.manager is null.");
                return;
            }
            ClearCurrentRoomSubscriptions();//from SubscribeToWholeHouseListEvents
            //get the name of the current subsystem
            string subName = _parent.manager.SubsystemZ[CurrentSubsystemNumber].Name;
            if (subName.ToUpper().Contains("HVAC") || subName.ToUpper().Contains("CLIMATE"))
            {
                for (ushort j = 0; j < WholeHouseRoomList.Count; j++)
                {
                    // Get the subsystem scenario for the specified room
                    RoomConfig room = _parent.manager.RoomZ[WholeHouseRoomList[j]];
                    // Define the subscription
                    this.UserInterface.SmartObjects[10].StringInput[(ushort)(3 * j + 12)].StringValue = room.HVACStatusText;
                    CrestronConsole.PrintLine("SubscribeToWholeHouseListEvents HVACStatusText: {0}", room.HVACStatusText);
                    Action<ushort, string> statusSubscription = (rNumber, status) =>
                    {
                        this.UserInterface.SmartObjects[10].StringInput[(ushort)(3 * j + 12)].StringValue = status;
                    };
                    // Subscribe to the HVACStatusChanged event
                    room.HVACStatusChanged += statusSubscription;
                    // Add to the subscriptions dictionary
                    _roomListStatusSubscriptions[room.Number] = statusSubscription;
                    this.UserInterface.SmartObjects[10].StringInput[(ushort)(3 * j + 11)].StringValue = room.Name;
                    this.UserInterface.SmartObjects[10].StringInput[(ushort)(3 * j + 13)].StringValue = _parent.manager.SubsystemZ[CurrentSubsystemNumber].IconSerial;
                }
                this.UserInterface.SmartObjects[10].UShortInput[3].UShortValue = (ushort)WholeHouseRoomList.Count;
            }
            else if (subName.ToUpper().Contains("LIGHT"))
            {
                for (ushort j = 0; j < WholeHouseRoomList.Count; j++)
                {
                    // Get the subsystem scenario for the specified room
                    RoomConfig room = _parent.manager.RoomZ[WholeHouseRoomList[j]];
                    // Store the room and index in closure-safe variables
                    ushort capturedIndex = j;
                    ushort roomNumber = room.Number;
                    this.UserInterface.SmartObjects[10].StringInput[(ushort)(3 * capturedIndex + 12)].StringValue = room.LightStatusText;
                    // Define the subscription with properly captured variables
                    Action<ushort, string> statusSubscription = (rNumber, status) =>
                    {
                        if (rNumber == roomNumber)
                        {
                            this.UserInterface.SmartObjects[10].StringInput[(ushort)(3 * capturedIndex + 12)].StringValue = status;
                            CrestronConsole.PrintLine("Light status updated for room {0}: {1}", rNumber, status);
                        }
                    };
                    // Subscribe to the LightStatusChanged event
                    room.LightStatusChanged += statusSubscription;
                    // Add to the subscriptions dictionary
                    _roomListStatusSubscriptions[room.Number] = statusSubscription;
                    this.UserInterface.SmartObjects[10].StringInput[(ushort)(3 * j + 11)].StringValue = room.Name;
                    this.UserInterface.SmartObjects[10].StringInput[(ushort)(3 * j + 13)].StringValue = _parent.manager.SubsystemZ[CurrentSubsystemNumber].IconSerial;
                }
                this.UserInterface.SmartObjects[10].UShortInput[3].UShortValue = (ushort)WholeHouseRoomList.Count;
            }
        }
    }
}
