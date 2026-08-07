using System;
using System.Collections.Generic;
using System.Text;
using Crestron.SimplSharp;
using Crestron.SimplSharp.CrestronIO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ACS_4Series_Template_V3.Intercom
{
    /// <summary>
    /// Intercom subsystem for HTML panels — a SIP door-station page driven by the
    /// panel's own VOIP reserved joins (see UI/TouchpanelUI.Voip.cs for why that
    /// access is reflection-based).
    ///
    /// Joins (all raw, HTML-reserved 1500+ — no contract/cse2j involvement, same
    /// style as the page descriptor 1520, quick actions 1530-1534, cameras 1540-1554):
    ///
    ///   1560 (serial,  C#→HTML): state  { "state":"incoming", "name":"Front Door",
    ///                                     "num":"1001", "dnd":0, "mute":0,
    ///                                     "reg":1, "voip":1 }
    ///   1561 (serial,  HTML→C#): command { "seq":&lt;n&gt;, "cmd":"answer|reject|hangup|dnd|mute|pageall" }
    ///   1562 (serial,  C#→HTML): video url for the active/incoming call → ch5-video
    ///   1564 (analog,  HTML→C#): panel speaker volume set
    ///   1565 (analog,  C#→HTML): panel speaker volume feedback
    ///
    /// The video url is deliberately on its OWN join and not folded into 1560: urls
    /// are long, 1560 is always-on, and long always-on serial joins get TRUNCATED on
    /// hardware and mobile panels (xpanel does not, which is how that bites you late).
    /// Same reason the quick-actions descriptor is kept lean.
    ///
    /// VIDEO SOURCE — the configured RTSP url, and nothing else.
    ///   \NVRAM\intercomConfig.json, matched to the caller by SIP number, then by
    ///   display name, then the single configured station of that scenario.
    ///   Requires the 2N Enhanced Video licence (its RTSP server), and H.264 — panels
    ///   will not decode H.265.
    ///
    /// This was originally the FALLBACK behind VOIPVideoURLFeedback, which would have
    /// needed no licence. That member does not exist on this hardware — dumping the live
    /// Tss752VoipReservedSigs on a TST-1080 found no video-url member at all — so the
    /// config path is the whole story. The panel is still read once per call and logs
    /// loudly if it ever does return a url; see ApplyVideoUrl.
    ///
    /// Test the video window WITHOUT a doorbell press: `intercomvideo <tp>` pushes the
    /// resolved url and forces the page. That is the loop to use while iterating.
    ///
    /// Page-flip: the "Intercom" subsystem name routes through the normal descriptor
    /// (SubsystemPageKey("Intercom") → "intercom") when the user taps the home-page
    /// button. An INCOMING CALL additionally FORCES the page via
    /// TouchpanelUI.ShowIntercomPage(), whatever the panel was showing.
    /// </summary>
    public class IntercomManager
    {
        private readonly ControlSystem _parent;

        private const string ConfigFilePath = @"\NVRAM\intercomConfig.json";

        public const ushort StateJoin = 1560;    // serial C#→HTML
        public const ushort CommandJoin = 1561;  // serial HTML→C#
        public const ushort VideoUrlJoin = 1562; // serial C#→HTML
        public const ushort VolumeSetJoin = 1564; // analog HTML→C#
        public const ushort VolumeFbJoin = 1565;  // analog C#→HTML

        // ch5-video's own diagnostics for the door-station stream, reported by the
        // panel's decoder (HTML→C#). Same set the Cameras page has carried from the
        // start, on its own joins so a console line names the page it came from.
        //
        // ⚠ These are SEND joins: HTML writes them and cannot read them back, so the
        // page itself can never know why its video failed. Everything anyone will want
        // to know when the door video is black — bad codec, rtsps url, exhausted RTSP
        // sessions, wrong resolution — arrives ONLY here.
        public const ushort VideoStateJoin = 1566;        // analog
        public const ushort VideoErrorCodeJoin = 1567;    // analog
        public const ushort VideoErrorMessageJoin = 1568; // serial
        public const ushort VideoResolutionJoin = 1569;   // serial
        public const ushort VideoRetryCountJoin = 1570;   // analog

        public static bool IsVideoDiagAnalogJoin(uint join)
        {
            return join == VideoStateJoin || join == VideoErrorCodeJoin || join == VideoRetryCountJoin;
        }

        public static bool IsVideoDiagSerialJoin(uint join)
        {
            return join == VideoErrorMessageJoin || join == VideoResolutionJoin;
        }

        private static string Ts()
        {
            return DateTime.Now.ToString("HH:mm:ss.fff");
        }

        /// <summary>
        /// Per-event raw VOIP sig logging. **ON by default.**
        ///
        /// ⚠ DO NOT DEFAULT THIS OFF while anything in this area is still being debugged. It was
        /// switched off on 2026-08-06 purely because it was making the console noisy, which was
        /// not a good enough reason: the owner had only asked for the *extender member dump* to be
        /// suppressed, and losing this at the same time removed evidence in the middle of an
        /// active investigation. Noise is recoverable; a missing log line costs a whole test cycle
        /// on real hardware.
        ///
        /// Turn it off per-session with `intercomraw off` when the console needs to be quiet.
        /// </summary>
        public static bool RawSigLogging = true;

        // ─── Config ─────────────────────────────────────────────────────────

        public class StationEntry
        {
            /// <summary>Display name. Matched (case-insensitive, contains) against the
            /// panel's IncomingDisplayNameFeedback when the SIP number does not match.</summary>
            [JsonProperty("name")]
            public string Name { get; set; }

            /// <summary>SIP extension/ID this station calls from. Primary match key.</summary>
            [JsonProperty("sipNumber")]
            public string SipNumber { get; set; }

            /// <summary>Fallback RTSP url, used only when the panel reports no video url.</summary>
            [JsonProperty("rtspUrl")]
            public string RtspUrl { get; set; }

            /// <summary>
            /// Door station kind, which selects the INTERCOM SCENARIO (page layout):
            ///   "sip"   → scenario 1. A SIP/Rava station (2N Verso). Full call control via
            ///             the panel's VOIP extender.
            ///   "unifi" → scenario 2. A view-only station (UniFi G6 Entry): RTSP video with
            ///             audio, no SIP session, so no answer/reject/hangup/mic.
            /// Defaults to "sip" so existing config keeps working unchanged.
            /// </summary>
            [JsonProperty("type")]
            public string Type { get; set; }

            /// <summary>The intercom scenario this station's layout maps to.</summary>
            public ushort Scenario
            {
                get
                {
                    return (Type != null && Type.Trim().Equals("unifi", StringComparison.OrdinalIgnoreCase))
                        ? (ushort)2
                        : (ushort)1;
                }
            }
        }

        private class IntercomFile
        {
            [JsonProperty("stations")]
            public List<StationEntry> Stations { get; set; }
        }

        private readonly object stationsLock = new object();
        private List<StationEntry> stations = new List<StationEntry>();

        public IntercomManager(ControlSystem parent)
        {
            _parent = parent;
        }

        public void Load()
        {
            try
            {
                if (File.Exists(ConfigFilePath))
                {
                    string json = File.ReadToEnd(ConfigFilePath, Encoding.UTF8);
                    var loaded = JsonConvert.DeserializeObject<IntercomFile>(json);
                    lock (stationsLock)
                    {
                        stations = (loaded != null && loaded.Stations != null)
                            ? loaded.Stations
                            : new List<StationEntry>();
                    }
                    CrestronConsole.PrintLine("Intercom: loaded {0} station(s) from {1}", stations.Count, ConfigFilePath);
                }
                else
                {
                    // This file WAS optional, back when the panel was expected to report the
                    // video url itself. It does not (see ApplyVideoUrl), so a missing file
                    // now means no door video at all — everything else on the page still
                    // works, which is why this is a warning and not a failure.
                    CrestronConsole.PrintLine("Intercom: NO CONFIG FILE ({0}) - call control will work but there will be NO DOOR VIDEO (the panel does not supply a url)", ConfigFilePath);
                    lock (stationsLock) { stations = new List<StationEntry>(); }
                }
            }
            catch (Exception ex)
            {
                ErrorLog.Error("Intercom Load error: {0}", ex.Message);
                lock (stationsLock) { stations = new List<StationEntry>(); }
            }
        }

        /// <summary>Re-reads the config and re-pushes state to every panel (console
        /// command "reloadintercom"), so station/url edits take effect without a restart.</summary>
        public void Reload()
        {
            Load();
            foreach (var tp in _parent.manager.touchpanelZ) { SendStateTo(tp.Value); }
            int count;
            lock (stationsLock) { count = stations.Count; }
            CrestronConsole.PrintLine("Intercom: reloaded {0} station(s) and re-pushed to all panels", count);
        }

        // ─── Per-panel call tracking ────────────────────────────────────────

        // stateLock is NOT optional: DeviceExtenderSigChange fires off-thread and once
        // per panel, so a house-wide ring (every panel in the call group, simultaneously)
        // mutates these dictionaries concurrently — which corrupts a plain Dictionary
        // rather than merely racing on a value.
        private readonly object stateLock = new object();

        // ─── DND is OURS, not the panel's ───────────────────────────────────
        //
        // ⚠ DO NOT GO BACK TO THE EXTENDER'S DoNotDisturb(). It looks like the obvious
        // implementation and it is the wrong one.
        //
        // The panel's native DND makes the panel DECLINE the SIP call. Every panel in the
        // 2N's call group receives the same call, so one panel on native DND answers for
        // the whole house: the 2N gets a decline and the person at the door hears BUSY,
        // even though every other panel rang perfectly. Observed 2026-08-06 — TP-1 sat at
        // dnd=1, emitted a `term` pulse ~300ms after each ring, and the door reported busy
        // until the button was pressed two or three times.
        //
        // DND means "do not disturb the person standing at THIS panel". It is a local
        // preference and the door must never learn about it. So:
        //   - our own per-panel flag is the DND the UI shows and the page obeys;
        //   - the panel's native DND is forced OFF and kept off (EnsureNativeDndOff);
        //   - a DND panel simply is not woken, not flipped to the page, and is silenced
        //     for the duration of the call. The call is never declined, so the 2N keeps
        //     ringing the other panels and hears nothing unusual.
        //
        // In memory only: DND resets to off on a program restart. That matches how the
        // native flag behaved and nobody has asked for it to persist; if that changes it
        // belongs in the same file as the rest of the intercom config.
        private readonly Dictionary<ushort, bool> dndByTp = new Dictionary<ushort, bool>();

        // Panel volume saved before an inbound call silences a DND panel, so it can be put
        // back exactly as the user left it. Presence in this dictionary IS the "currently
        // silenced" flag — re-silencing an already-silenced panel would save 0 as the
        // restore value and leave it permanently mute.
        private readonly Dictionary<ushort, ushort> silencedVolumeByTp = new Dictionary<ushort, ushort>();

        /// <summary>
        /// When we last tried to clear a panel's NATIVE dnd, per panel. A timestamp rather
        /// than a "done once" flag, for two reasons:
        ///   - once-per-run would miss a panel where someone turns native DND on later from
        ///     the panel's own intercom UI, and one such panel busies the whole house again;
        ///   - but DoNotDisturb() is a TOGGLE, so firing it again before the feedback has
        ///     caught up would switch it straight back ON. That is exactly how the mic-mute
        ///     bug behaved on this hardware.
        /// So: re-check continuously, but never re-fire within NativeDndRetryMs.
        /// </summary>
        private readonly Dictionary<ushort, DateTime> nativeDndLastClearByTp = new Dictionary<ushort, DateTime>();
        private const int NativeDndRetryMs = 3000;

        /// <summary>Whether this panel is on (our) DND. Caller must NOT hold stateLock.</summary>
        public bool IsDnd(ushort tpNumber)
        {
            lock (stateLock)
            {
                bool on;
                return dndByTp.TryGetValue(tpNumber, out on) && on;
            }
        }

        /// <summary>Sets our DND flag for a panel and re-pushes its state. Returns the new value.</summary>
        public bool SetDnd(ushort tpNumber, bool on)
        {
            lock (stateLock) { dndByTp[tpNumber] = on; }

            UI.TouchpanelUI tp;
            if (_parent.manager.touchpanelZ.TryGetValue(tpNumber, out tp) && tp != null)
            {
                // Going into DND releases any stream this panel is still holding. A panel
                // parked on the intercom page after an earlier call keeps its stream (by
                // design), and switching it to DND would otherwise leave it holding the door
                // station's RTSP session while refusing to show anything — which on a
                // station serving one session silently blocks every other panel. "Do not
                // disturb me" cannot mean "and keep the door camera to myself".
                if (on && HasVideoUrl(tp))
                {
                    CrestronConsole.PrintLine("{0} INTERCOM TP-{1} DND on - clearing the stream it was still holding",
                        Ts(), tpNumber);
                    SetVideoUrl(tp, string.Empty);
                }

                // Belt and braces: whatever the UI just did, the panel's own DND must stay
                // off or the house-wide busy problem comes straight back.
                EnsureNativeDndOff(tp);
                SendStateTo(tp);
            }
            CrestronConsole.PrintLine("{0} INTERCOM TP-{1} DND {2} (this panel stays quiet; the door is NOT told)",
                Ts(), tpNumber, on ? "ON" : "off");
            return on;
        }

        /// <summary>
        /// Forces the PANEL'S OWN do-not-disturb off, since that is the thing that declines
        /// calls and makes the door hear busy. Only acts when the feedback says it is on —
        /// the extender exposes DoNotDisturb() as a toggle with no absolute set, so a blind
        /// call would switch it ON for every panel that was correctly off.
        /// </summary>
        private void EnsureNativeDndOff(UI.TouchpanelUI tp)
        {
            EnsureNativeDndOff(tp, false);
        }

        /// <summary>
        /// Clears a panel's NATIVE do-not-disturb now, ignoring the retry rate-limit.
        /// Console entry point (`intercomdnd`).
        ///
        /// The native flag is the only reason one panel's DND can affect the rest of the
        /// house: it makes that panel DECLINE the SIP call, and the 2N reports the decline
        /// as busy to whoever is standing at the door. Our own DND never declines anything,
        /// so once this is off, a panel on DND is silent by itself and nobody else notices.
        /// </summary>
        public void ClearNativeDnd(ushort tpNumber)
        {
            UI.TouchpanelUI tp;
            if (!_parent.manager.touchpanelZ.TryGetValue(tpNumber, out tp) || tp == null) { return; }
            EnsureNativeDndOff(tp, true);
        }

        /// <summary>
        /// As above. <paramref name="force"/> skips the retry rate-limit — used when a human
        /// explicitly asked (the console command), where waiting is just confusing.
        /// </summary>
        private void EnsureNativeDndOff(UI.TouchpanelUI tp, bool force)
        {
            if (tp == null || !tp.HasVoip || !tp.VoipDndActive) { return; }

            if (!force)
            {
                // ⚠ Rate limit, not a once-per-run flag. The first version used "check each
                // panel once" and it FAILED on the panel it was written for: at boot the
                // extender feedback has not settled (TP-1 was observed reporting cs=0 before
                // cs=5), so the single check read dnd=false, spent its one shot, and never
                // looked again — leaving the panel declining calls for the whole house.
                DateTime last;
                lock (stateLock)
                {
                    if (nativeDndLastClearByTp.TryGetValue(tp.Number, out last) &&
                        (DateTime.Now - last).TotalMilliseconds < NativeDndRetryMs)
                    {
                        return;
                    }
                    nativeDndLastClearByTp[tp.Number] = DateTime.Now;
                }
            }
            else
            {
                lock (stateLock) { nativeDndLastClearByTp[tp.Number] = DateTime.Now; }
            }

            CrestronConsole.PrintLine("{0} INTERCOM TP-{1} panel's NATIVE DND is on - turning it off (it DECLINES calls, which reaches the door as BUSY). Intercom DND is handled in software instead.",
                Ts(), tp.Number);
            tp.VoipDnd();
        }

        private const string StateIdle = "idle";
        private const string StateIncoming = "incoming";
        private const string StateRinging = "ringing";
        private const string StateActive = "active";
        private const string StateBusy = "busy";

        // ─── The inbound-call LATCH ─────────────────────────────────────────
        //
        // ⚠ WHY THIS EXISTS — do not replace it with a direct sig read.
        //
        // VOIPRingingFeedback is MOMENTARY. Measured on a TST-1080 taking a real call:
        // it pulses high for ~50ms once per ring burst, 3.005s apart. It is the ringer
        // cadence, not a call state. An earlier version derived the UI state straight
        // from the sigs, which meant:
        //   - the Answer button was enabled for 50ms out of every 3000ms (it "flashed"),
        //   - the banner read "Idle" for 97% of the ring,
        //   - and the idle→ring→idle churn re-fired the panel wake AND the forced page
        //     flip every 3 seconds.
        // VOIPIncomingCallFeedback did not assert at all on that panel, so there is no
        // latched sig to lean on instead.
        //
        // So: a pulse on EITHER ringing or incoming LATCHES an inbound call here, and the
        // latch is what the UI sees. It clears on answer (CallActive), on a
        // CallTerminated pulse, or — the backstop — when no further ring pulse has
        // arrived for InboundIdleMs.
        //
        // The watchdog RE-READS the sigs before clearing, so this is also correct on a
        // panel family where Incoming/Ringing DO latch: there, the sig is still true when
        // the timer fires and the latch is simply re-armed instead of dropped.
        private const int InboundIdleMs = 6000;   // 2x the observed 3.0s ring cadence

        private class CallState
        {
            public bool InboundLatched;
            public string Derived = StateIdle;
            public CTimer Watchdog;
        }

        private readonly Dictionary<ushort, CallState> callByTp = new Dictionary<ushort, CallState>();

        // Panels already warned about a missing speaker-volume signal, so the warning is
        // logged once instead of once per analog write during a slider drag.
        private readonly HashSet<ushort> volumeUnsupportedLogged = new HashSet<ushort>();

        /// <summary>Gets (or creates) the latch record for a panel. Caller holds stateLock.</summary>
        private CallState CallFor(ushort tpNumber)
        {
            CallState cs;
            if (!callByTp.TryGetValue(tpNumber, out cs))
            {
                cs = new CallState();
                callByTp[tpNumber] = cs;
            }
            return cs;
        }

        /// <summary>
        /// Derives the UI state from the LATCH plus the sigs that genuinely are level
        /// states. Order matters: an answered call asserts CallActive while an inbound
        /// latch may still be held, so active must win.
        ///
        /// Note `ringing` here means OUTBOUND ringback (we called, far end is ringing) —
        /// a different thing from our own ringer, which is what feeds the inbound latch
        /// and surfaces as `incoming`.
        /// </summary>
        private static string DeriveState(UI.TouchpanelUI tp, CallState cs)
        {
            if (tp.VoipCallActive) { return StateActive; }
            if (cs.InboundLatched) { return StateIncoming; }
            if (tp.VoipRingback) { return StateRinging; }
            if (tp.VoipBusy) { return StateBusy; }
            return StateIdle;
        }

        private static bool IsCallState(string state)
        {
            return state == StateIncoming || state == StateRinging || state == StateActive;
        }

        /// <summary>Stops and clears a panel's watchdog. Caller holds stateLock.</summary>
        private static void KillWatchdog(CallState cs)
        {
            if (cs.Watchdog != null)
            {
                cs.Watchdog.Stop();
                cs.Watchdog.Dispose();
                cs.Watchdog = null;
            }
        }

        /// <summary>
        /// Called from TouchpanelUI.TeardownVoipExtenders (i.e. Dispose) so a reload does
        /// not leave a running CTimer behind. A live CTimer is a GC root and would pin the
        /// entire previous panel graph — the same leak that reloadjson hit before.
        /// </summary>
        public void OnPanelDisposed(ushort tpNumber)
        {
            lock (stateLock)
            {
                CallState cs;
                if (callByTp.TryGetValue(tpNumber, out cs)) { KillWatchdog(cs); }
                callByTp.Remove(tpNumber);
            }
        }

        /// <summary>
        /// Backstop for "the caller gave up": no ring pulse for InboundIdleMs. Re-reads
        /// the sigs first so a panel family whose Incoming/Ringing genuinely latch just
        /// re-arms instead of being cleared out from under a live call.
        /// </summary>
        private void InboundWatchdogExpired(ushort tpNumber)
        {
            UI.TouchpanelUI tp;
            if (!_parent.manager.touchpanelZ.TryGetValue(tpNumber, out tp) || tp == null) { return; }

            bool stillRinging = tp.VoipIncoming || tp.VoipRinging || tp.VoipCallActive;

            lock (stateLock)
            {
                CallState cs = CallFor(tpNumber);
                cs.Watchdog = null;
                if (stillRinging)
                {
                    ArmInboundWatchdog(tpNumber, cs);
                    return;
                }
                if (!cs.InboundLatched) { return; }
                cs.InboundLatched = false;
            }

            CrestronConsole.PrintLine("{0} INTERCOM TP-{1} inbound latch expired ({2}ms with no ring) -> idle",
                Ts(), tpNumber, InboundIdleMs);
            OnVoipStateChanged(tp);
        }

        /// <summary>(Re)starts the inbound watchdog. Caller holds stateLock.</summary>
        private void ArmInboundWatchdog(ushort tpNumber, CallState cs)
        {
            KillWatchdog(cs);
            ushort captured = tpNumber;
            cs.Watchdog = new CTimer(o => InboundWatchdogExpired(captured), InboundIdleMs);
        }

        /// <summary>
        /// Called from the panel's VOIP DeviceExtenderSigChange hook. Recomputes state,
        /// pushes it to HTML, and on a transition INTO a call: wakes the panel, forces
        /// the Intercom page, suppresses the idle timeout, and resolves the video url.
        ///
        /// On the way out of a call it clears the idle suppression but deliberately
        /// does NOT navigate away — the panel stays on the Intercom page and the normal
        /// idle timeout takes it home, which is what was asked for.
        /// </summary>
        public void OnVoipStateChanged(UI.TouchpanelUI tp)
        {
            if (tp == null) { return; }

            // Snapshot the raw sigs ONCE — each read is a reflection call into the
            // extender, and reading the same sig twice during one event could straddle a
            // momentary pulse and give inconsistent answers within a single evaluation.
            bool rawIncoming = tp.VoipIncoming;
            bool rawRinging = tp.VoipRinging;
            bool rawActive = tp.VoipCallActive;
            bool rawBusy = tp.VoipBusy;
            bool rawTerminated = tp.VoipTerminated;
            bool rawRingback = tp.VoipRingback;

            string state;
            string previous;
            bool hadPrevious;

            lock (stateLock)
            {
                CallState cs = CallFor(tp.Number);
                hadPrevious = cs.Derived != null;
                previous = cs.Derived;

                if (rawActive || rawTerminated)
                {
                    // Answered, or the call is over: either way the inbound latch is done.
                    cs.InboundLatched = false;
                    KillWatchdog(cs);
                }
                else if (rawIncoming || rawRinging)
                {
                    // A ring pulse. Latch (or refresh) and restart the give-up timer.
                    cs.InboundLatched = true;
                    ArmInboundWatchdog(tp.Number, cs);
                }

                state = DeriveState(tp, cs);
                cs.Derived = state;
            }

            bool changed = !hadPrevious || previous != state;

            if (changed)
            {
                CrestronConsole.PrintLine("{0} INTERCOM TP-{1} state {2} -> {3}  (name=\"{4}\" num=\"{5}\" dnd={6} mute={7})",
                    Ts(), tp.Number, hadPrevious ? previous : "(init)", state,
                    tp.VoipCallerName, tp.VoipCallerNumber, tp.VoipDndActive, tp.VoipMicMuted);
            }

            // Raw sigs on EVERY event, not just on a derived change — the whole
            // ringing-is-momentary problem was invisible until the pulses were visible, and a
            // suppressed-because-unchanged log is exactly what hid it.
            //
            // BUT it is off by default now. Every panel connect/reconnect emits several of these
            // per panel, which on this fleet buried the lines actually being looked for (the
            // UniFi doorbell output) in a wall of idle-state noise. A diagnostic that hides other
            // diagnostics has a real cost, not just a cosmetic one.
            //
            // Turn it back on with `intercomraw on` before debugging a new panel family — that is
            // the only situation where per-event raw sigs matter, and it is exactly the situation
            // the comment above describes.
            if (RawSigLogging)
            {
            CrestronConsole.PrintLine("{0} INTERCOM TP-{1} raw[inc={2} ring={3} act={4} busy={5} term={6} rb={7} dnd={8} mic={9} cs={10}] latch={11} -> {12}",
                Ts(), tp.Number,
                rawIncoming ? 1 : 0, rawRinging ? 1 : 0, rawActive ? 1 : 0,
                rawBusy ? 1 : 0, rawTerminated ? 1 : 0, rawRingback ? 1 : 0,
                tp.VoipDndActive ? 1 : 0, tp.VoipMicMuted ? 1 : 0,
                tp.VoipCallStateCode,
                state == StateIncoming ? 1 : 0, state);
            }

            bool wasInCall = hadPrevious && IsCallState(previous);
            bool nowInCall = IsCallState(state);

            // Entering a call: make sure the panel is awake, on the page, and will not
            // be pulled home mid-call by the idle timeout.
            if (nowInCall && !wasInCall)
            {
                // DND panel: take no part in this call. Do not wake it, do not flip it to
                // the page, do not open a stream on it — and above all do NOT decline, which
                // is what the panel's native DND used to do and what made the door hear busy
                // for the whole house. The call simply rings on unanswered here while every
                // other panel behaves normally.
                if (IsDnd(tp.Number))
                {
                    SilencePanel(tp);
                    CrestronConsole.PrintLine("{0} INTERCOM TP-{1} DND - not waking, not flipping, not streaming (call NOT declined)",
                        Ts(), tp.Number);
                    SendStateTo(tp);
                    return;
                }

                tp.IntercomCallActive = true;   // suppresses the idle-timeout go-home
                tp.WakePanel();

                // ⚠ ORDER MATTERS: url BEFORE the page flip. ch5-video will not re-open
                // a stream when receivestateurl changes while it is already playing, so
                // HTML has to cycle the play gate to pick up a late url — and that costs
                // a ~3s RTSP teardown gap. Setting the url while the page is still CLOSED
                // means the gate is down anyway, and pageRouter raises it as part of
                // opening the page, so the very first read is already the right url and
                // no restart is needed. Flip first and you pay the gap on every call.
                ApplyVideoUrl(tp);
                // A VOIP-extender call is by definition a SIP station → scenario 1. The
                // UniFi path (scenario 2) does not come through here at all; it has no VOIP
                // extender involvement and will call ShowIntercomPage(2) from its own driver.
                if (tp.HTML_UI) { tp.ShowIntercomPage(1); }
            }
            else if (!nowInCall && wasInCall)
            {
                tp.IntercomCallActive = false;
                RestorePanelVolume(tp);   // no-op unless DND silenced it for this call

                // ⚠ THE VIDEO DELIBERATELY SURVIVES THE CALL. It used to be cleared here,
                // which meant hanging up killed the picture instantly — and hanging up is
                // exactly when you most want to keep looking at the door ("who was that?",
                // "did they leave the parcel?"). The stream now stays until the panel
                // actually LEAVES the page: the close button, a navigation, or the idle
                // timeout, all of which land on SetPageActive(false). Clearing IntercomCallActive
                // above is what re-allows that timeout, so nothing parks on the page forever.
                //
                // The cost is that the panel holds the RTSP session for the rest of the page
                // visit rather than releasing it at hangup. That is the intended trade, but
                // it is why SetPageActive MUST stay wired to every route off this page — a
                // missed one leaks a session until the next reboot.
            }
            else if (nowInCall && changed)
            {
                // Still in a call but the stage changed (e.g. incoming -> active). Re-resolve
                // ONLY if we have nothing: the caller URI does not arrive until the call is
                // answered (verified on hardware — `num` is empty on the incoming frame), so
                // a station that could only be matched by number gets its second chance here.
                //
                // ⚠ Guarded, not unconditional. Re-applying a url that is already playing
                // makes HTML cycle the play gate for a ~3s RTSP teardown gap, so an
                // unconditional re-resolve would blank the door video the instant you answer
                // — the one moment it must not.
                if (!HasVideoUrl(tp)) { ApplyVideoUrl(tp); }
            }

            SendStateTo(tp);
        }

        // ─── State push (1560) ──────────────────────────────────────────────

        /// <summary>
        /// Pushes this panel's intercom state and current speaker volume. Also the
        /// reconnect-replay entry point (called from StartupPanel at boot and on
        /// panel-online), so a panel that reloads mid-call comes back correct.
        /// </summary>
        public void SendStateTo(UI.TouchpanelUI tp)
        {
            try
            {
                if (tp == null) { return; }

                // Clear any NATIVE DND left set on the hardware — from earlier testing, from
                // the panel's own intercom UI, or from a build predating software DND. One
                // panel still holding it declines calls for the WHOLE HOUSE.
                //
                // ⚠ Deliberately BEFORE the HTML_UI guard below. A panel that declines SIP
                // calls hurts every other panel whether or not it happens to run our HTML UI
                // — this is about the panel's membership of the 2N's call group, not about
                // what is on its screen.
                //
                // This runs on every state push rather than once, because the feedback is not
                // reliable at boot; see the rate-limit note in EnsureNativeDndOff.
                if (tp.HasVoip) { EnsureNativeDndOff(tp); }

                if (!tp.HTML_UI || tp.UserInterface == null) { return; }

                // Read the LATCHED state, never re-derive from the sigs here: this is also
                // the reconnect-replay path, and re-deriving would land between ring pulses
                // and report "idle" to a panel that is actively ringing.
                string state = StateIdle;
                if (tp.HasVoip)
                {
                    lock (stateLock) { state = CallFor(tp.Number).Derived ?? StateIdle; }
                }

                var sb = new StringBuilder();
                sb.Append("{\"state\":\"").Append(state).Append("\"");
                sb.Append(",\"name\":\"").Append(Escape(tp.VoipCallerName)).Append("\"");
                sb.Append(",\"num\":\"").Append(Escape(tp.VoipCallerNumber)).Append("\"");
                // OUR DND, not tp.VoipDndActive — the native flag is forced off and is only
                // kept in the raw log as a diagnostic (it should read 0 forever now).
                sb.Append(",\"dnd\":").Append(IsDnd(tp.Number) ? 1 : 0);
                sb.Append(",\"mute\":").Append(tp.VoipMicMuted ? 1 : 0);
                sb.Append(",\"reg\":").Append(tp.VoipRegistered ? 1 : 0);
                // voip:0 tells HTML this panel has no VOIP extender at all (xpanel), so
                // the page can say so instead of presenting dead buttons.
                sb.Append(",\"voip\":").Append(tp.HasVoip ? 1 : 0);
                sb.Append("}");

                tp.UserInterface.StringInput[StateJoin].StringValue = sb.ToString();

                // Echo the panel's own speaker level so the slider starts in the right place.
                if (tp.HasVoip)
                {
                    tp.UserInterface.UShortInput[VolumeFbJoin].UShortValue = tp.GetPanelSpeakerVolume();
                }
            }
            catch (Exception ex)
            {
                ErrorLog.Error("Intercom SendStateTo error: {0}", ex.Message);
            }
        }

        private static string Escape(string s)
        {
            if (string.IsNullOrEmpty(s)) { return string.Empty; }
            return s.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }

        // ─── Video url (1562) ───────────────────────────────────────────────

        /// <summary>
        /// Resolves and pushes the video url for the current call.
        ///
        /// ⚠ THE CONFIGURED RTSP URL IS NOW THE PRIMARY (and in practice the only) PATH.
        /// The original design preferred the panel's own VOIPVideoURLFeedback because it
        /// would have needed no 2N licence. That member DOES NOT EXIST: dumping the live
        /// Tss752VoipReservedSigs on a TST-1080 showed no video-url member of any kind
        /// (the only string members are MyURIFeedback and IncomingURIFeedback, both SIP
        /// identities, not streams). It is absent, not misspelled, so no candidate-name
        /// list can recover it. See INTERCOM-HANDOFF.md, "Video".
        ///
        /// The panel read is KEPT — but demoted to a second look, and it logs loudly if it
        /// ever returns anything, because a panel family that does expose a url would mean
        /// door video without the licence, which is worth knowing immediately.
        /// </summary>
        private void ApplyVideoUrl(UI.TouchpanelUI tp)
        {
            // Scenario 1: this is a call arriving on the panel's VOIP extender, so the
            // caller can only be a SIP station. Excluding the "unifi" entries matters more
            // than it looks — the single-station fallback below is what actually resolves
            // most houses, and one unrelated UniFi entry in the file would otherwise make
            // the count 2 and kill it.
            string how;
            var station = MatchStation(tp.VoipCallerNumber, tp.VoipCallerName, 1, out how);
            string url = (station != null) ? (station.RtspUrl ?? string.Empty) : string.Empty;
            string source = (station != null)
                ? "station \"" + (station.Name ?? "unnamed") + "\" matched by " + how
                : null;

            if (string.IsNullOrEmpty(url))
            {
                string panelUrl = tp.VoipVideoUrl ?? string.Empty;
                if (!string.IsNullOrEmpty(panelUrl))
                {
                    // Should be unreachable on every panel family checked so far. If this
                    // ever prints, INTERCOM-HANDOFF.md's "Video" section is wrong for that
                    // family and the 2N Enhanced Video licence may not be needed there.
                    CrestronConsole.PrintLine("{0} INTERCOM TP-{1} *** PANEL REPORTED A VIDEO URL *** ({2}) - this member was believed absent; see INTERCOM-HANDOFF.md",
                        Ts(), tp.Number, panelUrl);
                    url = panelUrl;
                    source = "panel VOIP extender (unexpected)";
                }
            }

            if (string.IsNullOrEmpty(url))
            {
                // Say WHICH of the two failures this is — "no station matched" and "the
                // matched station has no url configured" have completely different fixes,
                // and the old single message could not tell them apart.
                if (station == null)
                {
                    int configured;
                    lock (stationsLock) { configured = stations.Count; }
                    CrestronConsole.PrintLine("{0} INTERCOM TP-{1} no video url: no station matched caller num=\"{2}\" name=\"{3}\" ({4} station(s) configured in {5})",
                        Ts(), tp.Number, tp.VoipCallerNumber, tp.VoipCallerName, configured, ConfigFilePath);
                }
                else
                {
                    CrestronConsole.PrintLine("{0} INTERCOM TP-{1} no video url: station \"{2}\" matched but its rtspUrl is empty (needs the 2N Enhanced Video licence + RTSP server enabled)",
                        Ts(), tp.Number, station.Name ?? "unnamed");
                }
            }
            else
            {
                // The url itself, not just its length. It is a LAN RTSP address, the
                // console is already privileged, and "len 47" told you nothing about the
                // one thing that goes wrong here — a typo'd or rtsps:// url.
                CrestronConsole.PrintLine("{0} INTERCOM TP-{1} video url from {2}: {3}",
                    Ts(), tp.Number, source, url);
                WarnIfUrlLooksWrong(tp, url);
            }

            SetVideoUrl(tp, url);
        }

        /// <summary>
        /// Flags the two url mistakes that produce a black viewer with no other symptom.
        /// Both are one-line fixes but neither is visible from the panel, and chasing
        /// either through a doorbell-press test cycle costs an afternoon.
        /// </summary>
        private static void WarnIfUrlLooksWrong(UI.TouchpanelUI tp, string url)
        {
            string u = url.Trim();

            // Panels cannot do RTSPS/SRTP. This is the exact trap the UniFi path documents
            // (rtsps://…:7441/id?enableSrtp must become rtsp://…:7447/id) and the 2N will
            // hand out an rtsps url just as happily.
            if (u.StartsWith("rtsps:", StringComparison.OrdinalIgnoreCase))
            {
                CrestronConsole.PrintLine("{0} INTERCOM TP-{1} WARNING url is rtsps:// - panels cannot decode RTSPS/SRTP, use plain rtsp://",
                    Ts(), tp.Number);
            }
            // udp:/rtp: are here for the MULTICAST path: a multicast door stream may be
            // addressed as udp://@239.x.x.x:port rather than as an rtsp:// url, and warning
            // "no recognised scheme" at someone who has just correctly configured multicast
            // would send them looking for a fault that is not there.
            else if (!u.StartsWith("rtsp:", StringComparison.OrdinalIgnoreCase) &&
                     !u.StartsWith("udp:", StringComparison.OrdinalIgnoreCase) &&
                     !u.StartsWith("rtp:", StringComparison.OrdinalIgnoreCase) &&
                     !u.StartsWith("http:", StringComparison.OrdinalIgnoreCase) &&
                     !u.StartsWith("https:", StringComparison.OrdinalIgnoreCase))
            {
                CrestronConsole.PrintLine("{0} INTERCOM TP-{1} WARNING url has no recognised scheme - expected rtsp://user:pass@host:554/path, or udp://@<group>:<port> for multicast",
                    Ts(), tp.Number);
            }
        }

        /// <summary>
        /// Console-command entry point: push a video url to one panel (or all) and open
        /// the Intercom page, with no call involved. Backs `intercomvideo`.
        ///
        /// WHY THIS EXISTS. The video window is the unfinished part of this subsystem, and
        /// every one of its failure modes — wrong codec, rtsps instead of rtsp, an opaque
        /// element over the viewer, the play gate not cycling — needs a look at a real
        /// panel to diagnose. Without this, each look costs a walk to the door and a
        /// button press, and the ring lasts long enough for about one observation. This
        /// makes it a console command and a stopwatch-free stare at the screen.
        ///
        /// Passing an explicit url also isolates the two halves: if a known-good camera
        /// url renders here but the door station does not, the problem is the 2N (licence,
        /// codec, RTSP server), not this program or the page.
        /// </summary>
        /// <param name="tpNumber">Panel number, or 0 for every HTML panel.</param>
        /// <param name="url">
        /// Url to push. **null means "resolve from config"; empty string means "clear the
        /// url"** — they are genuinely different requests and cannot be collapsed, or
        /// `intercomvideo 12 off` silently re-pushes the configured url instead of
        /// clearing it.
        /// </param>
        /// <summary>
        /// Lists which panels currently hold a door-station url — i.e. which ones are
        /// holding an RTSP session on the 2N. Backs `intercomvideo status`.
        ///
        /// This exists because the door video now OUTLIVES THE CALL: a panel parked on the
        /// intercom page keeps its stream until it navigates away or its 60s idle timeout
        /// fires. If the 2N only serves one or two concurrent sessions, a single parked
        /// panel starves every other panel — and from the console there was previously no
        /// way to see that, which made "the second panel gets 56532" impossible to test
        /// cleanly.
        /// </summary>
        public void ReportVideoHolders()
        {
            int holding = 0;
            foreach (var kv in _parent.manager.touchpanelZ)
            {
                var tp = kv.Value;
                if (tp == null || !tp.HTML_UI || tp.UserInterface == null) { continue; }

                string url = string.Empty;
                try { url = tp.UserInterface.StringInput[VideoUrlJoin].StringValue ?? string.Empty; }
                catch { }

                if (!string.IsNullOrEmpty(url))
                {
                    holding++;
                    CrestronConsole.PrintLine("TP-{0} HOLDING door video: {1}", kv.Key, url);
                }
            }
            if (holding == 0)
            {
                CrestronConsole.PrintLine("No panel is holding a door-station stream.");
            }
            else
            {
                CrestronConsole.PrintLine("{0} panel(s) holding a stream. Clear one with 'intercomvideo <tp> off' and allow a few seconds for the RTSP session to release.",
                    holding);
            }
        }

        public void TestVideo(ushort tpNumber, string url)
        {
            bool explicitUrl = (url != null);

            foreach (var kv in _parent.manager.touchpanelZ)
            {
                var tp = kv.Value;
                if (tp == null || !tp.HTML_UI) { continue; }
                if (tpNumber > 0 && kv.Key != tpNumber) { continue; }

                string push = url;
                if (!explicitUrl)
                {
                    // Same resolution the real call path uses, minus the caller identity —
                    // with no call there is nothing to match on, so this lands on the
                    // single-station fallback. That is the common case anyway.
                    var station = MatchStation(null, null, 1);
                    if (station == null || string.IsNullOrEmpty(station.RtspUrl))
                    {
                        CrestronConsole.PrintLine("intercomvideo TP-{0}: no url configured for a sip station in {1} - pass one: intercomvideo {0} rtsp://...",
                            kv.Key, ConfigFilePath);
                        continue;
                    }
                    push = station.RtspUrl;
                }

                CrestronConsole.PrintLine("intercomvideo TP-{0}: {1}", kv.Key,
                    string.IsNullOrEmpty(push) ? "(clearing url)" : push);
                if (!string.IsNullOrEmpty(push)) { WarnIfUrlLooksWrong(tp, push); }

                // ⚠ Url BEFORE the page flip, for the same load-bearing reason as the real
                // call path: ch5-video will not re-open a stream when receivestateurl
                // changes while it is already playing, so setting it while the page is shut
                // means pageRouter's own gate-raise is the first read. Flip first and the
                // page has to cycle the gate, which costs the ~3s RTSP teardown gap and
                // makes this look broken when it is not.
                SetVideoUrl(tp, push ?? string.Empty);
                tp.WakePanel();
                tp.ShowIntercomPage(1);
            }
        }

        /// <summary>
        /// Logs one ch5-video diagnostic event from the intercom page's decoder. Routed
        /// here from TouchpanelUI.SigChange for joins 1566-1570.
        ///
        /// Watch this while the door video is on screen — it is the ONLY account of why
        /// a stream will not render, because the panel's decoder is the thing that knows
        /// and HTML cannot read its own send-joins back. The two failures worth
        /// recognising on sight are annotated inline rather than left as bare numbers:
        /// the Cameras work lost rounds to exactly these, and a raw "errorCode = 64533"
        /// means nothing to whoever reads this log next.
        /// </summary>
        public void LogVideoDiag(ushort tpNumber, uint join, string value)
        {
            string label;
            switch (join)
            {
                case VideoStateJoin:        label = "state";        break;
                case VideoErrorCodeJoin:    label = "errorCode";    break;
                case VideoErrorMessageJoin: label = "errorMessage"; break;
                case VideoResolutionJoin:   label = "resolution";   break;
                case VideoRetryCountJoin:   label = "retryCount";   break;
                default:                    label = "join " + join; break;
            }

            CrestronConsole.PrintLine("{0} INTERCOM TP-{1} ch5-video {2} = \"{3}\"{4}",
                Ts(), tpNumber, label, value, Annotate(join, value));
        }

        /// <summary>Turns the decoder's numbers into the sentence you would otherwise
        /// have to go and look up. Empty string when there is nothing useful to add.</summary>
        private static string Annotate(uint join, string value)
        {
            int n;
            if (!int.TryParse(value, out n)) { return string.Empty; }

            if (join == VideoStateJoin)
            {
                // 2 = playing and 7 = failed are the two that matter; a stream can reach
                // 2 and then drop to 7 seconds later, which is what "it worked once" means.
                if (n == 2) { return "  <- PLAYING"; }
                if (n == 7) { return "  <- FAILED (stays failed; the play gate must be cycled)"; }
                return string.Empty;
            }

            if (join == VideoErrorCodeJoin)
            {
                if (n == 0) { return string.Empty; }
                // Learned on the Cameras page, and both apply verbatim to the door station.
                if (n == 64533)
                {
                    return "  <- CODEC. Panels will not decode H.265 - set the 2N to H.264 (Video tab: 640x480, 30fps, 2048kbps)";
                }
                if (n == 56529 || n == 56532)
                {
                    return "  <- RTSP SESSION. The panel opened a stream before releasing the last one, or the source is out of sessions";
                }
                return "  <- see the Crestron ch5-video error table";
            }

            return string.Empty;
        }

        private void SetVideoUrl(UI.TouchpanelUI tp, string url)
        {
            try
            {
                if (tp == null || !tp.HTML_UI || tp.UserInterface == null) { return; }
                tp.UserInterface.StringInput[VideoUrlJoin].StringValue = url ?? string.Empty;
            }
            catch (Exception ex)
            {
                ErrorLog.Error("Intercom SetVideoUrl error: {0}", ex.Message);
            }
        }

        // ─── Silencing a DND panel for the duration of a call ────────────────
        //
        // The panel rings by itself: the Rava call arrives at the panel's own VOIP stack and
        // its ringer is not something the program is handed a control for. With the native
        // DND gone (deliberately — it declines the call and the door hears busy) the only
        // remaining lever on this hardware is the panel's audio output level, which is the
        // same one the intercom volume slider drives.
        //
        // ⚠ NOT CONFIRMED ON HARDWARE. AllAudioVolume is "all audio" and the ring is audio,
        // so it should cover it — but that is an inference, not a measurement. If a DND panel
        // still chirps, the ringer is on a path this extender does not expose and the
        // remaining option is the panel's own local setup. Everything else about DND (no
        // wake, no page flip, no stream, and crucially NO DECLINE) works regardless.

        /// <summary>Drops a DND panel's output to zero, remembering what it was.</summary>
        private void SilencePanel(UI.TouchpanelUI tp)
        {
            if (tp == null || !tp.HasVoip) { return; }

            lock (stateLock)
            {
                // Already silenced — do not overwrite the saved level with the 0 we just set,
                // or the restore puts the panel back to mute and it stays there.
                if (silencedVolumeByTp.ContainsKey(tp.Number)) { return; }
                silencedVolumeByTp[tp.Number] = tp.GetPanelSpeakerVolume();
            }

            ushort saved;
            lock (stateLock) { saved = silencedVolumeByTp[tp.Number]; }
            tp.SetPanelSpeakerVolume(0);
            CrestronConsole.PrintLine("{0} INTERCOM TP-{1} DND - silencing panel for this call (volume {2} -> 0, restored on call end)",
                Ts(), tp.Number, saved);
        }

        /// <summary>Puts a silenced panel's volume back. No-op if we never silenced it.</summary>
        private void RestorePanelVolume(UI.TouchpanelUI tp)
        {
            if (tp == null) { return; }

            ushort saved;
            lock (stateLock)
            {
                if (!silencedVolumeByTp.TryGetValue(tp.Number, out saved)) { return; }
                silencedVolumeByTp.Remove(tp.Number);
            }

            tp.SetPanelSpeakerVolume(saved);
            CrestronConsole.PrintLine("{0} INTERCOM TP-{1} DND - call over, volume restored to {2}",
                Ts(), tp.Number, saved);
            SendStateTo(tp);
        }

        /// <summary>Whether this panel currently has a door-station url loaded.</summary>
        private static bool HasVideoUrl(UI.TouchpanelUI tp)
        {
            try
            {
                if (tp == null || !tp.HTML_UI || tp.UserInterface == null) { return false; }
                return !string.IsNullOrEmpty(tp.UserInterface.StringInput[VideoUrlJoin].StringValue);
            }
            catch { return false; }
        }

        /// <summary>
        /// Called from every page-descriptor write: true when this panel is now showing the
        /// Intercom page, false when it goes anywhere else (close button, a subsystem, home,
        /// or the idle timeout — they all write a descriptor).
        ///
        /// ⚠ THIS IS WHAT RELEASES THE RTSP SESSION. The door video deliberately outlives the
        /// call now (see OnVoipStateChanged), so leaving the page is the ONLY thing that stops
        /// the stream. Every route off this page must reach here or the panel keeps a session
        /// open with nothing on screen — and these panels have a small session pool, so a leak
        /// eventually breaks every stream on the panel, cameras included.
        /// </summary>
        /// <summary>
        /// The panel went to sleep or woke up. Sleeping drops the door stream.
        ///
        /// This is the THIRD way off the intercom page and the only one that writes no page
        /// descriptor — the program still believes the panel is on the page, because as far
        /// as navigation is concerned it is. Without this a panel that dimmed while sitting
        /// on the intercom page held the door station's RTSP session behind a dark screen,
        /// which on a station serving one session blocks every other panel from seeing the
        /// door.
        ///
        /// Waking deliberately does NOT restore the stream: whoever wakes the panel is
        /// looking at a page whose call ended some time ago, and silently re-opening a
        /// stream nobody asked for would take the session straight back. A new call, or
        /// re-entering the page, starts it again.
        /// </summary>
        public void OnPanelSleepChanged(UI.TouchpanelUI tp, bool asleep)
        {
            if (tp == null || !asleep) { return; }
            if (!HasVideoUrl(tp)) { return; }

            CrestronConsole.PrintLine("{0} INTERCOM TP-{1} panel asleep - clearing video url (releasing the RTSP session)",
                Ts(), tp.Number);
            SetVideoUrl(tp, string.Empty);
        }

        public void SetPageActive(ushort tpNumber, bool active)
        {
            if (active) { return; }

            UI.TouchpanelUI tp;
            if (!_parent.manager.touchpanelZ.TryGetValue(tpNumber, out tp) || tp == null) { return; }
            if (!HasVideoUrl(tp)) { return; }

            CrestronConsole.PrintLine("{0} INTERCOM TP-{1} left the page - clearing video url (releasing the RTSP session)",
                Ts(), tpNumber);
            SetVideoUrl(tp, string.Empty);
        }

        /// <summary>
        /// Finds the configured station for a call: SIP number first (the reliable key),
        /// then a case-insensitive name match, then — when only one station of this
        /// scenario is configured — that one, since a single-door system has no ambiguity
        /// to resolve.
        ///
        /// <paramref name="scenario"/> restricts the search to stations of one kind
        /// (1 = sip/Rava, 2 = unifi). Pass 0 to search all. A call arriving on the VOIP
        /// extender can only be a SIP station, and filtering first is what keeps the
        /// single-station fallback alive in a house that also has a UniFi entry in the
        /// same file.
        /// </summary>
        private StationEntry MatchStation(string sipNumber, string displayName, ushort scenario)
        {
            string ignored;
            return MatchStation(sipNumber, displayName, scenario, out ignored);
        }

        /// <summary>
        /// As above, and reports WHICH key matched. With one door station nobody cares; with
        /// several it is the difference between "the wrong camera came up" and knowing why —
        /// name matching is a substring test and two stations called "Front Door" and
        /// "Front Gate" are one careless rename away from matching each other.
        /// </summary>
        private StationEntry MatchStation(string sipNumber, string displayName, ushort scenario, out string how)
        {
            how = "no match";
            lock (stationsLock)
            {
                var candidates = new List<StationEntry>();
                foreach (var s in stations)
                {
                    if (s == null) { continue; }
                    if (scenario == 0 || s.Scenario == scenario) { candidates.Add(s); }
                }
                if (candidates.Count == 0) { return null; }

                // ⚠ NOT an equality test. The panel reports IncomingURIFeedback, which is a
                // full SIP URI ("sip:1001@192.168.1.50", sometimes with a display name or
                // ";params" attached) — never the bare extension the installer typed into
                // the config. Comparing the whole strings matched nothing on real hardware,
                // which then silently fell through to the name/single-station paths and hid
                // the problem. Compare against the URI's USER PART.
                string user = SipUserPart(sipNumber);
                if (!string.IsNullOrEmpty(user))
                {
                    foreach (var s in candidates)
                    {
                        if (!string.IsNullOrEmpty(s.SipNumber) &&
                            s.SipNumber.Trim().Equals(user, StringComparison.OrdinalIgnoreCase))
                        {
                            how = "sipNumber " + user;
                            return s;
                        }
                    }
                }

                if (!string.IsNullOrEmpty(displayName))
                {
                    foreach (var s in candidates)
                    {
                        if (!string.IsNullOrEmpty(s.Name) &&
                            displayName.IndexOf(s.Name, StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            how = "name \"" + s.Name + "\" in \"" + displayName + "\"";
                            return s;
                        }
                    }
                }

                // On the Rava peer-to-peer path there may be no usable number at all — the
                // 2N is addressed by Crestron device name, not by a SIP account — so this
                // is the path most single-door houses actually resolve on, not a last-ditch
                // guess. It stays deliberately strict: with two door stations, guessing
                // would put the wrong camera on screen, which is worse than none.
                if (candidates.Count == 1)
                {
                    how = "only station configured";
                    return candidates[0];
                }

                // Several stations and nothing matched. Say so loudly with the material
                // needed to fix it, because the symptom (no picture) is identical to every
                // other video failure and the cause is a one-line config edit.
                CrestronConsole.PrintLine("{0} INTERCOM {1} stations configured but none matched num=\"{2}\" name=\"{3}\". Each station's \"name\" must be a substring of the caller's display name, or its \"sipNumber\" must equal the caller URI's user part.",
                    Ts(), candidates.Count, sipNumber, displayName);
                foreach (var s in candidates)
                {
                    CrestronConsole.PrintLine("    station name=\"{0}\" sipNumber=\"{1}\"", s.Name, s.SipNumber);
                }
                return null;
            }
        }

        /// <summary>
        /// Extracts the user part of a SIP URI: "Front Door &lt;sip:1001@10.0.0.5;transport=tcp&gt;"
        /// → "1001". Returns the input trimmed when it is already a bare number/name, so a
        /// panel family that reports a plain extension still works.
        /// </summary>
        private static string SipUserPart(string uri)
        {
            if (string.IsNullOrEmpty(uri)) { return string.Empty; }
            string s = uri.Trim();

            // Strip a display-name wrapper: Name <sip:user@host>
            int lt = s.IndexOf('<');
            if (lt >= 0)
            {
                int gt = s.IndexOf('>', lt + 1);
                s = (gt > lt) ? s.Substring(lt + 1, gt - lt - 1) : s.Substring(lt + 1);
                s = s.Trim();
            }

            // Strip the scheme. `rava:` is here too — on the peer-to-peer path the far end
            // is addressed as rava:DEVICENAME and that name is what shows up.
            foreach (string scheme in new[] { "sip:", "sips:", "rava:" })
            {
                if (s.StartsWith(scheme, StringComparison.OrdinalIgnoreCase))
                {
                    s = s.Substring(scheme.Length);
                    break;
                }
            }

            int at = s.IndexOf('@');
            if (at >= 0) { s = s.Substring(0, at); }

            int semi = s.IndexOf(';');
            if (semi >= 0) { s = s.Substring(0, semi); }

            return s.Trim();
        }

        // ─── Command channel (1561) ─────────────────────────────────────────

        /// <summary>Entry point from TouchpanelUI.SigChange for serial join 1561.</summary>
        public void HandleCommand(ushort tpNumber, string json)
        {
            if (string.IsNullOrEmpty(json)) { return; }

            UI.TouchpanelUI tp;
            if (!_parent.manager.touchpanelZ.TryGetValue(tpNumber, out tp) || tp == null)
            {
                CrestronConsole.PrintLine("Intercom: command from unknown TP-{0}", tpNumber);
                return;
            }

            try
            {
                var obj = JObject.Parse(json);
                string cmd = (string)obj["cmd"] ?? string.Empty;
                CrestronConsole.PrintLine("{0} INTERCOM TP-{1} cmd \"{2}\"", Ts(), tpNumber, cmd);

                if (!tp.HasVoip)
                {
                    CrestronConsole.PrintLine("Intercom: TP-{0} ({1}) has no VOIP extender - \"{2}\" ignored",
                        tpNumber, tp.Type, cmd);
                    return;
                }

                switch (cmd.ToLower())
                {
                    case "answer":
                        tp.VoipAnswer();
                        // Answering is an explicit user action mid-call: keep the idle
                        // suppression alive even if the state sig lags.
                        tp.IntercomCallActive = true;
                        break;
                    case "reject":
                        tp.VoipReject();
                        break;
                    case "hangup":
                        tp.VoipHangup();
                        break;
                    case "dnd":
                        // OUR flag, not tp.VoipDnd(). The panel's native DND declines the
                        // call and the door hears busy for the whole house — see the DND
                        // section above. This toggle only decides whether THIS panel is
                        // disturbed.
                        SetDnd(tp.Number, !IsDnd(tp.Number));
                        break;
                    case "mute":
                        tp.VoipMicMute();
                        break;
                    case "pageall":
                        tp.VoipPageAll();
                        break;
                    default:
                        CrestronConsole.PrintLine("Intercom: TP-{0} unknown cmd \"{1}\"", tpNumber, cmd);
                        return;
                }

                // The extender sig change will republish state, but DND/mute toggles are
                // the ones users tap repeatedly and expect instant feedback on, and some
                // families do not raise a sig event for their own echo. Cheap to be sure.
                SendStateTo(tp);
            }
            catch (Exception ex)
            {
                ErrorLog.Error("Intercom HandleCommand error: {0}", ex.Message);
            }
        }

        // ─── Volume (1564 in / 1565 out) ────────────────────────────────────

        /// <summary>Entry point from TouchpanelUI.SigChange for analog join 1564.</summary>
        public void HandleVolume(ushort tpNumber, ushort value)
        {
            UI.TouchpanelUI tp;
            if (!_parent.manager.touchpanelZ.TryGetValue(tpNumber, out tp) || tp == null) { return; }

            if (!tp.SetPanelSpeakerVolume(value))
            {
                // Log ONCE per panel, not per analog write. HTML throttles the slider to
                // ~10 writes/sec, so an unsupported panel produced pages of identical lines
                // that buried everything else in the console during a drag.
                lock (stateLock)
                {
                    if (!volumeUnsupportedLogged.Add(tpNumber)) { return; }
                }
                CrestronConsole.PrintLine("Intercom: TP-{0} ({1}) has no speaker-volume signal - volume ignored (further writes silent). Run 'intercomdump {0}' for the real member list.",
                    tpNumber, tp.Type);
                return;
            }

            lock (stateLock) { volumeUnsupportedLogged.Remove(tpNumber); }

            // Echo back so the slider tracks what the panel actually accepted rather
            // than only what the finger did.
            try
            {
                if (tp.HTML_UI && tp.UserInterface != null)
                {
                    tp.UserInterface.UShortInput[VolumeFbJoin].UShortValue = tp.GetPanelSpeakerVolume();
                }
            }
            catch (Exception ex)
            {
                ErrorLog.Error("Intercom HandleVolume echo error: {0}", ex.Message);
            }
        }
    }
}
