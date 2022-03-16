using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Crestron.SimplSharp;                          	// For Basic SIMPL# Classes
using System.Timers;
using Crestron.SimplSharpPro;                       	// For Basic SIMPL#Pro classes
using Crestron.SimplSharpPro.CrestronThread;        	// For Threading
using Crestron.SimplSharpPro.Diagnostics;		    	// For System Monitor Access
using Crestron.SimplSharpPro.DeviceSupport;         	// For Generic Device Support
using Crestron.SimplSharpPro.UI;
using Crestron.SimplSharp.CrestronIO;
using Crestron.SimplSharpPro.Lighting;
using Crestron.SimplSharpPro.EthernetCommunication;
using Newtonsoft.Json;

namespace ACS_4Series_Template_V1
{
    public class ControlSystem : CrestronControlSystem
    {
        //public InternalRFExGateway gway;
        //public Tsr302 My302;
        //public ThreeSeriesTcpIpEthernetIntersystemCommunications 
        public EthernetIntersystemCommunications roomSelectEISC, subsystemEISC, musicEISC1, musicEISC2, musicEISC3, videoEISC1, videoEISC2, videoEISC3, lightingEISC, HVACEISC, imageEISC;
        private Configuration.ConfigManager config;
        private QuickConfiguration.QuickConfigManager quickActionConfig;
        //private ConfigData.Configuration RoomConfig;
        private static CCriticalSection configLock = new CCriticalSection();
        public static bool initComplete = false;
        public static bool NAXsystem = false;
        public string[] multis = new string[100];
        public ushort[] volumes = new ushort[100];
        public string[] currentProviders = new string[100];
        public string IPaddress;
        private SystemManager manager;
        private QuickSystemManager quickActionManager;
        private readonly uint appID;
        public List<ushort> roomList = new List<ushort>();
        public CTimer NAXoutputChangedTimer;
        public static Timer xtimer;
        public bool NAXtimerBusy = false;
        public ushort lastMusicSrc, lastSwitcherInput, lastSwitcherOutput;

        /// <summary>
        /// ControlSystem Constructor. Starting point for the SIMPL#Pro program.
        /// Use the constructor to:
        /// * Initialize the maximum number of threads (max = 400)
        /// * Register devices
        /// * Register event handlers
        /// * Add Console Commands
        /// 
        /// Please be aware that the constructor needs to exit quickly; if it doesn't
        /// exit in time, the SIMPL#Pro program will exit.
        /// 
        /// You cannot send / receive data in the constructor
        /// </summary>
        public ControlSystem()
            : base()
        {

            if (this.SupportsEthernet)
            {
                roomSelectEISC = new EthernetIntersystemCommunications(0x89, "127.0.0.2", this);
                subsystemEISC = new EthernetIntersystemCommunications(0x8A, "127.0.0.2", this);
                musicEISC1 = new EthernetIntersystemCommunications(0x8B, "127.0.0.2", this);
                musicEISC2 = new EthernetIntersystemCommunications(0x8C, "127.0.0.2", this);
                musicEISC3 = new EthernetIntersystemCommunications(0x8D, "127.0.0.2", this);
                videoEISC1 = new EthernetIntersystemCommunications(0x8E, "127.0.0.2", this);
                videoEISC2 = new EthernetIntersystemCommunications(0x8F, "127.0.0.2", this);
                videoEISC3 = new EthernetIntersystemCommunications(0x90, "127.0.0.2", this);
                imageEISC = new EthernetIntersystemCommunications(0x91, "127.0.0.2", this);
                lightingEISC = new EthernetIntersystemCommunications(0x9A, "127.0.0.2", this);
                HVACEISC = new EthernetIntersystemCommunications(0x9B, "127.0.0.2", this);
                

                roomSelectEISC.SigChange += new SigEventHandler(MainsigChangeHandler);
                subsystemEISC.SigChange += new SigEventHandler(SubsystemSigChangeHandler);
                musicEISC1.SigChange += new SigEventHandler(Music1SigChangeHandler);
                musicEISC2.SigChange += new SigEventHandler(Music2SigChangeHandler);
                musicEISC3.SigChange += new SigEventHandler(Music3SigChangeHandler);
                videoEISC1.SigChange += new SigEventHandler(Video1SigChangeHandler);
                videoEISC2.SigChange += new SigEventHandler(Video2SigChangeHandler);
                videoEISC3.SigChange += new SigEventHandler(Video3SigChangeHandler);
                imageEISC.SigChange += new SigEventHandler(ImageSigChangeHandler);
                lightingEISC.SigChange += new SigEventHandler(LightingSigChangeHandler);
                HVACEISC.SigChange += new SigEventHandler(HVACSigChangeHandler);
                if (roomSelectEISC.Register() != eDeviceRegistrationUnRegistrationResponse.Success)
                    ErrorLog.Error("roomSelectEISC failed registration. Cause: {0}", roomSelectEISC.RegistrationFailureReason);
                if (subsystemEISC.Register() != eDeviceRegistrationUnRegistrationResponse.Success)
                    ErrorLog.Error("subsystemEISC failed registration. Cause: {0}", subsystemEISC.RegistrationFailureReason);
                if (musicEISC1.Register() != eDeviceRegistrationUnRegistrationResponse.Success)
                    ErrorLog.Error("musicEISC1 failed registration. Cause: {0}", musicEISC1.RegistrationFailureReason);
                if (musicEISC2.Register() != eDeviceRegistrationUnRegistrationResponse.Success)
                    ErrorLog.Error("musicEISC2 failed registration. Cause: {0}", musicEISC2.RegistrationFailureReason);
                if (musicEISC3.Register() != eDeviceRegistrationUnRegistrationResponse.Success)
                    ErrorLog.Error("musicEISC3 failed registration. Cause: {0}", musicEISC3.RegistrationFailureReason);
                if (videoEISC1.Register() != eDeviceRegistrationUnRegistrationResponse.Success)
                    ErrorLog.Error("videoEISC1 failed registration. Cause: {0}", videoEISC1.RegistrationFailureReason);
                if (videoEISC2.Register() != eDeviceRegistrationUnRegistrationResponse.Success)
                    ErrorLog.Error("videoEISC2 failed registration. Cause: {0}", videoEISC2.RegistrationFailureReason);
                if (videoEISC3.Register() != eDeviceRegistrationUnRegistrationResponse.Success)
                    ErrorLog.Error("videoEISC3 failed registration. Cause: {0}", videoEISC3.RegistrationFailureReason);
                if (imageEISC.Register() != eDeviceRegistrationUnRegistrationResponse.Success)
                    ErrorLog.Error("imageEISC failed registration. Cause: {0}", imageEISC.RegistrationFailureReason);
                if (lightingEISC.Register() != eDeviceRegistrationUnRegistrationResponse.Success)
                    ErrorLog.Error("lighting EISC failed registration. Cause: {0}", lightingEISC.RegistrationFailureReason);
                if (HVACEISC.Register() != eDeviceRegistrationUnRegistrationResponse.Success)
                    ErrorLog.Error("videoEISC3 failed registration. Cause: {0}", HVACEISC.RegistrationFailureReason);
            }
            try
            {
                Thread.MaxNumberOfUserThreads = 20;

                //Subscribe to the controller events (System, Program, and Ethernet)
                CrestronEnvironment.SystemEventHandler += new SystemEventHandler(ControlSystem_ControllerSystemEventHandler);
                CrestronEnvironment.ProgramStatusEventHandler += new ProgramStatusEventHandler(ControlSystem_ControllerProgramEventHandler);
                CrestronEnvironment.EthernetEventHandler += new EthernetEventHandler(ControlSystem_ControllerEthernetEventHandler);
                this.appID = InitialParametersClass.ApplicationNumber;
                if (!CrestronConsole.AddNewConsoleCommand(ReinitializeSystem, "reloadjson", "reload the json file", ConsoleAccessLevelEnum.AccessOperator))
                {
                    ErrorLog.Error("Unable to add 'reload' command to console");
                }
                CrestronConsole.PrintLine("starting program {0}", this.ProgramNumber);

            }
            catch (Exception e)
            {
                ErrorLog.Error("Error in the constructor: {0}", e.Message);
            }
        }
        /*public Crestron.SimplSharp.SimplSharpProConsoleCmdFunction reloadJSON(){
            this.SystemSetup();
            return Crestron.SimplSharp.SimplSharpProConsoleCmdFunction;
        }*/
        public void ReinitializeSystem(string parms)
        {
            if (parms == "?")
            {
                CrestronConsole.ConsoleCommandResponse("reloadjson\n\r\treloads the configuration from the json file.\n\r");
            }
            else
            {
                InitializeSystem();
            }
        }
        void MainsigChangeHandler(GenericBase currentDevice, SigEventArgs args)
        {
            if (args.Event == eSigEvent.UShortChange)
            {
                if (args.Sig.Number <= 100)//select a floor#
                {
                    if (args.Sig.UShortValue <= 100)
                    {
                        SelectFloor((ushort)args.Sig.Number, (ushort)args.Sig.UShortValue);
                    }
                }
                else if (args.Sig.Number > 100 && args.Sig.Number < 201)
                {
                    ushort TPNumber = (ushort)(args.Sig.Number - 100);
                    manager.touchpanelZ[TPNumber].CurrentPageNumber = 2; // 2 = roomSubsystemList
                    SelectZone((TPNumber), (ushort)args.Sig.UShortValue);
                }
                else if (args.Sig.Number > 200)
                {
                    SelectSubsystem((ushort)(args.Sig.Number - 200), (ushort)args.Sig.UShortValue);
                }
            }
            if (args.Event == eSigEvent.StringChange)
            {
                if (args.Sig.Number > 0)
                {
                    manager.RoomZ[(ushort)args.Sig.Number].Name = args.Sig.StringValue;
                }
            }
            if (args.Event == eSigEvent.BoolChange)
            {
                if (args.Sig.Number >= 1)
                {
                    if (args.Sig.BoolValue == true)
                    {

                        SelectOnlyFloor((ushort)args.Sig.Number); //change room button pressed - this is the "<" left arrow button
                        manager.touchpanelZ[(ushort)args.Sig.Number].CurrentPageNumber = 1;// 1 = roomListPage
                        //update the rooms now playing status text
                        UpdateRoomsPageStatusText((ushort)args.Sig.Number);
                    }
                    else if (args.Sig.BoolValue == false) //the SR went low which means a room was selected
                    {
                        manager.touchpanelZ[(ushort)args.Sig.Number].CurrentPageNumber = 2; //this may be wrong. need to test
                    }
                }
            }
        }
        void SubsystemSigChangeHandler(GenericBase currentDevice, SigEventArgs args) {
            if (args.Event == eSigEvent.BoolChange && args.Sig.BoolValue == true) {
                if (args.Sig.Number > 100 && args.Sig.Number < 200)//home page button was pressedd
                {
                    ushort TPNumber = (ushort)(args.Sig.Number - 100);

                    //this function updates the current page number to home and updates the subsystem list
                    HomeButtonPress(TPNumber);
                    
                }
                else if (args.Sig.Number > 200 && args.Sig.Number < 300)//rooms page button was pressed
                {
                    
                    ushort TPNumber = (ushort)(args.Sig.Number - 200);
                    ushort currentRoom = manager.touchpanelZ[TPNumber].CurrentRoomNum;
                    manager.touchpanelZ[TPNumber].CurrentPageNumber = 1;//touchpanel is now on the roomList page
                    subsystemEISC.BooleanInput[(ushort)(TPNumber + 100)].BoolValue = false;
                    subsystemEISC.BooleanInput[(ushort)(TPNumber + 200)].BoolValue = true;//flip to rooms page

                    //update the room status text
                    SelectFloor(TPNumber, 0);
                    SelectZone(TPNumber, 0);

                }
                else if (args.Sig.Number > 300 && args.Sig.Number < 400)//arrow back button pressed
                {
                    ushort TPNumber = (ushort)(args.Sig.Number - 300);
                    //show the whole house list of subsystems
                    if (manager.touchpanelZ[TPNumber].CurrentPageNumber == 0)//the panel is currently on the HOME menu 
                    {
                        subsystemEISC.UShortInput[(ushort)(TPNumber + 100)].UShortValue = 0;//flip to page number 0 clears the subsystem page
                        subsystemEISC.UShortInput[(ushort)(TPNumber + 200)].UShortValue = 0;//subsystem equipID 0 disconnects from the subsystem
                    }
                }
                else if (args.Sig.Number > 400 && args.Sig.Number < 500)//close X pressed
                {
                    ushort TPNumber = (ushort)(args.Sig.Number - 400);
                    //first handle the case of the rooms menu
                    if (manager.touchpanelZ[TPNumber].CurrentPageNumber > 0)//the panel is currently not on the HOME page
                    {
                        manager.touchpanelZ[TPNumber].CurrentPageNumber = 2;//just closed a subystem menu so now were on the subystem list of a room
                        subsystemEISC.UShortInput[(ushort)(TPNumber + 100)].UShortValue = 0;//flip to page number 0 clears the subsystem page
                        subsystemEISC.UShortInput[(ushort)(TPNumber + 200)].UShortValue = 0;//subsystem equipID 0 disconnects from the subsystem
                        imageEISC.BooleanInput[TPNumber].BoolValue = false;//clear "current subsystem is video"
                        imageEISC.BooleanInput[(ushort)(TPNumber +100)].BoolValue = false;//clear "current subsystem is audio"

                    }
                    //then handle the case of the home menu
                    else
                    {
                        //we want to go back to the zone list page
                        WholeHouseUpdateZoneList(TPNumber);
                        SendSubsystemZonesPageNumber(TPNumber, true);

                    }
                }
            }
        }
        void Music1SigChangeHandler(GenericBase currentDevice, SigEventArgs args)
        {
            if (args.Event == eSigEvent.BoolChange)
            {
                if (args.Sig.BoolValue == true)
                {
                    if (args.Sig.Number <= 100)
                    {
                        ushort TPNumber = (ushort)(args.Sig.Number);
                        manager.touchpanelZ[TPNumber].CurrentASrcGroupNum++;
                        SetASRCGroup(TPNumber, manager.touchpanelZ[TPNumber].CurrentASrcGroupNum);
                    }
                    else if (args.Sig.Number <= 200) {
                        ushort actionNumber = (ushort)(args.Sig.Number - 100);
                        AudioFloorOff(actionNumber); // HA ALL OFF or floor off
                    }
                }
            }
            if (args.Event == eSigEvent.UShortChange)
            {
                if (args.Sig.Number <= 100)//select a music source
                {
                    SelectMusicSource((ushort)args.Sig.Number, args.Sig.UShortValue);
                }

                else if (args.Sig.Number <= 600)
                {
                    if (NAXsystem)
                    {
                        NAXOutputSrcChanged((ushort)args.Sig.Number, args.Sig.UShortValue);
                    }
                    else
                    {
                        SwampOutputSrcChanged((ushort)args.Sig.Number, args.Sig.UShortValue);
                    }
                }
                else if (args.Sig.Number <= 700)
                {
                    ushort playerNumber = (ushort)(args.Sig.Number - 600);
                    StreamingPlayerProviderChanged(playerNumber, args.Sig.UShortValue);
                }

            }
        }
        void Music2SigChangeHandler(GenericBase currentDevice, SigEventArgs args)
        {
            if (args.Event == eSigEvent.UShortChange)
            {
                if (args.Sig.Number <= 100)//select a music source
                {
                        SelectShareSource((ushort)args.Sig.Number, args.Sig.UShortValue);

                }
            }
            if (args.Event == eSigEvent.BoolChange)
            {
                if (args.Sig.Number <= 100)
                {
                    //updateSharingSrc((ushort)args.Sig.Number);
                }
            }
        }
        void Music3SigChangeHandler(GenericBase currentDevice, SigEventArgs args)
        {
            if (args.Event == eSigEvent.UShortChange)
            {
                if (args.Sig.Number == 1)//save quick action
                {
                    CrestronConsole.PrintLine("saving preset{0}", args.Sig.UShortValue);
                    SaveMusicSettingsToPreset(args.Sig.UShortValue);
                }
                else if (args.Sig.Number == 2) //recall quick action
                {
                    CrestronConsole.PrintLine("recalling preset{0}", args.Sig.UShortValue);
                }
                else if (args.Sig.Number <= 200)
                {
                    ushort switcherOutNum = (ushort)(args.Sig.Number - 100);
                    volumes[switcherOutNum-1] = args.Sig.UShortValue;//this stores the zones current volume
                }
            }
            if (args.Event == eSigEvent.BoolChange)
            {
                if (args.Sig.Number == 1)
                {
                    RequestMusicSources();
                }
            }
            if (args.Event == eSigEvent.StringChange) {
                if (args.Sig.Number > 300) {
                    multis[args.Sig.Number - 300] = args.Sig.StringValue;
                    NAXZoneMulticastChanged((ushort)args.Sig.Number, args.Sig.StringValue);
                }
            }
        }
        void Video1SigChangeHandler(GenericBase currentDevice, SigEventArgs args)
        {
            if (args.Event == eSigEvent.BoolChange)
            {
                if (args.Sig.Number <= 100)
                {
                    if (args.Sig.BoolValue == true)
                    {
                        ushort TPNumber = (ushort)(args.Sig.Number);
                        manager.touchpanelZ[TPNumber].CurrentVSrcGroupNum++;//more vsrcs button pressed so increment the group number
                        //group = (ushort)(tpConfigs[TPNumber].currentVSrcGroupNum + 1);
                        SetVSRCGroup(TPNumber, manager.touchpanelZ[TPNumber].CurrentVSrcGroupNum);
                    }
                }
            }
            if (args.Event == eSigEvent.UShortChange)
            {
                if (args.Sig.Number <= 100)//select a video source
                {
                    CrestronConsole.PrintLine("TP-{0} select vsrc{1}", args.Sig.Number, args.Sig.UShortValue);
                    SelectVideoSource((ushort)args.Sig.Number, args.Sig.UShortValue);
                }
                else if (args.Sig.Number > 500 && initComplete)
                {
                    CrestronConsole.PrintLine("dm output changed------------ {0} {1} {2}", args.Sig.Number, args.Sig.UShortValue, initComplete);
                    DmOutputChanged((ushort)args.Sig.Number, args.Sig.UShortValue);

                }
            }
        }
        void Video2SigChangeHandler(GenericBase currentDevice, SigEventArgs args)
        {
            if (args.Event == eSigEvent.UShortChange)
            {
                if (args.Sig.Number <= 100)//select a music source
                {
                    {
                        //selectVideoSource((ushort)args.Sig.Number, args.Sig.UShortValue);
                    }
                }
            }
        }
        void Video3SigChangeHandler(GenericBase currentDevice, SigEventArgs args) { }
        void ImageSigChangeHandler(GenericBase currentDevice, SigEventArgs args) {
            if (args.Event == eSigEvent.UShortChange)
            {
                try
                {
                    if (args.Sig.Number <= 100 && args.Sig.UShortValue > 0)//select a zone
                    {

                        ushort TPNumber = (ushort)args.Sig.Number;
                        ushort subsystemNumber = manager.touchpanelZ[TPNumber].CurrentSubsystemNumber;
                        ushort currentRoomNumber = 0;
                        if (roomList.Count > 0)
                        {
                            currentRoomNumber = roomList[args.Sig.UShortValue - 1];
                            manager.touchpanelZ[TPNumber].CurrentRoomNum = currentRoomNumber;
                            subsystemEISC.StringInput[TPNumber].StringValue = manager.RoomZ[currentRoomNumber].Name;
                        }
                        

                        
                        if (subsystemNumber > 0)
                        {
                            subsystemEISC.UShortInput[(ushort)(TPNumber + 100)].UShortValue = (ushort)(manager.SubsystemZ[subsystemNumber].FlipsToPageNumber);
                            subsystemEISC.UShortInput[(ushort)(TPNumber + 200)].UShortValue = (ushort)(manager.SubsystemZ[subsystemNumber].EquipID + TPNumber); //get the equipID for the subsystem
                        }
                        if (currentRoomNumber > 0)
                        {
                            subsystemEISC.UShortInput[(ushort)((TPNumber - 1) * 10 + 303)].UShortValue = manager.RoomZ[currentRoomNumber].LightsID;
                            subsystemEISC.UShortInput[(ushort)((TPNumber - 1) * 10 + 304)].UShortValue = manager.RoomZ[currentRoomNumber].ShadesID;
                            subsystemEISC.UShortInput[(ushort)((TPNumber - 1) * 10 + 305)].UShortValue = manager.RoomZ[currentRoomNumber].ClimateID;
                        }

                    }
                }
                catch (Exception e) {
                    ErrorLog.Warn("imagesigchangehandler number {0} value {1} {2}", args.Sig.Number, args.Sig.UShortValue, e.Message);
                }
            }
        }
        void LightingSigChangeHandler(GenericBase currentDevice, SigEventArgs args)
        {
            if (args.Event == eSigEvent.BoolChange)
            {
                UpdateLightingStatus((ushort)args.Sig.Number, args.Sig.BoolValue);
            }
        }

