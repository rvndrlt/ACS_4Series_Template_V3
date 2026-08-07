//-----------------------------------------------------------------------
// <copyright file="TouchpanelUI.Voip.cs" company="Crestron">
//     Copyright (c) Crestron Electronics. All rights reserved.
// </copyright>
//-----------------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using Crestron.SimplSharp;
using Crestron.SimplSharpPro;

namespace ACS_4Series_Template_V3.UI
{
    /// <summary>
    /// VOIP (SIP intercom) reserved-join access for TSW/TST panels, plus the panel
    /// audio and wake extenders the Intercom page needs alongside it.
    ///
    /// WHY THIS IS ALL REFLECTION
    /// --------------------------
    /// Two independent reasons, both structural:
    ///
    ///  1. Panels in this program are instantiated by reflection from the config
    ///     `type` string (TouchpanelUI.Core.RetrieveUiObject → Assembly.LoadFrom +
    ///     GetType), so `this.UserInterface` is only ever typed as BasicTriList here.
    ///     There is no compile-time panel class to reach `ExtenderVoipReservedSigs`
    ///     through. The existing `_ethernetExtender` code has the same problem and
    ///     solves it by casting to each concrete type; that does not scale to the
    ///     four panel families this feature targets.
    ///
    ///  2. The VOIP extender CLASS DIFFERS PER PANEL FAMILY and the members are not
    ///     a shared interface:
    ///        TSW-x60          → Tswx52VoipReservedSigs
    ///        TSW-x70 / x80    → Tss752VoipReservedSigs
    ///        TST-x80          → (TSW-x70 family)
    ///        TSR-3xx          → Tsr302VoipReservedSigs  (much smaller set)
    ///     There is no common base or interface exposing Answer/Reject/Hangup, so
    ///     even with concrete casts you would need one code path per family.
    ///
    /// Reflecting BY MEMBER NAME collapses both problems: every family that has a
    /// given function exposes it under the same name, and a family that lacks it
    /// simply misses the lookup and no-ops instead of failing to compile.
    ///
    /// ⚠ COMMANDS ARE METHODS, FEEDBACK ARE PROPERTIES. On the documented extenders
    /// the actions (`VOIPAnswer`, `VOIPReject`, ...) are parameterless METHODS, while
    /// the states (`VOIPIncomingCallFeedback`, ...) are PROPERTIES returning output
    /// sigs. Some families instead expose an action as a BoolInputSig property. The
    /// helpers below accept either shape, so do not assume one.
    ///
    /// ⚠ Extenders MUST have Use() called BEFORE UserInterface.Register(), which is
    /// why SetupVoipExtenders() is called from Register() and not lazily.
    ///
    /// The exact member set on Tss752VoipReservedSigs could not be confirmed offline
    /// (the type will not reflection-load outside the SimplSharp runtime), so
    /// SetupVoipExtenders() DUMPS every discovered member to the console on the first
    /// panel that has the extender. Read that dump on the first hardware test — it is
    /// the authoritative list for the panel in front of you, and the candidate-name
    /// arrays below can be corrected from it without touching any other code.
    /// </summary>
    public partial class TouchpanelUI
    {
        // ─── Extender handles (null when the panel family lacks it) ─────────
        private DeviceExtender _voipExtender;
        private DeviceExtender _panelAudioExtender;
        private DeviceExtender _systemExtender;
        private DeviceExtender _screenSaverExtender;

        // Note the delegate is DeviceExtenderJoinChangeEventHandler, not the
        // ...SigChangeEventHandler the event name suggests.
        private DeviceExtenderJoinChangeEventHandler _voipSigHook;
        private DeviceExtenderJoinChangeEventHandler _sleepSigHook;

        // Last observed sleep state, so only TRANSITIONS are reported. These extenders emit
        // an event per sig, and re-clearing the video url on every one of them would be
        // pointless traffic on an already-cleared join.
        private bool _lastPanelAsleep;

        /// <summary>
        /// True when the panel is asleep: screensaver up, or backlight off.
        ///
        /// Both are checked because "asleep" means different things depending on the panel's
        /// standby configuration and they are independent — the same reason WakePanel fires
        /// both ScreensaverOff and BacklightOn rather than picking one.
        ///
        /// ⚠ Backlight is INVERTED: its feedback asserts when the panel is AWAKE. A family
        /// exposing neither member reads false forever, i.e. "never asleep", which is the
        /// safe default — it falls back to the page-change and idle-timeout paths rather
        /// than dropping a stream someone is watching.
        /// </summary>
        public bool PanelAsleep
        {
            get
            {
                if (ReadExtenderBool(_screenSaverExtender, FbScreensaverOn)) { return true; }
                if (FindExtenderMember(_systemExtender, FbBacklightOn) != null &&
                    !ReadExtenderBool(_systemExtender, FbBacklightOn))
                {
                    return true;
                }
                return false;
            }
        }

        /// <summary>
        /// First candidate name that actually exists on an extender, or null. Used to tell
        /// "the member says false" apart from "the member is not there" — which matters for
        /// any inverted feedback, where absent would otherwise read as asleep.
        /// </summary>
        private static PropertyInfo FindExtenderMember(DeviceExtender ext, string[] candidates)
        {
            if (ext == null) { return null; }
            System.Type t = ext.GetType();
            foreach (string name in candidates)
            {
                try
                {
                    PropertyInfo p = FindProperty(t, name);
                    if (p != null) { return p; }
                }
                catch { }
            }
            return null;
        }

        /// <summary>True when this panel exposes a usable VOIP extender.</summary>
        public bool HasVoip { get { return _voipExtender != null; } }

