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

        private const int MaxHouseScenes = 10;      // App03 hard ceiling per subsystem
        private const int MaxNameLength = 40;
        private const int SceneOpTimeoutMs = 4000;  // wait for App03 metadata re-push

        private QuickActionStore store = new QuickActionStore();
        private readonly object storeLock = new object();

        private CTimer saveTimer;
        private CTimer volumeTimer;
        private CTimer revalidateTimer;
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

        private string BuildDescriptorJson()
        {
            object payload;
            lock (storeLock)
            {
                payload = new
                {
                    seq = ++descriptorSeq,
                    actions = store.Actions.Select(a => new
                    {
                        id = a.Id,
                        name = a.Name,
                        subsystem = a.Subsystem,
                        favorite = a.Favorite
                    }).ToArray(),
                    canCreate = new
                    {
                        music = SystemHasMusic(),
                        climate = SystemHasClimate(),
                        lights = _parent.lightingScenario2Control != null && _parent.lightingScenario2Control.IsConfigured,
                        shades = _parent.shadesScenario2Control != null && _parent.shadesScenario2Control.IsConfigured
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

        /// <summary>Push the descriptor to one HTML panel (boot / panel-online re-send).</summary>
        public void SendDescriptorTo(UI.TouchpanelUI tp)
        {
            try
            {
                if (tp == null || !tp.HTML_UI || tp.UserInterface == null) return;
                tp.UserInterface.StringInput[DescriptorJoin].StringValue = BuildDescriptorJson();
            }
            catch (Exception ex)
            {
                ErrorLog.Error("QuickActions SendDescriptorTo error: {0}", ex.Message);
            }
        }

        public void SendDescriptorToAll()
        {
            string json = BuildDescriptorJson();
            foreach (var tp in _parent.manager.touchpanelZ)
            {
                try
                {
                    if (tp.Value.HTML_UI && tp.Value.UserInterface != null)
                        tp.Value.UserInterface.StringInput[DescriptorJoin].StringValue = json;
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
                        Create(tpNumber, ((string)obj["subsystem"] ?? string.Empty).ToLower(), (string)obj["name"]);
                        break;
                    case "delete":
                        Delete(tpNumber, (int?)obj["id"] ?? 0);
                        break;
                    case "favorite":
                        SetFavorite(tpNumber, (int?)obj["id"] ?? 0, (bool?)obj["value"] ?? false);
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
                    music.UpdateAllPanelsTextWhenAudioChanges(); // called after quick action recall settles
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

        private void Create(ushort tpNumber, string subsystem, string name)
        {
            name = (name ?? string.Empty).Trim();
            if (name.Length == 0)
            {
                SendResult(tpNumber, "create", false, "badName", "Name cannot be empty");
                return;
            }
            if (name.Length > MaxNameLength) name = name.Substring(0, MaxNameLength);

            switch (subsystem)
            {
                case "music":
                    if (!SystemHasMusic())
                    {
                        SendResult(tpNumber, "create", false, "noSubsystem", "No music zones in this system");
                        return;
                    }
                    AddAction(SnapshotMusic(name));
                    SendResult(tpNumber, "create", true, "", name);
                    break;
                case "climate":
                    if (!SystemHasClimate())
                    {
                        SendResult(tpNumber, "create", false, "noSubsystem", "No climate zones in this system");
                        return;
                    }
                    AddAction(SnapshotClimate(name));
                    SendResult(tpNumber, "create", true, "", name);
                    break;
                case "lights":
                case "shades":
                    CreateHouseSceneAction(tpNumber, subsystem, name);
                    break;
                default:
                    SendResult(tpNumber, "create", false, "badSubsystem", "Unknown subsystem");
                    break;
            }
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

        /// <summary>Snapshot every audio zone's cached state — same reads the legacy
        /// XML writeSubsystems() does (CurrentMusicSrc + VOLUMEEISC output).</summary>
        private QuickAction SnapshotMusic(string name)
        {
            var payload = new MusicPayload();
            foreach (var rm in _parent.manager.RoomZ)
            {
                if (rm.Value.AudioID > 0)
                {
                    payload.Zones.Add(new MusicZoneSetting
                    {
                        AudioId = rm.Value.AudioID,
                        Source = rm.Value.CurrentMusicSrc,
                        Volume = _parent.VOLUMEEISC.UShortOutput[rm.Value.AudioID].UShortValue
                    });
                }
            }
            return new QuickAction { Name = name, Subsystem = "music", Music = payload };
        }

        private QuickAction SnapshotClimate(string name)
        {
            var payload = new ClimatePayload();
            foreach (var rm in _parent.manager.RoomZ)
            {
                if (rm.Value.ClimateID > 0)
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
            return new QuickAction { Name = name, Subsystem = "climate", Climate = payload };
        }

        private IHouseSceneBridge SceneControl(string subsystem)
        {
            if (subsystem == "lights") return _parent.lightingScenario2Control;
            if (subsystem == "shades") return _parent.shadesScenario2Control;
            return null;
        }

        /// <summary>
        /// Lights/shades create = tell App03 to snapshot the whole house into a new
        /// scene slot: send the name on serial 620, then command 301+idx. App03
        /// persists and re-pushes count+names; that re-push is our confirmation.
        /// </summary>
        private void CreateHouseSceneAction(ushort tpNumber, string subsystem, string name)
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
                TpNumber = tpNumber
            };
            StartPendingOpTimeout();

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
                    SceneName = op.Name
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
    }
}