        void HVACSigChangeHandler(GenericBase currentDevice, SigEventArgs args) {
            ushort roomNumber = 0;
            if (args.Event == eSigEvent.UShortChange)
            {
                ushort zoneNumber = 0;
                ushort function = 0;
                
                if (args.Sig.Number <= 100)//temp changed
                {
                    zoneNumber = (ushort)args.Sig.Number;
                    function = 1;
                    //updateCurrentTemp((ushort)args.Sig.Number, args.Sig.UShortValue);

                }
                else if (args.Sig.Number <= 200)//heat setpoint changed
                {
                    //updateHeatSetpoint((ushort)args.Sig.Number, args.Sig.UShortValue);
                    zoneNumber = (ushort)(args.Sig.Number - 100);
                    function = 2;
                }
                else if (args.Sig.Number <= 300)//cool setpoint changed
                {
                    //updateCoolSetpoint((ushort)args.Sig.Number, args.Sig.UShortValue);
                    zoneNumber = (ushort)(args.Sig.Number - 200);
                    function = 3;
                }
                foreach (var room in manager.RoomZ)
                {
                    if (room.Value.ClimateID == zoneNumber)
                    {
                        roomNumber = room.Value.Number;
                        switch (function) {
                            case (1): {
                                    room.Value.CurrentTemperature = args.Sig.UShortValue;
                                    break; }
                            case (2): {
                                    room.Value.CurrentHeatSetpoint = args.Sig.UShortValue; 
                                    break; }
                            case (3): {
                                    room.Value.CurrentCoolSetpoint = args.Sig.UShortValue; 
                                    break; }
                            default: break;
                        }
                        if (manager.RoomZ[roomNumber].CurrentTemperature > 0) { 
                            UpdateRoomHVACText(roomNumber);
                        }
                    }
                }
                
            }
            if (args.Event == eSigEvent.BoolChange)
            {
                if (args.Sig.BoolValue == true)
                {
                    ushort zoneNumber = 0;
                    ushort function = 0;
                    if (args.Sig.Number <= 100)//mode changed to auto
                    {
                        //updateMode((ushort)args.Sig.Number);
                        zoneNumber = (ushort)args.Sig.Number;
                        function = 1;
                    }
                    else if (args.Sig.Number <= 200)//mode changed to heat
                    {
                        //updateMode((ushort)args.Sig.Number);
                        zoneNumber = (ushort)(args.Sig.Number - 100);
                        function = 2;
                    }
                    else if (args.Sig.Number <= 300)//mode changed to cool
                    {
                        //updateMode((ushort)args.Sig.Number);
                        zoneNumber = (ushort)(args.Sig.Number - 200);
                        function = 3;
                    }
                    else if (args.Sig.Number <= 400)//mode changed to off
                    {
                        //updateMode((ushort)args.Sig.Number);
                        zoneNumber = (ushort)(args.Sig.Number - 300);
                        function = 4;
                    }
                    foreach (var room in manager.RoomZ)
                    {
                        if (room.Value.ClimateID == zoneNumber)
                        {
                            roomNumber = room.Value.Number;
                            switch (function)
                            {
                                case (1):
                                    {
                                        room.Value.ClimateMode = "Auto";
                                        break;
                                    }
                                case (2):
                                    {
                                        room.Value.ClimateMode = "Heat";
                                        break;
                                    }
                                case (3):
                                    {
                                        room.Value.ClimateMode = "Cool";
                                        break;
                                    }
                                case (4):
                                    {
                                        room.Value.ClimateMode = "Off";
                                        break;
                                    }
                                default: break;
                            }
                            if (manager.RoomZ[roomNumber].CurrentTemperature > 0)
                            {
                                CrestronConsole.PrintLine("room hvac {0}", roomNumber);
                                UpdateRoomHVACText(roomNumber);
                            }
                        }
                    }

                }
            }
            
        }

        public void StartupPanels(ushort TPNumber)
        {

            ushort floorScenarioNum = manager.touchpanelZ[TPNumber].FloorScenario;
            ushort currentRoomNumber = manager.touchpanelZ[TPNumber].CurrentRoomNum;
            
            if (manager.touchpanelZ[TPNumber].DontInheritSubsystemScenario == false)
            {
                manager.touchpanelZ[TPNumber].SubSystemScenario = manager.RoomZ[currentRoomNumber].SubSystemScenario;
            }
            //initialize the current floor for the panel since we can't do it when the panel is instantiated as it comes before the floor scenarios.
            manager.touchpanelZ[TPNumber].CurrentFloorNum = manager.FloorScenarioZ[floorScenarioNum].IncludedFloors[0];
            //Update the number of floors, current room number, room name
            roomSelectEISC.UShortInput[(ushort)(TPNumber)].UShortValue = (ushort)manager.FloorScenarioZ[floorScenarioNum].IncludedFloors.Count;
            subsystemEISC.UShortInput[(ushort)((TPNumber - 1) * 10 + 301)].UShortValue = manager.RoomZ[currentRoomNumber].AudioID;
            UpdateSubsystems(TPNumber);
            UpdateTPVideoMenu(TPNumber);
            ushort asrcScenarioNum = manager.RoomZ[currentRoomNumber].AudioSrcScenario;
            ushort numASrcs = (ushort)manager.AudioSrcScenarioZ[asrcScenarioNum].IncludedSources.Count;
            //update music sources to select from
            if (asrcScenarioNum > 0)
            {
                musicEISC1.UShortInput[(ushort)(TPNumber)].UShortValue = numASrcs;// Number of sources to show
                for (ushort i = 0; i < numASrcs; i++)
                {
                    ushort srcNum = manager.AudioSrcScenarioZ[asrcScenarioNum].IncludedSources[i];
                    musicEISC1.StringInput[(ushort)((TPNumber - 1) * 20 + i + 1)].StringValue = manager.MusicSourceZ[srcNum].Name;
                    if (manager.touchpanelZ[TPNumber].HTML_UI) { musicEISC1.StringInput[(ushort)((TPNumber - 1) * 20 + i + 2001)].StringValue = manager.MusicSourceZ[srcNum].IconHTML; }
                    else { musicEISC1.StringInput[(ushort)((TPNumber - 1) * 20 + i + 2001)].StringValue = manager.MusicSourceZ[srcNum].IconSerial; }
                    musicEISC1.UShortInput[(ushort)((TPNumber - 1) * 20 + i + 1001)].UShortValue = manager.MusicSourceZ[srcNum].AnalogModeNumber;
                }
            }

            else { musicEISC1.UShortInput[(ushort)(TPNumber + 1)].UShortValue = 0; } // no sources to display

            UpdateTPMusicMenu(TPNumber);
            if (manager.FloorScenarioZ[floorScenarioNum].IncludedFloors.Count > 1)
            {
                //send the floor names
                for (ushort i = 0; i < manager.FloorScenarioZ[floorScenarioNum].IncludedFloors.Count; i++)
                {
                    //calculate EISC string input numbers
                    ushort stringInputNum = (ushort)((TPNumber - 1) * 10 + i + 1);

                    roomSelectEISC.StringInput[stringInputNum].StringValue = manager.Floorz[manager.FloorScenarioZ[floorScenarioNum].IncludedFloors[i]].Name;
                }
            }
            else
            {

                ushort currentNumberOfZones = (ushort)manager.Floorz[manager.FloorScenarioZ[floorScenarioNum].IncludedFloors[0]].IncludedRooms.Count;
                roomSelectEISC.UShortInput[(ushort)(TPNumber + 100)].UShortValue = currentNumberOfZones;
                for (ushort i = 0; i < currentNumberOfZones; i++)
                {
                    ushort stringInputNum = (ushort)((TPNumber - 1) * 50 + i + 1001); //current zone names start at string 1000
                    ushort zoneTemp = manager.Floorz[manager.FloorScenarioZ[floorScenarioNum].IncludedFloors[0]].IncludedRooms[i];
                    roomSelectEISC.StringInput[stringInputNum].StringValue = manager.RoomZ[zoneTemp].Name;
                }
            }
            UpdateRoomOptions(TPNumber);
            UpdateEquipIDsForSubsystems(TPNumber, currentRoomNumber);
            CrestronConsole.PrintLine("TP-{0} complete!!", (TPNumber));
        }
        public void SelectFloor(ushort TPNumber, ushort floorButtonNumber)
        {
            ushort floorScenarioNum = manager.touchpanelZ[TPNumber].FloorScenario;//GET the floor scenario assigned to this panel

            //FIRST get the current floor
            ushort currentFloor = 1;
            if (floorButtonNumber > 0)
            {
                currentFloor = this.manager.FloorScenarioZ[floorScenarioNum].IncludedFloors[floorButtonNumber - 1];
            }
            else if (this.manager.touchpanelZ[TPNumber].CurrentFloorNum > 0)
            {
                currentFloor = this.manager.touchpanelZ[TPNumber].CurrentFloorNum;
            }

            if (manager.FloorScenarioZ[floorScenarioNum].IncludedFloors.Count > 1)
            {
                this.manager.touchpanelZ[TPNumber].CurrentFloorNum = currentFloor; //SET the current floor for this panel
            }
            ushort currentNumberOfZones = (ushort)this.manager.Floorz[currentFloor].IncludedRooms.Count();
            roomSelectEISC.UShortInput[(ushort)(TPNumber + 100)].UShortValue = currentNumberOfZones;
            UpdateRoomsPageStatusText(TPNumber);
        }
        public void UpdateRoomsPageStatusText(ushort TPNumber) {
            //update all of the room names and status for the rooms page
            ushort currentNumberOfZones = (ushort)this.manager.Floorz[manager.touchpanelZ[TPNumber].CurrentFloorNum].IncludedRooms.Count();
            for (ushort i = 0; i < currentNumberOfZones; i++) //send the zone names for current floor out to the xsig
            {
                ushort stringInputNum = (ushort)((TPNumber - 1) * 50 + i + 1001); //current zone names start at string 1000
                ushort zoneTemp = this.manager.Floorz[manager.touchpanelZ[TPNumber].CurrentFloorNum].IncludedRooms[i];
                roomSelectEISC.StringInput[stringInputNum].StringValue = this.manager.RoomZ[zoneTemp].Name;
                //update hvac status
                ushort eiscPosition = (ushort)(601 + (30 * (TPNumber - 1)) + i);
                string hvacStatusText = GetHVACStatusText(zoneTemp);
                musicEISC3.StringInput[eiscPosition].StringValue = hvacStatusText;
                //update room status text
                eiscPosition = (ushort)(201 + (30 * (TPNumber - 1)) + i);
                string statusText = manager.RoomZ[zoneTemp].LightStatusText + manager.RoomZ[zoneTemp].VideoStatusText + manager.RoomZ[zoneTemp].MusicStatusText;
                videoEISC2.StringInput[eiscPosition].StringValue = statusText;
                string imagePath = string.Format("http://{0}/{1}", IPaddress, manager.RoomZ[zoneTemp].ImageURL);
                imageEISC.StringInput[(ushort)(30 * (TPNumber - 1) + i + 101)].StringValue = imagePath;
            }
        }