        // Dump the member list once per panel TYPE, not once per program run. The whole
        // point of the dump is that the extender class differs per family, so a fleet
        // with a TSW-1060 and a TST-1080 needs BOTH — an earlier once-globally version
        // dumped whichever panel registered first and hid the other, which is exactly
        // the case you need it for.
        private static readonly HashSet<string> _voipDumpedTypes = new HashSet<string>();

        /// <summary>
        /// Whether to dump every VOIP/AUDIO extender member at panel setup. **Off**: the member
        /// names are known and documented, so this is ~50 lines of noise per panel family on
        /// every boot. `intercomdump` prints it on demand.
        ///
        /// Turn it on when a NEW panel family appears — the classes are undocumented, differ per
        /// family and share no interface, so reading the real member names off the live panel is
        /// the only reliable way to wire one up.
        /// </summary>
        private static bool VoipDumpOnStartup = false;

        // ─── Candidate member names, most-specific first ─────────────────────
        // Each array is one logical function. The first name that resolves on the
        // panel's actual extender wins. Correct these from the startup dump if a
        // family turns out to use a different spelling.
        private static readonly string[] SigAnswer      = { "VOIPAnswer", "Answer" };
        private static readonly string[] SigReject      = { "VOIPReject", "Reject" };
        private static readonly string[] SigHangup      = { "VOIPHangup", "Hangup", "HangUp" };
        private static readonly string[] SigDnd         = { "VOIPDoNotDisturb", "DoNotDisturb" };
        private static readonly string[] SigPageAll     = { "VOIPPageAll", "PageAll" };
        // The one unexplored video lead on Tss752VoipReservedSigs. It takes no arguments
        // and returns nothing, so whatever it does happens ON THE PANEL — most likely it
        // pops the panel's own native door-station preview. Fired only by the
        // `intercompreview` console command; nothing in the normal call path touches it,
        // because a native overlay appearing mid-call would be worse than no video.
        private static readonly string[] SigPreview     = { "Preview", "VOIPPreview" };
        // Mic mute: a BoolInputSig, PULSED (see VoipMicMute for why). `Muted` on
        // Tss752VoipReservedSigs (TSW-x70 / TST-x80), `Mute` on CrestronAppVOIP.
        private static readonly string[] SigMicMute     = { "Muted", "Mute", "MicMute" };

        // ⚠ NOT `IncomingCallFeedback` — on Tss752VoipReservedSigs that name is a
        // **StringOutputSig** (the caller string), so a bool read of it silently returned
        // false forever and `inc` was stuck at 0 through several debug rounds. The BOOL is
        // `IncomingFeedback`. Confirmed from intercomdump on a TST-1080.
        private static readonly string[] FbIncoming     = { "IncomingFeedback", "IncomingCallDetectedFeedback", "VOIPIncomingCallFeedback" };
        // ⚠ MOMENTARY on real hardware. Verified on a TST-1080: this pulses high for
        // ~50ms once per ring burst (measured 3.005s apart), it does NOT stay high for
        // the duration of the call. Never treat it as a latched state — see the latch in
        // IntercomManager.
        private static readonly string[] FbRinging      = { "VOIPRingingFeedback", "RingingFeedback" };
        // Outbound ringback (we called, the far end is ringing) — a genuinely different
        // thing from FbRinging above, which is our own ringer.
        private static readonly string[] FbRingback     = { "VOIPRingbackFeedback", "RingbackFeedback" };
        private static readonly string[] FbActive       = { "VOIPCallActiveFeedback", "CallActiveFeedback" };
        private static readonly string[] FbBusy         = { "VOIPBusyFeedback", "BusyFeedback" };
        private static readonly string[] FbTerminated   = { "VOIPCallTerminatedFeedback", "CallTerminatedFeedback" };
        private static readonly string[] FbDnd          = { "VOIPDoNotDisturbFeedback", "DoNotDisturbFeedback" };
        private static readonly string[] FbMicMuted     = { "MutedFeedback", "VOIPMutedFeedback" };
        private static readonly string[] FbRegistered   = { "ConnectedToServerFeedback", "VOIPConnectedtoServerFeedback", "RegisteredFeedback" };

        // Numeric call state. Present on Tss752VoipReservedSigs and likely more reliable
        // than the individual bools; logged for now rather than trusted, until its value
        // map is known from hardware.
        private static readonly string[] FbCallState    = { "CallStateFeedback" };

        private static readonly string[] FbCallerName   = { "IncomingDisplayNameFeedback", "VOIPIncomingDisplayNameFeedback" };
        // The caller's number/URI. None of the earlier guesses (VOIPInUIDFeedback etc.)
        // exist — the real names are these, which is why `num` was always empty.
        private static readonly string[] FbCallerNumber = { "IncomingURIFeedback", "IncomingCallerInformationFeedback", "IncomingCallFeedback" };
        private static readonly string[] FbVideoUrl     = { "VOIPVideoURLFeedback", "VideoURLFeedback" };

        // Panel speaker level. On TSW-x60/x70 and TST-x80 the audio extender is
        // TsxCcsUcCodec100AudioReservedSigs and the panel's overall output level is
        // **AllAudioVolume** — confirmed by intercomdump. `SpeakersVolume` and friends do
        // not exist there at all, which is why the slider did nothing. Searched on the
        // AUDIO extender first, then the VOIP one, since families differ.
        private static readonly string[] SigSpeakerVol   = { "AllAudioVolume", "SpeakersVolume", "Volume", "DefaultSpeakerVolume", "LocalAudioVolume" };
        private static readonly string[] FbSpeakerVol    = { "AllAudioVolumeFeedback", "SpeakersVolumeFeedback", "VolumeFeedback", "DefaultSpeakerVolumeFeedback", "LocalAudioVolumeFeedback" };

