//-----------------------------------------------------------------------
// <copyright file="SystemManager.cs" company="Crestron">
//     Copyright (c) Crestron Electronics. All rights reserved.
// </copyright>
//-----------------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
//using System.Threading.Tasks;
using Crestron.SimplSharp;                       // For Basic SIMPL# Classes
using Crestron.SimplSharp.CrestronIO;            // For Directory
using Crestron.SimplSharp.Ssh;
using Crestron.SimplSharpPro;                    // For Basic SIMPL#Pro classes
using Crestron.SimplSharpPro.CrestronThread;     // For Threading
using Crestron.SimplSharpPro.DeviceSupport;      // For Generic Device Support
using Crestron.SimplSharpPro.Diagnostics;        // For System Monitor Access
using Crestron.SimplSharpPro.UI;
using Crestron.SimplSharpPro.EthernetCommunication;
//using Crestron.SimplSharpPro.DM;
//using Crestron.SimplSharpPro.DM.Streaming;
//using Crestron.SimplSharpPro.DM.Endpoints;

namespace ACS_4Series_Template_V1
{
    /// <summary>
    /// Used to manage all the different subsystems
    /// </summary>
    public class SystemManager
    {
        /// <summary>
        /// Keeps track of all the touchpanels that are registered
        /// </summary>
        //private Dictionary<string, BasicTriListWithSmartObject> touchpanels;
        public Dictionary<ushort, UI.TouchpanelUI> touchpanelZ = new Dictionary<ushort, UI.TouchpanelUI>();
        public Dictionary<ushort, Room.RoomConfig> RoomZ = new Dictionary<ushort, Room.RoomConfig>();
        public Dictionary<ushort, DmTransmitter.DmNVXtransmitter> dmSourceZ = new Dictionary<ushort, DmTransmitter.DmNVXtransmitter>();
        public Dictionary<ushort, DmReceiver.DmNVXreceiver> dmDestinationZ = new Dictionary<ushort, DmReceiver.DmNVXreceiver>();
        public Dictionary<ushort, FloorScenarios.FloorScenariosConfig> FloorScenarioZ = new Dictionary<ushort, ACS_4Series_Template_V1.FloorScenarios.FloorScenariosConfig>();
        public Dictionary<ushort, Subsystem.SubsystemConfig> SubsystemZ = new Dictionary<ushort, ACS_4Series_Template_V1.Subsystem.SubsystemConfig>();
        public Dictionary<ushort, SubsystemScenarios.SubsystemScenarioConfig> SubsystemScenarioZ = new Dictionary<ushort, ACS_4Series_Template_V1.SubsystemScenarios.SubsystemScenarioConfig>();
        public Dictionary<ushort, WholeHouseSubsystemScenarios.WholeHouseSubsystemScenarioConfig> WholeHouseSubsystemScenarioZ = new Dictionary<ushort, ACS_4Series_Template_V1.WholeHouseSubsystemScenarios.WholeHouseSubsystemScenarioConfig>();
        public Dictionary<ushort, MusicSources.MusicSourceConfig> MusicSourceZ = new Dictionary<ushort, ACS_4Series_Template_V1.MusicSources.MusicSourceConfig>();
        public Dictionary<ushort, AudioSrcScenarios.AudioSrcScenariosConfig> AudioSrcScenarioZ = new Dictionary<ushort, ACS_4Series_Template_V1.AudioSrcScenarios.AudioSrcScenariosConfig>();
        public Dictionary<ushort, AudioSrcSharingScenarios.AudioSrcSharingScenariosConfig> AudioSrcSharingScenarioZ = new Dictionary<ushort, ACS_4Series_Template_V1.AudioSrcSharingScenarios.AudioSrcSharingScenariosConfig>();
        public Dictionary<ushort, VideoSources.VideoSourceConfig> VideoSourceZ = new Dictionary<ushort, ACS_4Series_Template_V1.VideoSources.VideoSourceConfig>();
        public Dictionary<ushort, VideoSrcScenarios.VideoSrcScenariosConfig> VideoSrcScenarioZ = new Dictionary<ushort, ACS_4Series_Template_V1.VideoSrcScenarios.VideoSrcScenariosConfig>();
        public Dictionary<ushort, VidConfigScenarios.VidConfigScenariosConfig> VideoConfigScenarioZ = new Dictionary<ushort, ACS_4Series_Template_V1.VidConfigScenarios.VidConfigScenariosConfig>();
        public Dictionary<ushort, LiftScenarios.LiftScenariosConfig> LiftScenarioZ = new Dictionary<ushort, ACS_4Series_Template_V1.LiftScenarios.LiftScenariosConfig>();
        public Dictionary<ushort, SleepScenarios.SleepScenariosConfig> SleepScenarioZ = new Dictionary<ushort, ACS_4Series_Template_V1.SleepScenarios.SleepScenariosConfig>();
        public Dictionary<ushort, FormatScenarios.FormatScenariosConfig> FormatScenarioZ = new Dictionary<ushort, ACS_4Series_Template_V1.FormatScenarios.FormatScenariosConfig>();
        public Dictionary<ushort, FloorScenarios.FloorConfig> Floorz = new Dictionary<ushort, ACS_4Series_Template_V1.FloorScenarios.FloorConfig>();
        public Dictionary<ushort, LiftScenarios.LiftCmdConfig> LiftCmdZ = new Dictionary<ushort, ACS_4Series_Template_V1.LiftScenarios.LiftCmdConfig>();
        public Dictionary<ushort, SleepScenarios.SleepCmdConfig> SleepCmdZ = new Dictionary<ushort, ACS_4Series_Template_V1.SleepScenarios.SleepCmdConfig>();
        public Dictionary<ushort, FormatScenarios.FormatCmdConfig> FormatCmdZ = new Dictionary<ushort, ACS_4Series_Template_V1.FormatScenarios.FormatCmdConfig>();

