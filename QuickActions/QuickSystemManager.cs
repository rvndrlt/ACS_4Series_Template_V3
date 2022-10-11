using System;
using System.Collections.Generic;
using System.Linq;
using Crestron.SimplSharp;
using Crestron.SimplSharpPro;

namespace ACS_4Series_Template_V2
{
    class QuickSystemManager
    {
        public Dictionary<ushort, MusicPresets.MusicPresetsConfig> MusicPresetZ = new Dictionary<ushort, ACS_4Series_Template_V2.MusicPresets.MusicPresetsConfig>();
        private readonly MusicPresets.MusicPresetsConfig musicPreset;
        public QuickSystemManager(QuickConfiguration.QuickActionConfigData.QuickConfiguration QuickConfig, CrestronControlSystem cs)
        {
            CrestronConsole.PrintLine("quick action manager start");
            if (QuickConfig.MusicPresets != null)
            {
                foreach (var musicPre in QuickConfig.MusicPresets)
                {
                    musicPreset = new MusicPresets.MusicPresetsConfig(musicPre.MusicPresetNum, musicPre.MusicPresetName, musicPre.Sources, musicPre.Volumes);
                    this.MusicPresetZ[musicPre.MusicPresetNum] = this.musicPreset;
                    CrestronConsole.PrintLine("musicPre.Name {0}", this.MusicPresetZ[musicPre.MusicPresetNum].PresetName);
                }
            }
        }
    }
}