        // Wake: try the screensaver extender first (that is what is actually up when
        // a panel looks "asleep"), then the backlight on the system extender.
        private static readonly string[] SigScreensaverOff = { "ScreensaverOff" };
        private static readonly string[] SigBacklightOn    = { "BacklightOn" };

        // Sleep detection. A sleeping panel is the one way to leave the intercom page
        // WITHOUT writing a page descriptor, so nothing else notices it — which is exactly
        // how a sleeping panel ended up holding the door station's RTSP session with
        // nothing on screen. Names are candidates per family, same as everything else here;
        // whichever resolves is logged once at setup.
        private static readonly string[] FbScreensaverOn = {
            "ScreensaverOnFeedback", "ScreenSaverOnFeedback", "ScreensaverActiveFeedback",
            "ScreenSaverActiveFeedback", "ScreensaverFeedback"
        };
        // Backlight reads the opposite way round (ON means awake), so it is inverted below.
        private static readonly string[] FbBacklightOn = {
            "BacklightOnFeedback", "BackLightOnFeedback", "BacklightFeedback"
        };

        // ─── Setup / teardown ───────────────────────────────────────────────

        /// <summary>
        /// Resolves the VOIP / audio / system / screensaver extenders for this panel,
        /// calls Use() on each, and subscribes to VOIP sig changes.
        ///
        /// MUST be called from Register() BEFORE UserInterface.Register() — an extender
        /// that has not had Use() called before registration is not usable afterwards.
        /// Every step is individually guarded: a panel family that lacks any given
        /// extender is a normal outcome (xpanel has no VOIP at all), not an error.
        /// </summary>
        private void SetupVoipExtenders()
        {
            _voipExtender        = ResolveExtender("ExtenderVoipReservedSigs", "ExtenderVOIPReservedSigs", "ExtenderCrestronAppVOIP");
            _panelAudioExtender  = ResolveExtender("ExtenderAudioReservedSigs", "ExtenderAudioGeneralAudioReservedSigs");
            _systemExtender      = ResolveExtender("ExtenderSystemReservedSigs");
            _screenSaverExtender = ResolveExtender("ExtenderScreenSaverReservedSigs");

            string typeKey = this.Type ?? "(unknown)";

            if (_voipExtender == null)
            {
                // Loud, and it names the panel object's REAL runtime class — the config
                // `type` string alone does not tell you which class got instantiated, and
                // that is usually what is wrong when this fires.
                CrestronConsole.PrintLine(LogHeader + "TP-{0} ({1}, class {2}): NO VOIP EXTENDER - intercom unavailable on this panel",
                    this.Number, typeKey,
                    this.UserInterface != null ? this.UserInterface.GetType().FullName : "(null)");

                // Dump the panel's own extender-ish properties so it is obvious whether the
                // class simply spells it differently or genuinely has none.
                if (_voipDumpedTypes.Add(typeKey + ":none") && this.UserInterface != null)
                {
                    CrestronConsole.PrintLine("INTERCOM available Extender* properties on {0}:",
                        this.UserInterface.GetType().FullName);
                    foreach (PropertyInfo p in this.UserInterface.GetType().GetProperties())
                    {
                        if (p.Name.StartsWith("Extender")) { CrestronConsole.PrintLine("  {0}", p.Name); }
                    }
                }
                return;
            }

            // The member dump is DISCOVERY output, not routine startup logging. It existed
            // because the extender classes differ per panel family, are undocumented, and share
            // no interface — so the real member names had to be read off a live panel. They are
            // known now (and recorded in INTERCOM-HANDOFF.md), so printing ~50 lines per panel
            // family on every boot is just noise that buries the lines that matter.
            //
            // Still one console command away when a new panel family appears: `intercomdump`.
            //
            // The VIDEO-related members are still printed, because the intercom video window is
            // unfinished and that is the part actively being worked on.
            if (_voipDumpedTypes.Add(typeKey))
            {
                if (VoipDumpOnStartup)
                {
                    DumpExtenderMembers(_voipExtender, "VOIP");
                    DumpExtenderMembers(_panelAudioExtender, "AUDIO");
                }
                else
                {
                    DumpVideoRelatedMembers(_voipExtender, "VOIP");
                }
            }

            // Re-read and republish state on ANY voip sig change rather than trying to
            // identify which sig moved. The full state is a handful of sig reads, and
            // identifying sigs would mean holding per-family Sig references — exactly
            // the coupling this file exists to avoid.
            _voipSigHook = (dev, args) =>
            {
                try
                {
                    if (_parent != null && _parent.intercomManager != null)
                    {
                        _parent.intercomManager.OnVoipStateChanged(this);
                    }
                }
                catch (Exception ex)
                {
                    CrestronConsole.PrintLine(LogHeader + "TP-{0} voip sig error: {1}", this.Number, ex.Message);
                }
            };
            _voipExtender.DeviceExtenderSigChange += _voipSigHook;

            SetupSleepHooks();

            CrestronConsole.PrintLine(LogHeader + "TP-{0} ({1}): VOIP extender ready", this.Number, this.Type);
        }

