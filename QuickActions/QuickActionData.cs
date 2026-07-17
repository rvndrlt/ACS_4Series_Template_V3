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

        /// <summary>Site location for astronomical (sunrise/sunset) schedules.
        /// Auto-populated at boot from the processor's Toolbox latitude/longitude
        /// (Source="auto"), or overridden via the "quickactionloc" console command
        /// (Source="manual"). Null only when neither is available — astronomical
        /// scheduling is disabled in the UI until then.</summary>
        [JsonProperty("location", NullValueHandling = NullValueHandling.Ignore)]
        public SiteLocation Location { get; set; }
    }

    public class SiteLocation
    {
        [JsonProperty("latitude")]
        public double Latitude { get; set; }

        [JsonProperty("longitude")]
        public double Longitude { get; set; }

        /// <summary>"auto" = derived from the processor's configured lat/long (refreshed
        /// each boot); "manual" = set via quickactionloc (never overwritten by auto-read).
        /// Absent on legacy stores; treated as "manual" so a hand-set value is preserved.</summary>
        [JsonProperty("source", NullValueHandling = NullValueHandling.Ignore)]
        public string Source { get; set; }
    }

    /// <summary>One scheduled firing of a quick action (max 5 per action).</summary>
    public class QuickSchedule
    {
        /// <summary>Days of week, 0=Sunday .. 6=Saturday (JS Date.getDay convention).</summary>
        [JsonProperty("days")]
        public List<int> Days { get; set; } = new List<int>();

        /// <summary>"clock" | "sunrise" | "sunset"</summary>
        [JsonProperty("mode")]
        public string Mode { get; set; } = "clock";

        /// <summary>Clock mode: 24-hour "HH:mm".</summary>
        [JsonProperty("time", NullValueHandling = NullValueHandling.Ignore)]
        public string Time { get; set; }

        /// <summary>Astronomical modes: offset in minutes from the event, clamped ±240.</summary>
        [JsonProperty("offset")]
        public int Offset { get; set; }
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

        /// <summary>Room numbers included in this action's snapshot. Null/absent = all
        /// rooms (pre-feature actions load as null and keep whole-house behavior).
        /// Excluded rooms are untouched on recall: music/climate payloads simply never
        /// contain them; lights/shades scenes are created in App03 without them.</summary>
        [JsonProperty("includedRooms", NullValueHandling = NullValueHandling.Ignore)]
        public List<ushort> IncludedRooms { get; set; }

        /// <summary>Recurring schedules for this action (max 5). Null/absent = none;
        /// pre-scheduler actions load unchanged.</summary>
        [JsonProperty("schedules", NullValueHandling = NullValueHandling.Ignore)]
        public List<QuickSchedule> Schedules { get; set; }
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
