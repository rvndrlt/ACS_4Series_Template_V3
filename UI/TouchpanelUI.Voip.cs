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

        /// <summary>True when this panel exposes a usable VOIP extender.</summary>
        public bool HasVoip { get { return _voipExtender != null; } }

        // Only dump the member list once per program run — it is long, and every
        // panel of the same family repeats it.
        private static bool _voipMembersDumped;

        // ─── Candidate member names, most-specific first ─────────────────────
        // Each array is one logical function. The first name that resolves on the
        // panel's actual extender wins. Correct these from the startup dump if a
        // family turns out to use a different spelling.
        private static readonly string[] SigAnswer      = { "VOIPAnswer", "Answer" };
        private static readonly string[] SigReject      = { "VOIPReject", "Reject" };
        private static readonly string[] SigHangup      = { "VOIPHangup", "Hangup", "HangUp" };
        private static readonly string[] SigDnd         = { "VOIPDoNotDisturb", "DoNotDisturb" };
        private static readonly string[] SigPageAll     = { "VOIPPageAll", "PageAll" };
        private static readonly string[] SigMicMute     = { "Mute", "VOIPMute", "MicMute" };

        private static readonly string[] FbIncoming     = { "VOIPIncomingCallFeedback", "IncomingCallFeedback" };
        private static readonly string[] FbRinging      = { "VOIPRingingFeedback", "RingingFeedback" };
        private static readonly string[] FbActive       = { "VOIPCallActiveFeedback", "CallActiveFeedback" };
        private static readonly string[] FbBusy         = { "VOIPBusyFeedback", "BusyFeedback" };
        private static readonly string[] FbTerminated   = { "VOIPCallTerminatedFeedback", "CallTerminatedFeedback" };
        private static readonly string[] FbDnd          = { "VOIPDoNotDisturbFeedback", "DoNotDisturbFeedback" };
        private static readonly string[] FbMicMuted     = { "MutedFeedback", "VOIPMutedFeedback" };
        private static readonly string[] FbRegistered   = { "VOIPConnectedtoServerFeedback", "ConnectedtoServerFeedback", "RegisteredFeedback" };

        private static readonly string[] FbCallerName   = { "IncomingDisplayNameFeedback", "VOIPIncomingDisplayNameFeedback" };
        private static readonly string[] FbCallerNumber = { "VOIPInUIDFeedback", "InUIDFeedback", "IncomingCallerIdFeedback" };
        private static readonly string[] FbVideoUrl     = { "VOIPVideoURLFeedback", "VideoURLFeedback" };

        // Panel speaker level (NOT on the VOIP extender — it lives on the audio one).
        private static readonly string[] SigSpeakerVol   = { "SpeakersVolume", "DefaultSpeakerVolume", "LocalAudioVolume" };
        private static readonly string[] FbSpeakerVol    = { "SpeakersVolumeFeedback", "DefaultSpeakerVolumeFeedback", "LocalAudioVolumeFeedback" };

        // Wake: try the screensaver extender first (that is what is actually up when
        // a panel looks "asleep"), then the backlight on the system extender.
        private static readonly string[] SigScreensaverOff = { "ScreensaverOff" };
        private static readonly string[] SigBacklightOn    = { "BacklightOn" };

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

            if (_voipExtender == null)
            {
                CrestronConsole.PrintLine(LogHeader + "TP-{0} ({1}): no VOIP extender - intercom unavailable on this panel type",
                    this.Number, this.Type);
                return;
            }

            if (!_voipMembersDumped)
            {
                _voipMembersDumped = true;
                DumpExtenderMembers(_voipExtender, "VOIP");
                DumpExtenderMembers(_panelAudioExtender, "AUDIO");
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

            CrestronConsole.PrintLine(LogHeader + "TP-{0} ({1}): VOIP extender ready", this.Number, this.Type);
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
                    PropertyInfo prop = this.UserInterface.GetType().GetProperty(name);
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
        /// Fires a command on an extender. Accepts either shape: a parameterless
        /// METHOD (how the documented VOIP actions are exposed) or a BoolInputSig
        /// PROPERTY (which gets a real pulse). Returns false when no candidate name
        /// resolved, so callers can log an unsupported function instead of silently
        /// doing nothing.
        /// </summary>
        private bool FireExtenderCommand(DeviceExtender ext, string[] candidates, string label)
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
                    MethodInfo m = t.GetMethod(name, System.Type.EmptyTypes);
                    if (m != null)
                    {
                        m.Invoke(ext, null);
                        CrestronConsole.PrintLine("INTERCOM TP-{0} {1} -> {2}()", this.Number, label, name);
                        return true;
                    }

                    PropertyInfo p = t.GetProperty(name);
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

            CrestronConsole.PrintLine("INTERCOM TP-{0} {1}: no matching member (tried {2}) - unsupported on this panel",
                this.Number, label, string.Join("/", candidates));
            return false;
        }

        /// <summary>Reads a BoolOutputSig feedback by name. False when absent.</summary>
        private bool ReadExtenderBool(DeviceExtender ext, string[] candidates)
        {
            if (ext == null) { return false; }
            System.Type t = ext.GetType();
            foreach (string name in candidates)
            {
                try
                {
                    PropertyInfo p = t.GetProperty(name);
                    if (p == null) { continue; }
                    var value = p.GetValue(ext, null);
                    var outSig = value as BoolOutputSig;
                    if (outSig != null) { return outSig.BoolValue; }
                    var inSig = value as BoolInputSig;
                    if (inSig != null) { return inSig.BoolValue; }
                }
                catch { /* absent or throwing on this family — treat as false */ }
            }
            return false;
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
                    PropertyInfo p = t.GetProperty(name);
                    if (p == null) { continue; }
                    var value = p.GetValue(ext, null);
                    var outSig = value as StringOutputSig;
                    if (outSig != null) { return outSig.StringValue ?? string.Empty; }
                    var inSig = value as StringInputSig;
                    if (inSig != null) { return inSig.StringValue ?? string.Empty; }
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
                    PropertyInfo p = t.GetProperty(name);
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
                    PropertyInfo p = t.GetProperty(name);
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
        public bool VoipMicMute() { return FireExtenderCommand(_voipExtender, SigMicMute, "micmute"); }

        public bool VoipIncoming     { get { return ReadExtenderBool(_voipExtender, FbIncoming); } }
        public bool VoipRinging      { get { return ReadExtenderBool(_voipExtender, FbRinging); } }
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
            return WriteExtenderUShort(_panelAudioExtender, SigSpeakerVol, value);
        }

        public ushort GetPanelSpeakerVolume()
        {
            return ReadExtenderUShort(_panelAudioExtender, FbSpeakerVol);
        }

        /// <summary>
        /// Brings a sleeping panel forward so an incoming call is actually seen.
        /// Fires screensaver-off AND backlight-on: which one is up depends on the
        /// panel's own standby configuration, both are harmless when already awake,
        /// and neither is reliably observable from here.
        /// </summary>
        public void WakePanel()
        {
            FireExtenderCommand(_screenSaverExtender, SigScreensaverOff, "screensaverOff");
            FireExtenderCommand(_systemExtender, SigBacklightOn, "backlightOn");
        }
    }
}