        /// <summary>
        /// Watches the screensaver / backlight extenders so the intercom can drop the door
        /// stream when the panel goes to sleep.
        ///
        /// WHY THIS IS NEEDED SEPARATELY: every other way of leaving the intercom page
        /// writes a page descriptor, and clearing the stream hangs off that. Going to sleep
        /// writes nothing — the panel is still "on" the intercom page as far as the program
        /// is concerned — so a sleeping panel sat holding the 2N's RTSP session with a dark
        /// screen, and on a door station that only serves one session that blocked every
        /// other panel from showing the door.
        /// </summary>
        private void SetupSleepHooks()
        {
            _sleepSigHook = (dev, args) =>
            {
                try
                {
                    bool asleep = this.PanelAsleep;
                    if (asleep == _lastPanelAsleep) { return; }   // sig noise, not a transition
                    _lastPanelAsleep = asleep;

                    if (_parent != null && _parent.intercomManager != null)
                    {
                        _parent.intercomManager.OnPanelSleepChanged(this, asleep);
                    }
                }
                catch (Exception ex)
                {
                    CrestronConsole.PrintLine(LogHeader + "TP-{0} sleep sig error: {1}", this.Number, ex.Message);
                }
            };

            if (_screenSaverExtender != null) { _screenSaverExtender.DeviceExtenderSigChange += _sleepSigHook; }
            if (_systemExtender != null) { _systemExtender.DeviceExtenderSigChange += _sleepSigHook; }

            // Say plainly whether sleep is observable on this panel family. If NEITHER
            // resolves, the stream will only be dropped by a page change or the idle
            // timeout — worth knowing up front rather than discovering it as a stuck stream.
            bool haveScreensaver = FindExtenderMember(_screenSaverExtender, FbScreensaverOn) != null;
            bool haveBacklight = FindExtenderMember(_systemExtender, FbBacklightOn) != null;
            CrestronConsole.PrintLine(LogHeader + "TP-{0} sleep detection: screensaver={1} backlight={2}{3}",
                this.Number,
                haveScreensaver ? "yes" : "no",
                haveBacklight ? "yes" : "no",
                (!haveScreensaver && !haveBacklight)
                    ? "  <- NEITHER: door video will only stop on a page change or the idle timeout"
                    : string.Empty);
        }

        /// <summary>Detaches the VOIP hook. Called from Dispose().</summary>
        private void TeardownVoipExtenders()
        {
            try
            {
                if (_voipExtender != null && _voipSigHook != null)
                {
                    _voipExtender.DeviceExtenderSigChange -= _voipSigHook;
                }
            }
            catch (Exception ex) { Warn("voipExtender", ex); }

            try
            {
                if (_sleepSigHook != null)
                {
                    if (_screenSaverExtender != null) { _screenSaverExtender.DeviceExtenderSigChange -= _sleepSigHook; }
                    if (_systemExtender != null) { _systemExtender.DeviceExtenderSigChange -= _sleepSigHook; }
                }
            }
            catch (Exception ex) { Warn("sleepExtender", ex); }
            _sleepSigHook = null;

            // Kill this panel's inbound watchdog CTimer. A live CTimer is a GC root and
            // would pin the whole previous panel graph across a reload.
            try
            {
                if (_parent != null && _parent.intercomManager != null)
                {
                    _parent.intercomManager.OnPanelDisposed(this.Number);
                }
            }
            catch (Exception ex) { Warn("intercomWatchdog", ex); }

            _voipSigHook = null;
            _voipExtender = null;
            _panelAudioExtender = null;
            _systemExtender = null;
            _screenSaverExtender = null;
        }

        /// <summary>
        /// Finds the first named property on the panel object that is a DeviceExtender
        /// and calls Use() on it. Returns null when the panel family has none of them.
        /// </summary>
        private DeviceExtender ResolveExtender(params string[] propertyNames)
        {
            if (this.UserInterface == null) { return null; }

            foreach (string name in propertyNames)
            {
                try
                {
                    PropertyInfo prop = FindProperty(this.UserInterface.GetType(), name);
                    if (prop == null) { continue; }

                    // Reading the property itself can throw NotSupportedException on the
                    // "Nc" (no-comms) panel variants, which declare it and then refuse.
                    var ext = prop.GetValue(this.UserInterface, null) as DeviceExtender;
                    if (ext == null) { continue; }

                    ext.Use();
                    return ext;
                }
                catch (Exception ex)
                {
                    // TargetInvocationException wraps whatever the property threw.
                    var inner = ex.InnerException != null ? ex.InnerException.Message : ex.Message;
                    CrestronConsole.PrintLine(LogHeader + "TP-{0} extender '{1}' unavailable: {2}",
                        this.Number, name, inner);
                }
            }
            return null;
        }

        /// <summary>
        /// Members whose names suggest they carry video: a URL/URI, a preview, a snapshot or a
        /// stream. Kept deliberately broad — the point is to notice a member that exists rather
        /// than to confirm one already known.
        /// </summary>
        private static readonly string[] VideoMemberHints =
            { "video", "url", "uri", "preview", "image", "snapshot", "stream", "camera", "jpeg", "mjpeg" };

        private static bool LooksVideoRelated(string name)
        {
            if (string.IsNullOrEmpty(name)) { return false; }
            string lower = name.ToLower();
            for (int i = 0; i < VideoMemberHints.Length; i++)
            {
                if (lower.Contains(VideoMemberHints[i])) { return true; }
            }
            return false;
        }

