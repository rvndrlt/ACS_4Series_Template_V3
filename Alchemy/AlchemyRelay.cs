using System;
using System.Collections.Generic;
using System.Text;
using Crestron.SimplSharp;
using Crestron.SimplSharpPro;
using Crestron.SimplSharpPro.DeviceSupport;
using Crestron.SimplSharpPro.EthernetCommunication;

namespace ACS_4Series_Template_V3.Alchemy
{
    /// <summary>
    /// Barco Alchemy bridge: the barcoAlchemy program's media EISC (IPID 0xA1) on one side,
    /// panel direct joins 1620-1630 on the other.
    ///
    /// Same shape as the fireplace bridge, and for the same reasons. The EISC side is discrete
    /// joins because the EISC is local and cheap -- 50 content titles are 50 serials. The panel
    /// side is JSON, because 50 titles on per-item panel joins would be 50 direct joins plus a
    /// re-allocation every time the server's content changes.
    ///
    /// Join numbers on the EISC side must match rvndrlt/barcoAlchemy/JOINMAP.md, and on the
    /// panel side html/DIRECT-JOINS.md block 1620-1639. Neither is free to drift: this program
    /// is the only thing that reconciles them.
    ///
    /// Two behaviours are deliberate:
    ///
    ///  - **The DCI state (1620-1626) goes to every HTML panel, but only while a theater display
    ///    is actually on the DCI server.** Any panel in the house can select the theater room and
    ///    open this page, so restricting the audience by room would be wrong. Gating on the
    ///    source instead is both cheaper and more accurate: the page is only reachable by
    ///    selecting the Alchemy source, so when nothing is on it nobody can be looking, and the
    ///    player position stops ticking JSON at the whole house for no reason.
    ///
    ///  - **The warming and cooling overlays (1627-1630) are room-gated, and stay that way.**
    ///    They are full-screen and they are not about the DCI server at all -- the projector
    ///    warms for a Blu-ray just the same. A panel showing the kitchen must not be covered by
    ///    the theater's warming page.
    ///
    ///  - **Publishes are coalesced.** A single poll on the barcoAlchemy side moves a dozen
    ///    joins, each arriving as its own SigChange. Rebuilding and publishing the JSON on every
    ///    one would do that work a dozen times for one logical update.
    /// </summary>
    public class AlchemyRelay
    {
        /// <summary>
        /// Defaults for everything alchemyConfig.json can override. Also the answer when the
        /// relay has not been constructed, which is why the name test has a static form.
        /// </summary>
        private static readonly AlchemyConfig DefaultConfig = new AlchemyConfig();

        public const int MAX_CONTENT = 50;
        public const int MAX_PRESETS = 50;

        // ── EISC: commands we send (BooleanInput = towards barcoAlchemy) ───────────────────

        private const uint D_PLAY = 1;
        private const uint D_PAUSE = 2;
        private const uint D_STOP = 3;
        private const uint D_CLEAR = 4;
        private const uint D_NEXT = 5;
        private const uint D_PREV = 6;
        private const uint D_REFRESH = 7;
        private const uint D_PREPARE = 8;

        // 11-14 skip back 15s/30s/1min/5min, 15-18 the same forwards. There is no shuttle in
        // the Barco API -- only absolute seek -- so these are the only skip sizes that exist.
        private const uint D_SKIP_BACK_BASE = 11;
        private const uint D_SKIP_FWD_BASE = 15;
        private static readonly int[] SKIP_SECONDS = { 15, 30, 60, 300 };

        private const uint D_POWER_ON = 21;
        private const uint D_POWER_OFF = 22;
        private const uint D_DOWSER_OPEN = 23;
        private const uint D_DOWSER_CLOSE = 24;
        private const uint D_RECONNECT = 25;
        private const uint D_TEST_MODE_ENTER = 28;     // barcoAlchemy test mode: refuse dowser opens for an hour
        private const uint D_TEST_MODE_EXIT = 29;

        private const uint D_CONTENT_SELECT_BASE = 101;   // 101-150
        private const uint D_PRESET_SELECT_BASE = 201;    // 201-250

        // ── EISC: feedback we receive (arrives as SigChange on the Output side) ────────────

        private const uint DO_ONLINE = 1;
        private const uint DO_LOGGED_IN = 2;
        private const uint DO_PLAYING = 3;
        private const uint DO_PAUSED = 4;
        private const uint DO_STOPPED = 5;
        private const uint DO_CLEARED = 6;
        private const uint DO_BUSY = 7;
        private const uint DO_ERROR = 8;
        private const uint DO_MEDIA_INPUT_READY = 9;
        private const uint DO_LIST_VALID = 10;
        private const uint DO_PLAY_AVAILABLE = 11;
        private const uint DO_TRANSPORT_AVAILABLE = 12;

        private const uint DO_PROJ_ONLINE = 20;
        private const uint DO_POWER_ON = 21;
        private const uint DO_POWER_OFF = 22;
        private const uint DO_DOWSER_OPEN = 23;
        private const uint DO_DOWSER_CLOSED = 24;
        private const uint DO_PROJ_HEALTHY = 26;
        private const uint DO_PROJ_BUSY = 27;
        private const uint DO_TEST_MODE = 28;

        private const uint DO_WARMING = 30;
        private const uint DO_COOLING = 31;
        private const uint DO_STILL_WARM = 32;

        private const uint DO_CONTENT_SELECTED_BASE = 101;
        private const uint DO_PRESET_SELECTED_BASE = 201;

        private const uint AO_CONTENT_COUNT = 1;
        private const uint AO_PLAYER_STATE = 2;
        private const uint AO_POSITION = 3;
        private const uint AO_DURATION = 4;
        private const uint AO_REMAINING = 5;
        private const uint AO_PROGRESS = 6;
        private const uint AO_PLAYER_ERROR = 7;
        private const uint AO_PRESET_COUNT = 11;
        private const uint AO_LAMP_HOURS = 12;
        private const uint AO_PROJECTOR_STATE = 13;
        private const uint AO_DISCARDED_FRAMES = 14;
        private const uint AO_WARM_PROGRESS = 15;
        private const uint AO_WARM_SECONDS = 16;
        private const uint AO_COOL_PROGRESS = 17;
        private const uint AO_COOL_SECONDS = 18;
        private const uint AO_STILL_WARM_SECONDS = 19;
        private const uint AO_TEST_MODE_SECONDS = 20;

        private const uint SO_TITLE = 1;
        private const uint SO_CLIP = 2;
        private const uint SO_STATUS = 3;
        private const uint SO_POS_TEXT = 4;
        private const uint SO_DUR_TEXT = 5;
        private const uint SO_REM_TEXT = 6;
        private const uint SO_ACTIVE_PRESET = 11;
        private const uint SO_PROJECTOR_STATUS = 12;
        private const uint SO_CONTENT_NAME_BASE = 101;    // 101-150
        private const uint SO_PRESET_NAME_BASE = 201;     // 201-250

        // ── Panel side — html/DIRECT-JOINS.md, 1620-1639 ──────────────────────────────────

        public const ushort CatalogJoin = 1620;            // s  C#→HTML
        public const ushort CatalogRevJoin = 1621;         // n  C#→HTML
        public const ushort RepublishJoin = 1622;          // n  HTML→C#
        public const ushort PlayerJoin = 1623;             // s  C#→HTML
        public const ushort MediaCommandJoin = 1624;       // s  HTML→C#
        public const ushort ProjectorJoin = 1625;          // s  C#→HTML
        public const ushort ProjectorCommandJoin = 1626;   // s  HTML→C#
        public const ushort WarmingJoin = 1627;            // b  C#→panel
        public const ushort WarmingProgressJoin = 1628;    // n  C#→panel
        public const ushort CoolingJoin = 1629;            // b  C#→panel
        public const ushort CoolingProgressJoin = 1630;    // n  C#→panel

        // Exit buttons, panel→C#. Dumb panels (TSR-310) only in practice -- the HTML overlays
        // have no exit by design, since there is nothing useful behind them to get back to.
        //
        // These are NOT the joins originally proposed for the TSR project (144/145/172/173 and
        // analogs 12/13). 172 and 173 are already the sleep-scenario page flips (`170 + scenario`,
        // cleared by `171 + i`), and 144/145 sit inside the DVR sub-page block `140 + n`. The
        // 16xx range matches the TSR's existing lighting joins at 1101-1120 and leaves the legacy
        // page-flip arithmetic untouched.
        public const ushort WarmingExitJoin = 1631;        // b  panel→C#
        public const ushort CoolingExitJoin = 1632;        // b  panel→C#

