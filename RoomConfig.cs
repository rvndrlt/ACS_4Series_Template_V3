using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Crestron.SimplSharp;

namespace ACS_4Series_Template_V1.Room
{
    public class RoomConfig
    {
        public RoomConfig(ushort number, string name, ushort subSystemScenario, ushort videoSrcScenario, ushort audioSrcScenario, ushort audioSrcSharingScenario, ushort configurationScenario, ushort liftScenario, ushort sleepScenario, ushort formatScenario, ushort audioID, ushort videoOutputNum, ushort lightsID, ushort shadesID, ushort climateID, ushort miscID, ushort tvOutToAudioInputNumber, string imageURL )
        {
            this.Number = number;
            this.Name = name;
            this.SubSystemScenario = subSystemScenario;
            this.VideoSrcScenario = videoSrcScenario;
            this.AudioSrcScenario = audioSrcScenario;
            this.AudioSrcSharingScenario = audioSrcSharingScenario;
            this.ConfigurationScenario = configurationScenario;
            this.LiftScenario = liftScenario;
            this.SleepScenario = sleepScenario;
            this.FormatScenario = formatScenario;
            this.AudioID = audioID;
            this.VideoOutputNum = videoOutputNum;
            this.LightsID = lightsID;
            this.ShadesID = shadesID;
            this.ClimateID = climateID;
            this.MiscID = miscID;
            this.TvOutToAudioInputNumber = tvOutToAudioInputNumber;
            this.ImageURL = imageURL;
        }
        //defined from json
        public ushort Number { get; set; }
        public string Name { get; set; }
        public ushort SubSystemScenario { get; set; }
        public ushort VideoSrcScenario { get; set; }
        public ushort AudioSrcScenario { get; set; }
        public ushort AudioSrcSharingScenario { get; set; }
        public ushort ConfigurationScenario { get; set; }
        public ushort LiftScenario { get; set; }
        public ushort SleepScenario { get; set; }
        public ushort FormatScenario { get; set; }
        public ushort AudioID { get; set; }
        public ushort VideoOutputNum { get; set; }
        public ushort LightsID { get; set; }
        public ushort ShadesID { get; set; }
        public ushort ClimateID { get; set; }
        public ushort MiscID { get; set; }
        public ushort TvOutToAudioInputNumber { get; set; }
        public string ImageURL { get; set; }
        //defined by program
        public ushort CurrentVideoSrc { get; set; }
        public ushort CurrentMusicSrc { get; set; }
        public ushort CurrentSubsystem { get; set; }
        public bool LastSystemVid { get; set; }

        public bool LightsAreOff { get; set; }

        public string LightStatusText { get; set; }
        public string MusicStatusText { get; set; }
        public string VideoStatusText { get; set; }
        public string HVACStatusText { get; set; }
        public ushort CurrentTemperature { get; set; }
        public ushort CurrentHeatSetpoint { get; set; }
        public ushort CurrentCoolSetpoint { get; set; }
        public string ClimateMode { get; set; }
    }
}