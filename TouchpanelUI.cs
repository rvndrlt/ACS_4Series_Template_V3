//-----------------------------------------------------------------------
// <copyright file="TouchpanelUI.cs" company="Crestron">
//     Copyright (c) Crestron Electronics. All rights reserved.
// </copyright>
//-----------------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;

//using System.Threading.Tasks;
using Crestron.SimplSharp;                       // For Basic SIMPL# Classes
using Crestron.SimplSharp.CrestronIO;            // For Directory
using Crestron.SimplSharp.Reflection;
using Crestron.SimplSharpPro;                    // For Basic SIMPL#Pro classes
using Crestron.SimplSharpPro.CrestronThread;     // For Threading
using Crestron.SimplSharpPro.DeviceSupport;      // For Generic Device Support
using Crestron.SimplSharpPro.Diagnostics;        // For System Monitor Access

namespace ACS_4Series_Template_V3.UI
{
    /// <summary>
    /// Allows us to instantiate and register a touchpanel dynamically
    /// </summary>
    public class TouchpanelUI
    {
        
        

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
            securityZoneLiist = 21,
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
        public List<ushort> WholeHouseRoomList = new List<ushort>();
        public List<ushort> MusicRoomsToShareSourceTo = new List<ushort>();
        public List<bool> MusicRoomsToShareCheckbox = new List<bool>();
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
                this.UserInterface.SigChange += this.UserInterfaceObject_SigChange;

                // load smart objects
                string sgdPath = Path.Combine(Directory.GetApplicationDirectory(), "TSW-770.sgd");

                this.UserInterface.LoadSmartObjects(sgdPath);
                //ErrorLog.Notice(string.Format(LogHeader + "Loaded SmartObjects: {0}", this.UserInterface.SmartObjects.Count));
                foreach (KeyValuePair<uint, SmartObject> smartObject in this.UserInterface.SmartObjects)
                {
                    smartObject.Value.SigChange += new Crestron.SimplSharpPro.SmartObjectSigChangeEventHandler(this.SO_SigChange);
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
        private void SO_SigChange(GenericBase currentDevice, SmartObjectEventArgs args)
        {
            ushort TPNumber = this.Number;
            CrestronConsole.PrintLine("smartobject--TP-{0}-SmrtID{1} number{2} ", TPNumber, args.SmartObjectArgs.ID, args.Sig.Number);
            
            switch ((SmartObjectIDs)args.SmartObjectArgs.ID)
            {
                case SmartObjectIDs.cameraKeypad:
                    break;
                case SmartObjectIDs.securityKeypad:
                    break;
                case SmartObjectIDs.securityZoneLiist:
                    break;
                case SmartObjectIDs.spa:
                    break;
                case SmartObjectIDs.poolTab:
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
                                _parent.SelectSubsystem(TPNumber, subsystemButtonNumber);
                            }
                        }
                        break; }
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
                                _parent.SelectZone((TPNumber), (ushort)args.Sig.UShortValue, true);
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
                            CrestronConsole.PrintLine("TPRoom {0} tpCurrentSrc {1} ", tpCurrentRoom, audioSrcName);
                            CrestronConsole.PrintLine("command {0} slot{1} {2}", command, roomListPosition, roomname);
                            if (audioID > 0) { 
                                switch (command) {
                                    case 0://save volume
                                        break;
                                    case 1://checkbox toggle
                                        if (args.Sig.BoolValue == true) { this.MusicRoomsToShareCheckbox[roomListPosition - 1] = !this.MusicRoomsToShareCheckbox[roomListPosition - 1]; }
                                        CrestronConsole.PrintLine("checkbox {0} {1}", roomListPosition, this.MusicRoomsToShareCheckbox[roomListPosition - 1]);
                                        this.UserInterface.SmartObjects[7].BooleanInput[(ushort)(roomListPosition * 7 + 4004)].BoolValue = this.MusicRoomsToShareCheckbox[roomListPosition - 1];//checkbox fb
                                        this.UserInterface.SmartObjects[7].BooleanInput[(ushort)(roomListPosition * 7 + 4009)].BoolValue = this.MusicRoomsToShareCheckbox[roomListPosition - 1];//show/hide vol buttons
                                        //if the checkbox is selected, then send the source to the room
                                        if (this.MusicRoomsToShareCheckbox[roomListPosition - 1]) {
                                            this.UserInterface.SmartObjects[7].StringInput[(ushort)(roomListPosition * 2 + 10)].StringValue = audioSrcName;
                                            _parent.SwitcherSelectMusicSource(audioID, audioSrcNum);
                                        }
                                        else {
                                            this.UserInterface.SmartObjects[7].StringInput[(ushort)(roomListPosition * 2 + 10)].StringValue = "Off";
                                            _parent.SwitcherSelectMusicSource(audioID, 0);
                                        }
                                        
                                        break;
                                    case 2://vol up
                                        CrestronConsole.PrintLine("vol up {0}", _parent.manager.RoomZ[roomNumber].Name);
                                        if (args.Sig.BoolValue == true) { 
                                            _parent.musicEISC1.BooleanInput[(ushort)(audioID)].BoolValue = true;
                                        }
                                        else { 
                                            _parent.musicEISC1.BooleanInput[(ushort)(audioID)].BoolValue = false; 
                                        }
                                        break;
                                    case 3://vol dn
                                        CrestronConsole.PrintLine("vol down {0}", _parent.manager.RoomZ[roomNumber].Name);
                                        if (args.Sig.BoolValue == true) {
                                            _parent.musicEISC1.BooleanInput[(ushort)(audioID +100)].BoolValue = true; 
                                        }
                                        else {
                                            _parent.musicEISC1.BooleanInput[(ushort)(audioID +100)].BoolValue = false; 
                                        }
                                        break;
                                    case 4://mute
                                        if (args.Sig.BoolValue == true) { 
                                            _parent.musicEISC1.BooleanInput[(ushort)(audioID + 200)].BoolValue = true;
                                            _parent.musicEISC1.BooleanInput[(ushort)(audioID + 200)].BoolValue = false;
                                        }
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
                                        this.UserInterface.SmartObjects[7].StringInput[(ushort)(i * 2 + 12)].StringValue = _parent.manager.MusicSourceZ[asrcNumberToSend].Name;
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
                            _parent.SelectSubsystem(TPNumber, subsystemButtonNumber);
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
                                    _parent.subsystemEISC.UShortInput[(ushort)((TPNumber - 1) * 10 + 305)].UShortValue = _parent.manager.RoomZ[currentRoomNumber].ClimateID;
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
                default:
                    break;
            }
        }