        /// <summary>
        /// Prints only the VIDEO-related members of an extender at startup.
        ///
        /// The intercom video window is still unfinished, so this stays on while the full member
        /// dump does not: it is the part still being worked on, and it is a handful of lines
        /// instead of ~50 per panel family.
        ///
        /// Note what it reports on a TST-1080 / Tss752VoipReservedSigs: `Preview()` and the two
        /// URI feedbacks (`MyURIFeedback`, `IncomingURIFeedback`) — and **no video URL member at
        /// all**. There is no `VOIPVideoURLFeedback` on this class, which is the central fact for
        /// the video work: the panel's own VOIP extender will not hand over a stream URL, so the
        /// door station's RTSP url has to come from config, exactly as the Cameras page does it.
        /// </summary>
        private void DumpVideoRelatedMembers(DeviceExtender ext, string label)
        {
            if (ext == null) { return; }

            try
            {
                System.Type et = ext.GetType();
                var found = new List<string>();

                foreach (PropertyInfo p in et.GetProperties())
                {
                    if (LooksVideoRelated(p.Name))
                    {
                        found.Add("  P " + p.Name + " : " + p.PropertyType.Name);
                    }
                }
                foreach (MethodInfo m in et.GetMethods())
                {
                    if (m.IsSpecialName) { continue; }
                    if (m.DeclaringType == typeof(object)) { continue; }
                    if (m.GetParameters().Length != 0) { continue; }
                    if (LooksVideoRelated(m.Name)) { found.Add("  M " + m.Name + "()"); }
                }

                CrestronConsole.PrintLine("INTERCOM {0} video-related members on TP-{1} ({2}, {3}): {4}",
                    label, this.Number, this.Type, et.Name,
                    found.Count == 0 ? "NONE - no video url available from this extender" : found.Count + " found");
                for (int i = 0; i < found.Count; i++) { CrestronConsole.PrintLine(found[i]); }
            }
            catch (Exception ex)
            {
                CrestronConsole.PrintLine("INTERCOM {0} video member scan failed: {1}", label, ex.Message);
            }
        }

        /// <summary>
        /// Prints every member of an extender to the console. This is the authoritative
        /// list for the panel in front of you — use it to correct the candidate-name
        /// arrays above if a function does not resolve.
        /// </summary>
        private void DumpExtenderMembers(DeviceExtender ext, string label)
        {
            if (ext == null)
            {
                CrestronConsole.PrintLine("INTERCOM {0} extender: (none on TP-{1} type {2})", label, this.Number, this.Type);
                return;
            }

            try
            {
                System.Type et = ext.GetType();
                CrestronConsole.PrintLine("INTERCOM {0} extender on TP-{1} ({2}) = {3}", label, this.Number, this.Type, et.FullName);

                var sb = new StringBuilder();
                foreach (PropertyInfo p in et.GetProperties())
                {
                    sb.Length = 0;
                    sb.Append("  P ").Append(p.Name).Append(" : ").Append(p.PropertyType.Name);
                    CrestronConsole.PrintLine(sb.ToString());
                }
                foreach (MethodInfo m in et.GetMethods())
                {
                    if (m.IsSpecialName) { continue; }
                    if (m.DeclaringType == typeof(object)) { continue; }
                    if (m.GetParameters().Length != 0) { continue; }
                    CrestronConsole.PrintLine("  M {0}()", m.Name);
                }
            }
            catch (Exception ex)
            {
                CrestronConsole.PrintLine("INTERCOM {0} extender dump failed: {1}", label, ex.Message);
            }
        }

        // ─── Generic by-name sig access ─────────────────────────────────────

