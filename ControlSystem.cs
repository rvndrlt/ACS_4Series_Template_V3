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
using ACS_4Series_Template_V3.Music;
using System.Net;
using System.CodeDom.Compiler;
using Crestron.SimplSharpPro.AudioDistribution;
using ACS_4Series_Template_V3.UI;
using static Crestron.SimplSharpPro.Keypads.C2nLcdBXXBaseClass;
using ACS_4Series_Template_V3.Video;
using ACS_4Series_Template_V3.Climate;
using ACS_4Series_Template_V3.UserInterface;

namespace ACS_4Series_Template_V3
{
    public class ControlSystem : CrestronControlSystem
    {
        //public InternalRFExGateway gway;
        //public ThreeSeriesTcpIpEthernetIntersystemCommunications 
        public ThreeSeriesTcpIpEthernetIntersystemCommunications roomSelectEISC, subsystemEISC, musicEISC1, musicEISC2, musicEISC3, videoEISC1, videoEISC2, videoEISC3,  imageEISC;
        public ThreeSeriesTcpIpEthernetIntersystemCommunications VOLUMEEISC, HVACEISC, lightingEISC, subsystemControlEISC, securityEISC;
        private Configuration.ConfigManager config;
        //private QuickConfiguration.QuickConfigManager quickActionConfig;
        public QuickActions.QuickActionXML quickActionXML;
        //private ConfigData.Configuration RoomConfig;
        private static CCriticalSection configLock = new CCriticalSection();
        public NAX nax;
        public SWAMP swamp;
        public MusicSigChange musicSigChange;
        public VideoSigChange videoSigChange;
        public MusicSystemControl musicSystemControl;
        public VideoSystemControl videoSystemControl;
        public ClimateControl climateControl;
        public UserInterfaceControl userInterfaceControl;
        public QuickActions.QuickActionControl quickActionControl;
        public static bool initComplete = false;
        public static bool NAXsystem = false;
        
        public ushort[] volumes = new ushort[100];
        
        public string IPaddress, httpPort, httpsPort;
        public SystemManager manager;
        private readonly uint appID;
        public List<ushort> roomList = new List<ushort>();
        public List<ushort> HomePageMusicRooms = new List<ushort>(); //for the home page list of rooms with music playing
        public bool logging = false;

        
        public CTimer InitCompleteTimer;
        public static Timer xtimer;
        

        public ushort lastMusicSrc, lastSwitcherInput, lastSwitcherOutput;
        public CrestronApp app;


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
                roomSelectEISC = new ThreeSeriesTcpIpEthernetIntersystemCommunications(0x89, "127.0.0.2", this);
                subsystemEISC = new ThreeSeriesTcpIpEthernetIntersystemCommunications(0x8A, "127.0.0.2", this);
                musicEISC1 = new ThreeSeriesTcpIpEthernetIntersystemCommunications(0x8B, "127.0.0.2", this);
                musicEISC2 = new ThreeSeriesTcpIpEthernetIntersystemCommunications(0x8C, "127.0.0.2", this);
                musicEISC3 = new ThreeSeriesTcpIpEthernetIntersystemCommunications(0x8D, "127.0.0.2", this);
                videoEISC1 = new ThreeSeriesTcpIpEthernetIntersystemCommunications(0x8E, "127.0.0.2", this);
                videoEISC2 = new ThreeSeriesTcpIpEthernetIntersystemCommunications(0x8F, "127.0.0.2", this);
                videoEISC3 = new ThreeSeriesTcpIpEthernetIntersystemCommunications(0x90, "127.0.0.2", this);
                imageEISC = new ThreeSeriesTcpIpEthernetIntersystemCommunications(0x91, "127.0.0.2", this);
                //lightingEISC = new EthernetIntersystemCommunications(0x9A, "127.0.0.2", this);
                //HVACEISC = new ThreeSeriesTcpIpEthernetIntersystemCommunications(0x9B, "127.0.0.2", this);
                VOLUMEEISC = new ThreeSeriesTcpIpEthernetIntersystemCommunications(0x9C, "127.0.0.2", this);
                subsystemControlEISC = new ThreeSeriesTcpIpEthernetIntersystemCommunications(0x9D, "127.0.0.2", this);
                nax = new NAX(this);
                swamp = new SWAMP(this);
                musicSystemControl = new MusicSystemControl(this);
                videoSystemControl = new VideoSystemControl(this);
                climateControl = new ClimateControl(this);
                userInterfaceControl = new UserInterfaceControl(this);
                quickActionControl = new QuickActions.QuickActionControl(this);
                musicSigChange = new MusicSigChange(this);
                videoSigChange = new VideoSigChange(this);
                /*
                
                CrestronConsole.PrintLine("++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++");
                app = new CrestronApp(0x03, this);
                app.Description = "DD";
                app.ParameterProjectName.Value = "IPAD";
                app.Register();
                
                CrestronConsole.PrintLine("Crestron App projectname: {0}", app.ParameterProjectName.Value);
                
                */

                roomSelectEISC.SigChange += new SigEventHandler(MainsigChangeHandler);
                subsystemEISC.SigChange += new SigEventHandler(SubsystemSigChangeHandler);
                musicEISC1.SigChange += new SigEventHandler(musicSigChange.Music1SigChangeHandler);
                musicEISC2.SigChange += new SigEventHandler(musicSigChange.Music2SigChangeHandler);
                musicEISC3.SigChange += new SigEventHandler(musicSigChange.Music3SigChangeHandler);
                videoEISC1.SigChange += new SigEventHandler(videoSigChange.Video1SigChangeHandler);
                videoEISC2.SigChange += new SigEventHandler(videoSigChange.Video2SigChangeHandler);
                videoEISC3.SigChange += new SigEventHandler(videoSigChange.Video3SigChangeHandler);
                imageEISC.SigChange += new SigEventHandler(ImageSigChangeHandler);
                //lightingEISC.SigChange += new SigEventHandler(LightingSigChangeHandler);
                //HVACEISC.SigChange += new SigEventHandler(HVACSigChangeHandler);
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
                if (VOLUMEEISC.Register() != eDeviceRegistrationUnRegistrationResponse.Success)
                    ErrorLog.Error("VOLUMEEISC failed registration. Cause: {0}", VOLUMEEISC.RegistrationFailureReason);
                if (subsystemControlEISC.Register() != eDeviceRegistrationUnRegistrationResponse.Success)
                    ErrorLog.Error("subsystemControlEISC failed registration. Cause: {0}", subsystemControlEISC.RegistrationFailureReason);
                VOLUMEEISC.SigChange += this.musicSigChange.Volume_Sigchange;
                subsystemControlEISC.SigChange += this.subysystemControl_SigChange;
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
                if (!CrestronConsole.AddNewConsoleCommand(EnableLogging, "logging", "enable or disable logging", ConsoleAccessLevelEnum.AccessOperator))
                {
                    ErrorLog.Error("Unable to add 'logging' command to console");
                }
                if (!CrestronConsole.AddNewConsoleCommand(QueryLights, "querylights", "report the status of lights in all rooms", ConsoleAccessLevelEnum.AccessOperator))
                {
                    ErrorLog.Error("Unable to add 'querylights' command to console");
                }
               
                //durp = new CrestronGo(0x03, this);
                //durp.Register();
                //durp.GetType().GetProperty("Name").SetValue(durp, "CrestronGo", null);
                CrestronConsole.AddNewConsoleCommand(
                    (s) =>
                    {
                        CrestronConsole.PrintLine(
                            "Git Commit: {0}\r\nGit Branch: {1}\r\nGit Date: {2}",
                            ACS_4Series_Template_V3.GitVersionInfo.CommitHash,
                            ACS_4Series_Template_V3.GitVersionInfo.Branch,
                            ACS_4Series_Template_V3.GitVersionInfo.CommitDate
                        );
                    },
                    "gitinfo",
                    "Displays the current Git commit, branch, and date for this build.",
                    ConsoleAccessLevelEnum.AccessOperator
                );
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

        private void CreateAndRegisterEISCs()
        {
            foreach (var kv in manager.SubsystemZ)
            {
                var sub = kv.Value;            // your SubsystemConfig
                if (sub.Name.ToUpper().Contains("LIGHT"))
                {

                    uint ipid = sub.IPID != 0 ? sub.IPID : 0x9Au;
                    string address = sub.IPaddress;  // might be null

                    lightingEISC = new ThreeSeriesTcpIpEthernetIntersystemCommunications(ipid, address, this);
                    // hook its SigChange to your handler(s) …
                    lightingEISC.SigChange += new SigEventHandler(LightingSigChangeHandler);

                    // register it
                    var resp = lightingEISC.Register();
                    if (resp != eDeviceRegistrationUnRegistrationResponse.Success)
                        ErrorLog.Error(
                          "EISC for subsystem {0} failed registration: {1}",
                          sub.Number,
                          lightingEISC.RegistrationFailureReason);
                }
                else if (sub.Name.ToUpper().Contains("HVAC") || (sub.Name.ToUpper().Contains("CLIMATE")))
                {
                    uint ipid = sub.IPID != 0 ? sub.IPID : 0x9Bu;
                    string address = sub.IPaddress;  // might be null

                    HVACEISC = new ThreeSeriesTcpIpEthernetIntersystemCommunications(ipid, address, this);
                    // hook its SigChange to your handler(s) …
                    HVACEISC.SigChange += new SigEventHandler(HVACSigChangeHandler);

                    // register it
                    var resp = HVACEISC.Register();
                    if (resp != eDeviceRegistrationUnRegistrationResponse.Success)
                        ErrorLog.Error(
                          "EISC for subsystem {0} failed registration: {1}",
                          sub.Number,
                          HVACEISC.RegistrationFailureReason);
                }
                else if (sub.Name.ToUpper().Contains("SECURITY"))
                {
                    uint ipid = sub.IPID != 0 ? sub.IPID : 0xB2u;
                    string address = sub.IPaddress;  // might be null
                    securityEISC = new ThreeSeriesTcpIpEthernetIntersystemCommunications(ipid, address, this);
                    securityEISC.SigChange += new SigEventHandler(securitySigChangeHandler);
                    var resp = securityEISC.Register();
                    if (resp != eDeviceRegistrationUnRegistrationResponse.Success)
                        ErrorLog.Error(
                          "EISC for security {0} failed registration: {1}",
                          sub.Number,
                          securityEISC.RegistrationFailureReason);
                }
            }
        }
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
        public void EnableLogging(string parms)
        {
            if (parms == "?")
            {
                CrestronConsole.ConsoleCommandResponse("logging on\n\r\tturns on logging\n\rlogging off\n\r\tturns off logging\n\r");
            }
            else if (parms == "on")
            {
                logging = true;
                CrestronConsole.PrintLine("logging enabled");
            }
            else if (parms == "off")
            {
                logging = false;
                CrestronConsole.PrintLine("logging disabled");
            }
        }
        public void ReportHVAC(string parms) {
            foreach (var rm in manager.RoomZ)
            {
                CrestronConsole.PrintLine("{0} {1}", rm.Value.Name, rm.Value.CurrentTemperature);
            }
        }

