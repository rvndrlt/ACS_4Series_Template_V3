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
using Crestron.SimplSharp.CrestronDataStore;
using System.Net;
using System.CodeDom.Compiler;
using Crestron.SimplSharpPro.AudioDistribution;

namespace ACS_4Series_Template_V3
{
    public class ControlSystem : CrestronControlSystem
    {
        //public InternalRFExGateway gway;
        //public ThreeSeriesTcpIpEthernetIntersystemCommunications 
        public EthernetIntersystemCommunications roomSelectEISC, subsystemEISC, musicEISC1, musicEISC2, musicEISC3, videoEISC1, videoEISC2, videoEISC3, lightingEISC, HVACEISC, imageEISC;
        public ThreeSeriesTcpIpEthernetIntersystemCommunications VOLUMEEISC;
        private Configuration.ConfigManager config;
        //private QuickConfiguration.QuickConfigManager quickActionConfig;
        private QuickActions.QuickActionXML quickActionXML;
        //private ConfigData.Configuration RoomConfig;
        private static CCriticalSection configLock = new CCriticalSection();
        public static bool initComplete = false;
        public static bool NAXsystem = false;
        public string[] multis = new string[100];
        public ushort[] volumes = new ushort[100];
        public string[] currentProviders = new string[100];
        public string IPaddress, httpPort, httpsPort;
        public SystemManager manager;
        private readonly uint appID;
        public List<ushort> roomList = new List<ushort>();
        public CTimer NAXoutputChangedTimer;
        public CTimer NAXoffTimer;
        public CTimer SendVolumeAfterMusicPresetTimer;
        public static Timer xtimer;
        public bool RecallMusicPresetTimerBusy = false;
        public bool NAXAllOffBusy = false;
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
                VOLUMEEISC = new ThreeSeriesTcpIpEthernetIntersystemCommunications(0x9C, "127.0.0.2", this);

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
                if (VOLUMEEISC.Register() != eDeviceRegistrationUnRegistrationResponse.Success)
                    ErrorLog.Error("VOLUMEEISC failed registration. Cause: {0}", VOLUMEEISC.RegistrationFailureReason);
                VOLUMEEISC.SigChange += this.Volume_Sigchange;
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
                if (!CrestronConsole.AddNewConsoleCommand(TestingPageNumber, "currentpage", "set the page number for all panels", ConsoleAccessLevelEnum.AccessOperator))
                {
                    ErrorLog.Error("Unable to add 'testingpage' command to console");
                }
                if (!CrestronConsole.AddNewConsoleCommand(ReportHVAC, "reporthvac", "show current temps for all rooms", ConsoleAccessLevelEnum.AccessOperator))
                {
                    ErrorLog.Error("Unable to add 'reporthvac' command to console");
                }
                if (!CrestronConsole.AddNewConsoleCommand(ReportQuickAction, "reportquick", "show sources for quick action", ConsoleAccessLevelEnum.AccessOperator))
                {
                    ErrorLog.Error("Unable to add 'reportquickaction' command to console");
                }
                if (!CrestronConsole.AddNewConsoleCommand(TestWriteXML, "testwrite", "test writing to the xml file", ConsoleAccessLevelEnum.AccessOperator))
                {
                    ErrorLog.Error("Unable to add 'testwrite' command to console");
                }
                if (!CrestronConsole.AddNewConsoleCommand(ReportIP, "reportip", "report ip information", ConsoleAccessLevelEnum.AccessOperator))
                {
                    ErrorLog.Error("Unable to add 'reportip' command to console");
                }
                if (!CrestronConsole.AddNewConsoleCommand(ReportHome, "reporthome", "report home image path", ConsoleAccessLevelEnum.AccessOperator))
                {
                    ErrorLog.Error("Unable to add 'reporthome' command to console");
                }
                if (!CrestronConsole.AddNewConsoleCommand(numFloors, "numFloors", "change the number of floors", ConsoleAccessLevelEnum.AccessOperator))
                {
                    ErrorLog.Error("Unable to add 'numFloors' command to console");
                }
                if (!CrestronConsole.AddNewConsoleCommand(numZones, "numZones", "change the number of zoness", ConsoleAccessLevelEnum.AccessOperator))
                {
                    ErrorLog.Error("Unable to add 'numZones' command to console");
                }
                if (!CrestronConsole.AddNewConsoleCommand(StartupPanel, "startuppanels", "startup the panelse", ConsoleAccessLevelEnum.AccessOperator))
                {
                    ErrorLog.Error("Unable to add 'startuppanels' command to console");
                }
                if (!CrestronConsole.AddNewConsoleCommand(UNSUB, "unsub", "unsubscribe", ConsoleAccessLevelEnum.AccessOperator))
                {
                    ErrorLog.Error("Unable to add 'unsub' command to console");
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
        public void TestingPageNumber(string parms)
        {
            ushort pageNum = 0;
            if (parms == "?")
            {
                foreach (var tp in manager.touchpanelZ)
                {
                    CrestronConsole.PrintLine("TP-{0} page {1}", tp.Value.Number, tp.Value.CurrentPageNumber);
                }
            }
            else { 
                if (parms == "1") { pageNum = 1; }
                else if (parms == "2") { pageNum = 2; }
                foreach (var tp in manager.touchpanelZ) {
                    tp.Value.CurrentPageNumber = pageNum;
                }
                CrestronConsole.PrintLine("set all panels to page {0}", pageNum);
            }
        }
        public void ReportHVAC(string parms) {
            foreach (var rm in manager.RoomZ)
            {
                CrestronConsole.PrintLine("{0} {1}", rm.Value.Name, rm.Value.CurrentTemperature);
            }
        }
        public void UNSUB(string parms)
        {
            manager.touchpanelZ[1].UnsubscribeTouchpanelFromAllVolMuteChanges();        
        }
        public void numFloors(string parms)
        {
            manager.touchpanelZ[1].UserInterface.SmartObjects[3].UShortInput[4].UShortValue = Convert.ToUInt16(parms);
            CrestronConsole.PrintLine("setting floors to {0}", Convert.ToUInt16(parms));
        }
        public void numZones(string parms)
        {
            manager.touchpanelZ[1].UserInterface.SmartObjects[4].UShortInput[1].UShortValue = Convert.ToUInt16(parms);
            manager.touchpanelZ[1].UserInterface.SmartObjects[4].UShortInput[2].UShortValue = Convert.ToUInt16(parms);
            manager.touchpanelZ[1].UserInterface.SmartObjects[4].UShortInput[3].UShortValue = Convert.ToUInt16(parms);

            CrestronConsole.PrintLine("setting zones to {0}", Convert.ToUInt16(parms));
        }
        public void ReportQuickAction(string parms)
        {
            CrestronConsole.PrintLine("quick");
            ushort preset = Convert.ToUInt16(parms);

            CrestronConsole.PrintLine("{0}", quickActionXML.PresetName[preset - 1]);

        }
        public void TestWriteXML(string parms)
        {
            this.quickActionXML.PresetName[2] = Convert.ToString(parms);
            //not implemented
        }
        public void ReportIP(string parms)
        {
            CrestronConsole.PrintLine("ipaddress {0} httpPort {1} httpsPort {2}", IPaddress, httpPort, httpsPort);
        }
        public void ReportHome(string parms)
        {
            CrestronConsole.PrintLine("url {0}", string.Format("https://{0}:{1}/HOME.JPG", IPaddress, httpsPort));
        }
        public bool isThisSubsystemInQuickActionList(string subsystemName)
        {
            bool subsysIsHere = false;
            ushort subsysNumber = 0;
            //get the subsystem number
            foreach (var subsys in manager.SubsystemZ)
            {
                if (subsystemName.ToUpper() == subsys.Value.Name.ToUpper())
                {
                    subsysNumber = subsys.Value.Number;
                }
            }
            //see if that subsystem number is in the included subsystems list
            for (ushort i = 0; i < quickActionXML.NumberOfIncludedSubsystems[quickActionXML.quickActionToRecallOrSave-1]; i++)
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
        void MainsigChangeHandler(GenericBase currentDevice, SigEventArgs args)
        {
            if (args.Event == eSigEvent.UShortChange)
            {
                if (args.Sig.Number <= 100)//select a floor#
                {
                    if (args.Sig.UShortValue <= 100)
                    {
/*                        ushort TPNumber = (ushort)args.Sig.Number;
                        ushort floorButtonNumber = (ushort)args.Sig.UShortValue;

                        SelectFloor(TPNumber, floorButtonNumber);*/
                    }
                }
                else if (args.Sig.Number > 100 && args.Sig.Number < 201)
                {
/*                    ushort TPNumber = (ushort)(args.Sig.Number - 100);
                    manager.touchpanelZ[TPNumber].CurrentPageNumber = 2; // 2 = roomSubsystemList
                    SelectZone((TPNumber), (ushort)args.Sig.UShortValue, true);*/
                }
                else if (args.Sig.Number > 200)
                {
                    //SelectSubsystem((ushort)(args.Sig.Number - 200), (ushort)args.Sig.UShortValue);
                }
            }
            if (args.Event == eSigEvent.StringChange)
            {
                if (args.Sig.Number > 0)
                {
                    //manager.RoomZ[(ushort)args.Sig.Number].Name = args.Sig.StringValue;
                }
            }
            if (args.Event == eSigEvent.BoolChange)
            {
                if (args.Sig.Number <= 100)
                {
/*                    if (args.Sig.BoolValue == true)
                    {
                        //Change room - show the list of rooms 
                        ushort TPNumber = (ushort)args.Sig.Number;

                        //we don't want to make these changes unless it's not an iphone
                        if (!manager.touchpanelZ[TPNumber].Name.ToUpper().Contains("IPHONE"))
                        {
                            imageEISC.BooleanInput[TPNumber].BoolValue = false;//clear "current subsystem is video"
                            manager.touchpanelZ[TPNumber].CurrentSubsystemIsVideo = false;
                            manager.touchpanelZ[TPNumber].subsystemPageFlips(1000);//this shows the list of rooms sub for NON iphone projects
                        }
                        SelectOnlyFloor(TPNumber); //change room button pressed - this is the "<" left arrow button
                        manager.touchpanelZ[TPNumber].CurrentPageNumber = 1;// 1 = roomListPage
                        //update the rooms now playing status text
                        UpdateRoomsPageStatusText(TPNumber);
                    }*/
                }
                else if (args.Sig.Number <= 200)//This checks if the APP is connected locally on the LAN or Remotely
                {
                    ushort TPNumber = (ushort)(args.Sig.Number - 100);
                    manager.touchpanelZ[TPNumber].IsConnectedRemotely = (args.Sig.BoolValue == true) ? true : false;
                }
            }
        }
        void SubsystemSigChangeHandler(GenericBase currentDevice, SigEventArgs args) {
           if (args.Event == eSigEvent.BoolChange && args.Sig.BoolValue == true) 
            {
                /*if (args.Sig.Number > 100 && args.Sig.Number < 200)//home page button was pressedd
                {
                    ushort TPNumber = (ushort)(args.Sig.Number - 100);

                    //this function updates the current page number to home and updates the subsystem list
                    HomeButtonPress(TPNumber);

                }
                else if (args.Sig.Number > 200 && args.Sig.Number < 300)//rooms page button was pressed
                {

                    ushort TPNumber = (ushort)(args.Sig.Number - 200);//
                    RoomButtonPress(TPNumber, false);//room controls page select

                }
                else if (args.Sig.Number > 300 && args.Sig.Number < 400)//arrow back button pressed
                {
                    ushort TPNumber = (ushort)(args.Sig.Number - 300);
                    //show the whole house list of subsystems

                    subsystemEISC.UShortInput[(ushort)(TPNumber + 100)].UShortValue = 0;//flip to page number 0 clears the subsystem page
                    subsystemEISC.UShortInput[(ushort)(TPNumber + 200)].UShortValue = 0;//subsystem equipID 0 disconnects from the subsystem
                }
                else if (args.Sig.Number > 400 && args.Sig.Number < 500)//close X pressed - so now on subsystem list page
                {
                    ushort TPNumber = (ushort)(args.Sig.Number - 400);
                    PressCloseXButton(TPNumber);
                }
                else if (args.Sig.Number > 500 && args.Sig.Number < 600) // rooms page select
                {
                    ushort TPNumber = (ushort)(args.Sig.Number - 500);
                    RoomListButtonPress(TPNumber);
                }*/
                if (args.Sig.Number > 600 && args.Sig.Number <= 700) // panel timed out. 
                {
                    
                    ushort TPNumber = (ushort)(args.Sig.Number - 600);//
                    RoomButtonPress(TPNumber, true);//room controls page select - this is the timed out event
                }
            }
        }
        void Music1SigChangeHandler(GenericBase currentDevice, SigEventArgs args)
        {
            if (args.Event == eSigEvent.BoolChange)
            {
                if (args.Sig.Number <= 300 && args.Sig.Number > 200)//mute
                {
                    ushort zoneNumber = (ushort)(args.Sig.Number - 200);
                    foreach(var room in manager.RoomZ)
                    {
                        if (room.Value.AudioID == zoneNumber)
                        {
                            room.Value.MusicMuted = args.Sig.BoolValue;
                        }
                    }
                    foreach (var TP in manager.touchpanelZ)
                    {
                        if (manager.RoomZ[TP.Value.CurrentRoomNum].AudioID == zoneNumber)
                        {
                            TP.Value.UserInterface.BooleanInput[1009].BoolValue = args.Sig.BoolValue;
                        }
                    }
                }
                else if (args.Sig.BoolValue == true)
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
                    ushort TPNumber = (ushort)args.Sig.Number;
                    ushort asrc = TranslateButtonNumberToASrc((ushort)args.Sig.Number, args.Sig.UShortValue);//get the music source from the button number press
                    ushort currentRoomNum = manager.touchpanelZ[TPNumber].CurrentRoomNum;
                    ushort switcherOutputNum = manager.RoomZ[currentRoomNum].AudioID;

                    SwitcherSelectMusicSource(switcherOutputNum, asrc);//from sigchangehandler
                    PanelSelectMusicSource(TPNumber, asrc);
                    if (asrc == 0)
                    {
                        manager.touchpanelZ[TPNumber].subsystemPageFlips(0);//clear the music subpage. show the subsystem list
                        musicEISC1.UShortInput[(ushort)(TPNumber + 200)].UShortValue = 0;//clear the music source subpage
                        musicEISC3.UShortInput[(ushort)(switcherOutputNum + 100)].UShortValue = 0;//set the volume to 0
                    }
                }
                else if (args.Sig.Number <= 200)
                {
/*                    ushort TPNumber = (ushort)(args.Sig.Number - 100);
                    SelectMusicFloor(TPNumber, args.Sig.UShortValue);*/
                }

                else if (args.Sig.Number > 500 && args.Sig.Number <= 600)
                {
                    if (NAXsystem)
                    {
                        if (!NAXAllOffBusy && !RecallMusicPresetTimerBusy)
                        {
                            ushort switcherOutputNumber = (ushort)(args.Sig.Number - 500);
                            CrestronConsole.PrintLine("nax output changed outputnum{0} value{1}", switcherOutputNumber, args.Sig.UShortValue);
                            NAXOutputSrcChanged(switcherOutputNumber, args.Sig.UShortValue);
                        }
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
                    CrestronConsole.PrintLine("share-num{0} , value{1}", args.Sig.Number, args.Sig.UShortValue);    
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
        
        //write a sig change handler that reports continuous volume changes

        public void Volume_Sigchange(BasicTriList currentDevice, SigEventArgs args)
        {
            CrestronConsole.PrintLine("volume sig change {0} {1} {2}", args.Sig.Number, args.Sig.UShortValue, args.Sig.IsRamping);
            //if (args.Sig is { IsInput: false, Type: eSigType.UShort, Number: 1 })
            if (!args.Sig.IsInput && args.Sig.Type == eSigType.UShort)
            {
                ushort audioID = (ushort)args.Sig.Number;
                ushort roomNumber = 0;
                //find the room that corresponds to the audioID
                foreach (var room in manager.RoomZ)
                {
                    if (room.Value.AudioID == audioID)
                    {
                        roomNumber = room.Value.Number;
                    }
                }
                if (roomNumber > 0) { 
                    if (args.Sig.IsRamping)
                    {
                        manager.RoomZ[roomNumber].MusicVolRamping = true;
                        manager.RoomZ[roomNumber].MusicVolume = args.Sig.UShortValue;
                        manager.touchpanelZ[1].UserInterface.UShortInput[2].CreateRamp(args.Sig.RampingInformation);
                        //_tsw1070.UShortInput[17].CreateRamp(args.Sig.RampingInformation);
                    }
                    else
                    {
                        manager.RoomZ[roomNumber].MusicVolRamping = false;
                        manager.touchpanelZ[1].UserInterface.UShortInput[2].StopRamp();
                    }
                }
            }
        }
        void Music3SigChangeHandler(BasicTriList currentDevice, SigEventArgs args)
        {
            if (args.Sig.Type == eSigType.UShort)
            {

                    ushort switcherOutNum = (ushort)(args.Sig.Number - 100);
                    volumes[switcherOutNum-1] = args.Sig.UShortValue;//this stores the zones current volume
                    CrestronConsole.PrintLine("volume changed {0} {1}", args.Sig.Number, args.Sig.UShortValue);
                    //store the volume in the room object
                    foreach (var room in manager.RoomZ)
                    {
                        if (room.Value.AudioID == switcherOutNum)
                        {
                            room.Value.MusicVolume = args.Sig.UShortValue;
                        }
                    }
                    //update the volume on the touchpanel
                    foreach (var TP in manager.touchpanelZ)
                    {
                        
                        if (manager.RoomZ[TP.Value.CurrentRoomNum].AudioID == switcherOutNum)
                        {
                            TP.Value.UserInterface.UShortInput[2].UShortValue = args.Sig.UShortValue;
                        }
                    }
            }
            else if (args.Event == eSigEvent.BoolChange && args.Sig.BoolValue == true)
            {
                if (args.Sig.Number == 1)
                {
                    RequestMusicSources();
                }
                else if (args.Sig.Number == 2)
                {

                }
                else if (args.Sig.Number == 3)
                {

                }
            }
            else if (args.Event == eSigEvent.StringChange) {
                if (args.Sig.Number == 1) {
                    quickActionXML.newQuickActionPresetName = args.Sig.StringValue;
                    CrestronConsole.PrintLine("-{0}", quickActionXML.newQuickActionPresetName);
                }
                else if (args.Sig.Number > 300) {
                    ushort switcherOutNum = (ushort)(args.Sig.Number - 300);
                    multis[switcherOutNum] = args.Sig.StringValue;
                    NAXZoneMulticastChanged(switcherOutNum, args.Sig.StringValue);
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
                else if (args.Sig.Number <= 200 && args.Sig.BoolValue == true)
                {
                    ushort TPNumber = (ushort)(args.Sig.Number - 100);
                    TurnOffAllDisplays(TPNumber);                
                }
            }
            if (args.Event == eSigEvent.UShortChange)
            {
                if (args.Sig.Number <= 100)//select a video source
                {
                    CrestronConsole.PrintLine("TP-{0} select vsrc{1}", args.Sig.Number, args.Sig.UShortValue);
                    SelectVideoSourceFromTP((ushort)args.Sig.Number, args.Sig.UShortValue);
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
                if (args.Sig.Number <= 100)//not currently used
                {

                }
                else if (args.Sig.Number <= 200)//a video display is being selected
                {
                    ushort TPNumber = (ushort)(args.Sig.Number - 100);
                    ushort ButtonNumber = (ushort)(args.Sig.UShortValue);
                    SelectDisplay(TPNumber, ButtonNumber);
                }
                else if (args.Sig.Number <= 300)//not currently used
                {

                }
                else if (args.Sig.Number <= 400)//not currently used
                { 
                
                }
                else if (args.Sig.Number <= 500)
                {
                    ushort displayNumber = (ushort)(args.Sig.Number - 400);
                    SelectDisplayVideoSource(displayNumber, args.Sig.UShortValue);//display number / button number / this is used if the program needs to turn off a tv or send a source outside of a remote.
                }
            }
        }
        void Video3SigChangeHandler(GenericBase currentDevice, SigEventArgs args) { }
        void ImageSigChangeHandler(GenericBase currentDevice, SigEventArgs args)
        {
            if (args.Event == eSigEvent.UShortChange)
            {
                try
                {

                    if (args.Sig.Number <= 100 && args.Sig.UShortValue > 0)//whole house subsystem select a zone
                    {

                        ushort TPNumber = (ushort)args.Sig.Number;
                        /*ushort subsystemNumber = manager.touchpanelZ[TPNumber].CurrentSubsystemNumber;
                        ushort currentRoomNumber = 0;
                        if (manager.touchpanelZ[TPNumber].WholeHouseRoomList.Count > 0)
                        //if (roomList.Count > 0)
                        {
                            //currentRoomNumber = roomList[args.Sig.UShortValue - 1];
                            currentRoomNumber = manager.touchpanelZ[TPNumber].WholeHouseRoomList[args.Sig.UShortValue - 1];
                            manager.touchpanelZ[TPNumber].CurrentRoomNum = currentRoomNumber;
                            subsystemEISC.StringInput[TPNumber].StringValue = manager.RoomZ[currentRoomNumber].Name;
                        }*/


/*
                        if (subsystemNumber > 0)
                        {
                            manager.touchpanelZ[TPNumber].subsystemPageFlips(manager.SubsystemZ[subsystemNumber].FlipsToPageNumber);
                            if (manager.SubsystemZ[subsystemNumber].EquipID > 99)
                            {
                                subsystemEISC.UShortInput[(ushort)(TPNumber + 200)].UShortValue = (ushort)(manager.SubsystemZ[subsystemNumber].EquipID + TPNumber); //get the equipID for the subsystem
                            }
                            else {
                                subsystemEISC.UShortInput[(ushort)(TPNumber + 200)].UShortValue = (ushort)(manager.SubsystemZ[subsystemNumber].EquipID);
                            }
                        }
                        if (currentRoomNumber > 0)
                        {
                            subsystemEISC.UShortInput[(ushort)((TPNumber - 1) * 10 + 303)].UShortValue = manager.RoomZ[currentRoomNumber].LightsID;
                            subsystemEISC.UShortInput[(ushort)((TPNumber - 1) * 10 + 304)].UShortValue = manager.RoomZ[currentRoomNumber].ShadesID;
                            subsystemEISC.UShortInput[(ushort)((TPNumber - 1) * 10 + 305)].UShortValue = manager.RoomZ[currentRoomNumber].ClimateID;
                        }*/

                    }
                    else if (args.Sig.Number == 101)
                    {

                    }
                    else if (args.Sig.Number == 102) //save quick action
                    {

                        if (args.Sig.UShortValue > 0)
                        {
                            CrestronConsole.PrintLine("preparing to save preset{0}", args.Sig.UShortValue);
                            quickActionXML.saving = true;
                            //clear all the checkmarks because sometimes they're checked even though the fb is low.
                            for (ushort i = 0; i < 100; i++)
                            {
                                imageEISC.BooleanInput[(ushort)(i + 401)].BoolValue = false;//clear all the checkboxes.
                                quickActionXML.climateCheckboxes[i] = false;
                                quickActionXML.musicCheckboxes[i] = false;
                            }
                            quickActionXML.quickActionToRecallOrSave = args.Sig.UShortValue;
                            //pull up the current settings of included subsystems and check the boxes. 
                            quickActionXML.SetQuickActionSubsystemVisibility(); //set the visibility status of the checkbox buttons in the list
                            imageEISC.StringInput[3100].StringValue = quickActionXML.PresetName[quickActionXML.quickActionToRecallOrSave - 1];//send the current name
                        }
                    }
                    else if (args.Sig.Number == 103) //recall quick action
                    {
                        if (args.Sig.UShortValue > 0)
                        {
                            quickActionXML.saving = false;

                            quickActionXML.quickActionToRecallOrSave = args.Sig.UShortValue;
                            quickActionXML.SelectQuickActionToView();
                        }
                    }
                }
                catch (Exception e)
                {
                    ErrorLog.Warn("imagesigchangehandler number {0} value {1} {2}", args.Sig.Number, args.Sig.UShortValue, e.Message);
                }
            }
            else if (args.Event == eSigEvent.BoolChange && args.Sig.BoolValue == true)
            {
                if (args.Sig.Number > 200 && args.Sig.Number < 211) //quick action checkbox toggle
                {
                    ushort idx = (ushort)(args.Sig.Number - 220);
                    SelectQuickActionIncludedSubsystem(idx);
                }
                else if (args.Sig.Number > 210 && args.Sig.Number < 221) //quick action view subsystem
                {
                    ushort idx = (ushort)(args.Sig.Number - 210);
                    quickActionXML.SelectQuickActionSubsystem(idx);
                }
                else if (args.Sig.Number > 220 && args.Sig.Number < 231) //quick action select subsystem from the save menu
                {
                    ushort idx = (ushort)(args.Sig.Number - 220);
                    quickActionXML.SelectSubsystemCurrentStatusToSave(idx);
                }
                else if (args.Sig.Number == 231) //quick action save go
                {
                    quickActionXML.writeSubsystems(quickActionXML.quickActionToRecallOrSave);
                }
                else if (args.Sig.Number == 232) //quick action recall go
                {
                    if (isThisSubsystemInQuickActionList("audio") || isThisSubsystemInQuickActionList("music"))
                    {
                        RecallMusicPreset(quickActionXML.quickActionToRecallOrSave);
                    }
                    if (isThisSubsystemInQuickActionList("climate") || isThisSubsystemInQuickActionList("hvac"))
                    {
                        RecallClimatePreset(quickActionXML.quickActionToRecallOrSave);
                    }
                }
                else if (args.Sig.Number == 233)//cancel pressed
                {
                    quickActionXML.saving = false;
                }

                else if (args.Sig.Number > 400 && args.Sig.Number <= 500) //check boxes to enable a zone to be saved 
                {
                    ushort idx = (ushort)(args.Sig.Number - 400);
                    imageEISC.BooleanInput[(ushort)(idx + 400)].BoolValue = !imageEISC.BooleanInput[(ushort)(idx + 400)].BoolValue; // toggle the button
                    if (quickActionXML.currentSubsysIsMusic)
                    {
                        quickActionXML.musicCheckboxes[idx - 1] = imageEISC.BooleanInput[(ushort)(idx + 400)].BoolValue;
                    }
                    else if (quickActionXML.currentSubsystemIsClimate)
                    {
                        quickActionXML.climateCheckboxes[idx - 1] = imageEISC.BooleanInput[(ushort)(idx + 400)].BoolValue;
                    }
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
            ushort ClimateRoomNumber = 0;
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
                    zoneNumber = (ushort)(args.Sig.Number - 200);
                    function = 3;
                }
                else if (args.Sig.Number <= 400)//auto single setpoint changed
                {
                    zoneNumber = (ushort)(args.Sig.Number - 300);
                    function = 4;
                }
                foreach (var room in manager.RoomZ)
                {
                    if (room.Value.ClimateID == zoneNumber && args.Sig.UShortValue > 44)
                    {
                        ClimateRoomNumber = room.Value.Number;
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
                            case (4):
                                {
                                    room.Value.CurrentAutoSingleSetpoint = args.Sig.UShortValue;
                                    break;
                                }
                            default: break;
                        }
                        if (ClimateRoomNumber > 0) { 
                            if (manager.RoomZ[ClimateRoomNumber].CurrentTemperature > 0) { 
                                UpdateRoomHVACText(ClimateRoomNumber);
                            }
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
                    else if (args.Sig.Number <= 500)
                    {
                        //auto mode is dual setpoint
                        zoneNumber = (ushort)(args.Sig.Number - 400);
                        function = 5;
                    }
                    foreach (var room in manager.RoomZ)
                    {
                        if (room.Value.ClimateID == zoneNumber)
                        {
                            ClimateRoomNumber = room.Value.Number;
                            switch (function)
                            {
                                case (1):
                                    {
                                        room.Value.ClimateMode = "Auto";
                                        room.Value.ClimateModeNumber = 1;
                                        break;
                                    }
                                case (2):
                                    {
                                        room.Value.ClimateMode = "Heat";
                                        room.Value.ClimateModeNumber = 2;
                                        break;
                                    }
                                case (3):
                                    {
                                        room.Value.ClimateMode = "Cool";
                                        room.Value.ClimateModeNumber = 3;
                                        break;
                                    }
                                case (4):
                                    {
                                        room.Value.ClimateMode = "Off";
                                        room.Value.ClimateModeNumber = 4;
                                        break;
                                    }
                                case (5):
                                    {
                                        room.Value.ClimateAutoModeIsSingleSetpoint = true;

                                        break;
                                    }
                                default: break;
                            }
                            if (ClimateRoomNumber > 0)
                            {
                                if (manager.RoomZ[ClimateRoomNumber].CurrentTemperature > 0)
                                {
                                    CrestronConsole.PrintLine("room hvac {0}", ClimateRoomNumber);
                                    UpdateRoomHVACText(ClimateRoomNumber);
                                }
                            }
                        }
                    }

                }
                else if (args.Sig.BoolValue == false)
                {
                    if (args.Sig.Number > 400 && args.Sig.Number <= 500)//auto mode is single setpoint
                    {
                        ushort zoneNumber = (ushort)(args.Sig.Number - 400);
                        foreach (var room in manager.RoomZ)
                        {
                            if (room.Value.ClimateID == zoneNumber)
                            {
                                room.Value.ClimateAutoModeIsSingleSetpoint = false;
                            }
                        }
                    }
                }
            }
            
        }
        //updated to v3 5-31-24
        public void StartupPanel(string parms)
        {
            ushort TPNumber = Convert.ToUInt16(parms);
            
            imageEISC.BooleanInput[(ushort)(TPNumber + 100)].BoolValue = false;//current subsystem is NOT audio
            manager.touchpanelZ[TPNumber].CurrentSubsystemIsAudio = false;
            ushort floorScenarioNum = manager.touchpanelZ[TPNumber].FloorScenario;
            ushort currentRoomNumber = manager.touchpanelZ[TPNumber].CurrentRoomNum;
            manager.touchpanelZ[TPNumber].UserInterface.BooleanInput[11].BoolValue = true;//pulse high to go to home page
            if (manager.touchpanelZ[TPNumber].DontInheritSubsystemScenario == false)
            {
                manager.touchpanelZ[TPNumber].SubSystemScenario = manager.RoomZ[currentRoomNumber].SubSystemScenario;
            }
            //initialize the current floor for the panel since we can't do it when the panel is instantiated as it comes before the floor scenarios.
            manager.touchpanelZ[TPNumber].CurrentFloorNum = manager.FloorScenarioZ[floorScenarioNum].IncludedFloors[0];
            //Update the number of floors, current room number, room name

            //roomSelectEISC.UShortInput[(ushort)(TPNumber)].UShortValue = (ushort)manager.FloorScenarioZ[floorScenarioNum].IncludedFloors.Count;

            manager.touchpanelZ[TPNumber].UserInterface.SmartObjects[3].UShortInput[4].UShortValue = (ushort)manager.FloorScenarioZ[floorScenarioNum].IncludedFloors.Count;
            manager.touchpanelZ[TPNumber].UserInterface.SmartObjects[9].UShortInput[4].UShortValue = (ushort)manager.FloorScenarioZ[floorScenarioNum].IncludedFloors.Count;//music page
            subsystemEISC.UShortInput[(ushort)((TPNumber - 1) * 10 + 301)].UShortValue = manager.RoomZ[currentRoomNumber].AudioID;
            UpdateSubsystems(TPNumber);//from startup panels
            
            UpdateTPVideoMenu(TPNumber);//from startup panels
            
            ushort asrcScenarioNum = manager.RoomZ[currentRoomNumber].AudioSrcScenario;
            manager.touchpanelZ[TPNumber].UserInterface.BooleanInput[11].BoolValue = false; //pulse home low
            //update music sources to select from
            if (asrcScenarioNum > 0)
            {
                ushort numASrcs = (ushort)manager.AudioSrcScenarioZ[asrcScenarioNum].IncludedSources.Count;
                //musicEISC1.UShortInput[(ushort)(TPNumber)].UShortValue = numASrcs;// Number of sources to show
                manager.touchpanelZ[TPNumber].UserInterface.SmartObjects[6].UShortInput[4].UShortValue = numASrcs;// Number of sources to show
                for (ushort i = 0; i < numASrcs; i++)
                {
                    ushort srcNum = manager.AudioSrcScenarioZ[asrcScenarioNum].IncludedSources[i];
                    //musicEISC1.StringInput[(ushort)((TPNumber - 1) * 20 + i + 1)].StringValue = manager.MusicSourceZ[srcNum].Name;
                    manager.touchpanelZ[TPNumber].UserInterface.SmartObjects[6].StringInput[(ushort)(i+11)].StringValue = manager.MusicSourceZ[srcNum].Name;
                    if (manager.touchpanelZ[TPNumber].HTML_UI) { musicEISC1.StringInput[(ushort)((TPNumber - 1) * 20 + i + 2001)].StringValue = manager.MusicSourceZ[srcNum].IconHTML; }
                    else { 
                        //musicEISC1.StringInput[(ushort)((TPNumber - 1) * 20 + i + 2001)].StringValue = manager.MusicSourceZ[srcNum].IconSerial;
                        manager.touchpanelZ[TPNumber].UserInterface.SmartObjects[6].StringInput[(ushort)(i + 2011)].StringValue = manager.MusicSourceZ[srcNum].IconSerial;
                    }
                    //musicEISC1.UShortInput[(ushort)((TPNumber - 1) * 20 + i + 1001)].UShortValue = manager.MusicSourceZ[srcNum].AnalogModeNumber;
                    if (i < 6 && manager.touchpanelZ[TPNumber].UseAnalogModes) { 
                        manager.touchpanelZ[TPNumber].UserInterface.UShortInput[(ushort)(i+211)].UShortValue = manager.MusicSourceZ[srcNum].AnalogModeNumber;
                    }
                }
            }
            
            else { //musicEISC1.UShortInput[(ushort)(TPNumber + 1)].UShortValue = 0;
                manager.touchpanelZ[TPNumber].UserInterface.SmartObjects[6].UShortInput[4].UShortValue = 0;// no sources to display
            } 
            UpdateTPMusicMenu(TPNumber);
            if (manager.FloorScenarioZ[floorScenarioNum].IncludedFloors.Count > 1)
            {
                //send the floor names
                for (ushort i = 0; i < manager.FloorScenarioZ[floorScenarioNum].IncludedFloors.Count; i++)
                {
                    //calculate EISC string input numbers
                    //ushort stringInputNum = (ushort)((TPNumber - 1) * 10 + i + 1);

                    string floorName = string.Format(@"<FONT size=""26"">{0}</FONT>", manager.Floorz[manager.FloorScenarioZ[floorScenarioNum].IncludedFloors[i]].Name);
                    //roomSelectEISC.StringInput[stringInputNum].StringValue = floorName;
                    manager.touchpanelZ[TPNumber].UserInterface.SmartObjects[3].StringInput[(ushort)(i + 11)].StringValue = floorName;
                    manager.touchpanelZ[TPNumber].UserInterface.SmartObjects[9].StringInput[(ushort)(i + 11)].StringValue = floorName;//music
                }
            }
            else
            {

                ushort currentNumberOfZones = (ushort)manager.Floorz[manager.FloorScenarioZ[floorScenarioNum].IncludedFloors[0]].IncludedRooms.Count;
                //roomSelectEISC.UShortInput[(ushort)(TPNumber + 100)].UShortValue = currentNumberOfZones;
                manager.touchpanelZ[TPNumber].UserInterface.SmartObjects[4].UShortInput[3].UShortValue = currentNumberOfZones;
                CrestronConsole.PrintLine("TP-{0}, number of zones{1}", TPNumber, currentNumberOfZones);
                for (ushort i = 0; i < currentNumberOfZones; i++)
                {
                    ushort stringInputNum = (ushort)((TPNumber - 1) * 50 + i + 1001); //current zone names start at string 1000
                    ushort zoneTemp = manager.Floorz[manager.FloorScenarioZ[floorScenarioNum].IncludedFloors[0]].IncludedRooms[i];
                    //roomSelectEISC.StringInput[stringInputNum].StringValue = manager.RoomZ[zoneTemp].Name;
                    manager.touchpanelZ[TPNumber].UserInterface.SmartObjects[4].StringInput[(ushort)(4 * i + 1)].StringValue = manager.RoomZ[zoneTemp].Name;
                    manager.touchpanelZ[TPNumber].UserInterface.SmartObjects[10].StringInput[(ushort)(3 * i + 1)].StringValue = manager.RoomZ[zoneTemp].Name;//whole house zone list
                }
            }
            UpdateRoomOptions(TPNumber);
            UpdateEquipIDsForSubsystems(TPNumber, currentRoomNumber);
            CrestronConsole.PrintLine("TP-{0} complete!!", (TPNumber));
        }
        public ushort FindOutWhichFloorThisRoomIsOn(ushort TPNumber, ushort roomNumber)
        {
            ushort floorNumber = 0;
            ushort floorScenario = manager.touchpanelZ[TPNumber].FloorScenario;
            for (ushort i = 1; i <= manager.FloorScenarioZ[floorScenario].IncludedFloors.Count; i++)
            {
                if (manager.Floorz[i].IncludedRooms.Contains(roomNumber) && manager.Floorz[i].Name.ToUpper() != "ALL")
                {
                    floorNumber = i;
                }
            }
            return floorNumber;
        }
        public void PressCloseXButton(ushort TPNumber)
        {
            CrestronConsole.PrintLine("TP-{0} closeX page{1}", TPNumber, manager.touchpanelZ[TPNumber].CurrentPageNumber);
            //clear out the music source subpage
            manager.touchpanelZ[TPNumber].musicPageFlips(0);//clear the music page
            manager.touchpanelZ[TPNumber].videoPageFlips(0);//clear the video page
            manager.touchpanelZ[TPNumber].UserInterface.BooleanInput[998].BoolValue = false;//clear the sharing sub
            manager.touchpanelZ[TPNumber].UserInterface.BooleanInput[999].BoolValue = false;//clear the sharing sub
            manager.touchpanelZ[TPNumber].UserInterface.BooleanInput[1002].BoolValue = false;//clear the sharing button fb
            manager.touchpanelZ[TPNumber].SrcSharingButtonFB = false;
            
            manager.touchpanelZ[TPNumber].subsystemPageFlips(0);//clear the subsystem page
            subsystemEISC.UShortInput[(ushort)(TPNumber + 200)].UShortValue = 0;//subsystem equipID 0 disconnects from the subsystem

            //first handle the case of the rooms menu
            if (manager.touchpanelZ[TPNumber].CurrentPageNumber > 0)//the panel is currently not on the HOME page
            {
                manager.touchpanelZ[TPNumber].CurrentPageNumber = 2;//just closed a subystem menu so now were on the subystem list of a room
                //TODO - get rid of this and TP INTERFACE MODULE
                imageEISC.BooleanInput[TPNumber].BoolValue = false;//clear "current subsystem is video"
                manager.touchpanelZ[TPNumber].CurrentSubsystemIsVideo = false;
                imageEISC.BooleanInput[(ushort)(TPNumber + 100)].BoolValue = false;//clear "current subsystem is audio"
                                                                                   //update the current music source
                UpdatePanelSubsystemText(TPNumber);//from close X button
            }
            //then handle the case of the home menu
            else
            {
                //we want to go back to the zone list page
                WholeHouseUpdateZoneList(TPNumber);
                SendSubsystemZonesPageNumber(TPNumber, true);

            }
        }
        public ushort FloorButtonNumberToHighLight(ushort TPNumber, ushort floorNumber)
        {

            ushort floorScenario = 0;
            ushort buttonNumber = 0;

            if (TPNumber > 0 && floorNumber > 0) { 
                floorScenario = manager.touchpanelZ[TPNumber].FloorScenario;
                buttonNumber = (ushort)manager.FloorScenarioZ[floorScenario].IncludedFloors.IndexOf(floorNumber);
            }
            buttonNumber++;
            return buttonNumber;
        }
        /// <summary>
        ///this function gets the current floor number and number of zones and calls the update rooms page status text function
        ///passing 0 to floorbuttonnumber will use the last floor selected by the panel
        /// </summary>
        /// updated to V3 5-30-24
        public void SelectFloor(ushort TPNumber, ushort floorButtonNumber)
        {
            ushort floorScenarioNum = manager.touchpanelZ[TPNumber].FloorScenario;//GET the floor scenario assigned to this panel
            CrestronConsole.PrintLine("TP{0} btn{1} scenario{2}", TPNumber, floorButtonNumber, floorScenarioNum);
            
            //FIRST get the current floor
            ushort currentFloor = 1;
            if (floorButtonNumber > 0)
            {
                currentFloor = this.manager.FloorScenarioZ[floorScenarioNum].IncludedFloors[floorButtonNumber - 1];
            }
            else if (this.manager.touchpanelZ[TPNumber].CurrentFloorNum > 0)
            {
                currentFloor = this.manager.touchpanelZ[TPNumber].CurrentFloorNum;
                
                floorButtonNumber = FloorButtonNumberToHighLight(TPNumber, currentFloor);
            }
            CrestronConsole.PrintLine("current{0}", currentFloor);
            if (manager.FloorScenarioZ[floorScenarioNum].IncludedFloors.Count > 1)
            {
                this.manager.touchpanelZ[TPNumber].CurrentFloorNum = currentFloor; //SET the current floor for this panel
            }
            ushort currentNumberOfZones = (ushort)this.manager.Floorz[currentFloor].IncludedRooms.Count();
            //roomSelectEISC.UShortInput[(ushort)(TPNumber + 100)].UShortValue = currentNumberOfZones;
            manager.touchpanelZ[TPNumber].UserInterface.SmartObjects[4].UShortInput[3].UShortValue = currentNumberOfZones;
            CrestronConsole.PrintLine("currentNumZones{0}", currentNumberOfZones);
            //roomSelectEISC.UShortInput[(ushort)(TPNumber + 200)].UShortValue = floorButtonNumber;//highlight the floor button
            manager.touchpanelZ[TPNumber].floorButtonFB(floorButtonNumber);//highlight the floor button
            UpdateRoomsPageStatusText(TPNumber);
        }

        public void SelectMusicFloor(ushort TPNumber, ushort floorButtonNumber)
        {
            ushort floorScenarioNum = manager.touchpanelZ[TPNumber].FloorScenario;//GET the floor scenario assigned to this panel

            //FIRST get the current floor
            ushort currentFloor = 1;
            if (floorButtonNumber > 0)
            {
                currentFloor = this.manager.FloorScenarioZ[floorScenarioNum].IncludedFloors[floorButtonNumber - 1];
                ushort currentRoomNumber = manager.touchpanelZ[TPNumber].CurrentRoomNum;
                manager.touchpanelZ[TPNumber].CurrentMusicFloorNum = currentFloor;
                //highlight the floor button
                manager.touchpanelZ[TPNumber].musicFloorButtonFB(floorButtonNumber);
                UpdateMusicSharingPage(TPNumber, currentRoomNumber);//from music floor select
            }
        }
        /// <summary>
        /// this updates the image and status text of each room on the list of rooms to select page
        /// </summary>
        /// updated to V3 5-30-24
        public void UpdateRoomsPageStatusText(ushort TPNumber) {
            //update all of the room names and status for the rooms page
            ushort currentNumberOfZones = (ushort)this.manager.Floorz[manager.touchpanelZ[TPNumber].CurrentFloorNum].IncludedRooms.Count();
            for (ushort i = 0; i < currentNumberOfZones; i++) //send the zone names for current floor out to the xsig
            {
                //ushort stringInputNum = (ushort)((TPNumber - 1) * 50 + i + 1001); //current zone names start at string 1000
                ushort zoneTemp = this.manager.Floorz[manager.touchpanelZ[TPNumber].CurrentFloorNum].IncludedRooms[i];
                //room name
                //roomSelectEISC.StringInput[stringInputNum].StringValue = this.manager.RoomZ[zoneTemp].Name;
                manager.touchpanelZ[TPNumber].UserInterface.SmartObjects[4].StringInput[(ushort)(4*i+11)].StringValue = this.manager.RoomZ[zoneTemp].Name;
                //update hvac status
                //ushort eiscPosition = (ushort)(601 + (30 * (TPNumber - 1)) + i);
                string hvacStatusText = GetHVACStatusText(zoneTemp, TPNumber);
                
                //musicEISC3.StringInput[eiscPosition].StringValue = hvacStatusText; //zone status line 1
                manager.touchpanelZ[TPNumber].UserInterface.SmartObjects[4].StringInput[(ushort)(4 * i + 12)].StringValue = hvacStatusText; //zone status line 1
                //update room status text
                //eiscPosition = (ushort)(301 + (30 * (TPNumber - 1)) + i);

                string statusText = manager.RoomZ[zoneTemp].LightStatusText + manager.RoomZ[zoneTemp].VideoStatusText + manager.RoomZ[zoneTemp].MusicStatusText;
                //videoEISC2.StringInput[eiscPosition].StringValue = statusText; //zone status line 2
                manager.touchpanelZ[TPNumber].UserInterface.SmartObjects[4].StringInput[(ushort)(4 * i + 13)].StringValue = statusText; //zone status line 2
                string imagePath = (manager.touchpanelZ[TPNumber].IsConnectedRemotely) ? string.Format("http://{0}:{1}/{2}", manager.ProjectInfoZ[0].DDNSAdress, httpPort, manager.RoomZ[zoneTemp].ImageURL) : string.Format("http://{0}:{1}/{2}", IPaddress, httpPort, manager.RoomZ[zoneTemp].ImageURL);
                //imageEISC.StringInput[(ushort)(30 * (TPNumber - 1) + i + 101)].StringValue = imagePath;
                manager.touchpanelZ[TPNumber].UserInterface.SmartObjects[4].StringInput[(ushort)(4 * i + 14)].StringValue = imagePath;
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

        /// <summary>
        ///this function updates the touchpanel and current room to reflect the current status and configuration of the selected display
        /// </summary>
        /// updated 5-30-24
        public void SelectDisplay(ushort TPNumber, ushort ButtonNumber)
        {
            ushort currentRoomNumber = manager.touchpanelZ[TPNumber].CurrentRoomNum;
            ushort displayNumber = manager.RoomZ[currentRoomNumber].ListOfDisplays[(ButtonNumber-1)];
            ushort videoOutputNumber = manager.VideoDisplayZ[displayNumber].VideoOutputNum;
            manager.RoomZ[currentRoomNumber].CurrentDisplayNumber = displayNumber;
            manager.touchpanelZ[TPNumber].CurrentDisplayNumber = displayNumber;
            manager.RoomZ[currentRoomNumber].VideoOutputNum = videoOutputNumber;
            manager.RoomZ[currentRoomNumber].VideoSrcScenario = manager.VideoDisplayZ[displayNumber].VideoSourceScenario;
            manager.RoomZ[currentRoomNumber].ConfigurationScenario = manager.VideoDisplayZ[displayNumber].VidConfigurationScenario;
            manager.RoomZ[currentRoomNumber].LiftScenario = manager.VideoDisplayZ[displayNumber].LiftScenario;
            manager.RoomZ[currentRoomNumber].FormatScenario = manager.VideoDisplayZ[displayNumber].FormatScenario;
            subsystemEISC.UShortInput[(ushort)((TPNumber - 1) * 10 + 302)].UShortValue = videoOutputNumber; //this changes the equipment crosspoint for the TP to connect to the room
            //videoEISC3.StringInput[(ushort)(TPNumber + 2300)].StringValue = manager.VideoDisplayZ[displayNumber].DisplayName;
            manager.touchpanelZ[TPNumber].UserInterface.StringInput[10].StringValue = manager.VideoDisplayZ[displayNumber].DisplayName;
            manager.RoomZ[currentRoomNumber].CurrentVideoSrc = manager.VideoDisplayZ[displayNumber].CurrentVideoSrc;
            UpdateTPVideoMenu(TPNumber);//from select display
            CrestronConsole.PrintLine("selected {0} out{1}", manager.VideoDisplayZ[displayNumber].DisplayName, manager.VideoDisplayZ[displayNumber].VideoOutputNum);
        }

        //updated 5-30-24
        public void UpdatePanelToMusicZoneOff(ushort TPNumber) {
            //musicEISC2.StringInput[TPNumber].StringValue = "Off";//current source to TP
            manager.touchpanelZ[TPNumber].UserInterface.StringInput[3].StringValue = "Off";//current source to TP
            musicEISC1.UShortInput[(ushort)(TPNumber + 100)].UShortValue = 0;//current asrc number to panel media server and sharing objects
            //musicEISC1.UShortInput[(ushort)(TPNumber + 200)].UShortValue = 0;//current asrc page number to panel
            manager.touchpanelZ[TPNumber].musicPageFlips(0);//clear the music page
            //musicEISC1.UShortInput[(ushort)(TPNumber + 400)].UShortValue = 0;//clear the button feedback
            manager.touchpanelZ[TPNumber].musicButtonFB(0);//clear the button feedback
        }

        //updated 5/30/24
        public void UpdateMusicSharingPage(ushort TPNumber, ushort currentRoomNumber)
        {
            ushort numRooms = 0;
            ushort room = 0;
            ushort flag = 0;
            manager.touchpanelZ[TPNumber].MusicRoomsToShareSourceTo.Clear();
            manager.touchpanelZ[TPNumber].MusicRoomsToShareCheckbox.Clear();

            CrestronConsole.PrintLine("updated music sharing page {0}", TPNumber);
            //Update rooms available to share music sources to
            if (manager.RoomZ[currentRoomNumber].AudioSrcSharingScenario > 0)
            {
                if (manager.RoomZ[currentRoomNumber].CurrentMusicSrc > 0) { 
                    manager.touchpanelZ[TPNumber].UserInterface.BooleanInput[1001].BoolValue = true;//enable the source sharing button
                }
                else { 
                    manager.touchpanelZ[TPNumber].UserInterface.BooleanInput[1001].BoolValue = false;//disable the source sharing button
                }
                
                if (manager.RoomZ[currentRoomNumber].AudioSrcSharingScenario > 50)//this means we're using the floor
                {
                    ushort currentFloor = manager.touchpanelZ[TPNumber].CurrentMusicFloorNum;
                    numRooms = (ushort)this.manager.Floorz[currentFloor].IncludedRooms.Count();
                    //show the sharing menu with floors
                    if (manager.touchpanelZ[TPNumber].CurrentSubsystemIsAudio && manager.touchpanelZ[TPNumber].SrcSharingButtonFB)
                    {
                        manager.touchpanelZ[TPNumber].UserInterface.BooleanInput[998].BoolValue = false;//clear the regular sharing sub 
                        manager.touchpanelZ[TPNumber].UserInterface.BooleanInput[999].BoolValue = true;//show the sharing sub with floors
                    }
                    else { 
                        manager.touchpanelZ[TPNumber].UserInterface.BooleanInput[999].BoolValue = false;
                    }
                    
                    for (ushort i = 0; i < numRooms; i++)
                    {
                        manager.touchpanelZ[TPNumber].UserInterface.SmartObjects[7].BooleanInput[(ushort)(i * 7 + 4011)].BoolValue = false;//clear the checkboxes
                        room = manager.Floorz[currentFloor].IncludedRooms[i];
                        //build a list of rooms to add
                        if (room == currentRoomNumber || manager.RoomZ[room].AudioID == 0) //Skip over this room
                        {
                            flag++;
                            CrestronConsole.PrintLine("flag {0}", manager.RoomZ[room].Name);
                        }
                        else {
                            manager.touchpanelZ[TPNumber].MusicRoomsToShareSourceTo.Add(room);
                            manager.touchpanelZ[TPNumber].MusicRoomsToShareCheckbox.Add(false);
                        }
                    }
                }
                else { //do the regular sharing scenario -- not grouped by floor
                    numRooms = (ushort)manager.AudioSrcSharingScenarioZ[manager.RoomZ[currentRoomNumber].AudioSrcSharingScenario].IncludedZones.Count;
                    //musicEISC1.BooleanInput[(ushort)(TPNumber + 200)].BoolValue = false; //don't use the multi floor sharing sub
                    manager.touchpanelZ[TPNumber].UserInterface.BooleanInput[999].BoolValue = false;//don't use the multi floor sharing sub
                    for (ushort i = 0; i < numRooms; i++)
                    {
                        manager.touchpanelZ[TPNumber].UserInterface.SmartObjects[7].BooleanInput[(ushort)(i * 7 + 4011)].BoolValue = false;//clear the checkboxes
                        room = manager.AudioSrcSharingScenarioZ[manager.RoomZ[currentRoomNumber].AudioSrcSharingScenario].IncludedZones[i];
                        if (room == currentRoomNumber) { flag = 1; }
                        else
                        {
                            manager.touchpanelZ[TPNumber].MusicRoomsToShareSourceTo.Add(room);
                            manager.touchpanelZ[TPNumber].MusicRoomsToShareCheckbox.Add(false);
                        }
                    }
                }
                manager.touchpanelZ[TPNumber].UnsubscribeTouchpanelFromAllVolMuteChanges();
                for (ushort j = 0; j < manager.touchpanelZ[TPNumber].MusicRoomsToShareSourceTo.Count; j++)
                {
                    //get the room
                    ushort roomNumber = manager.touchpanelZ[TPNumber].MusicRoomsToShareSourceTo[j];
                    var rm = manager.RoomZ[roomNumber];
                    //populate the room names
                    manager.touchpanelZ[TPNumber].UserInterface.SmartObjects[7].StringInput[(ushort)(2 * j + 11)].StringValue = manager.RoomZ[manager.touchpanelZ[TPNumber].MusicRoomsToShareSourceTo[j]].Name;
                    //get the current music source number of each room in the sharing list
                    ushort currentMusicSource = manager.RoomZ[manager.touchpanelZ[TPNumber].MusicRoomsToShareSourceTo[j]].CurrentMusicSrc;
                    if (currentMusicSource > 0) { 
                        manager.touchpanelZ[TPNumber].UserInterface.SmartObjects[7].StringInput[(ushort)(2 * j + 12)].StringValue = manager.MusicSourceZ[currentMusicSource].Name;
                        manager.touchpanelZ[TPNumber].UserInterface.SmartObjects[7].BooleanInput[(ushort)(7 * j + 4016)].BoolValue = true;//make the volume buttons visible
                        manager.touchpanelZ[TPNumber].UserInterface.SmartObjects[7].UShortInput[(ushort)(1 * j + 11)].UShortValue = manager.RoomZ[manager.touchpanelZ[TPNumber].MusicRoomsToShareSourceTo[j]].MusicVolume;
                        manager.touchpanelZ[TPNumber].UserInterface.SmartObjects[7].BooleanInput[(ushort)(7 * j + 4014)].BoolValue = manager.RoomZ[manager.touchpanelZ[TPNumber].MusicRoomsToShareSourceTo[j]].MusicMuted;
                    }
                    else { 
                        manager.touchpanelZ[TPNumber].UserInterface.SmartObjects[7].StringInput[(ushort)(2 * j + 12)].StringValue = "Off";
                        manager.touchpanelZ[TPNumber].UserInterface.SmartObjects[7].BooleanInput[(ushort)(7 * j + 4016)].BoolValue = false;//make the volume buttons hidden
                    }
                    //subscribe to mute change events
                    SubscribeToVolMuteChange(rm, manager.touchpanelZ[TPNumber], j);
                }
                manager.touchpanelZ[TPNumber].UserInterface.SmartObjects[7].UShortInput[3].UShortValue = (ushort)manager.touchpanelZ[TPNumber].MusicRoomsToShareSourceTo.Count;//number of rooms available to share to
            }
            else
            {
                //musicEISC2.BooleanInput[(ushort)(TPNumber)].BoolValue = false;//clear the source sharing button
                manager.touchpanelZ[TPNumber].UserInterface.BooleanInput[1001].BoolValue = false;//clear the source sharing button
            }
        }
        private void MusicMuteChange(UI.TouchpanelUI touchpanel, int index, bool isMuted)
        {
            touchpanel.UserInterface.SmartObjects[7].BooleanInput[(ushort)(7 * index + 4014)].BoolValue = isMuted;
        }
        private void MusicVolumeChange(UI.TouchpanelUI touchpanel, int index, ushort volume)
        {
            touchpanel.UserInterface.SmartObjects[7].UShortInput[(ushort)(1 * index + 11)].UShortValue = volume;
        }
        private void SubscribeToVolMuteChange(Room.RoomConfig room, UI.TouchpanelUI touchpanel, int index)
        {
            EventHandler muteHandler = (sender, e) => MusicMuteChange(touchpanel, index, room.MusicMuted);
            EventHandler volumeHandler = (sender, e) => MusicVolumeChange(touchpanel, index, room.MusicVolume);
            room.MusicMutedChanged += muteHandler;
            room.MusicVolumeChanged += volumeHandler;
            touchpanel.MuteChangeHandlers[room] = muteHandler;
            touchpanel.VolumeChangeHandlers[room] = volumeHandler;
        }

        //updated to V3 5-29-24
        public void SelectZone(ushort TPNumber, ushort zoneListButtonNumber, bool selectDefaultSubsystem)
        {
            imageEISC.BooleanInput[(ushort)(TPNumber + 100)].BoolValue = false;//current subsystem is NOT audio
            imageEISC.BooleanInput[TPNumber].BoolValue = false;//current subsystem is NOT video
            manager.touchpanelZ[TPNumber].CurrentSubsystemIsAudio = false;
            manager.touchpanelZ[TPNumber].CurrentSubsystemIsVideo = false;
            ushort currentRoomNumber = 0;

            //get the current room number from the button press
            if (zoneListButtonNumber > 0)
            {
                currentRoomNumber = manager.Floorz[manager.touchpanelZ[TPNumber].CurrentFloorNum].IncludedRooms[zoneListButtonNumber - 1];
                manager.touchpanelZ[TPNumber].CurrentRoomNum = currentRoomNumber;//assign the room to the panel
            }
            if (currentRoomNumber > 0) { 
                //if the panel isn't assigned to a display then update it
                if (manager.RoomZ[currentRoomNumber].CurrentDisplayNumber > 0)
                {
                    manager.touchpanelZ[TPNumber].CurrentDisplayNumber = manager.RoomZ[currentRoomNumber].CurrentDisplayNumber;//update which display the panel is controlling
                }
                //Update current subsystem scenario number to the panel
                if (!manager.touchpanelZ[TPNumber].DontInheritSubsystemScenario)
                {
                    manager.touchpanelZ[TPNumber].SubSystemScenario = manager.RoomZ[currentRoomNumber].SubSystemScenario;
                }
                //update the current music source when selecting a zone.
                ushort currentMusicSource = manager.RoomZ[currentRoomNumber].CurrentMusicSrc;
            
                if (currentMusicSource > 0) { 
                    //musicEISC2.StringInput[TPNumber].StringValue = manager.MusicSourceZ[currentMusicSource].Name;//current source to TP
                    manager.touchpanelZ[TPNumber].UserInterface.StringInput[3].StringValue = manager.MusicSourceZ[currentMusicSource].Name;//current source to TP
                }
                //update the mute status
                manager.touchpanelZ[TPNumber].UserInterface.BooleanInput[1009].BoolValue = manager.RoomZ[currentRoomNumber].MusicMuted;
                //update the volume bar
                manager.touchpanelZ[TPNumber].UserInterface.UShortInput[2].UShortValue = manager.RoomZ[currentRoomNumber].MusicVolume;
                //Update eisc with subsystem names and icons for current panel
                UpdateSubsystems(TPNumber);//from SelectZone
                UpdateEquipIDsForSubsystems(TPNumber, currentRoomNumber);
            
                //Update current room image
                string imagePath = (manager.touchpanelZ[TPNumber].IsConnectedRemotely) ? string.Format("http://{0}:{1}/{2}", manager.ProjectInfoZ[0].DDNSAdress, httpPort, manager.RoomZ[currentRoomNumber].ImageURL) : string.Format("http://{0}:{1}/{2}", IPaddress, httpPort, manager.RoomZ[currentRoomNumber].ImageURL);
                //imageEISC.StringInput[(ushort)(TPNumber)].StringValue = imagePath;
                manager.touchpanelZ[TPNumber].UserInterface.StringInput[5].StringValue = imagePath;
                //Update A/V Sources available for this room
                ushort asrcScenarioNum = manager.RoomZ[currentRoomNumber].AudioSrcScenario;

                //ushort currentVSRC = manager.RoomZ[currentRoomNumber].CurrentVideoSrc;
                if (currentMusicSource == 0)
                {
                    UpdatePanelToMusicZoneOff(TPNumber);
                }
                //update the MUSIC sources to display for this room
                if (asrcScenarioNum > 0)
                {
                    ushort numASrcs = (ushort)manager.AudioSrcScenarioZ[asrcScenarioNum].IncludedSources.Count;
                    //musicEISC1.UShortInput[(ushort)(TPNumber)].UShortValue = numASrcs;// Number of sources to show
                    manager.touchpanelZ[TPNumber].UserInterface.SmartObjects[6].UShortInput[4].UShortValue = numASrcs;//number of sources to show
                    bool useAnalogModes = manager.touchpanelZ[TPNumber].UseAnalogModes;
                    if (useAnalogModes && numASrcs > 6) { 
                        //musicEISC1.UShortInput[(ushort)(TPNumber)].UShortValue = 6;
                        for (ushort i = 0; i <6; i++) { 
                            manager.touchpanelZ[TPNumber].UserInterface.BooleanInput[(ushort)(541+i)].BoolValue = true;
                        }
                    }
                    for (ushort i = 0; i < numASrcs; i++)
                    {
                        ushort srcNum = manager.AudioSrcScenarioZ[asrcScenarioNum].IncludedSources[i];
                        //musicEISC1.StringInput[(ushort)((TPNumber - 1) * 20 + i + 1)].StringValue = manager.MusicSourceZ[srcNum].Name;
                        manager.touchpanelZ[TPNumber].UserInterface.SmartObjects[6].StringInput[(ushort)(i+1)].StringValue = manager.MusicSourceZ[srcNum].Name;
                        if (manager.touchpanelZ[TPNumber].HTML_UI) { musicEISC1.StringInput[(ushort)((TPNumber - 1) * 20 + i + 2001)].StringValue = manager.MusicSourceZ[srcNum].IconHTML; }
                        else { 
                            //musicEISC1.StringInput[(ushort)((TPNumber - 1) * 20 + i + 2001)].StringValue = manager.MusicSourceZ[srcNum].IconSerial;
                            manager.touchpanelZ[TPNumber].UserInterface.SmartObjects[6].StringInput[(ushort)(i + 21)].StringValue = manager.MusicSourceZ[srcNum].IconSerial;
                        }
                        //musicEISC1.UShortInput[(ushort)((TPNumber - 1) * 20 + i + 1001)].UShortValue = manager.MusicSourceZ[srcNum].AnalogModeNumber;
                        if (useAnalogModes)
                        { 
                            manager.touchpanelZ[TPNumber].UserInterface.UShortInput[(ushort)(i + 211)].UShortValue = manager.MusicSourceZ[srcNum].AnalogModeNumber; 
                        }
                        //Update the current audio source of this room to the panel and highlight the appropriate button
                        if (srcNum == currentMusicSource)
                        {
                            //musicEISC1.UShortInput[(ushort)(TPNumber + 400)].UShortValue = (ushort)(i + 1);//i+1 = button number to highlight
                            manager.touchpanelZ[TPNumber].musicButtonFB((ushort)(i + 1));//music source button to highlight
                            //musicEISC1.UShortInput[(ushort)(TPNumber + 200)].UShortValue = manager.MusicSourceZ[srcNum].FlipsToPageNumber;//page to show
                            manager.touchpanelZ[TPNumber].musicPageFlips(manager.MusicSourceZ[srcNum].FlipsToPageNumber);//page to show
                        }
                    }
                }

            
                //do the same for the video sources
                CrestronConsole.PrintLine("current room {0} vsrcscenario {1}", manager.RoomZ[currentRoomNumber].Name, manager.RoomZ[currentRoomNumber].VideoSrcScenario);
                if (manager.RoomZ[currentRoomNumber].VideoSrcScenario > 0) { 
                    UpdateTPVideoMenu(TPNumber);//from select zone
                }
                UpdateRoomOptions(TPNumber);
                UpdateDisplaysAvailableForSelection(TPNumber, currentRoomNumber);

                UpdateMusicSharingPage(TPNumber, currentRoomNumber);//from select zone
                //enable or disable the vol feedback for dist audio
                ushort configScenario = manager.RoomZ[currentRoomNumber].ConfigurationScenario;
                //if has receiver AND music through receiver AND receiver has NO vol feedback
                //this is for the music menu. it is not currently being used.
                if (configScenario > 0 && manager.VideoConfigScenarioZ[configScenario].HasReceiver && manager.VideoConfigScenarioZ[configScenario].MusicThroughReceiver > 0 && !manager.VideoConfigScenarioZ[configScenario].ReceiverHasVolFB)
                {
                    //musicEISC3.BooleanInput[TPNumber].BoolValue = false;
                    manager.touchpanelZ[TPNumber].UserInterface.BooleanInput[1000].BoolValue = false;//disable the vol feedback
                }
                else 
                {
                    //musicEISC3.BooleanInput[TPNumber].BoolValue = true;//enable the vol feedback
                    manager.touchpanelZ[TPNumber].UserInterface.BooleanInput[1000].BoolValue = true;//enable the vol feedback
                }
                UpdateTPMusicMenu((ushort)(TPNumber));
                UpdatePanelHVACTextInSubsystemList(TPNumber);
                UpdatePanelSubsystemText(TPNumber);//from zone select

            
                //this was requested by clarfield. not applicable to most projects. just write 0 for openSubsysNumOnRmSelect.
                ushort flipToSubsysNumOnSelect = manager.RoomZ[currentRoomNumber].OpenSubsysNumOnRmSelect;
                ushort currentSubsystemScenario = manager.RoomZ[currentRoomNumber].SubSystemScenario;
                //CrestronConsole.PrintLine("{0} flipto {1}", manager.RoomZ[currentRoomNumber].Name, flipToSubsysNumOnSelect);
                if (selectDefaultSubsystem && manager.SubsystemScenarioZ[currentSubsystemScenario].IncludedSubsystems.Contains(flipToSubsysNumOnSelect) && manager.touchpanelZ[TPNumber].Type.ToUpper() != "CRESTRONAPP" && manager.touchpanelZ[TPNumber].Type.ToUpper() != "TSR310")
                {
                    SelectSubsystemPage(TPNumber, flipToSubsysNumOnSelect);//from selectZone
                }
            }
        }
        //updated to V3 5-29-24
        public void UpdateDisplaysAvailableForSelection(ushort TPNumber, ushort currentRoomNumber)
        {
            //if the room has multiple displays enable the change display button
            if (manager.RoomZ[currentRoomNumber].NumberOfDisplays > 1)
            {
                //videoEISC3.BooleanInput[(ushort)(TPNumber + 700)].BoolValue = true;//enable the change display button
                manager.touchpanelZ[TPNumber].UserInterface.BooleanInput[350].BoolValue = true;//enable the change display button
                //videoEISC2.UShortInput[(ushort)(TPNumber + 100)].UShortValue = manager.RoomZ[currentRoomNumber].NumberOfDisplays;
                manager.touchpanelZ[TPNumber].UserInterface.SmartObjects[34].UShortInput[4].UShortValue = manager.RoomZ[currentRoomNumber].NumberOfDisplays;
                ushort i = 1;
                foreach (var display in manager.VideoDisplayZ)
                {
                    if (display.Value.AssignedToRoomNum == currentRoomNumber)
                    {
                        ushort eiscPosition = (ushort)((TPNumber-1) * 10 + 2400 + i);
                        //videoEISC3.StringInput[eiscPosition].StringValue = display.Value.DisplayName;
                        manager.touchpanelZ[TPNumber].UserInterface.SmartObjects[34].StringInput[i].StringValue = display.Value.DisplayName;
                        i++;    
                    }
                }
                if (manager.RoomZ[currentRoomNumber].CurrentDisplayNumber == 0)
                {
                    SelectDisplay(TPNumber, 1);//default to first display
                }
                else
                {
                    //videoEISC3.StringInput[(ushort)(TPNumber + 2300)].StringValue = manager.VideoDisplayZ[manager.RoomZ[currentRoomNumber].CurrentDisplayNumber].DisplayName;
                    manager.touchpanelZ[TPNumber].UserInterface.StringInput[10].StringValue = manager.VideoDisplayZ[manager.RoomZ[currentRoomNumber].CurrentDisplayNumber].DisplayName;
                }

            }
            else { //videoEISC3.BooleanInput[(ushort)(TPNumber + 700)].BoolValue = false;
                manager.touchpanelZ[TPNumber].UserInterface.BooleanInput[350].BoolValue = false;//remove the change display button
            }//remove the change display button
        }

        //this function returns the index of the subsystem in the wholeHouseSubsystem scenarios list of rooms
        public ushort GetWholeHouseSubsystemIndex(ushort TPNumber) {
            ushort index = 0;
            ushort wholeHouseScenarioNum = manager.touchpanelZ[TPNumber].HomePageScenario;
            ushort subsystemNumber = manager.touchpanelZ[TPNumber].CurrentSubsystemNumber;
            ushort numSubsystems = (ushort)this.config.RoomConfig.WholeHouseSubsystemScenarios[wholeHouseScenarioNum - 1].IncludedSubsystems.Count;
            for (ushort i = 0; i < numSubsystems; i++)
            {
                if (this.config.RoomConfig.WholeHouseSubsystemScenarios[wholeHouseScenarioNum - 1].IncludedSubsystems[i].subsystemNumber == subsystemNumber)
                { index = i; }
            }

            return index;
        }
        //this function would be called when you are on the home page and select lights or climate which then brings up a list of rooms
        //updated to V3 5-29-24
        public void WholeHouseUpdateZoneList(ushort TPNumber) {
            ushort subsystemNumber = manager.touchpanelZ[TPNumber].CurrentSubsystemNumber;
            ushort wholeHouseScenarioNum = manager.touchpanelZ[TPNumber].HomePageScenario;
            ushort index = GetWholeHouseSubsystemIndex(TPNumber);
            ushort numRooms = (ushort)this.config.RoomConfig.WholeHouseSubsystemScenarios[wholeHouseScenarioNum - 1].IncludedSubsystems[index].IncludedRooms.Count;
            //update the zone list and status for the subsystem
            //figure out which subsystem
            if (manager.SubsystemZ[subsystemNumber].DisplayName.ToUpper() == "LIGHTS" || manager.SubsystemZ[subsystemNumber].DisplayName.ToUpper() == "LIGHTING")
            {

                manager.touchpanelZ[TPNumber].WholeHouseRoomList.Clear();
                //get all rooms that have lights
                ushort i = 0;
                for (ushort j = 0; j < numRooms; j++)
                {
                    ushort roomNumber = (ushort)this.config.RoomConfig.WholeHouseSubsystemScenarios[wholeHouseScenarioNum - 1].IncludedSubsystems[index].IncludedRooms[j];
                    manager.touchpanelZ[TPNumber].WholeHouseRoomList.Add(roomNumber);
                    manager.touchpanelZ[TPNumber].UserInterface.SmartObjects[10].StringInput[(ushort)(3*j + 11)].StringValue = manager.RoomZ[roomNumber].Name;//whole house zone list
                    string statusText = "";
                    if (manager.RoomZ[roomNumber].Name.ToUpper() == "GLOBAL")
                    {
                        statusText = "";
                    }
                    else if (manager.RoomZ[roomNumber].LightsAreOff)
                    {
                        statusText = "Lights are off. ";
                    }
                    else
                    {
                        statusText = "Lights are on. ";
                    }
                    manager.touchpanelZ[TPNumber].UserInterface.SmartObjects[10].StringInput[(ushort)(3 * j + 12)].StringValue = statusText;
                    //send the icon to the zonestatus line2 serial on the WHOLE HOUSE LIST PAGE
                    manager.touchpanelZ[TPNumber].UserInterface.SmartObjects[10].StringInput[(ushort)(3 * j + 13)].StringValue = manager.SubsystemZ[subsystemNumber].IconSerial;
                    i++;
                }
                manager.touchpanelZ[TPNumber].UserInterface.SmartObjects[10].UShortInput[3].UShortValue = numRooms;
            }
            else if (manager.SubsystemZ[subsystemNumber].DisplayName.ToUpper() == "CLIMATE" || manager.SubsystemZ[subsystemNumber].DisplayName.ToUpper() == "HVAC")
            {
                manager.touchpanelZ[TPNumber].WholeHouseRoomList.Clear();
                ushort i = 0;
                for (ushort j = 0; j < numRooms; j++)
                {
                    ushort roomNumber = (ushort)this.config.RoomConfig.WholeHouseSubsystemScenarios[wholeHouseScenarioNum - 1].IncludedSubsystems[index].IncludedRooms[j];
                    manager.touchpanelZ[TPNumber].WholeHouseRoomList.Add(roomNumber);
                    manager.touchpanelZ[TPNumber].UserInterface.SmartObjects[10].StringInput[(ushort)(3 * j + 11)].StringValue = manager.RoomZ[roomNumber].Name;
                    string statusText = GetHVACStatusText(roomNumber, TPNumber);
                    manager.touchpanelZ[TPNumber].UserInterface.SmartObjects[10].StringInput[(ushort)(3 * j + 12)].StringValue = statusText;
                    manager.touchpanelZ[TPNumber].UserInterface.SmartObjects[10].StringInput[(ushort)(3 * j + 13)].StringValue = manager.SubsystemZ[subsystemNumber].IconSerial;
                    i++;
                }
                manager.touchpanelZ[TPNumber].UserInterface.SmartObjects[10].UShortInput[3].UShortValue = i;
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
                    subsystemNumber = this.config.RoomConfig.WholeHouseSubsystemScenarios[homePageScenario-1].IncludedSubsystems[subsystemButtonNumber].subsystemNumber;
                    CrestronConsole.PrintLine("homePageScenario{0} subsystemNumber{1}", homePageScenario, subsystemNumber);
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
                        manager.touchpanelZ[TPNumber].CurrentSubsystemIsVideo = true;
                        manager.touchpanelZ[TPNumber].CurrentSubsystemIsAudio = false;
                        imageEISC.BooleanInput[(ushort)(TPNumber + 100)].BoolValue = false;//current subsystem is NOT audio
                    }
                    else if (subsystemNumber == audioIsSystemNumber)
                    {
                        manager.RoomZ[currentRoomNum].LastSystemVid = false;
                        imageEISC.BooleanInput[(ushort)(TPNumber + 100)].BoolValue = true;//current subsystem is audio
                        imageEISC.BooleanInput[TPNumber].BoolValue = false;//current subsystem is NOT video
                        manager.touchpanelZ[TPNumber].CurrentSubsystemIsVideo = false;
                        manager.touchpanelZ[TPNumber].CurrentSubsystemIsAudio = true;
                        //when selecting the music subsystem, display the current music source page
                        ushort currentMusicSrc = manager.RoomZ[currentRoomNum].CurrentMusicSrc;
                        if (currentMusicSrc > 0)
                        {
                            manager.touchpanelZ[TPNumber].musicPageFlips(manager.MusicSourceZ[currentMusicSrc].FlipsToPageNumber);
                            musicEISC1.UShortInput[(ushort)(TPNumber + 100)].UShortValue = manager.MusicSourceZ[currentMusicSrc].Number;//for the media server object router
                            musicEISC1.UShortInput[(ushort)(TPNumber + 300)].UShortValue = manager.MusicSourceZ[currentMusicSrc].EquipID;
                        }
                    }
                    else
                    {
                        imageEISC.BooleanInput[(ushort)(TPNumber + 100)].BoolValue = false;//current subsystem is NOT audio
                        imageEISC.BooleanInput[TPNumber].BoolValue = false;//current subsystem is NOT video
                        manager.touchpanelZ[TPNumber].CurrentSubsystemIsVideo = false;
                    }
                    manager.touchpanelZ[TPNumber].subsystemPageFlips(manager.SubsystemZ[subsystemNumber].FlipsToPageNumber);
                }
                
                //musicEISC3.StringInput[(ushort)(TPNumber + 200)].StringValue = manager.SubsystemZ[subsystemNumber].DisplayName;
                manager.touchpanelZ[TPNumber].UserInterface.StringInput[4].StringValue = manager.SubsystemZ[subsystemNumber].DisplayName;
                if (manager.SubsystemZ[subsystemNumber].EquipID > 99)
                {
                    subsystemEISC.UShortInput[(ushort)(TPNumber + 200)].UShortValue = (ushort)(manager.SubsystemZ[subsystemNumber].EquipID + TPNumber);
                }
                else {
                    subsystemEISC.UShortInput[(ushort)(TPNumber + 200)].UShortValue = (ushort)(manager.SubsystemZ[subsystemNumber].EquipID);
                }
            }
            else { manager.RoomZ[currentRoomNum].CurrentSubsystem = 0; }//this would be when the home button is pushed
        }

        public void SendSubsystemZonesPageNumber(ushort TPNumber, bool close) {
            
            //this is for when on the home menu we want to display the list of zones
            ushort currentSub = manager.touchpanelZ[TPNumber].CurrentSubsystemNumber;
            if (manager.SubsystemZ[currentSub].DisplayName.ToUpper() == "LIGHTS" || manager.SubsystemZ[currentSub].DisplayName.ToUpper() == "LIGHTING")
            {
                manager.touchpanelZ[TPNumber].subsystemPageFlips(91);
            }
            else if (manager.SubsystemZ[currentSub].DisplayName.ToUpper() == "CLIMATE" || manager.SubsystemZ[currentSub].DisplayName.ToUpper() == "HVAC")
            {
                manager.touchpanelZ[TPNumber].subsystemPageFlips(92);
            }
            else if (manager.SubsystemZ[currentSub].DisplayName.ToUpper() == "SHADES" || manager.SubsystemZ[currentSub].DisplayName.ToUpper() == "DRAPES")
            {
                manager.touchpanelZ[TPNumber].subsystemPageFlips(93);
            }
            else if (close)
            {
                manager.touchpanelZ[TPNumber].subsystemPageFlips(0);
            }
            else {
                manager.touchpanelZ[TPNumber].subsystemPageFlips(manager.SubsystemZ[currentSub].FlipsToPageNumber);
            }

        }

        public ushort TranslateButtonNumberToASrc(ushort TPNumber, ushort sourceButtonNumber) 
        {
            //calculate the source # because source button # isn't the source #
            ushort adjustedButtonNum = sourceButtonNumber;
            ushort currentASRCscenario = manager.RoomZ[manager.touchpanelZ[TPNumber].CurrentRoomNum].AudioSrcScenario;
            ushort srcGroup = manager.touchpanelZ[TPNumber].CurrentASrcGroupNum;
            ushort currentASRC = 0;
            if (srcGroup > 0)
            {
                adjustedButtonNum = (ushort)(sourceButtonNumber + (srcGroup - 1) * 6 - 1);//this is for TSR-310's or panels with groups of 6 sources
            }
            if (sourceButtonNumber > 0)
            {
                //this is the source "number" property from the json file
                currentASRC = manager.AudioSrcScenarioZ[currentASRCscenario].IncludedSources[(ushort)(adjustedButtonNum)];
            }
            return currentASRC;
        }
        //updated to V3
        public void PanelSelectMusicSource(ushort TPNumber, ushort ASRCtoSend)
        {
            //set the current music source for the room
            ushort currentRoom = manager.touchpanelZ[TPNumber].CurrentRoomNum;
            ushort musicSrcScenario = manager.RoomZ[currentRoom].AudioSrcScenario;
            manager.RoomZ[currentRoom].CurrentMusicSrc = ASRCtoSend;
            
            if (ASRCtoSend > 0)
            {
                manager.RoomZ[currentRoom].MusicStatusText = manager.MusicSourceZ[ASRCtoSend].Name + " is playing.";
                manager.touchpanelZ[TPNumber].UserInterface.StringInput[3].StringValue = manager.MusicSourceZ[ASRCtoSend].Name;
                musicEISC1.UShortInput[(ushort)(TPNumber + 100)].UShortValue = manager.MusicSourceZ[ASRCtoSend].Number; //send source number for media server object router
                manager.touchpanelZ[TPNumber].musicPageFlips(manager.MusicSourceZ[ASRCtoSend].FlipsToPageNumber);
                //musicEISC1.UShortInput[(ushort)(TPNumber + 200)].UShortValue = manager.MusicSourceZ[ASRCtoSend].FlipsToPageNumber;

                //highlight the button feedback for the music source
                for (ushort i = 0; i< manager.AudioSrcScenarioZ[musicSrcScenario].IncludedSources.Count; i++)
                {
                    if (manager.AudioSrcScenarioZ[musicSrcScenario].IncludedSources[i] == ASRCtoSend)
                    {
                        manager.touchpanelZ[TPNumber].musicButtonFB((ushort)(i + 1));
                    }
                }
                musicEISC1.UShortInput[(ushort)(TPNumber + 300)].UShortValue = manager.MusicSourceZ[ASRCtoSend].EquipID;
            }
            else 
            {
                
                manager.touchpanelZ[TPNumber].UserInterface.StringInput[3].StringValue = "Off";
                musicEISC1.UShortInput[(ushort)(TPNumber + 100)].UShortValue = 0;//current asrc number
                manager.touchpanelZ[TPNumber].musicPageFlips(0);
                musicEISC1.UShortInput[(ushort)(TPNumber + 300)].UShortValue = 0;//equip ID
                manager.touchpanelZ[TPNumber].musicButtonFB(0);
            }
        }

        /// <summary>
        ///this function sends source=0 to audio switcher output / multicast 0.0.0.0 and "off" to the zone module 
        /// </summary>
        public void SwitcherAudioZoneOff(ushort audioSwitcherOutputNum)
        {
            CrestronConsole.PrintLine("switcheraudiozoneoff {0} ", audioSwitcherOutputNum);
            ushort roomNum = 0;
            if (audioSwitcherOutputNum > 0)
            {
                //get the room number associated with this audio output
                foreach (var room in manager.RoomZ)
                {
                    if (room.Value.AudioID == audioSwitcherOutputNum)
                    {
                        roomNum = room.Value.Number;    
                    }
                }
                ushort vidConfigScenario = manager.RoomZ[roomNum].ConfigurationScenario;
                bool vidVolThroughDistAudio = false;
                if (vidConfigScenario > 0) { 
                    vidVolThroughDistAudio = manager.VideoConfigScenarioZ[vidConfigScenario].VideoVolThroughDistAudio;
                }
                //if vidVolThroughDistAudio then change the current audio source to the current video source
                //this is if TV was on and then they switched to listen to music then turned the music off it should go back to listening to video
                ushort vsrc = manager.RoomZ[roomNum].CurrentVideoSrc;
                if (vidVolThroughDistAudio && vsrc > 0)
                {
                    musicEISC1.UShortInput[(ushort)(audioSwitcherOutputNum + 500)].UShortValue = 17; //
                    musicEISC3.StringInput[(ushort)(audioSwitcherOutputNum + 300)].StringValue = manager.VideoSourceZ[vsrc].MultiCastAddress;
                    multis[audioSwitcherOutputNum] = manager.VideoSourceZ[vsrc].MultiCastAddress; //this is to prevent feedback from going to previous audio source.
                }
                else
                {
                    musicEISC1.UShortInput[(ushort)(audioSwitcherOutputNum + 500)].UShortValue = 0;//to switcher
                    musicEISC3.StringInput[(ushort)(audioSwitcherOutputNum + 300)].StringValue = "0.0.0.0"; //multicast off
                    musicEISC3.StringInput[(ushort)(audioSwitcherOutputNum + 500)].StringValue = "Off";
                    updateQuickActionMusicSource(audioSwitcherOutputNum, "Off");
                    CrestronConsole.PrintLine("{0} {1} audio off", roomNum, manager.RoomZ[roomNum].Name);
                }

            }
        }

        public void SwitcherSelectMusicSource(ushort switcherOutputNum, ushort ASRCtoSend)
        {
            ushort videoConfigurationScenario = 0;
            ushort currentRoomNum = 0;
            //set the current music source for the room
            foreach (var rm in manager.RoomZ)
            {
                if (rm.Value.AudioID == switcherOutputNum)
                {
                    rm.Value.CurrentMusicSrc = ASRCtoSend;
                    currentRoomNum = rm.Value.Number;
                    if (ASRCtoSend > 0) { 
                        rm.Value.MusicStatusText = manager.MusicSourceZ[ASRCtoSend].Name + " is playing. ";
                    }
                    else
                    {
                        rm.Value.MusicStatusText = "";
                    }
                }
            }
            if (ASRCtoSend > 0)
            {
                //calculate whether to select AES67 Stream input 17
                //first get the NAXBoxNumber this source is connected to
                ushort sourceBoxNumber = manager.MusicSourceZ[ASRCtoSend].NaxBoxNumber;
                //then get the current zones box number
                int zoneBoxNumber = ((switcherOutputNum-1) / 8) + 1;
                //if the source is on a different box than the zone, use the stream
                //CrestronConsole.PrintLine("sourceBoxNumber{0} zoneBoxNumber{1}", sourceBoxNumber, zoneBoxNumber);
                if (sourceBoxNumber > 0 && sourceBoxNumber != zoneBoxNumber)//then this is a streaming source
                {
                    musicEISC1.UShortInput[(ushort)(switcherOutputNum + 500)].UShortValue = 17;
                    musicEISC3.StringInput[(ushort)(switcherOutputNum + 300)].StringValue = manager.MusicSourceZ[ASRCtoSend].MultiCastAddress;
                    multis[switcherOutputNum] = manager.MusicSourceZ[ASRCtoSend].MultiCastAddress;
                    CrestronConsole.PrintLine("audio in 17 to out {0} srcNum {1} MULTI {2}", switcherOutputNum, ASRCtoSend, manager.MusicSourceZ[ASRCtoSend].MultiCastAddress);
                }
                //otherwise its on the same box so just use the switcher input number
                else 
                {
                    musicEISC1.UShortInput[(ushort)(switcherOutputNum + 500)].UShortValue = manager.MusicSourceZ[ASRCtoSend].SwitcherInputNumber;//switcher input # to output
                    musicEISC3.StringInput[(ushort)(switcherOutputNum + 300)].StringValue = "0.0.0.0";//clear the multicast address, we're not using streaming
                    CrestronConsole.PrintLine("audio in {1} to out {0} srcNum {2}", switcherOutputNum, manager.MusicSourceZ[ASRCtoSend].SwitcherInputNumber, ASRCtoSend);
                }
                musicEISC3.StringInput[(ushort)(switcherOutputNum + 500)].StringValue = manager.MusicSourceZ[ASRCtoSend].Name;//update the current source to the zone module which also updates the sharing page
                if (manager.MusicSourceZ[ASRCtoSend].StreamingProviderNumber > 0 && manager.MusicSourceZ[ASRCtoSend].SwitcherInputNumber > 8)//this is a streaming source
                {
                    musicEISC1.UShortInput[(ushort)(600 + manager.MusicSourceZ[ASRCtoSend].SwitcherInputNumber - 8)].UShortValue = manager.MusicSourceZ[ASRCtoSend].StreamingProviderNumber;
                }
                ReceiverOnOffFromDistAudio(currentRoomNum, ASRCtoSend); //turn on receiver from switcherselectmusicsource
                //updateMusicSourceInUse(ASRCtoSend, manager.MusicSourceZ[ASRCtoSend].SwitcherInputNumber, switcherOutputNum);
            }
            else 
            {
                
                videoConfigurationScenario = manager.RoomZ[currentRoomNum].ConfigurationScenario;
                CrestronConsole.PrintLine("SwitcherSelectMusicSource ASRCtoSend {0} out{1} config{2}", ASRCtoSend, switcherOutputNum, videoConfigurationScenario);
                if (currentRoomNum > 0 && videoConfigurationScenario > 0 && manager.VideoConfigScenarioZ[videoConfigurationScenario].HasReceiver)
                {
                    //TODO test for current receiver input so you can turn it off only if its listening to music
                    videoEISC1.UShortInput[(ushort)(manager.RoomZ[currentRoomNum].VideoOutputNum + 700)].UShortValue = 0;//receiver input
                }
                SwitcherAudioZoneOff(switcherOutputNum);
                //updateMusicSourceInUse(0, 0, switcherOutputNum);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="actionNumber"></param>
        /// updated to V3 5/29/24
        public void AudioFloorOff(ushort actionNumber) {
            CrestronConsole.PrintLine("STARTING ALL Off {0}:{1}", DateTime.Now.Second, DateTime.Now.Millisecond);
            NAXAllOffBusy = true;
            NAXoffTimer = new CTimer(NAXAllOffCallback, 0, 10000);
            //ha all off
            if (actionNumber == 1)
            {
                CrestronConsole.PrintLine("HA ALL Off");
                for (ushort i = 0; i < 100; i++)
                {
                    //musicEISC1.UShortInput[(ushort)(401 + i)].UShortValue = 0; //clear the asrc button fb
                    musicEISC1.UShortInput[(ushort)(101 + i)].UShortValue = 0; //clear the current arsc number to the Media Player.
                    imageEISC.BooleanInput[(ushort)(101 + i)].BoolValue = false; //current subsystem is not audio for any panel.
                    musicEISC3.UShortInput[(ushort)(101 + i)].UShortValue = 0; //set volume to 0
                    updateQuickActionMusicSource((ushort)(i+1), "Off");
                }
                
                foreach (var tp in manager.touchpanelZ) {
                    tp.Value.CurrentSubsystemIsAudio = false;
                    for (ushort i = 0; i < 20; i++) {
                        tp.Value.UserInterface.SmartObjects[6].BooleanInput[(ushort)(i+ 3)].BoolValue = false;//clear the music source button fb
                    }
                }
                foreach (var room in manager.RoomZ)
                {
                    room.Value.CurrentMusicSrc = 0;
                    room.Value.MusicStatusText = "";
                    musicEISC3.StringInput[(ushort)(room.Value.AudioID + 500)].StringValue = "Off";
                    musicEISC3.StringInput[(ushort)(room.Value.AudioID + 300)].StringValue = "0.0.0.0";
                    ushort config = room.Value.ConfigurationScenario;
                    if (config > 0 && manager.VideoConfigScenarioZ[config].VideoVolThroughDistAudio && room.Value.CurrentVideoSrc > 0)
                    {
                        CrestronConsole.PrintLine("skipping off command for {0}", room.Value.Name);  
                    }
                    else { musicEISC1.UShortInput[(ushort)(room.Value.AudioID + 500)].UShortValue = 0; //current source to switcher
                    }
                    
                    ReceiverOnOffFromDistAudio(room.Value.Number, 0);//from audioflooroff
                }
            }
            else //floor off
            {
                foreach (ushort rmNum in manager.Floorz[(ushort)(actionNumber-1)].IncludedRooms)
                {
                    CrestronConsole.PrintLine("AudioFloorOff {0} {1}", rmNum, manager.RoomZ[rmNum].Name);
                    manager.RoomZ[rmNum].CurrentMusicSrc = 0;
                    manager.RoomZ[rmNum].MusicStatusText = "";
                    musicEISC3.StringInput[(ushort)(manager.RoomZ[rmNum].AudioID + 500)].StringValue = "Off";
                    updateQuickActionMusicSource(manager.RoomZ[rmNum].AudioID, "Off");
                    musicEISC3.StringInput[(ushort)(manager.RoomZ[rmNum].AudioID + 300)].StringValue = "0.0.0.0";
                    SwitcherAudioZoneOff(manager.RoomZ[rmNum].AudioID);
                    ReceiverOnOffFromDistAudio(rmNum, 0);//from audioflooroff
                }
            }
            UpdateAllPanelsTextWhenAudioChanges();//called from AudioFloorOff
            CrestronConsole.PrintLine("FINISHED ALL Off {0}:{1}", DateTime.Now.Second, DateTime.Now.Millisecond);
        }
        /// <summary>
        /// zone number needs to be greater than 0
        /// </summary>
        /// <param name="zoneNumber"></param>
        /// <param name="srcName"></param>
        public void updateQuickActionMusicSource(ushort zoneNumber, string srcName)
        {
            if (zoneNumber > 0) { 
                foreach (var tp in manager.touchpanelZ)
                {
                    tp.Value.UserInterface.SmartObjects[30].StringInput[(ushort)(2 * zoneNumber)].StringValue = srcName;
                }
            }
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

        public void ChangeCurrentSourceWhenAMultiDisplayGoesOff(ushort displayNumber)
        {
            ushort roomNumber = manager.VideoDisplayZ[displayNumber].AssignedToRoomNum;
            ushort newVsrc = 0;
            ushort config = 0;
            ushort newDisplay = 0;
            ushort buttonNum = 0; // this is to send to the selectDisplay function
            CrestronConsole.PrintLine("display {0} turned off, update the current source for this room", displayNumber);
            


            for (ushort i = 0; i < manager.RoomZ[roomNumber].NumberOfDisplays; i++)
            {
                
                ushort tempDisplay = manager.RoomZ[roomNumber].ListOfDisplays[i];
                //if 1 display goes off then the multi-display should clear. or at least not be counted. 
                //exclude the multi-display. it's not technically a display.
                //set it to no source.
                if (manager.VideoDisplayZ[tempDisplay].TieToDisplayNumbers[0] > 0)
                {
                    manager.VideoDisplayZ[tempDisplay].CurrentVideoSrc = 0;
                    manager.VideoDisplayZ[newDisplay].CurrentSourceText = "";
                }
                //find out if theres a display in the room that's still on
                if (manager.VideoDisplayZ[tempDisplay].CurrentVideoSrc > 0)
                {
                    newVsrc = manager.VideoDisplayZ[tempDisplay].CurrentVideoSrc;
                    newDisplay = tempDisplay;
                    config = manager.VideoDisplayZ[tempDisplay].VidConfigurationScenario;
                    buttonNum = (ushort)(i+1);
                }
            }
            if (newVsrc > 0)
            {
                manager.RoomZ[roomNumber].CurrentVideoSrc = newVsrc;//update the current vsrc for the room
                CrestronConsole.PrintLine("new vsrc = {0}", manager.VideoSourceZ[newVsrc].DisplayName);
                manager.RoomZ[roomNumber].VideoStatusText = manager.VideoSourceZ[newVsrc].DisplayName + " is on. ";
                //update the current display for the room
                manager.RoomZ[roomNumber].CurrentDisplayNumber = newDisplay;
                CrestronConsole.PrintLine("starting to update displays - {0}:{1}", DateTime.Now.Second, DateTime.Now.Millisecond);
                foreach (var tp in manager.touchpanelZ)
                {

                    if (tp.Value.CurrentRoomNum == roomNumber)
                    {
                        SelectDisplay(tp.Value.Number, buttonNum);
                    }
                }
                CrestronConsole.PrintLine("ended to update displays - {0}:{1}", DateTime.Now.Second, DateTime.Now.Millisecond);

                //update the audio 
                //this will have to be updated if there are 
                if (manager.VideoConfigScenarioZ[config].VideoVolThroughDistAudio)
                {
                    ushort audioID = manager.RoomZ[roomNumber].AudioID;
                    musicEISC3.StringInput[(ushort)(audioID + 300)].StringValue = manager.VideoSourceZ[newVsrc].MultiCastAddress;
                }
            }
            else
            {
                manager.RoomZ[roomNumber].CurrentVideoSrc = 0;
                manager.RoomZ[roomNumber].VideoStatusText = "";
            }
        }

        public void SelectMultiDisplayVideoSource(ushort displayNumber, ushort sourceButtonNumber)
        {
            ushort numberOfDisplays = (ushort)manager.VideoDisplayZ[displayNumber].TieToDisplayNumbers.Count;
            ushort currentRoomNum = manager.VideoDisplayZ[displayNumber].AssignedToRoomNum;
            ushort vidConfigScenario = manager.VideoDisplayZ[displayNumber].VidConfigurationScenario;
            ushort audioSwitcherOutputNum = manager.RoomZ[currentRoomNum].AudioID;
            ushort vsrcScenario = manager.VideoDisplayZ[displayNumber].VideoSourceScenario;
            ushort currentVSRC = 0;
            CrestronConsole.PrintLine("multidisplayvideosource disp{0} btnnum{1}", displayNumber, sourceButtonNumber);
            if (sourceButtonNumber == 0)
            {
                manager.RoomZ[currentRoomNum].CurrentVideoSrc = 0;
                manager.RoomZ[currentRoomNum].VideoStatusText = "";
                manager.VideoDisplayZ[displayNumber].CurrentVideoSrc = 0;

                if (manager.VideoConfigScenarioZ[vidConfigScenario].VideoVolThroughDistAudio)
                {
                    SwitcherAudioZoneOff(audioSwitcherOutputNum);//turn the audio off
                }
                for (ushort i = 0; i < numberOfDisplays; i++)
                {
                    ushort currentDisplayNumber = manager.VideoDisplayZ[displayNumber].TieToDisplayNumbers[i];
                    ushort videoSwitcherOutputNum = manager.VideoDisplayZ[currentDisplayNumber].VideoOutputNum;

                    videoEISC1.UShortInput[(ushort)(videoSwitcherOutputNum + 600)].UShortValue = 0;//display input
                    videoEISC1.UShortInput[(ushort)(videoSwitcherOutputNum + 700)].UShortValue = 0;//receiver input
                    videoEISC1.UShortInput[(ushort)(videoSwitcherOutputNum + 800)].UShortValue = 0;//alt switcher input
                    videoEISC2.StringInput[(ushort)(videoSwitcherOutputNum + 200)].StringValue = "0.0.0.0"; //clear the NVX multicast address
                    videoEISC2.UShortInput[(ushort)(currentDisplayNumber + 400)].UShortValue = 0;
                    manager.VideoDisplayZ[currentDisplayNumber].CurrentVideoSrc = 0;//clear the current source for the display

                    videoEISC1.UShortInput[(ushort)(videoSwitcherOutputNum + 500)].UShortValue = 0;//this is for the DM. switcher input # to output
                    videoEISC1.UShortInput[(ushort)(videoSwitcherOutputNum + 900)].UShortValue = 0;//this is for the room module - this may be redundant

                }
            }
            //select the source
            else
            {
                //if this room has a receiver and the music is through the receiver then turn the music off
                if (vidConfigScenario > 0 && manager.VideoConfigScenarioZ[vidConfigScenario].HasReceiver && manager.VideoConfigScenarioZ[vidConfigScenario].MusicThroughReceiver > 0)
                {
                    SwitcherAudioZoneOff(audioSwitcherOutputNum);//turn the switcher output off
                }
                //this will work for panels that don't use the 6 per page analog modes because srcGroup will always be 1

                ushort adjustedButtonNum = (ushort)(sourceButtonNumber - 1);//this is for a handheld using analog mode buttons 6 per page and shouldn't affect other panels
                currentVSRC = manager.VideoSrcScenarioZ[vsrcScenario].IncludedSources[adjustedButtonNum];

                //set the current video source for the room
                manager.RoomZ[currentRoomNum].CurrentVideoSrc = currentVSRC;
                manager.VideoDisplayZ[displayNumber].CurrentVideoSrc = currentVSRC;
                for (ushort i = 0; i < numberOfDisplays; i++)
                {
                    ushort currentDisplayNumber = manager.VideoDisplayZ[displayNumber].TieToDisplayNumbers[i];
                    ushort videoSwitcherOutputNum = manager.VideoDisplayZ[currentDisplayNumber].VideoOutputNum;
                    CrestronConsole.PrintLine("vidout{0} to in{1}", videoSwitcherOutputNum, manager.VideoSourceZ[currentVSRC].VidSwitcherInputNumber);
                    //SEND THE SWITCHING COMMANDS
                    videoEISC1.UShortInput[(ushort)(videoSwitcherOutputNum + 600)].UShortValue = manager.VideoSrcScenarioZ[vsrcScenario].DisplayInputs[adjustedButtonNum];
                    videoEISC1.UShortInput[(ushort)(videoSwitcherOutputNum + 700)].UShortValue = manager.VideoSrcScenarioZ[vsrcScenario].ReceiverInputs[adjustedButtonNum];
                    videoEISC1.UShortInput[(ushort)(videoSwitcherOutputNum + 800)].UShortValue = manager.VideoSrcScenarioZ[vsrcScenario].AltSwitcherInputs[adjustedButtonNum];
                    videoEISC2.StringInput[(ushort)(videoSwitcherOutputNum + 200)].StringValue = manager.VideoSourceZ[currentVSRC].StreamLocation;//set the DM NVX Video Source address to subscribe to
                    videoEISC2.UShortInput[(ushort)(currentDisplayNumber + 400)].UShortValue = currentVSRC; //tell the simpl program which source# the display is viewing

                    videoEISC1.UShortInput[(ushort)(videoSwitcherOutputNum + 500)].UShortValue = manager.VideoSourceZ[currentVSRC].VidSwitcherInputNumber;//this is for the DM. switcher input # to output
                    videoEISC1.UShortInput[(ushort)(videoSwitcherOutputNum + 900)].UShortValue = manager.VideoSourceZ[currentVSRC].VidSwitcherInputNumber;//this is for the room module - this may be redundant 
                    UpdateRoomVideoStatusText(videoSwitcherOutputNum, currentVSRC);

                }
                //send multicast address to audio zone if video sound is through distributed audio
                //turn on the NAX stream for audio in this zone
                if (vidConfigScenario > 0 && manager.VideoConfigScenarioZ[vidConfigScenario].VideoVolThroughDistAudio)
                {
                    musicEISC1.UShortInput[(ushort)(audioSwitcherOutputNum + 500)].UShortValue = 17;//to switcher
                    musicEISC3.StringInput[(ushort)(audioSwitcherOutputNum + 300)].StringValue = manager.VideoSourceZ[currentVSRC].MultiCastAddress;
                    multis[audioSwitcherOutputNum] = manager.VideoSourceZ[currentVSRC].MultiCastAddress;
                    manager.RoomZ[currentRoomNum].CurrentMusicSrc = 0;
                    manager.RoomZ[currentRoomNum].MusicStatusText = "";
                }
            }
            
            foreach (var tp in manager.touchpanelZ)
            {
                if (tp.Value.CurrentDisplayNumber == displayNumber)
                {
                    UpdateTPVideoMenu(tp.Value.Number);
                }
            }
        }
        public void SelectDisplayVideoSource(ushort displayNumber, ushort sourceButtonNumber)
        {
            if (displayNumber > 0)
            {
                CrestronConsole.PrintLine("display {0} {1} buttonnum {2}", displayNumber, manager.VideoDisplayZ[displayNumber].DisplayName, sourceButtonNumber);
                ushort videoSwitcherOutputNum = manager.VideoDisplayZ[displayNumber].VideoOutputNum;
                ushort vidConfigScenario = manager.VideoDisplayZ[displayNumber].VidConfigurationScenario;
                ushort currentRoomNum = manager.VideoDisplayZ[displayNumber].AssignedToRoomNum;
                ushort audioSwitcherOutputNum = manager.RoomZ[currentRoomNum].AudioID;
                ushort vsrcScenario = manager.VideoDisplayZ[displayNumber].VideoSourceScenario;

                ushort currentVSRC = 0;
                //OFF
                if (sourceButtonNumber == 0)
                {
                    videoEISC1.UShortInput[(ushort)(videoSwitcherOutputNum + 600)].UShortValue = 0;//display input
                    videoEISC1.UShortInput[(ushort)(videoSwitcherOutputNum + 700)].UShortValue = 0;//receiver input
                    videoEISC1.UShortInput[(ushort)(videoSwitcherOutputNum + 800)].UShortValue = 0;//alt switcher input
                    videoEISC2.StringInput[(ushort)(videoSwitcherOutputNum + 200)].StringValue = "0.0.0.0"; //clear the NVX multicast address
                    videoEISC2.UShortInput[(ushort)(displayNumber + 400)].UShortValue = 0;
                    manager.VideoDisplayZ[displayNumber].CurrentVideoSrc = 0;//clear the current source for the display
                    manager.RoomZ[currentRoomNum].CurrentVideoSrc = 0;
                    manager.RoomZ[currentRoomNum].VideoStatusText = "";
                    manager.VideoDisplayZ[displayNumber].CurrentSourceText = "";
                    //in this case since 1 display is turning off the multi display should no longer be 'ON'
                    if (manager.RoomZ[currentRoomNum].NumberOfDisplays > 1)
                    {
                        //find the multi-display
                        foreach (var disp in manager.VideoDisplayZ)
                        {
                            if (disp.Value.AssignedToRoomNum == currentRoomNum && disp.Value.TieToDisplayNumbers[0] > 0)//found a multi display in this room
                            {
                                disp.Value.CurrentVideoSrc = 0;
                            }
                        }
                    }
                    //turn off the audio or update the current source in the room
                    if (vidConfigScenario > 0)
                    {
                        if (manager.RoomZ[currentRoomNum].NumberOfDisplays == 1 || AreAllDisplaysOffInThisRoom(currentRoomNum))
                        {

                            if (manager.VideoConfigScenarioZ[vidConfigScenario].VideoVolThroughDistAudio)
                            {
                                SwitcherAudioZoneOff(audioSwitcherOutputNum);//turn the audio off
                            }
                        }
                        else
                        {
                            ChangeCurrentSourceWhenAMultiDisplayGoesOff(displayNumber);
                        }
                    }

                }
                //select the source
                else
                {
                    ushort adjustedButtonNum = (ushort)(sourceButtonNumber - 1);//this is for a handheld using analog mode buttons 6 per page and shouldn't affect other panels
                                                                                //if this room has a receiver and the music is through the receiver then turn the music off
                    if (vidConfigScenario > 0 && manager.VideoConfigScenarioZ[vidConfigScenario].HasReceiver && manager.VideoConfigScenarioZ[vidConfigScenario].MusicThroughReceiver > 0)
                    {
                        SwitcherAudioZoneOff(audioSwitcherOutputNum);//turn the switcher output off
                    }
                    //this will work for panels that don't use the 6 per page analog modes because srcGroup will always be 1


                    currentVSRC = manager.VideoSrcScenarioZ[vsrcScenario].IncludedSources[adjustedButtonNum];

                    //set the current video source for the room
                    manager.RoomZ[currentRoomNum].CurrentVideoSrc = currentVSRC;
                    manager.VideoDisplayZ[displayNumber].CurrentVideoSrc = currentVSRC;

                    CrestronConsole.PrintLine("vidout{0} to in{1}", videoSwitcherOutputNum, manager.VideoSourceZ[currentVSRC].VidSwitcherInputNumber);
                    //SEND THE SWITCHING COMMANDS
                    videoEISC1.UShortInput[(ushort)(videoSwitcherOutputNum + 600)].UShortValue = manager.VideoSrcScenarioZ[vsrcScenario].DisplayInputs[adjustedButtonNum];
                    videoEISC1.UShortInput[(ushort)(videoSwitcherOutputNum + 700)].UShortValue = manager.VideoSrcScenarioZ[vsrcScenario].ReceiverInputs[adjustedButtonNum];
                    videoEISC1.UShortInput[(ushort)(videoSwitcherOutputNum + 800)].UShortValue = manager.VideoSrcScenarioZ[vsrcScenario].AltSwitcherInputs[adjustedButtonNum];
                    videoEISC2.StringInput[(ushort)(videoSwitcherOutputNum + 200)].StringValue = manager.VideoSourceZ[currentVSRC].StreamLocation;//set the DM NVX Video Source address to subscribe to
                    videoEISC2.UShortInput[(ushort)(displayNumber + 400)].UShortValue = currentVSRC; //tell the simpl program which source# the display is viewing

                    //send multicast address to audio zone if video sound is through distributed audio
                    //turn on the NAX stream for audio in this zone
                    if (vidConfigScenario > 0 && manager.VideoConfigScenarioZ[vidConfigScenario].VideoVolThroughDistAudio)
                    {
                        musicEISC1.UShortInput[(ushort)(audioSwitcherOutputNum + 500)].UShortValue = 17;//to switcher
                        musicEISC3.StringInput[(ushort)(audioSwitcherOutputNum + 300)].StringValue = manager.VideoSourceZ[currentVSRC].MultiCastAddress;
                        multis[audioSwitcherOutputNum] = manager.VideoSourceZ[currentVSRC].MultiCastAddress;
                        manager.RoomZ[currentRoomNum].CurrentMusicSrc = 0;
                        manager.RoomZ[currentRoomNum].MusicStatusText = "";
                    }
                }

                UpdateRoomVideoStatusText(videoSwitcherOutputNum, currentVSRC);

                if (currentVSRC > 0)
                {
                    videoEISC1.UShortInput[(ushort)(videoSwitcherOutputNum + 500)].UShortValue = manager.VideoSourceZ[currentVSRC].VidSwitcherInputNumber;//this is for the DM. switcher input # to output
                    videoEISC1.UShortInput[(ushort)(videoSwitcherOutputNum + 900)].UShortValue = manager.VideoSourceZ[currentVSRC].VidSwitcherInputNumber;//this is for the room module - this may be redundant 
                }
                else
                {
                    videoEISC1.UShortInput[(ushort)(videoSwitcherOutputNum + 500)].UShortValue = 0;//this is for the DM. switcher input # to output
                    videoEISC1.UShortInput[(ushort)(videoSwitcherOutputNum + 900)].UShortValue = 0;//this is for the room module - this may be redundant
                }
                foreach (var tp in manager.touchpanelZ)
                {
                    if (tp.Value.CurrentDisplayNumber == displayNumber)
                    {
                        UpdateTPVideoMenu(tp.Value.Number);
                    }
                }
            }
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="TPNumber"></param>
        /// <param name="sourceButtonNumber"></param>
        /// updated to V3 5-29-24
        public void SelectVideoSourceFromTP(ushort TPNumber, ushort sourceButtonNumber)
        {
            //calculate the source # because source button # isn't the source #
            ushort currentRoomNum = manager.touchpanelZ[TPNumber].CurrentRoomNum;
            ushort vidConfigScenario;
            ushort audioSwitcherOutputNum = manager.RoomZ[currentRoomNum].AudioID;
            ushort adjustedButtonNum = 0;
            ushort displayNumber = 0;
            
            //default display is for handheld remotes. doesn't apply to ipads etc.
            //this forces a remote to a room. otherwise it's a panel or ipad that could be on any room
            //get the display number and config scenario
            if (manager.touchpanelZ[TPNumber].DefaultDisplay > 0)
            {
                
                displayNumber = manager.touchpanelZ[TPNumber].DefaultDisplay;
                vidConfigScenario = manager.VideoDisplayZ[displayNumber].VidConfigurationScenario;
            }
            else
            {
                displayNumber = manager.RoomZ[currentRoomNum].CurrentDisplayNumber;
                vidConfigScenario = manager.RoomZ[currentRoomNum].ConfigurationScenario;
            }
            CrestronConsole.PrintLine("display {0}", displayNumber);
            ushort srcGroup = manager.touchpanelZ[TPNumber].CurrentVSrcGroupNum;
            imageEISC.BooleanInput[TPNumber].BoolValue = true;//this tells the program that the current subsystem is video for this panel
            manager.touchpanelZ[TPNumber].CurrentSubsystemIsVideo = true;
            //this will work for panels that don't use the 6 per page analog modes because srcGroup will always be 1
            if (srcGroup > 0) { 
                adjustedButtonNum = (ushort)(sourceButtonNumber + (srcGroup - 1) * 6);//this is for a handheld using analog mode buttons 6 per page and shouldn't affect other panels
                CrestronConsole.PrintLine("adjusted {0}", adjustedButtonNum);
            }
            //check if there's a display to track this one.
            if (manager.VideoDisplayZ[displayNumber].TieToDisplayNumbers[0] > 0)
            {
                SelectMultiDisplayVideoSource(displayNumber, adjustedButtonNum);
            }
            else
            {
                SelectDisplayVideoSource(displayNumber, adjustedButtonNum);
            }
            //OFF
            if (sourceButtonNumber == 0)
            {
                manager.touchpanelZ[TPNumber].UserInterface.StringInput[2].StringValue = "Off";

                if (manager.RoomZ[currentRoomNum].NumberOfDisplays == 1)
                {
                    PressCloseXButton(TPNumber);//close the menu when turning off
                }
                else if (AreAllDisplaysOffInThisRoom(currentRoomNum)) 
                {
                    PressCloseXButton(TPNumber);//close the menu when turning off    
                }
            }
            
            //select the source
            else
            {
                //if this room has a receiver and the music is through the receiver then turn the music off
                if (vidConfigScenario > 0 && manager.VideoConfigScenarioZ[vidConfigScenario].HasReceiver && manager.VideoConfigScenarioZ[vidConfigScenario].MusicThroughReceiver > 0)
                {
                    PanelSelectMusicSource(TPNumber, 0);//turn this panels music off
                }
                if (vidConfigScenario > 0 && manager.VideoConfigScenarioZ[vidConfigScenario].VideoVolThroughDistAudio)
                {
                    UpdatePanelToMusicZoneOff(TPNumber);
                }
            }
            UpdateTPVideoMenu(TPNumber);
        }
        public void TurnOffAllDisplays(ushort TPNumber)
        {
            ushort currentRoomNumber = manager.touchpanelZ[TPNumber].CurrentRoomNum;
            manager.RoomZ[currentRoomNumber].CurrentVideoSrc = 0;
            manager.RoomZ[currentRoomNumber].VideoStatusText = "";
            foreach (var display in manager.VideoDisplayZ)
            {
                if (display.Value.AssignedToRoomNum == currentRoomNumber)
                {
                    display.Value.CurrentSourceText = "";
                    display.Value.CurrentVideoSrc = 0;
                    manager.RoomZ[currentRoomNumber].VideoOutputNum = display.Value.VideoOutputNum;
                    SelectVideoSourceFromTP(TPNumber, 0);
                }
            }
        }
        /// <summary>
        /// this just updates the current vsrc ushort # and text for the the display class instance
        /// </summary>
        /// <param name="switcherOutputNum"></param>
        /// <param name="currentVSRC"></param>


        public void UpdateRoomVideoStatusText(ushort switcherOutputNumber, ushort videoSourceNumber)
        {
            foreach (var display in manager.VideoDisplayZ)
            {
                if (display.Value.VideoOutputNum == switcherOutputNumber)
                {
                    ushort roomNum = display.Value.AssignedToRoomNum;
                    display.Value.CurrentVideoSrc = videoSourceNumber;

                    if (videoSourceNumber > 0)
                    {
                        display.Value.CurrentSourceText = manager.VideoSourceZ[videoSourceNumber].DisplayName;
                        manager.RoomZ[roomNum].CurrentVideoSrc = videoSourceNumber;
                        manager.RoomZ[roomNum].VideoStatusText = manager.VideoSourceZ[videoSourceNumber].DisplayName + " is on. ";
                    }
                    else //TURNING OFF
                    {
                        display.Value.CurrentSourceText = "";
                        if (manager.RoomZ[roomNum].NumberOfDisplays < 2)
                        {
                            manager.RoomZ[roomNum].VideoStatusText = "";
                            manager.RoomZ[roomNum].CurrentVideoSrc = 0;
                        }

                    }

                }
            }
        }
        //This function determines which switcher output number to route the current source to. It does not manage turning the zone off
        public void SelectShareSource(ushort TPNumber, ushort zoneButtonNumber)
        {
            //zone button number is 0 based
            try
            {
                //get current room number and current source
                ushort currentRoom = manager.touchpanelZ[TPNumber].CurrentRoomNum;
                if (currentRoom > 0)
                {
                    ushort currentASRC = manager.RoomZ[currentRoom].CurrentMusicSrc;//this is the number in the list of music sources
                    ushort sharingRoomNumber = 0;
                    ushort numRooms = 0;
                    List<ushort> roomList = new List<ushort>();

                    if (manager.RoomZ[currentRoom].AudioSrcSharingScenario > 50)//this means we're using the floor room list
                    {
                        ushort currentFloor = manager.touchpanelZ[TPNumber].CurrentMusicFloorNum;
                        numRooms = (ushort)this.manager.Floorz[currentFloor].IncludedRooms.Count();
                        //build the list of rooms that are not the current room and are part of the music system
                        for (ushort i = 0; i < numRooms; i++)
                        {
                            ushort room = manager.Floorz[currentFloor].IncludedRooms[i];
                            if (room != currentRoom && manager.RoomZ[room].AudioID > 0)
                            {
                                roomList.Add(room);
                            }
                        }
                        
                        sharingRoomNumber = roomList[zoneButtonNumber];
                        SendShareSource(sharingRoomNumber, currentASRC);
                        //also ALL MUSIC OFF - the current zone volume feedback should go to 0 but it doesn't

                    }
                    else //we're using the audioSrcSharingScenario not the floor room list
                    {
                        ushort sharingScenario = manager.RoomZ[currentRoom].AudioSrcSharingScenario;
                        numRooms = (ushort)manager.AudioSrcSharingScenarioZ[sharingScenario].IncludedZones.Count;
                        //if the current room is in the sharing list skip over it
                        for (ushort i = 0; i < numRooms; i++)
                        {
                            if (manager.AudioSrcSharingScenarioZ[sharingScenario].IncludedZones[i] != currentRoom)
                            {
                                roomList.Add(manager.AudioSrcSharingScenarioZ[sharingScenario].IncludedZones[i]);
                            }
                        }
                        sharingRoomNumber = roomList[zoneButtonNumber];
                        SendShareSource(sharingRoomNumber, currentASRC);
                    }
                        
                }
            }
            catch (Exception e)
            {
                ErrorLog.Warn("select share source tpnumber {0} zonebuttonnumber {1} {2} ", TPNumber, zoneButtonNumber, e.Message);
            }
        }
        public void SendShareSource(ushort sharingRoomNumber, ushort ASRCtoSend)
        {
            ushort inputNum = 0;
            string multicastAddress = "0.0.0.0";
            ushort switcherOutputNum = manager.RoomZ[sharingRoomNumber].AudioID;//switcher outputnumber for the room to be shared to.
            if (ASRCtoSend > 0)
            {
                inputNum = manager.MusicSourceZ[ASRCtoSend].SwitcherInputNumber;
                //send the name of the source
                musicEISC3.StringInput[(ushort)(switcherOutputNum + 500)].StringValue = manager.MusicSourceZ[ASRCtoSend].Name;
                //update the SAVE_MUSIC_QUICK_ACTION source name
                foreach (var tp in manager.touchpanelZ)
                {
                    tp.Value.UserInterface.SmartObjects[30].StringInput[(ushort)(2*switcherOutputNum)].StringValue = manager.MusicSourceZ[ASRCtoSend].Name;
                }
            }
            if (NAXsystem && ASRCtoSend > 0)
            {
                int zoneBoxNumber = ((switcherOutputNum - 1) / 8) + 1;
                int srcBoxNumber = manager.MusicSourceZ[ASRCtoSend].NaxBoxNumber;
                if (srcBoxNumber != zoneBoxNumber) //this source will be streamed via multicast 
                {
                    inputNum = 17;
                    multicastAddress = manager.MusicSourceZ[ASRCtoSend].MultiCastAddress;
                }
            }
            musicEISC1.UShortInput[(ushort)(switcherOutputNum + 500)].UShortValue = inputNum;//send the source to switcher
            if (ASRCtoSend > 0)
            {
                //send the multicast address
                if (inputNum == 17) { 
                    musicEISC3.StringInput[(ushort)(switcherOutputNum + 300)].StringValue = multicastAddress;
                }
                //update the room status
                manager.RoomZ[sharingRoomNumber].CurrentMusicSrc = ASRCtoSend;
                manager.RoomZ[sharingRoomNumber].MusicStatusText = manager.MusicSourceZ[ASRCtoSend].Name + " is playing. ";
                CrestronConsole.PrintLine("sharing switcherOutputNum{0} - {1} {2}", switcherOutputNum, manager.MusicSourceZ[ASRCtoSend].Name, manager.RoomZ[sharingRoomNumber].Name);
                ReceiverOnOffFromDistAudio(sharingRoomNumber, ASRCtoSend);//from selectShareSource
            }
        }

        /// <summary>
        /// in the case that there is only 1 floor available, this will select it. otherwise it does nothing
        /// </summary>
        public void SelectOnlyFloor(ushort TPNumber)
        {
            ushort floorScenarioNum = manager.touchpanelZ[TPNumber].FloorScenario;
            if (manager.FloorScenarioZ[floorScenarioNum].IncludedFloors.Count == 1)
            {
                SelectFloor((ushort)(TPNumber), 1);// there's only 1 floor in this scenario so select it
            }
        }
        /// <summary>
        /// this will update a panels flipstopagenumber, equipID of the subsystem and it will highlight the subsystem button
        /// </summary>
        /// <param name="TPNumber"></param>
        /// <param name="SubsystemNumber"></param>
        /// updated to V3 5-29-24
        public void SelectSubsystemPage(ushort TPNumber, ushort SubsystemNumber)
        {
            ushort equipID = manager.SubsystemZ[SubsystemNumber].EquipID;
            ushort currentRoomNumber = manager.touchpanelZ[TPNumber].CurrentRoomNum;
            manager.touchpanelZ[TPNumber].subsystemPageFlips(manager.SubsystemZ[SubsystemNumber].FlipsToPageNumber);
            //if the equipid is 1 or 2 that connects to audio or video. other wise in the 100's its another subsystem.
            if (equipID > 99) { equipID = (ushort)(equipID + TPNumber); }
            subsystemEISC.UShortInput[(ushort)(TPNumber + 200)].UShortValue = equipID;
            //roomSelectEISC.UShortInput[(ushort)(TPNumber + 300)].UShortValue = 1; //highlight the first subsystem button
            manager.RoomZ[currentRoomNumber].CurrentSubsystem = SubsystemNumber; //update the room to the current subsystem
            if (manager.SubsystemZ[SubsystemNumber].Name.ToUpper() == "AUDIO" || manager.SubsystemZ[SubsystemNumber].Name.ToUpper() == "MUSIC")
            {
                imageEISC.BooleanInput[(ushort)(TPNumber + 100)].BoolValue = true;//current subsystem is audio
                manager.touchpanelZ[TPNumber].CurrentSubsystemIsAudio = true;

            }
            else { imageEISC.BooleanInput[(ushort)(TPNumber + 100)].BoolValue = false;
                manager.touchpanelZ[TPNumber].CurrentSubsystemIsAudio = false;
            }
        }
        /// <summary>
        /// updates the room subsystems
        /// </summary>
        /// updated to V3 5-29-24
        public void UpdateSubsystems(ushort TPNumber )
        {
            ushort currentRoomNumber = manager.touchpanelZ[TPNumber].CurrentRoomNum;
            ushort numberOfSubsystems = (ushort)manager.SubsystemScenarioZ[manager.touchpanelZ[TPNumber].SubSystemScenario].IncludedSubsystems.Count;
            ushort currentSubsystemScenario = manager.touchpanelZ[TPNumber].SubSystemScenario;
            ushort flipToSubsysNumOnSelect = manager.RoomZ[currentRoomNumber].OpenSubsysNumOnRmSelect;

            
            if (currentSubsystemScenario == 0) { currentSubsystemScenario = manager.RoomZ[currentRoomNumber].SubSystemScenario; }//inherit from the room if not defined
            ushort homepageScenario = manager.touchpanelZ[TPNumber].HomePageScenario;
            //Update eisc with current room number / name / number of subsystems for current panel
            //subsystemEISC.StringInput[(ushort)(TPNumber)].StringValue = manager.RoomZ[currentRoomNumber].Name;//is this necessary?
            manager.touchpanelZ[TPNumber].UserInterface.StringInput[1].StringValue = manager.RoomZ[currentRoomNumber].Name;
            //subsystemEISC.UShortInput[(ushort)(TPNumber)].UShortValue = (ushort)manager.SubsystemScenarioZ[currentSubsystemScenario].IncludedSubsystems.Count;
            manager.touchpanelZ[TPNumber].UserInterface.SmartObjects[2].UShortInput[3].UShortValue = numberOfSubsystems;//subsystem select
            
            //UPDATE THE PAGE TO DISPLAY
            //if panel has no home page OR only it has 1 subsystem THEN flip to first available subsystem
            if (homepageScenario == 0 || numberOfSubsystems == 1 )
            {
                //flip to first available subsystem
                ushort subsystemNum = manager.SubsystemScenarioZ[currentSubsystemScenario].IncludedSubsystems[0];
                SelectSubsystemPage(TPNumber, subsystemNum);//from UpdateSubsystems only 1 subsystem
            }
            else //otherwise flip to the list of subsystems
            {
                //roomSelectEISC.UShortInput[(ushort)(TPNumber + 300)].UShortValue = 0;//clear the subsystem buttons
                manager.touchpanelZ[TPNumber].subsystemPageFlips(0);//flip to home
            }
            //Update panel with subsystem names and icons for current panel and highlight the appropriate button
            updateSubsystemListSmartObject(TPNumber, false);//from UpdateSubsystems
        }
        /// <summary>
        /// TODO - this hsould probably call the update subysstem status text
        /// </summary>
        /// <param name="TPNumber"></param>
        /// <param name="wholeHouseYes"></param>
        public void updateSubsystemListSmartObject(ushort TPNumber, bool wholeHouseYes)
        {
            ushort homePageScenario = manager.touchpanelZ[TPNumber].HomePageScenario;
            ushort currentSubsystemScenario = manager.touchpanelZ[TPNumber].SubSystemScenario;
            ushort numberOfSubs = 0;
            ushort subsystemNum = 0;
            if (wholeHouseYes)
            {
                numberOfSubs = (ushort)this.config.RoomConfig.WholeHouseSubsystemScenarios[homePageScenario - 1].IncludedSubsystems.Count;
                manager.touchpanelZ[TPNumber].UserInterface.SmartObjects[14].UShortInput[3].UShortValue = numberOfSubs;//whole house subsystem select
                for (ushort i = 0; i < numberOfSubs; i++)
                {
                    subsystemNum = this.config.RoomConfig.WholeHouseSubsystemScenarios[homePageScenario - 1].IncludedSubsystems[i].subsystemNumber;
                    CrestronConsole.PrintLine("{0} {1} {2} {3} {4}", i, subsystemNum, manager.SubsystemZ[subsystemNum].Name, (ushort)(2 * i + 12), manager.SubsystemZ[subsystemNum].IconSerial);
                    manager.touchpanelZ[TPNumber].UserInterface.SmartObjects[14].BooleanInput[(ushort)(i + 4016)].BoolValue = false;//clear the button feedback
                    manager.touchpanelZ[TPNumber].UserInterface.SmartObjects[14].StringInput[(ushort)(2 * i + 11)].StringValue = manager.SubsystemZ[subsystemNum].Name;//whole house subsystem
                    manager.touchpanelZ[TPNumber].UserInterface.SmartObjects[14].StringInput[(ushort)(2 * i + 12)].StringValue = manager.SubsystemZ[subsystemNum].IconSerial;//whole house subsystem icon
                }
            }
            else 
            {
                numberOfSubs = (ushort)manager.SubsystemScenarioZ[currentSubsystemScenario].IncludedSubsystems.Count;
                ushort currentRoomNumber = manager.touchpanelZ[TPNumber].CurrentRoomNum;
                for (ushort i = 0; i < numberOfSubs; i++)
                {
                    subsystemNum = manager.SubsystemScenarioZ[currentSubsystemScenario].IncludedSubsystems[i];
                    manager.touchpanelZ[TPNumber].UserInterface.SmartObjects[2].BooleanInput[(ushort)(i + 4016)].BoolValue = false;//clear the button feedback
                    manager.touchpanelZ[TPNumber].UserInterface.SmartObjects[2].StringInput[(ushort)(3 * i + 11)].StringValue = manager.SubsystemZ[subsystemNum].Name;//subsystem select
                                                                                                                                                                     //show the subsystem icon - HTML or smart graphic
                    if (manager.touchpanelZ[TPNumber].HTML_UI) { subsystemEISC.StringInput[(ushort)((TPNumber - 1) * 20 + i + 2001)].StringValue = manager.SubsystemZ[subsystemNum].IconHTML; }
                    else
                    {
                        manager.touchpanelZ[TPNumber].UserInterface.SmartObjects[2].StringInput[(ushort)(3 * i + 13)].StringValue = manager.SubsystemZ[subsystemNum].IconSerial;//subsystem select
                    }
                    //update the status text

                    //update the button number to highlight
                    if (manager.RoomZ[currentRoomNumber].CurrentSubsystem == subsystemNum)
                    {
                        //roomSelectEISC.UShortInput[(ushort)(TPNumber + 300)].UShortValue = (ushort)(i + 1);
                        manager.touchpanelZ[TPNumber].UserInterface.SmartObjects[2].BooleanInput[(ushort)(i + 4016)].BoolValue = true;
                    }
                }
            }

        }
        /// <summary>
        /// selects the floor and zone that the panel lives in
        /// </summary>
        public void RoomButtonPress(ushort TPNumber, bool TimedOut)
        {
            CrestronConsole.PrintLine("TP-{0} roomButtonPress", TPNumber);
            subsystemEISC.BooleanInput[(ushort)(TPNumber + 200)].BoolValue = true;//DELETE THIS PENDING REMOVAL OF MMV
            manager.touchpanelZ[TPNumber].UserInterface.BooleanInput[12].BoolValue = true;//pulse rooms page select
            ushort currentRoom = 0;
            if (TimedOut) { currentRoom = manager.touchpanelZ[TPNumber].DefaultRoom; }
            else { currentRoom = manager.touchpanelZ[TPNumber].CurrentRoomNum; }
            ushort floorNumber = FindOutWhichFloorThisRoomIsOn(TPNumber, currentRoom);
            //calculate the button # in the zone list the room is
            ushort zoneButtonNumber = (ushort)(manager.Floorz[floorNumber].IncludedRooms.IndexOf(currentRoom) + 1);
            manager.touchpanelZ[TPNumber].CurrentPageNumber = 1;//touchpanel is now on the roomList page / 0 would be the home page
            imageEISC.BooleanInput[(ushort)(TPNumber + 100)].BoolValue = false;//current subsystem is NOT audio
            imageEISC.BooleanInput[TPNumber].BoolValue = false;//current subsystem is NOT video
            manager.touchpanelZ[TPNumber].CurrentSubsystemIsVideo = false;
            manager.touchpanelZ[TPNumber].CurrentFloorNum = floorNumber;

            //selectfloor with 0 will default to the current floor. thats why its set above.
            SelectFloor(TPNumber, 0);//tpnumber, floorbuttonnumber NOT actual floor number
            SelectZone(TPNumber, zoneButtonNumber, TimedOut);// timed out will select the default subsystem
            subsystemEISC.BooleanInput[(ushort)(TPNumber + 200)].BoolValue = false;//DELETE THIS PENDING REMOVAL OF MMV
            manager.touchpanelZ[TPNumber].UserInterface.BooleanInput[12].BoolValue = false;//pulse rooms page select
        }
        /// <summary>
        /// this is the list of floors and rooms page
        /// </summary>
        /// updated to V3 5-29-24 
        public void RoomListButtonPress(ushort TPNumber)
        {
            imageEISC.BooleanInput[(ushort)(TPNumber + 100)].BoolValue = false;//current subsystem is NOT audio
            manager.touchpanelZ[TPNumber].CurrentSubsystemIsAudio = false;
            subsystemEISC.BooleanInput[(ushort)(TPNumber + 200)].BoolValue = true;//pulse rooms page select
                                                                                  //we don't want to make these changes unless it's not an iphone
            manager.touchpanelZ[TPNumber].UserInterface.BooleanInput[12].BoolValue = true;//pulse rooms page select
            if (!manager.touchpanelZ[TPNumber].Name.ToUpper().Contains("IPHONE"))
            {
                imageEISC.BooleanInput[TPNumber].BoolValue = false;//clear "current subsystem is video"
                manager.touchpanelZ[TPNumber].CurrentSubsystemIsVideo = false;
                manager.touchpanelZ[TPNumber].subsystemPageFlips(1000);//1000 is list of rooms sub for flip to page#
            }
            
            //selectfloor with 0 will default to the current floor.
            SelectFloor(TPNumber, 0);//tpnumber, floorbuttonnumber NOT actual floor number
            subsystemEISC.BooleanInput[(ushort)(TPNumber + 200)].BoolValue = false;
            manager.touchpanelZ[TPNumber].UserInterface.BooleanInput[12].BoolValue = false;//pulse rooms page select
        }
        /// <summary>
        /// sets the image / updates the whole house subsystem list
        /// </summary>
        /// this function updated to V3 5-29-24
        public void HomeButtonPress(ushort TPNumber) 
        {
            CrestronConsole.PrintLine("TP-{0} homebuttonpress", TPNumber);
            if (manager.touchpanelZ[TPNumber].Type != "Tsr310" && manager.touchpanelZ[TPNumber].Type != "HR310")
            {
                ushort homePageScenario = manager.touchpanelZ[TPNumber].HomePageScenario; //this refers to the wholehousesubsystemscenario
                string  homeImagePath = (manager.touchpanelZ[TPNumber].IsConnectedRemotely) ? string.Format("http://{0}:{1}/HOME.JPG", manager.ProjectInfoZ[0].DDNSAdress, httpPort) : string.Format("http://{0}:{1}/HOME.JPG", IPaddress, httpPort);
                CrestronConsole.PrintLine("home image ={0}", homeImagePath);
                manager.touchpanelZ[TPNumber].UserInterface.StringInput[5].StringValue = homeImagePath;
                manager.touchpanelZ[TPNumber].UserInterface.BooleanInput[11].BoolValue = true; //pulse high to go to home
                manager.touchpanelZ[TPNumber].subsystemPageFlips(10000);//flip to page# something out of range of used numbers
                imageEISC.BooleanInput[TPNumber].BoolValue = false;//clear "current subsystem is video"
                manager.touchpanelZ[TPNumber].CurrentSubsystemIsVideo = false;
                manager.touchpanelZ[TPNumber].CurrentPageNumber = 0;// 0 = home 
                //TODO the subsystem status text isn't being updated here. maybe it should be???
                CrestronConsole.PrintLine("whole house subsystemscenario length{0}", config.RoomConfig.WholeHouseSubsystemScenarios.Length);
                manager.touchpanelZ[TPNumber].UserInterface.BooleanInput[11].BoolValue = false; //pulse low
                if (homePageScenario > 0 && homePageScenario <= this.config.RoomConfig.WholeHouseSubsystemScenarios.Length) //make sure the homePageScenario isn't out of bounds
                {
                    updateSubsystemListSmartObject(TPNumber, true);//home button press
                }
            }
        }
        /// <summary>
        ///this function updates the list of video sources for the calling touchpanel and if the zone is currently viewing a source updates flipstopagenumber, video source equipID, display name, icon etc.
        /// </summary>
        /// this function has been updated to V3
        public void UpdateTPVideoMenu(ushort TPNumber)
        {
            ushort currentRoomNumber = manager.touchpanelZ[TPNumber].CurrentRoomNum;
            
            if (manager.RoomZ[currentRoomNumber].VideoSrcScenario > 0)
            {
                ushort numSrcs = (ushort)manager.VideoSrcScenarioZ[manager.RoomZ[currentRoomNumber].VideoSrcScenario].IncludedSources.Count;
                //videoEISC1.UShortInput[(ushort)(TPNumber)].UShortValue = numSrcs;
                manager.touchpanelZ[TPNumber].UserInterface.SmartObjects[5].UShortInput[4].UShortValue = numSrcs;
                ushort currentVSRC = manager.RoomZ[currentRoomNumber].CurrentVideoSrc;
                manager.touchpanelZ[TPNumber].CurrentVSrcNum = currentVSRC;
                CrestronConsole.PrintLine("TP-{0} room{1} vsrc{2}", TPNumber, currentRoomNumber, currentVSRC);
                //for tsr-310s  enable more sources button
                if (numSrcs > 6 )
                {
                    manager.touchpanelZ[TPNumber].UserInterface.BooleanInput[509].BoolValue = true; //enable more sources button
                    //videoEISC1.BooleanInput[(ushort)(TPNumber)].BoolValue = true;
                }
                else { 
                    //videoEISC1.BooleanInput[(ushort)(TPNumber)].BoolValue = false;
                    manager.touchpanelZ[TPNumber].UserInterface.BooleanInput[509].BoolValue = false;
                }

                subsystemEISC.UShortInput[(ushort)((TPNumber - 1) * 10 + 302)].UShortValue = manager.RoomZ[currentRoomNumber].VideoOutputNum;//this updates the equipment ID to connect the panel to the room
                if (currentVSRC > 0)
                {
                    //videoEISC1.UShortInput[(ushort)(TPNumber + 200)].UShortValue = manager.VideoSourceZ[currentVSRC].FlipsToPageNumber;
                    manager.touchpanelZ[TPNumber].videoPageFlips(manager.VideoSourceZ[currentVSRC].FlipsToPageNumber);
                    //CrestronConsole.PrintLine("tp{0} page{1}", TPNumber, manager.VideoSourceZ[currentVSRC].FlipsToPageNumber);
                    videoEISC1.UShortInput[(ushort)(TPNumber + 300)].UShortValue = manager.VideoSourceZ[currentVSRC].EquipID;
                    //videoEISC2.StringInput[(ushort)(TPNumber)].StringValue = manager.VideoSourceZ[currentVSRC].DisplayName;
                    manager.touchpanelZ[TPNumber].UserInterface.StringInput[2].StringValue = manager.VideoSourceZ[currentVSRC].DisplayName;
                }
                else
                {
                    //videoEISC1.UShortInput[(ushort)(TPNumber + 200)].UShortValue = 0;//video source page #
                    manager.touchpanelZ[TPNumber].videoPageFlips(0);
                    videoEISC1.UShortInput[(ushort)(TPNumber + 300)].UShortValue = 0;//equip ID
                    //videoEISC2.StringInput[(ushort)(TPNumber)].StringValue = "Off";
                    manager.touchpanelZ[TPNumber].UserInterface.StringInput[2].StringValue = "Off";
                    CrestronConsole.PrintLine("VSRC{0} clear button feedback", currentVSRC);
                    for (ushort i = 0; i<20; i++) { manager.touchpanelZ[TPNumber].UserInterface.SmartObjects[5].BooleanInput[(ushort)(i+3)].BoolValue = false; }//clear the video source button feedback

                }

                //update the video source list and highlight the appropriate button
                //ushort inUse = 0;
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
                            manager.touchpanelZ[TPNumber].UserInterface.BooleanInput[(ushort)(521 + i)].BoolValue = true;
                            //inUse |= (ushort)(1 << i);
                        }//set the bit
                        else
                        {
                            manager.touchpanelZ[TPNumber].UserInterface.BooleanInput[(ushort)(521 + i)].BoolValue = false;
                            //inUse &= (ushort)(~(1 << i));
                        }
                    }
                    //in use analog
                    //videoEISC2.UShortInput[(ushort)(TPNumber)].UShortValue = (ushort)inUse;
                }
                else
                {
                    for (ushort i = 0; i < numSrcs; i++)//loop through all video sources in this scenario
                    {
                        ushort srcNum = manager.VideoSrcScenarioZ[manager.RoomZ[currentRoomNumber].VideoSrcScenario].IncludedSources[i];
                        //update the names
                        manager.touchpanelZ[TPNumber].UserInterface.SmartObjects[5].StringInput[(ushort)(i+11)].StringValue = manager.VideoSourceZ[srcNum].DisplayName;
                        //videoEISC1.StringInput[(ushort)((TPNumber - 1) * 20 + i + 1)].StringValue = manager.VideoSourceZ[srcNum].DisplayName;
                        if (manager.touchpanelZ[TPNumber].HTML_UI) { videoEISC1.StringInput[(ushort)((TPNumber - 1) * 20 + i + 2001)].StringValue = manager.VideoSourceZ[srcNum].IconHTML; }
                        else {
                            manager.touchpanelZ[TPNumber].UserInterface.SmartObjects[5].StringInput[(ushort)(i + 2011)].StringValue = manager.VideoSourceZ[srcNum].IconSerial;
                            //videoEISC1.StringInput[(ushort)((TPNumber - 1) * 20 + i + 2001)].StringValue = manager.VideoSourceZ[srcNum].IconSerial; 
                        }
                        //for handheld remotes
                        manager.touchpanelZ[TPNumber].UserInterface.UShortInput[(ushort)(i + 201)].UShortValue = manager.VideoSourceZ[srcNum].AnalogModeNumber;
                        //videoEISC1.UShortInput[(ushort)((TPNumber - 1) * 20 + i + 2001)].UShortValue = manager.VideoSourceZ[srcNum].AnalogModeNumber;
                        //Update the current video source of this room to the panel and highlight the appropriate button
                        if (srcNum == manager.RoomZ[currentRoomNumber].CurrentVideoSrc)
                        {
                            manager.touchpanelZ[TPNumber].videoButtonFB((ushort)(i + 1));
                        }
                    }
                }
            }
        }
        /// this function has been updated to V3
        public void UpdateTPMusicMenu(ushort TPNumber)
        {//updates the source text on the sharing menu
            ushort currentRoomNumber = manager.touchpanelZ[TPNumber].CurrentRoomNum;
            ushort currentAudioZone = manager.RoomZ[currentRoomNumber].AudioID;
            if (currentAudioZone > 0)
            {
                ushort asrcScenarioNum = manager.RoomZ[currentRoomNumber].AudioSrcScenario;
                ushort numSrcs = (ushort)manager.AudioSrcScenarioZ[asrcScenarioNum].IncludedSources.Count;
                //for tsr-310s  enable more sources button
                if (numSrcs > 6)
                {
                    //musicEISC1.BooleanInput[(ushort)(TPNumber)].BoolValue = true;
                    manager.touchpanelZ[TPNumber].UserInterface.BooleanInput[529].BoolValue = true;//enable more music sources button
                }
                else
                {
                    //musicEISC1.BooleanInput[(ushort)(TPNumber)].BoolValue = false; }
                    manager.touchpanelZ[TPNumber].UserInterface.BooleanInput[529].BoolValue = false; //remove more music sources button
                }
                    
                    if (manager.RoomZ[currentRoomNumber].CurrentMusicSrc > 0)
                {
                    musicEISC1.UShortInput[(ushort)(TPNumber + 100)].UShortValue = manager.RoomZ[currentRoomNumber].CurrentMusicSrc;//this doesnt route to the panel
                    //musicEISC1.UShortInput[(ushort)(TPNumber + 200)].UShortValue = manager.MusicSourceZ[manager.RoomZ[currentRoomNumber].CurrentMusicSrc].FlipsToPageNumber;
                    manager.touchpanelZ[TPNumber].musicPageFlips(manager.MusicSourceZ[manager.RoomZ[currentRoomNumber].CurrentMusicSrc].FlipsToPageNumber);
                    musicEISC1.UShortInput[(ushort)(TPNumber + 300)].UShortValue = manager.MusicSourceZ[manager.RoomZ[currentRoomNumber].CurrentMusicSrc].EquipID;
                    
                    //musicEISC2.StringInput[(ushort)(TPNumber)].StringValue = manager.MusicSourceZ[manager.RoomZ[currentRoomNumber].CurrentMusicSrc].Name;
                    manager.touchpanelZ[TPNumber].UserInterface.StringInput[3].StringValue = manager.MusicSourceZ[manager.RoomZ[currentRoomNumber].CurrentMusicSrc].Name;
                }
                if (manager.RoomZ[currentRoomNumber].CurrentMusicSrc == 0)
                {
                    //musicEISC1.UShortInput[(ushort)(TPNumber + 400)].UShortValue = 0;//clear the button feedback
                    for (ushort i = 0; i< 20; i++) { 
                        manager.touchpanelZ[TPNumber].UserInterface.SmartObjects[6].BooleanInput[(ushort)(i + 3)].BoolValue = false;//clear all button feedback
                    }
                }
                //highlight button fb for the source
                for (ushort i = 0; i < numSrcs; i++)//loop through all music sources in this scenario. 'i+1' will be the button number in the list
                {
                    manager.touchpanelZ[TPNumber].UserInterface.BooleanInput[(ushort)(530 + ((i + 1) % 6))].BoolValue = false;//first clear the button
                    ushort srcNum = manager.AudioSrcScenarioZ[asrcScenarioNum].IncludedSources[i];
                    //Update the current audio source of this room to the panel and highlight the appropriate button
                    if (srcNum == manager.RoomZ[currentRoomNumber].CurrentMusicSrc)
                    {
                        //handle the hand helds
                        if (manager.touchpanelZ[TPNumber].UseAnalogModes)
                        {
                            if (i == 5)
                            {
                                //musicEISC1.UShortInput[(ushort)(TPNumber + 400)].UShortValue = 6;
                                manager.touchpanelZ[TPNumber].UserInterface.BooleanInput[536].BoolValue = true;
                            }//fb for button 6
                            else
                            {
                                //musicEISC1.UShortInput[(ushort)(TPNumber + 400)].UShortValue = (ushort)((i + 1) % 6); }//music source button fb for handheld remotes
                                manager.touchpanelZ[TPNumber].UserInterface.BooleanInput[(ushort)(530 + ((i + 1) % 6))].BoolValue = true;
                            }
                        }
                        else //this is not a handheld
                        {
                            //musicEISC1.UShortInput[(ushort)(TPNumber + 400)].UShortValue = (ushort)(i + 1);//music button fb
                            for (ushort j = 0; j < 20; j++)
                            {
                                manager.touchpanelZ[TPNumber].UserInterface.SmartObjects[6].BooleanInput[(ushort)(i + 3)].BoolValue = false;//clear all button feedback
                            }
                            manager.touchpanelZ[TPNumber].UserInterface.SmartObjects[6].BooleanInput[(ushort)(i + 3)].BoolValue = true;
                        }//music source list button number to highlight
                    }
                }
                //int inUse = 0;
                if (manager.touchpanelZ[TPNumber].UseAnalogModes)
                {
                    SetASRCGroup(TPNumber, manager.touchpanelZ[TPNumber].CurrentASrcGroupNum);
                    for (ushort i = 0; i < 6; i++)
                    {
                        if ((ushort)((manager.touchpanelZ[TPNumber].CurrentASrcGroupNum - 1) * 6 + i) >= numSrcs) { break; }
                        ushort srcNum = manager.AudioSrcScenarioZ[asrcScenarioNum].IncludedSources[(ushort)((manager.touchpanelZ[TPNumber].CurrentASrcGroupNum - 1) * 6 + i)];
                        //in use fb
                        if (manager.MusicSourceZ[srcNum].InUse) { 
                            //inUse |= (int)(1 << i);
                            manager.touchpanelZ[TPNumber].UserInterface.BooleanInput[(ushort)(551 + i)].BoolValue = true;
                        }//set the bit
                        else {
                            //inUse &= (int)(~(1 << i)); 
                            manager.touchpanelZ[TPNumber].UserInterface.BooleanInput[(ushort)(551 + i)].BoolValue = false;
                        }//clear bit
                    }
                    //in use analog
                    //musicEISC3.UShortInput[(ushort)(TPNumber)].UShortValue = (ushort)inUse;
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

        public void UpdateLightingStatus(ushort KeypadNumber, bool LightsAreOff) {

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
                    UpdateRoomStatusTextAllPanels(room.Value.Number);
                }
            }

            //get the subsystem scenario of the room and if it contains lights continue
            //then update the eisc for the panels to read the current status

        }
        
        /// <summary>
        /// This is for TSR-310 / 302 remotes only.
        /// </summary>
        /// <param name="TPNumber"></param>
        /// <param name="group"></param>
        /// this function has been updated to V3
        public void SetVSRCGroup(ushort TPNumber, ushort group)
        {
            ushort currentRoomNumber = manager.touchpanelZ[TPNumber].CurrentRoomNum;
            ushort numVSrcs = (ushort)manager.VideoSrcScenarioZ[manager.RoomZ[currentRoomNumber].VideoSrcScenario].IncludedSources.Count;
            ushort numVidGroups = (ushort)(numVSrcs / 6);
            ushort modVid = (ushort)(numVSrcs % 6);//this is the number of sources to display in the last group

            //set the number of groups
            if (modVid > 0) { numVidGroups++; }//there is at least 2 groups
            else if (numVidGroups == 0) { numVidGroups++; }//it has to be at least 1
            //update the current group number
            if (group <= numVidGroups) { manager.touchpanelZ[TPNumber].CurrentVSrcGroupNum = group; }
            else { manager.touchpanelZ[TPNumber].CurrentVSrcGroupNum = 1; }
            //set the number of sources to show
            if (manager.touchpanelZ[TPNumber].UseAnalogModes)
            {
                if (numVSrcs < 6) { 
                    //videoEISC1.UShortInput[(ushort)(TPNumber)].UShortValue = modVid;
                    for (ushort i = 0; i < 6; i++)
                    {
                        manager.touchpanelZ[TPNumber].UserInterface.BooleanInput[(ushort)(511 + i)].BoolValue = false;//first clear the button
                        if (i < modVid)
                        {
                            manager.touchpanelZ[TPNumber].UserInterface.BooleanInput[(ushort)(511 + i)].BoolValue = true;//then enable the button. visible true
                        }
                    }
                }
                else if (manager.touchpanelZ[TPNumber].CurrentVSrcGroupNum == numVidGroups && modVid > 0) {
                    //videoEISC1.UShortInput[(ushort)(TPNumber)].UShortValue = modVid; 
                    for (ushort i = 0; i < 6; i++)
                    {
                        manager.touchpanelZ[TPNumber].UserInterface.BooleanInput[(ushort)(511 + i)].BoolValue = false;//first clear the button
                        if (i < modVid)
                        {
                            manager.touchpanelZ[TPNumber].UserInterface.BooleanInput[(ushort)(511 + i)].BoolValue = true;//then enable the button. visible true
                        }
                    }
                }
                else { 
                    //videoEISC1.UShortInput[(ushort)(TPNumber)].UShortValue = 6; 
                    for (ushort i = 0; i < 6; i++)
                    {
                        manager.touchpanelZ[TPNumber].UserInterface.BooleanInput[(ushort)(511 + i)].BoolValue = true;
                    }
                }
            }
            //update the source buttons
            //videoEISC1.UShortInput[(ushort)(TPNumber + 400)].UShortValue = 0;//first clear the button fb, it will be updated later
            for (ushort i = 0; i < 6; i++) {
                manager.touchpanelZ[TPNumber].UserInterface.BooleanInput[(ushort)(501 + i)].BoolValue = false;//first clear the button fb, it will be updated later
            }
            //ushort inUse = 0;
            for (ushort i = 0; i < 6; i++)
            {
                if ((ushort)((manager.touchpanelZ[TPNumber].CurrentVSrcGroupNum - 1) * 6 + i) >= numVSrcs) { break; }
                ushort srcNum = manager.VideoSrcScenarioZ[manager.RoomZ[currentRoomNumber].VideoSrcScenario].IncludedSources[(ushort)((manager.touchpanelZ[TPNumber].CurrentVSrcGroupNum - 1) * 6 + i)];
                //in use
                if (manager.VideoSourceZ[srcNum].InUse)
                {
                    //inUse |= (ushort)(1 << (i));
                    manager.touchpanelZ[TPNumber].UserInterface.BooleanInput[(ushort)(521 + i)].BoolValue = true;
                }//set the bit
                else
                {
                    //inUse &= (ushort)(~(1 << i));
                    manager.touchpanelZ[TPNumber].UserInterface.BooleanInput[(ushort)(521 + i)].BoolValue = false;
                }//clear the bit

                if (srcNum == manager.RoomZ[currentRoomNumber].CurrentVideoSrc)
                {
                    if (i == 5) { 
                        //videoEISC1.UShortInput[(ushort)(TPNumber + 400)].UShortValue = 6;
                        manager.touchpanelZ[TPNumber].UserInterface.BooleanInput[(ushort)(506)].BoolValue = true;
                    }
                    else { 
                        //videoEISC1.UShortInput[(ushort)(TPNumber + 400)].UShortValue = (ushort)((i + 1) % 6);
                        manager.touchpanelZ[TPNumber].UserInterface.BooleanInput[(ushort)(500 + ((i + 1) % 6))].BoolValue = true;
                    }//video source button fb for handheld remote
                }
                //videoEISC1.StringInput[(ushort)((TPNumber - 1) * 20 + i + 1)].StringValue = manager.VideoSourceZ[srcNum].DisplayName;
                manager.touchpanelZ[TPNumber].UserInterface.StringInput[(ushort)(201 + i)].StringValue = manager.VideoSourceZ[srcNum].DisplayName;

                //videoEISC1.UShortInput[(ushort)((TPNumber - 1) * 20 + i + 2001)].UShortValue = manager.VideoSourceZ[srcNum].AnalogModeNumber;
                manager.touchpanelZ[TPNumber].UserInterface.UShortInput[(ushort)(201 + i)].UShortValue = manager.VideoSourceZ[srcNum].AnalogModeNumber;//show the proper icon
            }
            //CrestronConsole.PrintLine("vsrc in use bin{0} dec{1}", Convert.ToString(inUse, 2), inUse);
            //videoEISC2.UShortInput[(ushort)(TPNumber)].UShortValue = (ushort)inUse;
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
                //first clear out any garbage that may be in there
                for (ushort i = 1; i < 50; i++)
                {
                    musicEISC3.StringInput[i].StringValue = i.ToString();
                }
                for (ushort i = 1; i <= manager.MusicSourceZ.Count; i++)
                {
                    ushort eiscposition = 0;
                    if (manager.MusicSourceZ[i].NaxBoxNumber > 0)
                    {
                         eiscposition = (ushort)((manager.MusicSourceZ[i].NaxBoxNumber - 1) * 16 + manager.MusicSourceZ[i].SwitcherInputNumber);
                    }
                    else { 
                        eiscposition = manager.MusicSourceZ[i].SwitcherInputNumber;
                    }
                    if (eiscposition > 0)
                    { 
                        musicEISC3.StringInput[eiscposition].StringValue = manager.MusicSourceZ[i].Name;
                    }

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
        public void NAXOutputSrcChanged(ushort switcherOutputNumber, ushort switcherInputNumber)
        {
            if (switcherInputNumber != 17 && !NAXAllOffBusy) { 
                CrestronConsole.PrintLine("!!!!!!start NAX outputsrc chagnged {0}:{1} INPUT#{2}-OUTPUT#{3}------------------", DateTime.Now.Second, DateTime.Now.Millisecond, switcherInputNumber, switcherOutputNumber);
                ushort currentMusicSource = 0;
                ushort currentRmNum = 0;

                //zone source is off
                if (switcherInputNumber == 0) { musicEISC3.StringInput[(ushort)(switcherOutputNumber + 500)].StringValue = "Off";}
                //GET THE CURRENT SOURCE
                else
                {
                    //get the box number
                    int boxNumber = ((switcherOutputNumber - 1) / 8) + 1;
                    CrestronConsole.PrintLine("FB FROM NAX box {0} zone {1} input {2}", boxNumber, switcherOutputNumber, switcherInputNumber);
                    //now find out which source was selected
                    foreach (var src in manager.MusicSourceZ)
                    {
                        //if the nax box number and the input number match
                        if (src.Value.NaxBoxNumber == boxNumber && src.Value.SwitcherInputNumber == switcherInputNumber)
                        {    
                                currentMusicSource = src.Value.Number; //we found the source
                        }
                    }
                }
                
            /*else
            {//this is a streaming source
                //we need to check the multicast address because it may not have changed
                multiaddress = multis[switcherOutputNumber];
                try
                {
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
            }*/
            //send the source name to the audio zone module
            if (currentMusicSource > 0) { 
                    musicEISC3.StringInput[(ushort)(switcherOutputNumber + 500)].StringValue = manager.MusicSourceZ[currentMusicSource].Name; 
                }


                
                //update the room to reflect the current source
                foreach (var rm in manager.RoomZ)
                {
                    if (rm.Value.AudioID == switcherOutputNumber)
                    {
                        currentRmNum = rm.Value.Number;
                        if (switcherInputNumber == 0)
                        {
                            rm.Value.MusicStatusText = "";
                            rm.Value.CurrentMusicSrc = 0;
                        }
                        
                        else
                        {
                            rm.Value.CurrentMusicSrc = currentMusicSource;
                            CrestronConsole.PrintLine("NAX OUTPUT CHANGED {0} output{1} currentMusicSource{2} {3}", manager.RoomZ[currentRmNum].Name, switcherOutputNumber, currentMusicSource, manager.MusicSourceZ[currentMusicSource].Name);
                        }
                        
                    }
                }
                //don't call this if switcherinput == 17 and current music source == 0
                if (!(switcherInputNumber == 17 && currentMusicSource == 0)) //this is a garbage situation so do nothing if true
                {
                CrestronConsole.PrintLine("switcherInputNumber {0}  currentMusicSource {1} NAXOutputSrcChanged", switcherInputNumber, currentMusicSource);
                updateMusicSourceInUse(currentMusicSource, switcherInputNumber, switcherOutputNumber);
                if (currentRmNum > 0)
                {
                    ReceiverOnOffFromDistAudio(currentRmNum, currentMusicSource);//from NAX output changed
                }
            }

                CrestronConsole.PrintLine("!!!!!END NAXoutputsrcchanged {0}:{1}--------------------", DateTime.Now.Second, DateTime.Now.Millisecond);
            }
        }
        private void MusicPresetQuickActionCallback(object obj)
        {
            NAXoutputChangedTimer.Stop();
            NAXoutputChangedTimer.Dispose();
            UpdateAllPanelsTextWhenAudioChanges();//called from MusicPresetQuickActionCallback
            CrestronConsole.PrintLine("##############     NAXoutputChangedCallback {0}:{1}", DateTime.Now.Second, DateTime.Now.Millisecond);

            RecallMusicPresetTimerBusy = false;
        }
        private void NAXAllOffCallback(object obj)
        {
            NAXoffTimer.Stop();
            NAXoffTimer.Dispose();
            NAXAllOffBusy = false;
            CrestronConsole.PrintLine("##############     HA FLOOR / ALL OFF CALLBACK {0}:{1}", DateTime.Now.Second, DateTime.Now.Millisecond);
        }
        //TO DO !!!! add a lambda to send the preset number to recall and attach it to the callback
        private void SendVolumesMusicPresetCallback(object obj)
        {
            foreach (var rm in manager.RoomZ)
            {
                ushort switcherOutputNum = rm.Value.AudioID;
                ushort zoneChecked = quickActionXML.MusicZoneChecked[quickActionXML.quickActionToRecallOrSave - 1, switcherOutputNum - 1];
                ushort src = quickActionXML.Sources[quickActionXML.quickActionToRecallOrSave - 1, switcherOutputNum - 1];
                if (switcherOutputNum > 0 && zoneChecked > 0 && src > 0)
                {
                    ushort volumeToSend = quickActionXML.Volumes[quickActionXML.quickActionToRecallOrSave - 1, switcherOutputNum - 1];//need to change musicPresetToRecall to lambda
                    musicEISC3.UShortInput[(ushort)(100 + switcherOutputNum)].UShortValue = volumeToSend;//send the volume
                }
            }
        }

        //updated to v3 6-5-24
        public void UpdateAllPanelsTextWhenAudioChanges() 
        {
            foreach (var tp in manager.touchpanelZ)
            {
                ushort TPNumber = tp.Value.Number;
                ushort currentRoomNumber = tp.Value.CurrentRoomNum;
                ushort currentMusicSource = manager.RoomZ[currentRoomNumber].CurrentMusicSrc;
                //only update if the panel is currently on the rooms page

                //find which panels are connected to the current room and update the current source text
                
                CrestronConsole.PrintLine("TP-{0} Room#{1}", TPNumber, manager.RoomZ[currentRoomNumber].AudioID);
                if (manager.RoomZ[currentRoomNumber].CurrentMusicSrc == 0) 
                {
                    UpdatePanelToMusicZoneOff(TPNumber);
                }
                else
                {
                    //musicEISC2.StringInput[TPNumber].StringValue = manager.MusicSourceZ[currentMusicSource].Name;//current source to TP
                    manager.touchpanelZ[TPNumber].UserInterface.StringInput[3].StringValue = manager.MusicSourceZ[currentMusicSource].Name;//current source to TP
                    CrestronConsole.PrintLine("TP-{0} current music src == {1}", TPNumber, manager.MusicSourceZ[currentMusicSource].Name);
                    musicEISC1.UShortInput[(ushort)(TPNumber + 100)].UShortValue = manager.MusicSourceZ[currentMusicSource].Number;//current asrc number to panel media server and sharing objects
                    manager.touchpanelZ[TPNumber].musicPageFlips(manager.MusicSourceZ[currentMusicSource].FlipsToPageNumber);
                    //musicEISC1.UShortInput[(ushort)(TPNumber + 200)].UShortValue = manager.MusicSourceZ[currentMusicSource].FlipsToPageNumber;//current asrc page number to panel
                }
                if (manager.touchpanelZ[TPNumber].CurrentPageNumber == 1) // 1 = list of rooms page 
                {
                    UpdateRoomsPageStatusText(TPNumber);
                }
                else if (manager.touchpanelZ[TPNumber].CurrentPageNumber == 2) // 2 = roomSubsystemList - room is selected and displaying available subsystems
                {//this will update the current music source is playing text on the subsystems list menu
                    UpdatePanelSubsystemText(TPNumber);//from audio changed
                }
            }
        }
        /// <summary>
        ///this function updates the current music source for a room when the multicast address changes. it doesn't do any switching.
        /// </summary>
        public void NAXZoneMulticastChanged(ushort switcherOutputNumber, string multiAddress)
        {
            if (multiAddress != "0.0.0.0" && multiAddress != "" && multiAddress != null) //we don't want to do anything if the zone is turned off. the input will handle that case 
            { 
                ushort currentMusicSource = 0;
                CrestronConsole.PrintLine("NAXZoneMulticastChanged - zone {1} multi address changed to = {0} {2}:{3}", multiAddress, switcherOutputNumber, DateTime.Now.Second, DateTime.Now.Millisecond);

                //figure out which music source this is
                foreach (var src in manager.MusicSourceZ)
                {
                    if (src.Value.MultiCastAddress == multiAddress) { currentMusicSource = src.Value.Number; }
                }
                if (currentMusicSource > 0)
                {
                    musicEISC3.StringInput[(ushort)(switcherOutputNumber + 500)].StringValue = manager.MusicSourceZ[currentMusicSource].Name;//update the current source to the zone module which also updates the sharing page
                    updateMusicSourceInUse(currentMusicSource, manager.MusicSourceZ[currentMusicSource].SwitcherInputNumber, switcherOutputNumber);
                }
                CrestronConsole.PrintLine("END NAXZoneMulticastChanged {0}:{1}", DateTime.Now.Second, DateTime.Now.Millisecond);
                //PanelSelectMusicSource(TPNumber, currentMusicSource);
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
                    UpdateRoomStatusTextAllPanels(room.Value.Number);
                    

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
        public void ReceiverOnOffFromDistAudio(ushort roomNumber, ushort musicSourceNumber) 
        {
            if (roomNumber > 0) 
            { 
                ushort configNum = manager.RoomZ[roomNumber].ConfigurationScenario;
                bool hasRec = false;
                if (configNum > 0) { 
                    hasRec = manager.VideoConfigScenarioZ[configNum].HasReceiver;
                }
                ushort videoSwitcherOutputNum = manager.RoomZ[roomNumber].VideoOutputNum;//this is also the roomNumber in the simpl program. unfortunately. this should change so the dm output can change easily.
                ushort asrcScenario = manager.RoomZ[roomNumber].AudioSrcScenario;

                if (hasRec) 
                {     
                    if (musicSourceNumber == 0)
                    {
                        if (manager.RoomZ[roomNumber].CurrentVideoSrc == 0 && videoSwitcherOutputNum > 0) //make sure video isn't being watched. TODO - change this to check the current receiver input # and turn it off if its on a music input.
                        {
                            videoEISC1.UShortInput[(ushort)(videoSwitcherOutputNum + 700)].UShortValue = 0;//turn the receiver off
                        }
                    }
                    else if (asrcScenario > 0) // send the input to the receiver
                    {
                        for (ushort j = 0; j < manager.AudioSrcScenarioZ[asrcScenario].IncludedSources.Count; j++)
                        {
                            if (musicSourceNumber == manager.AudioSrcScenarioZ[asrcScenario].IncludedSources[j] && videoSwitcherOutputNum > 0)
                            {
                                videoEISC1.UShortInput[(ushort)(videoSwitcherOutputNum + 700)].UShortValue = manager.AudioSrcScenarioZ[asrcScenario].ReceiverInputs[j];//receiver input

                                //turn off video for the room
                                videoEISC1.UShortInput[(ushort)(videoSwitcherOutputNum + 600)].UShortValue = 0;//TV off - TV input = 0
                                videoEISC1.UShortInput[(ushort)(videoSwitcherOutputNum + 500)].UShortValue = 0; //DM off 
                                videoEISC2.StringInput[(ushort)(videoSwitcherOutputNum + 200)].StringValue = "0.0.0.0";//DM NVX multicast address off
                                CrestronConsole.PrintLine("Video off from DISTRIBUTAED AUDIO");
                            }
                        }
                    }
                }
            }
        }

        public void SendReceiverInputTurnOffVideo(ushort videoSwitcherOutputNum, ushort asrcScenario, ushort musicSourceNumber)
        {
           
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
                    ReceiverOnOffFromDistAudio(room.Value.Number, sourceNumber);//on from swamp
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
            UpdateRoomVideoStatusText(dmOutNumber, sourceNumber);
            foreach (var room in manager.RoomZ)
            {
                if (room.Value.VideoOutputNum == dmOutNumber)
                {
                    //update the text for panels connected to this room
                    UpdateRoomStatusTextAllPanels(room.Value.Number);
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
                    UpdateTPVideoMenu(j);
                }
            }
        }
        public void UpdateRoomAVConfig()
        {
            ushort videoOutNumber = 0;
            ushort roomNumber = 0;
            for (ushort i = 1; i <= manager.VideoDisplayZ.Count; i++)
            {
                ushort vidConfigNum = manager.VideoDisplayZ[i].VidConfigurationScenario;
                if (vidConfigNum > 0)
                {
                    videoOutNumber = manager.VideoDisplayZ[i].VideoOutputNum;
                    if (videoOutNumber > 0)
                    {
                        roomNumber = manager.VideoDisplayZ[i].AssignedToRoomNum;
                        //BOOLEANS
                        videoEISC3.BooleanInput[i].BoolValue = manager.VideoConfigScenarioZ[vidConfigNum].HasReceiver;
                        CrestronConsole.PrintLine("Room#{0} hasRec={1} vidconfignum{2}", i, manager.VideoConfigScenarioZ[vidConfigNum].HasReceiver, vidConfigNum);
                        videoEISC3.BooleanInput[(ushort)(i + 100)].BoolValue = manager.VideoConfigScenarioZ[vidConfigNum].ReceiverHasVolFB;
                        videoEISC3.BooleanInput[(ushort)(i + 200)].BoolValue = manager.VideoConfigScenarioZ[vidConfigNum].MusicHasVolFB;
                        videoEISC3.BooleanInput[(ushort)(i + 300)].BoolValue = manager.VideoConfigScenarioZ[vidConfigNum].TvHasVolFB;
                        videoEISC3.BooleanInput[(ushort)(i + 400)].BoolValue = manager.VideoConfigScenarioZ[vidConfigNum].VideoVolThroughDistAudio;
                        videoEISC3.BooleanInput[(ushort)(i + 500)].BoolValue = manager.VideoConfigScenarioZ[vidConfigNum].SendToSpeakers;
                        videoEISC3.BooleanInput[(ushort)(i + 600)].BoolValue = manager.VideoConfigScenarioZ[vidConfigNum].ReceiverHasBreakawayAudio;
                        //ANALOGS
                        videoEISC3.UShortInput[(ushort)(i + 300)].UShortValue = manager.RoomZ[roomNumber].AudioID;//swamp output number
                        videoEISC3.UShortInput[(ushort)(i + 400)].UShortValue = videoOutNumber;
                        videoEISC3.UShortInput[(ushort)(i + 500)].UShortValue = manager.VideoConfigScenarioZ[vidConfigNum].ReceiverInputDelay;
                        videoEISC3.UShortInput[(ushort)(i + 600)].UShortValue = manager.VideoConfigScenarioZ[vidConfigNum].DisplayInputDelay;
                        videoEISC3.UShortInput[(ushort)(i + 700)].UShortValue = manager.VideoDisplayZ[i].TvOutToAudioInputNumber;
                        videoEISC3.UShortInput[(ushort)(i + 800)].UShortValue = manager.VideoConfigScenarioZ[vidConfigNum].OffSubScenarioNum;
                        videoEISC3.UShortInput[(ushort)(i + 1100)].UShortValue = manager.VideoConfigScenarioZ[vidConfigNum].MusicThroughReceiver; //TODO this should probably be deleted because audioSrcScenarios has receiverInputs
                        //ROOM NAMES FOR VIDEO
                        videoEISC2.StringInput[(ushort)(i + 100)].StringValue = manager.VideoDisplayZ[i].DisplayName;

                        //LIFT
                        ushort liftScenarioNum = manager.VideoDisplayZ[i].LiftScenario;
                        if (liftScenarioNum > 0)
                        {
                            videoEISC3.UShortInput[(ushort)(i + 900)].UShortValue = manager.LiftScenarioZ[liftScenarioNum].OpenWithOnCmdNum;
                            videoEISC3.UShortInput[(ushort)(i + 1000)].UShortValue = manager.LiftScenarioZ[liftScenarioNum].CloseWithOffCmdNum;
                            for (ushort j = 0; j < manager.LiftScenarioZ[liftScenarioNum].LiftCmds.Count; j++)
                            {
                                ushort cmdNum = manager.LiftScenarioZ[liftScenarioNum].LiftCmds[j];
                                videoEISC3.UShortInput[(ushort)((i - 1) * 25 + 1200 + (j + 1))].UShortValue = manager.LiftCmdZ[cmdNum].CmdNum;
                                videoEISC3.UShortInput[(ushort)((i - 1) * 25 + 1205 + (j + 1))].UShortValue = manager.LiftCmdZ[cmdNum].PulseTime;
                            }
                        }
                        ushort sleepScenarioNum = manager.RoomZ[roomNumber].SleepScenario;
                        if (sleepScenarioNum > 0)
                        {
                            for (ushort j = 0; j < manager.SleepScenarioZ[sleepScenarioNum].SleepCmds.Count; j++)
                            {
                                ushort sleepCmd = manager.SleepScenarioZ[sleepScenarioNum].SleepCmds[j];
                                videoEISC3.UShortInput[(ushort)((i - 1) * 25 + 1210 + (j + 1))].UShortValue = manager.SleepCmdZ[sleepCmd].Length;
                            }
                        }
                        ushort formatScenarioNum = manager.VideoDisplayZ[i].FormatScenario;
                        if (formatScenarioNum > 0)
                        {
                            for (ushort j = 0; j < manager.FormatScenarioZ[formatScenarioNum].FormatCmds.Count; j++)
                            {
                                ushort formatCmd = manager.FormatScenarioZ[formatScenarioNum].FormatCmds[j];
                                videoEISC3.UShortInput[(ushort)((i - 1) * 25 + 1215 + (j + 1))].UShortValue = manager.FormatCmdZ[formatCmd].CmdNum;
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
            //set the video source scenario and other settings for each room
            foreach (var display in manager.VideoDisplayZ)
            {
                CrestronConsole.PrintLine("display{0} assigned to room{1} {2}", display.Value.DisplayName, display.Value.AssignedToRoomNum, manager.RoomZ[display.Value.AssignedToRoomNum].Name);
                manager.RoomZ[display.Value.AssignedToRoomNum].VideoSrcScenario = display.Value.VideoSourceScenario;
                CrestronConsole.PrintLine("{0}", manager.RoomZ[display.Value.AssignedToRoomNum].VideoSrcScenario);
                manager.RoomZ[display.Value.AssignedToRoomNum].CurrentDisplayNumber = display.Value.Number;
                manager.RoomZ[display.Value.AssignedToRoomNum].VideoOutputNum = display.Value.VideoOutputNum;
                manager.RoomZ[display.Value.AssignedToRoomNum].FormatScenario = display.Value.FormatScenario;
                manager.RoomZ[display.Value.AssignedToRoomNum].LiftScenario = display.Value.LiftScenario;
                manager.RoomZ[display.Value.AssignedToRoomNum].TvOutToAudioInputNumber = display.Value.TvOutToAudioInputNumber;
                manager.RoomZ[display.Value.AssignedToRoomNum].ConfigurationScenario = display.Value.VidConfigurationScenario;
                manager.RoomZ[display.Value.AssignedToRoomNum].NumberOfDisplays++;
                manager.RoomZ[display.Value.AssignedToRoomNum].ListOfDisplays.Add(display.Value.Number);
            }
            //set the panels on the room page that it lives in.
            foreach (var tp in manager.touchpanelZ)
            {
                if (tp.Value.DefaultRoom > 0)
                {
                    RoomButtonPress(tp.Value.Number, true);
                }
                else {
                    HomeButtonPress(tp.Value.Number);
                }
            }
        }

        public void InitializeMulticast() {
            //first clear it out
            for (ushort i = 0; i <100;  i++) { musicEISC3.StringInput[(ushort)(401 + i)].StringValue = "0.0.0.0"; }
            foreach (var src in manager.MusicSourceZ)
            {
                ushort box = src.Value.NaxBoxNumber;
                ushort input = src.Value.SwitcherInputNumber;
                if (box > 0) {
                    ushort eiscPosition = (ushort)(((box - 1) * 24) + input);
                    musicEISC3.StringInput[(ushort)(400 + eiscPosition)].StringValue = src.Value.MultiCastAddress;
                }
            }
        }

        public void UpdateRoomStatusTextAllPanels(ushort roomNumber) {
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
                                    ushort eiscPosition = (ushort)(301 + (30 * (tpNumber - 1)) + i);
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
                    UpdatePanelSubsystemText(tp.Value.Number);//from update all panels
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
            //ushort eiscPosition;
            for (ushort i = 0; i < numSubsystems; i++)
            {
                subName = manager.SubsystemZ[manager.SubsystemScenarioZ[subsystemScenario].IncludedSubsystems[i]].Name;
                //eiscPosition = (ushort)(2601 + (20 * (TPNumber - 1)) + i);

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
                    
                    if (currentVSRC > 0) {
                        statusText = manager.RoomZ[roomNumber].VideoStatusText;
                    }
                    else { statusText = "Off"; }
                }
                else if (subName.ToUpper().Contains("CLIMATE") || subName.ToUpper().Contains("HVAC")) {
                    statusText = GetHVACStatusText(roomNumber, TPNumber);
                }
                else
                {
                    statusText = "";
                }
                //musicEISC2.StringInput[eiscPosition].StringValue = statusText;
                manager.touchpanelZ[TPNumber].UserInterface.SmartObjects[2].StringInput[(ushort)(3 * i + 12)].StringValue = statusText;
            }
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

        public void UpdateRoomHVACText(ushort HVACRoomNumber) {
            //this function is called when the hvac status changes for a particular room
            //it will update the room list status text for all panels that have that room
            if (HVACRoomNumber > 0) { 
                CrestronConsole.PrintLine("updateRoomHVACText roomNumber {0}, temp {1}", HVACRoomNumber, manager.RoomZ[HVACRoomNumber].CurrentTemperature);
                //this updates the zone list room status
                foreach (var tp in manager.touchpanelZ)
                {
                    if (tp.Value.CurrentFloorNum > 0)
                    {
                        ushort numZones = (ushort)manager.Floorz[tp.Value.CurrentFloorNum].IncludedRooms.Count;
                        for (ushort i = 0; i < numZones; i++)
                        {
                            if (HVACRoomNumber == manager.Floorz[tp.Value.CurrentFloorNum].IncludedRooms[i])
                            {
                                ushort tpNumber = tp.Value.Number;
                                ushort eiscPosition = (ushort)(601 + (30 * (tpNumber - 1)) + i);
                                string statusText = GetHVACStatusText(HVACRoomNumber, tpNumber);
                                musicEISC3.StringInput[eiscPosition].StringValue = statusText;
                            }
                        }
                    }
                    //next update the subysytem status list for panels that are currently controlling this room.
                    if (tp.Value.CurrentRoomNum == HVACRoomNumber)
                    {
                        UpdatePanelHVACTextInSubsystemList(tp.Value.Number);
                    }
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
                    string statusText = GetHVACStatusText(roomNumber, TPNumber);

                    musicEISC2.StringInput[eiscPosition].StringValue = statusText;
                }
            }
        }
        public string GetHVACStatusText(ushort roomNumber, ushort TPNumber)
        {
            string statusText = "";
            bool htmlUI = manager.touchpanelZ[TPNumber].HTML_UI;
            string bold = "";
            string boldEnd = "";
            if (!htmlUI) { bold = "<B>"; boldEnd = "</B>"; }
            ushort subsystemScenario = manager.RoomZ[roomNumber].SubSystemScenario;
           
            for (int i = 0; i < manager.SubsystemScenarioZ[subsystemScenario].IncludedSubsystems.Count; i++) {


                string subName = manager.SubsystemZ[manager.SubsystemScenarioZ[subsystemScenario].IncludedSubsystems[i]].DisplayName;
                
                //make sure this room is an HVAC zone
                if (subName.ToUpper() == "CLIMATE" || subName.ToUpper() == "HVAC") {
                    switch (manager.RoomZ[roomNumber].ClimateMode)//1 = auto, 2 = heat, 3 = cool, 4 = off
                    {
                        case ("Auto"):
                            {
                                if (manager.RoomZ[roomNumber].ClimateAutoModeIsSingleSetpoint)
                                {
                                    statusText = bold + Convert.ToString(manager.RoomZ[roomNumber].CurrentTemperature) + "�" + boldEnd + " - Auto Setpoint " + Convert.ToString(manager.RoomZ[roomNumber].CurrentAutoSingleSetpoint) + "�";
                                }
                                else
                                {
                                    statusText = bold + Convert.ToString(manager.RoomZ[roomNumber].CurrentTemperature) + "�" + boldEnd + " - Auto Heat " + Convert.ToString(manager.RoomZ[roomNumber].CurrentHeatSetpoint) + "�" + " Cool " + Convert.ToString(manager.RoomZ[roomNumber].CurrentCoolSetpoint) + "�";
                                }
                                break;
                            }
                        case ("Heat"):
                            {
                                statusText =  bold + Convert.ToString(manager.RoomZ[roomNumber].CurrentTemperature) + "�" + boldEnd + " - Heating to " + Convert.ToString(manager.RoomZ[roomNumber].CurrentHeatSetpoint) + "�";
                                break;
                            }
                        case ("Cool"):
                            {
                                statusText = bold + Convert.ToString(manager.RoomZ[roomNumber].CurrentTemperature) + "�" + boldEnd + " - Cooling to " + Convert.ToString(manager.RoomZ[roomNumber].CurrentCoolSetpoint) + "�";
                                break;
                            }
                        default:
                            {
                                statusText = bold + Convert.ToString(manager.RoomZ[roomNumber].CurrentTemperature) + "�" + boldEnd;
                                break;
                            }
                    }
                }
            }

            return statusText;
        }

        /// <summary>
        ///this function translates the button number pressed to the desired subsystem to be included in the selected quick action
        /// </summary>
        public void SelectQuickActionIncludedSubsystem(ushort buttonNumber)
        {
            CrestronConsole.PrintLine("quick action to save {0}", quickActionXML.quickActionToRecallOrSave);
            if (buttonNumber > 0 && quickActionXML.quickActionToRecallOrSave > 0)
            {
                //toggle the button feedback
                imageEISC.BooleanInput[(ushort)(buttonNumber + 220)].BoolValue = !imageEISC.BooleanInput[(ushort)(buttonNumber + 220)].BoolValue;
            }
        }

        public void RecallClimatePreset(ushort presetNumber)
        {
            foreach (var rm in manager.RoomZ)
            {
                ushort zone = rm.Value.ClimateID;
                if (zone > 0)
                {
                    ushort zoneChecked = quickActionXML.HVACZoneChecked[presetNumber - 1, zone - 1];
                    if (zoneChecked > 0)
                    {
                        ushort modeToSend = quickActionXML.HVACModes[presetNumber - 1, zone - 1];
                        ushort heatSetpointToSend = quickActionXML.HVACHeatSetpoints[presetNumber - 1, zone - 1];
                        ushort coolSetpointToSend = quickActionXML.HVACCoolSetpoints[presetNumber - 1, zone - 1];
                        //modes are 1:auto 2:heat 3:cool 4:off
                        switch (modeToSend)
                        {
                            case 1://auto
                                {
                                    HVACEISC.BooleanInput[zone].BoolValue = true;
                                    HVACEISC.UShortInput[(ushort)(zone + 100)].UShortValue = (ushort)(heatSetpointToSend * 10);
                                    if (!rm.Value.ClimateAutoModeIsSingleSetpoint) 
                                    { 
                                        HVACEISC.UShortInput[(ushort)(zone + 200)].UShortValue = (ushort)(coolSetpointToSend * 10);
                                    }
                                    HVACEISC.BooleanInput[zone].BoolValue = false;
                                    break;
                                }
                            case 2://heat
                                {
                                    HVACEISC.BooleanInput[(ushort)(zone + 100)].BoolValue = true;
                                    HVACEISC.UShortInput[(ushort)(zone + 100)].UShortValue = (ushort)(heatSetpointToSend * 10);
                                    HVACEISC.BooleanInput[(ushort)(zone + 100)].BoolValue = false;
                                    break;
                                }
                            case 3://cool
                                {
                                    HVACEISC.BooleanInput[(ushort)(zone + 200)].BoolValue = true;
                                    HVACEISC.UShortInput[(ushort)(zone + 200)].UShortValue = (ushort)(coolSetpointToSend * 10);
                                    HVACEISC.BooleanInput[(ushort)(zone + 200)].BoolValue = false;
                                    break;
                                }
                            case 4://off
                                {
                                    HVACEISC.BooleanInput[(ushort)(zone + 300)].BoolValue = true;
                                    HVACEISC.BooleanInput[(ushort)(zone + 300)].BoolValue = false;
                                    break;
                                }
                            default: break;
                        }

                    }
                }
            }
        }

        public void RecallMusicPreset(ushort presetNumber)
        {
            //TO DO - add timer to block switcher from updating panels. i think this is done. not sure.
            //TO DO - this is causing an infinite loop. this may not be true. maybe dont need it???
            //TO DO !!!! add a lambda to send the preset number to recall and attach it to the callback


            //in the case that multiple zones are changing sources this delay will let the switching go through and then update the panel status later to prevent bogging down the system by calling the update function every time
            if (presetNumber > 0)
            {
                if (!RecallMusicPresetTimerBusy)
                {
                    NAXoutputChangedTimer = new CTimer(MusicPresetQuickActionCallback, 0, 5000);
                    RecallMusicPresetTimerBusy = true;
                    CrestronConsole.PrintLine("STARTED RECALL MUSIC PRESET {0}:{1}", DateTime.Now.Second, DateTime.Now.Millisecond);
                }
                foreach (var rm in manager.RoomZ)
                {
                    ushort switcherOutputNum = rm.Value.AudioID;
                    if (switcherOutputNum > 0)
                    {
                        ushort zoneChecked = quickActionXML.MusicZoneChecked[presetNumber - 1, switcherOutputNum - 1];
                        if (zoneChecked > 0)
                        {
                            ushort musicSrcToSend = quickActionXML.Sources[presetNumber - 1, switcherOutputNum - 1];
                            SwitcherSelectMusicSource(switcherOutputNum, musicSrcToSend);//from music preset
                            ReceiverOnOffFromDistAudio(rm.Value.Number, musicSrcToSend);//on from music preset
                            CrestronConsole.PrintLine("presetNumber {0} switcherOutput {1} source{2}", presetNumber, switcherOutputNum, musicSrcToSend);
                        }
                    }
                }
                SendVolumeAfterMusicPresetTimer = new CTimer(SendVolumesMusicPresetCallback, 0, 3000);

            }
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
                httpPort = CrestronEthernetHelper.GetEthernetParameter(CrestronEthernetHelper.ETHERNET_PARAMETER_TO_GET.GET_WEB_PORT, CrestronEthernetHelper.GetAdapterdIdForSpecifiedAdapterType(EthernetAdapterType.EthernetLANAdapter));
                httpsPort = CrestronEthernetHelper.GetEthernetParameter(CrestronEthernetHelper.ETHERNET_PARAMETER_TO_GET.GET_SECURE_WEB_PORT, CrestronEthernetHelper.GetAdapterdIdForSpecifiedAdapterType(EthernetAdapterType.EthernetLANAdapter));

                subsystemEISC.BooleanInput[1].BoolValue = false;// tell the av program that this program is not loaded
                foreach (var src in manager.MusicSourceZ) {
                    ushort srcNum = src.Key;
                    if (manager.MusicSourceZ[srcNum].NaxBoxNumber > 0) {
                        NAXsystem = true;
                    }
                }

                foreach (var tp in manager.touchpanelZ)
                {
                    ushort tpNum = tp.Key;
                    string startupNum = Convert.ToString(tpNum);
                    StartupPanel(startupNum);//initialize ssytem
                    
                    if (manager.touchpanelZ[tpNum].ChangeRoomButtonEnable)
                    {
                        manager.touchpanelZ[tpNum].UserInterface.BooleanInput[49].BoolValue = true;//show the change room button.
                        manager.touchpanelZ[tpNum].UserInterface.StringInput[6].StringValue = manager.ProjectInfoZ[0].ProjectName;
                        //update number of quick actions and their names
                        manager.touchpanelZ[tpNum].UserInterface.SmartObjects[15].UShortInput[4].UShortValue = (ushort)quickActionXML.NumberOfPresets;
                    }
                }
                if (NAXsystem)
                {
                    CrestronConsole.PrintLine("this system has NAX ---------------------------");
                    InitializeMulticast();
                }
                else
                {
                    CrestronConsole.PrintLine("this is not an NAX system---------------------");
                }


                StartupRooms();//this sets last system vid to true if no audio is found

                UpdateRoomAVConfig();
                //update number of quick actions and their names
                //imageEISC.UShortInput[101].UShortValue = (ushort)quickActionXML.NumberOfPresets;//DELETE THIS PENDING VERIFICATION OF SMART OBJECT 
                for (ushort i = 0; i <= quickActionXML.NumberOfPresets; i++)
                {
                    //imageEISC.StringInput[(ushort)(i + 3101)].StringValue = quickActionXML.PresetName[i];
                    foreach (var tp in manager.touchpanelZ) { 
                        tp.Value.UserInterface.SmartObjects[15].StringInput[(ushort)(i + 1)].StringValue = quickActionXML.PresetName[i];
                    }
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
            //this.quickActionConfig = new QuickConfiguration.QuickConfigManager();
            this.quickActionXML = new QuickActions.QuickActionXML(this);

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
            /*if (this.quickActionConfig.ReadConfig(@"\nvram\quickActionConfig.json", false))
            {
                this.quickActionManager = new QuickSystemManager(this.quickActionConfig.QuickConfig, this);
                CrestronConsole.PrintLine("read quick actions ok");
            }
            else
            {
                ErrorLog.Error("Unable to read QUICK ACTIONconfig!!!!");
                CrestronConsole.PrintLine("unable to read QUICK ACTION config");
            }*/
            try
            {
                this.quickActionXML.readXML(@"\nvram\quickActionConfig.xml");
                for (ushort x = 0; x < quickActionXML.NumberOfPresets; x++)
                {
                    CrestronConsole.PrintLine("no whammy = {0} {1}", this.quickActionXML.PresetName[x], this.quickActionXML.Sources[x, 2]);
                    for (ushort i = 0; i < quickActionXML.NumberOfAvailableSubsystems; i++)
                    {
                        ushort subsysnumber = quickActionXML.AvailableSubsystems[i];
                        imageEISC.StringInput[(ushort)(3131 + i)].StringValue = manager.SubsystemZ[subsysnumber].DisplayName;
                        imageEISC.StringInput[(ushort)(3141 + i)].StringValue = manager.SubsystemZ[subsysnumber].IconSerial;
                    }

                }
            }
            catch (Exception e) { 
                ErrorLog.Error("Error reading quick action xml: {0}", e.Message);
                CrestronConsole.PrintLine("Error reading quick action xml: {0}", e.Message);
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