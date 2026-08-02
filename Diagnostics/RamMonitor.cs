using System;
using System.Text;
using Crestron.SimplSharp;
using Crestron.SimplSharpPro.Diagnostics;

namespace ACS_4Series_Template_V3.Diagnostics
{
    /// <summary>
    /// Hourly unattended memory trace. Writes one line per sample to the processor error log so a
    /// slow leak can be characterised without anyone sitting at a console.
    ///
    /// Each line carries the raw `ramfree` console output (so the numbers match exactly what you
    /// see typing `ramfree` by hand) PLUS the program-side counters that say WHICH KIND of leak it
    /// is. Read them together:
    ///
    ///   ramfree used% climbs  +  heap flat  +  subs flat   -> NATIVE leak (sockets/SSL/timers/
    ///                                                        firmware). Not managed event handlers.
    ///   ramfree used% climbs  +  heap climbs               -> managed leak. Check `subs` next.
    ///   subs climbs                                        -> a room-event '+=' is missing its
    ///                                                        '-='. Run `subcounts` for the
    ///                                                        per-room/per-event breakdown.
    ///   volmute > room count                               -> music-sharing unsubscribe ordering.
    ///   tps grows across samples                           -> reload leaked the old panel graph.
    ///
    /// Logged at Warn level deliberately: Notice is filtered out at some log levels and the whole
    /// point is that the trace is still there when you come back to it a day later. The [RAM] tag
    /// makes it obvious these are not failures.
    /// </summary>
    public class RamMonitor
    {
        private const string Tag = "[RAM]";
        private const long DefaultIntervalMs = 60 * 60 * 1000; // 1 hour

        private readonly ControlSystem _cs;
        private CTimer _timer;
        private long _intervalMs = DefaultIntervalMs;
        private int _sampleNumber;
        private DateTime _startedAt;

        // First sample's values, so every line can show drift-since-start without anyone
        // having to diff the log by hand.
        private bool _haveBaseline;
        private long _baselineRamFree;
        private long _baselineHeap;
        private int _baselineSubs;

        public RamMonitor(ControlSystem cs)
        {
            _cs = cs;
        }

        public bool IsRunning { get { return _timer != null; } }

        /// <summary>
        /// (Re)starts the hourly sampler. Safe to call repeatedly — an existing timer is stopped and
        /// disposed first, so a reload can't leave a second sampler running (the exact bug this class
        /// exists to detect).
        /// </summary>
        public void Start(long intervalMs = DefaultIntervalMs)
        {
            Stop();

            if (intervalMs <= 0) return;

            _intervalMs = intervalMs;
            _startedAt = DateTime.Now;
            _sampleNumber = 0;
            _haveBaseline = false;

            // First sample fires immediately so the log always has a boot baseline to diff against;
            // after that it repeats on the interval.
            _timer = new CTimer(o => Sample("auto"), null, 0, _intervalMs);

            ErrorLog.Warn("{0} monitor started, interval {1} min", Tag, _intervalMs / 60000);
        }

        public void Stop()
        {
            if (_timer == null) return;
            try { _timer.Stop(); _timer.Dispose(); }
            catch { }
            _timer = null;
        }

        /// <summary>
        /// Takes one sample and writes it to the error log. `reason` distinguishes the hourly
        /// samples from ones you forced with the `ramlog` console command.
        /// </summary>
        public void Sample(string reason)
        {
            try
            {
                _sampleNumber++;

                long ramFree, ramTotal;
                string rawRamFree;
                ReadRam(out ramTotal, out ramFree, out rawRamFree);
                long heap = GC.GetTotalMemory(false);

                int subs = 0;
                int rooms = 0;
                int volmute = 0;
                int tps = 0;
                CollectProgramCounters(out subs, out rooms, out volmute, out tps);

                if (!_haveBaseline)
                {
                    _baselineRamFree = ramFree;
                    _baselineHeap = heap;
                    _baselineSubs = subs;
                    _haveBaseline = true;
                }

                double usedPct = (ramTotal > 0 && ramFree >= 0) ? (ramTotal - ramFree) * 100.0 / ramTotal : -1.0;
                double upHours = (DateTime.Now - _startedAt).TotalHours;

                // One line, fixed field order, so it greps and diffs cleanly out of `err`.
                ErrorLog.Warn(
                    "{0} #{1} {2} up={3:F1}h used={4:F1}% free={5:N0} (drift {6:N0}) heap={7:N0} (drift {8:N0}) subs={9} (drift {10}) rooms={11} volmute={12} tps={13}",
                    Tag,
                    _sampleNumber,
                    reason,
                    upHours,
                    usedPct,
                    ramFree,
                    ramFree - _baselineRamFree,
                    heap,
                    heap - _baselineHeap,
                    subs,
                    subs - _baselineSubs,
                    rooms,
                    volmute,
                    tps);

                // The verbatim `ramfree` text, so these numbers are directly comparable to what the
                // console prints. Kept on its own line because the console response is multi-line.
                if (!string.IsNullOrEmpty(rawRamFree))
                    ErrorLog.Warn("{0} #{1} ramfree: {2}", Tag, _sampleNumber, rawRamFree);
                else
                    ErrorLog.Warn("{0} #{1} ramfree: <console command returned nothing>", Tag, _sampleNumber);
            }
            catch (Exception ex)
            {
                ErrorLog.Error("{0} sample error: {1}", Tag, ex.Message);
            }
        }

