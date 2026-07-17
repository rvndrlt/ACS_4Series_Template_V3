using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Crestron.SimplSharp;
using Crestron.SimplSharp.CrestronIO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ACS_4Series_Template_V3.QuickActions
{
    /// <summary>
    /// Quick Actions for HTML panels. Persists named whole-house snapshots to
    /// \NVRAM\quickActions.json and exposes them to the panels as a JSON descriptor
    /// over raw serial joins (no contract/cse2j involvement — same style as the
    /// page descriptor on serial 1520):
    ///
    ///   1530 (C#→HTML): descriptor — full action list + canCreate/slotsFull capabilities
    ///   1531 (HTML→C#): command — {seq, cmd:"recall"|"create"|"delete"|"favorite", ...}
    ///   1532 (C#→HTML): result — {seq, forCmd, ok, reason, message}
    ///
    /// Music/climate actions store their payload here and recall through the same
    /// device paths the legacy XML presets use. Lights/shades actions reference an
    /// App03 (Lighting4Series) house scene by index; create/delete are forwarded to
    /// App03 over the scenario-2 EISCs (command values 301+idx / 401+idx, pending
    /// name on serial 620) and confirmed by App03's metadata re-push.
    ///
    /// The legacy Smart-Graphics quick action path (quickActionConfig.xml) is a
    /// completely separate store and is not touched by this class.
    /// </summary>
    public class QuickActionManager
    {
        private readonly ControlSystem _parent;

        private const string StoreFilePath = @"\NVRAM\quickActions.json";

        public const ushort DescriptorJoin = 1530; // serial C#→HTML
        public const ushort CommandJoin = 1531;    // serial HTML→C#
        public const ushort ResultJoin = 1532;     // serial C#→HTML
        public const ushort RoomsJoin = 1533;      // serial C#→HTML: rooms-by-floor catalog for the include picker

        private const int MaxHouseScenes = 10;      // App03 hard ceiling per subsystem
        private const int MaxNameLength = 40;
        private const int SceneOpTimeoutMs = 4000;  // wait for App03 metadata re-push
        private const int MaxSchedules = 5;         // schedule instances per action
        private const int MaxAstroOffsetMin = 240;  // ±4 h around sunrise/sunset

        private QuickActionStore store = new QuickActionStore();
        private readonly object storeLock = new object();

        private CTimer saveTimer;
        private CTimer volumeTimer;
        private CTimer revalidateTimer;
        private CTimer schedulerTimer;
        private string lastTickKey = "";
        private int descriptorSeq;
        private int resultSeq;

        // One in-flight App03 create/delete at a time (per whole system — these are rare,
        // user-driven operations; a second request while busy gets a "busy" result).
        private PendingSceneOp pendingOp;
        private CTimer pendingOpTimer;

        private class PendingSceneOp
        {
            public string Kind;        // "create" | "delete"
            public string Subsystem;   // "lights" | "shades"
            public string Name;        // create: new scene/action name
            public int SceneIndex;     // create: append slot; delete: slot being removed
            public ushort ExpectedCount;
            public ushort TpNumber;    // panel to answer on
            public int ActionId;       // delete: quick action being removed
            public List<ushort> IncludedRooms; // create: room numbers in the snapshot (null = all)
        }

        public QuickActionManager(ControlSystem parent)
        {
            _parent = parent;
        }

        // ─── Persistence ───────────────────────────────────────────────────

        public void Load()
        {
            try
            {
                if (File.Exists(StoreFilePath))
                {
                    string json = File.ReadToEnd(StoreFilePath, Encoding.UTF8);
                    var loaded = JsonConvert.DeserializeObject<QuickActionStore>(json);
                    if (loaded != null && loaded.Actions != null)
                    {
                        store = loaded;
                    }
                    CrestronConsole.PrintLine("QuickActions: loaded {0} actions from {1}", store.Actions.Count, StoreFilePath);
                }
                else
                {
                    CrestronConsole.PrintLine("QuickActions: no store file yet ({0})", StoreFilePath);
                }
            }
            catch (Exception ex)
            {
                ErrorLog.Error("QuickActions Load error: {0}", ex.Message);
                store = new QuickActionStore();
            }

            RegisterConsoleCommands();
            AutoLoadProcessorLocation();
            ScheduleNextTick();
        }

        // ─── Site location auto-read ───────────────────────────────────────

        /// <summary>
        /// Populate the astronomical site location from the processor's Toolbox-configured
        /// latitude/longitude so sunrise/sunset schedules work without the manual
        /// "quickactionloc" console command. Runs each boot:
        ///   - a "manual" location (set via quickactionloc) is never overwritten;
        ///   - an "auto" location is refreshed so Toolbox edits propagate on the next reboot;
        ///   - only runs on Appliance platforms — VC-4 (Server) has no single site location,
        ///     so it keeps relying on the stored/manual value.
        /// The manual command remains available as an override in all cases.
        /// </summary>
        private void AutoLoadProcessorLocation()
        {
            // Skip if the user pinned a manual override. Legacy stores have no Source; treat
            // them as manual so a previously hand-set value is preserved on upgrade.
            lock (storeLock)
            {
                if (store.Location != null && store.Location.Source != "auto")
                    return;
            }

            if (CrestronEnvironment.DevicePlatform != eDevicePlatform.Appliance)
                return;

            double lat, lng;
            if (TryReadProcessorLocation(out lat, out lng))
            {
                lock (storeLock)
                {
                    store.Location = new SiteLocation { Latitude = lat, Longitude = lng, Source = "auto" };
                }
                ScheduleSave();
                CrestronConsole.PrintLine(
                    "QuickActions: site location auto-loaded from processor: {0}, {1}\r\n", lat, lng);
            }
            else
            {
                CrestronConsole.PrintLine(
                    "QuickActions: processor latitude/longitude unavailable or unset; " +
                    "astronomical schedules need 'quickactionloc <lat> <long>'\r\n");
            }
        }

        /// <summary>
        /// Read the processor's configured latitude and longitude via the console and
        /// validate them. Returns false if either can't be read/parsed, is out of range,
        /// or is the (0,0) "null island" that Toolbox reports when location is unset.
        /// </summary>
        private bool TryReadProcessorLocation(out double lat, out double lng)
        {
            lat = 0; lng = 0;
            double latVal, lngVal;
            if (!TryReadConsoleDouble("latitude", out latVal)) return false;
            if (!TryReadConsoleDouble("longitude", out lngVal)) return false;

            if (latVal < -90 || latVal > 90 || lngVal < -180 || lngVal > 180) return false;
            // (0,0) is off the coast of Africa — in practice it means "not configured".
            if (Math.Abs(latVal) < 0.0001 && Math.Abs(lngVal) < 0.0001) return false;

            lat = latVal; lng = lngVal;
            return true;
        }

        /// <summary>
        /// Issue a control-system console command and extract the first signed decimal
        /// number from its response (e.g. the value out of "Latitude: 34.052235").
        /// </summary>
        private static bool TryReadConsoleDouble(string command, out double value)
        {
            value = 0;
            try
            {
                string resp = string.Empty;
                // Ignore the return value — its type varies across SimplSharp versions;
                // the populated response string is the reliable signal.
                CrestronConsole.SendControlSystemCommand(command, ref resp);
                if (string.IsNullOrEmpty(resp))
                    return false;

                var m = System.Text.RegularExpressions.Regex.Match(resp, @"-?\d+(\.\d+)?");
                if (!m.Success)
                {
                    CrestronConsole.PrintLine(
                        "QuickActions: could not parse a number from '{0}' response: {1}",
                        command, resp.Trim());
                    return false;
                }
                return double.TryParse(m.Value, out value);
            }
            catch (Exception ex)
            {
                ErrorLog.Error("QuickActions: reading '{0}' from processor failed: {1}", command, ex.Message);
                return false;
            }
        }

        private void RegisterConsoleCommands()
        {
            try
            {
                CrestronConsole.AddNewConsoleCommand(HandleLocationCommand, "quickactionloc",
                    "Set site location for astronomical quick-action schedules: quickactionloc <latitude> <longitude>",
                    ConsoleAccessLevelEnum.AccessOperator);
            }
            catch (Exception ex)
            {
                ErrorLog.Error("QuickActions console cmd registration error: {0}", ex.Message);
            }
        }

        private void HandleLocationCommand(string args)
        {
            var parts = (args ?? "").Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            double lat, lng;
            if (parts.Length != 2 ||
                !double.TryParse(parts[0], out lat) || !double.TryParse(parts[1], out lng) ||
                lat < -90 || lat > 90 || lng < -180 || lng > 180)
            {
                CrestronConsole.ConsoleCommandResponse(BuildLocationStatus());
                return;
            }
            lock (storeLock)
            {
                store.Location = new SiteLocation { Latitude = lat, Longitude = lng, Source = "manual" };
            }
            ScheduleSave();
            SendDescriptorToAll();
            CrestronConsole.ConsoleCommandResponse("Location saved.\r\n" + BuildLocationStatus());
        }

        /// <summary>
        /// Human-readable summary of the current site location: source (auto from the
        /// processor vs. manual override), the timezone SunCalc uses, and today's resolved
        /// sunrise/sunset — so you can confirm at a glance where the value came from and
        /// that it computes sensibly. Also prints the usage line as a reminder.
        /// </summary>
        private string BuildLocationStatus()
        {
            SiteLocation cur;
            lock (storeLock) { cur = store.Location; }

            var sb = new StringBuilder();
            sb.Append("usage: quickactionloc <latitude> <longitude>\r\n");

            if (cur == null)
            {
                sb.Append("current: (not set — astronomical schedules disabled)\r\n");
                sb.Append("Set the processor's latitude/longitude in Toolbox (auto-loads on reboot), ");
                sb.Append("or set it here manually with the command above.\r\n");
                return sb.ToString();
            }

            string source = string.IsNullOrEmpty(cur.Source) ? "manual (legacy)"
                : cur.Source == "auto" ? "auto (from processor / Toolbox)"
                : "manual (override)";

            sb.Append("current:  ").Append(cur.Latitude).Append(", ").Append(cur.Longitude).Append("\r\n");
            sb.Append("source:   ").Append(source).Append("\r\n");
            sb.Append("timezone: ").Append(TimeZoneInfo.Local.StandardName)
              .Append(" (UTC").Append(TimeZoneInfo.Local.GetUtcOffset(DateTime.Now).TotalHours.ToString("+0.##;-0.##"))
              .Append(")\r\n");

            DateTime rise, set;
            if (SunCalc.TryGetSunTimes(DateTime.Now, cur.Latitude, cur.Longitude, out rise, out set))
                sb.Append("today:    sunrise ").Append(rise.ToString("HH:mm"))
                  .Append(", sunset ").Append(set.ToString("HH:mm")).Append("\r\n");
            else
                sb.Append("today:    (no sunrise/sunset at this latitude today)\r\n");

            return sb.ToString();
        }

        private void SaveNow()
        {
            try
            {
                string json;
                lock (storeLock)
                {
                    json = JsonConvert.SerializeObject(store, Formatting.Indented);
                }
                using (var fs = new FileStream(StoreFilePath, FileMode.Create))
                using (var writer = new StreamWriter(fs))
                {
                    writer.Write(json);
                }
            }
            catch (Exception ex)
            {
                ErrorLog.Error("QuickActions Save error: {0}", ex.Message);
            }
        }

        private void ScheduleSave()
        {
            if (saveTimer != null)
                saveTimer.Stop();
            saveTimer = new CTimer(o => SaveNow(), 5000);
        }

        // ─── Descriptor (1530) ─────────────────────────────────────────────

        private bool SystemHasMusic()
        {
            return _parent.manager.RoomZ.Any(rm => rm.Value.AudioID > 0);
        }

        /// <summary>
        /// Honor the per-subsystem "quickActionsEnabled" config flag. The quick-action
        /// categories (music/lights/shades/climate) are matched to the configured
        /// subsystems by their conventional name; a category is disabled only when a
        /// matching subsystem entry explicitly sets quickActionsEnabled=false.
        /// No matching subsystem -> enabled (default), so existing configs are unaffected.
        /// </summary>
        private bool QuickActionsEnabledFor(string category)
        {
            foreach (var s in _parent.manager.SubsystemZ.Values)
            {
                if (SubsystemMatchesCategory(s.Name, category) && !s.QuickActionsEnabled)
                    return false;
            }
            return true;
        }

        private static bool SubsystemMatchesCategory(string name, string category)
        {
            if (string.IsNullOrEmpty(name)) return false;
            name = name.ToLower();
            switch (category)
            {
                case "music":   return name.Contains("audio") || name.Contains("music") || name.Contains("sound");
                case "lights":  return name.Contains("light");
                case "shades":  return name.Contains("shade") || name.Contains("blind") || name.Contains("drape");
                case "climate": return name.Contains("climate") || name.Contains("hvac") || name.Contains("therm");
                default:        return false;
            }
        }

        private bool SystemHasClimate()
        {
            return _parent.manager.RoomZ.Any(rm => rm.Value.ClimateID > 0);
        }

        private ushort LightsSceneCount()
        {
            return _parent.lightingScenario2Control != null ? _parent.lightingScenario2Control.GetHouseSceneCount() : (ushort)0;
        }

        private ushort ShadesSceneCount()
        {
            return _parent.shadesScenario2Control != null ? _parent.shadesScenario2Control.GetHouseSceneCount() : (ushort)0;
        }

        private object BuildSunInfo()
        {
            SiteLocation loc;
            lock (storeLock) { loc = store.Location; }
            DateTime rise, set;
            if (loc != null && SunCalc.TryGetSunTimes(DateTime.Now, loc.Latitude, loc.Longitude, out rise, out set))
            {
                return new { hasLocation = true, sunrise = rise.ToString("HH:mm"), sunset = set.ToString("HH:mm") };
            }
            return new { hasLocation = false, sunrise = "", sunset = "" };
        }

        private string BuildDescriptorJson()
        {
            object sun = BuildSunInfo();
            object payload;
            lock (storeLock)
            {
                payload = new
                {
                    seq = ++descriptorSeq,
                    sun,
                    actions = store.Actions.Select(a => new
                    {
                        id = a.Id,
                        name = a.Name,
                        subsystem = a.Subsystem,
                        favorite = a.Favorite,
                        schedules = a.Schedules ?? new List<QuickSchedule>()
                    }).ToArray(),
                    canCreate = new
                    {
                        music = SystemHasMusic() && QuickActionsEnabledFor("music"),
                        climate = SystemHasClimate() && QuickActionsEnabledFor("climate"),
                        lights = _parent.lightingScenario2Control != null && _parent.lightingScenario2Control.IsConfigured && QuickActionsEnabledFor("lights"),
                        shades = _parent.shadesScenario2Control != null && _parent.shadesScenario2Control.IsConfigured && QuickActionsEnabledFor("shades")
                    },
                    slotsFull = new
                    {
                        lights = LightsSceneCount() >= MaxHouseScenes,
                        shades = ShadesSceneCount() >= MaxHouseScenes
                    }
                };
            }
            return JsonConvert.SerializeObject(payload);
        }

        /// <summary>
        /// Rooms-by-floor catalog for the HTML "rooms to include" picker (serial 1533).
        /// Per room: which quick-action subsystems it participates in. Rooms not in any
        /// configured floor are grouped under floor 0 "Other".
        /// </summary>
        private string BuildRoomsCatalogJson()
        {
            var floors = new List<object>();
            var assignedRooms = new HashSet<ushort>();

            foreach (var flr in _parent.manager.Floorz.Values.OrderBy(f => f.FloorNumber))
            {
                var roomEntries = new List<object>();
                if (flr.IncludedRooms != null)
                {
                    foreach (ushort roomNum in flr.IncludedRooms)
                    {
                        if (!_parent.manager.RoomZ.ContainsKey(roomNum)) continue;
                        var rm = _parent.manager.RoomZ[roomNum];
                        assignedRooms.Add(roomNum);
                        roomEntries.Add(new
                        {
                            num = roomNum,
                            name = rm.Name,
                            music = rm.AudioID > 0,
                            lights = rm.LightsID > 0,
                            shades = rm.ShadesID > 0,
                            climate = rm.ClimateID > 0
                        });
                    }
                }
                if (roomEntries.Count > 0)
                    floors.Add(new { num = flr.FloorNumber, name = flr.Name, rooms = roomEntries });
            }

            var orphans = new List<object>();
            foreach (var rm in _parent.manager.RoomZ)
            {
                if (assignedRooms.Contains(rm.Key)) continue;
                orphans.Add(new
                {
                    num = rm.Key,
                    name = rm.Value.Name,
                    music = rm.Value.AudioID > 0,
                    lights = rm.Value.LightsID > 0,
                    shades = rm.Value.ShadesID > 0,
                    climate = rm.Value.ClimateID > 0
                });
            }
            if (orphans.Count > 0)
                floors.Add(new { num = 0, name = "Other", rooms = orphans });

            return JsonConvert.SerializeObject(new { seq = ++descriptorSeq, floors });
        }

        /// <summary>Push the descriptor + rooms catalog to one HTML panel (boot / panel-online re-send).</summary>
        public void SendDescriptorTo(UI.TouchpanelUI tp)
        {
            try
            {
                if (tp == null || !tp.HTML_UI || tp.UserInterface == null) return;
                tp.UserInterface.StringInput[DescriptorJoin].StringValue = BuildDescriptorJson();
                tp.UserInterface.StringInput[RoomsJoin].StringValue = BuildRoomsCatalogJson();
            }
            catch (Exception ex)
            {
                ErrorLog.Error("QuickActions SendDescriptorTo error: {0}", ex.Message);
            }
        }

        public void SendDescriptorToAll()
        {
            string json = BuildDescriptorJson();
            string roomsJson = BuildRoomsCatalogJson();
            foreach (var tp in _parent.manager.touchpanelZ)
            {
                try
                {
                    if (tp.Value.HTML_UI && tp.Value.UserInterface != null)
                    {
                        tp.Value.UserInterface.StringInput[DescriptorJoin].StringValue = json;
                        tp.Value.UserInterface.StringInput[RoomsJoin].StringValue = roomsJson;
                    }
                }
                catch (Exception ex)
                {
                    ErrorLog.Error("QuickActions descriptor to TP-{0} error: {1}", tp.Key, ex.Message);
                }
            }
        }

        // ─── Result (1532) ─────────────────────────────────────────────────

        private void SendResult(ushort tpNumber, string forCmd, bool ok, string reason, string message)
        {
            try
            {
                if (!_parent.manager.touchpanelZ.ContainsKey(tpNumber)) return;
                var tp = _parent.manager.touchpanelZ[tpNumber];
                if (!tp.HTML_UI || tp.UserInterface == null) return;
                string json = JsonConvert.SerializeObject(new
                {
                    seq = ++resultSeq,
                    forCmd,
                    ok,
                    reason = reason ?? string.Empty,
                    message = message ?? string.Empty
                });
                tp.UserInterface.StringInput[ResultJoin].StringValue = json;
            }
            catch (Exception ex)
            {
                ErrorLog.Error("QuickActions SendResult error: {0}", ex.Message);
            }
        }

        // ─── Command (1531) ────────────────────────────────────────────────

        /// <summary>Entry point from TouchpanelUI.SigChange for serial join 1531.</summary>
        public void HandleCommand(ushort tpNumber, string json)
        {
            if (string.IsNullOrEmpty(json)) return;
            try
            {
                var obj = JObject.Parse(json);
                string cmd = (string)obj["cmd"] ?? string.Empty;
                CrestronConsole.PrintLine("QuickActions: TP-{0} cmd \"{1}\"", tpNumber, cmd);
                switch (cmd)
                {
                    case "recall":
                        Recall(tpNumber, (int?)obj["id"] ?? 0);
                        break;
                    case "create":
                        List<ushort> includeRooms = null;
                        var arr = obj["includeRooms"] as JArray;
                        if (arr != null && arr.Count > 0)
                        {
                            includeRooms = arr.Select(t => (ushort)t).ToList();
                        }
                        Create(tpNumber, ((string)obj["subsystem"] ?? string.Empty).ToLower(), (string)obj["name"], includeRooms);
                        break;
                    case "delete":
                        Delete(tpNumber, (int?)obj["id"] ?? 0);
                        break;
                    case "favorite":
                        SetFavorite(tpNumber, (int?)obj["id"] ?? 0, (bool?)obj["value"] ?? false);
                        break;
                    case "setSchedules":
                        SetSchedules(tpNumber, (int?)obj["id"] ?? 0, obj["schedules"] as JArray);
                        break;
                    default:
                        CrestronConsole.PrintLine("QuickActions: unknown cmd \"{0}\"", cmd);
                        break;
                }
            }
            catch (Exception ex)
            {
                ErrorLog.Error("QuickActions HandleCommand error: {0} json: {1}", ex.Message, json);
            }
        }

        private QuickAction FindAction(int id)
        {
            lock (storeLock)
            {
                return store.Actions.FirstOrDefault(a => a.Id == id);
            }
        }

        // ─── Recall ────────────────────────────────────────────────────────

        private void Recall(ushort tpNumber, int id)
        {
            var action = FindAction(id);
            if (action == null)
            {
                SendResult(tpNumber, "recall", false, "notFound", "Quick action not found");
                return;
            }
            switch (action.Subsystem)
            {
                case "music":
                    RecallMusic(action.Music);
                    SendResult(tpNumber, "recall", true, "", action.Name);
                    break;
                case "climate":
                    RecallClimate(action.Climate);
                    SendResult(tpNumber, "recall", true, "", action.Name);
                    break;
                case "lights":
                case "shades":
                    RecallHouseScene(tpNumber, action);
                    break;
                default:
                    SendResult(tpNumber, "recall", false, "badSubsystem", "Unknown subsystem");
                    break;
            }
        }

        /// <summary>
        /// Whole-house music recall from a JSON payload. Mirrors
        /// MusicSystemControl.RecallMusicPreset (which reads the legacy XML arrays):
        /// switch every zone first, then send volumes on a 3 s timer so switching
        /// settles. Shares RecallMusicPresetTimerBusy / the NAX panel-refresh timer
        /// so XML and JSON recalls can't run interleaved refresh cycles.
        /// </summary>
        private void RecallMusic(MusicPayload payload)
        {
            if (payload == null || payload.Zones == null) return;
            var music = _parent.musicSystemControl;

            if (!music.RecallMusicPresetTimerBusy)
            {
                if (_parent.nax.NAXoutputChangedTimer != null)
                {
                    _parent.nax.NAXoutputChangedTimer.Stop();
                    _parent.nax.NAXoutputChangedTimer.Dispose();
                }
                _parent.nax.NAXoutputChangedTimer = new CTimer(o =>
                {
                    _parent.nax.NAXoutputChangedTimer.Stop();
                    _parent.nax.NAXoutputChangedTimer.Dispose();
                    music.UpdateAllPanelsTextWhenAudioChanges(suppressPageFlip: true); // recall is whole-house from the home page — update text but never flip a panel to the media player
                    music.RecallMusicPresetTimerBusy = false;
                }, 0, 5000);
                music.RecallMusicPresetTimerBusy = true;
            }

            var byAudioId = payload.Zones.ToDictionary(z => z.AudioId, z => z);
            foreach (var rm in _parent.manager.RoomZ)
            {
                ushort switcherOutputNum = rm.Value.AudioID;
                if (switcherOutputNum > 0 && byAudioId.ContainsKey(switcherOutputNum))
                {
                    ushort src = byAudioId[switcherOutputNum].Source;
                    music.SwitcherSelectMusicSource(switcherOutputNum, src); // src 0 = zone off
                    music.ReceiverOnOffFromDistAudio(rm.Value.Number, src);
                }
            }

            var zones = payload.Zones;
            if (volumeTimer != null)
            {
                volumeTimer.Stop();
                volumeTimer.Dispose();
            }
            volumeTimer = new CTimer(o =>
            {
                foreach (var z in zones)
                {
                    if (z.AudioId > 0 && z.Source > 0)
                    {
                        _parent.VOLUMEEISC.UShortInput[z.AudioId].UShortValue = z.Volume;
                    }
                }
            }, 3000);
        }

        /// <summary>
        /// Whole-house climate recall from a JSON payload. Join scheme matches
        /// QuickActionControl.RecallClimatePreset: HVACEISC mode pulse bools at
        /// zone/+100/+200/+300, setpoints ushort at +100/+200 scaled ×10.
        /// </summary>
        private void RecallClimate(ClimatePayload payload)
        {
            if (payload == null || payload.Zones == null) return;
            var byClimateId = payload.Zones.ToDictionary(z => z.ClimateId, z => z);
            foreach (var rm in _parent.manager.RoomZ)
            {
                ushort zone = rm.Value.ClimateID;
                if (zone == 0 || !byClimateId.ContainsKey(zone)) continue;
                var z = byClimateId[zone];
                switch (z.Mode)
                {
                    case 1: // auto
                        _parent.HVACEISC.BooleanInput[zone].BoolValue = true;
                        if (rm.Value.ClimateAutoModeIsSingleSetpoint)
                        {
                            _parent.HVACEISC.UShortInput[(ushort)(zone + 100)].UShortValue = (ushort)(z.AutoSp * 10);
                        }
                        else
                        {
                            _parent.HVACEISC.UShortInput[(ushort)(zone + 100)].UShortValue = (ushort)(z.HeatSp * 10);
                            _parent.HVACEISC.UShortInput[(ushort)(zone + 200)].UShortValue = (ushort)(z.CoolSp * 10);
                        }
                        _parent.HVACEISC.BooleanInput[zone].BoolValue = false;
                        break;
                    case 2: // heat
                        _parent.HVACEISC.BooleanInput[(ushort)(zone + 100)].BoolValue = true;
                        _parent.HVACEISC.UShortInput[(ushort)(zone + 100)].UShortValue = (ushort)(z.HeatSp * 10);
                        _parent.HVACEISC.BooleanInput[(ushort)(zone + 100)].BoolValue = false;
                        break;
                    case 3: // cool
                        _parent.HVACEISC.BooleanInput[(ushort)(zone + 200)].BoolValue = true;
                        _parent.HVACEISC.UShortInput[(ushort)(zone + 200)].UShortValue = (ushort)(z.CoolSp * 10);
                        _parent.HVACEISC.BooleanInput[(ushort)(zone + 200)].BoolValue = false;
                        break;
                    case 4: // off
                        _parent.HVACEISC.BooleanInput[(ushort)(zone + 300)].BoolValue = true;
                        _parent.HVACEISC.BooleanInput[(ushort)(zone + 300)].BoolValue = false;
                        break;
                    default:
                        break;
                }
            }
        }

        private void RecallHouseScene(ushort tpNumber, QuickAction action)
        {
            var ctrl = SceneControl(action.Subsystem);
            if (ctrl == null || !ctrl.IsConfigured)
            {
                SendResult(tpNumber, "recall", false, "eiscOffline", "Lighting system is not available");
                return;
            }
            if (!action.SceneIndex.HasValue)
            {
                SendResult(tpNumber, "recall", false, "notFound", "Scene reference missing");
                return;
            }
            // 201 + index = whole-house recall (Lighting4Series HandleSaveCommand)
            bool sent = ctrl.SendHouseSceneCommand(tpNumber, (ushort)(201 + action.SceneIndex.Value));
            SendResult(tpNumber, "recall", sent, sent ? "" : "eiscOffline", sent ? action.Name : "Lighting system is not available");
        }

        // ─── Create ────────────────────────────────────────────────────────

        /// <param name="includeRooms">Room numbers to include in the snapshot; null = all
        /// rooms (whole-house). Excluded rooms are left untouched when recalling.</param>
        private void Create(ushort tpNumber, string subsystem, string name, List<ushort> includeRooms)
        {
            name = (name ?? string.Empty).Trim();
            if (name.Length == 0)
            {
                SendResult(tpNumber, "create", false, "badName", "Name cannot be empty");
                return;
            }
            if (name.Length > MaxNameLength) name = name.Substring(0, MaxNameLength);

            if (!QuickActionsEnabledFor(subsystem))
            {
                SendResult(tpNumber, "create", false, "disabled", "Quick actions are disabled for this subsystem");
                return;
            }

            switch (subsystem)
            {
                case "music":
                    if (!SystemHasMusic())
                    {
                        SendResult(tpNumber, "create", false, "noSubsystem", "No music zones in this system");
                        return;
                    }
                    AddAction(SnapshotMusic(name, includeRooms));
                    SendResult(tpNumber, "create", true, "", name);
                    break;
                case "climate":
                    if (!SystemHasClimate())
                    {
                        SendResult(tpNumber, "create", false, "noSubsystem", "No climate zones in this system");
                        return;
                    }
                    AddAction(SnapshotClimate(name, includeRooms));
                    SendResult(tpNumber, "create", true, "", name);
                    break;
                case "lights":
                case "shades":
                    CreateHouseSceneAction(tpNumber, subsystem, name, includeRooms);
                    break;
                default:
                    SendResult(tpNumber, "create", false, "badSubsystem", "Unknown subsystem");
                    break;
            }
        }

        private static bool RoomIncluded(List<ushort> includeRooms, ushort roomNum)
        {
            return includeRooms == null || includeRooms.Contains(roomNum);
        }

        private void AddAction(QuickAction action)
        {
            lock (storeLock)
            {
                action.Id = store.NextId++;
                store.Actions.Add(action);
            }
            ScheduleSave();
            SendDescriptorToAll();
        }

        /// <summary>Snapshot included audio zones' cached state — same reads the legacy
        /// XML writeSubsystems() does (CurrentMusicSrc + VOLUMEEISC output). Zones for
        /// excluded rooms are omitted from the payload entirely, so recall never
        /// touches them.</summary>
        private QuickAction SnapshotMusic(string name, List<ushort> includeRooms)
        {
            var payload = new MusicPayload();
            foreach (var rm in _parent.manager.RoomZ)
            {
                if (rm.Value.AudioID > 0 && RoomIncluded(includeRooms, rm.Key))
                {
                    payload.Zones.Add(new MusicZoneSetting
                    {
                        AudioId = rm.Value.AudioID,
                        Source = rm.Value.CurrentMusicSrc,
                        Volume = _parent.VOLUMEEISC.UShortOutput[rm.Value.AudioID].UShortValue
                    });
                }
            }
            return new QuickAction { Name = name, Subsystem = "music", Music = payload, IncludedRooms = includeRooms };
        }

        private QuickAction SnapshotClimate(string name, List<ushort> includeRooms)
        {
            var payload = new ClimatePayload();
            foreach (var rm in _parent.manager.RoomZ)
            {
                if (rm.Value.ClimateID > 0 && RoomIncluded(includeRooms, rm.Key))
                {
                    payload.Zones.Add(new ClimateZoneSetting
                    {
                        ClimateId = rm.Value.ClimateID,
                        Mode = rm.Value.ClimateModeNumber,
                        HeatSp = rm.Value.CurrentHeatSetpoint,
                        CoolSp = rm.Value.CurrentCoolSetpoint,
                        AutoSp = rm.Value.CurrentAutoSingleSetpoint,
                        SingleSetpoint = rm.Value.ClimateAutoModeIsSingleSetpoint
                    });
                }
            }
            return new QuickAction { Name = name, Subsystem = "climate", Climate = payload, IncludedRooms = includeRooms };
        }

        private IHouseSceneBridge SceneControl(string subsystem)
        {
            if (subsystem == "lights") return _parent.lightingScenario2Control;
            if (subsystem == "shades") return _parent.shadesScenario2Control;
            return null;
        }

        /// <summary>
        /// Lights/shades create = tell App03 to snapshot the (included rooms of the)
        /// house into a new scene slot: send the include-list on serial 621 and the
        /// name on serial 620, then command 301+idx. App03 persists and re-pushes
        /// count+names; that re-push is our confirmation.
        /// </summary>
        private void CreateHouseSceneAction(ushort tpNumber, string subsystem, string name, List<ushort> includeRooms)
        {
            var ctrl = SceneControl(subsystem);
            if (ctrl == null || !ctrl.IsConfigured || !ctrl.EiscOnline)
            {
                SendResult(tpNumber, "create", false, "eiscOffline", "Lighting system is offline");
                return;
            }
            if (pendingOp != null)
            {
                SendResult(tpNumber, "create", false, "busy", "Another scene operation is in progress");
                return;
            }
            ushort count = ctrl.GetHouseSceneCount();
            if (count >= MaxHouseScenes)
            {
                SendResult(tpNumber, "create", false, "slotsFull",
                    string.Format("All {0} {1} scene slots are in use", MaxHouseScenes, subsystem));
                return;
            }

            pendingOp = new PendingSceneOp
            {
                Kind = "create",
                Subsystem = subsystem,
                Name = name,
                SceneIndex = count,
                ExpectedCount = (ushort)(count + 1),
                TpNumber = tpNumber,
                IncludedRooms = includeRooms
            };
            StartPendingOpTimeout();

            // Include-list = CSV of App03 room IDs (LightsID/ShadesID). Empty = all rooms.
            string includeCsv = "";
            if (includeRooms != null)
            {
                var ids = new List<string>();
                foreach (ushort roomNum in includeRooms)
                {
                    if (!_parent.manager.RoomZ.ContainsKey(roomNum)) continue;
                    var rm = _parent.manager.RoomZ[roomNum];
                    ushort id = subsystem == "lights" ? rm.LightsID : rm.ShadesID;
                    if (id > 0) ids.Add(id.ToString());
                }
                includeCsv = string.Join(",", ids);
            }
            ctrl.SendPendingIncludeRooms(includeCsv);
            ctrl.SendPendingSceneName(name);
            ctrl.SendHouseSceneCommand(tpNumber, (ushort)(301 + count));
        }

        // ─── Delete / favorite ─────────────────────────────────────────────

        private void Delete(ushort tpNumber, int id)
        {
            var action = FindAction(id);
            if (action == null)
            {
                SendResult(tpNumber, "delete", false, "notFound", "Quick action not found");
                return;
            }

            if (action.Subsystem == "music" || action.Subsystem == "climate")
            {
                lock (storeLock)
                {
                    store.Actions.Remove(action);
                }
                ScheduleSave();
                SendDescriptorToAll();
                SendResult(tpNumber, "delete", true, "", action.Name);
                return;
            }

            // lights/shades: delete the App03 scene too
            var ctrl = SceneControl(action.Subsystem);
            if (ctrl == null || !ctrl.IsConfigured || !ctrl.EiscOnline)
            {
                SendResult(tpNumber, "delete", false, "eiscOffline", "Lighting system is offline");
                return;
            }
            if (pendingOp != null)
            {
                SendResult(tpNumber, "delete", false, "busy", "Another scene operation is in progress");
                return;
            }
            if (!action.SceneIndex.HasValue)
            {
                // dangling reference — just drop the action
                lock (storeLock) { store.Actions.Remove(action); }
                ScheduleSave();
                SendDescriptorToAll();
                SendResult(tpNumber, "delete", true, "", action.Name);
                return;
            }

            ushort count = ctrl.GetHouseSceneCount();
            pendingOp = new PendingSceneOp
            {
                Kind = "delete",
                Subsystem = action.Subsystem,
                SceneIndex = action.SceneIndex.Value,
                ExpectedCount = (ushort)(count > 0 ? count - 1 : 0),
                TpNumber = tpNumber,
                ActionId = action.Id
            };
            StartPendingOpTimeout();

            ctrl.SendHouseSceneCommand(tpNumber, (ushort)(401 + action.SceneIndex.Value));
        }

        private void SetFavorite(ushort tpNumber, int id, bool value)
        {
            var action = FindAction(id);
            if (action == null)
            {
                SendResult(tpNumber, "favorite", false, "notFound", "Quick action not found");
                return;
            }
            lock (storeLock)
            {
                action.Favorite = value;
            }
            ScheduleSave();
            SendDescriptorToAll();
        }

        // ─── Scheduler ─────────────────────────────────────────────────────

        /// <summary>Replace the full schedule list for one action (UI sends the whole
        /// edited set each save — simplest replace semantics, max 5).</summary>
        private void SetSchedules(ushort tpNumber, int id, JArray schedulesJson)
        {
            var action = FindAction(id);
            if (action == null)
            {
                SendResult(tpNumber, "setSchedules", false, "notFound", "Quick action not found");
                return;
            }

            var schedules = new List<QuickSchedule>();
            if (schedulesJson != null)
            {
                foreach (var t in schedulesJson)
                {
                    if (schedules.Count >= MaxSchedules) break;
                    var s = new QuickSchedule
                    {
                        Mode = ((string)t["mode"] ?? "clock").ToLower(),
                        Time = (string)t["time"],
                        Offset = (int?)t["offset"] ?? 0,
                        Days = (t["days"] as JArray ?? new JArray())
                            .Select(d => (int)d).Where(d => d >= 0 && d <= 6).Distinct().OrderBy(d => d).ToList()
                    };
                    if (s.Days.Count == 0) continue; // no days = dead schedule, drop it
                    if (s.Mode == "clock")
                    {
                        int min = ParseClockMinutes(s.Time);
                        if (min < 0) continue;
                        s.Offset = 0;
                    }
                    else if (s.Mode == "sunrise" || s.Mode == "sunset")
                    {
                        s.Time = null;
                        s.Offset = Math.Max(-MaxAstroOffsetMin, Math.Min(MaxAstroOffsetMin, s.Offset));
                    }
                    else
                    {
                        continue;
                    }
                    schedules.Add(s);
                }
            }

            lock (storeLock)
            {
                action.Schedules = schedules.Count > 0 ? schedules : null;
            }
            ScheduleSave();
            SendDescriptorToAll();
            SendResult(tpNumber, "setSchedules", true, "", action.Name);
        }

        private static int ParseClockMinutes(string hhmm)
        {
            if (string.IsNullOrEmpty(hhmm)) return -1;
            var parts = hhmm.Split(':');
            int h, m;
            if (parts.Length != 2 || !int.TryParse(parts[0], out h) || !int.TryParse(parts[1], out m)) return -1;
            if (h < 0 || h > 23 || m < 0 || m > 59) return -1;
            return h * 60 + m;
        }

        private void ScheduleNextTick()
        {
            var now = DateTime.Now;
            int ms = (60 - now.Second) * 1000 - now.Millisecond + 250; // just past the minute boundary
            if (ms < 500) ms += 60000;
            if (schedulerTimer != null)
            {
                schedulerTimer.Stop();
                schedulerTimer.Dispose();
            }
            schedulerTimer = new CTimer(o =>
            {
                try { SchedulerTick(); }
                catch (Exception ex) { ErrorLog.Error("QuickActions scheduler tick error: {0}", ex.Message); }
                finally { ScheduleNextTick(); }
            }, ms);
        }

        private void SchedulerTick()
        {
            var now = DateTime.Now;
            string key = now.ToString("yyyyMMddHHmm");
            if (key == lastTickKey) return; // never double-fire the same minute
            lastTickKey = key;

            int today = (int)now.DayOfWeek; // 0=Sunday, matches the stored convention
            int nowMin = now.Hour * 60 + now.Minute;

            SiteLocation loc;
            var toFire = new List<QuickAction>();
            lock (storeLock)
            {
                loc = store.Location;
                foreach (var a in store.Actions)
                {
                    if (a.Schedules == null) continue;
                    foreach (var s in a.Schedules)
                    {
                        if (s.Days == null || !s.Days.Contains(today)) continue;
                        if (ResolveScheduleMinutes(s, now, loc) != nowMin) continue;
                        toFire.Add(a);
                        break; // one fire per action per minute even if schedules overlap
                    }
                }
            }

            foreach (var a in toFire)
            {
                CrestronConsole.PrintLine("QuickActions: scheduled fire \"{0}\" ({1}) at {2:HH:mm}", a.Name, a.Subsystem, now);
                RecallInternal(a);
            }
        }

        /// <summary>Minute-of-day this schedule resolves to for the given date, or -1 if
        /// it can't fire (bad data, astronomical mode without a location, or an offset
        /// that lands outside the same calendar day).</summary>
        private static int ResolveScheduleMinutes(QuickSchedule s, DateTime date, SiteLocation loc)
        {
            if (s.Mode == "clock") return ParseClockMinutes(s.Time);

            if (loc == null) return -1;
            DateTime rise, set;
            if (!SunCalc.TryGetSunTimes(date, loc.Latitude, loc.Longitude, out rise, out set)) return -1;
            DateTime baseTime = s.Mode == "sunrise" ? rise : (s.Mode == "sunset" ? set : DateTime.MinValue);
            if (baseTime == DateTime.MinValue) return -1;

            int offset = Math.Max(-MaxAstroOffsetMin, Math.Min(MaxAstroOffsetMin, s.Offset));
            int minutes = baseTime.Hour * 60 + baseTime.Minute + offset;
            if (minutes < 0 || minutes > 1439) return -1; // crossed midnight — day semantics get ambiguous, skip
            return minutes;
        }

        /// <summary>Recall without a requesting panel (scheduler path) — no result join.
        /// Lights/shades commands fall back to the first assigned panel slot.</summary>
        private void RecallInternal(QuickAction action)
        {
            switch (action.Subsystem)
            {
                case "music":
                    RecallMusic(action.Music);
                    break;
                case "climate":
                    RecallClimate(action.Climate);
                    break;
                case "lights":
                case "shades":
                    var ctrl = SceneControl(action.Subsystem);
                    if (ctrl != null && ctrl.IsConfigured && action.SceneIndex.HasValue)
                        ctrl.SendHouseSceneCommand(0, (ushort)(201 + action.SceneIndex.Value));
                    else
                        CrestronConsole.PrintLine("QuickActions: scheduled \"{0}\" skipped — lighting bridge unavailable", action.Name);
                    break;
                default:
                    break;
            }
        }

        // ─── App03 confirmation / drift handling ───────────────────────────

        private void StartPendingOpTimeout()
        {
            if (pendingOpTimer != null)
            {
                pendingOpTimer.Stop();
                pendingOpTimer.Dispose();
            }
            pendingOpTimer = new CTimer(o =>
            {
                var op = pendingOp;
                pendingOp = null;
                if (op != null)
                {
                    CrestronConsole.PrintLine("QuickActions: {0} {1} timed out waiting for App03", op.Kind, op.Subsystem);
                    SendResult(op.TpNumber, op.Kind, false, "timeout", "Lighting system did not respond");
                }
            }, SceneOpTimeoutMs);
        }

        /// <summary>
        /// Called (via ControlSystem wiring) whenever LightingScenario2Control /
        /// ShadesScenario2Control see the house-scene count or names change on the
        /// EISC. Completes any pending create/delete, then re-validates stored
        /// sceneIndex↔sceneName bindings (scenes can be edited from native lighting
        /// panels — positional indexes shift on delete).
        /// </summary>
        public void OnHouseSceneMetadataChanged(string subsystem)
        {
            try
            {
                var op = pendingOp;
                if (op != null && op.Subsystem == subsystem)
                {
                    var ctrl = SceneControl(subsystem);
                    if (ctrl != null && ctrl.GetHouseSceneCount() == op.ExpectedCount)
                    {
                        pendingOp = null;
                        if (pendingOpTimer != null) pendingOpTimer.Stop();
                        FinalizePendingOp(op);
                    }
                }
                ScheduleRevalidate();
            }
            catch (Exception ex)
            {
                ErrorLog.Error("QuickActions OnHouseSceneMetadataChanged error: {0}", ex.Message);
            }
        }

        private void FinalizePendingOp(PendingSceneOp op)
        {
            if (op.Kind == "create")
            {
                AddAction(new QuickAction
                {
                    Name = op.Name,
                    Subsystem = op.Subsystem,
                    SceneIndex = op.SceneIndex,
                    SceneName = op.Name,
                    IncludedRooms = op.IncludedRooms
                });
                SendResult(op.TpNumber, "create", true, "", op.Name);
            }
            else // delete
            {
                lock (storeLock)
                {
                    var action = store.Actions.FirstOrDefault(a => a.Id == op.ActionId);
                    if (action != null) store.Actions.Remove(action);
                    // App03 shifts higher scenes down one slot — compensate our references
                    foreach (var a in store.Actions)
                    {
                        if (a.Subsystem == op.Subsystem && a.SceneIndex.HasValue && a.SceneIndex.Value > op.SceneIndex)
                        {
                            a.SceneIndex = a.SceneIndex.Value - 1;
                        }
                    }
                }
                ScheduleSave();
                SendDescriptorToAll();
                SendResult(op.TpNumber, "delete", true, "", "");
            }
        }

        /// <summary>Debounced — App03 pushes names one serial at a time.</summary>
        private void ScheduleRevalidate()
        {
            if (revalidateTimer != null)
                revalidateTimer.Stop();
            revalidateTimer = new CTimer(o => RevalidateSceneBindings(), 1000);
        }

        private void RevalidateSceneBindings()
        {
            try
            {
                if (pendingOp != null) return; // don't fight an in-flight operation
                bool changed = false;
                lock (storeLock)
                {
                    var toRemove = new List<QuickAction>();
                    foreach (var a in store.Actions)
                    {
                        if (a.Subsystem != "lights" && a.Subsystem != "shades") continue;
                        var ctrl = SceneControl(a.Subsystem);
                        if (ctrl == null || !ctrl.IsConfigured || !ctrl.EiscOnline) continue; // can't verify — leave alone
                        ushort count = ctrl.GetHouseSceneCount();
                        if (count == 0) continue; // App03 may still be booting — don't drop on empty data

                        int idx = a.SceneIndex.HasValue ? a.SceneIndex.Value : -1;
                        if (idx >= 0 && idx < count && ctrl.GetHouseSceneName(idx) == a.SceneName)
                            continue; // binding intact

                        // scene moved (delete shifted indexes) — re-find by name
                        int found = -1;
                        for (int i = 0; i < count; i++)
                        {
                            if (ctrl.GetHouseSceneName(i) == a.SceneName) { found = i; break; }
                        }
                        if (found >= 0)
                        {
                            if (a.SceneIndex != found)
                            {
                                a.SceneIndex = found;
                                changed = true;
                                CrestronConsole.PrintLine("QuickActions: rebound \"{0}\" to {1} scene {2}", a.Name, a.Subsystem, found);
                            }
                        }
                        else
                        {
                            toRemove.Add(a);
                            CrestronConsole.PrintLine("QuickActions: dropping \"{0}\" — {1} scene \"{2}\" no longer exists", a.Name, a.Subsystem, a.SceneName);
                        }
                    }
                    foreach (var a in toRemove)
                    {
                        store.Actions.Remove(a);
                        changed = true;
                    }
                }
                if (changed)
                {
                    ScheduleSave();
                    SendDescriptorToAll();
                }
            }
            catch (Exception ex)
            {
                ErrorLog.Error("QuickActions Revalidate error: {0}", ex.Message);
            }
        }
    }

    /// <summary>
    /// The slice of LightingScenario2Control / ShadesScenario2Control that
    /// QuickActionManager needs — both bridges implement it identically apart
    /// from their join layouts.
    /// </summary>
    public interface IHouseSceneBridge
    {
        bool IsConfigured { get; }
        bool EiscOnline { get; }
        ushort GetHouseSceneCount();
        string GetHouseSceneName(int index);
        /// <summary>Write a save-command value on the requesting panel's slot
        /// (falls back to the first assigned slot). Returns false if impossible.</summary>
        bool SendHouseSceneCommand(ushort tpNumber, ushort commandValue);
        /// <summary>Send the pending scene name (serial 620) ahead of a 301+idx create.</summary>
        bool SendPendingSceneName(string name);
        /// <summary>Send the pending include-list (serial 621) ahead of a 301+idx create:
        /// CSV of App03 room IDs (LightsID/ShadesID) to capture. Empty = all rooms.</summary>
        bool SendPendingIncludeRooms(string csv);
    }
}
