using System.Collections.Generic;
using Newtonsoft.Json;

namespace ACS_4Series_Template_V3.QuickActions
{
    /// <summary>
    /// JSON data model for \NVRAM\quickActions.json — the HTML-panel quick action store.
    /// A quick action is a named snapshot of ONE subsystem's whole-house state:
    ///   music/climate: full payload stored here (App01 owns the state),
    ///   lights/shades: a reference to an App03 house scene (sceneIndex + sceneName).
    /// Independent of the legacy Smart-Graphics quickActionConfig.xml store.
    /// </summary>
    public class QuickActionStore
    {
        [JsonProperty("version")]
        public int Version { get; set; } = 1;

        [JsonProperty("nextId")]
        public int NextId { get; set; } = 1;

        [JsonProperty("actions")]
        public List<QuickAction> Actions { get; set; } = new List<QuickAction>();
    }

    public class QuickAction
    {
        [JsonProperty("id")]
        public int Id { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        /// <summary>"music" | "climate" | "lights" | "shades"</summary>
        [JsonProperty("subsystem")]
        public string Subsystem { get; set; }

        [JsonProperty("favorite")]
        public bool Favorite { get; set; }

        [JsonProperty("music", NullValueHandling = NullValueHandling.Ignore)]
        public MusicPayload Music { get; set; }

        [JsonProperty("climate", NullValueHandling = NullValueHandling.Ignore)]
        public ClimatePayload Climate { get; set; }

        /// <summary>App03 house scene index (0-based) — lights/shades only.</summary>
        [JsonProperty("sceneIndex", NullValueHandling = NullValueHandling.Ignore)]
        public int? SceneIndex { get; set; }

        /// <summary>App03 house scene name at creation time — used to re-validate/re-bind
        /// sceneIndex when scenes are edited or deleted outside this program.</summary>
        [JsonProperty("sceneName", NullValueHandling = NullValueHandling.Ignore)]
        public string SceneName { get; set; }
    }

    public class MusicPayload
    {
        [JsonProperty("zones")]
        public List<MusicZoneSetting> Zones { get; set; } = new List<MusicZoneSetting>();
    }

    /// <summary>One audio zone's state. source 0 = zone off (snapshots include off
    /// zones so a recall restores the whole house exactly).</summary>
    public class MusicZoneSetting
    {
        [JsonProperty("audioId")]
        public ushort AudioId { get; set; }

        [JsonProperty("source")]
        public ushort Source { get; set; }

        [JsonProperty("volume")]
        public ushort Volume { get; set; }
    }

    public class ClimatePayload
    {
        [JsonProperty("zones")]
        public List<ClimateZoneSetting> Zones { get; set; } = new List<ClimateZoneSetting>();
    }

    /// <summary>One thermostat's state. Setpoints stored as displayed integers;
    /// the ×10 EISC scaling happens in the recall path only (matches RecallClimatePreset).</summary>
    public class ClimateZoneSetting
    {
        [JsonProperty("climateId")]
        public ushort ClimateId { get; set; }

        /// <summary>1=Auto 2=Heat 3=Cool 4=Off (ClimateModeNumber convention).</summary>
        [JsonProperty("mode")]
        public ushort Mode { get; set; }

        [JsonProperty("heatSp")]
        public ushort HeatSp { get; set; }

        [JsonProperty("coolSp")]
        public ushort CoolSp { get; set; }

        [JsonProperty("autoSp")]
        public ushort AutoSp { get; set; }

        [JsonProperty("singleSetpoint")]
        public bool SingleSetpoint { get; set; }
    }
}