        // ── TSR-310 movie list ────────────────────────────────────────────────────────────────
        // Smart object 37 in TSR-310.sgd is MOVIE_LIST, a 30-item Dynamic Button List Vertical.
        // Presses arrive through the normal SmartObjectIDs.tsrMovieList case.
        public const uint TsrMovieListId = (uint)UI.TouchpanelUI.SmartObjectIDs.tsrMovieList;
        public const int TsrListSize = 30;                  // items the .sgd defines
        // Sig numbers are the .sgd cue numbers minus the [~BeginGroup~]/[~EndGroup~] markers ahead
        // of them -- see ShadesScenario2Control and ControlSystem.Subsystems. "Item 1 Text" is
        // cue 12 with one marker ahead, so sig 11. Using the raw cues put every title one slot
        // down (item 1 blank) and made a press select the item above the one touched.
        private const uint TSR_LIST_COUNT = 4;              // analog in: Set Number of Items (cue 4, no markers)
        private const uint TSR_LIST_ITEM_BASE = 10;         // item N text / selected / pressed = 10 + N (cue 11+N, 1 marker)
        private const uint TSR_LIST_ENABLE_BASE = 2010;     // item N enabled = 2010 + N (cue 2013+N, 3 markers)
        private const uint TSR_LIST_VISIBLE_BASE = 4010;    // item N visible = 4010 + N (cue 4015+N, 5 markers)

        // Status for the movie list page. Plain panel joins on the TSR, outside the smart object.
        // Taken from the DCI reserve because they are DCI state. HTML panels get the same facts
        // inside the player JSON on 1623 and do not use these.
        public const ushort TsrStatusTextJoin = 1633;      // s  "Loading, please wait", "Ready to play", errors
        public const ushort TsrLoadedTitleJoin = 1634;     // s  what is loaded in the player now
        public const ushort TsrBusyJoin = 1635;            // b  high during any transient (selecting, starting...)

        private readonly ControlSystem cs;
        private AlchemyConfig cfg;
        private ThreeSeriesTcpIpEthernetIntersystemCommunications eisc;
        private List<ushort> projectorDisplays;

        // Cached feedback. Everything published is built from these, never read back off the
        // EISC: an Output sig read before the far end has sent anything returns a default that
        // is indistinguishable from a real zero.
        private readonly bool[] mediaFb = new bool[13];
        private readonly bool[] projFb = new bool[33];
        private ushort contentCount, playerState, position, duration, progress, playerError;
        private ushort presetCount, projectorState;
        private ushort warmProgress, coolProgress;
        private bool testMode;
        private ushort testModeSeconds;

        // Panels whose Exit button hid the current transition. Cleared when that transition ends,
        // so a dismissal never carries into the next one. Guarded by `gate`.
        private readonly HashSet<ushort> warmDismissed = new HashSet<ushort>();
        private readonly HashSet<ushort> coolDismissed = new HashSet<ushort>();

        // What the TSR movie lists were last given. Every TSR gets identical data, so one cache
        // serves them all, and the player poll -- which marks the player state dirty every
        // second -- writes nothing unless something actually moved.
        private string lastTsrCatalog;
        private int lastTsrSelected = -1;
        private string lastTsrStatus, lastTsrTitle;
        private bool? lastTsrBusy;
        private string sTitle = "", sClip = "", sStatus = "", sPosText = "", sDurText = "", sRemText = "";
        private string sActivePreset = "", sProjectorStatus = "";
        private readonly string[] contentName = new string[MAX_CONTENT];
        private readonly string[] presetName = new string[MAX_PRESETS];
        private int selectedContent, selectedPreset;

        private ushort catalogRev;
        private string lastCatalog = "";

        // Coalescing. One barcoAlchemy poll moves a dozen joins; without this we would rebuild
        // and republish once per join for a single logical update.
        private const long COALESCE_MS = 150;
        private CTimer publishTimer;
        private bool catalogDirty, playerDirty, projectorDirty, transitionDirty;

        private readonly object gate = new object();
        private readonly object pulseGate = new object();
        private readonly Dictionary<uint, CTimer> pulseTimers = new Dictionary<uint, CTimer>();
        private List<ushort> theaterRooms;
        private bool lastDciActive;

        public AlchemyRelay(ControlSystem parent)
        {
            cs = parent;
            cfg = AlchemyConfig.Load();

            for (int i = 0; i < MAX_CONTENT; i++) contentName[i] = string.Empty;
            for (int i = 0; i < MAX_PRESETS; i++) presetName[i] = string.Empty;

            eisc = new ThreeSeriesTcpIpEthernetIntersystemCommunications(cfg.EiscIpid, cfg.EiscAddress, cs);
            eisc.SigChange += EISC_SigChange;
            eisc.OnlineStatusChange += EISC_OnlineStatusChange;

            var resp = eisc.Register();
            if (resp != eDeviceRegistrationUnRegistrationResponse.Success)
            {
                ErrorLog.Error("AlchemyRelay: EISC 0x{0:X2} failed to register: {1}",
                    cfg.EiscIpid, eisc.RegistrationFailureReason);
                eisc = null;
                return;
            }

            CrestronConsole.PrintLine("AlchemyRelay: EISC 0x{0:X2} registered for barcoAlchemy", cfg.EiscIpid);
        }

        // ── which rooms are "the theater" ─────────────────────────────────────────────────

        /// <summary>
        /// True when this video source's configured Name is the Barco Alchemy / DCI server.
        ///
        /// The single source of truth for that question: TouchpanelUI.VideoSourcePageKey calls
        /// it too, so the page key and the theater-room derivation below can never disagree
        /// about which source is the DCI server.
        ///
        /// Matches Name, not DisplayName, for the same reason as every other rule there --
        /// DisplayName is whatever reads well on screen ("Movies") and carries no device type.
        /// </summary>
        public static bool IsDciServerSource(string sourceName)
        {
            return DefaultConfig.IsDciSource(sourceName);
        }

        /// <summary>
        /// The same question against this site's configured name list. Callers that have the
        /// relay should use this; the static above is the answer before it is constructed.
        /// </summary>
        public bool IsDciSource(string sourceName)
        {
            return (cfg ?? DefaultConfig).IsDciSource(sourceName);
        }

        /// <summary>
        /// The rooms that can reach the DCI server, derived rather than configured: any room
        /// whose display runs a source scenario that includes an Alchemy source.
        ///
        /// Derived because every hardcoding of "the theater is room N" in this codebase has
        /// eventually been wrong -- on 30 BPT the theater is room 1 on display 17, and the
        /// repo's other config has it as room 69 on display 20. Walking the config costs one
        /// pass at startup and cannot go stale.
        /// </summary>
        private List<ushort> TheaterRooms()
        {
            if (theaterRooms != null) return theaterRooms;

            var rooms = new List<ushort>();
            if (cs == null || cs.manager == null
                || cs.manager.VideoDisplayZ == null
                || cs.manager.VideoSrcScenarioZ == null
                || cs.manager.VideoSourceZ == null)
            {
                return rooms;   // not cached: the config may not be loaded yet
            }

            foreach (var dkv in cs.manager.VideoDisplayZ)
            {
                var disp = dkv.Value;
                if (disp == null || disp.AssignedToRoomNum == 0) continue;
                if (!cs.manager.VideoSrcScenarioZ.ContainsKey(disp.VideoSourceScenario)) continue;

                var scen = cs.manager.VideoSrcScenarioZ[disp.VideoSourceScenario];
                if (scen == null || scen.IncludedSources == null) continue;

                foreach (ushort srcNum in scen.IncludedSources)
                {
                    if (srcNum == 0 || !cs.manager.VideoSourceZ.ContainsKey(srcNum)) continue;
                    if (!IsDciSource(cs.manager.VideoSourceZ[srcNum].Name)) continue;

                    if (!rooms.Contains(disp.AssignedToRoomNum)) rooms.Add(disp.AssignedToRoomNum);
                    break;
                }
            }

            // Only cache once the config is actually loaded. Caching an empty answer taken
            // before VideoDisplayZ is filled would make the page dark for the life of the
            // program, with nothing to show for it but one startup warning.
            if (cs.manager.VideoDisplayZ.Count == 0) return rooms;
            theaterRooms = rooms;

            if (rooms.Count == 0)
            {
                // Worth a loud line: the page exists, the EISC is up, and nothing will ever
                // appear. Almost always a source Name that IsDciServerSource does not match.
                ErrorLog.Warn("AlchemyRelay: no video source matches the DCI server - "
                    + "the Alchemy page will never publish. Check the source Name in the config.");
            }
            else
            {
                var sb = new StringBuilder();
                foreach (ushort r in rooms) sb.Append(sb.Length > 0 ? ", " : "").Append(r);
                CrestronConsole.PrintLine("AlchemyRelay: theater room(s) {0}", sb);
            }
            return rooms;
        }

