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
    /// <summary>
    /// Main ControlSystem class - split into partial classes for maintainability.
    /// - ControlSystem.cs: Core fields, constructor, initialization
    /// - ControlSystem.ConsoleCommands.cs: Console command methods
    /// - ControlSystem.SigHandlers.cs: EISC signal change handlers
    /// - ControlSystem.Navigation.cs: Floor/Zone/Room navigation
    /// - ControlSystem.Subsystems.cs: Subsystem selection and updates
    /// - ControlSystem.HomePageMusic.cs: Home page music zone methods
    /// </summary>
    public partial class ControlSystem : CrestronControlSystem
    {
        #region Fields

        public ThreeSeriesTcpIpEthernetIntersystemCommunications roomSelectEISC, subsystemEISC, musicEISC1, musicEISC2, musicEISC3, videoEISC1, videoEISC2, videoEISC3, imageEISC;
        public ThreeSeriesTcpIpEthernetIntersystemCommunications VOLUMEEISC, HVACEISC, lightingEISC, subsystemControlEISC, securityEISC;
        private Configuration.ConfigManager config;
        public QuickActions.QuickActionXML quickActionXML;
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
        public List<ushort> HomePageMusicRooms = new List<ushort>();
        public bool logging = false;

        public CTimer InitCompleteTimer;
        public static Timer xtimer;

        public ushort lastMusicSrc, lastSwitcherInput, lastSwitcherOutput;
        public CrestronApp app;

        #endregion

        #region Constructor

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

                roomSelectEISC.SigChange += new SigEventHandler(MainsigChangeHandler);
                subsystemEISC.SigChange += new SigEventHandler(SubsystemSigChangeHandler);
                musicEISC1.SigChange += new SigEventHandler(musicSigChange.Music1SigChangeHandler);
                musicEISC2.SigChange += new SigEventHandler(musicSigChange.Music2SigChangeHandler);
                musicEISC3.SigChange += new SigEventHandler(musicSigChange.Music3SigChangeHandler);
                videoEISC1.SigChange += new SigEventHandler(videoSigChange.Video1SigChangeHandler);
                videoEISC2.SigChange += new SigEventHandler(videoSigChange.Video2SigChangeHandler);
                videoEISC3.SigChange += new SigEventHandler(videoSigChange.Video3SigChangeHandler);
                imageEISC.SigChange += new SigEventHandler(ImageSigChangeHandler);

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

                CrestronEnvironment.SystemEventHandler += new SystemEventHandler(ControlSystem_ControllerSystemEventHandler);
                CrestronEnvironment.ProgramStatusEventHandler += new ProgramStatusEventHandler(ControlSystem_ControllerProgramEventHandler);
                CrestronEnvironment.EthernetEventHandler += new EthernetEventHandler(ControlSystem_ControllerEthernetEventHandler);
                this.appID = InitialParametersClass.ApplicationNumber;

                // Register console commands
                RegisterConsoleCommands();

                CrestronConsole.PrintLine("starting program {0}", this.ProgramNumber);
            }
            catch (Exception e)
            {
                ErrorLog.Error("Error in the constructor: {0}", e.Message);
            }
        }

        private void RegisterConsoleCommands()
        {
            CrestronConsole.AddNewConsoleCommand(ReinitializeSystem, "reloadjson", "reload the json file", ConsoleAccessLevelEnum.AccessOperator);
            CrestronConsole.AddNewConsoleCommand(TestingPageNumber, "currentpage", "set the page number for all panels", ConsoleAccessLevelEnum.AccessOperator);
            CrestronConsole.AddNewConsoleCommand(ReportHVAC, "reporthvac", "show current temps for all rooms", ConsoleAccessLevelEnum.AccessOperator);
            CrestronConsole.AddNewConsoleCommand(ReportQuickAction, "reportquick", "show sources for quick action", ConsoleAccessLevelEnum.AccessOperator);
            CrestronConsole.AddNewConsoleCommand(TestWriteXML, "testwrite", "test writing to the xml file", ConsoleAccessLevelEnum.AccessOperator);
            CrestronConsole.AddNewConsoleCommand(ReportIP, "reportip", "report ip information", ConsoleAccessLevelEnum.AccessOperator);
            CrestronConsole.AddNewConsoleCommand(ReportHome, "reporthome", "report home image path", ConsoleAccessLevelEnum.AccessOperator);
            CrestronConsole.AddNewConsoleCommand(numFloors, "numFloors", "change the number of floors", ConsoleAccessLevelEnum.AccessOperator);
            CrestronConsole.AddNewConsoleCommand(numZones, "numZones", "change the number of zones", ConsoleAccessLevelEnum.AccessOperator);
            CrestronConsole.AddNewConsoleCommand(StartupPanelCommand, "startuppanels", "startup the panels", ConsoleAccessLevelEnum.AccessOperator);
            CrestronConsole.AddNewConsoleCommand(EnableLogging, "logging", "enable or disable logging", ConsoleAccessLevelEnum.AccessOperator);
            CrestronConsole.AddNewConsoleCommand(QueryLights, "querylights", "report the status of lights in all rooms", ConsoleAccessLevelEnum.AccessOperator);
            CrestronConsole.AddNewConsoleCommand(TestImageUrl, "testimage", "send test image URL to TP 3 and 6. Usage: testimage <url>", ConsoleAccessLevelEnum.AccessOperator);
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
        }

        // Wrapper for console command
        private void StartupPanelCommand(string parms)
        {
            StartupPanel(parms);
        }

        /// <summary>
        /// Test image URL command - sends URL to StringInput[5] on touchpanels 3 (XPanel) and 6 (CrestronOne)
        /// Usage: testimage [url]
        /// Examples:
        ///   testimage http://192.168.1.100:80/HOME.JPG
        ///   testimage https://192.168.1.100:443/KITCHEN.jpg
        ///   testimage /html/HOME.JPG
        ///   testimage clear  (clears the image URL)
        /// </summary>
        private void TestImageUrl(string parms)
        {
            try
            {
                if (string.IsNullOrEmpty(parms))
                {
                    CrestronConsole.PrintLine("Usage: testimage <url>");
                    CrestronConsole.PrintLine("Examples:");
                    CrestronConsole.PrintLine("  testimage http://{0}:{1}/HOME.JPG", IPaddress, httpPort);
                    CrestronConsole.PrintLine("  testimage https://{0}:{1}/HOME.JPG", IPaddress, httpsPort);
                    CrestronConsole.PrintLine("  testimage /html/HOME.JPG");
                    CrestronConsole.PrintLine("  testimage clear");
                    CrestronConsole.PrintLine("");
                    CrestronConsole.PrintLine("Current values:");
                    if (manager.touchpanelZ.ContainsKey(3))
                        CrestronConsole.PrintLine("  TP3 (XPanel): {0}", manager.touchpanelZ[3].UserInterface.StringInput[5].StringValue);
                    if (manager.touchpanelZ.ContainsKey(6))
                        CrestronConsole.PrintLine("  TP6 (CrestronOne): {0}", manager.touchpanelZ[6].UserInterface.StringInput[5].StringValue);
                    return;
                }

                string url = parms.Trim();
                
                // Handle "clear" command
                if (url.ToLower() == "clear")
                {
                    url = "";
                }

                // Send to TP3 (XPanel)
                if (manager.touchpanelZ.ContainsKey(3))
                {
                    manager.touchpanelZ[3].UserInterface.StringInput[5].StringValue = url;
                    CrestronConsole.PrintLine("TP3 (XPanel) StringInput[5] set to: {0}", url);
                }
                else
                {
                    CrestronConsole.PrintLine("TP3 not found in touchpanelZ");
                }

                // Send to TP6 (CrestronOne)
                if (manager.touchpanelZ.ContainsKey(6))
                {
                    manager.touchpanelZ[6].UserInterface.StringInput[5].StringValue = url;
                    CrestronConsole.PrintLine("TP6 (CrestronOne) StringInput[5] set to: {0}", url);
                }
                else
                {
                    CrestronConsole.PrintLine("TP6 not found in touchpanelZ");
                }
            }
            catch (Exception ex)
            {
                CrestronConsole.PrintLine("Error in TestImageUrl: {0}", ex.Message);
            }
        }

        #endregion
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
                if (room.Value.LightsID > 0 && room.Value.Name.ToUpper() != "GLOBAL")
                {
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
                else
                {
                    HomeButtonPress(tp.Value.Number);//from startup rooms
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
            if (manager.touchpanelZ[TPNumber].HTML_UI)
            {
                manager.touchpanelZ[TPNumber]._HTMLContract.FloorList.NumberOfFloors(
                        (sig, wh) => sig.UShortValue = (ushort)manager.FloorScenarioZ[floorScenarioNum].IncludedFloors.Count);
            }
            else
            {
                manager.touchpanelZ[TPNumber].UserInterface.SmartObjects[3].UShortInput[4].UShortValue = (ushort)manager.FloorScenarioZ[floorScenarioNum].IncludedFloors.Count;
                manager.touchpanelZ[TPNumber].UserInterface.SmartObjects[9].UShortInput[4].UShortValue = (ushort)manager.FloorScenarioZ[floorScenarioNum].IncludedFloors.Count;//music page
            }
            subsystemEISC.UShortInput[(ushort)((TPNumber - 1) * 10 + 301)].UShortValue = manager.RoomZ[currentRoomNumber].AudioID;
            UpdateSubsystems(TPNumber);//from startup panels
            videoSystemControl.UpdateTPVideoMenu(TPNumber);//from startup panels
            //CrestronConsole.PrintLine("111TP-{0} current subsystem is audio {1}", TPNumber, manager.touchpanelZ[TPNumber].CurrentSubsystemIsAudio);

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
                manager.touchpanelZ[TPNumber].SubscribeToMediaPlayer();
            }
            UpdateEquipIDsForSubsystems(TPNumber, currentRoomNumber);//from startup panels
            CrestronConsole.PrintLine("TP-{0} complete!!", (TPNumber));
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
        #region EISC Registration

        private void CreateAndRegisterEISCs()
        {
            foreach (var kv in manager.SubsystemZ)
            {
                var sub = kv.Value;
                if (sub.Name.ToUpper().Contains("LIGHT"))
                {
                    uint ipid = sub.IPID != 0 ? sub.IPID : 0x9Au;
                    string address = sub.IPaddress;

                    lightingEISC = new ThreeSeriesTcpIpEthernetIntersystemCommunications(ipid, address, this);
                    lightingEISC.SigChange += new SigEventHandler(LightingSigChangeHandler);

                    var resp = lightingEISC.Register();
                    if (resp != eDeviceRegistrationUnRegistrationResponse.Success)
                        ErrorLog.Error("EISC for subsystem {0} failed registration: {1}", sub.Number, lightingEISC.RegistrationFailureReason);
                }
                else if (sub.Name.ToUpper().Contains("HVAC") || (sub.Name.ToUpper().Contains("CLIMATE")))
                {
                    uint ipid = sub.IPID != 0 ? sub.IPID : 0x9Bu;
                    string address = sub.IPaddress;

                    HVACEISC = new ThreeSeriesTcpIpEthernetIntersystemCommunications(ipid, address, this);
                    HVACEISC.SigChange += new SigEventHandler(HVACSigChangeHandler);

                    var resp = HVACEISC.Register();
                    if (resp != eDeviceRegistrationUnRegistrationResponse.Success)
                        ErrorLog.Error("EISC for subsystem {0} failed registration: {1}", sub.Number, HVACEISC.RegistrationFailureReason);
                }
                else if (sub.Name.ToUpper().Contains("SECURITY"))
                {
                    uint ipid = sub.IPID != 0 ? sub.IPID : 0xB2u;
                    string address = sub.IPaddress;
                    securityEISC = new ThreeSeriesTcpIpEthernetIntersystemCommunications(ipid, address, this);
                    securityEISC.SigChange += new SigEventHandler(securitySigChangeHandler);
                    var resp = securityEISC.Register();
                    if (resp != eDeviceRegistrationUnRegistrationResponse.Success)
                        ErrorLog.Error("EISC for security {0} failed registration: {1}", sub.Number, securityEISC.RegistrationFailureReason);
                }
            }
        }

        #endregion

        #region Initialization

        public override void InitializeSystem()
        {
            try
            {
                CrestronConsole.PrintLine("system setup start");
                this.SystemSetup();
                CrestronConsole.PrintLine("system setup complete");
                CreateAndRegisterEISCs();
                CrestronConsole.PrintLine("EISC setup complete");
                IPaddress = CrestronEthernetHelper.GetEthernetParameter(CrestronEthernetHelper.ETHERNET_PARAMETER_TO_GET.GET_CURRENT_IP_ADDRESS, CrestronEthernetHelper.GetAdapterdIdForSpecifiedAdapterType(EthernetAdapterType.EthernetLANAdapter));
                httpPort = CrestronEthernetHelper.GetEthernetParameter(CrestronEthernetHelper.ETHERNET_PARAMETER_TO_GET.GET_WEB_PORT, CrestronEthernetHelper.GetAdapterdIdForSpecifiedAdapterType(EthernetAdapterType.EthernetLANAdapter));
                httpsPort = CrestronEthernetHelper.GetEthernetParameter(CrestronEthernetHelper.ETHERNET_PARAMETER_TO_GET.GET_SECURE_WEB_PORT, CrestronEthernetHelper.GetAdapterdIdForSpecifiedAdapterType(EthernetAdapterType.EthernetLANAdapter));
                CrestronConsole.PrintLine("IP address: {0} HTTP port: {1} HTTPS port: {2}", IPaddress, httpPort, httpsPort);
                subsystemEISC.BooleanInput[1].BoolValue = false;
                foreach (var src in manager.MusicSourceZ)
                {
                    ushort srcNum = src.Key;
                    if (manager.MusicSourceZ[srcNum].NaxBoxNumber > 0)
                    {
                        NAXsystem = true;
                    }
                }

                foreach (var tp in manager.touchpanelZ)
                {
                    ushort tpNum = tp.Key;
                    string startupNum = Convert.ToString(tpNum);
                    StartupPanel(startupNum);
                    CrestronConsole.PrintLine("tpNum: {0}", tpNum);
                    if (manager.touchpanelZ[tpNum].ChangeRoomButtonEnable)
                    {
                        manager.touchpanelZ[tpNum].UserInterface.BooleanInput[49].BoolValue = true;
                        manager.touchpanelZ[tpNum].UserInterface.StringInput[6].StringValue = manager.ProjectInfoZ[0].ProjectName;
                        if (manager.touchpanelZ[tpNum].HTML_UI)
                        {
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

                StartupRooms();

                UpdateRoomAVConfig();

                foreach (var tp in manager.touchpanelZ)
                {
                    ushort TPNumber = tp.Value.Number;
                    UpdateRoomOptions(TPNumber);
                }

                for (ushort i = 0; i <= quickActionXML.NumberOfPresets; i++)
                {
                    foreach (var tp in manager.touchpanelZ)
                    {
                        if (tp.Value.HTML_UI)
                        {
                        }
                        else
                        {
                            tp.Value.UserInterface.SmartObjects[15].StringInput[(ushort)(i + 1)].StringValue = quickActionXML.PresetName[i];
                        }
                    }
                }

                subsystemEISC.BooleanInput[1].BoolValue = true;
                InitCompleteTimer = new CTimer(InitCompleteCallback, 0, 20000);
            }
            catch (Exception e)
            {
                CrestronConsole.PrintLine("Error in InitializeSystem: {0}", e.Message);
                ErrorLog.Error("Error in InitializeSystem: {0}", e.Message);
            }
        }

        private void InitCompleteCallback(object obj)
        {
            CrestronConsole.PrintLine("##############     INIT COMPLETE CALLBACK {0}:{1}", DateTime.Now.Second, DateTime.Now.Millisecond);
            InitCompleteTimer.Stop();
            InitCompleteTimer.Dispose();
            initComplete = true;
        }

        public object SystemSetup()
        {
            this.config = new Configuration.ConfigManager();
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
        #endregion
    }
}