        public void numFloors(string parms)
        {
            if (manager.touchpanelZ[1].HTML_UI) { 
                
            }
            else
            {
                manager.touchpanelZ[1].UserInterface.SmartObjects[3].UShortInput[4].UShortValue = Convert.ToUInt16(parms);
            }
            CrestronConsole.PrintLine("setting floors to {0}", Convert.ToUInt16(parms));
        }
        public void numZones(string parms)
        {
            if (manager.touchpanelZ[1].HTML_UI)
            {

            }
            else
            {
                manager.touchpanelZ[1].UserInterface.SmartObjects[4].UShortInput[1].UShortValue = Convert.ToUInt16(parms);
                manager.touchpanelZ[1].UserInterface.SmartObjects[4].UShortInput[2].UShortValue = Convert.ToUInt16(parms);
                manager.touchpanelZ[1].UserInterface.SmartObjects[4].UShortInput[3].UShortValue = Convert.ToUInt16(parms);
            }

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
            for (ushort i = 0; i < quickActionXML.NumberOfIncludedSubsystems[quickActionXML.quickActionToRecallOrSave - 1]; i++)
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



        //write a sig change handler that reports continuous volume changes


        public void subysystemControl_SigChange(BasicTriList currentDevice, SigEventArgs args)
        {
            if (args.Sig.Type == eSigType.UShort)
            {
                //there are 100 ushort values per touchpanel. 1-100 is for the first touchpanel, 101-200 is for the second touchpanel, etc.
                //find the TPNumber
                ushort TPNumber = (ushort)((args.Sig.Number / 100) + 1);
                //if this is the first ushort value for a touchpanel, route it to the lighting smart object
                if (args.Sig.Number % 100 == 1)
                {
                    if (manager.touchpanelZ[TPNumber].CurrentSubsystemIsVideo)
                    {
                        manager.touchpanelZ[TPNumber].UserInterface.UShortInput[1].UShortValue = args.Sig.UShortValue;//this is the video volume fb
                    }
                    else if (manager.touchpanelZ[TPNumber].CurrentSubsystemIsLights)
                    {
                        CrestronConsole.PrintLine("numberoflightbuttons{0}", args.Sig.UShortValue);
                        if (manager.touchpanelZ[TPNumber].HTML_UI)
                        {
                            manager.touchpanelZ[TPNumber]._HTMLContract.LightButtonList.NumberOfLightButtons(
                                (sig, wh) => sig.UShortValue = args.Sig.UShortValue);
                        }
                        else
                        {
                            manager.touchpanelZ[TPNumber].UserInterface.SmartObjects[8].UShortInput[4].UShortValue = args.Sig.UShortValue;//ushortInput[4] is set # of items
                        }
                    }
                    else if (manager.touchpanelZ[TPNumber].CurrentSubsystemIsShades)
                    {
                        CrestronConsole.PrintLine("numberofShadesColumns{0}", args.Sig.UShortValue);
                        if (manager.touchpanelZ[TPNumber].HTML_UI)
                        {
                            manager.touchpanelZ[TPNumber]._HTMLContract.ShadesList.numberOfShadeColumns(
                                (sig, wh) => sig.UShortValue = args.Sig.UShortValue);
                        }
                    }
                    else if (subsystemEISC.UShortInput[(ushort)(TPNumber + 200)].UShortValue > 300 && subsystemEISC.UShortInput[(ushort)(TPNumber + 200)].UShortValue < 400)
                    {
                        //quick action
                        CrestronConsole.PrintLine("currentsubsystemisquickaction");
                        quickActionControl.RefreshQuickAction(TPNumber);
                    }
                }
                ushort sigNumber = (ushort)(args.Sig.Number % 100);
                //CrestronConsole.PrintLine("subsystemControl sigNumber {0} TPNumber {1} eiscPosition", sigNumber, TPNumber);
                manager.touchpanelZ[TPNumber].UserInterface.UShortInput[(ushort)(sigNumber + 100)].UShortValue = args.Sig.UShortValue;
            }
            else if (args.Sig.Type == eSigType.String)
            {
                //there are 100 string values per touchpanel. 1-100 is for the first touchpanel, 101-200 is for the second touchpanel, etc.
                //find the TPNumber
                ushort TPNumber = (ushort)((args.Sig.Number / 100) + 1);
                //if this is the first ushort value for a touchpanel, route it to the lighting smart object
                ushort stringNumber = (ushort)(args.Sig.Number % 100);
                if (stringNumber == 0 || !manager.touchpanelZ.ContainsKey(TPNumber))
                    return;
                //lights smart object
                if (manager.touchpanelZ[TPNumber].HTML_UI)
                {
                    ushort index = (ushort)(stringNumber - 1);
                    if (manager.touchpanelZ[TPNumber].CurrentSubsystemIsShades)
                    {
                        if (index < manager.touchpanelZ[TPNumber]._HTMLContract.ShadeButtons.Length)
                        {
                            manager.touchpanelZ[TPNumber]._HTMLContract.ShadeButtons[index].ShadeName(
                                (sig, wh) => sig.StringValue = args.Sig.StringValue);
                        }
                    }
                    else if (index < manager.touchpanelZ[TPNumber]._HTMLContract.LightButton.Length)
                    {
                        manager.touchpanelZ[TPNumber]._HTMLContract.LightButton[index].LightButtonName(
                            (sig, wh) => sig.StringValue = args.Sig.StringValue);
                    }
                }
                else {
                    manager.touchpanelZ[TPNumber].UserInterface.SmartObjects[8].StringInput[(ushort)(stringNumber + 10)].StringValue = args.Sig.StringValue;//stringInput[11] is set item 1 text
                }
                manager.touchpanelZ[TPNumber].UserInterface.StringInput[(ushort)(stringNumber + 300)].StringValue = args.Sig.StringValue;//update the text for whatever subsystem page is selected

            }
            else if (args.Sig.Type == eSigType.Bool)
            {
                ushort TPNumber = (ushort)((args.Sig.Number / 200) + 1);//this wont work for button 100. needs fixing
                //if this is the first ushort value for a touchpanel, route it to the lighting smart object
                ushort boolNumber = (ushort)(args.Sig.Number % 200);
                //CrestronConsole.PrintLine("SUBSYSTEM_BUTTON{1} TPNumber {0} sigNumber {2}", TPNumber, boolNumber, args.Sig.Number);
                if (manager.touchpanelZ[TPNumber].CurrentSubsystemIsVideo)
                {
                    if (boolNumber < 140)
                    {
                        manager.touchpanelZ[TPNumber].UserInterface.BooleanInput[(ushort)(boolNumber + 200)].BoolValue = args.Sig.BoolValue;
                    }
                    else if (boolNumber >= 140)
                    {
                        manager.touchpanelZ[TPNumber].UserInterface.BooleanInput[(ushort)(boolNumber)].BoolValue = args.Sig.BoolValue;
                    }

                }
                else if (manager.touchpanelZ[TPNumber].CurrentSubsystemIsShades)
                { 
                    if (manager.touchpanelZ[TPNumber].HTML_UI)
                    {
                        ushort shadeIndex = (ushort)((boolNumber - 1) / 3);
                        ushort signalType = (ushort)((boolNumber - 1) % 3); // 0=open, 1=stop, 2=close

                        if (shadeIndex < manager.touchpanelZ[TPNumber]._HTMLContract.ShadeButtons.Length)
                        {
                            switch (signalType)
                            {
                                case 0: // Open feedback
                                    manager.touchpanelZ[TPNumber]._HTMLContract.ShadeButtons[shadeIndex].ShadeOpened(
                                        (sig, wh) => sig.BoolValue = args.Sig.BoolValue);
                                    break;
                                case 1: // Stop feedback
                                    manager.touchpanelZ[TPNumber]._HTMLContract.ShadeButtons[shadeIndex].ShadeStopped(
                                        (sig, wh) => sig.BoolValue = args.Sig.BoolValue);
                                    break;
                                case 2: // Close feedback
                                    manager.touchpanelZ[TPNumber]._HTMLContract.ShadeButtons[shadeIndex].ShadeClosed(
                                        (sig, wh) => sig.BoolValue = args.Sig.BoolValue);
                                    break;
                            }
                        }
                    } 
                }
                else if (manager.touchpanelZ[TPNumber].CurrentSubsystemIsLights)
                {
                    //lighting button fb
                    if (manager.touchpanelZ[TPNumber].HTML_UI)
                    {

                        if (boolNumber > 0 && boolNumber <= manager.touchpanelZ[TPNumber]._HTMLContract.LightButton.Length)
                        {
                            manager.touchpanelZ[TPNumber]._HTMLContract.LightButton[boolNumber - 1].LightButtonSelected(
                                (sig, wh) => sig.BoolValue = args.Sig.BoolValue);
                        }
                    }
                    else
                    {
                        manager.touchpanelZ[TPNumber].UserInterface.SmartObjects[8].BooleanInput[(ushort)(boolNumber + 10)].BoolValue = args.Sig.BoolValue;//boolInput[11] is item 1 selected
                    }
                }
            //Quick action EQUIPID
            else if (subsystemEISC.UShortInput[(ushort)(TPNumber + 200)].UShortValue > 300 && subsystemEISC.UShortInput[(ushort)(TPNumber + 200)].UShortValue < 400)
            {
                //quick action button fb
                if (manager.touchpanelZ[TPNumber].HTML_UI)
                {
                    //TODO - build contract for quick action buttons
                }
                else
                {
                    manager.touchpanelZ[TPNumber].UserInterface.SmartObjects[15].BooleanInput[(ushort)(boolNumber + 15)].BoolValue = args.Sig.BoolValue;
                }
            }
            else if (boolNumber < 101)//if this is over 100 it will start triggering hvac menus
            {
                manager.touchpanelZ[TPNumber].UserInterface.BooleanInput[(ushort)(boolNumber + 600)].BoolValue = args.Sig.BoolValue;//feedback from current subsystem
            }
            }
        }




        void ImageSigChangeHandler(GenericBase currentDevice, SigEventArgs args)
        {
            if (args.Event == eSigEvent.UShortChange)
            {
                try
                {

                    if (args.Sig.Number <= 100 && args.Sig.UShortValue > 0)//whole house subsystem select a zone
                    {

                        ushort TPNumber = (ushort)args.Sig.Number;

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
                    quickActionControl.SelectQuickActionIncludedSubsystem(idx);
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
                        musicSystemControl.RecallMusicPreset(quickActionXML.quickActionToRecallOrSave);
                    }
                    if (isThisSubsystemInQuickActionList("climate") || isThisSubsystemInQuickActionList("hvac"))
                    {
                        quickActionControl.RecallClimatePreset(quickActionXML.quickActionToRecallOrSave);
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
        void HVACSigChangeHandler(GenericBase currentDevice, SigEventArgs args)
        {
            if (args.Event == eSigEvent.UShortChange)
            {
                ushort zoneNumber = 0;
                ushort function = 0;

                if (args.Sig.Number <= 100) // Temp changed
                {
                    zoneNumber = (ushort)args.Sig.Number;
                    function = 1;
                }
                else if (args.Sig.Number <= 200) // Heat setpoint changed
                {
                    zoneNumber = (ushort)(args.Sig.Number - 100);
                    function = 2;
                }
                else if (args.Sig.Number <= 300) // Cool setpoint changed
                {
                    zoneNumber = (ushort)(args.Sig.Number - 200);
                    function = 3;
                }
                else if (args.Sig.Number <= 400) // Auto single setpoint changed
                {
                    zoneNumber = (ushort)(args.Sig.Number - 300);
                    function = 4;
                }
                else if (args.Sig.Number <= 500) // Scenario number
                {
                    zoneNumber = (ushort)(args.Sig.Number - 400);
                    function = 5;
                }

                // Update the relevant room's HVAC property
                foreach (var room in manager.RoomZ.Values)
                {
                    if (room.ClimateID == zoneNumber && args.Sig.UShortValue > 0)
                    {
                        switch (function)
                        {
                            case 1:
                                room.CurrentTemperature = args.Sig.UShortValue;
                                break;
                            case 2:
                                room.CurrentHeatSetpoint = args.Sig.UShortValue;
                                break;
                            case 3:
                                room.CurrentCoolSetpoint = args.Sig.UShortValue;
                                break;
                            case 4:
                                room.CurrentAutoSingleSetpoint = args.Sig.UShortValue;
                                break;
                            case 5:
                                room.HVACScenario = args.Sig.UShortValue;
                                break;

                        }

                    }
                }
            }
            else if (args.Event == eSigEvent.BoolChange)
                {
                    if (args.Sig.Number > 500) // This is for the boolean output signals that we're subscribed to
                    {
                        // Calculate the climate ID and signal index from the signal number
                        ushort index = (ushort)(args.Sig.Number - 500);
                        ushort climateID = (ushort)(((index - 1) / 30) + 1);
                        ushort signalIndex = (ushort)(((index - 1) % 30) + 1);
                        ushort tpInputNumber = (ushort)(signalIndex + 600);
                        CrestronConsole.PrintLine("climateID {0} signalIndex {1} tpInputNumber {2}", climateID, signalIndex, tpInputNumber);
                        CrestronConsole.PrintLine("TP-1 climate id{0}", manager.touchpanelZ[1].CurrentClimateID);
                        // Update all touchpanels that have the matching climate ID
                        foreach (var panel in manager.touchpanelZ)
                        {
                            if (manager.touchpanelZ[panel.Value.Number].CurrentClimateID == climateID)
                            {
                                // Update the touchpanel's boolean input corresponding to this climate signal
                                manager.touchpanelZ[panel.Value.Number].UserInterface.BooleanInput[tpInputNumber].BoolValue = args.Sig.BoolValue;
                            }
                        }
                    }
                    else if (args.Sig.BoolValue == true)
                    {
                        ushort zoneNumber = 0;
                        ushort function = 0;
                        if (args.Sig.Number <= 100)//mode changed to auto
                        {
                            //updateMode((ushort)args.Sig.Number);
                            zoneNumber = (ushort)args.Sig.Number;
                            function = 1;
                            climateControl.UpdateRoomClimateMode(zoneNumber, function);
                        }
                        else if (args.Sig.Number <= 200)//mode changed to heat
                        {
                            //updateMode((ushort)args.Sig.Number);
                            zoneNumber = (ushort)(args.Sig.Number - 100);
                            function = 2;
                            climateControl.UpdateRoomClimateMode(zoneNumber, function);
                        }
                        else if (args.Sig.Number <= 300)//mode changed to cool
                        {
                            //updateMode((ushort)args.Sig.Number);
                            zoneNumber = (ushort)(args.Sig.Number - 200);
                            function = 3;
                            climateControl.UpdateRoomClimateMode(zoneNumber, function);
                        }
                        else if (args.Sig.Number <= 400)//mode changed to off
                        {
                            //updateMode((ushort)args.Sig.Number);
                            zoneNumber = (ushort)(args.Sig.Number - 300);
                            function = 4;
                            climateControl.UpdateRoomClimateMode(zoneNumber, function);
                        }
                        else if (args.Sig.Number <= 500)
                        {
                            //auto mode is dual setpoint
                            zoneNumber = (ushort)(args.Sig.Number - 400);
                            function = 5;
                            climateControl.UpdateRoomClimateMode(zoneNumber, function);
                        }

                    }
                    else if (args.Sig.BoolValue == false)
                    {

                    }
                
            }
        }

        void securitySigChangeHandler(GenericBase currentDevice, SigEventArgs args)
        {
            if (args.Event == eSigEvent.BoolChange)
            { 
                if (args.Sig.Number > 0 && args.Sig.Number < 50)
                {
                    foreach (var tp in manager.touchpanelZ)
                    {
                        tp.Value.UserInterface.BooleanInput[(ushort)(args.Sig.Number + 750)].BoolValue = args.Sig.BoolValue; //update the security system status on the touchpanel smart object
                    }
                }
                else if (args.Sig.Number > 50 && args.Sig.Number < 55)
                {
                    foreach (var tp in manager.touchpanelZ)
                    {
                        if (tp.Value.HTML_UI)
                        {
                            //TODO - build HTML contract for security partition select buttons
                        }
                        else
                        {
                            tp.Value.UserInterface.SmartObjects[19].BooleanInput[(ushort)(args.Sig.Number)].BoolValue = args.Sig.BoolValue; //security tab button to select partitions
                        }
                    }
                }
                else if (args.Sig.Number > 100 && args.Sig.Number < 300)
                {
                    ushort buttonNumber = (ushort)(args.Sig.Number - 100);
                    foreach (var tp in manager.touchpanelZ)
                    {
                        if (tp.Value.HTML_UI)
                        {
                            tp.Value._HTMLContract.SecurityZone[buttonNumber].ZoneBypassed(
                                (sig, wh) => sig.BoolValue = args.Sig.BoolValue);
                        }
                        else
                        {
                            tp.Value.UserInterface.SmartObjects[21].BooleanInput[(ushort)(buttonNumber + 15)].BoolValue = args.Sig.BoolValue; //security toggle bypass 
                        }
                    }
                }
                else if (args.Sig.Number > 300)
                {
                    ushort buttonNumber = (ushort)(args.Sig.Number - 300); //button number is the signal number minus 300
                    foreach (var tp in manager.touchpanelZ)
                    {
                        if (tp.Value.HTML_UI)
                        {
                            tp.Value._HTMLContract.SecurityZone[buttonNumber].Zone_Visible(
                                (sig, wh) => sig.BoolValue = args.Sig.BoolValue);
                        }
                        else
                        {
                            tp.Value.UserInterface.SmartObjects[21].BooleanInput[(ushort)(buttonNumber + 4015)].BoolValue = args.Sig.BoolValue; //update the security zone violated visibility
                        }
                    }
                }
            }
            else if (args.Event == eSigEvent.UShortChange)
            {
                foreach (var tp in manager.touchpanelZ)
                {
                    if (tp.Value.HTML_UI)
                    {
                        tp.Value._HTMLContract.NumberOfSecurityZones.NumberOfSecurityZones(
                            (sig, wh) => sig.UShortValue = 100);
                    }
                    else
                    {
                        tp.Value.UserInterface.SmartObjects[21].UShortInput[4].UShortValue = 100; //set the number of zones 
                    }
                }
            }
            else if (args.Event == eSigEvent.StringChange)
            {
                if (args.Sig.Number > 200)
                {
                    foreach (var tp in manager.touchpanelZ)
                    {
                        ushort buttonNumber = (ushort)(args.Sig.Number - 200);
                        tp.Value.UserInterface.StringInput[(ushort)(buttonNumber + 11)].StringValue = args.Sig.StringValue;
                    }
                }
                else {
                    foreach (var tp in manager.touchpanelZ)
                    {
                        if (tp.Value.HTML_UI)
                        {
                            tp.Value._HTMLContract.SecurityZone[args.Sig.Number].ZoneName(
                                (sig, wh) => sig.StringValue = args.Sig.StringValue);
                        }
                        else
                        {
                            tp.Value.UserInterface.SmartObjects[21].StringInput[args.Sig.Number + 15].StringValue = args.Sig.StringValue; //zone names
                        }
                    }
                }
            }
        }

        public void QueryLights(string parms)
        {
            foreach (var rm in manager.RoomZ)
            {
                if (rm.Value.LightsID > 0)
                { 
                    CrestronConsole.PrintLine("querying lights {0} offBool{1} {2}", rm.Value.Name, rm.Value.LightsAreOff, rm.Value.LightStatusText);
                }
            }
        }
        public void StartupPanel(string parms)
        {
            ushort TPNumber = Convert.ToUInt16(parms);
            ushort floorScenarioNum = manager.touchpanelZ[TPNumber].FloorScenario;
            imageEISC.BooleanInput[(ushort)(TPNumber + 100)].BoolValue = false;//current subsystem is NOT audio
            manager.touchpanelZ[TPNumber].CurrentSubsystemIsAudio = false;
            ushort currentRoomNumber = manager.touchpanelZ[TPNumber].CurrentRoomNum;
            ushort asrcScenarioNum = manager.RoomZ[currentRoomNumber].AudioSrcScenario;
            if (manager.touchpanelZ[TPNumber].DontInheritSubsystemScenario == false)
            {
                manager.touchpanelZ[TPNumber].SubSystemScenario = manager.RoomZ[currentRoomNumber].SubSystemScenario;
            }
            //initialize the current floor for the panel since we can't do it when the panel is instantiated as it comes before the floor scenarios.
            manager.touchpanelZ[TPNumber].CurrentFloorNum = manager.FloorScenarioZ[floorScenarioNum].IncludedFloors[0];
            //Update the number of floors, current room number, room name
            if (manager.touchpanelZ[TPNumber].HTML_UI) {
                manager.touchpanelZ[TPNumber]._HTMLContract.FloorList.NumberOfFloors(
                        (sig, wh) => sig.UShortValue = (ushort)manager.FloorScenarioZ[floorScenarioNum].IncludedFloors.Count);
            }
            else { 
                manager.touchpanelZ[TPNumber].UserInterface.SmartObjects[3].UShortInput[4].UShortValue = (ushort)manager.FloorScenarioZ[floorScenarioNum].IncludedFloors.Count;
                manager.touchpanelZ[TPNumber].UserInterface.SmartObjects[9].UShortInput[4].UShortValue = (ushort)manager.FloorScenarioZ[floorScenarioNum].IncludedFloors.Count;//music page
            }
            subsystemEISC.UShortInput[(ushort)((TPNumber - 1) * 10 + 301)].UShortValue = manager.RoomZ[currentRoomNumber].AudioID;
            UpdateSubsystems(TPNumber);//from startup panels
            videoSystemControl.UpdateTPVideoMenu(TPNumber);//from startup panels
            CrestronConsole.PrintLine("111TP-{0} current subsystem is audio {1}", TPNumber, manager.touchpanelZ[TPNumber].CurrentSubsystemIsAudio);

            //update music sources to select from
            if (asrcScenarioNum > 0)
            {
                ushort numASrcs = (ushort)manager.AudioSrcScenarioZ[asrcScenarioNum].IncludedSources.Count;
                if (manager.touchpanelZ[TPNumber].HTML_UI)
                {
                    manager.touchpanelZ[TPNumber]._HTMLContract.musicSourceList.numberOfMusicSources(
                            (sig, wh) => sig.UShortValue = numASrcs);
                }
                else
                {
                    manager.touchpanelZ[TPNumber].UserInterface.SmartObjects[6].UShortInput[4].UShortValue = numASrcs;// Number of sources to show
                }
                for (ushort i = 0; i < numASrcs; i++)
                {
                    ushort srcNum = manager.AudioSrcScenarioZ[asrcScenarioNum].IncludedSources[i];
                    if (manager.touchpanelZ[TPNumber].HTML_UI)
                    {
                        manager.touchpanelZ[TPNumber]._HTMLContract.musicSourceSelect[i].musicSourceName(
                                (sig, wh) => sig.StringValue = manager.MusicSourceZ[srcNum].Name);
                        manager.touchpanelZ[TPNumber]._HTMLContract.musicSourceSelect[i].musicSourceIcon(
                                (sig, wh) => sig.StringValue = manager.MusicSourceZ[srcNum].IconHTML);
                    }
                    else
                    {
                        manager.touchpanelZ[TPNumber].UserInterface.SmartObjects[6].StringInput[(ushort)(i + 11)].StringValue = BuildHTMLString(TPNumber, manager.MusicSourceZ[srcNum].Name, "26");//set the font size to 26
                        manager.touchpanelZ[TPNumber].UserInterface.SmartObjects[6].StringInput[(ushort)(i + 2011)].StringValue = manager.MusicSourceZ[srcNum].IconSerial;

                        if (i < 6 && manager.touchpanelZ[TPNumber].UseAnalogModes)
                        {
                            manager.touchpanelZ[TPNumber].UserInterface.UShortInput[(ushort)(i + 211)].UShortValue = manager.MusicSourceZ[srcNum].AnalogModeNumber;
                        }
                    }
                }
            }

            else
            {
                if (manager.touchpanelZ[TPNumber].HTML_UI)
                {
                    manager.touchpanelZ[TPNumber]._HTMLContract.musicSourceList.numberOfMusicSources(
                            (sig, wh) => sig.UShortValue = 0);
                }
                else
                {
                    manager.touchpanelZ[TPNumber].UserInterface.SmartObjects[6].UShortInput[4].UShortValue = 0;// no sources to display
                }
            }
            musicSystemControl.UpdateTPMusicMenu(TPNumber);//from startup panels
            manager.touchpanelZ[TPNumber].SubscribeToMusicMenuEvents(currentRoomNumber);//from startup panels
            manager.touchpanelZ[TPNumber].SubscribeToVideoMenuEvents(currentRoomNumber);//from startup panels
            if (manager.FloorScenarioZ[floorScenarioNum].IncludedFloors.Count > 1)
            {
                UpdateTPFloorNames(TPNumber);//from startup panels
            }
            else
            {
                //there is only 1 floor in this scenario
                ushort currentNumberOfZones = (ushort)manager.Floorz[manager.FloorScenarioZ[floorScenarioNum].IncludedFloors[0]].IncludedRooms.Count;
                if (manager.touchpanelZ[TPNumber].HTML_UI)
                {
                    manager.touchpanelZ[TPNumber]._HTMLContract.roomList.numberOfZones(
                            (sig, wh) => sig.UShortValue = currentNumberOfZones);
                }
                else
                {
                    manager.touchpanelZ[TPNumber].UserInterface.SmartObjects[4].UShortInput[3].UShortValue = currentNumberOfZones;//set the room list size
                }
                CrestronConsole.PrintLine("TP-{0}, number of zones{1}", TPNumber, currentNumberOfZones);
                for (ushort i = 0; i < currentNumberOfZones; i++)
                {
                    ushort zoneTemp = manager.Floorz[manager.FloorScenarioZ[floorScenarioNum].IncludedFloors[0]].IncludedRooms[i];
                    if (manager.touchpanelZ[TPNumber].HTML_UI)
                    {
                        manager.touchpanelZ[TPNumber]._HTMLContract.roomButton[i].zoneName(
                                (sig, wh) => sig.StringValue = manager.RoomZ[zoneTemp].Name);
                        //TODO - add room status and image
                    }
                    else
                    {
                        manager.touchpanelZ[TPNumber].UserInterface.SmartObjects[4].StringInput[(ushort)(4 * i + 1)].StringValue = manager.RoomZ[zoneTemp].Name;
                        manager.touchpanelZ[TPNumber].UserInterface.SmartObjects[10].StringInput[(ushort)(3 * i + 1)].StringValue = manager.RoomZ[zoneTemp].Name;//whole house zone list
                    }
                }
            }
            if (manager.touchpanelZ[TPNumber].HTML_UI)
            {
                InitializeHomePageMusicZonesForHTML(TPNumber);
            }
            UpdateEquipIDsForSubsystems(TPNumber, currentRoomNumber);//from startup panels
            CrestronConsole.PrintLine("TP-{0} complete!!", (TPNumber));
        }
        public void UpdateTPFloorNames(ushort TPNumber) {
            //send the floor names
            ushort floorScenarioNum = manager.touchpanelZ[TPNumber].FloorScenario;
            if (manager.touchpanelZ[TPNumber].HTML_UI)
            {
                manager.touchpanelZ[TPNumber]._HTMLContract.FloorList.NumberOfFloors((sig, wh) => sig.UShortValue = (ushort)manager.FloorScenarioZ[floorScenarioNum].IncludedFloors.Count);
            }
            else {
                manager.touchpanelZ[TPNumber].UserInterface.SmartObjects[3].UShortInput[4].UShortValue = (ushort)manager.FloorScenarioZ[floorScenarioNum].IncludedFloors.Count;
            }
            for (ushort i = 0; i < manager.FloorScenarioZ[floorScenarioNum].IncludedFloors.Count; i++)
            {
                if (manager.touchpanelZ[TPNumber].HTML_UI)
                {
                    manager.touchpanelZ[TPNumber]._HTMLContract.FloorSelect[i].FloorName(
                            (sig, wh) => sig.StringValue = manager.Floorz[manager.FloorScenarioZ[floorScenarioNum].IncludedFloors[i]].Name);
                }
                else
                {
                    string floorName = string.Format(@"<FONT size=""26"">{0}</FONT>", manager.Floorz[manager.FloorScenarioZ[floorScenarioNum].IncludedFloors[i]].Name);
                    manager.touchpanelZ[TPNumber].UserInterface.SmartObjects[3].StringInput[(ushort)(i + 11)].StringValue = floorName;
                    manager.touchpanelZ[TPNumber].UserInterface.SmartObjects[9].StringInput[(ushort)(i + 11)].StringValue = floorName;//music
                }
            }
        }
        public ushort FindOutWhichFloorThisRoomIsOn(ushort TPNumber, ushort roomNumber)
        {
            ushort floorNumber = 0;
            ushort floorScenario = manager.touchpanelZ[TPNumber].FloorScenario;
            for (ushort i = 0; i < manager.FloorScenarioZ[floorScenario].IncludedFloors.Count; i++)
            {
                ushort actualFloorNum = manager.FloorScenarioZ[floorScenario].IncludedFloors[i];
                // Check if this floor exists and contains the room
                if (manager.Floorz.ContainsKey(actualFloorNum) &&
                    manager.Floorz[actualFloorNum].IncludedRooms.Contains(roomNumber) &&
                    manager.Floorz[actualFloorNum].Name.ToUpper() != "ALL")
                {
                    floorNumber = actualFloorNum;
                    break; // Found it, no need to continue
                }
            }
            return floorNumber;
        }


        /// <summary>
        /// Builds an HTML string for iphones using the input text with a specified font size.
        /// </summary>
        /// <param name="TPNumber"></param>
        /// <param name="input"></param>
        /// <param name="fontSize"></param>
        /// <returns></returns>
        public string BuildHTMLString(ushort TPNumber, string input, string fontSize)
        {
            string htmlString = "";
            //wrap the input in a font tag with the fontsize
            if (manager.touchpanelZ[TPNumber].Name.ToUpper().Contains("IPHONE"))
            {
                htmlString = string.Format(@"<FONT size=""{0}"">{1}</FONT>", fontSize, input);
            }
            else 
            {
                htmlString = input;
            }
            return htmlString;

        }
        public void PressCloseXButton(ushort TPNumber)
        {
            CrestronConsole.PrintLine("TP-{0} closeX page{1}", TPNumber, manager.touchpanelZ[TPNumber].CurrentPageNumber);
            ushort currentRoomNum = manager.touchpanelZ[TPNumber].CurrentRoomNum;

            //clear out the music source subpage
            manager.touchpanelZ[TPNumber].CurrentSubsystemIsAudio = false;//clear the current subsystem is audio
            manager.touchpanelZ[TPNumber].musicPageFlips(0);//from CloseXButton clear the music page
            manager.touchpanelZ[TPNumber].videoPageFlips(0);//from close X / clear the video page
            manager.touchpanelZ[TPNumber].SleepFormatLiftMenu("CLOSE", 0);
            manager.touchpanelZ[TPNumber].UserInterface.BooleanInput[351].BoolValue = false;//clear the video displays sub
            manager.touchpanelZ[TPNumber].UserInterface.BooleanInput[998].BoolValue = false;//clear the sharing sub
            manager.touchpanelZ[TPNumber].UserInterface.BooleanInput[999].BoolValue = false;//clear the sharing sub
            manager.touchpanelZ[TPNumber].UserInterface.BooleanInput[1002].BoolValue = false;//clear the sharing button fb
            manager.touchpanelZ[TPNumber].SrcSharingButtonFB = false;
            manager.touchpanelZ[TPNumber].subsystemPageFlips(0);//close x clear the subsystem page
            //if home page
            if (manager.touchpanelZ[TPNumber].CurrentPageNumber == (ushort)TouchpanelUI.CurrentPageType.Home)
            {
                subsystemEISC.UShortInput[(ushort)(TPNumber + 200)].UShortValue = (ushort)(300 + TPNumber);//home/quickaction equipID is 3
            }
            else {
                subsystemEISC.UShortInput[(ushort)(TPNumber + 200)].UShortValue = 0;//subsystem equipID 0 disconnects from the subsystem
            }
            //first handle the case of the rooms menu
            if (manager.touchpanelZ[TPNumber].CurrentPageNumber > 0)//the panel is currently not on the HOME page
            {
                manager.touchpanelZ[TPNumber].CurrentPageNumber = 2;//just closed a subystem menu so now were on the subystem list of a room
                //TODO - get rid of this and TP INTERFACE MODULE
                imageEISC.BooleanInput[TPNumber].BoolValue = false;//clear "current subsystem is video"
                manager.touchpanelZ[TPNumber].CurrentSubsystemIsVideo = false;
                imageEISC.BooleanInput[(ushort)(TPNumber + 100)].BoolValue = false;//clear "current subsystem is audio"
                                                                                   //update the current music source
                //UpdatePanelSubsystemText(TPNumber);//from close X button
            }
            //then handle the case of the home menu
            else
            {
                //we want to go back to the zone list page
                WholeHouseUpdateZoneList(TPNumber);
                SendSubsystemZonesPageNumber(TPNumber, true);//from close X button on home page
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
                floorButtonNumber = FloorButtonNumberToHighLight(TPNumber, currentFloor);
            }
            if (manager.FloorScenarioZ[floorScenarioNum].IncludedFloors.Count > 1)
            {
                this.manager.touchpanelZ[TPNumber].CurrentFloorNum = currentFloor; //SET the current floor for this panel
            }
            ushort currentNumberOfZones = (ushort)this.manager.Floorz[currentFloor].IncludedRooms.Count();
            //send the number of room buttons to show
            if (manager.touchpanelZ[TPNumber].HTML_UI)
            {
                manager.touchpanelZ[TPNumber]._HTMLContract.roomList.numberOfZones(
                        (sig, wh) => sig.UShortValue = currentNumberOfZones);
            }
            else { 
                manager.touchpanelZ[TPNumber].UserInterface.SmartObjects[4].UShortInput[3].UShortValue = currentNumberOfZones;
            }
            manager.touchpanelZ[TPNumber].floorButtonFB(floorButtonNumber);//highlight the floor button
            manager.touchpanelZ[TPNumber].SubscribeToListOfRoomsStatusEvents(currentFloor);//from select floor
            UpdateRoomsPageStatusText(TPNumber);
        }
        public void SelectWholeHouseFloor(ushort TPNumber, ushort floorButtonNumber)
        {
            ushort wholeHouseSubsystemScenarioNum = manager.touchpanelZ[TPNumber].HomePageScenario;
            ushort currentSubsystem = manager.touchpanelZ[TPNumber].CurrentSubsystemNumber;

            for (ushort i = 0; i < manager.WholeHouseSubsystemScenarioZ[wholeHouseSubsystemScenarioNum].WholeHouseSubsystems.Count; i++)
            {
                if (manager.WholeHouseSubsystemScenarioZ[wholeHouseSubsystemScenarioNum].WholeHouseSubsystems[i].SubsystemNumber == currentSubsystem)
                {
                    ushort floorNumber = manager.WholeHouseSubsystemScenarioZ[wholeHouseSubsystemScenarioNum].WholeHouseSubsystems[i].IncludedFloors[floorButtonNumber - 1];

                    // Store the current floor
                    manager.touchpanelZ[TPNumber].CurrentFloorNum = floorNumber;

                    // Build the room list and filter based on subsystem type
                    manager.touchpanelZ[TPNumber].WholeHouseRoomList.Clear();
                    string subsystemName = manager.SubsystemZ[currentSubsystem].Name.ToUpper();
                    string subsystemIcon = manager.SubsystemZ[currentSubsystem].IconSerial;
                    string subsystemIconHTML = manager.SubsystemZ[currentSubsystem].IconHTML;

                    // Loop through rooms on this floor and filter by valid subsystem ID
                    foreach (ushort roomNumber in manager.Floorz[floorNumber].IncludedRooms)
                    {
                        var room = manager.RoomZ[roomNumber];
                        bool isValidRoom = false;

                        // Check if room has valid ID for the current subsystem
                        if (subsystemName.Contains("LIGHT"))
                        {
                            isValidRoom = room.LightsID > 0;
                        }
                        else if (subsystemName.Contains("CLIMATE") || subsystemName.Contains("HVAC"))
                        {
                            isValidRoom = room.ClimateID > 0;
                        }
                        else if (subsystemName.Contains("SHADE") || subsystemName.Contains("DRAPE"))
                        {
                            isValidRoom = room.ShadesID > 0;
                        }
                        else
                        {
                            isValidRoom = true; // For other subsystems, include all rooms
                        }

                        if (isValidRoom)
                        {
                            manager.touchpanelZ[TPNumber].WholeHouseRoomList.Add(roomNumber);
                        }
                    }

                    ushort currentNumberOfZones = (ushort)manager.touchpanelZ[TPNumber].WholeHouseRoomList.Count;

                    // Send number of zone buttons to show
                    if (manager.touchpanelZ[TPNumber].HTML_UI)
                    {
                        manager.touchpanelZ[TPNumber]._HTMLContract.WholeHouseZoneList.numberOfWholeHouseZones(
                                (sig, wh) => sig.UShortValue = currentNumberOfZones);
                    }
                    else
                    {
                        manager.touchpanelZ[TPNumber].UserInterface.SmartObjects[10].UShortInput[3].UShortValue = currentNumberOfZones;
                    }

                    // Populate each zone button
                    for (ushort j = 0; j < currentNumberOfZones; j++)
                    {
                        ushort roomNumber = manager.touchpanelZ[TPNumber].WholeHouseRoomList[j];
                        var room = manager.RoomZ[roomNumber];
                        string roomName = room.Name;
                        string statusText = "";

                        // Get status text based on subsystem type
                        if (subsystemName.Contains("CLIMATE") || subsystemName.Contains("HVAC"))
                        {
                            statusText = room.HVACStatusText ?? "";
                        }
                        else if (subsystemName.Contains("LIGHT"))
                        {
                            statusText = room.LightStatusText ?? "";
                            CrestronConsole.PrintLine("Room {0} LightStatusText: {1}", room.Name, statusText);
                        }

                        // Capture loop variable for lambda closure
                        ushort index = j;
                        string capturedRoomName = roomName;
                        string capturedStatusText = statusText;
                        string capturedIcon = subsystemIconHTML;

                        // Update zone button
                        if (manager.touchpanelZ[TPNumber].HTML_UI)
                        {
                            manager.touchpanelZ[TPNumber]._HTMLContract.WholeHouseZone[index].HouseZoneName(
                                    (sig, wh) => sig.StringValue = capturedRoomName);
                            manager.touchpanelZ[TPNumber]._HTMLContract.WholeHouseZone[index].HouseZoneStatus(
                                    (sig, wh) => sig.StringValue = capturedStatusText);
                            manager.touchpanelZ[TPNumber]._HTMLContract.WholeHouseZone[index].HouseZoneIcon(
                                    (sig, wh) => sig.StringValue = capturedIcon);
                        }
                        else
                        {
                            manager.touchpanelZ[TPNumber].UserInterface.SmartObjects[10].StringInput[(ushort)(3 * j + 11)].StringValue = roomName;
                            manager.touchpanelZ[TPNumber].UserInterface.SmartObjects[10].StringInput[(ushort)(3 * j + 12)].StringValue = statusText;
                            manager.touchpanelZ[TPNumber].UserInterface.SmartObjects[10].StringInput[(ushort)(3 * j + 13)].StringValue = subsystemIcon;
                        }
                    }

                    CrestronConsole.PrintLine("SelectWholeHouseFloor TP-{0} floor{1} numZones{2}", TPNumber, floorNumber, currentNumberOfZones);
                    manager.touchpanelZ[TPNumber].floorButtonFB(floorButtonNumber);
                    manager.touchpanelZ[TPNumber].SubscribeToWholeHouseListEvents();

                    break;
                }
            }
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

                foreach (var rm in manager.Floorz[currentFloor].IncludedRooms)
                {
                    var room = manager.RoomZ[rm];
                    room.MusicVolume = room.MusicVolume;  // <<<<<<<< FORCES THE EVENT TO FIRE
                }

                UpdateMusicSharingPage(TPNumber, currentRoomNumber);//from music floor select
                if (manager.touchpanelZ[TPNumber].SrcSharingButtonFB)
                {
                    manager.touchpanelZ[TPNumber].SubscribeToMusicSharingChanges();
                }
            }
        }
        /// <summary>
        /// this updates the image and status text of each room on the list of rooms to select page
        /// </summary>
        public void UpdateRoomsPageStatusText(ushort TPNumber)
        {
            if (!manager.touchpanelZ.ContainsKey(TPNumber))
            {
                ErrorLog.Error("Error: Invalid TPNumber {0} in UpdateRoomsPageStatusText", TPNumber);
                CrestronConsole.PrintLine("Error: Invalid TPNumber {0} in UpdateRoomsPageStatusText", TPNumber);
                return;
            }

            ushort currentFloorNum = manager.touchpanelZ[TPNumber].CurrentFloorNum;
            if (currentFloorNum == 0 || !manager.Floorz.ContainsKey(currentFloorNum))
            {
                CrestronConsole.PrintLine("Error: Invalid floor {0} for TP-{1}", currentFloorNum, TPNumber);
                return;
            }

            //update all of the room names and status for the rooms page
            ushort currentNumberOfZones = (ushort)this.manager.Floorz[currentFloorNum].IncludedRooms.Count();
            for (ushort i = 0; i < currentNumberOfZones; i++) //send the zone names for current floor out to the xsig
            {
                ushort zoneTemp = this.manager.Floorz[currentFloorNum].IncludedRooms[i];
                if (!manager.RoomZ.ContainsKey(zoneTemp))
                {
                    ErrorLog.Error("UpdateRoomsPageStatusText: Room {0} doesn't exist in RoomZ from TP-{1}", zoneTemp, TPNumber);
                    CrestronConsole.PrintLine("UpdateRoomsPageStatusText: Room {0} doesn't exist in RoomZ from TP-{1}", zoneTemp, TPNumber);
                    continue;
                }

                // Guard against null ImageURL
                string imageUrl = manager.RoomZ[zoneTemp].ImageURL ?? "";
                string imagePath = "";

                if (!string.IsNullOrEmpty(imageUrl))
                {
                    imagePath = (manager.touchpanelZ[TPNumber].IsConnectedRemotely)
                        ? string.Format("http://{0}:{1}/{2}", manager.ProjectInfoZ[0].DDNSAdress ?? "", httpsPort ?? "", imageUrl)
                        : string.Format("http://{0}:{1}/{2}", IPaddress ?? "", httpsPort ?? "", imageUrl);
                }

                //room name
                if (manager.touchpanelZ[TPNumber].HTML_UI)
                {
                    manager.touchpanelZ[TPNumber]._HTMLContract.roomButton[i].zoneName(
                            (sig, wh) => sig.StringValue = this.manager.RoomZ[zoneTemp].Name ?? "");
                    manager.touchpanelZ[TPNumber]._HTMLContract.roomButton[i].zoneImage(
                            (sig, wh) => sig.StringValue = imagePath);
                }
                else
                {
                    if (!string.IsNullOrEmpty(imageUrl))
                    {
                        imagePath = (manager.touchpanelZ[TPNumber].IsConnectedRemotely)
                            ? string.Format("http://{0}:{1}/{2}", manager.ProjectInfoZ[0].DDNSAdress ?? "", httpPort ?? "", imageUrl)
                            : string.Format("http://{0}:{1}/{2}", IPaddress ?? "", httpPort ?? "", imageUrl);
                    }

                    manager.touchpanelZ[TPNumber].UserInterface.SmartObjects[4].StringInput[(ushort)(4 * i + 11)].StringValue = this.manager.RoomZ[zoneTemp].Name ?? "";
                    manager.touchpanelZ[TPNumber].UserInterface.SmartObjects[4].StringInput[(ushort)(4 * i + 14)].StringValue = imagePath;
                }
            }
        }

        public void UpdateEquipIDsForSubsystems(ushort TPNumber, ushort currentRoomNumber) {
            //Update eisc with current equipIDs for the subsystems of the currently selected room
            subsystemEISC.UShortInput[(ushort)((TPNumber - 1) * 10 + 301)].UShortValue = manager.RoomZ[currentRoomNumber].AudioID;//audioID is also switcher output number
            subsystemEISC.UShortInput[(ushort)((TPNumber - 1) * 10 + 302)].UShortValue = manager.RoomZ[currentRoomNumber].VideoOutputNum;//in the simpl program this is the room number
            subsystemEISC.UShortInput[(ushort)((TPNumber - 1) * 10 + 303)].UShortValue = manager.RoomZ[currentRoomNumber].LightsID;
            subsystemEISC.UShortInput[(ushort)((TPNumber - 1) * 10 + 304)].UShortValue = manager.RoomZ[currentRoomNumber].ShadesID;
            subsystemEISC.UShortInput[(ushort)((TPNumber - 1) * 10 + 306)].UShortValue = manager.RoomZ[currentRoomNumber].MiscID;
        }


        /// <summary>
        /// updates the smart graphic list of tvs in a room
        /// </summary>
        /// <param name="TPNumber"></param>
        public void UpdateVideoDisplayList(ushort TPNumber) { 
            ushort currentRoomNum = manager.touchpanelZ[TPNumber].CurrentRoomNum;
            ushort numDisplays = (ushort)manager.RoomZ[currentRoomNum].ListOfDisplays.Count;
            if (manager.touchpanelZ[TPNumber].HTML_UI) {
                //TODO - build HTML contract for video display list
            }
            else {
                manager.touchpanelZ[TPNumber].UserInterface.SmartObjects[34].UShortInput[4].UShortValue = numDisplays; //update the number of displays to show
                for (ushort i = 0; i < numDisplays; i++)
                { 
                    manager.touchpanelZ[TPNumber].UserInterface.SmartObjects[34].StringInput[(ushort)(i + 11)].StringValue = manager.VideoDisplayZ[manager.RoomZ[currentRoomNum].ListOfDisplays[i]].DisplayName;
                }
            }
        }


        public void UpdateMusicSharingPage(ushort TPNumber, ushort currentRoomNumber)
        {
            ushort numRooms = 0;
            ushort roomNumber = 0;
            ushort flag = 0;
            manager.touchpanelZ[TPNumber].MusicRoomsToShareSourceTo.Clear();
            manager.touchpanelZ[TPNumber].MusicRoomsToShareCheckbox.Clear();

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
                        if (manager.touchpanelZ[TPNumber].HTML_UI)
                        {
                            manager.touchpanelZ[TPNumber]._HTMLContract.MusicRoomControl[i].musicZoneSelected(
                                (sig, wh) => sig.BoolValue = false);//clear the checkboxes
                        }
                        else
                        {
                            manager.touchpanelZ[TPNumber].UserInterface.SmartObjects[7].BooleanInput[(ushort)(i * 7 + 4011)].BoolValue = false;//clear the checkboxes
                        }
                        roomNumber = manager.Floorz[currentFloor].IncludedRooms[i];
                        //build a list of rooms to add
                        if (roomNumber == currentRoomNumber || manager.RoomZ[roomNumber].AudioID == 0) //Skip over this room
                        {
                            flag++;
                            //CrestronConsole.PrintLine("flag {0}", manager.RoomZ[roomNumber].Name);
                        }
                        else {
                            manager.touchpanelZ[TPNumber].MusicRoomsToShareSourceTo.Add(roomNumber);
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
                        if (manager.touchpanelZ[TPNumber].HTML_UI)
                        {
                            manager.touchpanelZ[TPNumber]._HTMLContract.MusicRoomControl[i].musicZoneSelected(
                                (sig, wh) => sig.BoolValue = false);//clear the checkboxes
                        }
                        else
                        {
                            manager.touchpanelZ[TPNumber].UserInterface.SmartObjects[7].BooleanInput[(ushort)(i * 7 + 4011)].BoolValue = false;//clear the checkboxes
                        }
                        roomNumber = manager.AudioSrcSharingScenarioZ[manager.RoomZ[currentRoomNumber].AudioSrcSharingScenario].IncludedZones[i];
                        if (roomNumber == currentRoomNumber) { flag = 1; }
                        else
                        {
                            manager.touchpanelZ[TPNumber].MusicRoomsToShareSourceTo.Add(roomNumber);
                            manager.touchpanelZ[TPNumber].MusicRoomsToShareCheckbox.Add(false);
                        }
                    }
                }
                manager.touchpanelZ[TPNumber].UnsubscribeTouchpanelFromAllVolMuteChanges();//from updateMusicSharing
                //re subscribe to the master volume and mute changes
                manager.touchpanelZ[TPNumber].UserInterface.UShortInput[2].UShortValue = manager.RoomZ[currentRoomNumber].MusicVolume;
                manager.touchpanelZ[TPNumber].UserInterface.BooleanInput[1009].BoolValue = manager.RoomZ[currentRoomNumber].MusicMuted;
                EventHandler volumeHandler = (sender, e) => manager.touchpanelZ[TPNumber].UserInterface.UShortInput[2].UShortValue = manager.RoomZ[currentRoomNumber].MusicVolume;
                manager.RoomZ[currentRoomNumber].MusicVolumeChanged += volumeHandler;
                manager.touchpanelZ[TPNumber].VolumeChangeHandlers[manager.RoomZ[currentRoomNumber]] = volumeHandler;
                //populate the music sharing page with music status of all rooms
                for (ushort j = 0; j < manager.touchpanelZ[TPNumber].MusicRoomsToShareSourceTo.Count; j++)
                {
                    //get the room
                    ushort roomNum = manager.touchpanelZ[TPNumber].MusicRoomsToShareSourceTo[j];
                    var rm = manager.RoomZ[roomNum];
                    //get the current music source number of each room in the sharing list
                    ushort currentMusicSource = manager.RoomZ[manager.touchpanelZ[TPNumber].MusicRoomsToShareSourceTo[j]].CurrentMusicSrc;
                    //populate the room names
                    if (manager.touchpanelZ[TPNumber].HTML_UI)
                    {
                        manager.touchpanelZ[TPNumber]._HTMLContract.MusicRoomControl[j].musicZoneName(
                            (sig, wh) => sig.StringValue = manager.RoomZ[manager.touchpanelZ[TPNumber].MusicRoomsToShareSourceTo[j]].Name);//room name
                        if (currentMusicSource > 0) {
                            manager.touchpanelZ[TPNumber]._HTMLContract.MusicRoomControl[j].musicZoneSource(
                                (sig, wh) => sig.StringValue = manager.MusicSourceZ[currentMusicSource].Name);//room current source
                            manager.touchpanelZ[TPNumber]._HTMLContract.MusicRoomControl[j].musicVolEnable(
                                (sig, wh) => sig.BoolValue = true);//enable the volume buttons
                            manager.touchpanelZ[TPNumber]._HTMLContract.MusicRoomControl[j].musicVolume(
                                (sig, wh) => sig.UShortValue = rm.MusicVolume);//room current volume
                            manager.touchpanelZ[TPNumber]._HTMLContract.MusicRoomControl[j].musicZoneMuted(
                                (sig, wh) => sig.BoolValue = rm.MusicMuted);
                        }
                        else {
                            manager.touchpanelZ[TPNumber]._HTMLContract.MusicRoomControl[j].musicZoneSource(
                                (sig, wh) => sig.StringValue = "Off");//room current source
                            manager.touchpanelZ[TPNumber]._HTMLContract.MusicRoomControl[j].musicVolEnable(
                                (sig, wh) => sig.BoolValue = false);//disable the volume buttons
                        }
                    }
                    else
                    {
                        manager.touchpanelZ[TPNumber].UserInterface.SmartObjects[7].StringInput[(ushort)(2 * j + 11)].StringValue = BuildHTMLString(TPNumber, manager.RoomZ[manager.touchpanelZ[TPNumber].MusicRoomsToShareSourceTo[j]].Name, "24");
                        if (currentMusicSource > 0)
                        {
                            manager.touchpanelZ[TPNumber].UserInterface.SmartObjects[7].StringInput[(ushort)(2 * j + 12)].StringValue = BuildHTMLString(TPNumber, manager.MusicSourceZ[currentMusicSource].Name, "24");
                            manager.touchpanelZ[TPNumber].UserInterface.SmartObjects[7].BooleanInput[(ushort)(7 * j + 4016)].BoolValue = true;//make the volume buttons visible                                                                                                           
                            manager.touchpanelZ[TPNumber].UserInterface.SmartObjects[7].UShortInput[(ushort)(1 * j + 11)].UShortValue = rm.MusicVolume;//volume
                            manager.touchpanelZ[TPNumber].UserInterface.SmartObjects[7].BooleanInput[(ushort)(7 * j + 4014)].BoolValue = manager.RoomZ[manager.touchpanelZ[TPNumber].MusicRoomsToShareSourceTo[j]].MusicMuted;

                        }
                        else
                        {
                            manager.touchpanelZ[TPNumber].UserInterface.SmartObjects[7].StringInput[(ushort)(2 * j + 12)].StringValue = BuildHTMLString(TPNumber, "Off", "24");
                            manager.touchpanelZ[TPNumber].UserInterface.SmartObjects[7].BooleanInput[(ushort)(7 * j + 4016)].BoolValue = false;//make the volume buttons hidden
                        }
                    }


                    //subscribe to mute change events
                    SubscribeToVolMuteChange(rm, manager.touchpanelZ[TPNumber], j);
                }
                if (manager.touchpanelZ[TPNumber].HTML_UI)
                {
                    manager.touchpanelZ[TPNumber]._HTMLContract.musicNumberOfRooms.numberOfMusicZones(
                            (sig, wh) => sig.UShortValue = (ushort)manager.touchpanelZ[TPNumber].MusicRoomsToShareSourceTo.Count);//number of rooms available to share to
                }
                else
                {
                    manager.touchpanelZ[TPNumber].UserInterface.SmartObjects[7].UShortInput[3].UShortValue = (ushort)manager.touchpanelZ[TPNumber].MusicRoomsToShareSourceTo.Count;//number of rooms available to share to
                }
                
            }
            else
            {
                manager.touchpanelZ[TPNumber].UserInterface.BooleanInput[1001].BoolValue = false;//clear the source sharing button
            }
        }

        private void SubscribeToVolMuteChange(Room.RoomConfig room, UI.TouchpanelUI touchpanel, int smartGraphicIndex)
        {
            if (touchpanel.HTML_UI)
            {
                EventHandler muteHandler = (sender, e) => manager.touchpanelZ[touchpanel.Number]._HTMLContract.MusicRoomControl[smartGraphicIndex].musicZoneMuted(
                                (sig, wh) => sig.BoolValue = room.MusicMuted);
                EventHandler volumeHandler = (sender, e) => manager.touchpanelZ[touchpanel.Number]._HTMLContract.MusicRoomControl[smartGraphicIndex].musicVolume(
                                (sig, wh) => sig.UShortValue = room.MusicVolume);//room current volume
                room.MusicMutedChanged += muteHandler;
                room.MusicVolumeChanged += volumeHandler;
                touchpanel.MuteChangeHandlers[room] = muteHandler;
                touchpanel.VolumeChangeHandlers[room] = volumeHandler;
            }
            else { 
                EventHandler muteHandler = (sender, e) => touchpanel.UserInterface.SmartObjects[7].BooleanInput[(ushort)(7 * smartGraphicIndex + 4014)].BoolValue = room.MusicMuted;
                EventHandler volumeHandler = (sender, e) => touchpanel.UserInterface.SmartObjects[7].UShortInput[(ushort)(1 * smartGraphicIndex + 11)].UShortValue = room.MusicVolume;
                room.MusicMutedChanged += muteHandler;
                room.MusicVolumeChanged += volumeHandler;
                touchpanel.MuteChangeHandlers[room] = muteHandler;
                touchpanel.VolumeChangeHandlers[room] = volumeHandler;
            }
        }

        public void SelectZone(ushort TPNumber, ushort zoneListButtonNumber, bool selectDefaultSubsystem)
        {
            imageEISC.BooleanInput[(ushort)(TPNumber + 100)].BoolValue = false;//current subsystem is NOT audio
            imageEISC.BooleanInput[TPNumber].BoolValue = false;//current subsystem is NOT video
            manager.touchpanelZ[TPNumber].CurrentSubsystemIsAudio = false;
            manager.touchpanelZ[TPNumber].CurrentSubsystemIsVideo = false;
            ushort currentRoomNumber = 0;
            ushort previousRoomNumber = 0;
            if (manager.touchpanelZ[TPNumber].CurrentRoomNum > 0)
            {
                previousRoomNumber = manager.touchpanelZ[TPNumber].CurrentRoomNum;
            }
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
                UpdateEquipIDsForSubsystems(TPNumber, currentRoomNumber);//from select zone
                climateControl.SyncPanelToClimateZone(TPNumber);//from select zone
                //Update current room image
                string imagePath = (manager.touchpanelZ[TPNumber].IsConnectedRemotely) ? string.Format("http://{0}:{1}/{2}", manager.ProjectInfoZ[0].DDNSAdress, httpPort, manager.RoomZ[currentRoomNumber].ImageURL) : string.Format("http://{0}:{1}/{2}", IPaddress, httpPort, manager.RoomZ[currentRoomNumber].ImageURL);
                if (manager.touchpanelZ[TPNumber].HTML_UI)
                {
                    imagePath = (manager.touchpanelZ[TPNumber].IsConnectedRemotely) ? string.Format("http://{0}:{1}/{2}", manager.ProjectInfoZ[0].DDNSAdress, httpsPort, manager.RoomZ[currentRoomNumber].ImageURL) : string.Format("http://{0}:{1}/{2}", IPaddress, httpsPort, manager.RoomZ[currentRoomNumber].ImageURL);
                }
                //imageEISC.StringInput[(ushort)(TPNumber)].StringValue = imagePath;
                manager.touchpanelZ[TPNumber].UserInterface.StringInput[5].StringValue = imagePath;
                //Update A/V Sources available for this room
                ushort asrcScenarioNum = manager.RoomZ[currentRoomNumber].AudioSrcScenario;

                //ushort currentVSRC = manager.RoomZ[currentRoomNumber].CurrentVideoSrc;
                if (currentMusicSource == 0)
                {
                    musicSystemControl.UpdatePanelToMusicZoneOff(TPNumber);
                }
                //update the MUSIC sources to display for this room
                if (asrcScenarioNum > 0)
                {
                    ushort numASrcs = (ushort)manager.AudioSrcScenarioZ[asrcScenarioNum].IncludedSources.Count;
                    if (manager.touchpanelZ[TPNumber].HTML_UI)
                    {
                        manager.touchpanelZ[TPNumber]._HTMLContract.musicSourceList.numberOfMusicSources(
                                (sig, wh) => sig.UShortValue = numASrcs);//disable the volume buttons
                    }
                    else { 
                        manager.touchpanelZ[TPNumber].UserInterface.SmartObjects[6].UShortInput[4].UShortValue = numASrcs;//number of sources to show
                    }
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
                        //set the font size for iphones
                        //music source name
                       

                        if (manager.touchpanelZ[TPNumber].HTML_UI) 
                        {
                            manager.touchpanelZ[TPNumber]._HTMLContract.musicSourceSelect[i].musicSourceName(
                                (sig, wh) => sig.StringValue = manager.MusicSourceZ[srcNum].Name);//popuplate source names
                            manager.touchpanelZ[TPNumber]._HTMLContract.musicSourceSelect[i].musicSourceIcon(
                                (sig, wh) => sig.StringValue = manager.MusicSourceZ[srcNum].IconHTML);//populate source icons
                        }
                        else
                        {
                            manager.touchpanelZ[TPNumber].UserInterface.SmartObjects[6].StringInput[(ushort)(i + 11)].StringValue = BuildHTMLString(TPNumber, manager.MusicSourceZ[srcNum].Name, "26");
                            manager.touchpanelZ[TPNumber].UserInterface.SmartObjects[6].StringInput[(ushort)(i + 2011)].StringValue = manager.MusicSourceZ[srcNum].IconSerial;
                        }
                        if (useAnalogModes)
                        { 
                            manager.touchpanelZ[TPNumber].UserInterface.UShortInput[(ushort)(i + 211)].UShortValue = manager.MusicSourceZ[srcNum].AnalogModeNumber; 
                        }
                        //Update the current audio source of this room to the panel and highlight the appropriate button
                        if (srcNum == currentMusicSource)
                        {
                            manager.touchpanelZ[TPNumber].musicButtonFB((ushort)(i + 1));//music source button to highlight
                            manager.touchpanelZ[TPNumber].musicPageFlips(manager.MusicSourceZ[srcNum].FlipsToPageNumber);//page to show
                        }
                    }
                }

            
                //do the same for the video sources
                CrestronConsole.PrintLine("current room {0} vsrcscenario {1}", manager.RoomZ[currentRoomNumber].Name, manager.RoomZ[currentRoomNumber].VideoSrcScenario);
                if (manager.RoomZ[currentRoomNumber].VideoSrcScenario > 0) {
                    videoSystemControl.UpdateTPVideoMenu(TPNumber);//from select zone
                }
                UpdateRoomOptions(TPNumber);//from select zone
                videoSystemControl.UpdateDisplaysAvailableForSelection(TPNumber, currentRoomNumber);

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
                musicSystemControl.UpdateTPMusicMenu((ushort)(TPNumber));//from select zone
                //UpdatePanelHVACTextInSubsystemList(TPNumber);
                //UpdatePanelSubsystemText(TPNumber);//from zone select
                try
                {
                    manager.touchpanelZ[TPNumber].SubscribeToMusicMenuEvents(currentRoomNumber);//from select zone
                }
                catch (KeyNotFoundException ex)
                {
                    CrestronConsole.PrintLine("Warning: Error in SubscribeToMusicMenuEvents for room {0}: {1}",
                        currentRoomNumber, ex.Message);
                }

                try
                {
                    manager.touchpanelZ[TPNumber].SubscribeToVideoMenuEvents(currentRoomNumber);//from select zone
                }
                catch (KeyNotFoundException ex)
                {
                    CrestronConsole.PrintLine("Warning: Error in SubscribeToVideoMenuEvents for room {0}: {1}",
                        currentRoomNumber, ex.Message);
                }//UpdatePanelSubsystemText(TPNumber);//from select zone
                manager.touchpanelZ[TPNumber].SubscribeToRoomSubsystemEvents(currentRoomNumber, previousRoomNumber);//from selectZone

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



        //this function returns the index of the subsystem in the wholeHouseSubsystem scenarios list of rooms
        public ushort GetWholeHouseSubsystemIndex(ushort TPNumber) {
            ushort index = 0;
            ushort wholeHouseScenarioNum = manager.touchpanelZ[TPNumber].HomePageScenario;
            ushort subsystemNumber = manager.touchpanelZ[TPNumber].CurrentSubsystemNumber;
            ushort numSubsystems = (ushort)this.config.RoomConfig.WholeHouseSubsystemScenarios[wholeHouseScenarioNum - 1].WholeHouseSubsystems.Count;
            for (ushort i = 0; i < numSubsystems; i++)
            {
                if (this.config.RoomConfig.WholeHouseSubsystemScenarios[wholeHouseScenarioNum - 1].WholeHouseSubsystems[i].SubsystemNumber == subsystemNumber)
                { index = i; }
            }

            return index;
        }
        public void BuildWholeHouseRoomListFromSubsys(ushort TPNumber)
        {
            ushort subsystemNumber = manager.touchpanelZ[TPNumber].CurrentSubsystemNumber;
            ushort floorNumber = manager.touchpanelZ[TPNumber].CurrentFloorNum;

            if (subsystemNumber > 0 && floorNumber > 0)
            {
                manager.touchpanelZ[TPNumber].WholeHouseRoomList.Clear();
                ushort numRooms = (ushort)manager.Floorz[floorNumber].IncludedRooms.Count;
                for (ushort i = 0; i < numRooms; i++)
                {
                    ushort roomNumber = manager.Floorz[floorNumber].IncludedRooms[i];
                    ushort subsysScenarioNum = manager.RoomZ[roomNumber].SubSystemScenario;
                    if (manager.SubsystemScenarioZ[subsysScenarioNum].IncludedSubsystems.Contains(subsystemNumber))
                    {
                        manager.touchpanelZ[TPNumber].WholeHouseRoomList.Add(roomNumber);
                    }
                }
            }
        }
        //this function would be called when you are on the home page and select lights or climate which then brings up a list of rooms
        public void WholeHouseUpdateZoneList(ushort TPNumber)
        {

            if (logging) { CrestronConsole.PrintLine("&&& start WholeHouseUpdateZoneList TP-{0} {1}:{2}", TPNumber, DateTime.Now.Second, DateTime.Now.Millisecond); }
            BuildWholeHouseRoomListFromSubsys(TPNumber);

            manager.touchpanelZ[TPNumber].SubscribeToWholeHouseListEvents();

            if (logging) { CrestronConsole.PrintLine("&&& end WholeHouseUpdateZoneList TP-{0} {1}:{2}", TPNumber, DateTime.Now.Second, DateTime.Now.Millisecond); }
        }

        /// <summary>
        /// This function sets boolean flags on the touchpanel to indicate the type of the current subsystem
        /// for example 'CurrentSubsystemIsLights', 'CurrentSubsystemIsClimate', etc.
        /// </summary>
        /// <param name="TPNumber"></param>
        public void SetTPCurrentSubsystemBools(ushort TPNumber)
        {
            try
            {
                ushort currentRoomNum = manager.touchpanelZ[TPNumber].CurrentRoomNum;
                ushort subsystemNumber = manager.touchpanelZ[TPNumber].CurrentSubsystemNumber;
                // Check if subsystemNumber is valid before accessing the dictionary
                if (subsystemNumber == 0 || !manager.SubsystemZ.ContainsKey(subsystemNumber))
                {
                    CrestronConsole.PrintLine("SetCurrentSubsystem: Invalid subsystem number {0} for touchpanel {1}",
                        subsystemNumber, TPNumber);
                    return; // Return early to avoid the exception
                }

                // Reset all subsystem flags
                manager.touchpanelZ[TPNumber].CurrentSubsystemIsLights = false;
                manager.touchpanelZ[TPNumber].CurrentSubsystemIsClimate = false;
                manager.touchpanelZ[TPNumber].CurrentSubsystemIsAudio = false;
                manager.touchpanelZ[TPNumber].CurrentSubsystemIsVideo = false;
                manager.touchpanelZ[TPNumber].CurrentSubsystemIsShades = false;



                // Go through all included subsystems in the current scenario
                for (ushort i = 0; i < manager.SubsystemZ.Count; i++)
                {

                    string subsystemName = manager.SubsystemZ[subsystemNumber].Name.ToUpper();

                    // Set the appropriate flag based on subsystem name
                    if (subsystemName == "VIDEO")
                    {
                        manager.touchpanelZ[TPNumber].CurrentSubsystemIsVideo = true;
                    }
                    else if (subsystemName == "AUDIO" || subsystemName == "MUSIC")
                    {
                        manager.touchpanelZ[TPNumber].CurrentSubsystemIsAudio = true;
                    }
                    else if (subsystemName == "HVAC" || subsystemName == "CLIMATE")
                    {
                        manager.touchpanelZ[TPNumber].CurrentSubsystemIsClimate = true;
                        manager.touchpanelZ[TPNumber].CurrentClimateID = manager.RoomZ[currentRoomNum].ClimateID;
                    }
                    else if (subsystemName == "LIGHTS" || subsystemName == "LIGHTING")
                    {
                        manager.touchpanelZ[TPNumber].CurrentSubsystemIsLights = true;
                    }
                    else if (subsystemName == "SHADES" || subsystemName == "WINDOWS" || subsystemName == "BLINDS" || subsystemName == "DRAPES")
                    {
                        manager.touchpanelZ[TPNumber].CurrentSubsystemIsShades = true;
                    }

                    // Once we've found the matching subsystem, we can break
                    break;
                }
            }
            catch (Exception ex)
            {
                ErrorLog.Error("Error in SetCurrentSubsystem: {0}", ex.Message);
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
                    subsystemNumber = this.config.RoomConfig.WholeHouseSubsystemScenarios[homePageScenario-1].WholeHouseSubsystems[subsystemButtonNumber].SubsystemNumber;
                    var includedFloors = manager.WholeHouseSubsystemScenarioZ[homePageScenario].WholeHouseSubsystems[subsystemButtonNumber].IncludedFloors;
                    ushort currentFloor = manager.touchpanelZ[TPNumber].CurrentFloorNum;
                    ushort floorButtonNumber = 1;
                    if (includedFloors.Count == 0 || (includedFloors.Count == 1 && includedFloors[0] == 0))
                    {
                        // No floor selection needed - show all rooms or skip floor logic
                        floorButtonNumber = 0; // Indicates no floor button to highlight
                    }
                    else if (!includedFloors.Contains(currentFloor) && includedFloors.Count > 0)
                    {
                        // Current floor not in the list, select the first available floor
                        manager.touchpanelZ[TPNumber].CurrentFloorNum = includedFloors[0];
                        floorButtonNumber = 1;
                    }
                    else
                    {
                        // Find the button number for the current floor
                        floorButtonNumber = (ushort)(includedFloors.IndexOf(currentFloor) + 1);
                    }
                    manager.touchpanelZ[TPNumber].CurrentSubsystemNumber = subsystemNumber; //store this in the panel. 

                    if (manager.touchpanelZ[TPNumber].HTML_UI)
                    {
                        // Only populate floor list if there are multiple valid floors (not just [0])
                        if (includedFloors.Count > 1 || (includedFloors.Count == 1 && includedFloors[0] != 0))
                        {
                            CrestronConsole.PrintLine("populate floor list HTML UI");
                            manager.touchpanelZ[TPNumber]._HTMLContract.FloorList.NumberOfFloors(
                                    (sig, wh) => sig.UShortValue = (ushort)includedFloors.Count);
                            CrestronConsole.PrintLine("set number of floors to {0}", (ushort)includedFloors.Count);

                            for (int i = 0; i < includedFloors.Count; i++)
                            {
                                ushort floorNum = includedFloors[i];
                                manager.touchpanelZ[TPNumber]._HTMLContract.FloorSelect[i].FloorName(
                                    (sig, wh) => sig.StringValue = manager.Floorz[floorNum].Name);
                            }
                            CrestronConsole.PrintLine("finished populate floor list HTML UI");
                        }
                        else
                        {
                            CrestronConsole.PrintLine("Skipping floor list - only 1 floor or no floor filter [0]");
                        }
                    }
                    else
                    {
                        // Only set number of floors if there are multiple valid floors
                        if (includedFloors.Count > 1 || (includedFloors.Count == 1 && includedFloors[0] != 0))
                        {
                            manager.touchpanelZ[TPNumber].UserInterface.SmartObjects[3].UShortInput[4].UShortValue = (ushort)includedFloors.Count;
                        }
                    }

                    if (floorButtonNumber > 0)
                    {
                        manager.touchpanelZ[TPNumber].floorButtonFB(floorButtonNumber);
                    }
                    SetTPCurrentSubsystemBools(TPNumber);//from select subsystem HOME- WHOLE HOUSE
                    WholeHouseUpdateZoneList(TPNumber);//from select subsystem HOME - WHOLE HOUSE
                    SendSubsystemZonesPageNumber(TPNumber, false);//from select subsystem HOME - WHOLE HOUSE
                    //CrestronConsole.PrintLine("3 TP-{0} after SendSubsystemZonesPageNumber", TPNumber);
                }
                else//if we are on the room page we want to show the control sub
                {
                    for (ushort i = 1; i <= manager.SubsystemZ.Count; i++)
                    {
                        if (manager.SubsystemZ[i].Name.ToUpper() == "VIDEO")
                        {
                            videoIsSystemNumber = i;
                            manager.touchpanelZ[TPNumber].CurrentSubsystemIsVideo = true;//set the current subsystem to video
                            manager.touchpanelZ[TPNumber].UserInterface.BooleanInput[70].BoolValue = manager.RoomZ[currentRoomNum].LiftGoWithOff;//update lift status button.
                            UpdateVideoDisplayList(TPNumber);


                        }
                        else if (manager.SubsystemZ[i].Name.ToUpper() == "AUDIO" || manager.SubsystemZ[i].Name.ToUpper() == "MUSIC")
                        {
                            audioIsSystemNumber = i;
                            manager.touchpanelZ[TPNumber].UnsubscribeTouchpanelFromAllVolMuteChanges();//from selectSubsystem
                            //subsribe to volume feedback for the room
                            manager.touchpanelZ[TPNumber].UserInterface.UShortInput[2].UShortValue = manager.RoomZ[currentRoomNum].MusicVolume;
                            manager.touchpanelZ[TPNumber].UserInterface.BooleanInput[1009].BoolValue = manager.RoomZ[currentRoomNum].MusicMuted;
                            EventHandler volumeHandler = (sender, e) => manager.touchpanelZ[TPNumber].UserInterface.UShortInput[2].UShortValue = manager.RoomZ[currentRoomNum].MusicVolume;
                            manager.RoomZ[currentRoomNum].MusicVolumeChanged += volumeHandler;
                            manager.touchpanelZ[TPNumber].VolumeChangeHandlers[manager.RoomZ[currentRoomNum]] = volumeHandler;
                        }
                        else if (manager.SubsystemZ[i].Name.ToUpper() == "CLIMATE" || manager.SubsystemZ[i].Name.ToUpper() == "HVAC")
                        {
                            // Create a timer to update the HVAC setpoint after a short delay
                            new CTimer(o => {
                                // Capture these values for use inside the timer callback
                                ushort tpNumber = TPNumber;
                                ushort roomNum = currentRoomNum;

                                // Check if the panel and room are still valid
                                if (manager.touchpanelZ.ContainsKey(tpNumber) &&
                                    manager.RoomZ.ContainsKey(roomNum) &&
                                    manager.RoomZ[roomNum].CurrentSetpoint > 0)
                                {
                                    // Update the UI value safely in the timer callback
                                    manager.touchpanelZ[tpNumber].UserInterface.UShortInput[101].UShortValue =
                                        manager.RoomZ[roomNum].CurrentSetpoint;

                                    CrestronConsole.PrintLine("Timer updated setpoint for TP-{0} to {1}",
                                        tpNumber, manager.RoomZ[roomNum].CurrentSetpoint);
                                }
                            }, null, 300); // 300ms delay
                        }
                    }
                    subsystemNumber = manager.SubsystemScenarioZ[currentSubsystemScenario].IncludedSubsystems[subsystemButtonNumber];//get the CURRENT subsystem number for this panel                                                                                                                      //update the panel and room current subsystem
                    manager.RoomZ[currentRoomNum].CurrentSubsystem = subsystemNumber;//update the room with the current subsystem number
                    manager.touchpanelZ[TPNumber].CurrentSubsystemNumber = subsystemNumber; //store this in the panel. 
                    SetTPCurrentSubsystemBools(TPNumber);//from select subsystem ROOMS
                    if (subsystemNumber == videoIsSystemNumber)
                    {
                        manager.RoomZ[currentRoomNum].LastSystemVid = true;
                        imageEISC.BooleanInput[TPNumber].BoolValue = true;//current subsystem is video
                        imageEISC.BooleanInput[(ushort)(TPNumber + 100)].BoolValue = false;//current subsystem is NOT audio
                        videoSystemControl.UpdateTPVideoMenu(TPNumber);//from select subsystem - if subsystem is video
                        if (manager.RoomZ[currentRoomNum].CurrentVideoSrc == 0) { 
                            manager.touchpanelZ[TPNumber].UserInterface.BooleanInput[53].BoolValue = true;//show the video source list page
                        }
                    }
                    else if (subsystemNumber == audioIsSystemNumber)
                    {
                        manager.RoomZ[currentRoomNum].LastSystemVid = false;
                        imageEISC.BooleanInput[(ushort)(TPNumber + 100)].BoolValue = true;//current subsystem is audio
                        imageEISC.BooleanInput[TPNumber].BoolValue = false;//current subsystem is NOT video
                        //when selecting the music subsystem, display the current music source page
                        ushort currentMusicSrc = manager.RoomZ[currentRoomNum].CurrentMusicSrc;
                        if (currentMusicSrc > 0)
                        {
                            manager.touchpanelZ[TPNumber].musicPageFlips(manager.MusicSourceZ[currentMusicSrc].FlipsToPageNumber);
                            musicEISC1.UShortInput[(ushort)(TPNumber + 100)].UShortValue = manager.MusicSourceZ[currentMusicSrc].Number;//for the media server object router
                            musicEISC1.UShortInput[(ushort)(TPNumber + 300)].UShortValue = manager.MusicSourceZ[currentMusicSrc].EquipID;
                        }
                        else if (manager.RoomZ[currentRoomNum].CurrentVideoSrc == 0)
                        {
                            manager.touchpanelZ[TPNumber].UserInterface.BooleanInput[55].BoolValue = true;//show the music source list page
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
            ushort floorScenario = manager.touchpanelZ[TPNumber].FloorScenario;
            if (new[] { "LIGHTS", "LIGHTING", "CLIMATE", "HVAC", "SHADES", "DRAPES" }
                .Contains(manager.SubsystemZ[currentSub].DisplayName.ToUpper()))
            {
                if (manager.FloorScenarioZ[floorScenario].IncludedFloors.Count > 1)
                { 
                    manager.touchpanelZ[TPNumber].subsystemPageFlips(94);
                }
                else { manager.touchpanelZ[TPNumber].subsystemPageFlips(91); }
                    
            }
            else if (close)
            {
                manager.touchpanelZ[TPNumber].subsystemPageFlips(0);//from SendSubsystemZonesPageNumber
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


        /// <summary>
        /// zone number needs to be greater than 0
        /// </summary>
        /// <param name="zoneNumber"></param>
        /// <param name="srcName"></param>

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


        /// <summary>
        /// in the case that there is only 1 floor available, this will select it. otherwise it does nothing
        /// </summary>
        public void SelectOnlyFloor(ushort TPNumber)
        {
            ushort floorScenarioNum = manager.touchpanelZ[TPNumber].FloorScenario;
            if (manager.FloorScenarioZ[floorScenarioNum].IncludedFloors.Count == 1)
            {
                SelectFloor((ushort)(TPNumber), 1);//FROM SelectOnlyFloor there's only 1 floor in this scenario so select it
            }
        }
        /// <summary>
        /// this will update a panels flipstopagenumber, equipID of the subsystem and it will highlight the subsystem button
        /// </summary>
        /// <param name="TPNumber"></param>
        /// <param name="SubsystemNumber"></param>
        public void SelectSubsystemPage(ushort TPNumber, ushort SubsystemNumber)
        {
            ushort equipID = manager.SubsystemZ[SubsystemNumber].EquipID;
            ushort currentRoomNumber = manager.touchpanelZ[TPNumber].CurrentRoomNum;
            manager.touchpanelZ[TPNumber].CurrentPageNumber = (ushort)(TouchpanelUI.CurrentPageType.SubsystemPage);
            CrestronConsole.PrintLine("select subsystem page {0} currentpageType{1}", TPNumber, manager.touchpanelZ[TPNumber].CurrentPageNumber);
            manager.touchpanelZ[TPNumber].subsystemPageFlips(manager.SubsystemZ[SubsystemNumber].FlipsToPageNumber);//from SelectSubsystemPage
            //if the equipid is 1 or 2 that connects to audio or video. other wise in the 100's its another subsystem.
            if (equipID > 99) { equipID = (ushort)(equipID + TPNumber); }
            subsystemEISC.UShortInput[(ushort)(TPNumber + 200)].UShortValue = equipID;
            manager.RoomZ[currentRoomNumber].CurrentSubsystem = SubsystemNumber; //update the room to the current subsystem
            SetTPCurrentSubsystemBools(TPNumber);//From SelectSubsystemPage
            if (manager.SubsystemZ[SubsystemNumber].Name.ToUpper() == "AUDIO" || manager.SubsystemZ[SubsystemNumber].Name.ToUpper() == "MUSIC")
            {
                imageEISC.BooleanInput[(ushort)(TPNumber + 100)].BoolValue = true;
            }//current subsystem is audio
            else { imageEISC.BooleanInput[(ushort)(TPNumber + 100)].BoolValue = false;}
        }
        /// <summary>
        /// updates the room subsystems
        /// </summary>
        public void UpdateSubsystems(ushort TPNumber )
        {
            ushort currentRoomNumber = manager.touchpanelZ[TPNumber].CurrentRoomNum;
            ushort numberOfSubsystems = (ushort)manager.SubsystemScenarioZ[manager.touchpanelZ[TPNumber].SubSystemScenario].IncludedSubsystems.Count;
            ushort currentSubsystemScenario = manager.touchpanelZ[TPNumber].SubSystemScenario;
            ushort flipToSubsysNumOnSelect = manager.RoomZ[currentRoomNumber].OpenSubsysNumOnRmSelect;
            
            if (currentSubsystemScenario == 0) { currentSubsystemScenario = manager.RoomZ[currentRoomNumber].SubSystemScenario; }//inherit from the room if not defined
            ushort homepageScenario = manager.touchpanelZ[TPNumber].HomePageScenario;

            //Update eisc with current room number / name / number of subsystems for current panel
            manager.touchpanelZ[TPNumber].UserInterface.StringInput[1].StringValue = manager.RoomZ[currentRoomNumber].Name;
            if (manager.touchpanelZ[TPNumber].HTML_UI)
            {
                manager.touchpanelZ[TPNumber]._HTMLContract.SubsystemList.NumberOfSubsystems(
                    (sig, wh) => sig.UShortValue = numberOfSubsystems);
            }
            else
            {
                manager.touchpanelZ[TPNumber].UserInterface.SmartObjects[2].UShortInput[3].UShortValue = numberOfSubsystems;//subsystem select
            }
            
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
                numberOfSubs = (ushort)this.config.RoomConfig.WholeHouseSubsystemScenarios[homePageScenario - 1].WholeHouseSubsystems.Count;
                if (manager.touchpanelZ[TPNumber].HTML_UI)
                {
                    manager.touchpanelZ[TPNumber]._HTMLContract.WholeHouseSubsystemList.numberOfWholeHouseSubsystems(
                        (sig, wh) => sig.UShortValue = numberOfSubs);
                }
                else {
                    manager.touchpanelZ[TPNumber].UserInterface.SmartObjects[14].UShortInput[3].UShortValue = numberOfSubs;//whole house subsystem select
                }
                    
                for (ushort i = 0; i < numberOfSubs; i++)
                {
                    subsystemNum = this.config.RoomConfig.WholeHouseSubsystemScenarios[homePageScenario - 1].WholeHouseSubsystems[i].SubsystemNumber;
                    //CrestronConsole.PrintLine("{0} {1} {2} {3} {4}", i, subsystemNum, manager.SubsystemZ[subsystemNum].Name, (ushort)(2 * i + 12), manager.SubsystemZ[subsystemNum].IconSerial);
                    if (manager.touchpanelZ[TPNumber].HTML_UI) {
                        manager.touchpanelZ[TPNumber]._HTMLContract.WholeHouseSubsystem[i].SubsystemIsSelected(
                            (sig, wh) => sig.BoolValue = false);
                        manager.touchpanelZ[TPNumber]._HTMLContract.WholeHouseSubsystem[i].SubsystemName(
                            (sig, wh) => sig.StringValue = manager.SubsystemZ[subsystemNum].Name);
                        manager.touchpanelZ[TPNumber]._HTMLContract.WholeHouseSubsystem[i].SubsystemIcon(
                            (sig, wh) => sig.StringValue = manager.SubsystemZ[subsystemNum].IconHTML);

                    }
                    else { 
                        manager.touchpanelZ[TPNumber].UserInterface.SmartObjects[14].BooleanInput[(ushort)(i + 4016)].BoolValue = false;//clear the button feedback
                        manager.touchpanelZ[TPNumber].UserInterface.SmartObjects[14].StringInput[(ushort)(2 * i + 11)].StringValue = manager.SubsystemZ[subsystemNum].Name;//whole house subsystem
                        manager.touchpanelZ[TPNumber].UserInterface.SmartObjects[14].StringInput[(ushort)(2 * i + 12)].StringValue = manager.SubsystemZ[subsystemNum].IconSerial;//whole house subsystem icon
                    }
                }
            }
            else 
            {
                numberOfSubs = (ushort)manager.SubsystemScenarioZ[currentSubsystemScenario].IncludedSubsystems.Count;
                ushort currentRoomNumber = manager.touchpanelZ[TPNumber].CurrentRoomNum;
                for (ushort i = 0; i < numberOfSubs; i++)
                {
                    subsystemNum = manager.SubsystemScenarioZ[currentSubsystemScenario].IncludedSubsystems[i];
                    if (manager.touchpanelZ[TPNumber].HTML_UI)
                    {
                        manager.touchpanelZ[TPNumber]._HTMLContract.SubsystemButton[i].SubsystemSelected(
                            (sig, wh) => sig.BoolValue = false);
                        manager.touchpanelZ[TPNumber]._HTMLContract.SubsystemButton[i].SubsystemName(
                            (sig, wh) => sig.StringValue = manager.SubsystemZ[subsystemNum].Name);
                        manager.touchpanelZ[TPNumber]._HTMLContract.SubsystemButton[i].SubsystemIcon(
                            (sig, wh) => sig.StringValue = manager.SubsystemZ[subsystemNum].IconHTML);
                        //update the button number to highlight
                        if (manager.RoomZ[currentRoomNumber].CurrentSubsystem == subsystemNum)
                        {
                            manager.touchpanelZ[TPNumber]._HTMLContract.SubsystemButton[i].SubsystemSelected(
                                (sig, wh) => sig.BoolValue = true);
                        }
                    }
                    else { 
                        manager.touchpanelZ[TPNumber].UserInterface.SmartObjects[2].BooleanInput[(ushort)(i + 4016)].BoolValue = false;//clear the button feedback
                        manager.touchpanelZ[TPNumber].UserInterface.SmartObjects[2].StringInput[(ushort)(3 * i + 11)].StringValue = manager.SubsystemZ[subsystemNum].Name;//subsystem select
                        manager.touchpanelZ[TPNumber].UserInterface.SmartObjects[2].StringInput[(ushort)(3 * i + 13)].StringValue = manager.SubsystemZ[subsystemNum].IconSerial;//subsystem select
                                                                                                                                                                                //update the button number to highlight
                        if (manager.RoomZ[currentRoomNumber].CurrentSubsystem == subsystemNum)
                        {
                            manager.touchpanelZ[TPNumber].UserInterface.SmartObjects[2].BooleanInput[(ushort)(i + 4016)].BoolValue = true;
                        }
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
            if (!manager.touchpanelZ.ContainsKey(TPNumber))
            {
                CrestronConsole.PrintLine("Error: touchpanelZ does not contain key: {0}", TPNumber);
                return;
            }
            manager.touchpanelZ[TPNumber].UserInterface.BooleanInput[91].BoolValue = false; //close the house room list
            manager.touchpanelZ[TPNumber].UserInterface.BooleanInput[94].BoolValue = false; //close the house room list with floors
            manager.touchpanelZ[TPNumber].UserInterface.BooleanInput[50].BoolValue = false; //close the roomlist with floors
            manager.touchpanelZ[TPNumber].UserInterface.BooleanInput[51].BoolValue = false; //close the roomlist without floors

            manager.touchpanelZ[TPNumber].videoPageFlips(0);//from RoomButtonPress
            ushort currentRoom = 0;
            if (TimedOut) { currentRoom = manager.touchpanelZ[TPNumber].DefaultRoom; }
            else { currentRoom = manager.touchpanelZ[TPNumber].CurrentRoomNum; }
            // Validate currentRoom
            if (currentRoom == 0)
            {
                CrestronConsole.PrintLine("Error: TP-{0} has no valid room assigned (currentRoom=0)", TPNumber);
                // Try to fall back to home page instead
                HomeButtonPress(TPNumber);
                return;
            }

            if (!manager.RoomZ.ContainsKey(currentRoom))
            {
                CrestronConsole.PrintLine("Error: Room {0} does not exist in RoomZ for TP-{1}", currentRoom, TPNumber);
                HomeButtonPress(TPNumber);
                return;
            }
            ushort floorNumber = FindOutWhichFloorThisRoomIsOn(TPNumber, currentRoom);
            CrestronConsole.PrintLine("TP-{0} room={1} floorNumber={2} floorScenario={3}",
                TPNumber, currentRoom, floorNumber, manager.touchpanelZ[TPNumber].FloorScenario);

            if (floorNumber == 0)
            {
                CrestronConsole.PrintLine("Error: Could not find floor for room {0} in floorScenario {1} for TP-{2}",
                    currentRoom, manager.touchpanelZ[TPNumber].FloorScenario, TPNumber);
                HomeButtonPress(TPNumber);
                return;
            }

            if (!manager.Floorz.ContainsKey(floorNumber))
            {
                CrestronConsole.PrintLine("Error: Floorz does not contain key: {0}", floorNumber);
                HomeButtonPress(TPNumber);
                return;
            }

            if (!manager.Floorz[floorNumber].IncludedRooms.Contains(currentRoom))
            {
                CrestronConsole.PrintLine("Error: Floor {0} does not include room {1}", floorNumber, currentRoom);
                HomeButtonPress(TPNumber);
                return;
            }
            //calculate the button # in the zone list the room is
            ushort zoneButtonNumber = (ushort)(manager.Floorz[floorNumber].IncludedRooms.IndexOf(currentRoom) + 1);
            manager.touchpanelZ[TPNumber].CurrentPageNumber = (ushort)TouchpanelUI.CurrentPageType.RoomSubsystemList;
            imageEISC.BooleanInput[(ushort)(TPNumber + 100)].BoolValue = false;//current subsystem is NOT audio
            imageEISC.BooleanInput[TPNumber].BoolValue = false;//current subsystem is NOT video
            manager.touchpanelZ[TPNumber].CurrentSubsystemIsVideo = false;
            manager.touchpanelZ[TPNumber].CurrentFloorNum = floorNumber;

            CrestronConsole.PrintLine("TP-{0} calling SelectFloor", TPNumber);
            try
            {
                SelectFloor(TPNumber, 0);
                CrestronConsole.PrintLine("TP-{0} SelectFloor completed", TPNumber);
            }
            catch (Exception ex)
            {
                CrestronConsole.PrintLine("TP-{0} SelectFloor failed: {1}", TPNumber, ex.Message);
                return;
            }

            CrestronConsole.PrintLine("TP-{0} calling SelectZone", TPNumber);
            try
            {
                SelectZone(TPNumber, zoneButtonNumber, TimedOut);
                CrestronConsole.PrintLine("TP-{0} SelectZone completed", TPNumber);
            }
            catch (Exception ex)
            {
                CrestronConsole.PrintLine("TP-{0} SelectZone failed: {1}", TPNumber, ex.Message);
                return;
            }

            CrestronConsole.PrintLine("TP-{0} setting BooleanInput[100] to TRUE", TPNumber);
            manager.touchpanelZ[TPNumber].UserInterface.BooleanInput[100].BoolValue = true; //show the subsystem list page.
            CrestronConsole.PrintLine("TP-{0} BooleanInput[100] is now: {1}", TPNumber, manager.touchpanelZ[TPNumber].UserInterface.BooleanInput[100].BoolValue);
        }
        /// <summary>
        /// this is the list of floors and rooms page
        /// </summary>
        public void RoomListButtonPress(ushort TPNumber)
        {
            manager.touchpanelZ[TPNumber].UserInterface.BooleanInput[91].BoolValue = false; //close the room list
            manager.touchpanelZ[TPNumber].UserInterface.BooleanInput[94].BoolValue = false; //close the room list with floors
            manager.touchpanelZ[TPNumber].CurrentPageNumber = (ushort)TouchpanelUI.CurrentPageType.RoomList;
            imageEISC.BooleanInput[(ushort)(TPNumber + 100)].BoolValue = false;//current subsystem is NOT audio
            manager.touchpanelZ[TPNumber].CurrentSubsystemIsAudio = false;
            manager.touchpanelZ[TPNumber].videoPageFlips(0);//from roomlist buttonpress / close any video menus                                                                   
            if (manager.FloorScenarioZ[manager.touchpanelZ[TPNumber].FloorScenario].IncludedFloors.Count > 1)
            {
                UpdateTPFloorNames(TPNumber);//from roomlistbuttonpress
            }

            imageEISC.BooleanInput[TPNumber].BoolValue = false;//clear "current subsystem is video"
            manager.touchpanelZ[TPNumber].CurrentSubsystemIsVideo = false;
            manager.touchpanelZ[TPNumber].subsystemPageFlips(1000);//1000 is list of rooms sub for flip to page#
            
            //selectfloor with 0 will default to the current floor.
            SelectFloor(TPNumber, 0);//From RoomListButtonPress tpnumber, floorbuttonnumber NOT actual floor number
            subsystemEISC.BooleanInput[(ushort)(TPNumber + 200)].BoolValue = false;
        }
        /// <summary>
        /// sets the image / updates the whole house subsystem list
        /// </summary>
        public void HomeButtonPress(ushort TPNumber) 
        {
            CrestronConsole.PrintLine("TP-{0} homebuttonpress", TPNumber);
            if (manager.touchpanelZ[TPNumber].Type != "Tsr310" && manager.touchpanelZ[TPNumber].Type != "HR310")
            {
                ushort homePageScenario = manager.touchpanelZ[TPNumber].HomePageScenario; //this refers to the wholehousesubsystemscenario
                string  homeImagePath = (manager.touchpanelZ[TPNumber].IsConnectedRemotely) ? string.Format("http://{0}:{1}/HOME.JPG", manager.ProjectInfoZ[0].DDNSAdress, httpPort) : string.Format("http://{0}:{1}/HOME.JPG", IPaddress, httpPort);
                if (manager.touchpanelZ[TPNumber].HTML_UI)
                {
                    homeImagePath = (manager.touchpanelZ[TPNumber].IsConnectedRemotely) ? string.Format("http://{0}:{1}/HOME.JPG", manager.ProjectInfoZ[0].DDNSAdress, httpsPort) : string.Format("http://{0}:{1}/HOME.JPG", IPaddress, httpsPort);
                }
                subsystemEISC.UShortInput[(ushort)(TPNumber + 200)].UShortValue = (ushort)(300 + TPNumber);//home/quickaction equipID is 300
                quickActionControl.RefreshQuickAction(TPNumber);
                manager.touchpanelZ[TPNumber].UserInterface.StringInput[5].StringValue = homeImagePath;
                for (ushort i = 0; i < 10; i++)
                {
                    manager.touchpanelZ[TPNumber].UserInterface.BooleanInput[(ushort)(i + 91)].BoolValue = false;//clear the whole house subsystems.
                }
                //clear the roomlist with floors and roomlist no floors pages
                manager.touchpanelZ[TPNumber].UserInterface.BooleanInput[50].BoolValue = false;//clear the list of rooms page
                manager.touchpanelZ[TPNumber].UserInterface.BooleanInput[51].BoolValue = false;//clear the room list sub w/no floors 
                manager.touchpanelZ[TPNumber].subsystemPageFlips(10000);//flip to page# something out of range of used numbers
                imageEISC.BooleanInput[TPNumber].BoolValue = false;//clear "current subsystem is video"
                manager.touchpanelZ[TPNumber].CurrentSubsystemIsVideo = false;
                manager.touchpanelZ[TPNumber].CurrentPageNumber = 0;// 0 = home 
                //TODO the subsystem status text isn't being updated here. maybe it should be???
                //CrestronConsole.PrintLine("whole house subsystemscenario length{0}", config.RoomConfig.WholeHouseSubsystemScenarios.Length);
                if (homePageScenario > 0 && homePageScenario <= this.config.RoomConfig.WholeHouseSubsystemScenarios.Length) //make sure the homePageScenario isn't out of bounds
                {
                    updateSubsystemListSmartObject(TPNumber, true);//home button press
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

        public void UpdateLightingStatus(ushort KeypadNumber, bool LightsAreOff)
        {
            foreach (var room in manager.RoomZ)
            {
                if (room.Value.LightsID == KeypadNumber)
                {
                    room.Value.LightsAreOff = LightsAreOff;
                    CrestronConsole.PrintLine("room {0} lightsid {1} lightsareoff {2}", room.Value.Name, room.Value.LightsID, room.Value.LightsAreOff);

                }
            }
        }

        /// <summary>
        /// This is for TSR-310 / 302 remotes only.
        /// </summary>
        /// <param name="TPNumber"></param>
        /// <param name="group"></param>
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


        private void InitCompleteCallback(object obj)
        {
            CrestronConsole.PrintLine("##############     INIT COMPLETE CALLBACK {0}:{1}", DateTime.Now.Second, DateTime.Now.Millisecond);
            InitCompleteTimer.Stop();
            InitCompleteTimer.Dispose();
            initComplete = true;
            //InitLightText();
        }


        public void SendReceiverInputTurnOffVideo(ushort videoSwitcherOutputNum, ushort asrcScenario, ushort musicSourceNumber)
        {
           
        }


        public bool is8ZoneBox(ushort roomNumber)
        {
            bool isEightZoneBox = false;
            if (manager.RoomZ[roomNumber].NAXBoxNumber > 0)
            {
                if (manager.NAXBoxZ[manager.RoomZ[roomNumber].NAXBoxNumber].Type.ToUpper().Contains("8"))
                {
                    isEightZoneBox = true;
                }
            }
            return isEightZoneBox;
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
            videoSystemControl.UpdateRoomVideoStatusText(dmOutNumber, sourceNumber);

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
                    videoSystemControl.UpdateTPVideoMenu(j);
                }
            }
        }
        public void UpdateRoomAVConfig()
        {
            ushort videoOutNumber = 0;
            ushort roomNumber = 0;
            CrestronConsole.PrintLine("Updating Room AV Configurations");
            for (ushort i = 1; i <= manager.VideoDisplayZ.Count; i++)
            {
                ushort vidConfigNum = manager.VideoDisplayZ[i].VidConfigurationScenario;
                CrestronConsole.PrintLine("Room#{0} vidconfignum{1}", i, vidConfigNum);
                if (vidConfigNum > 0)
                {
                    videoOutNumber = manager.VideoDisplayZ[i].VideoOutputNum;
                    CrestronConsole.PrintLine("Room#{0} videoOutNumber{1}", i, videoOutNumber);
                    if (videoOutNumber > 0)
                    {
                        roomNumber = manager.VideoDisplayZ[i].AssignedToRoomNum;
                        //BOOLEANS
                        videoEISC3.BooleanInput[i].BoolValue = manager.VideoConfigScenarioZ[vidConfigNum].HasReceiver;
                        CrestronConsole.PrintLine("Room#{0} hasRec={1} vidconfignum{2}", i, manager.VideoConfigScenarioZ[vidConfigNum].HasReceiver, vidConfigNum);
                        videoEISC3.BooleanInput[(ushort)(i + 100)].BoolValue = manager.VideoConfigScenarioZ[vidConfigNum].ReceiverHasVolFB;
                        videoEISC3.BooleanInput[(ushort)(i + 200)].BoolValue = manager.VideoConfigScenarioZ[vidConfigNum].MusicHasVolFB;
                        videoEISC3.BooleanInput[(ushort)(i + 300)].BoolValue = manager.VideoConfigScenarioZ[vidConfigNum].TvHasVolFB;
                        CrestronConsole.PrintLine("{0} TvHasVolFB={1} vidconfignum{2}", manager.VideoDisplayZ[i].DisplayName, manager.VideoConfigScenarioZ[vidConfigNum].TvHasVolFB, vidConfigNum);
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
            CrestronConsole.PrintLine("~~~~~~~~~~~~~~StartupRooms called~~~~~~~~~~~~~~");
            HomePageMusicRooms.Clear();
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
                }
                if (room.Value.AudioID > 0)
                {
                    HomePageMusicRooms.Add(room.Value.Number);
                    room.Value.MusicSrcStatusChanged += (musicSrc, flipsToPage, equipID, name, buttonNum) =>
                    {
                        musicSystemControl.HomePageMusicStatusText();//from startup rooms
                    };
                }
            }
            //set the video source scenario and other settings for each room
            foreach (var display in manager.VideoDisplayZ)
            {
                CrestronConsole.PrintLine("display{0} assigned to room{1} {2}", display.Value.DisplayName, display.Value.AssignedToRoomNum, manager.RoomZ[display.Value.AssignedToRoomNum].Name);
                manager.RoomZ[display.Value.AssignedToRoomNum].VideoSrcScenario = display.Value.VideoSourceScenario;
                //CrestronConsole.PrintLine("~~~~~~~~~~~~~~vsrc scenario{0}", manager.RoomZ[display.Value.AssignedToRoomNum].VideoSrcScenario);
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
                    RoomButtonPress(tp.Value.Number, true);//from startup rooms
                }
                else {
                    HomeButtonPress(tp.Value.Number);//from startup rooms
                }
            }
        }
        /// <summary>
        /// Initializes the Home Page Music Zones for HTML touchpanels
        /// Should be called after HomePageMusicRooms is populated
        /// </summary>
        public void InitializeHomePageMusicZonesForHTML(ushort TPNumber)
        {
            if (!manager.touchpanelZ.ContainsKey(TPNumber) || !manager.touchpanelZ[TPNumber].HTML_UI)
                return;

            var tp = manager.touchpanelZ[TPNumber];
            ushort numberOfZones = (ushort)HomePageMusicRooms.Count;

            tp._HTMLContract.HomeNumberOfMusicZones.NumberOfMusicZones(
                (sig, wh) => sig.UShortValue = numberOfZones);

            for (int i = 0; i < numberOfZones && i < tp._HTMLContract.HomeMusicZone.Length; i++)
            {
                int capturedIndex = i;
                ushort roomNumber = HomePageMusicRooms[i];
                var room = manager.RoomZ[roomNumber];

                // Set INITIAL visibility based on whether music is currently playing
                bool isPlaying = room.CurrentMusicSrc > 0;
                tp._HTMLContract.HomeMusicZone[capturedIndex].isVisible(
                    (sig, wh) => sig.BoolValue = isPlaying);

                tp._HTMLContract.HomeMusicZone[capturedIndex].ZoneName(
                    (sig, wh) => sig.StringValue = room.Name);

                string sourceName = "Off";
                if (room.CurrentMusicSrc > 0 && manager.MusicSourceZ.ContainsKey(room.CurrentMusicSrc))
                {
                    sourceName = manager.MusicSourceZ[room.CurrentMusicSrc].Name;
                }
                tp._HTMLContract.HomeMusicZone[capturedIndex].CurrentSource(
                    (sig, wh) => sig.StringValue = sourceName);

                tp._HTMLContract.HomeMusicZone[capturedIndex].Volume(
                    (sig, wh) => sig.UShortValue = room.MusicVolume);

                tp._HTMLContract.HomeMusicZone[capturedIndex].isMuted(
                    (sig, wh) => sig.BoolValue = room.MusicMuted);

                // Subscribe to music source changes to update visibility dynamically
                ushort capturedRoomNumber = roomNumber;
                ushort capturedTPNumber = TPNumber;

                room.MusicSrcStatusChanged += (musicSrc, flipsToPage, equipID, name, buttonNum) =>
                {
                    UpdateHomeMusicZoneForRoom(capturedTPNumber, capturedIndex, capturedRoomNumber);
                };

                // Subscribe to volume changes
                room.MusicVolumeChanged += (sender, e) =>
                {
                    if (manager.touchpanelZ.ContainsKey(capturedTPNumber) &&
                        manager.touchpanelZ[capturedTPNumber].HTML_UI &&
                        capturedIndex < manager.touchpanelZ[capturedTPNumber]._HTMLContract.HomeMusicZone.Length)
                    {
                        manager.touchpanelZ[capturedTPNumber]._HTMLContract.HomeMusicZone[capturedIndex].Volume(
                            (sig, wh) => sig.UShortValue = room.MusicVolume);
                    }
                };

                // Subscribe to mute changes
                room.MusicMutedChanged += (sender, e) =>
                {
                    if (manager.touchpanelZ.ContainsKey(capturedTPNumber) &&
                        manager.touchpanelZ[capturedTPNumber].HTML_UI &&
                        capturedIndex < manager.touchpanelZ[capturedTPNumber]._HTMLContract.HomeMusicZone.Length)
                    {
                        manager.touchpanelZ[capturedTPNumber]._HTMLContract.HomeMusicZone[capturedIndex].isMuted(
                            (sig, wh) => sig.BoolValue = room.MusicMuted);
                    }
                };
            }
        }
        /// <summary>
        /// Updates a specific Home Music Zone when its music source changes
        /// </summary>
        private void UpdateHomeMusicZoneForRoom(ushort TPNumber, int zoneIndex, ushort roomNumber)
        {
            if (!manager.touchpanelZ.ContainsKey(TPNumber) || !manager.touchpanelZ[TPNumber].HTML_UI)
                return;

            var tp = manager.touchpanelZ[TPNumber];
            if (zoneIndex >= tp._HTMLContract.HomeMusicZone.Length)
                return;

            if (!manager.RoomZ.ContainsKey(roomNumber))
                return;

            var room = manager.RoomZ[roomNumber];
            bool isPlaying = room.CurrentMusicSrc > 0;

            // Update visibility - show only if music is playing
            tp._HTMLContract.HomeMusicZone[zoneIndex].isVisible(
                (sig, wh) => sig.BoolValue = isPlaying);

            // Update current source name
            string sourceName = "Off";
            if (isPlaying && manager.MusicSourceZ.ContainsKey(room.CurrentMusicSrc))
            {
                sourceName = manager.MusicSourceZ[room.CurrentMusicSrc].Name;
            }
            tp._HTMLContract.HomeMusicZone[zoneIndex].CurrentSource(
                (sig, wh) => sig.StringValue = sourceName);

            // Update volume and mute state
            tp._HTMLContract.HomeMusicZone[zoneIndex].Volume(
                (sig, wh) => sig.UShortValue = room.MusicVolume);
            tp._HTMLContract.HomeMusicZone[zoneIndex].isMuted(
                (sig, wh) => sig.BoolValue = room.MusicMuted);
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
                    
                }
                else if (subName.ToUpper().Contains("CLIMATE") || subName.ToUpper().Contains("HVAC")) {
                    //statusText = GetHVACStatusText(roomNumber, TPNumber);
                }
                else
                {
                    statusText = "";
                }
                if (manager.touchpanelZ[TPNumber].HTML_UI)
                {
                    manager.touchpanelZ[TPNumber]._HTMLContract.SubsystemButton[i].SubsystemStatus(
                        (sig, wh) => sig.StringValue = statusText);
                }
                else
                {
                    manager.touchpanelZ[TPNumber].UserInterface.SmartObjects[2].StringInput[(ushort)(3 * i + 12)].StringValue = statusText;
                }
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


        /// <summary>
        ///this function translates the button number pressed to the desired subsystem to be included in the selected quick action
        /// </summary>




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
                CreateAndRegisterEISCs();
                CrestronConsole.PrintLine("EISC setup complete");
                IPaddress = CrestronEthernetHelper.GetEthernetParameter(CrestronEthernetHelper.ETHERNET_PARAMETER_TO_GET.GET_CURRENT_IP_ADDRESS, CrestronEthernetHelper.GetAdapterdIdForSpecifiedAdapterType(EthernetAdapterType.EthernetLANAdapter));
                httpPort = CrestronEthernetHelper.GetEthernetParameter(CrestronEthernetHelper.ETHERNET_PARAMETER_TO_GET.GET_WEB_PORT, CrestronEthernetHelper.GetAdapterdIdForSpecifiedAdapterType(EthernetAdapterType.EthernetLANAdapter));
                httpsPort = CrestronEthernetHelper.GetEthernetParameter(CrestronEthernetHelper.ETHERNET_PARAMETER_TO_GET.GET_SECURE_WEB_PORT, CrestronEthernetHelper.GetAdapterdIdForSpecifiedAdapterType(EthernetAdapterType.EthernetLANAdapter));
                CrestronConsole.PrintLine("IP address: {0} HTTP port: {1} HTTPS port: {2}", IPaddress, httpPort, httpsPort);
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
                    CrestronConsole.PrintLine("tpNum: {0}", tpNum);
                    if (manager.touchpanelZ[tpNum].ChangeRoomButtonEnable)
                    {
                        manager.touchpanelZ[tpNum].UserInterface.BooleanInput[49].BoolValue = true;//show the change room button.
                        manager.touchpanelZ[tpNum].UserInterface.StringInput[6].StringValue = manager.ProjectInfoZ[0].ProjectName;
                        //update number of quick actions and their names
                        if (manager.touchpanelZ[tpNum].HTML_UI)
                        {
                            //TODO update HTML quick action names and numbers
                        }
                        else
                        {
                            manager.touchpanelZ[tpNum].UserInterface.SmartObjects[15].UShortInput[4].UShortValue = (ushort)quickActionXML.NumberOfPresets;
                        }
                    }
                }
                if (NAXsystem)
                {
                    CrestronConsole.PrintLine("this system has NAX ---------------------------");
                }
                else
                {
                    CrestronConsole.PrintLine("this is not an NAX system---------------------");
                }


                StartupRooms();//this sets last system vid to true if no audio is found
                
                UpdateRoomAVConfig();
                
                foreach (var tp in manager.touchpanelZ)
                {
                    ushort TPNumber = tp.Value.Number;
                    UpdateRoomOptions(TPNumber);//from initializeSystem
                }
                
                //update number of quick actions and their names
                for (ushort i = 0; i <= quickActionXML.NumberOfPresets; i++)
                {
                    foreach (var tp in manager.touchpanelZ)
                    {
                        if (tp.Value.HTML_UI)
                        {
                            //TODO update HTML quick action names
                        }
                        else { 
                            tp.Value.UserInterface.SmartObjects[15].StringInput[(ushort)(i + 1)].StringValue = quickActionXML.PresetName[i];
                        }
                    }
                }
                
                subsystemEISC.BooleanInput[1].BoolValue = true;// tell the av program that this program has loaded
                InitCompleteTimer = new CTimer(InitCompleteCallback, 0, 20000);
            }
            catch (Exception e)
            {
                CrestronConsole.PrintLine("Error in InitializeSystem: {0}", e.Message);
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

            string configFilePath = this.config.FindLatestConfigFile();

            // If no config file was found, fall back to the default path
            if (string.IsNullOrEmpty(configFilePath))
            {
                configFilePath = @"\nvram\ACSconfig.json";
                CrestronConsole.PrintLine("No config files found, falling back to default: {0}", configFilePath);
            }

            // Try to read the config
            if (this.config.ReadConfig(configFilePath, true))
            {
                CrestronConsole.PrintLine("read config - starting manager");
                this.manager = new SystemManager(this.config.RoomConfig, this);
                CrestronConsole.PrintLine("read config ok");
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

    }
}