        private bool IsTheaterRoom(ushort roomNum)
        {
            return roomNum != 0 && TheaterRooms().Contains(roomNum);
        }

        /// <summary>
        /// True while some display that can reach the DCI server is currently showing it.
        ///
        /// This is the gate on publishing the DCI state. The page cannot be open unless this is
        /// true -- it is reached by selecting the Alchemy source and nothing else -- so when it
        /// is false there is nobody to publish to, however many panels are awake.
        /// </summary>
        private bool AnyDisplayOnDciServer()
        {
            if (cs == null || cs.manager == null
                || cs.manager.VideoDisplayZ == null || cs.manager.VideoSourceZ == null)
            {
                return false;
            }

            foreach (var kv in cs.manager.VideoDisplayZ)
            {
                var disp = kv.Value;
                if (disp == null || disp.CurrentVideoSrc == 0) continue;
                if (!cs.manager.VideoSourceZ.ContainsKey(disp.CurrentVideoSrc)) continue;
                if (IsDciSource(cs.manager.VideoSourceZ[disp.CurrentVideoSrc].Name)) return true;
            }
            return false;
        }

        /// <summary>
        /// Displays whose power commands the projector, from alchemyConfig.json if it names any,
        /// otherwise derived the same way the theater rooms are: every display whose source
        /// scenario includes a DCI source.
        ///
        /// The derivation is right whenever the DCI server feeds the projector, which is the
        /// normal case and needs no file. It is wrong for a DCI server feeding a flat panel, and
        /// that is what ProjectorDisplays in the config is for.
        /// </summary>
        private List<ushort> ProjectorDisplays()
        {
            if (cfg != null && cfg.ProjectorDisplays != null && cfg.ProjectorDisplays.Count > 0)
            {
                return cfg.ProjectorDisplays;
            }
            if (projectorDisplays != null) return projectorDisplays;

            var found = new List<ushort>();
            if (cs == null || cs.manager == null
                || cs.manager.VideoDisplayZ == null
                || cs.manager.VideoSrcScenarioZ == null
                || cs.manager.VideoSourceZ == null)
            {
                return found;
            }

            foreach (var dkv in cs.manager.VideoDisplayZ)
            {
                var disp = dkv.Value;
                if (disp == null) continue;
                if (!cs.manager.VideoSrcScenarioZ.ContainsKey(disp.VideoSourceScenario)) continue;

                var scen = cs.manager.VideoSrcScenarioZ[disp.VideoSourceScenario];
                if (scen == null || scen.IncludedSources == null) continue;

                foreach (ushort srcNum in scen.IncludedSources)
                {
                    if (srcNum == 0 || !cs.manager.VideoSourceZ.ContainsKey(srcNum)) continue;
                    if (!IsDciSource(cs.manager.VideoSourceZ[srcNum].Name)) continue;
                    if (!found.Contains(disp.Number)) found.Add(disp.Number);
                    break;
                }
            }

            if (cs.manager.VideoDisplayZ.Count == 0) return found;   // config not loaded yet
            projectorDisplays = found;
            return found;
        }

        /// <summary>
        /// True when this display's power should be sent to the projector instead of to an NVX
        /// receiver's IR port. VideoSystemControl asks before it fires a display command.
        /// </summary>
        public bool IsProjectorDisplay(ushort displayNumber)
        {
            if (cfg != null && !cfg.DrivePower) return false;
            return ProjectorDisplays().Contains(displayNumber);
        }

        /// <summary>
        /// Power the projector from a display power event.
        ///
        /// Sent straight to barcoAlchemy over the EISC, which is the program that can actually
        /// verify it: it wakes the projector, closes the dowser, lights the lamp, watches for the
        /// lamp to come on and re-sends if it does not. Nothing in this template could do that,
        /// which is the whole argument for sending the intent rather than a command.
        ///
        /// Repeats are passed through, not filtered. barcoAlchemy skips the dowser close when the
        /// lamp is already lit, so a second Power On costs nothing -- and filtering here would
        /// also swallow the case that matters, an operator re-sending a Power On that did not
        /// take. Deciding "already on" is the projector's job, not ours.
        /// </summary>
        public void OnDisplayPower(ushort displayNumber, bool on)
        {
            if (!IsProjectorDisplay(displayNumber)) return;

            if (eisc == null)
            {
                ErrorLog.Warn("AlchemyRelay: no EISC; dropped projector power {0} for display {1}",
                    on ? "ON" : "OFF", displayNumber);
                return;
            }

            // Always logged. Powering a cinema projector is worth a line.
            CrestronConsole.PrintLine("AlchemyRelay: display {0} -> projector POWER {1}",
                displayNumber, on ? "ON" : "OFF");
            Pulse(on ? D_POWER_ON : D_POWER_OFF);
        }

        /// <summary>Re-reads alchemyConfig.json and drops the derived caches.</summary>
        public string ReloadConfig()
        {
            cfg = AlchemyConfig.Load();
            theaterRooms = null;
            projectorDisplays = null;
            return "AlchemyRelay: " + cfg.Describe();
        }

        /// <summary>
        /// A display changed source. If that turned the DCI server on or off, the whole state
        /// has to move: on, because every panel has been receiving nothing and the page is about
        /// to open with an empty catalog; off, so nothing is left mid-publish.
        /// </summary>
        public void OnDisplaySourceChanged(ushort displayNumber)
        {
            bool nowOn = AnyDisplayOnDciServer();
            if (nowOn == lastDciActive) return;
            lastDciActive = nowOn;

            if (cs != null && cs.logging)
            {
                CrestronConsole.PrintLine("AlchemyRelay: DCI server {0} (display {1} changed source)",
                    nowOn ? "selected - publishing" : "deselected - publishing stops", displayNumber);
            }

            if (nowOn) MarkDirty(true, true, true, true);
        }

        // ── EISC → here ───────────────────────────────────────────────────────────────────

        private void EISC_OnlineStatusChange(GenericBase device, OnlineOfflineEventArgs args)
        {
            CrestronConsole.PrintLine("AlchemyRelay: barcoAlchemy EISC {0}",
                args.DeviceOnLine ? "online" : "OFFLINE");

            if (!args.DeviceOnLine)
            {
                // Say offline rather than freeze on the last good values. A stale "Playing" with
                // a frozen position is worse than an honest "Media server offline" -- the page
                // has a message for exactly this and it cannot show it if we keep lying.
                lock (gate)
                {
                    for (int i = 0; i < mediaFb.Length; i++) mediaFb[i] = false;
                    for (int i = 0; i < projFb.Length; i++) projFb[i] = false;
                    playerState = 0; position = 0; duration = 0; progress = 0; playerError = 0;
                    projectorState = 0; warmProgress = 0; coolProgress = 0;
                    sTitle = ""; sClip = ""; sStatus = ""; sPosText = ""; sDurText = ""; sRemText = "";
                    sActivePreset = ""; sProjectorStatus = "";
                    selectedContent = 0; selectedPreset = 0;
                }
            }

            MarkDirty(true, true, true, true);
        }

        private void EISC_SigChange(BasicTriList device, SigEventArgs args)
        {
            uint n = args.Sig.Number;

            switch (args.Sig.Type)
            {
                case eSigType.Bool:
                    HandleBool(n, args.Sig.BoolValue);
                    break;
                case eSigType.UShort:
                    HandleAnalog(n, args.Sig.UShortValue);
                    break;
                case eSigType.String:
                    HandleString(n, args.Sig.StringValue ?? string.Empty);
                    break;
            }
        }

        private void HandleBool(uint n, bool v)
        {
            lock (gate)
            {
                if (n >= DO_ONLINE && n <= DO_TRANSPORT_AVAILABLE)
                {
                    mediaFb[n] = v;
                    MarkDirty(false, true, false, false);
                    return;
                }
                if (n >= DO_PROJ_ONLINE && n <= DO_PROJ_BUSY)
                {
                    projFb[n] = v;
                    MarkDirty(false, false, true, false);
                    return;
                }
                if (n == DO_TEST_MODE)
                {
                    if (v != testMode)
                    {
                        CrestronConsole.PrintLine("AlchemyRelay: barcoAlchemy test mode {0}",
                            v ? "ON - dowser opens refused" : "OFF - dowser back to normal");
                    }
                    testMode = v;
                    return;
                }
                if (n >= DO_WARMING && n <= DO_STILL_WARM)
                {
                    bool was = projFb[n];
                    projFb[n] = v;
                    // A transition ending drops the Exit dismissals, so the next one is shown to
                    // everybody. Done on the falling edge, not when the next one starts, so the
                    // state is already clean before any progress tick arrives.
                    if (was && !v && (n == DO_WARMING || n == DO_COOLING)) OnTransitionEnded(n == DO_WARMING);
                    MarkDirty(false, false, false, true);
                    return;
                }
                if (n >= DO_CONTENT_SELECTED_BASE && n < DO_CONTENT_SELECTED_BASE + MAX_CONTENT)
                {
                    // Selection feedback is one join per slot; the page wants one index. Only a
                    // rising edge sets it -- a falling edge is the *previous* slot releasing and
                    // would otherwise clear the selection that just arrived.
                    if (v) selectedContent = (int)(n - DO_CONTENT_SELECTED_BASE) + 1;
                    else if (selectedContent == (int)(n - DO_CONTENT_SELECTED_BASE) + 1) selectedContent = 0;
                    MarkDirty(false, true, false, false);
                    return;
                }
                if (n >= DO_PRESET_SELECTED_BASE && n < DO_PRESET_SELECTED_BASE + MAX_PRESETS)
                {
                    if (v) selectedPreset = (int)(n - DO_PRESET_SELECTED_BASE) + 1;
                    else if (selectedPreset == (int)(n - DO_PRESET_SELECTED_BASE) + 1) selectedPreset = 0;
                    MarkDirty(false, false, true, false);
                    return;
                }
            }
        }