        /// <summary>
        /// Ambiguity-safe replacement for Type.GetProperty(name). USE THIS EVERYWHERE in
        /// this file — never call GetProperty directly.
        ///
        /// ⚠ WHY: Type.GetProperty(name) THROWS AmbiguousMatchException when a property is
        /// redeclared at more than one level of the inheritance hierarchy, and several
        /// Crestron panel classes do exactly that. Verified on a TSW-1060 (Tsw1060), whose
        /// property list shows ExtenderVoipReservedSigs THREE times — the panel has a
        /// perfectly good VOIP extender and reflection could not reach it, so the intercom
        /// reported "unavailable", never hooked the sig change, and therefore never flipped
        /// the page on an incoming call either. ExtenderSystemReservedSigs (also listed 3x
        /// on that panel) failed identically, which is what confirmed the cause.
        /// TSW-770 / TST-1080 list each name once and were unaffected — which is exactly
        /// how this hid.
        ///
        /// Resolution rule: on ambiguity, take the MOST DERIVED declaration (walk the type
        /// chain from the actual runtime type upward and return the first match), which is
        /// the same one C# overload resolution would pick.
        /// </summary>
        private static PropertyInfo FindProperty(System.Type t, string name)
        {
            if (t == null || string.IsNullOrEmpty(name)) { return null; }

            try
            {
                return t.GetProperty(name);
            }
            catch (AmbiguousMatchException)
            {
                // Redeclared up the hierarchy — walk it and take the most derived.
                for (System.Type cur = t; cur != null; cur = cur.BaseType)
                {
                    PropertyInfo p = cur.GetProperty(name,
                        BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
                    if (p != null) { return p; }
                }
                return null;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>Ambiguity-safe GetMethod for parameterless methods.</summary>
        private static MethodInfo FindMethod(System.Type t, string name)
        {
            if (t == null || string.IsNullOrEmpty(name)) { return null; }
            try
            {
                return t.GetMethod(name, System.Type.EmptyTypes);
            }
            catch (AmbiguousMatchException)
            {
                for (System.Type cur = t; cur != null; cur = cur.BaseType)
                {
                    MethodInfo m = cur.GetMethod(name,
                        BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly,
                        null, System.Type.EmptyTypes, null);
                    if (m != null) { return m; }
                }
                return null;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Fires a command on an extender. Accepts either shape: a parameterless
        /// METHOD (how the documented VOIP actions are exposed) or a BoolInputSig
        /// PROPERTY (which gets a real pulse). Returns false when no candidate name
        /// resolved, so callers can log an unsupported function instead of silently
        /// doing nothing.
        /// </summary>
        private bool FireExtenderCommand(DeviceExtender ext, string[] candidates, string label)
        {
            if (TryFireExtenderCommand(ext, candidates, label)) { return true; }

            CrestronConsole.PrintLine("INTERCOM TP-{0} {1}: no matching member (tried {2}) - unsupported on this panel. Run 'intercomdump {0}' for the real list.",
                this.Number, label, string.Join("/", candidates));
            return false;
        }

        /// <summary>
        /// Same as FireExtenderCommand but SILENT on failure, so a caller can try several
        /// shapes (method, then level sig, then a different extender) without emitting a
        /// misleading "unsupported" line for each attempt.
        /// </summary>
        private bool TryFireExtenderCommand(DeviceExtender ext, string[] candidates, string label)
        {
            if (ext == null) { return false; }
            System.Type t = ext.GetType();

            foreach (string name in candidates)
            {
                try
                {
                    // System.Type must be qualified throughout this file: TouchpanelUI has
                    // its own `Type` property (the panel type string), which shadows the
                    // framework type in any expression position.
                    MethodInfo m = FindMethod(t, name);
                    if (m != null)
                    {
                        m.Invoke(ext, null);
                        CrestronConsole.PrintLine("INTERCOM TP-{0} {1} -> {2}()", this.Number, label, name);
                        return true;
                    }

                    PropertyInfo p = FindProperty(t, name);
                    if (p != null)
                    {
                        var sig = p.GetValue(ext, null) as BoolInputSig;
                        if (sig != null)
                        {
                            sig.Pulse();
                            CrestronConsole.PrintLine("INTERCOM TP-{0} {1} -> {2} pulsed", this.Number, label, name);
                            return true;
                        }
                    }
                }
                catch (Exception ex)
                {
                    var inner = ex.InnerException != null ? ex.InnerException.Message : ex.Message;
                    CrestronConsole.PrintLine("INTERCOM TP-{0} {1} via '{2}' failed: {3}", this.Number, label, name, inner);
                }
            }
            return false;
        }

        /// <summary>
        /// SETS a BoolInputSig by name to an explicit level (as opposed to pulsing it).
        /// Required for level-style controls like `Muted`, where a pulse would toggle the
        /// state on and straight back off again.
        /// </summary>
        private bool SetExtenderBool(DeviceExtender ext, string[] candidates, bool value)
        {
            if (ext == null) { return false; }
            System.Type t = ext.GetType();
            foreach (string name in candidates)
            {
                try
                {
                    PropertyInfo p = FindProperty(t, name);
                    if (p == null) { continue; }
                    var sig = p.GetValue(ext, null) as BoolInputSig;
                    if (sig != null)
                    {
                        sig.BoolValue = value;
                        CrestronConsole.PrintLine("INTERCOM TP-{0} {1} = {2}", this.Number, name, value);
                        return true;
                    }
                }
                catch { /* absent on this family */ }
            }
            return false;
        }

        /// <summary>
        /// Reads a BoolOutputSig feedback by name. False when absent.
        ///
        /// ⚠ A name that EXISTS but is the wrong SIG TYPE is warned about loudly and then
        /// skipped, rather than silently reading as false. That silence is what hid the
        /// worst bug in this feature for three debug rounds: `IncomingCallFeedback` exists
        /// on Tss752VoipReservedSigs but is a **StringOutputSig**, so a bool read of it
        /// returned false forever and the incoming-call flag looked permanently dead. The
        /// bool is `IncomingFeedback`. Never let a type mismatch pass quietly here.
        /// </summary>
        private bool ReadExtenderBool(DeviceExtender ext, string[] candidates)
        {
            if (ext == null) { return false; }
            System.Type t = ext.GetType();
            foreach (string name in candidates)
            {
                try
                {
                    PropertyInfo p = FindProperty(t, name);
                    if (p == null) { continue; }
                    var value = p.GetValue(ext, null);
                    var outSig = value as BoolOutputSig;
                    if (outSig != null) { return outSig.BoolValue; }
                    var inSig = value as BoolInputSig;
                    if (inSig != null) { return inSig.BoolValue; }

                    WarnSigTypeOnce(t, name, "bool", value);
                }
                catch { /* absent or throwing on this family — treat as false */ }
            }
            return false;
        }

        // Type-mismatch warnings are once per (extender type + member) so a per-event read
        // can't turn a genuine warning into console spam.
        private static readonly HashSet<string> _sigTypeWarned = new HashSet<string>();

        private static void WarnSigTypeOnce(System.Type extType, string name, string wanted, object actual)
        {
            string key = extType.Name + "." + name + ":" + wanted;
            lock (_sigTypeWarned)
            {
                if (!_sigTypeWarned.Add(key)) { return; }
            }
            CrestronConsole.PrintLine("INTERCOM ⚠ {0}.{1} exists but is {2}, not a {3} sig - skipped. Fix the candidate list.",
                extType.Name, name, actual == null ? "null" : actual.GetType().Name, wanted);
        }

        /// <summary>Reads a StringOutputSig feedback by name. Empty when absent.</summary>
        private string ReadExtenderString(DeviceExtender ext, string[] candidates)
        {
            if (ext == null) { return string.Empty; }
            System.Type t = ext.GetType();
            foreach (string name in candidates)
            {
                try
                {
                    PropertyInfo p = FindProperty(t, name);
                    if (p == null) { continue; }
                    var value = p.GetValue(ext, null);
                    var outSig = value as StringOutputSig;
                    if (outSig != null) { return outSig.StringValue ?? string.Empty; }
                    var inSig = value as StringInputSig;
                    if (inSig != null) { return inSig.StringValue ?? string.Empty; }

                    WarnSigTypeOnce(t, name, "string", value);
                }
                catch { /* absent on this family */ }
            }
            return string.Empty;
        }

        /// <summary>Writes a UShortInputSig by name. Returns false when absent.</summary>
        private bool WriteExtenderUShort(DeviceExtender ext, string[] candidates, ushort value)
        {
            if (ext == null) { return false; }
            System.Type t = ext.GetType();
            foreach (string name in candidates)
            {
                try
                {
                    PropertyInfo p = FindProperty(t, name);
                    if (p == null) { continue; }
                    var sig = p.GetValue(ext, null) as UShortInputSig;
                    if (sig != null) { sig.UShortValue = value; return true; }
                }
                catch { /* absent on this family */ }
            }
            return false;
        }

        /// <summary>Reads a UShortOutputSig by name. 0 when absent.</summary>
        private ushort ReadExtenderUShort(DeviceExtender ext, string[] candidates)
        {
            if (ext == null) { return 0; }
            System.Type t = ext.GetType();
            foreach (string name in candidates)
            {
                try
                {
                    PropertyInfo p = FindProperty(t, name);
                    if (p == null) { continue; }
                    var value = p.GetValue(ext, null);
                    var outSig = value as UShortOutputSig;
                    if (outSig != null) { return outSig.UShortValue; }
                    var inSig = value as UShortInputSig;
                    if (inSig != null) { return inSig.UShortValue; }
                }
                catch { /* absent on this family */ }
            }
            return 0;
        }

        // ─── Public intercom surface (used by IntercomManager) ───────────────

        public bool VoipAnswer()  { return FireExtenderCommand(_voipExtender, SigAnswer,  "answer"); }
        public bool VoipReject()  { return FireExtenderCommand(_voipExtender, SigReject,  "reject"); }
        public bool VoipHangup()  { return FireExtenderCommand(_voipExtender, SigHangup,  "hangup"); }
        public bool VoipDnd()     { return FireExtenderCommand(_voipExtender, SigDnd,     "dnd"); }
        public bool VoipPageAll() { return FireExtenderCommand(_voipExtender, SigPageAll, "pageall"); }
        /// <summary>
        /// Fires the extender's Preview() — a diagnostic probe, not part of the call path.
        ///
        /// This is the last untested video lead on this hardware: the extender exposes no
        /// video-url member at all (confirmed by dumping the live Tss752VoipReservedSigs),
        /// so the configured RTSP url is the only way to get a picture into OUR page. If
        /// Preview() turns out to open the panel's own door-station view, that is a
        /// different (panel-native, not-in-our-page) answer worth knowing about before
        /// anyone pays for the 2N Enhanced Video licence. Run it during a live call —
        /// there is nothing to preview when idle.
        /// </summary>
        public bool VoipPreview() { return FireExtenderCommand(_voipExtender, SigPreview, "preview"); }
        /// <summary>
        /// Toggles mic mute by PULSING the mute sig.
        ///
        /// ⚠ WHY A PULSE AND NOT A LEVEL WRITE. `Muted` is a BoolInputSig paired with
        /// `MutedFeedback`, which looks like a level control, and it was first implemented
        /// that way — set true to mute, false to unmute. On a TST-1080 the mute worked and
        /// the UNMUTE never did: `Muted = False` produced no sig event and MutedFeedback
        /// stayed high until the call ended. The only reading consistent with that is a
        /// TOGGLE triggered on the RISING edge — the first write latched the sig high, so
        /// no later write ever produced another edge.
        ///
        /// A pulse gives a rising edge on every press, which toggles both directions. It is
        /// also how every other action on this extender behaves (Answer/Hangup/DoNotDisturb
        /// are all momentary), so a momentary mute is the consistent reading.
        ///
        /// If a future panel turns out to genuinely want a level, the tell is that mute
        /// stops working entirely (a pulse would return to unmuted) — at which point
        /// SetExtenderBool is still here for it.
        /// </summary>
        public bool VoipMicMute()
        {
            if (TryFireExtenderCommand(_voipExtender, SigMicMute, "micmute")) { return true; }
            if (TryFireExtenderCommand(_panelAudioExtender, SigMicMute, "micmute")) { return true; }

            CrestronConsole.PrintLine("INTERCOM TP-{0} micmute: no matching member on VOIP or AUDIO extender (tried {1}). Run 'intercomdump {0}'.",
                this.Number, string.Join("/", SigMicMute));
            return false;
        }

        /// <summary>Numeric call state, if the family exposes one. Logged, not trusted.</summary>
        public ushort VoipCallStateCode { get { return ReadExtenderUShort(_voipExtender, FbCallState); } }

        public bool VoipIncoming     { get { return ReadExtenderBool(_voipExtender, FbIncoming); } }
        public bool VoipRinging      { get { return ReadExtenderBool(_voipExtender, FbRinging); } }
        public bool VoipRingback     { get { return ReadExtenderBool(_voipExtender, FbRingback); } }
        public bool VoipCallActive   { get { return ReadExtenderBool(_voipExtender, FbActive); } }
        public bool VoipBusy         { get { return ReadExtenderBool(_voipExtender, FbBusy); } }
        public bool VoipTerminated   { get { return ReadExtenderBool(_voipExtender, FbTerminated); } }
        public bool VoipDndActive    { get { return ReadExtenderBool(_voipExtender, FbDnd); } }
        public bool VoipMicMuted     { get { return ReadExtenderBool(_voipExtender, FbMicMuted); } }
        public bool VoipRegistered   { get { return ReadExtenderBool(_voipExtender, FbRegistered); } }

        public string VoipCallerName   { get { return ReadExtenderString(_voipExtender, FbCallerName); } }
        public string VoipCallerNumber { get { return ReadExtenderString(_voipExtender, FbCallerNumber); } }
        public string VoipVideoUrl     { get { return ReadExtenderString(_voipExtender, FbVideoUrl); } }

        /// <summary>
        /// Sets the panel's SPEAKER volume (0-65535). This is the panel's own output
        /// level, not a call-scoped one — the VOIP extender has no volume signal, so
        /// there is nothing narrower to drive. It therefore also affects every other
        /// sound the panel makes.
        /// </summary>
        public bool SetPanelSpeakerVolume(ushort value)
        {
            // Search the AUDIO extender first, then the VOIP one — the level is not always
            // on the extender you would expect, and families differ.
            return WriteExtenderUShort(_panelAudioExtender, SigSpeakerVol, value)
                || WriteExtenderUShort(_voipExtender, SigSpeakerVol, value);
        }

        public ushort GetPanelSpeakerVolume()
        {
            ushort v = ReadExtenderUShort(_panelAudioExtender, FbSpeakerVol);
            if (v == 0) { v = ReadExtenderUShort(_voipExtender, FbSpeakerVol); }
            return v;
        }

        /// <summary>
        /// Prints this panel's real VOIP and AUDIO extender member lists to the console on
        /// demand (console command `intercomdump &lt;tp&gt;`), plus every `Extender*` property
        /// on the panel object.
        ///
        /// This exists because the startup dump is easy to miss — it scrolls past during
        /// boot, and by the time a control does not work you need the list and cannot get
        /// it back without a restart. Two debug rounds were lost to exactly that.
        /// </summary>
        public void DumpVoipDiagnostics()
        {
            CrestronConsole.PrintLine("=== INTERCOM diagnostics TP-{0} name=\"{1}\" configType={2} HTML_UI={3} ===",
                this.Number, this.Name, this.Type, this.HTML_UI);
            CrestronConsole.PrintLine("  panel class : {0}",
                this.UserInterface != null ? this.UserInterface.GetType().FullName : "(null)");
            CrestronConsole.PrintLine("  HasVoip     : {0}", this.HasVoip);

            if (this.UserInterface != null)
            {
                CrestronConsole.PrintLine("  Extender* properties on the panel class:");
                foreach (PropertyInfo p in this.UserInterface.GetType().GetProperties())
                {
                    if (p.Name.StartsWith("Extender")) { CrestronConsole.PrintLine("    {0}", p.Name); }
                }
            }

            DumpExtenderMembers(_voipExtender, "VOIP");
            DumpExtenderMembers(_panelAudioExtender, "AUDIO");
            DumpExtenderMembers(_systemExtender, "SYSTEM");
            DumpExtenderMembers(_screenSaverExtender, "SCREENSAVER");

            CrestronConsole.PrintLine("  current feedback: inc={0} ring={1} act={2} busy={3} term={4} rb={5} dnd={6} micMuted={7} reg={8} vol={9}",
                VoipIncoming, VoipRinging, VoipCallActive, VoipBusy, VoipTerminated,
                VoipRingback, VoipDndActive, VoipMicMuted, VoipRegistered, GetPanelSpeakerVolume());
            CrestronConsole.PrintLine("=== end INTERCOM diagnostics TP-{0} ===", this.Number);
        }

        /// <summary>
        /// Brings a sleeping panel forward so an incoming call is actually seen.
        /// Fires screensaver-off AND backlight-on: which one is up depends on the
        /// panel's own standby configuration, both are harmless when already awake,
        /// and neither is reliably observable from here.
        /// </summary>
        /// <summary>
        /// Brings the panel out of screensaver / backlight-off.
        ///
        /// Both extenders are fired, not just the first that works: "asleep" can mean the
        /// screensaver is up, the backlight is off, or both, and they are independent.
        ///
        /// Reports when NEITHER resolved. A panel that silently fails to wake looks identical to
        /// a panel that was never asked to wake — which is exactly the confusion that hid the
        /// camera-popup path not calling this at all. Wakes are rare, so logging each one costs
        /// nothing and makes the difference visible.
        /// </summary>
        public void WakePanel()
        {
            bool ss = TryFireExtenderCommand(_screenSaverExtender, SigScreensaverOff, "screensaverOff");
            bool bl = TryFireExtenderCommand(_systemExtender, SigBacklightOn, "backlightOn");

            if (ss || bl) { return; }

            // Two very different situations, which the first version of this message conflated:
            //
            //  1. NO wake extenders at all — xpanel, CrestronOne and other software panels. There
            //     is nothing to wake and nothing wrong; a software panel has no backlight or
            //     screensaver. Reported ONCE per panel type, informationally, because repeating it
            //     on every popup implies a fault that does not exist and buries real output.
            //     (`intercomdump` is useless here: the extenders genuinely do not exist.)
            //
            //  2. Extenders present but neither member resolved — a REAL problem, and the case the
            //     candidate-name arrays exist for. Logged every single time, with the dump hint.
            if (_screenSaverExtender == null && _systemExtender == null)
            {
                string typeKey = (this.Type ?? "(unknown)") + ":nowake";
                if (_voipDumpedTypes.Add(typeKey))
                {
                    CrestronConsole.PrintLine(
                        LogHeader + "TP-{0} ({1}): no screensaver/system extender - cannot wake, and does not need to (software panel). Not reported again for this type.",
                        this.Number, this.Type ?? "(unknown)");
                }
                return;
            }

            CrestronConsole.PrintLine(
                LogHeader + "TP-{0} ({1}): WAKE DID NOTHING - screensaver ext={2}, system ext={3}. Run 'intercomdump {0}' for the real member names.",
                this.Number, this.Type ?? "(unknown)",
                _screenSaverExtender == null ? "none" : _screenSaverExtender.GetType().Name,
                _systemExtender == null ? "none" : _systemExtender.GetType().Name);
        }
    }
}
