using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Crestron.SimplSharp;
using Crestron.SimplSharp.CrestronIO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ACS_4Series_Template_V3.AvState
{
    /// <summary>
    /// Remembers what every TV and music zone is playing across a program restart, and keeps a
    /// usage log of every TV / music on, off and source change.
    ///
    /// WHY (2026-10-07): a program reload reset every room to "no source" (SystemManager seeds
    /// CurrentVideoSrc / CurrentMusicSrc = 0), so a family room still watching Apple TV 1 showed
    /// "Off" on every panel until someone pressed something. On a DM system the switcher's route
    /// feedback could have rebuilt that, but NVX/NAX have nothing equivalent: an NVX decoder stays
    /// subscribed when its room is turned off (deliberately — see VideoSystemControl, "NOT clearing
    /// the NVX stream location on off"), and nothing reports TV power. So the state is written down
    /// instead, and read back at startup.
    ///
    /// STATE  \NVRAM\avState.json — each display's source and each room's current display and
    ///        music source, with when each started. Written SaveDebounceMs after a change (a burst
    ///        of presses is one write), via a temp file so a power cut mid-write cannot leave a
    ///        half file. A few writes an hour of under 1 KB: no measurable CPU or flash cost.
    ///
    /// RESTORE runs once, right after StartupRooms (displays are bound to rooms by then, and the
    ///        panels are built afterwards, so they pick the restored state up). A file older than
    ///        MaxRestoreAgeHours is ignored: after a long outage the house has probably moved on,
    ///        and "off" is the safer guess.
    ///        A saved source is only restored if it is still the SAME source: the number must
    ///        exist and its name and stream address must match what was saved. A restart is often
    ///        a config change — a source added, removed or edited — and a number that now means
    ///        something else must not be put back. Such a TV is left off (logged as NOT RESTORED).
    ///
    /// NVX    WHY (2026-10-07, family room): the restart stops this program for about a minute, and
    ///        the NVX decoders are registered HERE, not in SIMPL. A decoder reconnecting to the new
    ///        program gets the new program's stream setting — which nothing had set, so it was
    ///        empty. The family room lost its picture, its TV went to standby, and someone had to
    ///        reselect Her Apple TV. So when a decoder comes online and its TV is on, the stream it
    ///        should have is re-sent (after DecoderSettleMs, then only if the decoder disagrees),
    ///        and what the decoder reported is logged — the next restart proves it either way.
    ///
    /// NAX    The template sends NAX routing to SIMPL over EISCs: musicEISC1 (0x8B) analogs 500+zone
    ///        (switcher input) and 600+n (streaming provider), and musicEISC3 (0x8D) serials
    ///        300+zone (multicast stream) and 500+zone (source name to the zone module). A restarted
    ///        program's EISC values all start at 0 / empty, so the same reconnect would cut every
    ///        zone. They are saved with the rest of the state (read back from the EISC inputs, since
    ///        a dozen call sites write them) and re-sent by RestoreEiscRouting() immediately after
    ///        the EISCs are registered — before the link has connected, so SIMPL's first update
    ///        already carries the real routing rather than zeros.
    ///
    /// ROOM   videoEISC1 (0x8E) analogs 500/600/700/800/900 + video output: switcher input, TV
    ///        input, receiver input, alt switcher input, room module input. WHY (2026-10-07): these
    ///        feed SIMPL's ROOM_CONTROL_V2 module, which sends DISPLAY_OFF and REC_OFF the moment the
    ///        TV input and receiver input both read 0 — and 0 is exactly what a restarted program
    ///        reconnects with. That (not only the NVX stream) is what put the family room TV in
    ///        standby. They are saved per output and re-sent with the NAX routing, before the link
    ///        connects, but only for an output whose saved TV source (or music, for a receiver
    ///        playing music) passes the same-source check — otherwise it stays 0, i.e. off.
    ///
    /// PANELS Per panel: imageEISC (0x91) digital TP / TP+100 ("current subsystem is video / audio")
    ///        and videoEISC1 (0x8E) analog 300+TP (the video source equip ID that connects the
    ///        panel's controls to a source in SIMPL), plus each room's volume owner (LastSystemVid).
    ///        Sent right after EISC registration so SIMPL never sees zeros; once the TVs are restored
    ///        the program recalculates them itself for each panel's (default) room, which is then
    ///        authoritative.
    ///
    /// USAGE  User/usage/usage-yyyyMMdd.log, one line per event, with how long the previous state
    ///        lasted. ~100 bytes a line; a busy house is tens of KB a day. Kept up to MaxDays (90),
    ///        but oldest days are dropped once the folder passes MaxTotalBytes (2 MB) — never below
    ///        MinDays (30). For scale: a CP4 has ~2.8 GB free (`FREE`, 2026-10-07).
    ///
    /// Hooks: VideoDisplaysConfig.CurrentVideoSrc and RoomConfig.CurrentMusicSrc setters — the one
    /// place every change passes through. Until Restore() has run (Ready == false) they are
    /// ignored, so startup's own zeroing is neither saved nor logged.
    /// </summary>
    public class AvStateManager
    {
        private const string StatePath = @"\NVRAM\avState.json";
        private const string StateTmpPath = @"\NVRAM\avState.json.tmp";
        private const long SaveDebounceMs = 3000;
        private const double MaxRestoreAgeHours = 12;
        private const int MinDays = 30;
        private const int MaxDays = 90;
        // ~100 bytes a line; a busy 18-room house is 10-30 KB a day, so 90 days fits in 1-3 MB.
        // 2 MB keeps the folder small on purpose; MinDays still wins if a house is busier than that.
        private const long MaxTotalBytes = 2L * 1024 * 1024;
        private const long DecoderSettleMs = 3000;     // let the decoder report its stream first
        private const long NaxCheckMs = 60000;         // catch NAX / room / panel joins changed outside the hooks
        private const ushort MaxZone = 99;             // 500+99 stays clear of the 600+ provider block

        private readonly ControlSystem _cs;
        private string _lastNaxSignature = "";
        private CTimer _naxCheckTimer;
        private readonly HashSet<CTimer> _pendingDecoderChecks = new HashSet<CTimer>();
        private readonly object _lock = new object();
        private readonly string _usageDir;
        private string _usageDay = "";
        private CTimer _saveTimer;

        // When each display's current source / each room's music started. Kept in memory and in
        // the state file, so durations in the usage log survive a restart.
        private readonly Dictionary<ushort, DateTime> _displaySince = new Dictionary<ushort, DateTime>();
        private readonly Dictionary<ushort, DateTime> _musicSince = new Dictionary<ushort, DateTime>();

        /// <summary>False until Restore() has run. Setter hooks do nothing while false.</summary>
        public bool Ready { get; private set; }

        public AvStateManager(ControlSystem cs)
        {
            _cs = cs;
            _usageDir = string.Format(@"{0}/User/usage", Directory.GetApplicationRootDirectory());
        }

        public string UsageDirectory { get { return _usageDir; } }

        // ─── hooks (from the setters) ──────────────────────────────────────

        public void OnDisplaySourceChanged(ushort displayNumber, ushort oldSrc, ushort newSrc)
        {
            if (!Ready || oldSrc == newSrc) { return; }
            try
            {
                DateTime now = DateTime.Now;
                DateTime since;
                lock (_lock)
                {
                    if (!_displaySince.TryGetValue(displayNumber, out since)) { since = DateTime.MinValue; }
                    _displaySince[displayNumber] = now;
                }

                string where = DisplayLabel(displayNumber);
                if (oldSrc == 0)
                {
                    Log(string.Format("TV ON      {0} | {1}", where, VideoName(newSrc)));
                }
                else if (newSrc == 0)
                {
                    Log(string.Format("TV OFF     {0} | was {1}{2}", where, VideoName(oldSrc), For(since, now)));
                }
                else
                {
                    Log(string.Format("TV SOURCE  {0} | {1} -> {2} ({1}{3})", where, VideoName(oldSrc), VideoName(newSrc), For(since, now)));
                }
                ScheduleSave();
            }
            catch (Exception ex)
            {
                ErrorLog.Error("[AVSTATE] display {0} change not recorded: {1}", displayNumber, ex.Message);
            }
        }

        public void OnRoomMusicChanged(ushort roomNumber, ushort oldSrc, ushort newSrc)
        {
            if (!Ready || oldSrc == newSrc) { return; }
            try
            {
                DateTime now = DateTime.Now;
                DateTime since;
                lock (_lock)
                {
                    if (!_musicSince.TryGetValue(roomNumber, out since)) { since = DateTime.MinValue; }
                    _musicSince[roomNumber] = now;
                }

                string where = RoomName(roomNumber);
                if (oldSrc == 0)
                {
                    Log(string.Format("MUSIC ON   {0} | {1}", where, MusicName(newSrc)));
                }
                else if (newSrc == 0)
                {
                    Log(string.Format("MUSIC OFF  {0} | was {1}{2}", where, MusicName(oldSrc), For(since, now)));
                }
                else
                {
                    Log(string.Format("MUSIC SRC  {0} | {1} -> {2} ({1}{3})", where, MusicName(oldSrc), MusicName(newSrc), For(since, now)));
                }
                ScheduleSave();
            }
            catch (Exception ex)
            {
                ErrorLog.Error("[AVSTATE] room {0} music change not recorded: {1}", roomNumber, ex.Message);
            }
        }

        // ─── restore ───────────────────────────────────────────────────────

        /// <summary>
        /// Called at the start of InitializeSystem (including a reload). Writes any pending save
        /// first — the rooms are about to be rebuilt and this is the last moment their state is
        /// known — then ignores changes until the next Restore().
        /// </summary>
        public void Suspend()
        {
            bool pending;
            lock (_lock)
            {
                pending = _saveTimer != null;
                if (_saveTimer != null) { _saveTimer.Stop(); _saveTimer.Dispose(); _saveTimer = null; }
            }
            if (pending && Ready) { SaveNow(); }
            Ready = false;
            lock (_lock)
            {
                if (_naxCheckTimer != null) { _naxCheckTimer.Stop(); _naxCheckTimer.Dispose(); _naxCheckTimer = null; }
            }
        }

        /// <summary>The saved state if it is recent enough to trust, else null (with why in `note`).</summary>
        private JObject UsableState(out string note)
        {
            JObject state = ReadState();
            if (state == null) { note = "no saved state"; return null; }
            DateTime saved;
            if (DateTime.TryParse((string)state["saved"], out saved)
                && (DateTime.Now - saved).TotalHours > MaxRestoreAgeHours)
            {
                note = string.Format("saved state is from {0:yyyy-MM-dd HH:mm} (> {1}h old) - not restored, everything starts off", saved, MaxRestoreAgeHours);
                return null;
            }
            note = string.Format("saved {0}", (string)state["saved"] ?? "?");
            return state;
        }

        /// <summary>
        /// Re-send the NAX routing (0x8B analogs 500+/600+, 0x8D serials 300+/500+) exactly as it
        /// was. Call IMMEDIATELY after the EISCs are registered — the link connects a moment later
        /// and its first update to SIMPL should already carry these values, not zeros.
        /// </summary>
        public void RestoreEiscRouting()
        {
            try
            {
                string note;
                JObject state = UsableState(out note);
                JObject nax = state != null ? state["nax"] as JObject : null;
                if (nax == null)
                {
                    CrestronConsole.PrintLine("[AVSTATE] NAX routing not restored - {0}", state == null ? note : "none saved");
                    return;
                }
                int a = 0, s = 0;
                a += WriteAnalogs(_cs.musicEISC1, nax["a500"] as JObject, 500);
                a += WriteAnalogs(_cs.musicEISC1, nax["a600"] as JObject, 600);
                s += WriteSerials(_cs.musicEISC3, nax["s300"] as JObject, 300);
                s += WriteSerials(_cs.musicEISC3, nax["s500"] as JObject, 500);
                string line = string.Format("NAX ROUTING re-sent to SIMPL: {0} analog(s) on 0x8B, {1} serial(s) on 0x8D - {2}", a, s, note);
                CrestronConsole.PrintLine("[AVSTATE] {0}", line);
                Log(line);
            }
            catch (Exception ex)
            {
                ErrorLog.Error("[AVSTATE] NAX routing restore failed: {0}", ex.Message);
            }
            RestoreRoomInputs();
        }

        private static readonly ushort[] RoomInputBases = { 500, 600, 700, 800, 900 };

        /// <summary>
        /// Re-send each video output's TV / receiver / switcher inputs (0x8E analogs 500-999) so
        /// the SIMPL room module does not see them drop to 0 and switch the TV and receiver off.
        /// Only for an output whose saved TV source - or, for a receiver playing music, music
        /// source - is still the same source; anything else stays 0 (off), as the TV will.
        /// </summary>
        private void RestoreRoomInputs()
        {
            try
            {
                string note;
                JObject state = UsableState(out note);
                JObject outs = state != null ? state["roomInputs"] as JObject : null;
                if (outs == null || _cs.videoEISC1 == null) { return; }
                var manager = _cs.manager;

                // Outputs whose saved activity is still valid.
                var ok = new HashSet<ushort>();
                JObject displays = state["displays"] as JObject;
                if (displays != null)
                {
                    foreach (var p in displays.Properties())
                    {
                        ushort dispNum;
                        if (!ushort.TryParse(p.Name, out dispNum) || !manager.VideoDisplayZ.ContainsKey(dispNum)) { continue; }
                        ushort src = (ushort?)p.Value["src"] ?? 0;
                        if (src > 0 && VideoSourceMismatch(src, (string)p.Value["name"], (string)p.Value["stream"]) == null)
                        {
                            ok.Add(manager.VideoDisplayZ[dispNum].VideoOutputNum);
                        }
                    }
                }
                JObject rooms = state["rooms"] as JObject;
                if (rooms != null)
                {
                    foreach (var p in rooms.Properties())
                    {
                        ushort roomNum;
                        if (!ushort.TryParse(p.Name, out roomNum) || !manager.RoomZ.ContainsKey(roomNum)) { continue; }
                        ushort music = (ushort?)p.Value["music"] ?? 0;
                        string savedName = (string)p.Value["musicName"];
                        if (music > 0 && manager.MusicSourceZ.ContainsKey(music)
                            && (savedName == null || savedName == manager.MusicSourceZ[music].Name))
                        {
                            ok.Add(manager.RoomZ[roomNum].VideoOutputNum);
                        }
                    }
                }

                int sent = 0, held = 0;
                foreach (var p in outs.Properties())
                {
                    ushort outNum;
                    if (!ushort.TryParse(p.Name, out outNum) || outNum == 0 || outNum > MaxZone) { continue; }
                    if (!ok.Contains(outNum)) { held++; continue; }
                    foreach (ushort b in RoomInputBases)
                    {
                        ushort? v = (ushort?)p.Value["a" + b];
                        if (v.HasValue) { _cs.videoEISC1.UShortInput[(ushort)(b + outNum)].UShortValue = v.Value; sent++; }
                    }
                }
                string line = string.Format("ROOM INPUTS re-sent to SIMPL: {0} analog(s) on 0x8E{1}", sent,
                    held > 0 ? ", " + held + " output(s) left off (source changed or gone)" : "");
                CrestronConsole.PrintLine("[AVSTATE] {0}", line);
                Log(line);
            }
            catch (Exception ex)
            {
                ErrorLog.Error("[AVSTATE] room input restore failed: {0}", ex.Message);
            }
        }

        // Non-zero TV / receiver / switcher inputs, per video output.
        private JObject RoomInputSnapshot()
        {
            var o = new JObject();
            if (_cs.videoEISC1 == null) { return o; }
            for (ushort outNum = 1; outNum <= MaxZone; outNum++)
            {
                JObject e = null;
                foreach (ushort b in RoomInputBases)
                {
                    ushort v = _cs.videoEISC1.UShortInput[(ushort)(b + outNum)].UShortValue;
                    if (v == 0) { continue; }
                    if (e == null) { e = new JObject(); }
                    e["a" + b] = v;
                }
                if (e != null) { o[outNum.ToString()] = e; }
            }
            return o;
        }

        /// <summary>
        /// Put each room's volume owner back and re-send the saved per-panel joins (0x91 digitals
        /// 1-200, 0x8E analogs 301-400). Call right after the EISCs are registered, before the
        /// panels are started (the resolver they use reads LastSystemVid).
        /// </summary>
        public void RestorePanels()
        {
            try
            {
                string note;
                JObject state = UsableState(out note);
                if (state == null) { return; }
                var manager = _cs.manager;

                // Volume owner per room: only "audio" is saved, video is the startup default.
                JArray audioRooms = state["audioVolumeRooms"] as JArray;
                if (audioRooms != null)
                {
                    foreach (var t in audioRooms)
                    {
                        ushort r = (ushort?)t ?? 0;
                        if (manager.RoomZ.ContainsKey(r)) { manager.RoomZ[r].LastSystemVid = false; }
                    }
                }

                JObject panels = state["panels"] as JObject;
                if (panels == null)
                {
                    CrestronConsole.PrintLine("[AVSTATE] panel joins not restored - none saved");
                    return;
                }
                int joins = WritePanelJoins(panels);
                string line = string.Format("PANELS re-sent to SIMPL: {0} join(s) on 0x91/0x8E", joins);
                CrestronConsole.PrintLine("[AVSTATE] {0}", line);
                Log(line);
            }
            catch (Exception ex)
            {
                ErrorLog.Error("[AVSTATE] panel restore failed: {0}", ex.Message);
            }
        }

        // Saved flags and equip IDs onto the EISCs. An equip ID that no video source has any more
        // (config changed) is sent as 0 rather than binding the panel to the wrong device.
        private int WritePanelJoins(JObject panels)
        {
            if (panels == null || _cs.imageEISC == null || _cs.videoEISC1 == null) { return 0; }
            var manager = _cs.manager;
            var equipIds = new HashSet<ushort>(manager.VideoSourceZ.Values.Select(v => v.EquipID));
            int n = 0;
            foreach (var p in panels.Properties())
            {
                ushort tpNum;
                if (!ushort.TryParse(p.Name, out tpNum) || tpNum == 0 || tpNum > 100 || !manager.touchpanelZ.ContainsKey(tpNum)) { continue; }
                bool vid = (bool?)p.Value["vid"] ?? false;
                bool aud = (bool?)p.Value["aud"] ?? false;
                ushort equip = (ushort?)p.Value["equip"] ?? 0;
                if (equip != 0 && !equipIds.Contains(equip)) { equip = 0; }
                _cs.imageEISC.BooleanInput[tpNum].BoolValue = vid;
                _cs.imageEISC.BooleanInput[(ushort)(tpNum + 100)].BoolValue = aud;
                _cs.videoEISC1.UShortInput[(ushort)(tpNum + 300)].UShortValue = equip;
                n += 3;
            }
            return n;
        }

        // Every panel not at its defaults (both flags low, no equip ID).
        private JObject PanelSnapshot()
        {
            var o = new JObject();
            var manager = _cs.manager;
            if (_cs.imageEISC == null || _cs.videoEISC1 == null) { return o; }
            foreach (var kv in manager.touchpanelZ)
            {
                ushort tpNum = kv.Key;
                if (tpNum == 0 || tpNum > 100) { continue; }
                bool vid = _cs.imageEISC.BooleanInput[tpNum].BoolValue;
                bool aud = _cs.imageEISC.BooleanInput[(ushort)(tpNum + 100)].BoolValue;
                ushort equip = _cs.videoEISC1.UShortInput[(ushort)(tpNum + 300)].UShortValue;
                if (!vid && !aud && equip == 0) { continue; }
                o[tpNum.ToString()] = new JObject { ["vid"] = vid, ["aud"] = aud, ["equip"] = equip };
            }
            return o;
        }

        private JArray AudioVolumeRooms()
        {
            return new JArray(_cs.manager.RoomZ.Where(kv => !kv.Value.LastSystemVid).Select(kv => (int)kv.Key));
        }

        private static int WriteAnalogs(Crestron.SimplSharpPro.EthernetCommunication.ThreeSeriesTcpIpEthernetIntersystemCommunications eisc, JObject values, ushort baseJoin)
        {
            if (eisc == null || values == null) { return 0; }
            int n = 0;
            foreach (var p in values.Properties())
            {
                ushort offset;
                ushort? v = (ushort?)p.Value;
                if (!ushort.TryParse(p.Name, out offset) || offset == 0 || offset > MaxZone || !v.HasValue) { continue; }
                eisc.UShortInput[(ushort)(baseJoin + offset)].UShortValue = v.Value;
                n++;
            }
            return n;
        }

        private static int WriteSerials(Crestron.SimplSharpPro.EthernetCommunication.ThreeSeriesTcpIpEthernetIntersystemCommunications eisc, JObject values, ushort baseJoin)
        {
            if (eisc == null || values == null) { return 0; }
            int n = 0;
            foreach (var p in values.Properties())
            {
                ushort offset;
                string v = (string)p.Value;
                if (!ushort.TryParse(p.Name, out offset) || offset == 0 || offset > MaxZone || v == null) { continue; }
                eisc.StringInput[(ushort)(baseJoin + offset)].StringValue = v;
                n++;
            }
            return n;
        }

        /// <summary>Read avState.json and put every display and room back. Call once, after
        /// StartupRooms. Always ends with Ready = true, whatever happens.</summary>
        public void Restore()
        {
            int tvs = 0, zones = 0, skipped = 0;
            bool needsSave = false;
            string note = "";
            try
            {
                AttachDecoders();

                JObject state = UsableState(out note);
                if (state == null) { return; }

                var manager = _cs.manager;

                // Rooms' current display first: the setter rebinds the room's status text to that
                // display, which is what the display restore below then drives.
                JObject rooms = state["rooms"] as JObject;
                if (rooms != null)
                {
                    foreach (var p in rooms.Properties())
                    {
                        ushort roomNum;
                        if (!ushort.TryParse(p.Name, out roomNum) || !manager.RoomZ.ContainsKey(roomNum)) { continue; }
                        ushort disp = (ushort?)p.Value["display"] ?? 0;
                        if (disp > 0 && manager.VideoDisplayZ.ContainsKey(disp)
                            && manager.VideoDisplayZ[disp].AssignedToRoomNum == roomNum)
                        {
                            manager.RoomZ[roomNum].CurrentDisplayNumber = disp;
                        }
                    }
                }

                JObject displays = state["displays"] as JObject;
                if (displays != null)
                {
                    foreach (var p in displays.Properties())
                    {
                        ushort dispNum;
                        if (!ushort.TryParse(p.Name, out dispNum) || !manager.VideoDisplayZ.ContainsKey(dispNum)) { continue; }
                        ushort src = (ushort?)p.Value["src"] ?? 0;
                        if (src == 0) { continue; }

                        // Same source, or leave the TV off. A restart is often a config change, and
                        // a number that now means a different source must never be put back.
                        string why = VideoSourceMismatch(src, (string)p.Value["name"], (string)p.Value["stream"]);
                        if (why != null)
                        {
                            skipped++;
                            Log(string.Format("NOT RESTORED {0} | {1} - left off", DisplayLabel(dispNum), why));
                            continue;
                        }

                        var display = manager.VideoDisplayZ[dispNum];
                        display.CurrentVideoSrc = src;
                        display.CurrentSourceText = manager.VideoSourceZ[src].DisplayName;
                        RoomConfigFor(display.AssignedToRoomNum, r =>
                        {
                            if (r.CurrentDisplayNumber == dispNum) { r.UpdateVideoSrcStatus(src); }
                        });
                        lock (_lock) { _displaySince[dispNum] = ParseSince(p.Value["since"]); }
                        tvs++;
                        Log(string.Format("RESTORED   {0} | {1} (on since {2})", DisplayLabel(dispNum),
                            VideoName(src), FormatSince(_displaySince[dispNum])));
                        PresetDecoder(display.VideoOutputNum, src);
                    }
                }

                if (rooms != null)
                {
                    foreach (var p in rooms.Properties())
                    {
                        ushort roomNum;
                        if (!ushort.TryParse(p.Name, out roomNum) || !manager.RoomZ.ContainsKey(roomNum)) { continue; }
                        ushort music = (ushort?)p.Value["music"] ?? 0;
                        if (music == 0) { continue; }
                        string savedName = (string)p.Value["musicName"];
                        if (!manager.MusicSourceZ.ContainsKey(music)
                            || (savedName != null && savedName != manager.MusicSourceZ[music].Name))
                        {
                            skipped++;
                            Log(string.Format("NOT RESTORED {0} | music source {1} ({2}) {3} - left off", RoomName(roomNum), music,
                                savedName ?? "?", manager.MusicSourceZ.ContainsKey(music)
                                    ? "is now \"" + manager.MusicSourceZ[music].Name + "\"" : "no longer exists"));
                            continue;
                        }

                        manager.RoomZ[roomNum].UpdateMusicSrcStatus(music);
                        manager.MusicSourceZ[music].InUse = true;
                        lock (_lock) { _musicSince[roomNum] = ParseSince(p.Value["musicSince"]); }
                        zones++;
                        Log(string.Format("RESTORED   {0} | music {1} (since {2})", RoomName(roomNum),
                            MusicName(music), FormatSince(_musicSince[roomNum])));
                    }
                }

                // In-use lamps. Safe at startup: it only ever CLEARS EISC flags, which are clear.
                if (tvs > 0 && _cs.videoSystemControl != null) { _cs.videoSystemControl.RecalculateVideoSourceInUse(); }

                // The panels were started while every source still read off. Restoring the sources
                // above already pushed each room's equip ID / source name / button to its panels;
                // redo the video/audio flags now that the resolver sees the restored state.
                _cs.RefreshAllVolumeSubsystemFlags();

                // Rewrite the file now, in the current format (source identity + NAX routing),
                // rather than waiting for someone to press something.
                needsSave = true;
            }
            catch (Exception ex)
            {
                note = "restore failed: " + ex.Message;
                ErrorLog.Error("[AVSTATE] {0}", note);
            }
            finally
            {
                Ready = true;
                string line = string.Format("PROGRAM START - restored {0} TV(s), {1} music zone(s){2} - {3}",
                    tvs, zones, skipped > 0 ? ", " + skipped + " left off (source changed)" : "", note);
                CrestronConsole.PrintLine("[AVSTATE] {0}", line);
                Log(line);

                // Decoders already online got their (empty) setting before we knew what they
                // should show; check them now. Ones still connecting are caught by OnDecoderOnline.
                foreach (var kv in _cs.manager.dmDestinationZ)
                {
                    if (kv.Value != null && kv.Value.IsOnline) { ScheduleDecoderCheck(kv.Value, "after restore"); }
                }

                lock (_lock)
                {
                    _lastNaxSignature = NaxSignature();
                    if (_naxCheckTimer == null)
                    {
                        _naxCheckTimer = new CTimer(_ => CheckNaxChanged(), null, NaxCheckMs, NaxCheckMs);
                    }
                }
                if (needsSave) { ScheduleSave(); }
            }
        }

        /// <summary>
        /// Give a restored TV's decoder its stream NOW. A decoder takes this program's value for it
        /// when it connects, so if it has not connected yet it will come up on the right stream with
        /// no gap at all; if it already connected (and was handed an empty stream) this fixes it at
        /// once. What the decoder reported beforehand is logged — that is the evidence for whether a
        /// restart really does blank the stream.
        /// </summary>
        private void PresetDecoder(ushort videoOutputNum, ushort src)
        {
            try
            {
                var rx = _cs.manager.dmDestinationZ.Values.FirstOrDefault(r => r != null && r.DmOutputNumber == videoOutputNum);
                if (rx == null) { return; }   // not an NVX display (local sources, AVR, projector)
                string wanted = _cs.manager.VideoSourceZ[src].StreamLocation;
                if (string.IsNullOrEmpty(wanted)) { return; }
                string before = rx.IsOnline ? (rx.CurrentStreamFeedback() ?? "n/a") : "not connected yet";
                rx.SetStreamLocation(wanted);
                Log(string.Format("NVX {0} at restore: decoder reported \"{1}\", stream set to {2} ({3})",
                    rx.Name, before, VideoName(src), wanted));
            }
            catch (Exception ex)
            {
                ErrorLog.Error("[AVSTATE] decoder preset for output {0} failed: {1}", videoOutputNum, ex.Message);
            }
        }

        /// <summary>Null if saved source `src` is still the same source, else why not.</summary>
        private string VideoSourceMismatch(ushort src, string savedName, string savedStream)
        {
            var m = _cs.manager;
            if (!m.VideoSourceZ.ContainsKey(src))
            {
                return string.Format("video source {0} ({1}) no longer exists", src, savedName ?? "?");
            }
            var now = m.VideoSourceZ[src];
            if (savedName != null && savedName != now.DisplayName)
            {
                return string.Format("video source {0} was \"{1}\", is now \"{2}\"", src, savedName, now.DisplayName);
            }
            if (savedStream != null && savedStream != (now.StreamLocation ?? ""))
            {
                return string.Format("video source {0} \"{1}\" stream changed ({2} -> {3})", src, now.DisplayName, savedStream, now.StreamLocation);
            }
            return null;
        }

        // ─── NVX decoders ──────────────────────────────────────────────────

        private void AttachDecoders()
        {
            foreach (var kv in _cs.manager.dmDestinationZ)
            {
                if (kv.Value == null) { continue; }
                kv.Value.OnlineChanged -= OnDecoderOnlineChanged;   // a reload re-runs this
                kv.Value.OnlineChanged += OnDecoderOnlineChanged;
            }
        }

        private void OnDecoderOnlineChanged(DmReceiver.DmNVXreceiver rx, bool online)
        {
            if (!online)
            {
                if (Ready) { Log(string.Format("NVX OFFLINE {0}", rx.Name)); }
                return;
            }
            if (Ready) { ScheduleDecoderCheck(rx, "came online"); }
        }

        // Wait for the decoder to report its stream, then put it right if its TV is on and it
        // disagrees. Re-sending only on disagreement means a decoder that kept its stream is not
        // touched at all.
        private void ScheduleDecoderCheck(DmReceiver.DmNVXreceiver rx, string why)
        {
            CTimer t = null;
            t = new CTimer(_ =>
            {
                try { CheckDecoder(rx, why); }
                catch (Exception ex) { ErrorLog.Error("[AVSTATE] decoder check {0} failed: {1}", rx.Name, ex.Message); }
                finally
                {
                    lock (_lock) { _pendingDecoderChecks.Remove(t); }
                    if (t != null) { t.Dispose(); }
                }
            }, DecoderSettleMs);
            lock (_lock) { _pendingDecoderChecks.Add(t); }
        }

        private void CheckDecoder(DmReceiver.DmNVXreceiver rx, string why)
        {
            if (!Ready || !rx.IsOnline) { return; }
            var m = _cs.manager;
            var display = m.VideoDisplayZ.Values.FirstOrDefault(d => d.VideoOutputNum == rx.DmOutputNumber && d.CurrentVideoSrc > 0);
            string reported = rx.CurrentStreamFeedback();
            if (display == null)
            {
                Log(string.Format("NVX {0} {1}: TV off, nothing to send (decoder reports \"{2}\")", rx.Name, why, reported ?? "n/a"));
                return;
            }
            ushort src = display.CurrentVideoSrc;
            if (!m.VideoSourceZ.ContainsKey(src)) { return; }
            string wanted = m.VideoSourceZ[src].StreamLocation;
            if (string.IsNullOrEmpty(wanted))
            {
                Log(string.Format("NVX {0} {1}: {2} has no stream address (local source?) - nothing sent", rx.Name, why, VideoName(src)));
                return;
            }
            if (reported != null && string.Equals(reported, wanted, StringComparison.OrdinalIgnoreCase))
            {
                Log(string.Format("NVX {0} {1}: still on {2} ({3}) - left alone", rx.Name, why, VideoName(src), wanted));
                return;
            }
            rx.SetStreamLocation(wanted);
            Log(string.Format("NVX {0} {1}: decoder reported \"{2}\", re-sent {3} ({4})", rx.Name, why, reported ?? "n/a", VideoName(src), wanted));
        }

        // ─── NAX routing snapshot ──────────────────────────────────────────

        private JObject NaxSnapshot()
        {
            var nax = new JObject();
            nax["a500"] = ReadAnalogs(_cs.musicEISC1, 500);
            nax["a600"] = ReadAnalogs(_cs.musicEISC1, 600);
            nax["s300"] = ReadSerials(_cs.musicEISC3, 300, "0.0.0.0");
            nax["s500"] = ReadSerials(_cs.musicEISC3, 500, null);
            return nax;
        }

        private string NaxSignature()
        {
            try
            {
                return NaxSnapshot().ToString(Formatting.None) + RoomInputSnapshot().ToString(Formatting.None)
                    + PanelSnapshot().ToString(Formatting.None)
                    + AudioVolumeRooms().ToString(Formatting.None);
            }
            catch { return ""; }
        }

        // Only what the program actually set: 0 / empty are the restart defaults anyway.
        private static JObject ReadAnalogs(Crestron.SimplSharpPro.EthernetCommunication.ThreeSeriesTcpIpEthernetIntersystemCommunications eisc, ushort baseJoin)
        {
            var o = new JObject();
            if (eisc == null) { return o; }
            for (ushort z = 1; z <= MaxZone; z++)
            {
                ushort v = eisc.UShortInput[(ushort)(baseJoin + z)].UShortValue;
                if (v != 0) { o[z.ToString()] = v; }
            }
            return o;
        }

        private static JObject ReadSerials(Crestron.SimplSharpPro.EthernetCommunication.ThreeSeriesTcpIpEthernetIntersystemCommunications eisc, ushort baseJoin, string alsoDefault)
        {
            var o = new JObject();
            if (eisc == null) { return o; }
            for (ushort z = 1; z <= MaxZone; z++)
            {
                string v = eisc.StringInput[(ushort)(baseJoin + z)].StringValue;
                if (!string.IsNullOrEmpty(v) && v != alsoDefault) { o[z.ToString()] = v; }
            }
            return o;
        }

        // NAX routing and the panel joins are written from a dozen places, not just the source
        // setters the save hooks watch (grouping, sharing, floor off, subsystem and room changes).
        // Every NaxCheckMs, compare and save if anything moved.
        private void CheckNaxChanged()
        {
            if (!Ready) { return; }
            string sig = NaxSignature();
            bool changed;
            lock (_lock) { changed = sig != _lastNaxSignature; }
            if (changed) { ScheduleSave(); }
        }

        private void RoomConfigFor(ushort roomNum, Action<Room.RoomConfig> act)
        {
            Room.RoomConfig r;
            if (_cs.manager.RoomZ.TryGetValue(roomNum, out r) && r != null) { act(r); }
        }

        // ─── save ──────────────────────────────────────────────────────────

        private void ScheduleSave()
        {
            lock (_lock)
            {
                if (_saveTimer != null) { return; }   // one write covers the whole burst
                _saveTimer = new CTimer(_ =>
                {
                    lock (_lock)
                    {
                        if (_saveTimer != null) { _saveTimer.Dispose(); _saveTimer = null; }
                    }
                    SaveNow();
                }, SaveDebounceMs);
            }
        }

        private void SaveNow()
        {
            try
            {
                var manager = _cs.manager;
                var displays = new JObject();
                var rooms = new JObject();
                lock (_lock)
                {
                    foreach (var kv in manager.VideoDisplayZ)
                    {
                        if (kv.Value.CurrentVideoSrc == 0) { continue; }
                        DateTime since;
                        _displaySince.TryGetValue(kv.Key, out since);
                        ushort src = kv.Value.CurrentVideoSrc;
                        bool known = manager.VideoSourceZ.ContainsKey(src);
                        displays[kv.Key.ToString()] = new JObject
                        {
                            ["src"] = src,
                            // Identity, checked at restore: same number but a different source
                            // (config edited) must not be put back.
                            ["name"] = known ? manager.VideoSourceZ[src].DisplayName : null,
                            ["stream"] = known ? (manager.VideoSourceZ[src].StreamLocation ?? "") : null,
                            ["since"] = since == DateTime.MinValue ? null : since.ToString("o")
                        };
                    }
                    foreach (var kv in manager.RoomZ)
                    {
                        var r = kv.Value;
                        if (r.CurrentDisplayNumber == 0 && r.CurrentMusicSrc == 0) { continue; }
                        DateTime since;
                        _musicSince.TryGetValue(kv.Key, out since);
                        rooms[kv.Key.ToString()] = new JObject
                        {
                            ["display"] = r.CurrentDisplayNumber,
                            ["music"] = r.CurrentMusicSrc,
                            ["musicName"] = manager.MusicSourceZ.ContainsKey(r.CurrentMusicSrc) ? manager.MusicSourceZ[r.CurrentMusicSrc].Name : null,
                            ["musicSince"] = (r.CurrentMusicSrc == 0 || since == DateTime.MinValue) ? null : since.ToString("o")
                        };
                    }
                }
                JObject nax = NaxSnapshot();
                var state = new JObject
                {
                    ["saved"] = DateTime.Now.ToString("o"),
                    ["displays"] = displays,
                    ["rooms"] = rooms,
                    ["nax"] = nax,
                    ["roomInputs"] = RoomInputSnapshot(),
                    ["panels"] = PanelSnapshot(),
                    ["audioVolumeRooms"] = AudioVolumeRooms()
                };
                lock (_lock) { _lastNaxSignature = NaxSignature(); }

                using (var fs = new FileStream(StateTmpPath, FileMode.Create))
                using (var w = new StreamWriter(fs, new UTF8Encoding(false)))
                {
                    w.Write(state.ToString(Formatting.Indented));
                }
                if (File.Exists(StatePath)) { File.Delete(StatePath); }
                File.Move(StateTmpPath, StatePath);
            }
            catch (Exception ex)
            {
                ErrorLog.Error("[AVSTATE] save failed: {0}", ex.Message);
            }
        }

        private JObject ReadState()
        {
            // A save that died between delete and move leaves only the temp file — still good.
            string path = File.Exists(StatePath) ? StatePath : (File.Exists(StateTmpPath) ? StateTmpPath : null);
            if (path == null) { return null; }
            using (var r = new StreamReader(path)) { return JObject.Parse(r.ReadToEnd()); }
        }

        // ─── console ───────────────────────────────────────────────────────

        /// <summary>`avstate`: what the program currently believes, and what is saved.</summary>
        public void PrintState()
        {
            var manager = _cs.manager;
            CrestronConsole.PrintLine("[AVSTATE] ready={0}  file={1}", Ready, StatePath);
            foreach (var kv in manager.VideoDisplayZ)
            {
                if (kv.Value.CurrentVideoSrc == 0) { continue; }
                DateTime since;
                lock (_lock) { _displaySince.TryGetValue(kv.Key, out since); }
                CrestronConsole.PrintLine("  TV    {0} | {1} since {2}", DisplayLabel(kv.Key), VideoName(kv.Value.CurrentVideoSrc), FormatSince(since));
            }
            foreach (var kv in manager.RoomZ)
            {
                if (kv.Value.CurrentMusicSrc == 0) { continue; }
                DateTime since;
                lock (_lock) { _musicSince.TryGetValue(kv.Key, out since); }
                CrestronConsole.PrintLine("  MUSIC {0} | {1} since {2}", RoomName(kv.Key), MusicName(kv.Value.CurrentMusicSrc), FormatSince(since));
            }
            try
            {
                JObject saved = ReadState();
                CrestronConsole.PrintLine("  file saved at: {0}", saved == null ? "(no file)" : (string)saved["saved"] ?? "?");
            }
            catch (Exception ex) { CrestronConsole.PrintLine("  file unreadable: {0}", ex.Message); }
        }

        /// <summary>`usagelog [lines]`: tail of today's usage file.</summary>
        public void PrintUsageTail(int lines)
        {
            string path = UsagePath(DateTime.Now);
            try
            {
                if (!File.Exists(path))
                {
                    CrestronConsole.PrintLine("[USAGE] nothing logged today yet ({0})", path);
                    return;
                }
                string all;
                using (var r = new StreamReader(path)) { all = r.ReadToEnd(); }
                string[] split = all.Split('\n');
                int start = Math.Max(0, split.Length - lines - 1);
                for (int i = start; i < split.Length; i++)
                {
                    if (split[i].Length > 0) { CrestronConsole.PrintLine(split[i].TrimEnd('\r')); }
                }
                CrestronConsole.PrintLine("[USAGE] ({0})", path);
            }
            catch (Exception ex)
            {
                CrestronConsole.PrintLine("[USAGE] can't read {0}: {1}", path, ex.Message);
            }
        }

        // ─── usage file ────────────────────────────────────────────────────

        private string UsagePath(DateTime d)
        {
            return string.Format("{0}/usage-{1:yyyyMMdd}.log", _usageDir, d);
        }

        private void Log(string line)
        {
            DateTime now = DateTime.Now;
            lock (_lock)
            {
                try
                {
                    string day = now.ToString("yyyyMMdd");
                    if (day != _usageDay)
                    {
                        if (!Directory.Exists(_usageDir)) { Directory.CreateDirectory(_usageDir); }
                        _usageDay = day;
                        PruneUsage(now);
                    }
                    using (var fs = new FileStream(UsagePath(now), FileMode.Append))
                    using (var w = new StreamWriter(fs, new UTF8Encoding(false)))
                    {
                        w.Write(now.ToString("yyyy-MM-dd HH:mm:ss "));
                        w.Write(line);
                        w.Write("\n");
                    }
                }
                catch (Exception ex)
                {
                    ErrorLog.Error("[USAGE] can't write usage log: {0}", ex.Message);
                }
            }
        }

        // Up to MaxDays of files; past MaxTotalBytes, drop oldest days, but never below MinDays.
        // File names sort by date (usage-yyyyMMdd.log), so name order is age order.
        private void PruneUsage(DateTime now)
        {
            try
            {
                var files = Directory.GetFiles(_usageDir, "usage-*.log").OrderBy(f => f).ToList();
                DateTime minKeep = now.Date.AddDays(-(MinDays - 1));
                DateTime maxKeep = now.Date.AddDays(-(MaxDays - 1));

                var remaining = new List<string>();
                foreach (string f in files)
                {
                    DateTime d;
                    if (FileDay(f, out d) && d < maxKeep) { File.Delete(f); }
                    else { remaining.Add(f); }
                }

                long total = remaining.Sum(f => new FileInfo(f).Length);
                foreach (string f in remaining)
                {
                    if (total <= MaxTotalBytes) { break; }
                    DateTime d;
                    if (!FileDay(f, out d) || d >= minKeep) { break; }
                    total -= new FileInfo(f).Length;
                    File.Delete(f);
                }
            }
            catch (Exception ex)
            {
                ErrorLog.Error("[USAGE] can't prune old usage files: {0}", ex.Message);
            }
        }

        private static bool FileDay(string path, out DateTime day)
        {
            string name = Path.GetFileNameWithoutExtension(path);   // usage-yyyyMMdd
            day = DateTime.MinValue;
            return name.Length == 14 && DateTime.TryParseExact(name.Substring(6), "yyyyMMdd",
                System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out day);
        }

        // ─── names ─────────────────────────────────────────────────────────

        private string DisplayLabel(ushort displayNumber)
        {
            var m = _cs.manager;
            if (!m.VideoDisplayZ.ContainsKey(displayNumber)) { return "display " + displayNumber; }
            var d = m.VideoDisplayZ[displayNumber];
            return RoomName(d.AssignedToRoomNum) + " | " + d.DisplayName;
        }

        private string RoomName(ushort roomNumber)
        {
            Room.RoomConfig r;
            return _cs.manager.RoomZ.TryGetValue(roomNumber, out r) && r != null ? r.Name : "room " + roomNumber;
        }

        private string VideoName(ushort src)
        {
            return _cs.manager.VideoSourceZ.ContainsKey(src) ? _cs.manager.VideoSourceZ[src].DisplayName : "source " + src;
        }

        private string MusicName(ushort src)
        {
            return _cs.manager.MusicSourceZ.ContainsKey(src) ? _cs.manager.MusicSourceZ[src].Name : "source " + src;
        }

        private static string For(DateTime since, DateTime now)
        {
            if (since == DateTime.MinValue) { return ""; }
            TimeSpan t = now - since;
            return t.TotalDays >= 1
                ? string.Format(" for {0}d{1}h{2:00}m", (int)t.TotalDays, t.Hours, t.Minutes)
                : string.Format(" for {0}h{1:00}m", t.Hours, t.Minutes);
        }

        private static DateTime ParseSince(JToken t)
        {
            DateTime d;
            return (t != null && DateTime.TryParse((string)t, out d)) ? d : DateTime.MinValue;
        }

        private static string FormatSince(DateTime d)
        {
            return d == DateTime.MinValue ? "?" : d.ToString("MM-dd HH:mm");
        }
    }
}