        private void HandleAnalog(uint n, ushort v)
        {
            lock (gate)
            {
                switch (n)
                {
                    case AO_CONTENT_COUNT:
                        contentCount = v > MAX_CONTENT ? (ushort)MAX_CONTENT : v;
                        MarkDirty(true, false, false, false);
                        return;
                    case AO_PLAYER_STATE: playerState = v; MarkDirty(false, true, false, false); return;
                    case AO_POSITION: position = v; MarkDirty(false, true, false, false); return;
                    case AO_DURATION: duration = v; MarkDirty(false, true, false, false); return;
                    case AO_PROGRESS: progress = v; MarkDirty(false, true, false, false); return;
                    case AO_PLAYER_ERROR: playerError = v; MarkDirty(false, true, false, false); return;
                    case AO_PRESET_COUNT:
                        presetCount = v > MAX_PRESETS ? (ushort)MAX_PRESETS : v;
                        MarkDirty(false, false, true, false);
                        return;
                    case AO_PROJECTOR_STATE: projectorState = v; MarkDirty(false, false, true, false); return;
                    case AO_WARM_PROGRESS: warmProgress = v; MarkDirty(false, false, false, true); return;
                    case AO_COOL_PROGRESS: coolProgress = v; MarkDirty(false, false, false, true); return;
                    case AO_TEST_MODE_SECONDS: testModeSeconds = v; return;

                    // AO_REMAINING, AO_LAMP_HOURS, AO_DISCARDED_FRAMES, AO_WARM_SECONDS,
                    // AO_COOL_SECONDS and AO_STILL_WARM_SECONDS are received and deliberately
                    // not forwarded. The overlays show a percentage, remaining is derived from
                    // the text joins, and the rest are diagnostics for the console, not the
                    // panel. 1631/1632 are free if a countdown is ever wanted.
                }
            }
        }

        private void HandleString(uint n, string v)
        {
            lock (gate)
            {
                switch (n)
                {
                    case SO_TITLE: sTitle = v; MarkDirty(false, true, false, false); return;
                    case SO_CLIP: sClip = v; MarkDirty(false, true, false, false); return;
                    case SO_STATUS: sStatus = v; MarkDirty(false, true, false, false); return;
                    case SO_POS_TEXT: sPosText = v; MarkDirty(false, true, false, false); return;
                    case SO_DUR_TEXT: sDurText = v; MarkDirty(false, true, false, false); return;
                    case SO_REM_TEXT: sRemText = v; MarkDirty(false, true, false, false); return;
                    case SO_ACTIVE_PRESET: sActivePreset = v; MarkDirty(false, false, true, false); return;
                    case SO_PROJECTOR_STATUS: sProjectorStatus = v; MarkDirty(false, false, true, false); return;
                }

                if (n >= SO_CONTENT_NAME_BASE && n < SO_CONTENT_NAME_BASE + MAX_CONTENT)
                {
                    contentName[n - SO_CONTENT_NAME_BASE] = v;
                    MarkDirty(true, false, false, false);
                    return;
                }
                if (n >= SO_PRESET_NAME_BASE && n < SO_PRESET_NAME_BASE + MAX_PRESETS)
                {
                    presetName[n - SO_PRESET_NAME_BASE] = v;
                    MarkDirty(false, false, true, false);
                    return;
                }
            }
        }

        // ── coalescing ────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Takes the lock even though most callers already hold it. Monitor is reentrant, so
        /// that is free, and it means the one caller that does not -- the online/offline
        /// handler, which runs on its own thread -- cannot lose a flag to a concurrent publish.
        /// </summary>
        private void MarkDirty(bool catalog, bool player, bool projector, bool transition)
        {
            lock (gate)
            {
                if (catalog) catalogDirty = true;
                if (player) playerDirty = true;
                if (projector) projectorDirty = true;
                if (transition) transitionDirty = true;

                if (publishTimer == null || publishTimer.Disposed)
                {
                    publishTimer = new CTimer(o => PublishDirty(), COALESCE_MS);
                }
            }
        }

        private void PublishDirty()
        {
            bool doCatalog, doPlayer, doProjector, doTransition;
            lock (gate)
            {
                doCatalog = catalogDirty; doPlayer = playerDirty;
                doProjector = projectorDirty; doTransition = transitionDirty;
                catalogDirty = playerDirty = projectorDirty = transitionDirty = false;
                if (publishTimer != null) { publishTimer.Dispose(); publishTimer = null; }
            }

            // The overlays are room-gated and the DCI state is source-gated, so they have
            // different audiences and are published separately.
            if (doTransition)
            {
                var theater = TheaterPanels();
                foreach (var tp in theater) WriteTransitions(tp, true);
                lock (overlayStatus)
                {
                    transitionPublishes++;
                    lastTransitionPublish = DateTime.Now;
                    lastTransitionAudience = theater.Count;
                }
            }

            // TSR movie lists, ahead of the DCI-source gate below. The HTML page asks for a
            // republish when it opens; a TSR page cannot, so its list has to be current already.
            if (doCatalog || doPlayer) PublishTsrMovieLists();

            if (!(doCatalog || doPlayer || doProjector)) return;
            if (!AnyDisplayOnDciServer()) return;   // nobody can have the page open

            var panels = HtmlPanels();
            if (panels.Count == 0) return;

            string catalogJson = doCatalog ? BuildCatalog() : null;
            string playerJson = doPlayer ? BuildPlayerState() : null;
            string projectorJson = doProjector ? BuildProjectorState() : null;

            ushort rev = 0;
            if (catalogJson != null)
            {
                // Only a real change earns a revision bump. barcoAlchemy re-sends its whole
                // content block on every reconnect, and bumping on an unchanged list would make
                // every page re-render for nothing.
                lock (gate)
                {
                    if (catalogJson == lastCatalog) { catalogJson = null; }
                    else { lastCatalog = catalogJson; rev = NextRev(); }
                }
            }

            foreach (var tp in panels)
            {
                if (catalogJson != null)
                {
                    tp.UserInterface.StringInput[CatalogJoin].StringValue = catalogJson;
                    tp.UserInterface.UShortInput[CatalogRevJoin].UShortValue = rev;
                }
                if (playerJson != null) tp.UserInterface.StringInput[PlayerJoin].StringValue = playerJson;
                if (projectorJson != null) tp.UserInterface.StringInput[ProjectorJoin].StringValue = projectorJson;
            }
        }

        /// <summary>
        /// Bumped on every catalog publish, skipping zero. An identical string written to a
        /// panel join is not a change and so publishes nothing; a page that opens after the
        /// last publish would see no list at all. The counter always changes, which gives the
        /// page an edge to re-read on. Zero is the power-on value and therefore not an edge.
        /// </summary>
        private ushort NextRev()
        {
            catalogRev = (ushort)(catalogRev + 1);
            if (catalogRev == 0) catalogRev = 1;
            return catalogRev;
        }

        /// <summary>
        /// Every HTML panel. The audience for the DCI state, because any panel in the house can
        /// select the theater room and open the page.
        /// </summary>
        private List<UI.TouchpanelUI> HtmlPanels()
        {
            var list = new List<UI.TouchpanelUI>();
            if (cs == null || cs.manager == null || cs.manager.touchpanelZ == null) return list;

            foreach (var kv in cs.manager.touchpanelZ)
            {
                var tp = kv.Value;
                if (tp == null || !tp.HTML_UI || tp.UserInterface == null) continue;
                list.Add(tp);
            }
            return list;
        }