        public void UpdateEquipIDsForSubsystems(ushort TPNumber, ushort currentRoomNumber) {
            //Update eisc with current equipIDs for the subsystems of the currently selected room
            subsystemEISC.UShortInput[(ushort)((TPNumber - 1) * 10 + 301)].UShortValue = manager.RoomZ[currentRoomNumber].AudioID;//audioID is also switcher output number
            subsystemEISC.UShortInput[(ushort)((TPNumber - 1) * 10 + 302)].UShortValue = manager.RoomZ[currentRoomNumber].VideoOutputNum;//in the simpl program this is the room number
            subsystemEISC.UShortInput[(ushort)((TPNumber - 1) * 10 + 303)].UShortValue = manager.RoomZ[currentRoomNumber].LightsID;
            subsystemEISC.UShortInput[(ushort)((TPNumber - 1) * 10 + 304)].UShortValue = manager.RoomZ[currentRoomNumber].ShadesID;
            subsystemEISC.UShortInput[(ushort)((TPNumber - 1) * 10 + 305)].UShortValue = manager.RoomZ[currentRoomNumber].ClimateID;
            subsystemEISC.UShortInput[(ushort)((TPNumber - 1) * 10 + 306)].UShortValue = manager.RoomZ[currentRoomNumber].MiscID;
        }
        public void UpdatePanelToMusicZoneOff(ushort TPNumber) {
            musicEISC2.StringInput[TPNumber].StringValue = "Off";//current source to TP
            musicEISC1.UShortInput[(ushort)(TPNumber + 100)].UShortValue = 0;//current asrc number to panel media server and sharing objects
            musicEISC1.UShortInput[(ushort)(TPNumber + 200)].UShortValue = 0;//current asrc page number to panel
            musicEISC1.UShortInput[(ushort)(TPNumber + 400)].UShortValue = 0;//clear the button feedback
        }
        public void SelectZone(ushort TPNumber, ushort zoneListButtonNumber)
        {
            ushort currentRoomNumber;
            if (zoneListButtonNumber > 0)
            {
                currentRoomNumber = manager.Floorz[manager.touchpanelZ[TPNumber].CurrentFloorNum].IncludedRooms[zoneListButtonNumber - 1];
            }
            else {
                currentRoomNumber = manager.touchpanelZ[TPNumber].CurrentRoomNum;
            }
            manager.touchpanelZ[TPNumber].CurrentRoomNum = currentRoomNumber;
            //Update current subsystem scenario number to the panel
            if (!manager.touchpanelZ[TPNumber].DontInheritSubsystemScenario)
            {
                manager.touchpanelZ[TPNumber].SubSystemScenario = manager.RoomZ[currentRoomNumber].SubSystemScenario;
            }

            //Update eisc with subsystem names and icons for current panel
            UpdateSubsystems(TPNumber);
            UpdateEquipIDsForSubsystems(TPNumber, currentRoomNumber);

            //Update current room image
            string imagePath = string.Format("http://{0}/{1}", IPaddress, manager.RoomZ[currentRoomNumber].ImageURL);
            imageEISC.StringInput[(ushort)(TPNumber)].StringValue = imagePath;
            //Update A/V Sources available for this room
            ushort asrcScenarioNum = manager.RoomZ[currentRoomNumber].AudioSrcScenario;
            ushort numASrcs = (ushort)manager.AudioSrcScenarioZ[asrcScenarioNum].IncludedSources.Count;

            ushort currentASRC = manager.RoomZ[currentRoomNumber].CurrentMusicSrc;
            //ushort currentVSRC = manager.RoomZ[currentRoomNumber].CurrentVideoSrc;
            if (currentASRC == 0)
            {
                UpdatePanelToMusicZoneOff(TPNumber);
            }
            //update the MUSIC sources to display for this room
            if (asrcScenarioNum > 0)
            {
                musicEISC1.UShortInput[(ushort)(TPNumber)].UShortValue = numASrcs;// Number of sources to show
                bool useAnalogModes = manager.touchpanelZ[TPNumber].UseAnalogModes;
                if (useAnalogModes && numASrcs > 6) { musicEISC1.UShortInput[(ushort)(TPNumber)].UShortValue = 6; }
                for (ushort i = 0; i < numASrcs; i++)
                {
                    ushort srcNum = manager.AudioSrcScenarioZ[asrcScenarioNum].IncludedSources[i];
                    musicEISC1.StringInput[(ushort)((TPNumber - 1) * 20 + i + 1)].StringValue = manager.MusicSourceZ[srcNum].Name;
                    if (manager.touchpanelZ[TPNumber].HTML_UI) { musicEISC1.StringInput[(ushort)((TPNumber - 1) * 20 + i + 2001)].StringValue = manager.MusicSourceZ[srcNum].IconHTML; }
                    else { musicEISC1.StringInput[(ushort)((TPNumber - 1) * 20 + i + 2001)].StringValue = manager.MusicSourceZ[srcNum].IconSerial; }
                    musicEISC1.UShortInput[(ushort)((TPNumber - 1) * 20 + i + 1001)].UShortValue = manager.MusicSourceZ[srcNum].AnalogModeNumber;
                    //Update the current audio source of this room to the panel and highlight the appropriate button
                    if (srcNum == currentASRC)
                    {
                        musicEISC1.UShortInput[(ushort)(TPNumber + 400)].UShortValue = (ushort)(i + 1);//i+1 = button number to highlight
                        musicEISC1.UShortInput[(ushort)(TPNumber + 200)].UShortValue = manager.MusicSourceZ[srcNum].FlipsToPageNumber;//page to show
                    }
                }
            }
            else { musicEISC1.UShortInput[(ushort)(TPNumber)].UShortValue = 0; } // no sources to display

            //do the same for the video sources
            if (manager.RoomZ[currentRoomNumber].VideoSrcScenario > 0) { 
                UpdateTPVideoMenu(TPNumber);
            }
            UpdateRoomOptions(TPNumber);

            //Update rooms available to share music sources to
            if (manager.RoomZ[currentRoomNumber].AudioSrcSharingScenario > 0)
            {
                ushort numRooms = (ushort)manager.AudioSrcSharingScenarioZ[manager.RoomZ[currentRoomNumber].AudioSrcSharingScenario].IncludedZones.Count;
                ushort flag = 0;
                for (ushort i = 0; i < numRooms; i++)
                {
                    ushort room = manager.AudioSrcSharingScenarioZ[manager.RoomZ[currentRoomNumber].AudioSrcSharingScenario].IncludedZones[i];
                    if (room == currentRoomNumber) { flag = 1; }
                    else
                    {
                        musicEISC2.StringInput[(ushort)((TPNumber - 1) * 25 + i - flag + 101)].StringValue = manager.RoomZ[room].Name; //room names
                        //current source assigned to this room
                        musicEISC2.UShortInput[(ushort)((TPNumber - 1) * 25 + i - flag + 101)].UShortValue = manager.RoomZ[room].AudioID;//room switcher output number
                    }
                }
                musicEISC2.UShortInput[(ushort)(TPNumber)].UShortValue = (ushort)(numRooms - flag);//number of rooms available to share to
                musicEISC2.BooleanInput[(ushort)(TPNumber)].BoolValue = true;//enable the source sharing button
            }
            else
            {
                musicEISC2.BooleanInput[(ushort)(TPNumber)].BoolValue = false;//clear the source sharing button
            }
            //enable or disable the vol feedback for dist audio
            ushort configScenario = manager.RoomZ[currentRoomNumber].ConfigurationScenario;
            //if has receiver AND music through receiver AND receiver has NO vol feedback
            if (manager.VideoConfigScenarioZ[configScenario].HasReceiver && manager.VideoConfigScenarioZ[configScenario].MusicThroughReceiver > 0 && !manager.VideoConfigScenarioZ[configScenario].ReceiverHasVolFB)
            {
                musicEISC3.BooleanInput[TPNumber].BoolValue = false;
            }
            else 
            {
                musicEISC3.BooleanInput[TPNumber].BoolValue = true;//enable the vol feedback
            }
            UpdateTPMusicMenu((ushort)(TPNumber));
            UpdatePanelHVACTextInSubsystemList(TPNumber);
            UpdatePanelSubsystemText(TPNumber);
        }

        public void WholeHouseUpdateZoneList(ushort TPNumber) {
            ushort subsystemNumber = manager.touchpanelZ[TPNumber].CurrentSubsystemNumber;
            //update the zone list and status for the subsystem
            //figure out which subsystem
            if (manager.SubsystemZ[subsystemNumber].DisplayName.ToUpper() == "LIGHTS" || manager.SubsystemZ[subsystemNumber].DisplayName.ToUpper() == "LIGHTING")
            {
                roomList.Clear();
                //get all rooms that have lights
                ushort i = 0;
                foreach (var room in manager.RoomZ)
                {
                    if (room.Value.LightsID > 0)
                    {
                        
                        roomList.Add(room.Value.Number);
                        ushort stringInputNum = (ushort)((TPNumber - 1) * 50 + i + 1001); //current zone names start at string 1000
                        roomSelectEISC.StringInput[stringInputNum].StringValue = manager.RoomZ[room.Value.Number].Name;
                        ushort eiscPosition = (ushort)(601 + (30 * (TPNumber - 1)) + i);
                        string statusText = "";
                        if (room.Value.Name.ToUpper() == "GLOBAL") 
                        {
                            statusText = "";
                        }
                        else if (room.Value.LightsAreOff)
                        {
                            statusText = "Lights are off. ";
                        }
                        else
                        {
                            statusText = "Lights are on. ";
                        }
                        musicEISC3.StringInput[eiscPosition].StringValue = statusText;
                        //send to the zonestatus line2 serial
                        videoEISC2.StringInput[(ushort)(eiscPosition-400)].StringValue = manager.SubsystemZ[subsystemNumber].IconSerial;
                        i++;
                    }
                }
                imageEISC.UShortInput[TPNumber].UShortValue = i;
            }
            else if (manager.SubsystemZ[subsystemNumber].DisplayName.ToUpper() == "CLIMATE" || manager.SubsystemZ[subsystemNumber].DisplayName.ToUpper() == "HVAC")
            {
                //get all rooms that have hvac
                CrestronConsole.PrintLine("HVAC");
                roomList.Clear();
                ushort i = 0;
                foreach (var room in manager.RoomZ)
                {
                    if (room.Value.ClimateID > 0)
                    {
                        
                        roomList.Add(room.Value.Number);
                        ushort stringInputNum = (ushort)((TPNumber - 1) * 50 + i + 1001); //current zone names start at string 1000
                        roomSelectEISC.StringInput[stringInputNum].StringValue = manager.RoomZ[room.Value.Number].Name;
                        ushort eiscPosition = (ushort)(601 + (30 * (TPNumber - 1)) + i);
                        string statusText = GetHVACStatusText(room.Value.Number);

                        musicEISC3.StringInput[eiscPosition].StringValue = statusText;
                        videoEISC2.StringInput[(ushort)(eiscPosition - 400)].StringValue = manager.SubsystemZ[subsystemNumber].IconSerial;
                        i++;
                    }
                }
                imageEISC.UShortInput[TPNumber].UShortValue = i;
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
                subsystemButtonNumber--;//change to 0 based index
                if (manager.touchpanelZ[TPNumber].CurrentPageNumber == 0)//if we are on the home page and selected a subystem, we want to show the zone list first
                {
                    ushort homePageScenario = manager.touchpanelZ[TPNumber].HomePageScenario;
                    subsystemNumber = manager.WholeHouseSubsystemScenarioZ[homePageScenario].IncludedSubsystems[subsystemButtonNumber];
                    manager.touchpanelZ[TPNumber].CurrentSubsystemNumber = subsystemNumber; //store this in the panel. 
                    WholeHouseUpdateZoneList(TPNumber);
                    SendSubsystemZonesPageNumber(TPNumber, false);
                }
                else//if we are on the room page we want to show the control sub
                {
                    for (ushort i = 1; i <= manager.SubsystemZ.Count; i++)
                    {
                        if (manager.SubsystemZ[i].Name.ToUpper() == "VIDEO")
                        {
                            videoIsSystemNumber = i;
                        }
                        else if (manager.SubsystemZ[i].Name.ToUpper() == "AUDIO" || manager.SubsystemZ[i].Name.ToUpper() == "MUSIC")
                        {
                            audioIsSystemNumber = i;
                        }
                    }
                    subsystemNumber = manager.SubsystemScenarioZ[currentSubsystemScenario].IncludedSubsystems[subsystemButtonNumber];//get the CURRENT subsystem number for this panel                                                                                                                      //update the panel and room current subsystem
                    manager.RoomZ[currentRoomNum].CurrentSubsystem = subsystemNumber;//update the room with the current subsystem number
                    manager.touchpanelZ[TPNumber].CurrentSubsystemNumber = subsystemNumber; //store this in the panel. 
                    if (subsystemNumber == videoIsSystemNumber)
                    {
                        manager.RoomZ[currentRoomNum].LastSystemVid = true;
                        imageEISC.BooleanInput[TPNumber].BoolValue = true;//current subsystem is video
                        imageEISC.BooleanInput[(ushort)(TPNumber + 100)].BoolValue = false;//current subsystem is NOT audio
                    }
                    else if (subsystemNumber == audioIsSystemNumber)
                    {
                        manager.RoomZ[currentRoomNum].LastSystemVid = false;
                        imageEISC.BooleanInput[(ushort)(TPNumber + 100)].BoolValue = true;//current subsystem is audio
                        imageEISC.BooleanInput[TPNumber].BoolValue = false;//current subsystem is NOT video
                    }
                    else
                    {
                        imageEISC.BooleanInput[(ushort)(TPNumber + 100)].BoolValue = false;//current subsystem is NOT audio
                        imageEISC.BooleanInput[TPNumber].BoolValue = false;//current subsystem is NOT video
                    }
                    subsystemEISC.UShortInput[(ushort)(TPNumber + 100)].UShortValue = manager.SubsystemZ[subsystemNumber].FlipsToPageNumber;
                }
                
                musicEISC3.StringInput[(ushort)(TPNumber + 200)].StringValue = manager.SubsystemZ[subsystemNumber].DisplayName;
                subsystemEISC.UShortInput[(ushort)(TPNumber + 200)].UShortValue = (ushort)(manager.SubsystemZ[subsystemNumber].EquipID + TPNumber);
            }
            else { manager.RoomZ[currentRoomNum].CurrentSubsystem = 0; }//this would be when the home button is pushed
        }