        /// <summary>
        /// TouchpanelUI object to use for registration
        /// </summary>
        private readonly UI.TouchpanelUI tp;
        private readonly Room.RoomConfig rm;
        private readonly DmTransmitter.DmNVXtransmitter DMtransmitter;
        private readonly DmReceiver.DmNVXreceiver DMreceiver;
        private readonly FloorScenarios.FloorScenariosConfig floorScenario;
        private readonly FloorScenarios.FloorConfig flrs;
        private readonly Subsystem.SubsystemConfig subSys;
        private readonly SubsystemScenarios.SubsystemScenarioConfig subsysScenario;
        private readonly WholeHouseSubsystemScenarios.WholeHouseSubsystemScenarioConfig wholeHouseSubsystemScenario;
        private readonly MusicSources.MusicSourceConfig musicSource;
        private readonly AudioSrcScenarios.AudioSrcScenariosConfig aSrcScenario;
        private readonly AudioSrcSharingScenarios.AudioSrcSharingScenariosConfig aSrcSharingScenario;
        private readonly VideoSources.VideoSourceConfig videoSource;
        private readonly VideoSrcScenarios.VideoSrcScenariosConfig vidSrcScenario;
        private readonly VidConfigScenarios.VidConfigScenariosConfig videoConfigScenario;
        private readonly LiftScenarios.LiftScenariosConfig liftScenario;
        private readonly LiftScenarios.LiftCmdConfig liftCmd;
        private readonly SleepScenarios.SleepScenariosConfig sleepScenario;
        private readonly SleepScenarios.SleepCmdConfig sleepCmd;
        private readonly FormatScenarios.FormatScenariosConfig formatScenario;
        private readonly FormatScenarios.FormatCmdConfig formatCmd;

        public ushort i = 0;
        /// <summary>
        /// Initializes a new instance of the SystemManager class
        /// </summary>
        /// <param name="config">full config data</param>