        /// <summary>
        /// Panels currently showing a theater room. The audience for the warming and cooling
        /// overlays only -- they are full-screen, so they follow the room and not the source.
        ///
        /// **Unlike the DCI state, this deliberately includes dumb panels.** 1627-1632 are plain
        /// panel joins, not an HTML idea; a TSR-310 sitting in the theater wants the warming page
        /// as much as a TSW does, and writing four joins to a panel whose project happens not to
        /// define them costs nothing.
        /// </summary>
        private List<UI.TouchpanelUI> TheaterPanels()
        {
            var list = new List<UI.TouchpanelUI>();
            if (cs == null || cs.manager == null || cs.manager.touchpanelZ == null) return list;

            foreach (var kv in cs.manager.touchpanelZ)
            {
                var tp = kv.Value;
                if (tp == null || tp.UserInterface == null) continue;
                if (IsTheaterRoom(tp.CurrentRoomNum)) list.Add(tp);
            }
            return list;
        }

        /// <summary>
        /// TSR-310s whose .sgd actually has the movie list. An older .sgd without object 37 is
        /// skipped rather than written to, so a remote on the previous project keeps working.
        /// </summary>
        private List<UI.TouchpanelUI> TsrMovieListPanels()
        {
            var list = new List<UI.TouchpanelUI>();
            if (cs == null || cs.manager == null || cs.manager.touchpanelZ == null) return list;

            foreach (var kv in cs.manager.touchpanelZ)
            {
                var tp = kv.Value;
                if (tp == null || tp.TSR310 == null || tp.UserInterface == null) continue;
                if (!tp.UserInterface.SmartObjects.Contains(TsrMovieListId)) continue;
                list.Add(tp);
            }
            return list;
        }

        /// <summary>
        /// Titles, selection highlight and page status for every TSR movie list. Written to all of
        /// them regardless of room or source: it is a few dozen joins that change rarely, and a
        /// list that only fills once the page is open would show empty for a moment every time.
        /// Each part is skipped unless it changed since the last write.
        /// </summary>
        private void PublishTsrMovieLists()
        {
            var panels = TsrMovieListPanels();
            if (panels.Count == 0) return;

            string[] titles;
            int selected;
            string status, title;
            bool busy;
            lock (gate)
            {
                int n = Math.Min(contentCount, (ushort)TsrListSize);
                titles = new string[n];
                for (int i = 0; i < n; i++) titles[i] = contentName[i] ?? string.Empty;
                selected = selectedContent;
                status = sStatus ?? string.Empty;
                title = sTitle ?? string.Empty;
                busy = mediaFb[DO_BUSY];
            }

            string catalogKey = string.Join("\u0001", titles);
            bool catalogChanged = catalogKey != lastTsrCatalog;
            bool selectedChanged = catalogChanged || selected != lastTsrSelected;
            bool statusChanged = status != lastTsrStatus;
            bool titleChanged = title != lastTsrTitle;
            bool busyChanged = lastTsrBusy != busy;

            if (!(catalogChanged || selectedChanged || statusChanged || titleChanged || busyChanged)) return;

            if (catalogChanged && contentCount > TsrListSize)
            {
                CrestronConsole.PrintLine(
                    "AlchemyRelay: server has {0} items, TSR movie list shows the first {1}",
                    contentCount, TsrListSize);
            }

            foreach (var tp in panels)
            {
                try
                {
                    var so = tp.UserInterface.SmartObjects[TsrMovieListId];

                    if (catalogChanged)
                    {
                        so.UShortInput[TSR_LIST_COUNT].UShortValue = (ushort)titles.Length;
                        for (int i = 1; i <= TsrListSize; i++)
                        {
                            bool present = i <= titles.Length;
                            so.StringInput[(uint)(TSR_LIST_ITEM_BASE + i)].StringValue =
                                present ? titles[i - 1] : string.Empty;
                            so.BooleanInput[(uint)(TSR_LIST_ENABLE_BASE + i)].BoolValue = present;
                            so.BooleanInput[(uint)(TSR_LIST_VISIBLE_BASE + i)].BoolValue = present;
                        }
                    }

                    if (selectedChanged)
                    {
                        for (int i = 1; i <= TsrListSize; i++)
                        {
                            so.BooleanInput[(uint)(TSR_LIST_ITEM_BASE + i)].BoolValue = i == selected;
                        }
                    }

                    if (statusChanged) tp.UserInterface.StringInput[TsrStatusTextJoin].StringValue = status;
                    if (titleChanged) tp.UserInterface.StringInput[TsrLoadedTitleJoin].StringValue = title;
                    if (busyChanged) tp.UserInterface.BooleanInput[TsrBusyJoin].BoolValue = busy;
                }
                catch (Exception ex)
                {
                    CrestronConsole.PrintLine("AlchemyRelay: TP-{0} movie list write failed: {1}", tp.Number, ex.Message);
                }
            }

            lastTsrCatalog = catalogKey;
            lastTsrSelected = selected;
            lastTsrStatus = status;
            lastTsrTitle = title;
            lastTsrBusy = busy;
        }

        /// <summary>
        /// A press on a TSR movie list. Goes down exactly the path the HTML page's "select:N" does,
        /// so whether a press only loads the title or also plays it is barcoAlchemy's
        /// AutoPlayOnSelect -- one setting, the same for every panel.
        /// </summary>
        public void HandleTsrMovieListPress(ushort tpNumber, uint sigNumber)
        {
            if (sigNumber <= TSR_LIST_ITEM_BASE || sigNumber > TSR_LIST_ITEM_BASE + TsrListSize) return;
            int index = (int)(sigNumber - TSR_LIST_ITEM_BASE);

            int count;
            lock (gate) { count = contentCount; }
            if (index > count)
            {
                CrestronConsole.PrintLine("AlchemyRelay: TP-{0} pressed movie {1} but only {2} are listed",
                    tpNumber, index, count);
                return;
            }

            CrestronConsole.PrintLine("AlchemyRelay: TP-{0} movie list select {1}", tpNumber, index);
            HandleMediaCommand(tpNumber, "select:" + index);
        }

        private void WriteTransitions(UI.TouchpanelUI tp, bool active)
        {
            bool warming, cooling;
            ushort warmPct, coolPct;
            lock (gate)
            {
                warming = active && projFb[DO_WARMING] && !warmDismissed.Contains(tp.Number);
                cooling = active && projFb[DO_COOLING] && !coolDismissed.Contains(tp.Number);
                warmPct = warming ? warmProgress : (ushort)0;
                coolPct = cooling ? coolProgress : (ushort)0;
            }

            WriteTransitionJoins(tp, warming, warmPct, cooling, coolPct);
        }

        /// <summary>
        /// A panel's Exit button: hide the overlay on THAT panel for the rest of this transition.
        ///
        /// It has to be sticky. Simply clearing the join would last about a second -- the progress
        /// analog ticks throughout the warm-up and every tick republishes, so the page would come
        /// straight back and the button would look broken. The dismissal is dropped when the
        /// transition itself ends (see OnTransitionEnded), so the next warm-up shows normally.
        ///
        /// Per panel on purpose: one person dismissing the page in the theater should not clear it
        /// for someone else holding a different remote.
        /// </summary>
        public void DismissTransition(ushort tpNumber, bool warm)
        {
            lock (gate)
            {
                if (warm) warmDismissed.Add(tpNumber);
                else coolDismissed.Add(tpNumber);
            }
            CrestronConsole.PrintLine("AlchemyRelay: TP-{0} dismissed the {1} page",
                tpNumber, warm ? "warming" : "cooling");

            if (cs != null && cs.manager != null && cs.manager.touchpanelZ != null
                && cs.manager.touchpanelZ.ContainsKey(tpNumber))
            {
                var tp = cs.manager.touchpanelZ[tpNumber];
                if (tp != null && tp.UserInterface != null)
                {
                    WriteTransitions(tp, IsTheaterRoom(tp.CurrentRoomNum));
                }
            }
        }

        /// <summary>
        /// A transition finished. Forget who dismissed it, so the next one is shown to everybody.
        /// </summary>
        private void OnTransitionEnded(bool warm)
        {
            lock (gate)
            {
                if (warm) warmDismissed.Clear();
                else coolDismissed.Clear();
            }
        }

        /// <summary>What the overlay writer last did to one panel. Read by Describe().</summary>
        private sealed class OverlayWriteStatus
        {
            public bool Warming, Cooling;
            public ushort WarmPct, CoolPct;
            public ushort Room;
            public int Writes;
            public DateTime LastWrite;
            public string LastError;
            public DateTime LastErrorAt;
        }

        private readonly Dictionary<ushort, OverlayWriteStatus> overlayStatus =
            new Dictionary<ushort, OverlayWriteStatus>();