        public void SendSubsystemZonesPageNumber(ushort TPNumber, bool close) {
            
            //this is for when on the home menu we want to display the list of zones
            ushort currentSub = manager.touchpanelZ[TPNumber].CurrentSubsystemNumber;
            if (manager.SubsystemZ[currentSub].DisplayName.ToUpper() == "LIGHTS" || manager.SubsystemZ[currentSub].DisplayName.ToUpper() == "LIGHTING")
            {
                subsystemEISC.UShortInput[(ushort)(TPNumber + 100)].UShortValue = 91;
            }
            else if (manager.SubsystemZ[currentSub].DisplayName.ToUpper() == "CLIMATE" || manager.SubsystemZ[currentSub].DisplayName.ToUpper() == "HVAC")
            {
                subsystemEISC.UShortInput[(ushort)(TPNumber + 100)].UShortValue = 92;
            }
            else if (manager.SubsystemZ[currentSub].DisplayName.ToUpper() == "SHADES" || manager.SubsystemZ[currentSub].DisplayName.ToUpper() == "DRAPES")
            {
                subsystemEISC.UShortInput[(ushort)(TPNumber + 100)].UShortValue = 93;
            }
            else if (close)
            {
                subsystemEISC.UShortInput[(ushort)(TPNumber + 100)].UShortValue = 0;
            }
            else {
                subsystemEISC.UShortInput[(ushort)(TPNumber + 100)].UShortValue = manager.SubsystemZ[currentSub].FlipsToPageNumber;
            }

        }
        public void SelectMusicSource(ushort TPNumber, ushort sourceButtonNumber)
        {
            //calculate the source # because source button # isn't the source #
            ushort switcherOutputNum = manager.RoomZ[manager.touchpanelZ[TPNumber].CurrentRoomNum].AudioID;
            ushort currentRoomNum = manager.touchpanelZ[TPNumber].CurrentRoomNum;
            ushort currentASRC = 0;
            ushort currentASRCscenario = manager.RoomZ[manager.touchpanelZ[TPNumber].CurrentRoomNum].AudioSrcScenario;
            ushort srcGroup = manager.touchpanelZ[TPNumber].CurrentASrcGroupNum;
            ushort adjustedButtonNum = sourceButtonNumber;
            if (srcGroup > 0)
            {
                adjustedButtonNum = (ushort)(sourceButtonNumber + (srcGroup - 1) * 6 - 1);//this is for TSR-310's or panels with groups of 6 sources
            }

            if (sourceButtonNumber > 0)
            {
                //this is the source "number" property from the json file
                currentASRC = manager.AudioSrcScenarioZ[currentASRCscenario].IncludedSources[(ushort)(adjustedButtonNum)];
            }
            //set the current music source for the room
            manager.RoomZ[manager.touchpanelZ[TPNumber].CurrentRoomNum].CurrentMusicSrc = currentASRC;
            if (currentASRC > 0)
            {
                //calculate whether to select AES67 Stream input 17
                //first get the NAXBoxNumber this source is connected to
                ushort sourceBoxNumber = manager.MusicSourceZ[currentASRC].NaxBoxNumber;
                //then get the current zones box number
                int zoneBoxNumber = ((switcherOutputNum-1) / 8) + 1;
                //if the source is on a different box than the zone, use the stream
                CrestronConsole.PrintLine("sourceBoxNumber{0} zoneBoxNumber{1}", sourceBoxNumber, zoneBoxNumber);
                if (sourceBoxNumber > 0 && sourceBoxNumber != zoneBoxNumber)//then this is a streaming source
                {
                    musicEISC1.UShortInput[(ushort)(switcherOutputNum + 500)].UShortValue = 17;
                    musicEISC3.StringInput[(ushort)(switcherOutputNum + 300)].StringValue = manager.MusicSourceZ[currentASRC].MultiCastAddress;
                    multis[switcherOutputNum] = manager.MusicSourceZ[currentASRC].MultiCastAddress;
                    CrestronConsole.PrintLine("audio in 17 to out {0} srcNum {1} MULTI {2}", switcherOutputNum, currentASRC, manager.MusicSourceZ[currentASRC].MultiCastAddress);
                }
                //otherwise its on the same box so just use the switcher input number
                else 
                {
                    musicEISC1.UShortInput[(ushort)(switcherOutputNum + 500)].UShortValue = manager.MusicSourceZ[currentASRC].SwitcherInputNumber;//switcher input # to output
                    musicEISC3.StringInput[(ushort)(switcherOutputNum + 300)].StringValue = "0.0.0.0";//clear the multicast address, we're not using streaming
                    CrestronConsole.PrintLine("audio in {1} to out {0} srcNum {2}", switcherOutputNum, manager.MusicSourceZ[currentASRC].SwitcherInputNumber, currentASRC);
                }
                musicEISC3.StringInput[(ushort)(switcherOutputNum + 500)].StringValue = manager.MusicSourceZ[currentASRC].Name;//update the current source to the zone module which also updates the sharing page
                if (manager.MusicSourceZ[currentASRC].StreamingProviderNumber > 0 && manager.MusicSourceZ[currentASRC].SwitcherInputNumber > 8)//this is a streaming source
                {
                    musicEISC1.UShortInput[(ushort)(600 + manager.MusicSourceZ[currentASRC].SwitcherInputNumber - 8)].UShortValue = manager.MusicSourceZ[currentASRC].StreamingProviderNumber;
                }
                
                musicEISC1.UShortInput[(ushort)(TPNumber + 100)].UShortValue = manager.MusicSourceZ[currentASRC].Number; //send source number for media server object router
                musicEISC1.UShortInput[(ushort)(TPNumber + 200)].UShortValue = manager.MusicSourceZ[currentASRC].FlipsToPageNumber;
                musicEISC1.UShortInput[(ushort)(TPNumber + 300)].UShortValue = manager.MusicSourceZ[currentASRC].EquipID;
                musicEISC2.StringInput[(TPNumber)].StringValue = manager.MusicSourceZ[currentASRC].Name;
                //currentMusicSource is the 
                updateMusicSourceInUse(currentASRC, manager.MusicSourceZ[currentASRC].SwitcherInputNumber, switcherOutputNum);
            }
            else {
                CrestronConsole.PrintLine(" output {0} off", switcherOutputNum);
                if (manager.VideoConfigScenarioZ[manager.RoomZ[currentRoomNum].ConfigurationScenario].HasReceiver)
                {
                    //TODO test for current receiver input so you can turn it off only if its listening to music
                    videoEISC1.UShortInput[(ushort)(manager.RoomZ[currentRoomNum].VideoOutputNum + 700)].UShortValue = 0;//receiver input
                }
                audioZoneOff(TPNumber, switcherOutputNum);
                updateMusicSourceInUse(currentASRC, 0, switcherOutputNum);
            }
        }
        public void audioZoneOff(ushort TPNumber, ushort switcherOutputNum) 
        {
            musicEISC1.UShortInput[(ushort)(switcherOutputNum + 500)].UShortValue = 0;//to switcher
            musicEISC1.UShortInput[(ushort)(TPNumber + 100)].UShortValue = 0;//current asrc number
            musicEISC1.UShortInput[(ushort)(TPNumber + 200)].UShortValue = 0;//current asrc page number
            musicEISC1.UShortInput[(ushort)(TPNumber + 300)].UShortValue = 0;//equip ID
            musicEISC2.StringInput[(TPNumber)].StringValue = "Off";//current asrc
            musicEISC3.StringInput[(ushort)(switcherOutputNum + 300)].StringValue = "0.0.0.0"; //multicast off
        }
        public void AudioFloorOff(ushort actionNumber) {
            //todo - add timer to block feedback from NAX
            // update panel status text.
            //ha all off
            if (actionNumber == 1)
            {
                CrestronConsole.PrintLine("HA ALL Off");
                foreach (var room in manager.RoomZ)
                {
                    room.Value.CurrentMusicSrc = 0;
                    room.Value.MusicStatusText = "";
                }
            }
            else //floor off
            {
                foreach (ushort rmNum in manager.Floorz[(ushort)(actionNumber-1)].IncludedRooms)
                {
                    CrestronConsole.PrintLine("AudioFloorOff {0} {1}", rmNum, manager.RoomZ[rmNum].Name);
                    manager.RoomZ[rmNum].CurrentMusicSrc = 0;
                    manager.RoomZ[rmNum].MusicStatusText = "";
                }
            }
        }
        public void SelectVideoSource(ushort TPNumber, ushort sourceButtonNumber)
        {
            CrestronConsole.PrintLine("TPNumber{0} sourceButtonNumber{1}", TPNumber, sourceButtonNumber);
            //calculate the source # because source button # isn't the source #
            ushort currentRoomNum = manager.touchpanelZ[TPNumber].CurrentRoomNum;
            CrestronConsole.PrintLine("currentRoomNum{0}", currentRoomNum);
            ushort switcherOutputNum = manager.RoomZ[manager.touchpanelZ[TPNumber].CurrentRoomNum].VideoOutputNum;
            CrestronConsole.PrintLine("switcherOutputNum{0}", switcherOutputNum);
            ushort currentVSRC = 0;
            ushort srcGroup = manager.touchpanelZ[TPNumber].CurrentVSrcGroupNum;
            imageEISC.BooleanInput[TPNumber].BoolValue = true;//this tells the program that the current subsystem is video for this panel
            CrestronConsole.PrintLine("TP-{0} room{1} switcherout{2} srcGroup", TPNumber, currentRoomNum, switcherOutputNum, srcGroup);
            if (sourceButtonNumber == 0)
            {
                videoEISC2.StringInput[TPNumber].StringValue = "Off";
                videoEISC1.UShortInput[(ushort)(switcherOutputNum + 600)].UShortValue = 0;//display input
                videoEISC1.UShortInput[(ushort)(switcherOutputNum + 700)].UShortValue = 0;//receiver input
                videoEISC1.UShortInput[(ushort)(switcherOutputNum + 800)].UShortValue = 0;//alt switcher input
            }
            else
            {
                //if this room has a receiver and the music is through the receiver then turn the music off
                ushort vidConfigScenario = manager.RoomZ[currentRoomNum].ConfigurationScenario;
                if (manager.VideoConfigScenarioZ[vidConfigScenario].HasReceiver && manager.VideoConfigScenarioZ[vidConfigScenario].MusicThroughReceiver > 0)
                {
                    audioZoneOff(TPNumber, switcherOutputNum);
                }
                //this will work for panels that don't use the 6 per page analog modes because srcGroup will always be 1
                ushort adjustedButtonNum = (ushort)(sourceButtonNumber + (srcGroup - 1) * 6 - 1);//this is for a handheld using analog mode buttons 6 per page
                ushort vsrcScenario = manager.RoomZ[currentRoomNum].VideoSrcScenario;
                currentVSRC = manager.VideoSrcScenarioZ[vsrcScenario].IncludedSources[adjustedButtonNum];
                CrestronConsole.PrintLine("vidout{0} to in{1}", switcherOutputNum, manager.VideoSourceZ[currentVSRC].VidSwitcherInputNumber);
                //SEND THE SWITCHING COMMANDS
                videoEISC1.UShortInput[(ushort)(switcherOutputNum + 600)].UShortValue = manager.VideoSrcScenarioZ[vsrcScenario].DisplayInputs[adjustedButtonNum];
                videoEISC1.UShortInput[(ushort)(switcherOutputNum + 700)].UShortValue = manager.VideoSrcScenarioZ[vsrcScenario].ReceiverInputs[adjustedButtonNum];
                videoEISC1.UShortInput[(ushort)(switcherOutputNum + 800)].UShortValue = manager.VideoSrcScenarioZ[vsrcScenario].AltSwitcherInputs[adjustedButtonNum];

            }
            //set the current video source for the room
            manager.RoomZ[manager.touchpanelZ[TPNumber].CurrentRoomNum].CurrentVideoSrc = currentVSRC;
            if (currentVSRC > 0)
            {
                videoEISC1.UShortInput[(ushort)(switcherOutputNum + 500)].UShortValue = manager.VideoSourceZ[currentVSRC].VidSwitcherInputNumber;//this is for the DM. switcher input # to output
                videoEISC1.UShortInput[(ushort)(switcherOutputNum + 900)].UShortValue = manager.VideoSourceZ[currentVSRC].VidSwitcherInputNumber;//this is for the room module - this may be redundant 
            }
            else
            {
                videoEISC1.UShortInput[(ushort)(switcherOutputNum + 500)].UShortValue = 0;//this is for the DM. switcher input # to output
                videoEISC1.UShortInput[(ushort)(switcherOutputNum + 900)].UShortValue = 0;//this is for the room module - this may be redundant
            }


            UpdateTPVideoMenu(TPNumber);
        }
        
        //This function determines which switcher output number to route the current source to. It does not manage turning the zone off
        public void SelectShareSource(ushort TPNumber, ushort zoneButtonNumber)
        {
            try
            {
                //get current room number and current source
                ushort currentRoom = manager.touchpanelZ[TPNumber].CurrentRoomNum;
                if (currentRoom > 0)
                {
                    ushort currentASRC = manager.RoomZ[currentRoom].CurrentMusicSrc;//this is the number in the list of music sources
                    ushort inputNum = 0;
                    string multicastAddress = "0.0.0.0";
                    CrestronConsole.PrintLine("sharing tpnum{0} zonebuttnnum {1} currentasrce {2}", TPNumber, zoneButtonNumber, currentASRC);
                    if (currentASRC > 0)
                    {
                        inputNum = manager.MusicSourceZ[currentASRC].SwitcherInputNumber;
                    }
                    ushort index = 0;
                    ushort flag = 0;
                    ushort sharingRoomNumber = 0;

                    ushort sharingScenario = manager.RoomZ[currentRoom].AudioSrcSharingScenario;
                    ushort numRooms = (ushort)manager.AudioSrcSharingScenarioZ[sharingScenario].IncludedZones.Count;
                    //if the current room is in the sharing list skip over it
                    for (ushort i = 0; i < numRooms; i++)
                    {
                        if (manager.AudioSrcSharingScenarioZ[sharingScenario].IncludedZones[i] == currentRoom)
                        {
                            index = i;//get index of room to skip
                            flag = 1;
                        }
                    }
                    //translate zoneButtonNumber to room number
                    if (flag > 0 && zoneButtonNumber >= index )//if the current room is in the sharing scenario AND the selected zone number is beyond the current room
                    {
                        sharingRoomNumber = manager.AudioSrcSharingScenarioZ[sharingScenario].IncludedZones[(ushort)(zoneButtonNumber + 1)];
                    }
                    else
                    {
                        sharingRoomNumber = manager.AudioSrcSharingScenarioZ[sharingScenario].IncludedZones[(ushort)(zoneButtonNumber)];
                    }
                    //CrestronConsole.PrintLine("sharingRoomNumber {0} count {1} buttnum {2}", sharingRoomNumber, manager.AudioSrcSharingScenarioZ[sharingScenario].IncludedZones.Count, zoneButtonNumber);

                    ushort switcherOutputNum = manager.RoomZ[sharingRoomNumber].AudioID;
                    if (NAXsystem && currentASRC > 0)
                    {
                        int zoneBoxNumber = ((switcherOutputNum - 1) / 8) + 1;
                        int srcBoxNumber = manager.MusicSourceZ[currentASRC].NaxBoxNumber;
                        if (srcBoxNumber != zoneBoxNumber) //this source will be streamed via multicast 
                        {
                            inputNum = 17;
                            multicastAddress = manager.MusicSourceZ[currentASRC].MultiCastAddress;
                        }
                    }
                    musicEISC1.UShortInput[(ushort)(switcherOutputNum + 500)].UShortValue = inputNum;//send the source to switcher
                    if (currentASRC > 0)
                    {
                        //send the multicast address
                        musicEISC3.StringInput[(ushort)(switcherOutputNum + 300)].StringValue = multicastAddress;
                        //send the name of the source
                        musicEISC3.StringInput[(ushort)(switcherOutputNum + 500)].StringValue = manager.MusicSourceZ[currentASRC].Name;
                        CrestronConsole.PrintLine("sharing switcherOutputNum{0} - {1}", switcherOutputNum, manager.MusicSourceZ[currentASRC].Name);
                    }
                }
            }
            catch (Exception e)
            {
                ErrorLog.Warn("select share source tpnumber {0} zonebuttonnumber {1} {2} ", TPNumber, zoneButtonNumber, e.Message);
            }
        }
        public void SelectOnlyFloor(ushort TPNumber)
        {
            ushort floorScenarioNum = manager.touchpanelZ[TPNumber].FloorScenario;
            if (manager.FloorScenarioZ[floorScenarioNum].IncludedFloors.Count == 1)
            {
                SelectFloor((ushort)(TPNumber), 1);// there's only 1 floor in this scenario so select it
            }
        }
        public void UpdateSubsystems(ushort TPNumber)
        {
            ushort currentRoomNumber = manager.touchpanelZ[TPNumber].CurrentRoomNum;
            ushort numberOfSubsystems = (ushort)manager.SubsystemScenarioZ[manager.touchpanelZ[TPNumber].SubSystemScenario].IncludedSubsystems.Count;
            ushort currentSubsystemScenario = manager.touchpanelZ[TPNumber].SubSystemScenario;
            if (currentSubsystemScenario == 0) { currentSubsystemScenario = manager.RoomZ[currentRoomNumber].SubSystemScenario; }//inherit from the room if not defined
            ushort homepageScenario = manager.touchpanelZ[TPNumber].HomePageScenario;
            //Update eisc with current room number / name / number of subsystems for current panel
            roomSelectEISC.UShortInput[(ushort)(TPNumber + 200)].UShortValue = currentRoomNumber;
            subsystemEISC.StringInput[(ushort)(TPNumber)].StringValue = manager.RoomZ[currentRoomNumber].Name;
            subsystemEISC.UShortInput[(ushort)(TPNumber)].UShortValue = (ushort)manager.SubsystemScenarioZ[currentSubsystemScenario].IncludedSubsystems.Count;

            //UPDATE THE PAGE TO DISPLAY
            //if panel has no home page OR only it has 1 subsystem THEN flip to first available subsystem
            if (homepageScenario == 0 || numberOfSubsystems == 1)
            {
                    
                    //flip to first available subsystem
                    ushort subsystemNum = manager.SubsystemScenarioZ[currentSubsystemScenario].IncludedSubsystems[0];
                    subsystemEISC.UShortInput[(ushort)(TPNumber + 100)].UShortValue = manager.SubsystemZ[subsystemNum].FlipsToPageNumber;
                    roomSelectEISC.UShortInput[(ushort)(TPNumber + 300)].UShortValue = 1; //highlight the first subsystem button
                    manager.RoomZ[currentRoomNumber].CurrentSubsystem = subsystemNum; //update the room to the current subsystem
                //CrestronConsole.PrintLine("TP-{0} flip to {1} page #{2}", TPNumber, manager.SubsystemZ[subsystemNum].Name, manager.SubsystemZ[subsystemNum].FlipsToPageNumber);
            }
            else //otherwise flip to the list of subsystems
            {
                    //CrestronConsole.PrintLine("flip to first home");
                    roomSelectEISC.UShortInput[(ushort)(TPNumber + 300)].UShortValue = 0;//clear the buttons
                    subsystemEISC.UShortInput[(ushort)(TPNumber + 100)].UShortValue = 0;//flip to home
            }
            //Update eisc with subsystem names and icons for current panel and highlight the appropriate button
            for (ushort i = 0; i < manager.SubsystemScenarioZ[currentSubsystemScenario].IncludedSubsystems.Count; i++)
            {
                //update the names
                ushort subsystemNum = manager.SubsystemScenarioZ[currentSubsystemScenario].IncludedSubsystems[i];
                subsystemEISC.StringInput[(ushort)((TPNumber - 1) * 20 + i + 101)].StringValue = manager.SubsystemZ[subsystemNum].Name;
                //show the subsystem icon - HTML or smart graphic
                if (manager.touchpanelZ[TPNumber].HTML_UI) { subsystemEISC.StringInput[(ushort)((TPNumber - 1) * 20 + i + 2001)].StringValue = manager.SubsystemZ[subsystemNum].IconHTML; }
                else { subsystemEISC.StringInput[(ushort)((TPNumber - 1) * 20 + i + 2001)].StringValue = manager.SubsystemZ[subsystemNum].IconSerial; }
                //update the button number to highlight
                if (manager.RoomZ[currentRoomNumber].CurrentSubsystem == subsystemNum)
                {
                    roomSelectEISC.UShortInput[(ushort)(TPNumber + 300)].UShortValue = (ushort)(i + 1);
                }
            }
        }

