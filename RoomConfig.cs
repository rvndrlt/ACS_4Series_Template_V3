﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Crestron.SimplSharp;

namespace ACS_4Series_Template_V3.Room
{
    public class RoomConfig
    {
        public RoomConfig(ushort number, string name, ushort subSystemScenario, ushort audioSrcScenario, ushort audioSrcSharingScenario, ushort sleepScenario, ushort audioID, ushort lightsID, ushort shadesID, ushort climateID, ushort miscID, ushort openSubsysNumOnRmSelect, string imageURL )
        {
            this.Number = number;
            this.Name = name;
            this.SubSystemScenario = subSystemScenario;
            this.AudioSrcScenario = audioSrcScenario;
            this.AudioSrcSharingScenario = audioSrcSharingScenario;
            this.SleepScenario = sleepScenario;
            this.AudioID = audioID;
            this.LightsID = lightsID;
            this.ShadesID = shadesID;
            this.ClimateID = climateID;
            this.MiscID = miscID;
            this.OpenSubsysNumOnRmSelect = openSubsysNumOnRmSelect;
            this.ImageURL = imageURL;
        }
        //private
        private bool musicMuted;
        private ushort musicVolume;
        
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

        public List<ushort> ListOfDisplays = new List<ushort>();
        public ushort LightsID { get; set; }
        public ushort ShadesID { get; set; }
        public ushort ClimateID { get; set; }
        public ushort MiscID { get; set; }
        public ushort OpenSubsysNumOnRmSelect { get; set; }
        public ushort TvOutToAudioInputNumber { get; set; }
        public string ImageURL { get; set; }
        //defined by program
        public ushort CurrentVideoSrc { get; set; }
        public ushort NumberOfDisplays { get; set; }
        public ushort CurrentDisplayNumber { get; set; }
        public ushort CurrentMusicSrc { get; set; }
        public ushort CurrentSubsystem { get; set; }
        public bool LastSystemVid { get; set; }

        public bool MusicMuted
        {
            get { return musicMuted; }
            set
            {
                if (musicMuted != value)
                {
                    musicMuted = value;
                    OnMusicMutedChanged();
                }
            }
        }
        public bool VideoMuted { get; set; }

        public ushort MusicVolume
        {
            get { return musicVolume; }
            set
            {
                if (musicVolume != value)
                {
                    musicVolume = value;
                    OnMusicVolumeChanged();
                }
            }
        }
        public bool MusicVolRamping { get; set; }
        public bool VideoVolRamping { get; set; }
        public ushort VideoVolume { get; set; }

        public bool LightsAreOff { get; set; }

        public string LightStatusText { get; set; }
        public string MusicStatusText { get; set; }
        public string VideoStatusText { get; set; }
        public string HVACStatusText { get; set; }
        public ushort CurrentTemperature { get; set; }
        public ushort CurrentHeatSetpoint { get; set; }
        public ushort CurrentCoolSetpoint { get; set; }
        public ushort CurrentAutoSingleSetpoint { get; set; }
        public string ClimateMode { get; set; }

        public ushort ClimateModeNumber { get; set; }
        public bool ClimateAutoModeIsSingleSetpoint { get; set; }
        public event EventHandler MusicMutedChanged;
        public event EventHandler MusicVolumeChanged;
        protected virtual void OnMusicMutedChanged()
        {
            MusicMutedChanged?.Invoke(this, EventArgs.Empty);
        }
        protected virtual void OnMusicVolumeChanged()
        {
            MusicVolumeChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}