        // How often warming/cooling state reached the theater panels. During a transition this
        // should climb about four times a second; if it does not, ticks are not arriving on 0xA1.
        private int transitionPublishes;
        private DateTime lastTransitionPublish;
        private int lastTransitionAudience;

        /// <summary>
        /// The only place the four overlay joins are written. Split out of WriteTransitions so the
        /// console test can drive the identical path instead of inventing EISC state -- a test
        /// that takes a different route proves nothing about the route that matters.
        ///
        /// **Never throws, and each join is written on its own.** It runs in a loop over every
        /// theater panel on every progress tick, so one panel that rejects a join -- a remote
        /// whose project stops short of 1628, say -- would abort the loop and silently starve every
        /// panel after it, while still showing its own page because the digital went first. A
        /// failure is now recorded against that panel (see reportalchemy) and the rest carry on.
        /// </summary>
        private void WriteTransitionJoins(UI.TouchpanelUI tp, bool warming, ushort warmPct,
                                          bool cooling, ushort coolPct)
        {
            if (tp == null || tp.UserInterface == null) return;

            OverlayWriteStatus st;
            lock (overlayStatus)
            {
                if (!overlayStatus.TryGetValue(tp.Number, out st))
                {
                    st = new OverlayWriteStatus();
                    overlayStatus[tp.Number] = st;
                }
            }

            bool wasWarm = false, wasCool = false;
            string error = null;
            try { wasWarm = tp.UserInterface.BooleanInput[WarmingJoin].BoolValue; } catch { }
            try { wasCool = tp.UserInterface.BooleanInput[CoolingJoin].BoolValue; } catch { }

            try { tp.UserInterface.BooleanInput[WarmingJoin].BoolValue = warming; }
            catch (Exception ex) { error = "d" + WarmingJoin + ": " + ex.Message; }
            try { tp.UserInterface.UShortInput[WarmingProgressJoin].UShortValue = warmPct; }
            catch (Exception ex) { error = (error == null ? "" : error + "; ") + "a" + WarmingProgressJoin + ": " + ex.Message; }
            try { tp.UserInterface.BooleanInput[CoolingJoin].BoolValue = cooling; }
            catch (Exception ex) { error = (error == null ? "" : error + "; ") + "d" + CoolingJoin + ": " + ex.Message; }
            try { tp.UserInterface.UShortInput[CoolingProgressJoin].UShortValue = coolPct; }
            catch (Exception ex) { error = (error == null ? "" : error + "; ") + "a" + CoolingProgressJoin + ": " + ex.Message; }

            bool newError;
            lock (overlayStatus)
            {
                st.Warming = warming; st.WarmPct = warmPct;
                st.Cooling = cooling; st.CoolPct = coolPct;
                st.Room = tp.CurrentRoomNum;
                st.Writes++;
                st.LastWrite = DateTime.Now;
                newError = error != null && error != st.LastError;
                if (error != null) { st.LastError = error; st.LastErrorAt = DateTime.Now; }
            }

            // A failing join is logged once per distinct error, not on every tick.
            if (newError)
            {
                CrestronConsole.PrintLine("AlchemyRelay: TP-{0} overlay write FAILED - {1}", tp.Number, error);
            }

            // Edge-logged unconditionally, not under cs.logging. It is a rare, human-visible
            // event, and when the overlay fails to appear the first question is always whether
            // C# wrote the join at all.
            if (warming != wasWarm || cooling != wasCool)
            {
                CrestronConsole.PrintLine(
                    "AlchemyRelay: TP-{0} overlays warming={1} ({2}) cooling={3} ({4}) room {5}",
                    tp.Number, warming, warmPct, cooling, coolPct, tp.CurrentRoomNum);
            }
        }

        /// <summary>
        /// Force the overlay joins from the console, bypassing both the EISC and the room gate:
        ///     alchemyoverlay warm | cool | off [tp]
        ///
        /// This answers the one question static reading cannot. If the page appears, the panel
        /// joins and the HTML are sound and the fault is upstream -- the EISC or the room gate.
        /// If it does not, the fault is in the panel or the page.
        /// </summary>
        /// <summary>
        /// barcotestmode: ask barcoAlchemy to refuse dowser opens for an hour, or stop now.
        ///
        /// The mode, its hour and the dowser enforcement all live in barcoAlchemy, not here, so
        /// they hold if this program restarts. This only presses its join; status is whatever
        /// barcoAlchemy reports back on DigitalOut 28 / AnalogOut 20, never what was asked for.
        /// </summary>
        public string SetTestMode(bool on)
        {
            if (eisc == null) return "barcoAlchemy EISC not constructed";
            if (!eisc.IsOnline) return "barcoAlchemy EISC 0xA1 is OFFLINE - command not sent";

            Pulse(on ? D_TEST_MODE_ENTER : D_TEST_MODE_EXIT);
            return on
                ? "test mode requested: dowser opens refused for 60 minutes (barcotestmode status to check)"
                : "test mode exit requested (barcotestmode status to check)";
        }

        public string DescribeTestMode()
        {
            lock (gate)
            {
                return testMode
                    ? string.Format("ON - dowser opens refused, {0} min left", (testModeSeconds + 59) / 60)
                    : "off";
            }
        }

        public string ForceOverlay(string which, ushort onlyTp)
        {
            bool warm = which == "warm", cool = which == "cool";
            var targets = new List<UI.TouchpanelUI>();
            if (cs != null && cs.manager != null && cs.manager.touchpanelZ != null)
            {
                // Every panel, HTML or dumb -- the point of this command is to test a TSR-310's
                // pages as much as an HTML one's.
                foreach (var kv in cs.manager.touchpanelZ)
                {
                    var tp = kv.Value;
                    if (tp == null || tp.UserInterface == null) continue;
                    if (onlyTp == 0 || tp.Number == onlyTp) targets.Add(tp);
                }
            }
            if (targets.Count == 0)
            {
                return onlyTp == 0
                    ? "no panels are online - nothing to write to"
                    : string.Format("TP-{0} is not an online panel", onlyTp);
            }

            var sb = new StringBuilder();
            foreach (var tp in targets)
            {
                WriteTransitionJoins(tp, warm, warm ? (ushort)32767 : (ushort)0,
                                         cool, cool ? (ushort)32767 : (ushort)0);
                sb.Append(sb.Length > 0 ? ", " : "").Append("TP-").Append(tp.Number);
            }
            return string.Format(
                "forced {0} on {1}\r\n"
                + "  The room gate is ignored on purpose. If the page appears, the panel and the\r\n"
                + "  HTML are fine and the fault is upstream - check the 'overlays to' line of\r\n"
                + "  reportalchemy during a real warm-up.\r\n"
                + "  Clear it with: alchemyoverlay off",
                warm ? "WARMING" : cool ? "COOLING" : "off", sb);
        }

        // ── JSON ──────────────────────────────────────────────────────────────────────────

        private string BuildCatalog()
        {
            var sb = new StringBuilder("[");
            lock (gate)
            {
                bool first = true;
                for (int i = 0; i < contentCount && i < MAX_CONTENT; i++)
                {
                    // Empty slots are dropped from the array but "i" stays the real slot number,
                    // so a gap removes a button without renumbering the ones after it.
                    if (string.IsNullOrEmpty(contentName[i])) continue;
                    if (!first) sb.Append(',');
                    first = false;
                    sb.Append("{\"i\":").Append(i + 1)
                      .Append(",\"t\":\"").Append(JsonEscape(contentName[i])).Append("\"}");
                }
            }
            return sb.Append(']').ToString();
        }

        private string BuildPlayerState()
        {
            var sb = new StringBuilder(384);
            lock (gate)
            {
                sb.Append('{')
                  .Append("\"online\":").Append(B(mediaFb[DO_ONLINE]))
                  .Append(",\"loggedIn\":").Append(B(mediaFb[DO_LOGGED_IN]))
                  .Append(",\"state\":").Append(playerState)
                  .Append(",\"status\":\"").Append(JsonEscape(sStatus)).Append('"')
                  .Append(",\"title\":\"").Append(JsonEscape(sTitle)).Append('"')
                  .Append(",\"clip\":\"").Append(JsonEscape(sClip)).Append('"')
                  .Append(",\"selected\":").Append(selectedContent)
                  .Append(",\"pos\":").Append(position)
                  .Append(",\"dur\":").Append(duration)
                  .Append(",\"progress\":").Append(progress)
                  .Append(",\"posText\":\"").Append(JsonEscape(sPosText)).Append('"')
                  .Append(",\"durText\":\"").Append(JsonEscape(sDurText)).Append('"')
                  .Append(",\"remText\":\"").Append(JsonEscape(sRemText)).Append('"')
                  .Append(",\"playAvailable\":").Append(B(mediaFb[DO_PLAY_AVAILABLE]))
                  .Append(",\"transportAvailable\":").Append(B(mediaFb[DO_TRANSPORT_AVAILABLE]))
                  .Append(",\"mediaInputReady\":").Append(B(mediaFb[DO_MEDIA_INPUT_READY]))
                  .Append(",\"busy\":").Append(B(mediaFb[DO_BUSY]))
                  .Append(",\"error\":").Append(playerError)
                  .Append('}');
            }
            return sb.ToString();
        }

