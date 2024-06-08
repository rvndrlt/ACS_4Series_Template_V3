﻿using System.Collections.Generic;
using Newtonsoft.Json;

namespace ACS_4Series_Template_V3.QuickConfiguration
{
    public class QuickActionConfigData
    {
        public class MusicPresetsItem
        {
            [JsonProperty("musicPresetNum")]
            public ushort MusicPresetNum { get; set; }

            [JsonProperty("musicPresetName")]
            public string MusicPresetName { get; set; }

            [JsonProperty("sources")]
            public List<ushort> Sources { get; set; }

            [JsonProperty("volumes")]
            public List<ushort> Volumes { get; set; }
        }
        public class QuickConfiguration
        {
            [JsonProperty("houseMusicPresets")]
            public MusicPresetsItem[] MusicPresets { get; set; }
        }
    }
}