        public void HomeButtonPress(ushort TPNumber) {
            if (manager.touchpanelZ[TPNumber].Type != "Tsr310" && manager.touchpanelZ[TPNumber].Type != "HR310")
            {
                ushort homePageScenario = manager.touchpanelZ[TPNumber].HomePageScenario;
            
                string homeImagePath = string.Format("http://{0}/HOME.JPG", IPaddress);
                imageEISC.StringInput[TPNumber].StringValue = homeImagePath;

                imageEISC.BooleanInput[TPNumber].BoolValue = false;//clear "current subsystem is video"
                manager.touchpanelZ[TPNumber].CurrentPageNumber = 0;// 0 = home 
            
                subsystemEISC.BooleanInput[(ushort)(TPNumber + 200)].BoolValue = false;//clear flip to rooms
                subsystemEISC.BooleanInput[(ushort)(TPNumber + 100)].BoolValue = true;//flip to home page
                if (homePageScenario > 0) { 
                    subsystemEISC.UShortInput[(ushort)(TPNumber)].UShortValue = (ushort)manager.WholeHouseSubsystemScenarioZ[homePageScenario].IncludedSubsystems.Count;
                    //Update eisc with subsystem names and icons for current panel
                    for (ushort i = 0; i < manager.WholeHouseSubsystemScenarioZ[homePageScenario].IncludedSubsystems.Count; i++)
                    {
                        ushort subsystemNum = manager.WholeHouseSubsystemScenarioZ[homePageScenario].IncludedSubsystems[i];
                        subsystemEISC.StringInput[(ushort)((TPNumber - 1) * 20 + i + 101)].StringValue = manager.SubsystemZ[subsystemNum].Name;
                        if (manager.touchpanelZ[TPNumber].HTML_UI) { subsystemEISC.StringInput[(ushort)((TPNumber - 1) * 20 + i + 2001)].StringValue = manager.SubsystemZ[subsystemNum].IconHTML; }
                        else { subsystemEISC.StringInput[(ushort)((TPNumber - 1) * 20 + i + 2001)].StringValue = manager.SubsystemZ[subsystemNum].IconSerial; }
                    }
                }
            }
        }
        public void UpdateTPVideoMenu(ushort TPNumber)
        {
            ushort currentRoomNumber = manager.touchpanelZ[TPNumber].CurrentRoomNum;
            if (manager.RoomZ[currentRoomNumber].VideoSrcScenario > 0)
            {
                ushort numSrcs = (ushort)manager.VideoSrcScenarioZ[manager.RoomZ[currentRoomNumber].VideoSrcScenario].IncludedSources.Count;
                videoEISC1.UShortInput[(ushort)(TPNumber)].UShortValue = numSrcs;
                ushort currentVSRC = manager.RoomZ[currentRoomNumber].CurrentVideoSrc;
                //for tsr-310s  enable more sources button
                if (numSrcs > 6)
                {
                    videoEISC1.BooleanInput[(ushort)(TPNumber)].BoolValue = true;
                }
                else { videoEISC1.BooleanInput[(ushort)(TPNumber)].BoolValue = false; }
                subsystemEISC.UShortInput[(ushort)((TPNumber - 1) * 10 + 302)].UShortValue = manager.RoomZ[currentRoomNumber].VideoOutputNum;
                if (currentVSRC > 0)
                {
                    videoEISC1.UShortInput[(ushort)(TPNumber + 200)].UShortValue = manager.VideoSourceZ[currentVSRC].FlipsToPageNumber;
                    videoEISC1.UShortInput[(ushort)(TPNumber + 300)].UShortValue = manager.VideoSourceZ[currentVSRC].EquipID;
                    videoEISC2.StringInput[(ushort)(TPNumber)].StringValue = manager.VideoSourceZ[currentVSRC].DisplayName;
                }
                else
                {
                    videoEISC1.UShortInput[(ushort)(TPNumber + 200)].UShortValue = 0;
                    videoEISC1.UShortInput[(ushort)(TPNumber + 300)].UShortValue = 0;
                    videoEISC2.StringInput[(ushort)(TPNumber)].StringValue = "Off";
                }
                if (currentVSRC == 0)
                {
                    videoEISC1.UShortInput[(ushort)(TPNumber + 400)].UShortValue = 0;//clear the button feedback
                }
                //update the video source list and highlight the appropriate button
                ushort inUse = 0;
                if (manager.touchpanelZ[TPNumber].UseAnalogModes)
                {
                    ushort group = manager.touchpanelZ[TPNumber].CurrentVSrcGroupNum;
                    SetVSRCGroup(TPNumber, group);
                    for (ushort i = 0; i < 6; i++)
                    {
                        if ((ushort)((manager.touchpanelZ[TPNumber].CurrentVSrcGroupNum - 1) * 6 + i) >= numSrcs) { break; }
                        ushort srcNum = manager.VideoSrcScenarioZ[manager.RoomZ[currentRoomNumber].VideoSrcScenario].IncludedSources[(ushort)((group - 1) * 6 + i)];
                        if (manager.VideoSourceZ[srcNum].InUse)
                        {
                            inUse |= (ushort)(1 << i);
                            //CrestronConsole.PrintLine(" {0} IN USE", manager.VideoSourceZ[srcNum].Name);
                        }//set the bit
                        else
                        {
                            inUse &= (ushort)(~(1 << i));
                            //CrestronConsole.PrintLine(" {0} NOT IN USE", manager.VideoSourceZ[srcNum].Name);
                        }
                    }
                    //in use analog
                    videoEISC2.UShortInput[(ushort)(TPNumber)].UShortValue = (ushort)inUse;
                }
                else
                {
                    for (ushort i = 0; i < numSrcs; i++)//loop through all video sources in this scenario
                    {
                        ushort srcNum = manager.VideoSrcScenarioZ[manager.RoomZ[currentRoomNumber].VideoSrcScenario].IncludedSources[i];
                        videoEISC1.StringInput[(ushort)((TPNumber - 1) * 20 + i + 1)].StringValue = manager.VideoSourceZ[srcNum].DisplayName;
                        if (manager.touchpanelZ[TPNumber].HTML_UI) { videoEISC1.StringInput[(ushort)((TPNumber - 1) * 20 + i + 2001)].StringValue = manager.VideoSourceZ[srcNum].IconHTML; }
                        else { videoEISC1.StringInput[(ushort)((TPNumber - 1) * 20 + i + 2001)].StringValue = manager.VideoSourceZ[srcNum].IconSerial; }

                        videoEISC1.UShortInput[(ushort)((TPNumber - 1) * 20 + i + 2001)].UShortValue = manager.VideoSourceZ[srcNum].AnalogModeNumber;
                        //Update the current video source of this room to the panel and highlight the appropriate button
                        if (srcNum == manager.RoomZ[currentRoomNumber].CurrentVideoSrc)
                        {
                            /*if (useAnalogModes > 0)
                            {
                                if (i == 5) { videoEISC1.UShortInput[(ushort)(TPNumber + 401)].UShortValue = 6; }
                                else { videoEISC1.UShortInput[(ushort)(TPNumber + 401)].UShortValue = (ushort)((i + 1) % 6); }//video source button fb for handheld remote
                            }F
                            else*/
                            { videoEISC1.UShortInput[(ushort)(TPNumber + 400)].UShortValue = (ushort)(i + 1); }//video source list button number to highlight
                        }
                    }
                }
            }
        }
        public void UpdateTPMusicMenu(ushort TPNumber)
        {//updates the source text on the sharing menu
            ushort currentRoomNumber = manager.touchpanelZ[TPNumber].CurrentRoomNum;
            ushort asrcScenarioNum = manager.RoomZ[currentRoomNumber].AudioSrcScenario;
            ushort numSrcs = (ushort)manager.AudioSrcScenarioZ[asrcScenarioNum].IncludedSources.Count;
            //ushort useAnalogModes = tpConfigs[TPNumber].useAnalogModes;
            //for tsr-310s  enable more sources button
            if (numSrcs > 6)
            {
                musicEISC1.BooleanInput[(ushort)(TPNumber)].BoolValue = true;
            }
            else { musicEISC1.BooleanInput[(ushort)(TPNumber)].BoolValue = false; }
            //CrestronConsole.PrintLine("UpdateTPMusicMenu currentRoomNumber {0} CurrentMusicSrc{1}", currentRoomNumber, manager.RoomZ[currentRoomNumber].CurrentMusicSrc);
            if (manager.RoomZ[currentRoomNumber].CurrentMusicSrc > 0)
            {
                musicEISC1.UShortInput[(ushort)(TPNumber + 200)].UShortValue = manager.MusicSourceZ[manager.RoomZ[currentRoomNumber].CurrentMusicSrc].FlipsToPageNumber;
                musicEISC1.UShortInput[(ushort)(TPNumber + 300)].UShortValue = manager.MusicSourceZ[manager.RoomZ[currentRoomNumber].CurrentMusicSrc].EquipID;
                musicEISC2.StringInput[(ushort)(TPNumber)].StringValue = manager.MusicSourceZ[manager.RoomZ[currentRoomNumber].CurrentMusicSrc].Name;
            }
            if (manager.RoomZ[currentRoomNumber].CurrentMusicSrc == 0)
            {
                musicEISC1.UShortInput[(ushort)(TPNumber + 400)].UShortValue = 0;//clear the button feedback
            }
            //highlight button fb for the source
            for (ushort i = 0; i < numSrcs; i++)//loop through all music sources in this scenario
            {
                ushort srcNum = manager.AudioSrcScenarioZ[asrcScenarioNum].IncludedSources[i];
                //Update the current audio source of this room to the panel and highlight the appropriate button
                if (srcNum == manager.RoomZ[currentRoomNumber].CurrentMusicSrc)
                {
                    if (manager.touchpanelZ[TPNumber].UseAnalogModes)
                    {
                        if (i == 5) { musicEISC1.UShortInput[(ushort)(TPNumber + 400)].UShortValue = 6; }//fb for button 6
                        else { musicEISC1.UShortInput[(ushort)(TPNumber + 400)].UShortValue = (ushort)((i + 1) % 6); }//music source button fb for handheld remotes
                    }
                    else
                    { 
                        musicEISC1.UShortInput[(ushort)(TPNumber + 400)].UShortValue = (ushort)(i + 1);//music button fb
                        //CrestronConsole.PrintLine("UPDATE TP-{0} MUSIC MENU i={1}", TPNumber, i);
                    }//music source list button number to highlight
                }
            }
            int inUse = 0;
            if (manager.touchpanelZ[TPNumber].UseAnalogModes)
            {
                SetASRCGroup(TPNumber, manager.touchpanelZ[TPNumber].CurrentASrcGroupNum);
                for (ushort i = 0; i < 6; i++)
                {
                    if ((ushort)((manager.touchpanelZ[TPNumber].CurrentASrcGroupNum - 1) * 6 + i) >= numSrcs) { break; }
                    ushort srcNum = manager.AudioSrcScenarioZ[asrcScenarioNum].IncludedSources[(ushort)((manager.touchpanelZ[TPNumber].CurrentASrcGroupNum - 1) * 6 + i)];
                    //in use fb
                    if (manager.MusicSourceZ[srcNum].InUse) { inUse |= (int)(1 << i); }//set the bit
                    else { inUse &= (int)(~(1 << i)); }//clear bit
                }
                //in use analog
                musicEISC3.UShortInput[(ushort)(TPNumber)].UShortValue = (ushort)inUse;
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
            if (currentFormatScenario > 0)
            {
                videoEISC2.UShortInput[(ushort)(TPNumber + 100)].UShortValue = (ushort)manager.FormatScenarioZ[currentFormatScenario].FormatCmds.Count;
            }
            if (currentSleepScenario > 0)
            {
                videoEISC2.UShortInput[(ushort)(TPNumber + 200)].UShortValue = (ushort)manager.SleepScenarioZ[currentSleepScenario].SleepCmds.Count;
            }
            if (currentLiftScenario > 0)
            {
                videoEISC2.UShortInput[(ushort)(TPNumber + 300)].UShortValue = (ushort)manager.LiftScenarioZ[currentLiftScenario].LiftCmds.Count;
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

        public void UpdateLightingStatus(ushort KeypadNumber, bool LightsAreOff) {
            //get the room object

            /*var o = manager.RoomZ.FirstOrDefault(x => x.Value.LightsID == KeypadNumber);//this only does one room. there may be other rooms that satisfy the condition
            o.Value.LightsAreOn = onOrOff;

            //this is more robust
            var p = manager.RoomZ.Where(x => x.Value.LightsID == KeypadNumber);
            foreach (var pp in p) {
                pp.Value.LightsAreOn = onOrOff;
            }
            */

            //CrestronConsole.PrintLine("{0}", o.ToString());
            foreach (var room in manager.RoomZ) {
                if (room.Value.LightsID == 0) {
                    room.Value.LightStatusText = "";
                }
                else if (room.Value.LightsID == KeypadNumber) {
                    room.Value.LightsAreOff = LightsAreOff;
                    if (LightsAreOff)
                    {
                        room.Value.LightStatusText = "Lights are off. ";
                    }
                    else
                    {
                        room.Value.LightStatusText = "Lights are on. ";
                    }
                    //update the text for panels connected to this room
                    UpdateRoomStatusText(room.Value.Number);
                }
            }

            //get the subsystem scenario of the room and if it contains lights continue
            //then update the eisc for the panels to read the current status

        }
        public void SetVSRCGroup(ushort TPNumber, ushort group)
        {
            ushort currentRoomNumber = manager.touchpanelZ[TPNumber].CurrentRoomNum;
            ushort numVSrcs = (ushort)manager.VideoSrcScenarioZ[manager.RoomZ[currentRoomNumber].VideoSrcScenario].IncludedSources.Count;
            ushort numVidGroups = (ushort)(numVSrcs / 6);
            ushort modVid = (ushort)(numVSrcs % 6);
            //ushort useAnalogModes = tpConfigs[TPNumber].useAnalogModes;

            //set the number of groups
            if (modVid > 0) { numVidGroups++; }
            else if (numVidGroups == 0) { numVidGroups++; }
            //update the current group number
            if (group <= numVidGroups) { manager.touchpanelZ[TPNumber].CurrentVSrcGroupNum = group; }
            else { manager.touchpanelZ[TPNumber].CurrentVSrcGroupNum = 1; }
            //set the number of sources to show
            if (manager.touchpanelZ[TPNumber].UseAnalogModes)
            {
                if (numVSrcs < 6) { videoEISC1.UShortInput[(ushort)(TPNumber)].UShortValue = modVid; }
                else if (manager.touchpanelZ[TPNumber].CurrentVSrcGroupNum == numVidGroups && modVid > 0) { videoEISC1.UShortInput[(ushort)(TPNumber)].UShortValue = modVid; }
                else { videoEISC1.UShortInput[(ushort)(TPNumber)].UShortValue = 6; }
            }
            //update the source buttons
            videoEISC1.UShortInput[(ushort)(TPNumber + 400)].UShortValue = 0;//first clear the button fb, it will be updated later
            ushort inUse = 0;
            for (ushort i = 0; i < 6; i++)
            {
                if ((ushort)((manager.touchpanelZ[TPNumber].CurrentVSrcGroupNum - 1) * 6 + i) >= numVSrcs) { break; }
                ushort srcNum = manager.VideoSrcScenarioZ[manager.RoomZ[currentRoomNumber].VideoSrcScenario].IncludedSources[(ushort)((manager.touchpanelZ[TPNumber].CurrentVSrcGroupNum - 1) * 6 + i)];
                //in use
                if (manager.VideoSourceZ[srcNum].InUse)
                {
                    inUse |= (ushort)(1 << (i));
                    CrestronConsole.PrintLine("{0} in use", manager.VideoSourceZ[srcNum].DisplayName);
                }//set the bit
                else
                {
                    inUse &= (ushort)(~(1 << i));
                    CrestronConsole.PrintLine("{0} NOT in use", manager.VideoSourceZ[srcNum].DisplayName);
                }//clear the bit

                if (srcNum == manager.RoomZ[currentRoomNumber].CurrentVideoSrc)
                {
                    if (i == 5) { videoEISC1.UShortInput[(ushort)(TPNumber + 400)].UShortValue = 6; }
                    else { videoEISC1.UShortInput[(ushort)(TPNumber + 400)].UShortValue = (ushort)((i + 1) % 6); }//video source button fb for handheld remote
                }
                videoEISC1.StringInput[(ushort)((TPNumber - 1) * 20 + i + 1)].StringValue = manager.VideoSourceZ[srcNum].DisplayName;
                if (manager.touchpanelZ[TPNumber].HTML_UI) { videoEISC1.StringInput[(ushort)((TPNumber - 1) * 20 + i + 2001)].StringValue = manager.VideoSourceZ[srcNum].IconHTML; }
                else { videoEISC1.StringInput[(ushort)((TPNumber - 1) * 20 + i + 2001)].StringValue = manager.VideoSourceZ[srcNum].IconSerial; }
                videoEISC1.UShortInput[(ushort)((TPNumber - 1) * 20 + i + 2001)].UShortValue = manager.VideoSourceZ[srcNum].AnalogModeNumber;
            }
            CrestronConsole.PrintLine("vsrc in use bin{0} dec{1}", Convert.ToString(inUse, 2), inUse);
            videoEISC2.UShortInput[(ushort)(TPNumber)].UShortValue = (ushort)inUse;
        }
        public void SetASRCGroup(ushort TPNumber, ushort group)
        {
            ushort currentRoomNumber = manager.touchpanelZ[TPNumber].CurrentRoomNum;
            ushort asrcScenario = manager.RoomZ[currentRoomNumber].AudioSrcScenario;
            ushort numASrcs = (ushort)manager.AudioSrcScenarioZ[asrcScenario].IncludedSources.Count;
            ushort numMusicGroups = (ushort)(numASrcs / 6);
            ushort modMusic = (ushort)(numASrcs % 6);
            //ushort useAnalogModes = manager.touchpanelZ[TPNumber].useAnalogModes;
            //set the number of groups
            if (modMusic > 0) { numMusicGroups++; }
            else if (numMusicGroups == 0) { numMusicGroups++; }
            //update the current group number
            if (group <= numMusicGroups) { manager.touchpanelZ[TPNumber].CurrentASrcGroupNum = group; }
            else { manager.touchpanelZ[TPNumber].CurrentASrcGroupNum = 1; }
            //set the number of sources to show
            if (manager.touchpanelZ[TPNumber].UseAnalogModes)
            {
                if (numASrcs < 6) { musicEISC1.UShortInput[(ushort)(TPNumber)].UShortValue = modMusic; }
                else if (manager.touchpanelZ[TPNumber].CurrentASrcGroupNum == numMusicGroups && modMusic > 0) { musicEISC1.UShortInput[(ushort)(TPNumber)].UShortValue = modMusic; }
                else { musicEISC1.UShortInput[(ushort)(TPNumber)].UShortValue = 6; }
            }
            //update the source buttons
            musicEISC1.UShortInput[(ushort)(TPNumber + 400)].UShortValue = 0;//first clear the button fb, it will be updated later
            int inUse = 0;
            for (ushort i = 0; i < 6; i++)
            {
                if ((ushort)((manager.touchpanelZ[TPNumber].CurrentASrcGroupNum - 1) * 6 + i) >= numASrcs)
                {
                    break;
                }//exit the loop if all sources have been tested
                ushort srcNum = 0;
                if (i < manager.AudioSrcScenarioZ[asrcScenario].IncludedSources.Count)
                {
                    srcNum = manager.AudioSrcScenarioZ[asrcScenario].IncludedSources[(ushort)((manager.touchpanelZ[TPNumber].CurrentASrcGroupNum - 1) * 6 + i)];
                }
                if (srcNum > 0)
                {
                    //in use
                    if (manager.MusicSourceZ[srcNum].InUse) { inUse |= (int)(1 << (i)); }//set the bit
                    else { inUse &= (int)(~(1 << i)); }//clear the bit
                    //button fb
                    if (srcNum == manager.RoomZ[currentRoomNumber].CurrentMusicSrc)
                    {
                        if (i == 5) { musicEISC1.UShortInput[(ushort)(TPNumber + 400)].UShortValue = 6; }
                        else { musicEISC1.UShortInput[(ushort)(TPNumber + 400)].UShortValue = (ushort)((i + 1) % 6); } //music source button fb for handheld remotes
                    }
                    musicEISC1.StringInput[(ushort)((TPNumber - 1) * 20 + i + 1)].StringValue = manager.MusicSourceZ[srcNum].Name;
                    if (manager.touchpanelZ[TPNumber].HTML_UI) { musicEISC1.StringInput[(ushort)((TPNumber - 1) * 20 + i + 2001)].StringValue = manager.MusicSourceZ[srcNum].IconHTML; }
                    else { musicEISC1.StringInput[(ushort)((TPNumber - 1) * 20 + i + 2001)].StringValue = manager.MusicSourceZ[srcNum].IconSerial; }
                    musicEISC1.UShortInput[(ushort)((TPNumber - 1) * 20 + i + 1001)].UShortValue = manager.MusicSourceZ[srcNum].AnalogModeNumber;
                }
            }
            musicEISC3.UShortInput[(ushort)(TPNumber)].UShortValue = (ushort)inUse;
        }
        public void RequestMusicSources()
        {
            try
            {
                for (ushort i = 1; i <= manager.MusicSourceZ.Count; i++)
                {
                    ushort input = manager.MusicSourceZ[i].SwitcherInputNumber;
                    musicEISC3.StringInput[(ushort)(input)].StringValue = manager.MusicSourceZ[i].Name;
                }
            }
            catch (Exception e)
            {
                ErrorLog.Error("request musicSourceZ err {0}", e.Message);
            }
            try
            {
                for (ushort i = 1; i <= manager.RoomZ.Count; i++)
                {
                    ushort outputNum = manager.RoomZ[i].AudioID; //audio ID is the swamp output number
                    musicEISC3.StringInput[(ushort)(outputNum + 100)].StringValue = manager.RoomZ[i].Name;
                }
            }
            catch (Exception e)
            {
                ErrorLog.Error("roomZ err {0}", e.Message);
            }
        }

        public void StreamingPlayerProviderChanged(ushort playerNumber, ushort providerNumber) 
        {
            switch (providerNumber) 
            {

                case (0):
                    {
                        currentProviders[playerNumber] = "";
                        break;
                    }
                case (1):break;
                case (2):
                    { 
                        currentProviders[playerNumber] = "Airplay";
                        break;
                    }
                case (3):
                    {
                        currentProviders[playerNumber] = "Spotify Connect";
                        break;
                    }
                case (4):
                    {
                        currentProviders[playerNumber] = "Sirius XM";
                        break;
                    }
                case (5):
                    {
                        currentProviders[playerNumber] = "Pandora";
                        break;
                    }
                case (6):
                    {
                        currentProviders[playerNumber] = "iHeart Radio";
                        break;
                    }
                case (7):
                    {
                        currentProviders[playerNumber] = "Internet Radio";
                        break;
                    }
                case (8):
                    {
                        currentProviders[playerNumber] = "Podcasts";
                        break;
                    }
                default:
                    break;
            }

        }
        public void NAXOutputSrcChanged(ushort zoneNumber, ushort switcherInputNumber)
        {
            CrestronConsole.PrintLine("!!!!!!start NAX outputsrc chagnged {0}:{1} INPUT#{2}-OUTPUT#{3}------------------", DateTime.Now.Second, DateTime.Now.Millisecond, switcherInputNumber, (ushort)(zoneNumber - 500));
            string multiaddress = "";
            bool multiaddressEmpty = false;
            ushort currentMusicSource = 0;
            ushort switcherOutputNumber = (ushort)(zoneNumber - 500); //switcher output number


            //zone source is off
            if (switcherInputNumber == 0) { musicEISC3.StringInput[(ushort)(switcherOutputNumber + 500)].StringValue = "Off";}
            //GET THE CURRENT SOURCE
            else if (switcherInputNumber != 17)
            {
                //get the box number
                int boxNumber = ((switcherOutputNumber - 1) / 8) + 1;
                CrestronConsole.PrintLine("FB FROM NAX box {0} zone {1} input {2}", boxNumber, switcherOutputNumber, switcherInputNumber);
                //now find out which source was selected
                foreach (var src in manager.MusicSourceZ)
                {
                    ushort srckey = src.Key;//this is the place number in the list of sources
                    if (manager.MusicSourceZ[srckey].NaxBoxNumber == boxNumber)
                    {
                        if (manager.MusicSourceZ[srckey].SwitcherInputNumber == switcherInputNumber)
                        {
                            //we found the source
                            currentMusicSource = srckey;
                        }
                    }
                }
            }
            else
            {//this is a streaming source
                //we need to check the multicast address because it may not have changed
                multiaddress = multis[switcherOutputNumber];
                try
                {
                    CrestronConsole.PrintLine("multi address = {0} length {1}", multiaddress, multiaddress.Length);
                    if (multiaddress == "0.0.0.0" || multiaddress == "") {
                        multiaddressEmpty = true;
                    }
                    else
                    {
                        foreach (var src in manager.MusicSourceZ)
                        {
                            if (src.Value.MultiCastAddress == multiaddress)
                            {
                                currentMusicSource = src.Value.Number;
                                CrestronConsole.PrintLine("MULTII currentMusicSource {0}", currentMusicSource);
                            }
                        }
                        multiaddressEmpty = false;
                    }
                }
                catch 
                {
                    CrestronConsole.PrintLine("multi address is empty");
                    multiaddressEmpty = true;
                }
            }
            //send the source name to the audio zone module
            if (currentMusicSource > 0) { musicEISC3.StringInput[(ushort)(switcherOutputNumber + 500)].StringValue = manager.MusicSourceZ[currentMusicSource].Name; }
            updateMusicSourceInUse(currentMusicSource, switcherInputNumber, switcherOutputNumber);
            


            //update the room to reflect the current source
            foreach (var rm in manager.RoomZ)
            {
                if (rm.Value.AudioID == switcherOutputNumber)
                {
                    if (switcherInputNumber == 0)
                    {
                        rm.Value.MusicStatusText = "";
                        rm.Value.CurrentMusicSrc = 0;
                    }
                    else if (switcherInputNumber != 17 || !multiaddressEmpty)//this is a dumb fix to block clearing out a rooms currentmusicsrc value
                    {
                        rm.Value.CurrentMusicSrc = currentMusicSource;
                    }
                    CrestronConsole.PrintLine("NAX OUTPUT CHANGED switcherOutputNumber{0} currentMusicSource{1}", switcherOutputNumber, currentMusicSource);

                    //turn the receiver on or off
                    ushort configNum = rm.Value.ConfigurationScenario;
                    bool hasRec = manager.VideoConfigScenarioZ[configNum].HasReceiver;
                    //send receiver commands
                    if (hasRec)
                    {
                        ReceiverOnOffFromDistAudio(rm.Value.Number, currentMusicSource);
                    }
                }
            }
            //in the case that multiple zones are changing sources this delay will let the switching go through and then update the panel status later to prevent bogging down the system by calling the update function every time
            if (!NAXtimerBusy)
            {
                NAXoutputChangedTimer = new CTimer(NAXoutputChangedCallback, 0, 2000);
                NAXtimerBusy = true;
                /*xtimer = new System.Timers.Timer();
                xtimer.Interval = 2000;
                ElapsedEventHandler handler = (s, e) =>
                {
                    xtimer.Stop();
                    xtimer.Dispose();
                    CrestronConsole.PrintLine("############### s{0} e{1}", s, e.ToString());
                    CrestronConsole.PrintLine("############### {0}:{1}", DateTime.Now.Second, DateTime.Now.Millisecond);
                    UpdateAllPanelsTextWhenAudioChanges();

                    NAXtimerBusy = false;
                };
                xtimer.Elapsed += handler;
                xtimer.AutoReset = false;
                xtimer.Enabled = true;*/
                
                CrestronConsole.PrintLine("STARTED NAXoutputChangedCallback {0}:{1}", DateTime.Now.Second, DateTime.Now.Millisecond);
            }

            CrestronConsole.PrintLine("!!!!!END NAXoutputsrcchanged {0}:{1}--------------------", DateTime.Now.Second, DateTime.Now.Millisecond);
        }
        private void NAXoutputChangedCallback(object obj)
        {
            NAXoutputChangedTimer.Stop();
            NAXoutputChangedTimer.Dispose();
            UpdateAllPanelsTextWhenAudioChanges();
            CrestronConsole.PrintLine("##############     NAXoutputChangedCallback {0}:{1}", DateTime.Now.Second, DateTime.Now.Millisecond);

            NAXtimerBusy = false;
        }

        public void UpdateAllPanelsTextWhenAudioChanges() 
        {
            foreach (var tp in manager.touchpanelZ)
            {
                ushort TPNumber = tp.Value.Number;
                ushort currentRoomNumber = tp.Value.CurrentRoomNum;
                ushort currentMusicSource = manager.RoomZ[currentRoomNumber].CurrentMusicSrc;
                //only update if the panel is currently on the rooms page
                if (manager.touchpanelZ[TPNumber].CurrentPageNumber == 1)
                {
                    UpdateRoomsPageStatusText(TPNumber);
                }
                //find which panels are connected to the current room and update the current source text
                
                CrestronConsole.PrintLine("TP-{0} Room#{1}", TPNumber, manager.RoomZ[currentRoomNumber].AudioID);
                if (manager.RoomZ[currentRoomNumber].CurrentMusicSrc == 0) 
                {
                    UpdatePanelToMusicZoneOff(TPNumber);
                }
                else
                {
                    musicEISC2.StringInput[TPNumber].StringValue = manager.MusicSourceZ[currentMusicSource].Name;//current source to TP
                    CrestronConsole.PrintLine("TP-{0} current musocii src == {1}", TPNumber, manager.MusicSourceZ[currentMusicSource].Name);
                    musicEISC1.UShortInput[(ushort)(TPNumber + 100)].UShortValue = manager.MusicSourceZ[currentMusicSource].Number;//current asrc number to panel media server and sharing objects
                    musicEISC1.UShortInput[(ushort)(TPNumber + 200)].UShortValue = manager.MusicSourceZ[currentMusicSource].FlipsToPageNumber;//current asrc page number to panel
                }
                //this will update the current music source is playing text on the subsystems list menu
                UpdatePanelSubsystemText(TPNumber);
            }
        }
        public void NAXZoneMulticastChanged(ushort zoneNumber, string multiAddress)
        {
            if (multiAddress != "0.0.0.0") //we don't want to do anything if the zone is turned off. the input will handle that case 
            { 
                ushort currentMusicSource = 0;
                zoneNumber = (ushort)(zoneNumber - 300);
                CrestronConsole.PrintLine("NAXZoneMulticastChanged - zone {1} multi address changed to = {0}", multiAddress, zoneNumber);

                //figure out which music source this is
                foreach (var src in manager.MusicSourceZ)
                {
                    if (src.Value.MultiCastAddress == multiAddress) { currentMusicSource = src.Value.Number; }
                }
                //figure out the room and update its status
                if (currentMusicSource > 0)
                {
                    foreach (var rm in manager.RoomZ)
                    {
                        if (rm.Value.AudioID == zoneNumber) { rm.Value.CurrentMusicSrc = currentMusicSource; }
                    }
                    musicEISC3.StringInput[(ushort)(zoneNumber + 500)].StringValue = manager.MusicSourceZ[currentMusicSource].Name;//update the current source to the zone module which also updates the sharing page
                    updateMusicSourceInUse(currentMusicSource, manager.MusicSourceZ[currentMusicSource].SwitcherInputNumber, zoneNumber);
                }
            }
        }

        public void updateMusicSourceInUse(ushort sourceNumber, ushort switcherInputNumber, ushort switcherOutputNum) {
            ushort currentRoomNumber = 0;
            if (sourceNumber > 0) { 
                manager.MusicSourceZ[sourceNumber].InUse = true;
            }
            CrestronConsole.PrintLine("sourceNumber{0} switcherInputNumber{1} output{2}", sourceNumber, switcherInputNumber, switcherOutputNum);
            //update room status to indicate the current source playing
            foreach (var room in manager.RoomZ)
            {
                if (room.Value.AudioID == switcherOutputNum)//audio ID is the equipID as well as the zone output number
                {
                    currentRoomNumber = room.Value.Number;
                    room.Value.CurrentMusicSrc = sourceNumber;//update the room with the current audio source number
                    //update the music status text
                    if (switcherInputNumber > 0 && sourceNumber > 0)
                    {
                        //check if this source is defined in the config file. it could have switched to an input that isn't defined and will cause an out of bounds error
                        if (manager.MusicSourceZ.Count >= sourceNumber) //source number is not the same as switcher input number. it is the position in the list of sources
                        { 
                            room.Value.MusicStatusText = manager.MusicSourceZ[sourceNumber].Name + " is playing. ";
                        }
                    }
                    else
                    {
                        room.Value.MusicStatusText = "";
                    }
                    //update the text for panels connected to this room
                    UpdateRoomStatusText(room.Value.Number);
                    

                }
            }
            //loop through all sources and all rooms to find out if any source is no longer in use
            for (ushort i = 1; i <= manager.MusicSourceZ.Count; i++)
            {
                ushort k = 0;
                //find out if music source i is in use
                foreach (var room in manager.RoomZ)
                {
                    if (room.Value.CurrentMusicSrc == i) { k++; }
                }
                if (k == 0)//this means its not in use
                {
                    manager.MusicSourceZ[i].InUse = false;
                    if (manager.MusicSourceZ[i].SwitcherInputNumber > 8)//this is a streaming player 
                    { 
                        musicEISC1.UShortInput[(ushort)(600 + manager.MusicSourceZ[i].SwitcherInputNumber - 8)].UShortValue = 0;//streaming provider off
                    }
                }
            }
            foreach (var tp in manager.touchpanelZ)
            {
                UpdateTPMusicMenu(tp.Key);
            }
        }
        public void ReceiverOnOffFromDistAudio(ushort roomNumber, ushort sourceNumber) 
        {
            ushort videoSwitcherOutputNum = manager.RoomZ[roomNumber].VideoOutputNum;//this is also the roomNumber in the simpl program. unfortunately. this should change so the dm output can change easily.
            ushort asrcScenario = manager.RoomZ[roomNumber].AudioSrcScenario;
                if (sourceNumber == 0)
                {
                    if (manager.RoomZ[roomNumber].CurrentVideoSrc == 0) //make sure video isn't being watched. TODO - change this to check the current receiver input # and turn it off if its on a music input.
                    {
                        videoEISC1.UShortInput[(ushort)(videoSwitcherOutputNum + 700)].UShortValue = 0;//turn the receiver off
                    }
                }
                else // send the input to the receiver
                {
                    for (ushort j = 0; j < manager.AudioSrcScenarioZ[asrcScenario].IncludedSources.Count; j++)
                    {
                        if (sourceNumber == manager.AudioSrcScenarioZ[asrcScenario].IncludedSources[j])
                        {
                            videoEISC1.UShortInput[(ushort)(videoSwitcherOutputNum + 700)].UShortValue = manager.AudioSrcScenarioZ[asrcScenario].ReceiverInputs[j];//receiver input

                            //turn off video for the room
                            videoEISC1.UShortInput[(ushort)(videoSwitcherOutputNum + 600)].UShortValue = 0;//TV off
                            videoEISC1.UShortInput[(ushort)(videoSwitcherOutputNum + 500)].UShortValue = 0; //DM off
                            CrestronConsole.PrintLine("Video off from DISTRIBUTAED AUDIO");
                        }
                    }
                }
            
        }
        public void SwampOutputSrcChanged(ushort zoneNumber, ushort switcherInputNumber)
        {

            ushort switcherOutputNumber = (ushort)(zoneNumber - 500); //switcher output number
            ushort sourceNumber = 0;

            //translate source number from switcher input number if this is a swamp
            //then update the 'in use' status of the source
            for (ushort i = 1; i <= manager.MusicSourceZ.Count; i++)
            {
                if (manager.MusicSourceZ[i].SwitcherInputNumber == switcherInputNumber)
                {
                    sourceNumber = i;
                }
            }            //turn the receiver on or off
            foreach (var room in manager.RoomZ)
            {
                if (room.Value.AudioID == switcherOutputNumber)
                {

                    ushort configNum = room.Value.ConfigurationScenario;
                    bool hasRec = manager.VideoConfigScenarioZ[configNum].HasReceiver;
                    //send receiver commands
                    if (hasRec)
                    {
                        ReceiverOnOffFromDistAudio(room.Value.Number, sourceNumber);
                    }
                }
            }
            CrestronConsole.PrintLine("SWAMP zoneNumber {0} switcherInputNumber {1}", switcherOutputNumber, switcherInputNumber);
            updateMusicSourceInUse(sourceNumber, switcherInputNumber, switcherOutputNumber);

        }
        public void DmOutputChanged(ushort dmOutNumber, ushort switcherInputNumber)
        {
            dmOutNumber = (ushort)(dmOutNumber - 500);
            ushort sourceNumber = 0;
            ushort numberOfVSRCs = (ushort)manager.VideoSourceZ.Count;
            ushort numberOfRooms = (ushort)manager.RoomZ.Count;
            //set the source IN USe attribute to true if the input matches
            if (switcherInputNumber > 0)
            {
                videoEISC1.BooleanInput[(ushort)(switcherInputNumber + 100)].BoolValue = true;
                ushort key = manager.VideoSourceZ.FirstOrDefault(p => p.Value.VidSwitcherInputNumber == switcherInputNumber).Key;
                manager.VideoSourceZ[key].InUse = true;
                sourceNumber = key;
            }

            //update the rooms source
            //for (ushort i = 1; i <= numberOfRooms; i++)
            foreach(var room in manager.RoomZ)
            {
                if (room.Value.VideoOutputNum == dmOutNumber)
                {
                    room.Value.CurrentVideoSrc = sourceNumber;
                    if (sourceNumber > 0)
                    {
                        room.Value.VideoStatusText = manager.VideoSourceZ[sourceNumber].DisplayName + " is on. ";
                    }
                    else {
                        room.Value.VideoStatusText = "";
                    }
                    //update the text for panels connected to this room
                    UpdateRoomStatusText(room.Value.Number);
                }
            }
            //clear the IN USE attribute if it's not being used

            for (ushort i = 1; i <= numberOfVSRCs; i++)
            {
                ushort k = 0;
                for (ushort j = 1; j <= numberOfRooms; j++)
                {
                    if (manager.RoomZ[j].CurrentVideoSrc == i) { k++; }
                }
                if (k == 0)
                {
                    manager.VideoSourceZ[i].InUse = false;
                    videoEISC1.BooleanInput[(ushort)(manager.VideoSourceZ[i].VidSwitcherInputNumber + 100)].BoolValue = false;

                }
            }
            foreach (var tp in manager.touchpanelZ)
            {
                ushort j = tp.Key;
                ushort currentRoomNumber = manager.touchpanelZ[j].CurrentRoomNum;
                ushort panelVideoOutputNumber = manager.RoomZ[currentRoomNumber].VideoOutputNum;// this panel is currently connected to a room that is associated with this video switcher output# 
                //if this panel is watching this output, update it
                if (panelVideoOutputNumber == dmOutNumber)
                {
                    CrestronConsole.PrintLine("Room# {0} DM ouutput {1} in {2} j={3}", currentRoomNumber, dmOutNumber, switcherInputNumber, j);
                    UpdateTPVideoMenu(j);
                }
            }
        }
        public void UpdateRoomAVConfig()
        {
            ushort videoOutNumber = 0;
            for (ushort i = 1; i <= manager.RoomZ.Count; i++)
            {
                ushort vidConfigNum = manager.RoomZ[i].ConfigurationScenario;
                if (vidConfigNum > 0)
                {
                    videoOutNumber = manager.RoomZ[i].VideoOutputNum;
                    if (videoOutNumber > 0)
                    {
                        //BOOLEANS
                        videoEISC3.BooleanInput[videoOutNumber].BoolValue = manager.VideoConfigScenarioZ[vidConfigNum].HasReceiver;
                        CrestronConsole.PrintLine("Room#{0} hasRec={1} vidconfignum{2}", i, manager.VideoConfigScenarioZ[vidConfigNum].HasReceiver, vidConfigNum);
                        videoEISC3.BooleanInput[(ushort)(videoOutNumber + 100)].BoolValue = manager.VideoConfigScenarioZ[vidConfigNum].ReceiverHasVolFB;
                        videoEISC3.BooleanInput[(ushort)(videoOutNumber + 200)].BoolValue = manager.VideoConfigScenarioZ[vidConfigNum].MusicHasVolFB;
                        videoEISC3.BooleanInput[(ushort)(videoOutNumber + 300)].BoolValue = manager.VideoConfigScenarioZ[vidConfigNum].TvHasVolFB;
                        videoEISC3.BooleanInput[(ushort)(videoOutNumber + 400)].BoolValue = manager.VideoConfigScenarioZ[vidConfigNum].VideoVolThroughDistAudio;
                        videoEISC3.BooleanInput[(ushort)(videoOutNumber + 500)].BoolValue = manager.VideoConfigScenarioZ[vidConfigNum].SendToSpeakers;
                        videoEISC3.BooleanInput[(ushort)(videoOutNumber + 600)].BoolValue = manager.VideoConfigScenarioZ[vidConfigNum].ReceiverHasBreakawayAudio;
                        //ANALOGS
                        videoEISC3.UShortInput[(ushort)(videoOutNumber + 300)].UShortValue = manager.RoomZ[i].AudioID;//swamp output number
                        videoEISC3.UShortInput[(ushort)(videoOutNumber + 400)].UShortValue = manager.RoomZ[i].VideoOutputNum;
                        videoEISC3.UShortInput[(ushort)(videoOutNumber + 500)].UShortValue = manager.VideoConfigScenarioZ[vidConfigNum].ReceiverInputDelay;
                        videoEISC3.UShortInput[(ushort)(videoOutNumber + 600)].UShortValue = manager.VideoConfigScenarioZ[vidConfigNum].DisplayInputDelay;
                        videoEISC3.UShortInput[(ushort)(videoOutNumber + 700)].UShortValue = manager.RoomZ[i].TvOutToAudioInputNumber;
                        videoEISC3.UShortInput[(ushort)(videoOutNumber + 800)].UShortValue = manager.VideoConfigScenarioZ[vidConfigNum].OffSubScenarioNum;
                        videoEISC3.UShortInput[(ushort)(videoOutNumber + 1100)].UShortValue = manager.VideoConfigScenarioZ[vidConfigNum].MusicThroughReceiver; //TODO this should probably be deleted because audioSrcScenarios has receiverInputs

                    //ROOM NAMES FOR VIDEO
                    if (manager.RoomZ[i].VideoOutputNum > 0)
                    {
                        videoEISC2.StringInput[(ushort)(manager.RoomZ[i].VideoOutputNum + 100)].StringValue = manager.RoomZ[i].Name;
                    }
                    //LIFT
                    ushort liftScenarioNum = manager.RoomZ[i].LiftScenario;
                    if (liftScenarioNum > 0)
                    {
                        videoEISC3.UShortInput[(ushort)(videoOutNumber + 900)].UShortValue = manager.LiftScenarioZ[liftScenarioNum].OpenWithOnCmdNum;
                        videoEISC3.UShortInput[(ushort)(videoOutNumber + 1000)].UShortValue = manager.LiftScenarioZ[liftScenarioNum].CloseWithOffCmdNum;
                        for (ushort j = 0; j < manager.LiftScenarioZ[liftScenarioNum].LiftCmds.Count; j++)
                        {
                            ushort cmdNum = manager.LiftScenarioZ[liftScenarioNum].LiftCmds[j];
                            videoEISC3.UShortInput[(ushort)((videoOutNumber - 1) * 25 + 1200 + (j + 1))].UShortValue = manager.LiftCmdZ[cmdNum].CmdNum;
                            videoEISC3.UShortInput[(ushort)((videoOutNumber - 1) * 25 + 1205 + (j + 1))].UShortValue = manager.LiftCmdZ[cmdNum].PulseTime;
                        }
                    }
                    ushort sleepScenarioNum = manager.RoomZ[i].SleepScenario;
                    if (sleepScenarioNum > 0)
                    {
                        for (ushort j = 0; j < manager.SleepScenarioZ[sleepScenarioNum].SleepCmds.Count; j++)
                        {
                            ushort sleepCmd = manager.SleepScenarioZ[sleepScenarioNum].SleepCmds[j];
                            videoEISC3.UShortInput[(ushort)((videoOutNumber - 1) * 25 + 1210 + (j + 1))].UShortValue = manager.SleepCmdZ[sleepCmd].Length;
                        }
                    }
                    ushort formatScenarioNum = manager.RoomZ[i].FormatScenario;
                    if (formatScenarioNum > 0)
                    {
                        for (ushort j = 0; j < manager.FormatScenarioZ[formatScenarioNum].FormatCmds.Count; j++)
                        {
                            ushort formatCmd = manager.FormatScenarioZ[formatScenarioNum].FormatCmds[j];
                            videoEISC3.UShortInput[(ushort)((videoOutNumber - 1) * 25 + 1215 + (j + 1))].UShortValue = manager.FormatCmdZ[formatCmd].CmdNum;
                        }
                    }
                    }
                }
            }
        }
        public void StartupRooms()
        {
            bool audioFound = false;
            foreach (var room in manager.RoomZ)
            {
                ushort subsystemScenario = room.Value.SubSystemScenario;
                ushort numSubsystems = (ushort)manager.SubsystemScenarioZ[subsystemScenario].IncludedSubsystems.Count;

                //find out if the room has music. if not then default to last used system video.
                for (ushort i = 0; i < numSubsystems; i++)
                {
                    ushort j = manager.SubsystemScenarioZ[subsystemScenario].IncludedSubsystems[i];
                    if (manager.SubsystemZ[j].DisplayName.ToUpper() == "AUDIO" || manager.SubsystemZ[j].DisplayName.ToUpper() == "MUSIC")
                    {
                        audioFound = true;
                    }

                }
                if (!audioFound)
                {
                    room.Value.LastSystemVid = true;
                }
                //update the lighting status
                if (room.Value.LightsID > 0 && room.Value.Name.ToUpper() != "GLOBAL") {
                    //get the status of the lights
                    room.Value.LightsAreOff = lightingEISC.BooleanOutput[room.Value.LightsID].BoolValue;
                    if (room.Value.LightsAreOff)
                    {
                        room.Value.LightStatusText = "Lights are off. ";
                    }
                    else {
                        room.Value.LightStatusText = "Lights are on. ";
                    }
                }
            }
            foreach (var tp in manager.touchpanelZ)
            {
                ushort TPNumber = tp.Value.Number;
                ushort currentRoomNumber = tp.Value.CurrentRoomNum;
                HomeButtonPress(tp.Value.Number);
            }
        }

        public void InitializeMulticast() {
            foreach (var src in manager.MusicSourceZ)
            {
                ushort box = src.Value.NaxBoxNumber;
                ushort input = src.Value.SwitcherInputNumber;
                if (box > 0) {
                    ushort srcNum = (ushort)(((box - 1) * 16) + input);
                    musicEISC3.StringInput[(ushort)(400 + srcNum)].StringValue = src.Value.MultiCastAddress;
                }
            }
        }

        public void UpdateRoomStatusText(ushort roomNumber) {
            //cycle through all panels and update text IF the current room is in the panels current selected floor list of rooms
            ushort numZones = 0;
            foreach (var tp in manager.touchpanelZ)
            {
                
                if (manager.Floorz.ContainsKey(tp.Value.CurrentFloorNum))//this is just to check whether it exists to avoid out of bounds error
                {
                    numZones = (ushort)manager.Floorz[tp.Value.CurrentFloorNum].IncludedRooms.Count;
                }
                if (numZones > 0) {
                    for (ushort i = 0; i < numZones; i++)
                    {
                        try
                        {
                            if (tp.Value.CurrentFloorNum > 0)
                            {
                                if (roomNumber == manager.Floorz[tp.Value.CurrentFloorNum].IncludedRooms[i])
                                {
                                    ushort tpNumber = tp.Value.Number;
                                    ushort eiscPosition = (ushort)(201 + (30 * (tpNumber - 1)) + i);
                                    string statusText = manager.RoomZ[roomNumber].LightStatusText + manager.RoomZ[roomNumber].VideoStatusText + manager.RoomZ[roomNumber].MusicStatusText;
                                    //CrestronConsole.PrintLine("room{0}: {1}", roomNumber, statusText);
                                    videoEISC2.StringInput[eiscPosition].StringValue = statusText;
                                }
                            }
                        }
                        catch {
                            CrestronConsole.PrintLine("crapped out");
                        }
                    }
                }
                numZones = 0;
                //next update the subysytem status list for panels that are currently controlling this room.
                if (tp.Value.CurrentRoomNum == roomNumber)
                {
                    UpdatePanelSubsystemText(tp.Value.Number);
                }
            }
        }


        public void UpdatePanelSubsystemText(ushort TPNumber)
        {
            //On the list of subysystems menu - this function will update the status text
            ushort roomNumber = manager.touchpanelZ[TPNumber].CurrentRoomNum;
            ushort subsystemScenario = manager.RoomZ[roomNumber].SubSystemScenario;
            ushort numSubsystems = (ushort)manager.SubsystemScenarioZ[subsystemScenario].IncludedSubsystems.Count;
            string statusText = "";
            string subName = "";
            ushort eiscPosition;
            for (ushort i = 0; i < numSubsystems; i++)
            {
                subName = manager.SubsystemZ[manager.SubsystemScenarioZ[subsystemScenario].IncludedSubsystems[i]].Name;
                eiscPosition = (ushort)(2601 + (20 * (TPNumber - 1)) + i);
                //CrestronConsole.PrintLine("subName {0} eiscPosition {1} ", subName, eiscPosition);


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
                else if (subName.ToUpper().Contains("VIDEO") || subName.ToUpper().Contains("WATCH"))
                {
                    ushort currentVSRC = manager.RoomZ[roomNumber].CurrentVideoSrc;
                    if (currentVSRC > 0)
                    {
                        statusText = manager.VideoSourceZ[currentVSRC].DisplayName + " is on. ";
                    }
                    else { statusText = "Off"; }
                }
                else if (subName.ToUpper().Contains("CLIMATE") || subName.ToUpper().Contains("HVAC")) {
                    statusText = GetHVACStatusText(roomNumber);
                }
                else
                {
                    statusText = "";
                }
                musicEISC2.StringInput[eiscPosition].StringValue = statusText;
            }
        }

        public void UpdateRoomHVACText(ushort roomNumber) {
            //this function is called when the hvac status changes for a particular room
            //it will update the room list status text for all panels that have that room
            CrestronConsole.PrintLine("updateRoomHVACText roomNumber {0}, temp {1}", roomNumber, manager.RoomZ[roomNumber].CurrentTemperature);
            //this updates the zone list room status
            foreach (var tp in manager.touchpanelZ)
            {
                ushort numZones = (ushort)manager.Floorz[tp.Value.CurrentFloorNum].IncludedRooms.Count;
                for (ushort i = 0; i < numZones; i++)
                {
                    if (roomNumber == manager.Floorz[tp.Value.CurrentFloorNum].IncludedRooms[i])
                    {
                        ushort tpNumber = tp.Value.Number;
                        ushort eiscPosition = (ushort)(601 + (30 * (tpNumber - 1)) + i);
                        string statusText = GetHVACStatusText(roomNumber);
                        musicEISC3.StringInput[eiscPosition].StringValue = statusText;
                    }
                }
                //next update the subysytem status list for panels that are currently controlling this room.
                if (tp.Value.CurrentRoomNum == roomNumber)
                {
                    UpdatePanelHVACTextInSubsystemList(tp.Value.Number);
                }
            }
        }

        public void UpdatePanelHVACTextInSubsystemList(ushort TPNumber) {
            ushort roomNumber = manager.touchpanelZ[TPNumber].CurrentRoomNum;
            ushort subsystemScenario = manager.RoomZ[roomNumber].SubSystemScenario;
            ushort numSubsystems = (ushort)manager.SubsystemScenarioZ[subsystemScenario].IncludedSubsystems.Count;
            for (ushort i = 0; i < numSubsystems; i++)
            {
                string subName = manager.SubsystemZ[manager.SubsystemScenarioZ[subsystemScenario].IncludedSubsystems[i]].Name;
                if (subName.Contains("Climate") || subName.Contains("HVAC"))
                {
                    ushort eiscPosition = (ushort)(2601 + (20 * (TPNumber - 1)) + i);
                    string statusText = GetHVACStatusText(roomNumber);

                    musicEISC2.StringInput[eiscPosition].StringValue = statusText;
                }
            }
        }
        public string GetHVACStatusText(ushort roomNumber)
        {
            string statusText = "";
            ushort subsystemScenario = manager.RoomZ[roomNumber].SubSystemScenario;
            for (int i = 1; i < manager.SubsystemScenarioZ[subsystemScenario].IncludedSubsystems.Count; i++) {
                string subName = manager.SubsystemZ[manager.SubsystemScenarioZ[subsystemScenario].IncludedSubsystems[i]].DisplayName;
                if (subName.ToUpper() == "CLIMATE" || subName.ToUpper() == "HVAC") {
                    switch (manager.RoomZ[roomNumber].ClimateMode)
                    {
                        case ("Heat"):
                            {
                                statusText = "<B>" + Convert.ToString(manager.RoomZ[roomNumber].CurrentTemperature) + "° </B> - Heating to " + Convert.ToString(manager.RoomZ[roomNumber].CurrentHeatSetpoint) + "°";
                                break;
                            }
                        case ("Cool"):
                            {
                                statusText = "<B>" + Convert.ToString(manager.RoomZ[roomNumber].CurrentTemperature) + "° </B> - Cooling to " + Convert.ToString(manager.RoomZ[roomNumber].CurrentCoolSetpoint) + "°";
                                break;
                            }
                        default:
                            {
                                statusText = "<B>" + Convert.ToString(manager.RoomZ[roomNumber].CurrentTemperature) + "°  </B>";
                                break;
                            }
                    }
                }
            }

            return statusText;
        }

        public void SaveMusicSettingsToPreset(ushort presetNumber) {
            //TODO
            foreach (var rm in manager.RoomZ) {
                ushort switcherOutput = rm.Value.AudioID;
                ushort currentSrc = rm.Value.CurrentMusicSrc;

                quickActionManager.MusicPresetZ[presetNumber].Sources[switcherOutput-1] = currentSrc;
                quickActionManager.MusicPresetZ[presetNumber].Volumes[switcherOutput-1] = volumes[switcherOutput-1];
                CrestronConsole.PrintLine("presetNumber {0} switcherOutput {1} currentSrc {2}", presetNumber, switcherOutput, currentSrc);
            }
            CrestronConsole.PrintLine("save start");
            //TODO
            //write to json file
            this.quickActionConfig.UpdateConfiguration(this.quickActionConfig.QuickConfig);//durrr/
            CrestronConsole.PrintLine("saved preset {0}", presetNumber);
        }
        /// <summary>
        /// InitializeSystem - this method gets called after the constructor 
        /// has finished. 
        /// 
        /// Use InitializeSystem to:
        /// * Start threads
        /// * Configure ports, such as serial and verisports
        /// * Start and initialize socket connections
        /// Send initial device configurations
        /// 
        /// Please be aware that InitializeSystem needs to exit quickly also; 
        /// if it doesn't exit in time, the SIMPL#Pro program will exit.
        /// </summary>
        public override void InitializeSystem()
        {
            try
            {
                //find out how to add reference system.threading.task
                //Task.Run(() => this.SystemSetup());
                CrestronConsole.PrintLine("system setup start");
                this.SystemSetup();
                CrestronConsole.PrintLine("system setup complete");

                IPaddress = CrestronEthernetHelper.GetEthernetParameter(CrestronEthernetHelper.ETHERNET_PARAMETER_TO_GET.GET_CURRENT_IP_ADDRESS, CrestronEthernetHelper.GetAdapterdIdForSpecifiedAdapterType(EthernetAdapterType.EthernetLANAdapter));

                foreach (var src in manager.MusicSourceZ) {
                    CrestronConsole.PrintLine("msuic src {0}", src.Value.Name);
                    ushort srcNum = src.Key;
                    if (manager.MusicSourceZ[srcNum].NaxBoxNumber > 0) {
                        NAXsystem = true;
                    }
                }


                foreach (var tp in manager.touchpanelZ)
                {
                    ushort tpNum = tp.Key;
                    StartupPanels(tpNum);
                    if (manager.touchpanelZ[tpNum].ChangeRoomButtonEnable)
                    {
                        roomSelectEISC.BooleanInput[tpNum].BoolValue = true;
                    }
                    CrestronConsole.PrintLine("startup TP-{0}", tpNum);
                }
                if (NAXsystem)
                {
                    CrestronConsole.PrintLine("this system has NAX ---------------------------");
                    InitializeMulticast();
                }
                else
                {
                    CrestronConsole.PrintLine("this is not an NAX system----------------------");
                }
                
                UpdateRoomAVConfig();
                StartupRooms();//this sets last system vid to true if no audio is found

                imageEISC.UShortInput[101].UShortValue = (ushort)quickActionManager.MusicPresetZ.Count;
                for (ushort i = 1; i <= quickActionManager.MusicPresetZ.Count; i++) {
                    imageEISC.StringInput[(ushort)(i + 3100)].StringValue = quickActionManager.MusicPresetZ[i].Name;
                }
                
                subsystemEISC.BooleanInput[1].BoolValue = true;// tell the av program that this program has loaded
                initComplete = true;
            }
            catch (Exception e)
            {
                ErrorLog.Error("Error in InitializeSystem: {0}", e.Message);
            }
        }

        /// <summary>
        /// Event Handler for Ethernet events: Link Up and Link Down. 
        /// Use these events to close / re-open sockets, etc. 
        /// </summary>
        /// <param name="ethernetEventArgs">This parameter holds the values 
        /// such as whether it's a Link Up or Link Down event. It will also indicate 
        /// wich Ethernet adapter this event belongs to.
        /// </param>
        void ControlSystem_ControllerEthernetEventHandler(EthernetEventArgs ethernetEventArgs)
        {
            switch (ethernetEventArgs.EthernetEventType)
            {//Determine the event type Link Up or Link Down
                case (eEthernetEventType.LinkDown):
                    //Next need to determine which adapter the event is for. 
                    //LAN is the adapter is the port connected to external networks.
                    if (ethernetEventArgs.EthernetAdapter == EthernetAdapterType.EthernetLANAdapter)
                    {
                        //
                    }
                    break;
                case (eEthernetEventType.LinkUp):
                    if (ethernetEventArgs.EthernetAdapter == EthernetAdapterType.EthernetLANAdapter)
                    {

                    }
                    break;
            }
        }

        /// <summary>
        /// Event Handler for Programmatic events: Stop, Pause, Resume.
        /// Use this event to clean up when a program is stopping, pausing, and resuming.
        /// This event only applies to this SIMPL#Pro program, it doesn't receive events
        /// for other programs stopping
        /// </summary>
        /// <param name="programStatusEventType"></param>
        void ControlSystem_ControllerProgramEventHandler(eProgramStatusEventType programStatusEventType)
        {
            switch (programStatusEventType)
            {
                case (eProgramStatusEventType.Paused):
                    //The program has been paused.  Pause all user threads/timers as needed.
                    break;
                case (eProgramStatusEventType.Resumed):
                    //The program has been resumed. Resume all the user threads/timers as needed.
                    break;
                case (eProgramStatusEventType.Stopping):
                    //The program has been stopped.
                    //Close all threads. 
                    //Shutdown all Client/Servers in the system.
                    //General cleanup.
                    //Unsubscribe to all System Monitor events
                    break;
            }

        }

        /// <summary>
        /// Event Handler for system events, Disk Inserted/Ejected, and Reboot
        /// Use this event to clean up when someone types in reboot, or when your SD /USB
        /// removable media is ejected / re-inserted.
        /// </summary>
        /// <param name="systemEventType"></param>
        void ControlSystem_ControllerSystemEventHandler(eSystemEventType systemEventType)
        {
            switch (systemEventType)
            {
                case (eSystemEventType.DiskInserted):
                    //Removable media was detected on the system
                    break;
                case (eSystemEventType.DiskRemoved):
                    //Removable media was detached from the system
                    break;
                case (eSystemEventType.Rebooting):
                    //The system is rebooting. 
                    //Very limited time to preform clean up and save any settings to disk.
                    break;
            }

        }
        public object SystemSetup()
        {
            this.config = new Configuration.ConfigManager();
            this.quickActionConfig = new QuickConfiguration.QuickConfigManager();


            if (this.config.ReadConfig(@"\nvram\ACSconfig.json", true))
            {
                CrestronConsole.PrintLine("read config - starting manager");
                this.manager = new SystemManager(this.config.RoomConfig, this);
                CrestronConsole.PrintLine("read config ok");
                //updateRoomAVConfig();
            }
            else
            {
                ErrorLog.Error("Unable to read config!!!!");
                CrestronConsole.PrintLine("unable to read config");
            }
            if (this.quickActionConfig.ReadConfig(@"\nvram\quickActionConfig.json", false))
            {
                this.quickActionManager = new QuickSystemManager(this.quickActionConfig.QuickConfig, this);
                CrestronConsole.PrintLine("read quick actions ok");
            }

            return null;
        }
        //THIS IS NOT CURRENTLY BEING USED
        public void Update()
        {

            this.config = new Configuration.ConfigManager();
            string configData = string.Empty;
            if (File.Exists(@"\NVRAM\ACSconfig.json"))
            {
                configLock.Enter();

                // Open, read and close the file
                using (StreamReader file = new StreamReader(@"\NVRAM\ACSconfig.json"))
                {
                    configData = file.ReadToEnd();
                    file.Close();
                }

                try
                {
                    // Try to deserialize into a Room object. If this fails, the JSON file is probably malformed
                    //this.RoomConfig = JsonConvert.DeserializeObject<ConfigData.Configuration>(configData);
                    RootObject systemConfig = JsonConvert.DeserializeObject<RootObject>(configData);

                    CrestronConsole.PrintLine("update config file loaded!");
                }
                catch (Exception e)
                {

                    ErrorLog.Error("Exception in reading config file: {0}", e.Message);
                }
                finally
                {
                    configLock.Leave();
                }

            }
        }
        public class Settings
        {
            public List<Keyp> Keypads { get; set; }
        }
        public class Keyp
        {
            public ushort PresetMed { get; set; }
            public ushort PresetLow { get; set; }
        }
        public class RootObject
        {
            public Settings Settings { get; set; }
        }
    }
}