        private string BuildProjectorState()
        {
            var sb = new StringBuilder(512);
            lock (gate)
            {
                sb.Append('{')
                  .Append("\"online\":").Append(B(projFb[DO_PROJ_ONLINE]))
                  .Append(",\"power\":").Append(B(projFb[DO_POWER_ON]))
                  .Append(",\"dowserOpen\":").Append(B(projFb[DO_DOWSER_OPEN]))
                  .Append(",\"busy\":").Append(B(projFb[DO_PROJ_BUSY]))
                  .Append(",\"activePreset\":\"").Append(JsonEscape(sActivePreset)).Append('"')
                  .Append(",\"selected\":").Append(selectedPreset)
                  .Append(",\"status\":\"").Append(JsonEscape(sProjectorStatus)).Append('"')
                  .Append(",\"presets\":[");

                // Gaps are load-bearing: the ICMP's 50 preset slots are sparse and its web GUI
                // numbers them by slot, so slot 7 must stay preset 7 on the panel even when
                // 5 and 6 are blank. Emitting "" keeps the page's index alignment.
                for (int i = 0; i < presetCount && i < MAX_PRESETS; i++)
                {
                    if (i > 0) sb.Append(',');
                    sb.Append('"').Append(JsonEscape(presetName[i])).Append('"');
                }
                sb.Append("]}");
            }
            return sb.ToString();
        }

        private static string B(bool v) { return v ? "true" : "false"; }

        private static string JsonEscape(string s)
        {
            if (string.IsNullOrEmpty(s)) return string.Empty;
            var sb = new StringBuilder(s.Length + 8);
            foreach (char c in s)
            {
                if (c == '"' || c == '\\') sb.Append('\\').Append(c);
                else if (c == '\n') sb.Append("\\n");
                else if (c == '\r') sb.Append("\\r");
                else if (c == '\t') sb.Append("\\t");
                else if (c < ' ') sb.Append("\\u").Append(((int)c).ToString("x4"));
                else sb.Append(c);
            }
            return sb.ToString();
        }

        // ── panels → here ─────────────────────────────────────────────────────────────────

        /// <summary>One panel's DCI page just opened and wants the current state (analog 1622).</summary>
        public void HandleRepublish(ushort tpNumber)
        {
            if (cs == null || cs.manager == null
                || !cs.manager.touchpanelZ.ContainsKey(tpNumber)) return;
            var tp = cs.manager.touchpanelZ[tpNumber];
            if (tp == null || !tp.HTML_UI || tp.UserInterface == null) return;

            // Answered unconditionally. The page asking is proof it is open, which is better
            // evidence than anything we could infer, and this is the one path that has to work
            // when a page opens between publishes.
            PushEverythingTo(tp);
            if (cs.logging) CrestronConsole.PrintLine("AlchemyRelay: TP-{0} asked for the DCI state", tpNumber);
        }

        private void PushEverythingTo(UI.TouchpanelUI tp)
        {
            string catalogJson = BuildCatalog();
            ushort rev;
            lock (gate)
            {
                lastCatalog = catalogJson;
                rev = NextRev();
            }

            tp.UserInterface.StringInput[CatalogJoin].StringValue = catalogJson;
            tp.UserInterface.UShortInput[CatalogRevJoin].UShortValue = rev;
            tp.UserInterface.StringInput[PlayerJoin].StringValue = BuildPlayerState();
            tp.UserInterface.StringInput[ProjectorJoin].StringValue = BuildProjectorState();

            // Overlays follow the room even here: a panel that has wandered out of the theater
            // gets them cleared, not refreshed.
            WriteTransitions(tp, IsTheaterRoom(tp.CurrentRoomNum));
        }

        /// <summary>
        /// A panel changed rooms. Only the overlays care: they are full-screen and follow the
        /// room, so a panel entering the theater mid-warm-up needs to show it, and one leaving
        /// needs it cleared -- a warming page left up on a panel someone has walked away with is
        /// a panel nobody can use.
        ///
        /// The DCI state does not care. It goes to every HTML panel regardless of room.
        /// </summary>
        public void OnPanelRoomChanged(ushort tpNumber)
        {
            ReassertOverlays(tpNumber);
        }

        /// <summary>
        /// Re-send one panel's overlay joins from current state, gated by its room.
        ///
        /// Called on a room change and on entry to the video subsystem. The second is the case
        /// that is easy to miss: pressing Power Off drops the panel out of the video menu, and
        /// someone who walks straight back in expects to see the cooling page. The joins are
        /// sticky so in principle it is still set, but "in principle" is doing real work there --
        /// the panel may have been elsewhere, offline, or in another room when the edge went by,
        /// and re-asserting on entry costs four join writes.
        /// </summary>
        public void ReassertOverlays(ushort tpNumber)
        {
            if (cs == null || cs.manager == null
                || !cs.manager.touchpanelZ.ContainsKey(tpNumber)) return;
            var tp = cs.manager.touchpanelZ[tpNumber];
            // No HTML_UI filter: the overlay joins are plain panel joins and a TSR-310 wants them.
            if (tp == null || tp.UserInterface == null) return;

            WriteTransitions(tp, IsTheaterRoom(tp.CurrentRoomNum));
        }

        /// <summary>
        /// Media command from a panel on serial 1624:
        /// play / pause / stop / clear / next / prev / refresh / prepare
        /// skip:&lt;±seconds&gt; / select:&lt;1-50&gt;
        /// </summary>
        public void HandleMediaCommand(ushort tpNumber, string payload)
        {
            if (eisc == null || string.IsNullOrEmpty(payload)) return;
            string cmd = payload.Trim().ToLower();

            switch (cmd)
            {
                case "play": Pulse(D_PLAY); return;
                case "pause": Pulse(D_PAUSE); return;
                case "stop": Pulse(D_STOP); return;
                case "clear": Pulse(D_CLEAR); return;
                case "next": Pulse(D_NEXT); return;
                case "prev": Pulse(D_PREV); return;
                case "refresh": Pulse(D_REFRESH); return;
                case "prepare": Pulse(D_PREPARE); return;
            }

            if (cmd.StartsWith("select:"))
            {
                int index;
                if (!int.TryParse(cmd.Substring(7).Trim(), out index)
                    || index < 1 || index > MAX_CONTENT)
                {
                    CrestronConsole.PrintLine("AlchemyRelay: TP-{0} bad select \"{1}\"", tpNumber, payload);
                    return;
                }
                if (cs.logging) CrestronConsole.PrintLine("AlchemyRelay: TP-{0} select content {1}", tpNumber, index);
                Pulse(D_CONTENT_SELECT_BASE + (uint)(index - 1));
                return;
            }

            if (cmd.StartsWith("skip:"))
            {
                int seconds;
                if (!int.TryParse(cmd.Substring(5).Trim(), out seconds) || seconds == 0)
                {
                    CrestronConsole.PrintLine("AlchemyRelay: TP-{0} bad skip \"{1}\"", tpNumber, payload);
                    return;
                }

                // The EISC has four fixed skip sizes each way, not an arbitrary seek. Match the
                // magnitude exactly rather than rounding to the nearest: a mis-parsed value
                // silently jumping five minutes is worse than doing nothing and saying so.
                int magnitude = seconds < 0 ? -seconds : seconds;
                int slot = -1;
                for (int i = 0; i < SKIP_SECONDS.Length; i++)
                {
                    if (SKIP_SECONDS[i] == magnitude) { slot = i; break; }
                }
                if (slot < 0)
                {
                    CrestronConsole.PrintLine(
                        "AlchemyRelay: TP-{0} skip of {1}s has no join - only 15/30/60/300 exist",
                        tpNumber, magnitude);
                    return;
                }

                Pulse((seconds < 0 ? D_SKIP_BACK_BASE : D_SKIP_FWD_BASE) + (uint)slot);
                return;
            }

            CrestronConsole.PrintLine("AlchemyRelay: TP-{0} unknown media command \"{1}\"", tpNumber, payload);
        }