        private void onAnalogChangeEvent(uint deviceID, SigEventArgs args)
        { 
        
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
                CrestronConsole.PrintLine("Sig Change Event: {0}, Value: {1}", args.Sig.Number, args.Sig.BoolValue);
                if (args.Sig.Number == 1007)
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
                        currentDevice.BooleanInput[11].BoolValue = true;//flip to home start pulse
                        _parent.HomeButtonPress(tpNumber);//
                        currentDevice.BooleanInput[11].BoolValue = false;//flip to home end pulse
                        this.UserInterface.BooleanInput[998].BoolValue = false;//clear the sharing sub
                        this.UserInterface.BooleanInput[999].BoolValue = false;//clear the sharing sub with floors
                        this.UserInterface.BooleanInput[1002].BoolValue = false;//clear the sharing button
                    }
                    else if (args.Sig.Number == 15)
                    {
                        _parent.RoomButtonPress(tpNumber, false);//room controls page select - go straight to the current room subsystems list
                        this.musicPageFlips(0);
                        this.UserInterface.BooleanInput[998].BoolValue = false;//clear the sharing sub
                        this.UserInterface.BooleanInput[999].BoolValue = false;//clear the sharing sub with floors
                        this.UserInterface.BooleanInput[1002].BoolValue = false;//clear the sharing button
                    }
                    else if (args.Sig.Number == 16)
                    {
                        _parent.RoomListButtonPress(tpNumber);//list of rooms page
                        this.musicPageFlips(0);
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
                    else if (args.Sig.Number == 55)
                    {
                        //TODO - toggle music source sub. iphone only.
                    }
                    else if (args.Sig.Number == 60)
                    {
                        //TODO - toggle lift menu
                        this.UserInterface.BooleanInput[60].BoolValue = !this.UserInterface.BooleanInput[60].BoolValue;
                    }
                    else if (args.Sig.Number == 99)//this is the back arrow
                    {
                        subsystemPageFlips(0);
                        _parent.subsystemEISC.UShortInput[(ushort)(tpNumber + 200)].UShortValue = 0;//subsystem equipID 0 disconnects from the subsystem
                    }
                    else if (args.Sig.Number == 100)//this is the X close subsystem button
                    {
                        _parent.PressCloseXButton(tpNumber);
                    }
                    else if (args.Sig.Number == 150)
                    {
                        //TODO - video power off
                        this.videoPageFlips(0);
                        this.videoButtonFB(0);
                        _parent.SelectVideoSourceFromTP(tpNumber, 0);   
                    }
                    else if (args.Sig.Number == 154)
                    {
                        //TODO - video volume up
                    }
                    else if (args.Sig.Number == 155)
                    {
                        //TODO - video volume down
                    }
                    else if (args.Sig.Number == 156)
                    {
                        //TODO - video mute
                    }
                    else if (args.Sig.Number == 160)
                    {
                        //TODO - toggle sleep menu
                    }
                    else if (args.Sig.Number == 180)
                    {
                        //TODO - toggle format
                    }
                    else if (args.Sig.Number == 351)
                    {
                        //TODO - change TV
                    }
                    else if (args.Sig.Number == 352)
                    {
                        //TODO - all tvs off
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
                                    this.UserInterface.SmartObjects[7].StringInput[(ushort)(i * 2 + 12)].StringValue = "Off";
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
                            this.UserInterface.SmartObjects[7].StringInput[(ushort)(i * 2 + 12)].StringValue = _parent.manager.MusicSourceZ[audioSrcNum].Name;
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
                                this.UserInterface.SmartObjects[7].StringInput[(ushort)(i * 2 + 12)].StringValue = "Off";
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
                CrestronConsole.PrintLine("Sig Change Event: {0}, Value: {1}", args.Sig.Number, args.Sig.UShortValue);
            }
        }
        public void subsystemPageFlips(ushort pageNumber) 
        {
            //clear the current subsystem page
            this.UserInterface.BooleanInput[50].BoolValue = false;//clear the list of rooms page
            this.UserInterface.BooleanInput[100].BoolValue = false;//clear the room subsystems page
            for (ushort i = 0; i < 20; i++)
            {
                this.UserInterface.BooleanInput[(ushort)(i + 101)].BoolValue = false;
            }
            for (ushort i = 0; i < 10; i++)
            {
                this.UserInterface.BooleanInput[(ushort)(i + 91)].BoolValue = false;//clear the whole house subsystems.
            }
            //show the subsystem page
            if (pageNumber == 1000){ this.UserInterface.BooleanInput[50].BoolValue = true;}//list of rooms page
            else if (pageNumber == 0) { this.UserInterface.BooleanInput[100].BoolValue = true; }//room subsystems page
            else if (pageNumber < 90){this.UserInterface.BooleanInput[(ushort)(pageNumber + 100)].BoolValue = true;}//subsystem to show
            else {this.UserInterface.BooleanInput[pageNumber].BoolValue = true;}//whole house subsystems
        }
        public void videoPageFlips(ushort pageNumber) 
        {
            this.CurrentVideoPageNumber = pageNumber;
            if (this.CurrentSubsystemIsVideo) { 
                for (ushort i = 0; i < 20; i++) { 
                    this.UserInterface.BooleanInput[(ushort)(i + 121)].BoolValue = false;//clear any video subpages
                }
                this.UserInterface.BooleanInput[(ushort)(pageNumber + 120)].BoolValue = true;//show the video subpage
            }
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
            if (this.CurrentSubsystemIsAudio)
            {
                CrestronConsole.PrintLine("current subsystem is audio");
                //TODO - this is for the iphone. add test for iphone
                if (pageNumber == 0)
                {
                    this.UserInterface.BooleanInput[55].BoolValue = true;//show the source list page
                    this.UserInterface.BooleanInput[56].BoolValue = false;//hide the source list button
                }
                else {
                    this.UserInterface.BooleanInput[55].BoolValue = false;//hide the source list page
                    this.UserInterface.BooleanInput[56].BoolValue = true;//show the source list button
                    this.UserInterface.BooleanInput[(ushort)(pageNumber + 1010)].BoolValue = true;//show the music sources subpage
                }
                
            }
        }
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
        public void UnsubscribeTouchpanelFromAllVolMuteChanges()
        {
            CrestronConsole.PrintLine("unsubscribing from mute changes");
            foreach (var kvp in this.MuteChangeHandlers)
            {
                CrestronConsole.PrintLine("unsubscribing from {0}", kvp.Key);
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
        }
    }
}