        public SystemManager(Configuration.ConfigData.Configuration config, CrestronControlSystem cs)
        {
            CrestronConsole.PrintLine("system manager start");
            if (config.Touchpanels != null)
            {
                foreach (var touchpanel in config.Touchpanels)
                {
                    try
                    {
                        CrestronConsole.PrintLine("touchpanel {0} name {1} type {2}",touchpanel.Number, touchpanel.Name, touchpanel.Type);
                        this.tp = new UI.TouchpanelUI(touchpanel.Number, touchpanel.Ipid, touchpanel.Type, touchpanel.Name, touchpanel.HomePageScenario, touchpanel.SubSystemScenario, touchpanel.FloorScenario, touchpanel.DefaultRoom, touchpanel.ChangeRoomButtonEnable, touchpanel.ChangeRoomButtonText, touchpanel.UseAnalogModes, touchpanel.DontInheritSubsystemScenario);
                        {
                            tp.CurrentASrcGroupNum = 1;
                            tp.CurrentVSrcGroupNum = 1;
                            tp.CurrentRoomNum = touchpanel.DefaultRoom;
                            tp.OnHomePage = true; //false is on room page 
                        }
                        this.touchpanelZ[touchpanel.Number] = this.tp;

                    }
                    catch (Exception e)
                    {
                        CrestronConsole.PrintLine(string.Format("Error in the constructor: {0}", e.Message));
                    }
                }
                CrestronConsole.PrintLine("tp cnt {0}", touchpanelZ.Count);
                
            }
            if (config.Rooms != null)
            {
                foreach (var room in config.Rooms) 
                {
                    try
                    {
                        CrestronConsole.PrintLine("room {0} {1}", room.Number, room.Name);
                        this.rm = new Room.RoomConfig(room.Number, room.Name, room.SubSystemScenario, room.VideoSrcScenario, room.AudioSrcScenario, room.AudioSrcSharingScenario, room.ConfigurationScenario, room.LiftScenario, room.SleepScenario, room.FormatScenario, room.AudioID, room.VideoOutputNum, room.LightsID, room.ShadesID, room.ClimateID, room.MiscID, room.TvOutToAudioInputNumber, room.ImageURL);
                        {
                            rm.CurrentVideoSrc = 0;
                            rm.CurrentMusicSrc = 0;
                            rm.CurrentSubsystem = 0;
                            rm.LastSystemVid = false;
                            this.RoomZ[rm.Number] = rm;
                        }
                    }
                    catch (Exception e)
                    {
                        ErrorLog.Error(string.Format("room Error in the constructor: {0}", e.Message));
                        CrestronConsole.PrintLine(string.Format("room Error in the constructor: {0}", e.Message));
                    }
                }
                CrestronConsole.PrintLine("room count {0}", RoomZ.Count);
            }
            if (config.DmNVXtransmitter != null) {
                foreach (var nvx in config.DmNVXtransmitter) {
                    try {
                        CrestronConsole.PrintLine("nvx transmitter {0}", nvx.Name);
                        this.DMtransmitter = new DmTransmitter.DmNVXtransmitter(nvx.DmInputNumber, nvx.Name, nvx.Ipid, nvx.Type, nvx.MultiCastAddress, cs);
                        this.dmSourceZ[nvx.Number] = DMtransmitter;

                        if (!this.dmSourceZ[nvx.Number].Register())
                        {
                            //CrestronConsole.PrintLine("nvx reg failed: {0}", this.dmSourceZ[nvx.Number].DmNvx35X_BOX.RegistrationFailureReason);
                        }
                        else 
                        {
                            //CrestronConsole.PrintLine("registered nvx: {0}", this.dmSourceZ[nvx.Number].Name);
                        }
                    }
                    catch (Exception e)
                    {
                        ErrorLog.Error(string.Format("DM Error in the constructor: {0}", e.Message));
                        CrestronConsole.PrintLine(string.Format("1 DM Error in the constructor: {0}", e.Message));
                    }
                }
            }
            if (config.DmNVXreceiver != null)
            {
                foreach (var nvx in config.DmNVXreceiver)
                {
                    try
                    {
                        CrestronConsole.PrintLine("nvx receiver {0}", nvx.Name);
                        this.DMreceiver = new DmReceiver.DmNVXreceiver(nvx.DmOutputNumber, nvx.Name, nvx.Ipid, nvx.Type, nvx.MultiCastAddress, cs);
                        this.dmDestinationZ[nvx.Number] = DMreceiver;

                        if (!this.dmDestinationZ[nvx.Number].Register())
                        {
                            CrestronConsole.PrintLine("nvx reg failed: {0}", this.dmDestinationZ[nvx.Number].DmNvx35X.RegistrationFailureReason);
                        }
                        else
                        {
                            CrestronConsole.PrintLine("registered nvx: {0}", this.dmDestinationZ[nvx.Number].Name);
                        }
                    }
                    catch (Exception e)
                    {
                        ErrorLog.Error(string.Format("DM Error in the constructor: {0}", e.Message));
                        CrestronConsole.PrintLine(string.Format("DM Error in the constructor: {0}", e.Message));
                    }
                }
            }
            if (config.FloorScenarios != null) {
                foreach (var flrScenario in config.FloorScenarios)
                {
                    try 
                    {
                        this.floorScenario = new FloorScenarios.FloorScenariosConfig(flrScenario.Number, flrScenario.IncludedFloors);
                        this.FloorScenarioZ[flrScenario.Number] = this.floorScenario;
                    }
                    catch (Exception e)
                    {
                        CrestronConsole.PrintLine(string.Format("floor scenario Error in the constructor: {0}", e.Message));
                    }
                }
                CrestronConsole.PrintLine("FloorScenarioZ.Count {0}", FloorScenarioZ.Count);
            }
            if (config.Floors != null)
            {
                //Floorz.Clear();//clear the list from the previous loop
                foreach (var floor in config.Floors)
                {
                    this.flrs  = new FloorScenarios.FloorConfig(floor.Number, floor.Name, floor.IncludedRooms);
                    CrestronConsole.PrintLine("floors name {0}", flrs.Name);
                    Floorz[floor.Number] = this.flrs;
                }
                CrestronConsole.PrintLine("Floorz.Count {0}", Floorz.Count);
            }

            if (config.Subsystems != null) {
                foreach (var subsystem in config.Subsystems)
                {
                    try
                    {
                        //ushort subSystemsCount = (ushort)(config.Subsystems.Length);
                        this.subSys = new Subsystem.SubsystemConfig(subsystem.Number, subsystem.Name, subsystem.DisplayName, subsystem.IconSerial, subsystem.AnalogModeNumber, subsystem.FlipsToPageNumber, subsystem.EquipID);
                        CrestronConsole.PrintLine("subsystem {0} - {1}", subSys.Number, subSys.DisplayName);
                        this.SubsystemZ[subsystem.Number] = this.subSys;
                    }
                    catch (Exception e) {
                        CrestronConsole.PrintLine(string.Format("subsystem Error in the constructor: {0}", e.Message));
                    }
                }
            }
            if (config.SubSystemScenarios != null)
            {
                foreach (var subsystemScenario in config.SubSystemScenarios)
                {
                    try
                    {
                        //CrestronConsole.PrintLine("subsystemScenario {0} ", subsysScenario.Number);
                        //CrestronConsole.PrintLine("subsystemScenario {0} ", subsysScenario.IncludedSubsystems[0]);
                        this.subsysScenario = new SubsystemScenarios.SubsystemScenarioConfig(subsystemScenario.Number, subsystemScenario.IncludedSubsystems);
                        CrestronConsole.PrintLine("subsystemScenario {0} #items {1}", subsysScenario.Number, subsysScenario.IncludedSubsystems.Count);
                        this.SubsystemScenarioZ[subsystemScenario.Number] = this.subsysScenario;
                    }
                    catch (Exception e)
                    {
                        CrestronConsole.PrintLine(string.Format("subsystemScenario Error in the constructor: {0}", e.Message));
                    }
                }
            }
            else { CrestronConsole.PrintLine("subsystemScenario null "); }
            if (config.WholeHouseSubsystemScenarios != null)
            {
                foreach (var wholeHouseSubsystemScenario in config.WholeHouseSubsystemScenarios)
                {
                    try
                    {
                        this.wholeHouseSubsystemScenario = new WholeHouseSubsystemScenarios.WholeHouseSubsystemScenarioConfig(wholeHouseSubsystemScenario.Number, wholeHouseSubsystemScenario.IncludedSubsystems);
                        this.WholeHouseSubsystemScenarioZ[wholeHouseSubsystemScenario.Number] = this.wholeHouseSubsystemScenario;
                    }
                    catch (Exception e)
                    {
                        CrestronConsole.PrintLine(string.Format("wholeHouseSubsystemScenario Error in the constructor: {0}", e.Message));
                    }
                }
            }
            else { CrestronConsole.PrintLine("subsystemScenario null "); }
            if (config.MusicSources != null)
            {
                foreach (var musicSource in config.MusicSources)
                {
                    try
                    {
                        this.musicSource = new MusicSources.MusicSourceConfig(musicSource.Number, musicSource.Name, musicSource.IconSerial, musicSource.AnalogModeNumber, musicSource.SwitcherInputNumber, musicSource.FlipsToPageNumber, musicSource.EquipID);
                        CrestronConsole.PrintLine("musicSource {0} - {1}", musicSource.Number, musicSource.Name);
                        this.musicSource.InUse = false;
                        this.MusicSourceZ[musicSource.Number] = this.musicSource;
                    }
                    catch (Exception e)
                    {
                        CrestronConsole.PrintLine(string.Format("music source Error in the constructor: {0}", e.Message));
                    }
                }
            }
            if (config.AudioSrcScenarios != null)
            {
                foreach (var AudioSrcScenario in config.AudioSrcScenarios)
                {
                    try
                    {
                        //ushort subSystemsCount = (ushort)(config.Subsystems.Length);
                        this.aSrcScenario = new AudioSrcScenarios.AudioSrcScenariosConfig(AudioSrcScenario.Number, AudioSrcScenario.IncludedSources, AudioSrcScenario.ReceiverInputs);
                        CrestronConsole.PrintLine("audioSrcScenario {0} # of items {1}", aSrcScenario.Number, aSrcScenario.IncludedSources.Count);
                        this.AudioSrcScenarioZ[AudioSrcScenario.Number] = this.aSrcScenario;
                    }
                    catch (Exception e)
                    {
                        CrestronConsole.PrintLine(string.Format("audioSrcScenario Error in the constructor: {0}", e.Message));
                    }
                }
            }
            if (config.AudioSrcSharingScenarios != null)
            {
                foreach (var AudioSrcSharingScenario in config.AudioSrcSharingScenarios)
                {
                    try
                    {
                        //ushort subSystemsCount = (ushort)(config.Subsystems.Length);
                        this.aSrcSharingScenario = new AudioSrcSharingScenarios.AudioSrcSharingScenariosConfig(AudioSrcSharingScenario.Number, AudioSrcSharingScenario.IncludedZones);
                        CrestronConsole.PrintLine("audioSrcSharingScenario {0} # of items {1}", aSrcSharingScenario.Number, aSrcSharingScenario.IncludedZones.Count);
                        this.AudioSrcSharingScenarioZ[AudioSrcSharingScenario.Number] = this.aSrcSharingScenario;
                    }
                    catch (Exception e)
                    {
                        CrestronConsole.PrintLine(string.Format("audioSrcSharingScenario Error in the constructor: {0}", e.Message));
                    }
                }
            }
            if (config.VideoSources != null)
            {
                foreach (var videoSource in config.VideoSources)
                {
                    try
                    {
                        //ushort subSystemsCount = (ushort)(config.Subsystems.Length);
                        this.videoSource = new VideoSources.VideoSourceConfig(videoSource.Number, videoSource.Name, videoSource.DisplayName, videoSource.IconSerial, videoSource.AnalogModeNumber, videoSource.VidSwitcherInputNumber, videoSource.AudSwitcherInputNumber, videoSource.FlipsToPageNumber, videoSource.EquipID);
                        CrestronConsole.PrintLine("videoSource {0} - {1}", videoSource.Number, videoSource.Name);
                        this.videoSource.InUse = false;
                        this.VideoSourceZ[videoSource.Number] = this.videoSource;
                    }
                    catch (Exception e)
                    {
                        CrestronConsole.PrintLine(string.Format("video source Error in the constructor: {0}", e.Message));
                    }
                }
            }
            if (config.VideoSrcScenarios != null)
            {
                foreach (var VideoSrcScenario in config.VideoSrcScenarios)
                {
                    try
                    {
                        //ushort subSystemsCount = (ushort)(config.Subsystems.Length);
                        this.vidSrcScenario = new VideoSrcScenarios.VideoSrcScenariosConfig(VideoSrcScenario.Number, VideoSrcScenario.IncludedSources, VideoSrcScenario.DisplayInputs, VideoSrcScenario.ReceiverInputs, VideoSrcScenario.AltSwitcherInputs);
                        CrestronConsole.PrintLine("videoSrcScenario {0} # of items {1}", vidSrcScenario.Number, vidSrcScenario.IncludedSources.Count);
                        this.VideoSrcScenarioZ[VideoSrcScenario.Number] = this.vidSrcScenario;
                    }
                    catch (Exception e)
                    {
                        CrestronConsole.PrintLine(string.Format("vidSrcScenario Error in the constructor: {0}", e.Message));
                    }
                }
            }
            if (config.VidConfigurationScenarios != null)
            {
                foreach (var vidConfigScenario in config.VidConfigurationScenarios)
                {
                    try
                    {
                        this.videoConfigScenario = new VidConfigScenarios.VidConfigScenariosConfig(vidConfigScenario.Number, vidConfigScenario.SendToSpeakers, vidConfigScenario.OffSubScenarioNum, vidConfigScenario.HasReceiver, vidConfigScenario.ReceiverHasVolFB, vidConfigScenario.ReceiverInputDelay, vidConfigScenario.MusicThroughReceiver, vidConfigScenario.ReceiverHasBreakawayAudio, vidConfigScenario.MusicHasVolFB, vidConfigScenario.VideoVolThroughDistAudio, vidConfigScenario.DisplayInputDelay, vidConfigScenario.TvHasVolFB);
                        CrestronConsole.PrintLine("vidConfigScenario {0} has AVR? {1}", vidConfigScenario.Number, vidConfigScenario.HasReceiver);
                        this.VideoConfigScenarioZ[vidConfigScenario.Number] = this.videoConfigScenario;
                    }
                    catch (Exception e)
                    {
                        CrestronConsole.PrintLine(string.Format("vidConfigScenario Error in the constructor: {0}", e.Message));
                    }
                }
            }
            if (config.LiftScenarios != null) {
                foreach (var liftScen in config.LiftScenarios) {
                    try 
                    {
                        this.liftScenario = new LiftScenarios.LiftScenariosConfig(liftScen.Number, liftScen.ButtonLabel, liftScen.OpenWithOnCmdNum, liftScen.CloseWithOffCmdNum, liftScen.LiftCmds);
                        //this.liftScenario = new LiftScenarios.LiftScenariosConfig(liftScen.Number, liftScen.ButtonLabel, liftScen.OpenWithOnCmdNum, liftScen.CloseWithOffCmdNum, this.liftCmd);
                        CrestronConsole.PrintLine("lift Scenario #{0} cnt{1} ", liftScenario.Number, liftScenario.LiftCmds.Count());
                        this.LiftScenarioZ[liftScen.Number] = this.liftScenario;
                    }
                    catch (Exception e)
                    {
                        CrestronConsole.PrintLine(string.Format("lift Scenario Error in the constructor: {0}", e.Message));
                    }
                }
            }
            if (config.LiftCommands != null){
                foreach (var liftCommand in config.LiftCommands)
                {
                    //this.liftScenario.LiftCmdConfigs[i] = new LiftScenarios.LiftScenariosConfig.LiftCmdConfig(liftCmd.CmdNum, liftCmd.Name, liftCmd.PulseTime);
                    liftCmd = new LiftScenarios.LiftCmdConfig(liftCommand.CmdNum, liftCommand.Name, liftCommand.PulseTime);
                    CrestronConsole.PrintLine("liftCmd {0}", liftCmd.Name);
                    this.LiftCmdZ[liftCommand.CmdNum] = this.liftCmd;
                }
            }
            if (config.SleepScenarios != null)
            {
                foreach (var sleepScen in config.SleepScenarios)
                {
                    try
                    {
                        this.sleepScenario = new SleepScenarios.SleepScenariosConfig(sleepScen.Number, sleepScen.ButtonLabel, sleepScen.SleepCmds);
                        CrestronConsole.PrintLine("sleep Scenario {0} {1} ", sleepScenario.Number, sleepScenario.SleepCmds.Count());
                        this.SleepScenarioZ[sleepScen.Number] = this.sleepScenario;
                    }
                    catch (Exception e)
                    {
                        CrestronConsole.PrintLine(string.Format("sleep Scenario Error in the constructor: {0}", e.Message));
                    }
                }
            }
            if (config.SleepCommands != null) {
                foreach (var sleepCommand in config.SleepCommands)
                {
                    sleepCmd = new SleepScenarios.SleepCmdConfig(sleepCommand.CmdNum, sleepCommand.Name, sleepCommand.Length);
                    CrestronConsole.PrintLine("sleepCmd {0}", sleepCmd.Name);
                    this.SleepCmdZ[sleepCommand.CmdNum] = this.sleepCmd;
                    //this.sleepScenario.SleepCmdConfigs[i] = new SleepScenarios.SleepScenariosConfig.SleepCmdConfig(sleepCmd.Name, sleepCmd.Length);
                }
            }
            if (config.FormatScenarios != null)
            {
                foreach (var formatScen in config.FormatScenarios)
                {
                    try
                    {

                        this.formatScenario = new FormatScenarios.FormatScenariosConfig(formatScen.Number, formatScen.ButtonLabel, formatScen.FormatCmds);
                        CrestronConsole.PrintLine("format Scenario {0} {1}", formatScenario.Number, formatScenario.FormatCmds.Count());
                        this.FormatScenarioZ[formatScen.Number] = this.formatScenario;
                    }
                    catch (Exception e)
                    {
                        CrestronConsole.PrintLine(string.Format("format Scenario Error in the constructor: {0}", e.Message));
                    }
                }
            }
            if (config.FormatCommands != null) {
                foreach (var formatCommand in config.FormatCommands)
                {
                    formatCmd = new FormatScenarios.FormatCmdConfig(formatCommand.CmdNum, formatCommand.Name);
                    this.FormatCmdZ[formatCommand.CmdNum] = this.formatCmd;
                    CrestronConsole.PrintLine("format {0}", formatCmd.Name);
                    //this.formatScenario.FormatCmdConfigs[i] = new FormatScenarios.FormatScenariosConfig.FormatCmdConfig(formatCmd.CmdNum, formatCmd.Name );
                }
            }
        }
    }
}