        /// <summary>
        /// Projector command from a panel on serial 1626:
        /// power:on / power:off / dowser:open / dowser:close / preset:&lt;1-50&gt; / reconnect
        /// </summary>
        public void HandleProjectorCommand(ushort tpNumber, string payload)
        {
            if (eisc == null || string.IsNullOrEmpty(payload)) return;
            string cmd = payload.Trim().ToLower();

            switch (cmd)
            {
                // Always logged, whatever cs.logging says. Powering a cinema projector is worth
                // a line in the log, and so is the dowser -- it is the only thing between a
                // warming lamp and whatever is on screen.
                case "power:on":
                    CrestronConsole.PrintLine("AlchemyRelay: TP-{0} projector POWER ON", tpNumber);
                    Pulse(D_POWER_ON); return;
                case "power:off":
                    CrestronConsole.PrintLine("AlchemyRelay: TP-{0} projector POWER OFF", tpNumber);
                    Pulse(D_POWER_OFF); return;
                case "dowser:open":
                    CrestronConsole.PrintLine("AlchemyRelay: TP-{0} dowser OPEN", tpNumber);
                    Pulse(D_DOWSER_OPEN); return;
                case "dowser:close":
                    CrestronConsole.PrintLine("AlchemyRelay: TP-{0} dowser CLOSE", tpNumber);
                    Pulse(D_DOWSER_CLOSE); return;
                case "reconnect":
                    Pulse(D_RECONNECT); return;
            }

            if (cmd.StartsWith("preset:"))
            {
                int index;
                if (!int.TryParse(cmd.Substring(7).Trim(), out index)
                    || index < 1 || index > MAX_PRESETS)
                {
                    CrestronConsole.PrintLine("AlchemyRelay: TP-{0} bad preset \"{1}\"", tpNumber, payload);
                    return;
                }
                if (cs.logging) CrestronConsole.PrintLine("AlchemyRelay: TP-{0} preset {1}", tpNumber, index);
                Pulse(D_PRESET_SELECT_BASE + (uint)(index - 1));
                return;
            }

            CrestronConsole.PrintLine("AlchemyRelay: TP-{0} unknown projector command \"{1}\"", tpNumber, payload);
        }

        /// <summary>
        /// Rising-edge trigger, held high for PULSE_MS and then released.
        ///
        /// **The width is the point.** Setting true and false back to back works on a loopback
        /// EISC, where both writes are queued locally and delivered in order. Across a network
        /// EISC the two can be coalesced before transmission -- CIP sends the join's current
        /// value, and by the time it does, the value is false again. The far end sees no edge
        /// and the command vanishes, while every absolute value on the same link keeps arriving
        /// normally. That asymmetry is the signature: feedback fine, commands dead, and only
        /// after the link moved off 127.0.0.2.
        ///
        /// It is still released, rather than left high: barcoAlchemy acts on the rising edge,
        /// and a join left high would re-fire when the link reconnects and re-sends its state --
        /// which for power:on would relight the projector on its own.
        /// </summary>
        private const long PULSE_MS = 200;

        private void Pulse(uint join)
        {
            if (eisc == null) return;

            eisc.BooleanInput[join].BoolValue = true;

            lock (pulseGate)
            {
                CTimer existing;
                if (pulseTimers.TryGetValue(join, out existing) && existing != null)
                {
                    // A second press before the first released. Restart the clock rather than
                    // letting the old timer drop the join early.
                    existing.Stop();
                    existing.Dispose();
                }
                pulseTimers[join] = new CTimer(o => ReleasePulse(join), PULSE_MS);
            }
        }

        private void ReleasePulse(uint join)
        {
            try { if (eisc != null) eisc.BooleanInput[join].BoolValue = false; }
            catch (Exception ex) { ErrorLog.Error("AlchemyRelay: pulse release on {0} failed: {1}", join, ex.Message); }

            lock (pulseGate)
            {
                CTimer t;
                if (pulseTimers.TryGetValue(join, out t) && t != null) t.Dispose();
                pulseTimers.Remove(join);
            }
        }

        /// <summary>Console diagnostics: what the relay currently believes.</summary>
        public string Describe()
        {
            var sb = new StringBuilder();
            sb.AppendLine(string.Format("  EISC 0x{0:X2}      {1}", cfg.EiscIpid,
                eisc == null ? "NOT REGISTERED" : (eisc.IsOnline ? "online" : "offline")));

            sb.AppendLine(string.Format("  config         {0}", (cfg ?? DefaultConfig).Describe()));

            var rooms = TheaterRooms();
            var rsb = new StringBuilder();
            foreach (ushort r in rooms) rsb.Append(rsb.Length > 0 ? ", " : "").Append(r);
            sb.AppendLine(string.Format("  theater rooms  {0}", rooms.Count == 0 ? "(none found)" : rsb.ToString()));

            // The three ways projector power can silently do nothing, all printed together:
            // no display derived, DrivePower off, or the link down.
            var projDisp = ProjectorDisplays();
            var dsb = new StringBuilder();
            foreach (ushort d in projDisp) dsb.Append(dsb.Length > 0 ? ", " : "").Append(d);
            sb.AppendLine(string.Format("  projector disp {0}{1}",
                projDisp.Count == 0 ? "(none found)" : dsb.ToString(),
                (cfg != null && !cfg.DrivePower) ? "   <- DrivePower is FALSE, no power will be sent" : ""));

            if (projDisp.Count == 0)
            {
                sb.AppendLine("                 no display runs a source scenario containing a DCI source -");
                sb.AppendLine("                 check the source Name against the DciSourceNames list above");
            }

            bool onDci = AnyDisplayOnDciServer();
            sb.AppendLine(string.Format("  DCI selected   {0}{1}", onDci,
                onDci ? "" : "  <- DCI state is NOT being published; select the Alchemy source"));
            sb.AppendLine(string.Format("  DCI state to   {0} HTML panel(s)", HtmlPanels().Count));

            var panels = TheaterPanels();
            var psb = new StringBuilder();
            foreach (var tp in panels) psb.Append(psb.Length > 0 ? ", " : "").Append("TP-").Append(tp.Number);
            sb.AppendLine(string.Format("  overlays to    {0}",
                panels.Count == 0 ? "(no panel showing a theater room)" : psb.ToString()));

            // Overlay delivery, per panel. Answers "did C# send it, to whom, and did it stick".
            lock (overlayStatus)
            {
                sb.AppendLine(string.Format("  overlay ticks  {0} publish(es), last {1} to {2} panel(s)",
                    transitionPublishes,
                    transitionPublishes == 0 ? "never" : lastTransitionPublish.ToString("HH:mm:ss"),
                    lastTransitionAudience));
                foreach (var kv in overlayStatus)
                {
                    var st = kv.Value;
                    sb.AppendLine(string.Format(
                        "    TP-{0,-3} room {1,-3} warm={2} {3,5}  cool={4} {5,5}  writes {6,-6} last {7}{8}",
                        kv.Key, st.Room, st.Warming ? "ON " : "off", st.WarmPct,
                        st.Cooling ? "ON " : "off", st.CoolPct, st.Writes,
                        st.LastWrite.ToString("HH:mm:ss"),
                        st.LastError == null ? "" : "  ERROR @" + st.LastErrorAt.ToString("HH:mm:ss") + ": " + st.LastError));
                }
            }

            // Every panel's current room, on one line. The overlays follow CurrentRoomNum, so a
            // panel that "is in the theater" on screen but not here will never get them.
            if (cs != null && cs.manager != null && cs.manager.touchpanelZ != null)
            {
                var roomSb = new StringBuilder();
                foreach (var kv in cs.manager.touchpanelZ)
                {
                    if (kv.Value == null) continue;
                    roomSb.Append(roomSb.Length > 0 ? " " : "").Append(kv.Key).Append(':').Append(kv.Value.CurrentRoomNum);
                }
                sb.AppendLine("  panel rooms    " + roomSb);
            }

            sb.AppendLine(string.Format("  media          online={0} loggedIn={1} state={2} playAvail={3} inputReady={4}",
                mediaFb[DO_ONLINE], mediaFb[DO_LOGGED_IN], playerState,
                mediaFb[DO_PLAY_AVAILABLE], mediaFb[DO_MEDIA_INPUT_READY]));
            sb.AppendLine(string.Format("  projector      online={0} power={1} dowserOpen={2} preset={3} \"{4}\"",
                projFb[DO_PROJ_ONLINE], projFb[DO_POWER_ON], projFb[DO_DOWSER_OPEN],
                selectedPreset, sActivePreset));
            sb.AppendLine(string.Format("  transitions    warming={0} ({1}) cooling={2} ({3}) stillWarm={4}",
                projFb[DO_WARMING], warmProgress, projFb[DO_COOLING], coolProgress, projFb[DO_STILL_WARM]));
            sb.AppendLine("  test mode      " + DescribeTestMode());
            sb.AppendLine(string.Format("  content        {0} items, selected {1}", contentCount, selectedContent));
            sb.Append(string.Format("  presets        {0} slots", presetCount));
            return sb.ToString();
        }
    }
}
