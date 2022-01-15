using System;
using System.Collections.Generic;
using System.Linq;
using Crestron.SimplSharp;
using Crestron.SimplSharpPro;

namespace ACS_4Series_Template_V1
{
    class QuickSystemManager
    {
        public Dictionary<ushort, MusicPresets.MusicPresetsConfig> MusicPresetZ = new Dictionary<ushort, ACS_4Series_Template_V1.MusicPresets.MusicPresetsConfig>();
        private readonly MusicPresets.MusicPresetsConfig musicPreset;
        public QuickSystemManager(QuickConfiguration.QuickActionConfigData.QuickConfiguration config, CrestronControlSystem cs)
        {
            CrestronConsole.PrintLine("quick action manager start");
            if (config.MusicPresets != null)
            {
                foreach (var musicPre in config.MusicPresets)
                {
                    musicPreset = new MusicPresets.MusicPresetsConfig(musicPre.MusicPresetNum, musicPre.Name, musicPre.Sources, musicPre.Volumes);
                    this.MusicPresetZ[musicPre.MusicPresetNum] = this.musicPreset;
                    CrestronConsole.PrintLine("musicPre.Name {0}", musicPre.Name);
                }
            }
        }
    }
}