        /// <summary>
        /// Reads processor RAM, preferring the `ramfree` console command over SystemMonitor.
        ///
        /// SystemMonitor.RAMFree / TotalRAMSize return 0 on this DIN-AP4 — no exception, just zero —
        /// so they cannot be trusted as the primary source. The console command is the same thing you
        /// get typing `ramfree` by hand, so parsing it keeps the logged numbers directly comparable
        /// to a manual check. SystemMonitor is kept only as a fallback for platforms where it works
        /// and the console command does not.
        ///
        /// Outputs are -1 when a value could not be determined, so a broken reading is obvious in the
        /// log rather than silently looking like "0 bytes free".
        /// </summary>
        internal static void ReadRam(out long total, out long free, out string raw)
        {
            total = -1;
            free = -1;
            raw = RawRamFree();

            if (!string.IsNullOrEmpty(raw))
            {
                // Expected shape:
                //   92 percent of memory in use
                //   1026498560 total bytes of physical memory
                //   942604288 bytes actually used
                //   83894272 bytes free
                total = FindNumberBefore(raw, "total bytes of physical memory");
                free = FindNumberBefore(raw, "bytes free");
            }

            if (total <= 0)
            {
                try { long t = SystemMonitor.TotalRAMSize; if (t > 0) total = t; }
                catch { }
            }
            if (free < 0)
            {
                try { long f = SystemMonitor.RAMFree; if (f > 0) free = f; }
                catch { }
            }
        }

        /// <summary>
        /// Pulls the number immediately preceding <paramref name="marker"/> out of the flattened
        /// ramfree text. Deliberately hand-rolled rather than regex — the format is fixed and this
        /// avoids a dependency in a diagnostic that must never throw. Returns -1 when not found.
        /// </summary>
        private static long FindNumberBefore(string text, string marker)
        {
            try
            {
                int at = text.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
                if (at < 0) return -1;

                // Walk back over the space(s) between the number and the marker, then over the digits.
                int end = at - 1;
                while (end >= 0 && char.IsWhiteSpace(text[end])) end--;
                if (end < 0 || !char.IsDigit(text[end])) return -1;

                int start = end;
                while (start >= 0 && char.IsDigit(text[start])) start--;
                start++;

                long value;
                if (long.TryParse(text.Substring(start, end - start + 1), out value)) return value;
                return -1;
            }
            catch
            {
                return -1;
            }
        }

        /// <summary>
        /// Runs the real `ramfree` console command and flattens its multi-line response onto one
        /// log line. Returns empty if the platform refuses the command.
        /// </summary>
        private static string RawRamFree()
        {
            try
            {
                string response = string.Empty;
                if (!CrestronConsole.SendControlSystemCommand("ramfree", ref response))
                    return string.Empty;
                if (string.IsNullOrEmpty(response))
                    return string.Empty;

                var sb = new StringBuilder();
                foreach (string line in response.Split('\n'))
                {
                    string t = line.Trim().TrimEnd('\r');
                    if (t.Length == 0) continue;
                    if (sb.Length > 0) sb.Append(" | ");
                    sb.Append(t);
                }
                return sb.ToString();
            }
            catch
            {
                return string.Empty;
            }
        }

        /// <summary>
        /// Totals the program-side counters that distinguish a managed handler leak from a native
        /// one. Deliberately tolerant — a half-initialised system during a reload must not throw
        /// and kill the sampler.
        /// </summary>
        private void CollectProgramCounters(out int subs, out int rooms, out int volmute, out int tps)
        {
            subs = 0;
            rooms = 0;
            volmute = 0;
            tps = 0;

            try
            {
                if (_cs == null || _cs.manager == null) return;

                if (_cs.manager.RoomZ != null)
                {
                    foreach (var kvp in _cs.manager.RoomZ)
                    {
                        if (kvp.Value == null) continue;
                        rooms++;
                        string breakdown;
                        subs += kvp.Value.GetEventSubscriberCounts(out breakdown);
                    }
                }

                if (_cs.manager.touchpanelZ != null)
                {
                    foreach (var kvp in _cs.manager.touchpanelZ)
                    {
                        if (kvp.Value == null) continue;
                        tps++;
                        if (kvp.Value.MuteChangeHandlers != null) volmute += kvp.Value.MuteChangeHandlers.Count;
                        if (kvp.Value.VolumeChangeHandlers != null) volmute += kvp.Value.VolumeChangeHandlers.Count;
                    }
                }
            }
            catch
            {
                // Counters are best-effort; the RAM figures are the point.
            }
        }
    }
}
