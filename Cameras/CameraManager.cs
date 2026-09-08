using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Crestron.SimplSharp;
using Crestron.SimplSharp.CrestronIO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ACS_4Series_Template_V3.Cameras
{
    /// <summary>
    /// Cameras subsystem for HTML panels. Reads a whole-house camera list
    /// (name + RTSP url) from \NVRAM\cameraConfig.json and exposes it to the
    /// panels over raw serial/analog joins — no contract/cse2j involvement, same
    /// style as the page descriptor (1520) and quick actions (1530-1532):
    ///
    ///   1541 (serial,  C#→HTML): catalog { "cameras": [ { "name": "..." }, ... ] } (names only)
    ///   1542 (serial,  HTML→C#): select  { "seq": &lt;n&gt;, "index": &lt;1-based&gt; }
    ///   1544 (analog,  C#→HTML): selected camera number feedback (1-based; 0 = none)
    ///   1545 (serial,  C#→HTML): active RTSP url for the selected camera → ch5-video
    ///
    /// C# is the source of truth for the stream: the RTSP urls never leave the
    /// processor. HTML sends "I want camera N" and C# answers with the url (1545)
    /// + highlight (1544). Selection is tracked per panel so two panels can view
    /// different cameras independently; to mirror all panels instead, change
    /// ApplySelection to write every HTML panel.
    ///
    /// Page-flip: the "Cameras" subsystem routes through the existing descriptor
    /// on serial 1520 (SubsystemPageKey("Cameras") → "cameras"); pageRouter.js
    /// shows the CH5-free page. No work needed here for navigation.
    /// </summary>
    public class CameraManager
    {
        private readonly ControlSystem _parent;

        private const string ConfigFilePath = @"\NVRAM\cameraConfig.json";

        public const ushort CatalogJoin = 1541;    // serial C#→HTML
        public const ushort SelectJoin = 1542;     // serial HTML→C#
        public const ushort SelectedFbJoin = 1544; // analog C#→HTML
        public const ushort UrlJoin = 1545;        // serial C#→HTML
        public const ushort RetryJoin = 1546;      // analog C#→HTML: retry nonce; bump = "re-open the current stream"
        public const ushort GapJoin = 1547;        // analog C#→HTML: per-panel teardown gap (ms) for stop→start

        // ch5-video's own diagnostics, reported by the panel's decoder (HTML→C#).
        // These are the ONLY reliable way to find out why a given stream will not
        // render — the component tells you instead of us guessing about codecs.
        public const ushort VideoStateJoin = 1550;        // analog
        public const ushort VideoErrorCodeJoin = 1551;    // analog
        public const ushort VideoErrorMessageJoin = 1552; // serial
        public const ushort VideoResolutionJoin = 1553;   // serial
        public const ushort VideoRetryCountJoin = 1554;   // analog
        // HTML→C#, diagnostic. What HTML asked of the play gate (1540), which is HTML-owned
        // and therefore invisible here. Without it, "page opened, decoder went idle and never
        // connected" cannot be told apart from "the gate was never raised".
        public const ushort GateReportJoin = 1555;        // serial

        // ─── Doorbell chime ─────────────────────────────────────────────────
        //
        // ⚠ There is NO native way to play a sound on a TSW from this SDK. The audio reserved
        // sigs expose AllAudioOn/Off, volume and key-click only; WavOn/WavOff exist on
        // Tpmc8Base and Mtx3, NOT on TSW. VTPro-e's Sound Manager is a Smart Graphics (.vtz)
        // feature and these panels run an HTML/CH5 project, which has no equivalent. So the
        // chime is synthesized by the web app (Web Audio API) and C# only asks for it.
        //
        // A NONCE, not a digital pulse: a latched digital re-asserts when the EISC/panel link
        // re-establishes, which would ring the doorbell on every program restart and panel
        // reconnect. Same hazard the popup seq guards against, same fix.
        public const ushort ChimeJoin = 1556;             // analog C#→HTML: bump = ring now
        public const ushort ChimeReportJoin = 1557;       // serial HTML→C#: did it actually sound?

        public static bool IsVideoDiagAnalogJoin(uint join)
        {
            return join == VideoStateJoin || join == VideoErrorCodeJoin || join == VideoRetryCountJoin;
        }

        public static bool IsVideoDiagSerialJoin(uint join)
        {
            return join == VideoErrorMessageJoin || join == VideoResolutionJoin;
        }

        /// <summary>
        /// Logs a ch5-video diagnostic event to the Crestron console. Watch this
        /// while selecting a camera on the panel to see exactly why it fails —
        /// state/errorCode/errorMessage/resolution/retryCount come from the
        /// panel's decoder, not from us.
        /// </summary>
        // Millisecond timestamp so the console log can be used to measure how long
        // a stream takes to connect or fail (instead of eyeballing it). Local time,
        // HH:mm:ss.fff.
        private static string Ts()
        {
            return DateTime.Now.ToString("HH:mm:ss.fff");
        }

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

            int sel;
            string camName = "(none)";
            if (selectedByTp.TryGetValue(tpNumber, out sel) && sel >= 1)
            {
                lock (camerasLock)
                {
                    if (sel <= cameras.Count) { camName = cameras[sel - 1].Name ?? "(unnamed)"; }
                }
            }

            CrestronConsole.PrintLine("{0} TP-{1} ch5-video {2} = \"{3}\"  [camera: {4}]",
                Ts(), tpNumber, label, value, camName);

            // Feed the decoder state/errorCode into the auto-retry + adaptive gap.
            // HTML cannot observe these send-joins, so this logic lives here where
            // we DO see them.
            if (join == VideoErrorCodeJoin)
            {
                int code;
                if (int.TryParse(value, out code))
                {
                    lock (retryLock)
                    {
                        lastErrorCodeByTp[tpNumber] = code;
                        StreamAttempt a;
                        if (code != 0 && attemptByTp.TryGetValue(tpNumber, out a) && a != null)
                        {
                            a.TransientErr = code;
                        }
                    }
                }
            }
            else if (join == VideoStateJoin)
            {
                int state;
                if (int.TryParse(value, out state)) { OnVideoState(tpNumber, state); }
            }
            else if (join == VideoResolutionJoin)
            {
                // Carried on the attempt summary. It is the element's CSS box, not the
                // stream — useful as layout evidence, never as proof of decode.
                lock (retryLock) { NoteResolution(tpNumber, value); }
            }
        }

        // ─── Auto-retry on decoder failure ──────────────────────────────────
        //
        // A stream can connect (state 2) and then die a few seconds later with a
        // session error (56529/56532), or fail to open at all — either way it lands
        // on state 7 (FAILED) and STAYS there; ch5-video does not recover on its own
        // and the picture is dead until the play gate is cycled. HTML can't see the
        // state (it's a send-join), so C# is the detector: on a confirmed failure we
        // bump the retry nonce (1546) and HTML re-opens the current stream — the gate
        // stays HTML-owned (its local bridge), so we never fight it over CIP. Capped
        // so a truly dead/unsupported camera doesn't loop forever leaking sessions.
        //
        // Gated on the panel actually being ON the cameras page (SetPageActive, driven
        // from the page descriptor) so a retry never fires after the user navigates
        // away. Timer callbacks run off-thread → retryLock.
        //
        // Adaptive per-panel teardown gap: panels vary wildly in how fast they release
        // an RTSP session (a TSW-1060 is fine at 3s; a TST-1080 leaks sessions and needs
        // much longer). Rather than one global gap that's too slow for fast panels or
        // too fast for slow ones, each panel starts at BaseGapMs and ratchets UP by
        // GapStepMs (capped at MaxGapMs) every time it hits a session error — so it
        // self-tunes to its own hardware. C# owns the value and pushes it to HTML on
        // GapJoin (1547); HTML uses it as the stop→start gap. Codec errors (64533) do
        // NOT raise the gap — more time won't fix an unsupported codec.
        private const int VideoStatePlaying = 2;
        private const int VideoStateFailed = 7;
        private const long FailConfirmMs = 2500; // let a transient state-7 self-recover first
        private const int MaxAutoRetries = 2;

        private const ushort BaseGapMs = 3000;
        private const ushort GapStepMs = 1500;
        private const ushort MaxGapMs = 8000;

        private readonly object retryLock = new object();
        private readonly Dictionary<ushort, int> lastStateByTp = new Dictionary<ushort, int>();
        private readonly Dictionary<ushort, int> lastErrorCodeByTp = new Dictionary<ushort, int>();
        private readonly Dictionary<ushort, int> retryCountByTp = new Dictionary<ushort, int>();
        private readonly Dictionary<ushort, bool> pageActiveByTp = new Dictionary<ushort, bool>();
        private readonly Dictionary<ushort, CTimer> confirmTimerByTp = new Dictionary<ushort, CTimer>();
        private readonly Dictionary<ushort, ushort> retryNonceByTp = new Dictionary<ushort, ushort>();
        private readonly Dictionary<ushort, ushort> gapByTp = new Dictionary<ushort, ushort>();

        // ─── Stall detector (DIAGNOSTIC ONLY — does not retry) ───────────────
        //
        // The auto-retry above only arms on state 7 (Failed). Reported 2026-08-08: a person
        // detection woke the panels and flipped to Cameras, but one panel showed no picture and
        // nothing retried; the next detection a few minutes later worked. That fits a stream
        // that STALLS — sits at state 3/4 and never reaches either 7 or 2 — because a stall
        // declares nothing, so the state-7 trigger never fires and no line is ever logged. Same
        // shape as the websocket that never reported dead.
        //
        // ⚠ THAT IS A THEORY, NOT A MEASUREMENT, which is why this only LOGS. Retrying on an
        // unproven threshold is not free: a false positive forces a real stop→start with a
        // multi-second RTSP teardown gap, making a slow-but-working stream worse. Observed
        // healthy starts are 2-4 s, but the retry path took 10.3 s on TP-1, so the threshold
        // sits well clear of both. If the log below shows a last state of 3 or 4 with no 7, the
        // theory is confirmed and this becomes a retry by routing it into
        // ConfirmFailureAndRetry — the machinery is already there and needs no new logic.
        private const long StallLogMs = 15000;
        private readonly Dictionary<ushort, CTimer> stallTimerByTp = new Dictionary<ushort, CTimer>();

        /// <summary>Caller holds retryLock. CTimers are GC roots — an undisposed one pins the
        /// whole panel graph, the same leak that reloadjson hit, so this is called from every
        /// path that ends a stream's startup: playing, page-away, and fresh selection.</summary>
        private void CancelStall(ushort tpNumber)
        {
            CTimer t;
            if (stallTimerByTp.TryGetValue(tpNumber, out t) && t != null) { t.Stop(); t.Dispose(); }
            stallTimerByTp[tpNumber] = null;
        }

        /// <summary>Caller holds retryLock. Armed on every fresh selection/popup.</summary>
        private void ArmStall(ushort tpNumber)
        {
            CancelStall(tpNumber);
            ushort tp = tpNumber;
            stallTimerByTp[tpNumber] = new CTimer(o => ReportStall(tp), StallLogMs);
        }

        private void ReportStall(ushort tpNumber)
        {
            lock (retryLock)
            {
                stallTimerByTp[tpNumber] = null;
                if (!PageActive(tpNumber)) { return; }

                int state;
                if (lastStateByTp.TryGetValue(tpNumber, out state) && state == VideoStatePlaying) { return; }

                int errCode;
                lastErrorCodeByTp.TryGetValue(tpNumber, out errCode);

                // Error log as well as console: this is rare and unattended by definition —
                // the whole reason it went undiagnosed is that nobody was watching when it
                // happened.
                string msg = string.Format(
                    "Cameras: TP-{0} STALLED - {1}ms after selection, ch5-video state is {2} (err {3}), never reached playing. " +
                    "state 3/4 here = the stall theory is confirmed and the retry should be extended to cover it; " +
                    "state 7 = the existing auto-retry ran and lost, which is a different fault.",
                    tpNumber, StallLogMs, state, errCode);
                CrestronConsole.PrintLine("{0} {1}", Ts(), msg);
                try { ErrorLog.Notice(msg); } catch { }

                // Close the attempt too, so a stall lands in the tallies rather than sitting
                // open until something else supersedes it (which would score as ABORTED).
                FinishAttempt(tpNumber, "STALLED", "no state 2 and no state 7");

                // ⚠ MEASURED 2026-09-01, and it is NOT what this detector was built expecting.
                // Six panels stalled on three consecutive person triggers with state = -1 (the
                // "nothing reported since the attempt began" sentinel) and res = "?" — i.e. the
                // decoder emitted NOTHING. It was never asked to play; it was not struggling.
                // Root cause was the popup being a no-op on a panel already parked on the
                // Cameras page with the same url (see ForceReopen below), which is fixed at
                // source. This stays as the safety net for anything else that leaves a panel
                // silent.
                //
                // Retry ONLY from a silent/idle state. State 3 or 4 means the stream really is
                // connecting, and the original note here is still right that a stop→start would
                // make a slow-but-working stream worse by imposing the teardown gap on it — so
                // those remain log-only until something measures them.
                if (state == -1 || state == 1)
                {
                    int count;
                    retryCountByTp.TryGetValue(tpNumber, out count);
                    if (count >= MaxAutoRetries)
                    {
                        CrestronConsole.PrintLine("{0} Cameras: TP-{1} silent stall - retry budget spent, giving up until reselected",
                            Ts(), tpNumber);
                        return;
                    }
                    retryCountByTp[tpNumber] = count + 1;
                    CrestronConsole.PrintLine("{0} Cameras: TP-{1} silent stall (state {2}) - forcing re-open, retry {3} of {4}",
                        Ts(), tpNumber, state, count + 1, MaxAutoRetries);
                    RequestRetry(tpNumber);
                    // Re-arm so a retry that is also silent is caught rather than ending here,
                    // and open a fresh attempt so the outcome is still measured.
                    BeginAttempt(tpNumber, LastCameraName(tpNumber), "stall-retry");
                    ArmStall(tpNumber);
                }
            }
        }

        /// <summary>Name of the camera a panel currently has selected, for logging.</summary>
        private string LastCameraName(ushort tpNumber)
        {
            int sel;
            if (!selectedByTp.TryGetValue(tpNumber, out sel) || sel < 1) { return "(none)"; }
            lock (camerasLock)
            {
                if (sel <= cameras.Count) { return cameras[sel - 1].Name ?? "(unnamed)"; }
            }
            return "(none)";
        }

        // ─── Attempt outcome tracking (did a picture ACTUALLY appear?) ───────
        //
        // The per-event lines above report what ch5-video said; they do not say whether an
        // attempt to show a camera ENDED in a picture. Reading that out of interleaved
        // per-join lines from several panels is exactly the work this section removes:
        // every attempt to start a stream is opened here and closed with one greppable
        // "CAMSTREAM <outcome>" line carrying the conditions it ran under — trigger,
        // time-to-picture, retries, teardown gap, how long after the panel was woken, and
        // how long after the page went active.
        //
        // ⚠ state 2 (playing) is the ONLY evidence of a picture available on this side.
        // There is no "is it rendering" query, and ch5-video's own resolution event reports
        // the element's CSS box, not the stream — so it proves layout, never decode.
        //
        // An attempt STARTS when a url is pushed to a panel (ApplySelection) and ENDS at
        // the first of:
        //   OK      - reached state 2. Time-to-picture is recorded.
        //   FAILED  - state 7 confirmed and the auto-retry budget is spent.
        //   STALLED - StallLogMs with no state 2 and no state 7 (see the stall detector).
        //   ABORTED - the panel left the Cameras page, or a new selection superseded it.
        //             NOT a failure, but it MUST be counted apart or an unattended popup
        //             nobody looked at is indistinguishable from a stream that never came up.
        //
        // Tallies accumulate per camera and per panel; `camerastats` prints them. A success
        // RATE per trigger is the measurement this feature needs and that no single log line
        // can give — "it worked when I tried it" is how this shipped believing it was fine.
        private class StreamAttempt
        {
            public int Id;
            public string Camera;
            public string Trigger;
            public DateTime Started;
            public ushort Gap;
            public int Retries;
            // An error code seen DURING an attempt that still ended OK. ch5-video reports a
            // transient 64533 "Unsupported codec" ~350ms before settling to state 2 on this
            // system (documented as an audio-track artefact, not actionable). Without holding
            // it separately, that stale code was printed on the success line as err=64533 —
            // a success that reads like a failure is exactly the kind of misleading log that
            // has already cost time here.
            public int TransientErr;
            public double WakeAgeMs = -1;    // ms from the wake call to this attempt
            public double PageActiveMs = -1; // ms from start until the page went active
            public string Resolution = "?";
        }

        private class CamStats
        {
            public int Attempts, Ok, Failed, Stalled, Aborted, Retries;
            public double TotalPlayMs, MaxPlayMs;
            public int LastError;
            public string LastOutcome = "-";
        }

        // A wake older than this belongs to a previous event, not to this attempt.
        private const double WakeAttributionMs = 30000;

        private int attemptSeq;
        private readonly Dictionary<ushort, StreamAttempt> attemptByTp = new Dictionary<ushort, StreamAttempt>();
        private readonly Dictionary<ushort, DateTime> lastWakeByTp = new Dictionary<ushort, DateTime>();
        private readonly Dictionary<string, CamStats> statsByCamera = new Dictionary<string, CamStats>();
        private readonly Dictionary<ushort, CamStats> statsByTp = new Dictionary<ushort, CamStats>();

        private static double MsSince(DateTime t)
        {
            return (DateTime.Now - t).TotalMilliseconds;
        }

        private static string Ms(double v)
        {
            return v < 0 ? "?" : ((long)v).ToString() + "ms";
        }

        /// <summary>
        /// Records that a panel was just woken. The wake-to-stream race is the leading
        /// suspect for a popup that flips the page but shows nothing, and it is invisible
        /// unless the two are timestamped against each other — so the next attempt on this
        /// panel reports its age. Thread-safe (takes retryLock itself).
        /// </summary>
        public void NoteWake(ushort tpNumber)
        {
            lock (retryLock) { lastWakeByTp[tpNumber] = DateTime.Now; }
        }

        /// <summary>Caller holds retryLock.</summary>
        private CamStats StatsForCamera(string camera)
        {
            string key = string.IsNullOrEmpty(camera) ? "(unknown)" : camera;
            CamStats s;
            if (!statsByCamera.TryGetValue(key, out s) || s == null)
            {
                s = new CamStats();
                statsByCamera[key] = s;
            }
            return s;
        }

        /// <summary>Caller holds retryLock.</summary>
        private CamStats StatsForTp(ushort tpNumber)
        {
            CamStats s;
            if (!statsByTp.TryGetValue(tpNumber, out s) || s == null)
            {
                s = new CamStats();
                statsByTp[tpNumber] = s;
            }
            return s;
        }

        /// <summary>Open an attempt for a panel. Caller holds retryLock.</summary>
        private void BeginAttempt(ushort tpNumber, string camera, string trigger)
        {
            // A new url abandons whatever was starting. Closing it explicitly keeps the
            // tallies honest — dropping it on the floor would under-count everything that
            // never came up, which is the exact number this is here to measure.
            FinishAttempt(tpNumber, "ABORTED", "superseded by a new selection");

            attemptSeq++;
            var a = new StreamAttempt
            {
                Id = attemptSeq,
                Camera = string.IsNullOrEmpty(camera) ? "(unknown)" : camera,
                Trigger = string.IsNullOrEmpty(trigger) ? "?" : trigger,
                Started = DateTime.Now,
                Gap = GapFor(tpNumber)
            };

            DateTime woke;
            if (lastWakeByTp.TryGetValue(tpNumber, out woke))
            {
                double age = MsSince(woke);
                if (age <= WakeAttributionMs) { a.WakeAgeMs = age; }
            }

            // A popup applies the selection BEFORE the page flip, so the page is normally
            // not active yet here; SetPageActive fills this in when it arrives. A manual
            // pick from the page itself starts at 0.
            if (PageActive(tpNumber)) { a.PageActiveMs = 0; }

            // The previous stream's last state would otherwise be read as this one's — a
            // stale 2 in particular makes a stall look like a success.
            lastStateByTp[tpNumber] = -1;
            lastErrorCodeByTp[tpNumber] = 0;

            attemptByTp[tpNumber] = a;
            StatsForCamera(a.Camera).Attempts++;
            StatsForTp(tpNumber).Attempts++;

            CrestronConsole.PrintLine("{0} CAMSTREAM START   TP-{1} \"{2}\" trigger={3} gap={4}ms wake+{5} attempt#{6}",
                Ts(), tpNumber, a.Camera, a.Trigger, a.Gap, Ms(a.WakeAgeMs), a.Id);
        }

        /// <summary>Close the open attempt for a panel with an outcome. No-op when none is
        /// open, so every call site can fire unconditionally. Caller holds retryLock.</summary>
        private void FinishAttempt(ushort tpNumber, string outcome, string detail)
        {
            StreamAttempt a;
            if (!attemptByTp.TryGetValue(tpNumber, out a) || a == null) { return; }
            attemptByTp[tpNumber] = null;

            double ms = MsSince(a.Started);
            int state, err;
            lastStateByTp.TryGetValue(tpNumber, out state);
            lastErrorCodeByTp.TryGetValue(tpNumber, out err);

            CamStats cam = StatsForCamera(a.Camera);
            CamStats panel = StatsForTp(tpNumber);
            var both = new CamStats[] { cam, panel };
            foreach (CamStats s in both)
            {
                s.Retries += a.Retries;
                s.LastError = err;
                s.LastOutcome = outcome;
                if (outcome == "OK")
                {
                    s.Ok++;
                    s.TotalPlayMs += ms;
                    if (ms > s.MaxPlayMs) { s.MaxPlayMs = ms; }
                }
                else if (outcome == "FAILED") { s.Failed++; }
                else if (outcome == "STALLED") { s.Stalled++; }
                else { s.Aborted++; }
            }

            string line = string.Format(
                "CAMSTREAM {0,-7} TP-{1} \"{2}\" trigger={3} after {4} state={5} err={6} retries={7} gap={8}ms wake+{9} pageActive+{10} res={11} attempt#{12}{13}",
                outcome, tpNumber, a.Camera, a.Trigger, Ms(ms), state, err, a.Retries, a.Gap,
                Ms(a.WakeAgeMs), Ms(a.PageActiveMs), a.Resolution, a.Id,
                (string.IsNullOrEmpty(detail) ? "" : " - " + detail) +
                (a.TransientErr != 0 ? " (recovered from err " + a.TransientErr + ")" : ""));

            CrestronConsole.PrintLine("{0} {1}", Ts(), line);

            // FAILED/STALLED go to the error log as well: these happen unattended by
            // definition (a 3 a.m. person detection), and a console line nobody was
            // watching is how this went undiagnosed in the first place. ABORTED and OK
            // stay console-only — they are the common cases and would drown the log.
            if (outcome == "FAILED" || outcome == "STALLED")
            {
                try { ErrorLog.Notice(line); } catch { }
            }
        }

        // Panels that chime on a ring. Deliberately NOT every panel: a house-wide chime at 3am
        // is a different product decision from a camera popup, and on this system TP-1 (the
        // TSW-1060) is the only panel that physically exists. Add numbers here to widen it.
        private readonly ushort[] chimePanels = new ushort[] { 1 };
        private ushort chimeNonce;

        /// <summary>
        /// Ask the chime panels to sound the doorbell by bumping the chime nonce (1556).
        ///
        /// ⚠ The panel's webview will refuse to make noise until the page has seen a user
        /// gesture, and an unattended 3am ring is exactly that case. HTML primes its audio on
        /// any touch and reports back on 1557 whether the chime actually sounded — a silent
        /// doorbell that logs nothing is indistinguishable from an event that never arrived,
        /// which is the failure mode this whole subsystem keeps rediscovering.
        /// </summary>
        private void RingChime(int seq)
        {
            chimeNonce = (ushort)(chimeNonce + 1);

            int rung = 0;
            foreach (ushort tpNumber in chimePanels)
            {
                UI.TouchpanelUI tp;
                if (!_parent.manager.touchpanelZ.TryGetValue(tpNumber, out tp) ||
                    tp == null || !tp.HTML_UI || tp.UserInterface == null) { continue; }
                if (!tp.UserInterface.IsOnline)
                {
                    CrestronConsole.PrintLine("{0} Chime: TP-{1} offline - no ring", Ts(), tpNumber);
                    continue;
                }

                try
                {
                    tp.UserInterface.UShortInput[ChimeJoin].UShortValue = chimeNonce;
                    rung++;
                }
                catch (Exception ex)
                {
                    CrestronConsole.PrintLine("{0} Chime: TP-{1} FAILED: {2}", Ts(), tpNumber, ex.Message);
                }
            }

            CrestronConsole.PrintLine("{0} Chime: ring (popup seq {1}) -> {2} panel(s), nonce {3}",
                Ts(), seq, rung, chimeNonce);
        }

        /// <summary>Console-command entry point: ring the chime with no doorbell involved.
        /// The autoplay behaviour can only be tested on the panel, and walking to the door
        /// for each attempt makes that test cost more than it should.</summary>
        public void TestChime()
        {
            RingChime(0);
        }

        /// <summary>Log HTML's report of whether the chime actually sounded (serial 1557).
        /// "blocked" means the webview refused for want of a user gesture — the panel has not
        /// been touched since the page loaded. That is the expected failure and it must be
        /// visible, not silent.</summary>
        public void LogChimeReport(ushort tpNumber, string value)
        {
            if (string.IsNullOrEmpty(value)) { return; }
            CrestronConsole.PrintLine("{0} Chime: TP-{1} {2}", Ts(), tpNumber, value);
            if (value.IndexOf("blocked", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                try { ErrorLog.Notice(string.Format("Chime: TP-{0} {1}", tpNumber, value)); } catch { }
            }
        }

        /// <summary>Log what HTML asked of the play gate (serial 1555). Timestamped so it
        /// interleaves with the ch5-video state lines: a gate raise with no state 4 after it
        /// means the panel declined to start, which is a different fault from never being
        /// asked.</summary>
        public void LogGateReport(ushort tpNumber, string value)
        {
            if (string.IsNullOrEmpty(value)) { return; }
            if (!_parent.logging) { return; }
            CrestronConsole.PrintLine("{0} TP-{1} ch5-video PLAY {2}", Ts(), tpNumber, value);
        }

        /// <summary>Record ch5-video's reported resolution on the open attempt (layout
        /// evidence only — it is the element's CSS box, not the stream). Caller holds
        /// retryLock.</summary>
        private void NoteResolution(ushort tpNumber, string value)
        {
            StreamAttempt a;
            if (attemptByTp.TryGetValue(tpNumber, out a) && a != null) { a.Resolution = value; }
        }

        /// <summary>
        /// Console command `camerastats`: success/failure tallies per camera and per panel,
        /// plus every panel's current teardown gap and open attempt. `camerastats clear`
        /// zeroes the counters so a fresh test run can be measured on its own.
        /// </summary>
        public void PrintStats(string args)
        {
            bool clear = !string.IsNullOrEmpty(args) && args.Trim().ToLower().StartsWith("clear");

            lock (retryLock)
            {
                if (clear)
                {
                    statsByCamera.Clear();
                    statsByTp.Clear();
                    CrestronConsole.PrintLine("Cameras: stream stats cleared");
                    return;
                }

                CrestronConsole.PrintLine("=== ch5-video stream attempts (since program start / last clear) ===");
                CrestronConsole.PrintLine("{0,-22} {1,5} {2,4} {3,4} {4,5} {5,5} {6,5} {7,9} {8,9} {9,7}",
                    "camera", "tries", "ok", "fail", "stall", "abort", "retry", "avg play", "max play", "lastErr");
                foreach (var kv in statsByCamera)
                {
                    CamStats s = kv.Value;
                    double avg = s.Ok > 0 ? s.TotalPlayMs / s.Ok : 0;
                    CrestronConsole.PrintLine("{0,-22} {1,5} {2,4} {3,4} {4,5} {5,5} {6,5} {7,9} {8,9} {9,7}",
                        kv.Key, s.Attempts, s.Ok, s.Failed, s.Stalled, s.Aborted, s.Retries,
                        Ms(avg), Ms(s.MaxPlayMs), s.LastError);
                }

                CrestronConsole.PrintLine("--- per panel ---");
                foreach (var kv in statsByTp)
                {
                    CamStats s = kv.Value;
                    double avg = s.Ok > 0 ? s.TotalPlayMs / s.Ok : 0;
                    int state, err;
                    lastStateByTp.TryGetValue(kv.Key, out state);
                    lastErrorCodeByTp.TryGetValue(kv.Key, out err);

                    StreamAttempt open;
                    string openTxt = "-";
                    if (attemptByTp.TryGetValue(kv.Key, out open) && open != null)
                    {
                        openTxt = "\"" + open.Camera + "\" started " + Ms(MsSince(open.Started)) + " ago";
                    }

                    CrestronConsole.PrintLine(
                        "TP-{0}: tries {1} ok {2} fail {3} stall {4} abort {5} | avg play {6} | gap {7}ms | page {8} | state {9} err {10} | last {11} | in flight: {12}",
                        kv.Key, s.Attempts, s.Ok, s.Failed, s.Stalled, s.Aborted, Ms(avg),
                        GapFor(kv.Key), PageActive(kv.Key) ? "cameras" : "elsewhere", state, err,
                        s.LastOutcome, openTxt);
                }
                CrestronConsole.PrintLine("(OK = ch5-video reached state 2, the only proof of a picture. 'camerastats clear' resets.)");
            }
        }

        private static bool IsSessionError(int code)
        {
            return code == 56529 || code == 56532;
        }

        /// <summary>Current teardown gap for a panel (BaseGapMs until it has ratcheted).</summary>
        private ushort GapFor(ushort tpNumber)
        {
            ushort g;
            return gapByTp.TryGetValue(tpNumber, out g) && g > 0 ? g : BaseGapMs;
        }

        /// <summary>Push a panel's current teardown gap to HTML on 1547.</summary>
        private void SendGap(ushort tpNumber)
        {
            UI.TouchpanelUI tp;
            if (!_parent.manager.touchpanelZ.TryGetValue(tpNumber, out tp) ||
                tp == null || !tp.HTML_UI || tp.UserInterface == null) { return; }
            tp.UserInterface.UShortInput[GapJoin].UShortValue = GapFor(tpNumber);
        }

        /// <summary>Increase a panel's teardown gap after a session error (capped).
        /// Returns true and pushes the new value to HTML if it actually changed.
        /// Caller holds retryLock.</summary>
        private bool BumpGap(ushort tpNumber)
        {
            ushort current = GapFor(tpNumber);
            if (current >= MaxGapMs) { return false; }
            ushort next = (ushort)Math.Min(MaxGapMs, current + GapStepMs);
            gapByTp[tpNumber] = next;
            SendGap(tpNumber);
            CrestronConsole.PrintLine("{0} Cameras: TP-{1} session error - teardown gap {2} -> {3} ms",
                Ts(), tpNumber, current, next);
            return true;
        }

        /// <summary>Wind a panel's teardown gap back down one step after a clean start.
        /// Never below BaseGapMs. Caller holds retryLock.</summary>
        private void DecayGap(ushort tpNumber)
        {
            ushort current = GapFor(tpNumber);
            if (current <= BaseGapMs) { return; }
            ushort next = (ushort)Math.Max(BaseGapMs, current - GapStepMs);
            gapByTp[tpNumber] = next;
            SendGap(tpNumber);
            CrestronConsole.PrintLine("{0} Cameras: TP-{1} clean start - teardown gap {2} -> {3} ms",
                Ts(), tpNumber, current, next);
        }

        /// <summary>
        /// Called from the page-descriptor path: true when this panel is shown the
        /// Cameras page, false when it navigates away (any other page / home). While
        /// false, no auto-retry runs and any pending retry for the panel is cancelled.
        /// </summary>
        public void SetPageActive(ushort tpNumber, bool active)
        {
            lock (retryLock)
            {
                pageActiveByTp[tpNumber] = active;
                if (active)
                {
                    // A popup sets the url first and flips the page second, so this is when
                    // the stream actually became visible. The gap between the two is the
                    // timing suspect for "the page popped but showed nothing".
                    StreamAttempt a;
                    if (attemptByTp.TryGetValue(tpNumber, out a) && a != null && a.PageActiveMs < 0)
                    {
                        a.PageActiveMs = MsSince(a.Started);
                    }
                }
                else
                {
                    CancelConfirm(tpNumber);
                    // Navigating away is not a stall — and leaving this armed would both
                    // report a false one and pin the panel via the timer.
                    CancelStall(tpNumber);
                    // Not a failure either, but it ends the attempt: counting it apart is
                    // what keeps "nobody was looking" from reading as "it never came up".
                    FinishAttempt(tpNumber, "ABORTED", "left the Cameras page");
                    retryCountByTp[tpNumber] = 0;
                }
            }
        }

        private bool PageActive(ushort tpNumber)
        {
            bool a;
            return pageActiveByTp.TryGetValue(tpNumber, out a) && a;
        }

        private void CancelConfirm(ushort tpNumber)
        {
            CTimer t;
            if (confirmTimerByTp.TryGetValue(tpNumber, out t) && t != null) { t.Stop(); t.Dispose(); }
            confirmTimerByTp[tpNumber] = null;
        }

        private void OnVideoState(ushort tpNumber, int state)
        {
            lock (retryLock)
            {
                lastStateByTp[tpNumber] = state;

                if (state == VideoStatePlaying)
                {
                    // Reached playing — there is no stall to report.
                    CancelStall(tpNumber);
                    // Recovered / healthy — cancel any pending retry. We deliberately
                    // do NOT reset the retry budget here: a camera that connects then
                    // dies a few seconds later would otherwise reset on every reconnect
                    // and retry forever. The budget is per-selection (reset on a fresh
                    // pick / page open), so a connect→die→connect→die stream still stops
                    // after MaxAutoRetries.
                    CancelConfirm(tpNumber);
                    // Playing: whatever error preceded this was transient (the attempt kept a
                    // copy). Leaving it latched makes the next line that reads it lie.
                    lastErrorCodeByTp[tpNumber] = 0;

                    // Wind the teardown gap back down on a clean success. The ratchet was
                    // one-way, so a panel that hit session errors days ago stayed slow forever:
                    // TP-1 was measured at gap=6000ms on 2026-09-01 with a connect time of only
                    // ~1.6s, i.e. the gap was ~75% of the 8s the user waited. Decaying on
                    // success keeps the self-tuning while letting a panel recover its speed
                    // once whatever caused the session errors is gone (a reboot, usually).
                    DecayGap(tpNumber);

                    // State 2 is the only evidence a picture exists. This is the success
                    // half of the measurement — without it only failures are recorded and
                    // a rate cannot be computed.
                    FinishAttempt(tpNumber, "OK", null);
                    return;
                }

                if (state == VideoStateFailed && PageActive(tpNumber))
                {
                    CTimer existing;
                    bool pending = confirmTimerByTp.TryGetValue(tpNumber, out existing) && existing != null;
                    if (!pending)
                    {
                        ushort tp = tpNumber;
                        confirmTimerByTp[tpNumber] = new CTimer(o => ConfirmFailureAndRetry(tp), FailConfirmMs);
                    }
                }
            }
        }

        private void ConfirmFailureAndRetry(ushort tpNumber)
        {
            lock (retryLock)
            {
                confirmTimerByTp[tpNumber] = null;
                if (!PageActive(tpNumber)) { return; }

                int state;
                if (lastStateByTp.TryGetValue(tpNumber, out state) && state == VideoStatePlaying) { return; }

                // A session error means the panel opened the new stream before it
                // finished releasing the old one — give THIS panel more teardown time
                // next round. Self-tunes slow leakers (TST-1080) without slowing fast
                // panels (TSW-1060). Codec errors don't get more time (won't help).
                int errCode;
                lastErrorCodeByTp.TryGetValue(tpNumber, out errCode);
                if (IsSessionError(errCode)) { BumpGap(tpNumber); }

                int count;
                retryCountByTp.TryGetValue(tpNumber, out count);
                if (count >= MaxAutoRetries)
                {
                    CrestronConsole.PrintLine("{0} Cameras: TP-{1} stream still failed after {2} retries - giving up until reselected",
                        Ts(), tpNumber, MaxAutoRetries);
                    FinishAttempt(tpNumber, "FAILED", "auto-retry budget spent");
                    return;
                }
                retryCountByTp[tpNumber] = count + 1;
                {
                    // Count the retry on the attempt, not as a new attempt: one user-visible
                    // "show me the camera" that needed three goes is still one attempt, and
                    // splitting it would flatter the success rate.
                    StreamAttempt a;
                    if (attemptByTp.TryGetValue(tpNumber, out a) && a != null) { a.Retries = count + 1; }
                }
                CrestronConsole.PrintLine("{0} Cameras: TP-{1} stream failed (state {2}, err {3}) - auto-retry {4} of {5}",
                    Ts(), tpNumber, state, errCode, count + 1, MaxAutoRetries);
                RequestRetry(tpNumber);
            }
        }

        /// <summary>Ask HTML to re-open the current stream by bumping the retry
        /// nonce (1546). HTML owns the play gate and does the actual stop→gap→start,
        /// so the gate is never driven over CIP. Caller holds retryLock.</summary>
        private void RequestRetry(ushort tpNumber)
        {
            UI.TouchpanelUI tp;
            if (!_parent.manager.touchpanelZ.TryGetValue(tpNumber, out tp) ||
                tp == null || !tp.HTML_UI || tp.UserInterface == null) { return; }

            ushort nonce;
            retryNonceByTp.TryGetValue(tpNumber, out nonce);
            nonce = (ushort)(nonce + 1);
            retryNonceByTp[tpNumber] = nonce;
            tp.UserInterface.UShortInput[RetryJoin].UShortValue = nonce;
        }

        /// <summary>
        /// Force a panel to re-open its stream, whatever it currently thinks it is doing.
        ///
        /// ⚠ THIS IS THE FIX FOR THE 2026-09-01 FAILURE, and the reason is worth keeping:
        /// a popup was a silent NO-OP whenever the panel was already parked on the Cameras
        /// page showing the same camera. Neither layer restarts in that case — cameraList.js
        /// only restarts when the url CHANGES (a person trigger re-sends the same Front Door
        /// url), and pageRouter.openDomPage early-returns when the host is already `is-open`,
        /// so the play gate is never cycled either. Meanwhile the panel had been asleep for
        /// hours and its RTSP session was long dead. Nothing was asked of ch5-video, so it
        /// reported nothing, so the state-7 auto-retry never armed and nothing recovered.
        ///
        /// It looked like progressive degradation because it IS progressive: every popup
        /// leaves one more panel parked on the page, and a parked panel is immune to the
        /// next popup. Only a human navigating away restored it.
        ///
        /// So a popup bumps the retry nonce, which HTML answers with a real stop → teardown
        /// gap → start.
        ///
        /// ⚠ CONDITIONAL, not unconditional — this was the reverse a day earlier and the
        /// 2026-09-01 reboot test is why it changed. A panel reboot restored streaming after
        /// days of failure, which makes the root cause resource exhaustion ON THE PANEL that
        /// accumulates with stream churn. Forcing a stop → start even when the panel is
        /// already playing the requested camera adds a session cycle per event and feeds the
        /// very thing that breaks it. So: re-open only when the panel is NOT currently playing
        /// (state 2). If it is already showing the right camera, the popup needs nothing.
        /// </summary>
        private void ForceReopen(ushort tpNumber)
        {
            lock (retryLock)
            {
                int state;
                if (lastStateByTp.TryGetValue(tpNumber, out state) && state == VideoStatePlaying)
                {
                    CrestronConsole.PrintLine("{0} Cameras: TP-{1} already playing - popup needs no re-open",
                        Ts(), tpNumber);
                    return;
                }
                RequestRetry(tpNumber);
            }
        }

        /// <summary>Reset the auto-retry budget for a panel — a fresh user selection
        /// is not a recovery attempt.</summary>
        private void ResetRetry(ushort tpNumber)
        {
            lock (retryLock)
            {
                CancelConfirm(tpNumber);
                retryCountByTp[tpNumber] = 0;
            }
        }

        private readonly object camerasLock = new object();
        private List<CameraEntry> cameras = new List<CameraEntry>();
        private string catalogJson = "{\"cameras\":[]}";

        // Per-panel current selection (1-based; absent/0 = none).
        private readonly Dictionary<ushort, int> selectedByTp = new Dictionary<ushort, int>();

        public class CameraEntry
        {
            [JsonProperty("name")]
            public string Name { get; set; }

            [JsonProperty("rtspUrl")]
            public string RtspUrl { get; set; }
        }

        private class CameraFile
        {
            [JsonProperty("cameras")]
            public List<CameraEntry> Cameras { get; set; }
        }

        public CameraManager(ControlSystem parent)
        {
            _parent = parent;
        }

        // ─── Config load ────────────────────────────────────────────────────

        public void Load()
        {
            try
            {
                if (File.Exists(ConfigFilePath))
                {
                    string json = File.ReadToEnd(ConfigFilePath, Encoding.UTF8);
                    var loaded = JsonConvert.DeserializeObject<CameraFile>(json);
                    lock (camerasLock)
                    {
                        cameras = (loaded != null && loaded.Cameras != null)
                            ? loaded.Cameras
                            : new List<CameraEntry>();
                        catalogJson = BuildCatalogJson();
                    }
                    CrestronConsole.PrintLine("Cameras: loaded {0} cameras from {1}", cameras.Count, ConfigFilePath);
                }
                else
                {
                    CrestronConsole.PrintLine("Cameras: no config file ({0})", ConfigFilePath);
                    lock (camerasLock)
                    {
                        cameras = new List<CameraEntry>();
                        catalogJson = BuildCatalogJson();
                    }
                }
            }
            catch (Exception ex)
            {
                ErrorLog.Error("Cameras Load error: {0}", ex.Message);
                lock (camerasLock)
                {
                    cameras = new List<CameraEntry>();
                    catalogJson = BuildCatalogJson();
                }
            }
        }

        private string BuildCatalogJson()
        {
            var payload = new
            {
                cameras = cameras.Select(c => new { name = c.Name ?? string.Empty }).ToArray()
            };
            return JsonConvert.SerializeObject(payload);
        }

        // ─── Catalog (1541) ─────────────────────────────────────────────────

        /// <summary>Push the catalog to one HTML panel and re-apply its selection
        /// (boot / panel-online reconnect replay).</summary>
        public void SendCatalogTo(UI.TouchpanelUI tp)
        {
            try
            {
                if (tp == null || !tp.HTML_UI || tp.UserInterface == null) return;
                string json;
                lock (camerasLock) { json = catalogJson; }
                tp.UserInterface.StringInput[CatalogJoin].StringValue = json;

                // Push this panel's teardown gap so HTML starts with the right value
                // (a panel that already ratcheted up keeps it across reconnect).
                lock (retryLock) { SendGap(tp.Number); }

                // Reconnect replay: restore this panel's active stream + highlight.
                int sel;
                if (selectedByTp.TryGetValue(tp.Number, out sel) && sel > 0)
                {
                    ApplySelection(tp, sel, "replay");
                }
                else
                {
                    tp.UserInterface.UShortInput[SelectedFbJoin].UShortValue = 0;
                }
            }
            catch (Exception ex)
            {
                ErrorLog.Error("Cameras SendCatalogTo error: {0}", ex.Message);
            }
        }

        public void SendCatalogToAll()
        {
            foreach (var tp in _parent.manager.touchpanelZ)
            {
                SendCatalogTo(tp.Value);
            }
        }

        /// <summary>
        /// Re-reads \NVRAM\cameraConfig.json and re-pushes the catalog to every
        /// panel so edits take effect without a program restart (console command
        /// "reloadcameras"). Each panel's current selection is re-applied, which
        /// re-sends its RTSP url — so an edited url reconnects on its own; the
        /// HTML side only restarts the stream when the url actually changed.
        /// Caveat: selection is tracked by list index, so if you REORDER or REMOVE
        /// cameras a panel may briefly show the camera now at its old index until
        /// you reselect.
        /// </summary>
        public void Reload()
        {
            Load();
            SendCatalogToAll();
            int count;
            lock (camerasLock) { count = cameras.Count; }
            CrestronConsole.PrintLine("Cameras: reloaded {0} cameras and re-pushed to all panels", count);
        }

        /// <summary>
        /// Camera-popup EISC join numbers (IPID 0xC0 @ 127.0.0.2, shared with the
        /// VizioTVControl program).
        ///
        /// Serial-only with a `seq` counter, deliberately NOT an analog + digital pulse:
        /// re-sending an identical serial raises no sig change, so `seq` is what makes a
        /// repeat event fire — and a single self-describing serial avoids the
        /// "set the value, then pulse the trigger" ordering hazard entirely. Same pattern as
        /// quick actions (1531), camera select (1542) and intercom commands (1561).
        /// </summary>
        public const ushort PopupEiscCommandJoin = 1; // serial, App03 → App01

        private int lastPopupSeq = -1;

        /// <summary>
        /// Entry point from the camera-popup EISC handler in ControlSystem.
        /// Payload: { "seq": &lt;n&gt;, "camera": "Front Gate", "reason": "ring"|"person" }
        /// </summary>
        public void HandlePopupCommand(string json)
        {
            if (string.IsNullOrEmpty(json))
            {
                CrestronConsole.PrintLine("{0} Cameras: empty popup payload on the EISC - ignoring", Ts());
                return;
            }

            // Logged BEFORE parsing and before any guard can drop it. This is the arrival
            // receipt: it proves the payload crossed from App03 into this program, which is the
            // boundary that every "the doorbell did nothing" investigation has to establish
            // first. Everything after it can only narrow the cause.
            CrestronConsole.PrintLine("{0} Cameras: popup payload received: {1}", Ts(), json);

            try
            {
                var obj = JObject.Parse(json);
                int seq = (int?)obj["seq"] ?? 0;
                string camera = (string)obj["camera"] ?? string.Empty;
                string reason = (string)obj["reason"] ?? "external";

                // Ignore a replayed identical seq. The EISC re-asserts its serial values when
                // the link re-establishes (program restart on either side), and without this
                // an App01 restart would pop a camera for an event that happened minutes ago.
                //
                // ⚠ App03 seeds its seq from the clock precisely so a REAL popup can never
                // collide with this guard. It used to restart at 1 on every App03 restart, so
                // the first ring after a redeploy carried seq 1 and — if this program had ever
                // handled a seq 1 — was discarded as a replay. Hence the explicit "this may
                // have been real" wording: if this line ever appears for a press someone
                // actually made, the seeding is what regressed.
                if (seq != 0 && seq == lastPopupSeq)
                {
                    CrestronConsole.PrintLine(
                        "{0} Cameras: popup seq {1} already handled - ignoring as an EISC replay. " +
                        "If a doorbell was ACTUALLY pressed just now, this is a bug: App03's seq should never repeat.",
                        Ts(), seq);
                    return;
                }
                lastPopupSeq = seq;

                CrestronConsole.PrintLine("{0} Cameras: popup command seq={1} camera=\"{2}\" reason={3}",
                    Ts(), seq, camera, reason);
                PopupCameraOnAllPanels(camera, reason);

                // A ring also chimes. Only a ring — a person walking past must not ring the
                // doorbell, or the sound stops meaning "someone is at the door" within a day.
                // After the popup on purpose: the picture is the feature and must not be
                // delayed or endangered by the sound.
                if (string.Equals(reason, "ring", StringComparison.OrdinalIgnoreCase))
                {
                    RingChime(seq);
                }
            }
            catch (Exception ex)
            {
                // Console AND error log: the error log survives without a session attached, but
                // whoever is watching the console during a test needs to see it happen.
                CrestronConsole.PrintLine("{0} Cameras: popup payload FAILED to parse: {1} (payload: {2})",
                    Ts(), ex.Message, json);
                ErrorLog.Error("Cameras HandlePopupCommand error: {0} (payload: {1})", ex.Message, json);
            }
        }

        // ─── Select command (1542) ──────────────────────────────────────────

        /// <summary>Entry point from TouchpanelUI.SigChange for serial join 1542.</summary>
        public void HandleSelect(ushort tpNumber, string json)
        {
            if (string.IsNullOrEmpty(json)) return;
            try
            {
                var obj = JObject.Parse(json);
                int index = (int?)obj["index"] ?? 0; // 1-based
                CrestronConsole.PrintLine("{0} Cameras: TP-{1} select index {2}", Ts(), tpNumber, index);

                int count;
                lock (camerasLock) { count = cameras.Count; }
                if (index < 1 || index > count)
                {
                    CrestronConsole.PrintLine("Cameras: TP-{0} select index {1} out of range (1..{2})", tpNumber, index, count);
                    return;
                }

                selectedByTp[tpNumber] = index;
                if (_parent.manager.touchpanelZ.ContainsKey(tpNumber))
                {
                    ApplySelection(_parent.manager.touchpanelZ[tpNumber], index, "manual");
                }
            }
            catch (Exception ex)
            {
                ErrorLog.Error("Cameras HandleSelect error: {0}", ex.Message);
            }
        }

        // ─── External camera popup ──────────────────────────────────────────
        //
        // Entry point for "something happened, show camera X on the panels now". The
        // trigger lives outside this program — a UniFi Protect doorbell ring or person
        // detection, watched by the VizioTVControl program (App03) and delivered over the
        // camera-popup EISC (IPID 0xC0, serial 1). Kept deliberately generic: this program
        // knows nothing about UniFi, only "pop this camera".

        /// <summary>
        /// Resolves a camera NAME to its 1-based catalog index, or 0 when unknown.
        /// Case-insensitive, trimmed.
        ///
        /// Name rather than index is the wire format on purpose: the index is just the
        /// position in cameraConfig.json, so an index would silently point at the wrong
        /// camera the first time that file is reordered.
        /// </summary>
        public int IndexOfCameraName(string name)
        {
            if (string.IsNullOrEmpty(name)) { return 0; }
            string wanted = name.Trim();

            lock (camerasLock)
            {
                for (int i = 0; i < cameras.Count; i++)
                {
                    string n = cameras[i].Name;
                    if (!string.IsNullOrEmpty(n) &&
                        n.Trim().Equals(wanted, StringComparison.OrdinalIgnoreCase))
                    {
                        return i + 1; // 1-based on the wire
                    }
                }
            }
            return 0;
        }

        /// <summary>
        /// Selects a camera by name and forces the Cameras page onto every HTML panel.
        /// `reason` is free text used only for logging ("ring", "person", "manual").
        /// </summary>
        public void PopupCameraOnAllPanels(string cameraName, string reason)
        {
            int index = IndexOfCameraName(cameraName);
            if (index < 1)
            {
                // Loud, and it lists what IS available — a name mismatch between this
                // program's cameraConfig.json and whatever fired the trigger is the most
                // likely failure here, and it would otherwise be completely silent.
                string available;
                lock (camerasLock)
                {
                    available = cameras.Count == 0
                        ? "(catalog empty)"
                        : string.Join(", ", cameras.Select(c => "\"" + (c.Name ?? "") + "\"").ToArray());
                }
                CrestronConsole.PrintLine("{0} Cameras: popup for \"{1}\" IGNORED - no such camera. Catalog: {2}",
                    Ts(), cameraName, available);
                return;
            }

            int popped = 0;
            int skippedOffline = 0;
            foreach (var kv in _parent.manager.touchpanelZ)
            {
                var tp = kv.Value;
                if (tp == null || !tp.HTML_UI || tp.UserInterface == null) { continue; }

                // ⚠ SKIP PANELS THAT ARE NOT ACTUALLY THERE. A configured-but-absent panel
                // still has a live UserInterface object, so it passed every test above and got
                // a full popup — url, page flip, stall timer. It then reports nothing, because
                // there is no panel, and lands in the log as a STALLED attempt with state -1.
                // On 2026-09-01 five of six "failing panels" in the error log were ghosts, and
                // that noise made a real single-panel fault look like a house-wide one. An
                // offline panel is not a failure and must not be counted as one.
                if (!tp.UserInterface.IsOnline)
                {
                    skippedOffline++;
                    continue;
                }

                selectedByTp[tp.Number] = index;

                // ⚠ PER-PANEL ISOLATION. Without it, one panel throwing anywhere below abandons
                // every panel after it — and because the wake call was added to the front of this
                // block, a wake failure on the FIRST panel would silently kill the entire popup.
                // That is indistinguishable from "the trigger never arrived", which is exactly the
                // ambiguity that has made this feature hard to debug.
                try
                {
                    // WAKE FIRST. A popup on a sleeping panel is useless — the page flip happens
                    // behind a dark screen and nobody sees the person at the door, which is the
                    // entire point of the feature. Mirrors IntercomManager, which wakes before
                    // flipping for the same reason.
                    //
                    // Wrapped separately from the page flip: waking is the NICE-TO-HAVE and the
                    // page flip is the feature. A wake that throws must never cost the flip.
                    try
                    {
                        tp.WakePanel("camera popup");
                        // Timestamped so the attempt log can report how long after the wake
                        // the stream was asked for. A panel that has just lit up may not be
                        // ready to open an RTSP session yet, which is the standing theory for
                        // popups that show no picture — this is what will confirm or kill it.
                        NoteWake(tp.Number);
                    }
                    catch (Exception wakeEx)
                    {
                        CrestronConsole.PrintLine("Cameras: TP-{0} wake failed ({1}) - continuing with the page flip",
                            tp.Number, wakeEx.Message);
                    }

                    // ⚠ SELECTION BEFORE THE PAGE FLIP. ApplySelection sets the RTSP url, and
                    // ch5-video will not re-open a stream whose url changes while it is already
                    // playing — so a url that lands after the page opens costs a full stop/start
                    // with the ~3s RTSP teardown gap. Setting it first means the gate comes up
                    // already pointing at the right stream. Same ordering rule as
                    // IntercomManager.OnVoipStateChanged.
                    ApplySelection(tp, index, reason ?? "popup");
                    tp.ShowCamerasPage();
                    ForceReopen(tp.Number);
                    popped++;
                }
                catch (Exception ex)
                {
                    CrestronConsole.PrintLine("Cameras: TP-{0} popup FAILED: {1}", tp.Number, ex.Message);
                    ErrorLog.Error("Cameras popup failed for TP-{0}: {1} | {2}", tp.Number, ex.Message, ex.StackTrace);
                }
            }

            if (popped == 0)
            {
                // Everything upstream worked and the house still saw nothing. Says so in those
                // words, and names what it looked at — a panel dictionary that is empty, or full
                // of panels that are not HTML_UI, is otherwise a completely silent dead end.
                CrestronConsole.PrintLine(
                    "{0} Cameras: popup \"{1}\" (index {2}, reason {3}) reached NO PANELS - {4} panel(s) known, none of them HTML_UI with a live UserInterface",
                    Ts(), cameraName, index, reason ?? "?", _parent.manager.touchpanelZ.Count);
            }
            else
            {
                CrestronConsole.PrintLine("{0} Cameras: popup \"{1}\" (index {2}, reason {3}) -> {4} HTML panel(s){5}",
                    Ts(), cameraName, index, reason ?? "?", popped,
                    skippedOffline > 0 ? " (" + skippedOffline + " offline, skipped)" : "");
            }
        }

        /// <summary>Drive one panel's active RTSP url (1545) + selected-number
        /// highlight (1544) for a 1-based camera index. `trigger` is free text used only
        /// for the attempt log ("manual", "ring", "person", "replay") — knowing WHICH path
        /// asked for the stream is most of the diagnosis, since the popup path (asleep
        /// panel, page flip after the url) and a hand pick on an open page fail
        /// differently.</summary>
        private void ApplySelection(UI.TouchpanelUI tp, int index, string trigger)
        {
            if (tp == null || !tp.HTML_UI || tp.UserInterface == null) return;

            // Fresh selection (or reconnect replay) — not a recovery, so clear the
            // auto-retry budget and cancel any pending retry for this panel.
            ResetRetry(tp.Number);

            string url = string.Empty;
            string camName = "(unknown)";
            lock (camerasLock)
            {
                if (index >= 1 && index <= cameras.Count)
                {
                    url = cameras[index - 1].RtspUrl ?? string.Empty;
                    camName = cameras[index - 1].Name ?? "(unnamed)";
                }
            }

            tp.UserInterface.StringInput[UrlJoin].StringValue = url;
            tp.UserInterface.UShortInput[SelectedFbJoin].UShortValue = (ushort)index;
            CrestronConsole.PrintLine("{0} TP-{1} camera {2} -> url set (len {3})", Ts(), tp.Number, index, url.Length);

            // Start the stall clock only when a stream was actually requested. Arming on an
            // empty url (unknown index) would report a stall for a panel that was never asked
            // to play anything. ReportStall re-checks PageActive when it fires, so arming here —
            // before ShowCamerasPage, which is the required order for ch5-video — is safe.
            if (url.Length > 0)
            {
                lock (retryLock)
                {
                    // A reconnect replay on a panel that is NOT on the Cameras page is not an
                    // attempt to show anything — it only restores state. Measured on panel
                    // re-online 2026-09-01: it opened an attempt and the home descriptor closed
                    // it 2ms later as ABORTED, which is pure noise in the tallies. Every other
                    // trigger arms normally, including a popup (whose page flip follows the url
                    // by design, so PageActive is legitimately false right here).
                    if (trigger == "replay" && !PageActive(tp.Number))
                    {
                        return;
                    }

                    // Order matters: BeginAttempt clears the previous stream's last state so
                    // ArmStall's own state test cannot read a stale 2 as "already playing".
                    BeginAttempt(tp.Number, camName, trigger);
                    ArmStall(tp.Number);
                }
            }
        }
    }
}
