//-----------------------------------------------------------------------
// <copyright file="ConfigData.cs" company="Crestron">
//     Copyright (c) Crestron Electronics. All rights reserved.
// </copyright>
//-----------------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Crestron.SimplSharp;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace ACS_4Series_Template_V1.Configuration
{
    /// <summary>
    /// Class used to deserialize JSON to a usable configuration
    /// Used in ControlSystem.cs to start the system config
    /// </summary>
    public class ConfigData
    {
        public class TouchpanelsItem
        {
            [JsonProperty("number")]
            public ushort Number { get; set; }
            /// <summary>
            /// Gets or sets the ID of this source
            /// </summary>
            [JsonProperty("ipid")]
            public ushort Ipid { get; set; }

            /// <summary>
            /// Gets or sets the type of this source. Can be "tsw760" or " xpanel" in this example
            /// </summary>
            [JsonProperty("type")]
            public string Type { get; set; }

            /// <summary>
            /// Gets or sets the label that is shown on the touchpanel interface
            /// </summary>
            [JsonProperty("name")]
            public string Name { get; set; }

            [JsonProperty("HTML_UI")]
            public bool HTML_UI { get; set; }

            [JsonProperty("homePageScenario")]
            public ushort HomePageScenario { get; set; }

            [JsonProperty("subSystemScenario")]
            public ushort SubSystemScenario { get; set; }

            [JsonProperty("floorScenario")]
            public ushort FloorScenario { get; set; }

            [JsonProperty("defaultRoom")]
            public ushort DefaultRoom { get; set; }

            [JsonProperty("changeRoomButtonEnable")]
            public bool ChangeRoomButtonEnable { get; set; }

            [JsonProperty("changeRoomButtonText")]
            public string ChangeRoomButtonText { get; set; }

            [JsonProperty("useAnalogModes")]
            public bool UseAnalogModes { get; set; }

            [JsonProperty("dontInheritSubsystemScenario")]
            public bool DontInheritSubsystemScenario { get; set; }
        }
        /// <summary>
        /// All the rooms in this config
        /// </summary>
        public class RoomsItem
        {
            /// <summary>
            /// Gets or sets the Number of this room
            /// </summary>
            [JsonProperty("number")]
            public ushort Number { get; set; }

            /// <summary>
            /// Gets or sets the name of this room. 
            /// </summary>
            [JsonProperty("name")]
            public string Name { get; set; }

            /// <summary>
            /// Gets or sets the subSystemScenario
            /// </summary>
            [JsonProperty("subSystemScenario")]
            public ushort SubSystemScenario { get; set; }

            /// <summary>
            /// Gets or sets the videoSrcScenario.
            /// </summary>
            [JsonProperty("videoSrcScenario")]
            public ushort VideoSrcScenario { get; set; }

            [JsonProperty("audioSrcScenario")]
            public ushort AudioSrcScenario { get; set; }

            [JsonProperty("audioSrcSharingScenario")]
            public ushort AudioSrcSharingScenario { get; set; }

            [JsonProperty("configurationScenario")]
            public ushort ConfigurationScenario { get; set; }

            [JsonProperty("liftScenario")]
            public ushort LiftScenario { get; set; }

            [JsonProperty("sleepScenario")]
            public ushort SleepScenario { get; set; }

            [JsonProperty("formatScenario")]
            public ushort FormatScenario { get; set; }

            [JsonProperty("audioID")]
            public ushort AudioID { get; set; }

            [JsonProperty("videoOutputNum")]
            public ushort VideoOutputNum { get; set; }

            [JsonProperty("lightsID")]
            public ushort LightsID { get; set; }

            [JsonProperty("shadesID")]
            public ushort ShadesID { get; set; }

            [JsonProperty("climateID")]
            public ushort ClimateID { get; set; }

            [JsonProperty("miscID")]
            public ushort MiscID { get; set; }

            [JsonProperty("tvOutToAudioInputNumber")]
            public ushort TvOutToAudioInputNumber { get; set; }
            [JsonProperty("imageURL")]
            public string ImageURL { get; set; }
        }

        public class DmNVXtransmitterItem {
            [JsonProperty("number")]
            public ushort Number { get; set; }
            [JsonProperty("dmInputNumber")]
            public ushort DmInputNumber { get; set; }
            [JsonProperty("name")]
            public string Name { get; set; }
            [JsonProperty("ipid")]
            public ushort Ipid { get; set; }
            [JsonProperty("type")]
            public string Type { get; set; }
            [JsonProperty("multiCastAddress")]
            public string MultiCastAddress { get; set; }
        }
        public class DmNVXreceiverItem
        {
            [JsonProperty("number")]
            public ushort Number { get; set; }
            [JsonProperty("dmOutputNumber")]
            public ushort DmOutputNumber { get; set; }
            [JsonProperty("name")]
            public string Name { get; set; }
            [JsonProperty("ipid")]
            public ushort Ipid { get; set; }
            [JsonProperty("type")]
            public string Type { get; set; }
            [JsonProperty("multiCastAddress")]
            public string MultiCastAddress { get; set; }
        }

        public class FloorScenariosItem {
            [JsonProperty("number")]
            public ushort Number { get; set; }

            [JsonProperty("includedFloors")]
            public List<ushort> IncludedFloors { get; set; }
        }

        public class FloorsItem {
            [JsonProperty("number")]
            public ushort Number { get; set; }

            [JsonProperty("name")]
            public string Name { get; set; }

            [JsonProperty("includedRooms")]
            public List<ushort> IncludedRooms { get; set; }
        }
        /// <summary>
        /// All the subsystems in this config
        /// </summary>
        public class SubsystemsItem
        {
            /// <summary>
            /// Gets or sets the number of this subsystem
            /// </summary>
            [JsonProperty("number")]
            public ushort Number { get; set; }

            /// <summary>
            /// Gets or sets the name of this subsystem.
            /// </summary>
            [JsonProperty("name")]
            public string Name { get; set; }

            /// <summary>
            /// Gets or sets the label that is shown on the touchpanel interface
            /// </summary>
            [JsonProperty("displayName")]
            public string DisplayName { get; set; }

            /// <summary>
            /// Gets or sets the icon of this subsystem.
            /// </summary>
            [JsonProperty("iconSerial")]
            public string IconSerial { get; set; }

            [JsonProperty("iconHTML")]
            public string IconHTML { get; set; }

            [JsonProperty("analogModeNumber")]
            public ushort AnalogModeNumber { get; set; }

            [JsonProperty("flipsToPageNumber")]
            public ushort FlipsToPageNumber { get; set; }

            [JsonProperty("equipID")]
            public ushort EquipID { get; set; }
        }

        public class SubSystemScenariosItem
        {

            [JsonProperty("number")]
            public ushort Number { get; set; }

            [JsonProperty("includedSubsystems")]
            public List<ushort> IncludedSubsystems { get; set; }

        }
        public class WholeHouseSubsystemScenariosItem
        {

            [JsonProperty("number")]
            public ushort Number { get; set; }

            [JsonProperty("includedSubsystems")]
            public List<ushort> IncludedSubsystems { get; set; }

        }

        public class MusicSourcesItem
        {

            [JsonProperty("number")]
            public ushort Number { get; set; }

            [JsonProperty("name")]
            public string Name { get; set; }

            [JsonProperty("iconSerial")]
            public string IconSerial { get; set; }

            [JsonProperty("iconHTML")]
            public string IconHTML { get; set; }

            [JsonProperty("analogModeNumber")]
            public ushort AnalogModeNumber { get; set; }

            [JsonProperty("NAXBoxNumber")]
            public ushort NaxBoxNumber { get; set; }

            [JsonProperty("switcherInputNumber")]
            public ushort SwitcherInputNumber { get; set; }

            [JsonProperty("multiCastAddress")]
            public string MultiCastAddress { get; set; }
            [JsonProperty("flipsToPageNumber")]
            public ushort FlipsToPageNumber { get; set; }

            [JsonProperty("equipID")]
            public ushort EquipID { get; set; }
        }

        public class AudioSrcScenariosItem 
        {
            [JsonProperty("number")]
            public ushort Number { get; set; }

            [JsonProperty("includedSources")]
            public List<ushort> IncludedSources { get; set; }

            [JsonProperty("receiverInputs")]
            public List<ushort> ReceiverInputs { get; set; }
        }

        public class AudioSrcSharingScenariosItem
        {
            [JsonProperty("number")]
            public ushort Number { get; set; }

            [JsonProperty("includedZones")]
            public List<ushort> IncludedZones { get; set; }
        }

        public class VideoSourcesItem
        {

            [JsonProperty("number")]
            public ushort Number { get; set; }

            [JsonProperty("name")]
            public string Name { get; set; }

            [JsonProperty("displayName")]
            public string DisplayName { get; set; }

            [JsonProperty("iconSerial")]
            public string IconSerial { get; set; }

            [JsonProperty("iconHTML")]
            public string IconHTML { get; set; }

            [JsonProperty("analogModeNumber")]
            public ushort AnalogModeNumber { get; set; }

            [JsonProperty("vidSwitcherInputNumber")]
            public ushort VidSwitcherInputNumber { get; set; }

            [JsonProperty("audSwitcherInputNumber")]
            public ushort AudSwitcherInputNumber { get; set; }

            [JsonProperty("flipsToPageNumber")]
            public ushort FlipsToPageNumber { get; set; }

            [JsonProperty("equipID")]
            public ushort EquipID { get; set; }
        }
        public class VideoSrcScenariosItem
        {
            [JsonProperty("number")]
            public ushort Number { get; set; }

            [JsonProperty("includedSources")]
            public List<ushort> IncludedSources { get; set; }

            [JsonProperty("displayInputs")]
            public List<ushort> DisplayInputs { get; set; }

            [JsonProperty("receiverInputs")]
            public List<ushort> ReceiverInputs { get; set; }

            [JsonProperty("altSwitcherInputs")]
            public List<ushort> AltSwitcherInputs { get; set; }
        }
        public class VidConfigurationScenariosItem
        {
            [JsonProperty("number")]
            public ushort Number { get; set; }

            [JsonProperty("sendToSpeakers")]
            public bool SendToSpeakers { get; set; }

            [JsonProperty("offSubScenarioNum")]
            public ushort OffSubScenarioNum { get; set; }

            [JsonProperty("hasReceiver")]
            public bool HasReceiver { get; set; }

            [JsonProperty("receiverHasVolFB")]
            public bool ReceiverHasVolFB { get; set; }

            [JsonProperty("receiverInputDelay")]
            public ushort ReceiverInputDelay { get; set; }

            [JsonProperty("musicThroughReceiver")]
            public ushort MusicThroughReceiver { get; set; }

            [JsonProperty("receiverHasBreakawayAudio")]
            public bool ReceiverHasBreakawayAudio { get; set; }

            [JsonProperty("musicHasVolFB")]
            public bool MusicHasVolFB { get; set; }

            [JsonProperty("videoVolThroughDistAudio")]
            public bool VideoVolThroughDistAudio { get; set; }

            [JsonProperty("displayInputDelay")]
            public ushort DisplayInputDelay { get; set; }

            [JsonProperty("tvHasVolFB")]
            public bool TvHasVolFB { get; set; }
        }

        public class LiftScenariosItem {
            [JsonProperty("number")]
            public ushort Number { get; set; }

            [JsonProperty("buttonLabel")]
            public string ButtonLabel { get; set; }

            [JsonProperty("openWithOnCmdNum")]
            public ushort OpenWithOnCmdNum { get; set; }

            [JsonProperty("closeWithOffCmdNum")]
            public ushort CloseWithOffCmdNum { get; set; }

            [JsonProperty("commands")]
            public List<ushort> LiftCmds { get; set; }
        }

        public class LiftCommandsItem {
            [JsonProperty("cmdNum")]
            public ushort CmdNum { get; set; }

            [JsonProperty("name")]
            public string Name { get; set; }

            [JsonProperty("pulseTime")]
            public ushort PulseTime { get; set; }
        }

        public class SleepScenariosItem
        {
            [JsonProperty("number")]
            public ushort Number { get; set; }

            [JsonProperty("buttonLabel")]
            public string ButtonLabel { get; set; }

            [JsonProperty("commands")]
            public List<ushort> SleepCmds { get; set; }
        }
        public class SleepCommandsItem
        {
            [JsonProperty("cmdNum")]
            public ushort CmdNum { get; set; }

            [JsonProperty("name")]
            public string Name { get; set; }

            [JsonProperty("length")]
            public ushort Length { get; set; }
        }

        public class FormatScenariosItem
        {
            [JsonProperty("number")]
            public ushort Number { get; set; }

            [JsonProperty("buttonLabel")]
            public string ButtonLabel { get; set; }

            [JsonProperty("commands")]
            public List<ushort> FormatCmds { get; set; }
        }

        public class FormatCommandsItem
        {
            [JsonProperty("cmdNum")]
            public ushort CmdNum { get; set; }

            [JsonProperty("name")]
            public string Name { get; set; }
        }
        /// <summary>
        /// Configuration object
        /// </summary>
        public class Configuration
        {

            /// <summary>
            /// Gets or sets the List of touchpanels
            /// </summary>
            [JsonProperty("touchpanels")]
            public TouchpanelsItem[] Touchpanels { get; set; }

            /// <summary>
            /// Gets or sets the List of rooms
            /// </summary>
            [JsonProperty("rooms")]
            public RoomsItem[] Rooms { get; set; }

            [JsonProperty("dmNVXtransmitters")]
            public DmNVXtransmitterItem[] DmNVXtransmitter { get; set; }
            [JsonProperty("dmNVXreceivers")]
            public DmNVXreceiverItem[] DmNVXreceiver { get; set; }
            [JsonProperty("floorScenarios")]
            public FloorScenariosItem[] FloorScenarios { get; set; }

            [JsonProperty("floors")]
            public FloorsItem[] Floors { get; set; }
            /// <summary>
            /// Gets or sets the List of subsystems
            /// </summary>
            [JsonProperty("subSystems")]
            public SubsystemsItem[] Subsystems { get; set; }

            /// <summary>
            /// Gets or sets the List of subsystems scenarios
            /// </summary>
            [JsonProperty("subSystemScenarios")]
            public SubSystemScenariosItem[] SubSystemScenarios { get; set; }

            [JsonProperty("wholeHouseSubsystemScenarios")]
            public WholeHouseSubsystemScenariosItem[] WholeHouseSubsystemScenarios { get; set; }
            /// <summary>
            /// Gets or sets the List of music sources
            /// </summary>
            [JsonProperty("musicSources")]
            public MusicSourcesItem[] MusicSources { get; set; }

            [JsonProperty("audioSrcScenarios")]
            public AudioSrcScenariosItem[] AudioSrcScenarios { get; set; }

            [JsonProperty("audioSrcSharingScenarios")]
            public AudioSrcSharingScenariosItem[] AudioSrcSharingScenarios { get; set; }

            [JsonProperty("videoSources")]
            public VideoSourcesItem[] VideoSources { get; set; }

            [JsonProperty("videoSrcScenarios")]
            public VideoSrcScenariosItem[] VideoSrcScenarios { get; set; }

            [JsonProperty("vidConfigurationScenarios")]
            public VidConfigurationScenariosItem[] VidConfigurationScenarios { get; set; }

            [JsonProperty("liftScenarios")]
            public LiftScenariosItem[] LiftScenarios { get; set; }

            [JsonProperty("liftCommands")]
            public LiftCommandsItem[] LiftCommands { get; set; }

            [JsonProperty("sleepScenarios")]
            public SleepScenariosItem[] SleepScenarios { get; set; }

            [JsonProperty("sleepCommands")]
            public SleepCommandsItem[] SleepCommands { get; set; }

            [JsonProperty("formatScenarios")]
            public FormatScenariosItem[] FormatScenarios { get; set; }

            [JsonProperty("formatCommands")]
            public FormatCommandsItem[] FormatCommands { get; set; }
            /// <summary>
            /// Gets or sets the time the config file was last updated
            /// </summary>
            [JsonProperty("lastupdate")]
            public string LastUpdate { get; set; }
        }
    }
}