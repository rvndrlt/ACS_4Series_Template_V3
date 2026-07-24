using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Crestron.SimplSharp;
using Crestron.SimplSharp.CrestronIO;

namespace ACS_4Series_Template_V3.ConfigEditor
{
    /// <summary>
    /// Backs up the active config AND the quick-actions store to removable media
    /// (USB / SD card) after the Config Editor / Quick Actions have been idle for a
    /// few minutes.
    ///
    /// Flow:
    ///  - Every config-editor read/write (ConfigRouteHandler GET/POST) and every
    ///    quick-action save (QuickActionManager) calls NotifyActivity(), which
    ///    (re)arms a debounce timer. The backup only runs once the timer elapses with
    ///    no further activity — i.e. the user is done making changes for now.
    ///  - Both tracked files are backed up together, each a NEW file stamped with the
    ///    same date + time; prior backups are never overwritten.
    ///  - Removable media is auto-detected and model-agnostic: it works whether the
    ///    processor exposes a USB stick or an SD card, under any mount name, so no
    ///    path is hard-coded. If nothing is inserted, the backup is simply skipped.
    ///  - Retention is count-based, PER FILE: only the newest MaxBackups of each are
    ///    kept; once the cap is reached each new backup prunes the oldest. The files
    ///    are small (tens of KB), so 500 each is a few tens of MB — trivial on 4 GB+.
    /// </summary>
    public class ConfigBackupManager
    {
        // ── Tunables ──
        private const int    IdleMinutes      = 5;                    // idle-after-edit delay before a backup
        private const int    MaxBackups       = 500;                  // keep newest N PER FILE; oldest overwritten beyond this
        private const string BackupDirName    = "ACS_ConfigBackups";  // folder created on the card
        private const string NvramDir         = @"\nvram\";
        private const string QuickActionsFile = @"\NVRAM\quickActions.json"; // HTML-panel quick-action store

        // ── Tracked files: each is versioned + capped independently on the card. ──
        private sealed class BackupTarget
        {
            public string Prefix;            // backup filename prefix on the card
            public Func<string> ResolveSource; // returns the current source path, or null if none
        }
        private readonly List<BackupTarget> _targets;

        public ConfigBackupManager()
        {
            _targets = new List<BackupTarget>
            {
                // Active config (highest-version ACSconfig in NVRAM — what the editor edits).
                new BackupTarget { Prefix = "ACSconfig",    ResolveSource = FindLatestConfigFile },
                // HTML quick-actions store.
                new BackupTarget { Prefix = "quickActions", ResolveSource = () => SafeFileExists(QuickActionsFile) ? QuickActionsFile : null }
            };
        }

        // Root volumes that are NOT removable media. Anything else at "\" is treated
        // as a candidate card. Covers what a 4-series appliance exposes internally.
        private static readonly HashSet<string> InternalVolumes =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "ftp", "html", "logs", "nvram", "romdisk", "simpl", "sshbanner", "sys",
                "user", "opt", "dev", "proc", "tmp", "bin", "lib", "etc", "var", "run"
            };

        private readonly object _lock = new object();
        private CTimer _idleTimer;
        private bool _backupRunning;

        // ── Activity signal — called on config-editor reads/writes and quick-action saves ──
        public void NotifyActivity()
        {
            lock (_lock)
            {
                if (_idleTimer != null)
                {
                    _idleTimer.Stop();
                    _idleTimer.Dispose();
                }
                // Debounce: fire only after IdleMinutes with no further activity.
                _idleTimer = new CTimer(o => OnIdleElapsed(), IdleMinutes * 60 * 1000);
            }
        }

        private void OnIdleElapsed()
        {
            lock (_lock)
            {
                if (_idleTimer != null)
                {
                    _idleTimer.Stop();
                    _idleTimer.Dispose();
                    _idleTimer = null;
                }
            }
            RunBackup("idle-after-edit");
        }

        // ── Removable-media events (from ControlSystem system-event handler) ──
        public void OnMediaInserted()
        {
            CrestronConsole.PrintLine("[ConfigBackup] Removable media inserted");
            // Give the card a moment to mount, and don't block the system-event thread.
            // Captures the current config so a freshly inserted card has it right away.
            new CTimer(o => RunBackup("media-inserted"), 3000);
        }

        public void OnMediaRemoved()
        {
            CrestronConsole.PrintLine("[ConfigBackup] Removable media removed");
        }

        // ── Manual trigger (console command) ──
        public string RunBackupNow()
        {
            return RunBackup("manual");
        }

        // ── Core backup ──
        private string RunBackup(string reason)
        {
            lock (_lock)
            {
                if (_backupRunning) return "backup already running";
                _backupRunning = true;
            }

            try
            {
                string card = FindRemovableRoot();
                if (card == null)
                {
                    CrestronConsole.PrintLine("[ConfigBackup] No removable media present - skipping ({0})", reason);
                    return "no removable media";
                }

                string destDir = card + BackupDirName + @"\";
                try
                {
                    if (!Directory.Exists(destDir)) Directory.CreateDirectory(destDir);
                }
                catch (Exception ex)
                {
                    ErrorLog.Error("[ConfigBackup] cannot create {0}: {1}", destDir, ex.Message);
                    return "cannot create backup dir";
                }

                // One timestamp for the whole run so the config + quick-actions copies correlate.
                string stamp = DateTime.Now.ToString("yyyy-MM-dd_HHmmss");
                int done = 0;
                var results = new List<string>();

                foreach (var target in _targets)
                {
                    string src = null;
                    try { src = target.ResolveSource(); } catch { }
                    if (string.IsNullOrEmpty(src) || !SafeFileExists(src))
                    {
                        results.Add(target.Prefix + ": no source");
                        continue;
                    }

                    string contents;
                    try { contents = ReadAllText(src); }
                    catch (Exception ex) { results.Add(target.Prefix + ": read failed"); ErrorLog.Error("[ConfigBackup] read {0}: {1}", src, ex.Message); continue; }

                    // Count-based retention PER FILE: prune oldest first, leaving room for this one.
                    EnforceMaxBackups(destDir, target.Prefix, MaxBackups - 1);

                    string destPath = destDir + target.Prefix + "-" + stamp + ".json";
                    try
                    {
                        WriteAllText(destPath, contents);
                        done++;
                        results.Add(target.Prefix + ": ok");
                        CrestronConsole.PrintLine("[ConfigBackup] Backed up {0} -> {1} ({2})",
                            Path.GetFileName(src), destPath, reason);
                    }
                    catch (Exception ex)
                    {
                        results.Add(target.Prefix + ": write failed");
                        ErrorLog.Error("[ConfigBackup] write {0}: {1}", destPath, ex.Message);
                    }
                }

                string summary = string.Join("; ", results.ToArray());
                if (done == 0) return "nothing backed up (" + summary + ")";
                return "backed up " + done + " file(s) to " + destDir + " [" + summary + "]";
            }
            catch (Exception ex)
            {
                ErrorLog.Error("[ConfigBackup] Unexpected error: {0}", ex.Message);
                return "error: " + ex.Message;
            }
            finally
            {
                lock (_lock) { _backupRunning = false; }
            }
        }

        // ── Removable-media detection (USB or SD, any mount name) ──
        private string FindRemovableRoot()
        {
            // Removable cards only make sense on an appliance (not VC-4/server).
            if (CrestronEnvironment.DevicePlatform != eDevicePlatform.Appliance)
                return null;

            // Fast path: well-known mount names.
            string[] candidates = { @"\USB\", @"\USB1\", @"\SDCard\", @"\SD\", @"\usbstorage\" };
            foreach (var c in candidates)
                if (SafeExists(c) && IsWritable(c)) return c;

            // Fallback: any non-internal, writable volume at the root. This makes the
            // feature model-agnostic — whatever name the card mounts under is found.
            try
            {
                foreach (var d in Directory.GetDirectories(@"\"))
                {
                    string name = Path.GetFileName(d.TrimEnd('\\', '/'));
                    if (string.IsNullOrEmpty(name) || InternalVolumes.Contains(name)) continue;
                    string path = @"\" + name + @"\";
                    if (IsWritable(path)) return path;
                }
            }
            catch { }

            return null;
        }

        private bool SafeExists(string dir)
        {
            try { return Directory.Exists(dir); }
            catch { return false; }
        }

        private bool SafeFileExists(string path)
        {
            try { return File.Exists(path); }
            catch { return false; }
        }

        private bool IsWritable(string dir)
        {
            string probe = dir + "._acswrite.tmp";
            try
            {
                WriteAllText(probe, "x");
                File.Delete(probe);
                return true;
            }
            catch { return false; }
        }

        // ── Backup housekeeping (per file prefix) ──
        private List<string> ListBackups(string destDir, string prefix)
        {
            var list = new List<string>();
            try
            {
                foreach (var f in Directory.GetFiles(destDir, prefix + "-*.json"))
                    list.Add(f);
            }
            catch { }
            // Timestamped names sort chronologically -> index 0 is the oldest.
            list.Sort(StringComparer.OrdinalIgnoreCase);
            return list;
        }

        private void EnforceMaxBackups(string destDir, string prefix, int keep)
        {
            var backups = ListBackups(destDir, prefix);
            while (backups.Count > keep && backups.Count > 0)
            {
                if (!TryDelete(backups[0])) break;
                backups.RemoveAt(0);
            }
        }

        private bool TryDelete(string path)
        {
            try
            {
                File.Delete(path);
                CrestronConsole.PrintLine("[ConfigBackup] Deleted old backup {0}", Path.GetFileName(path));
                return true;
            }
            catch (Exception ex)
            {
                ErrorLog.Error("[ConfigBackup] delete failed {0}: {1}", path, ex.Message);
                return false;
            }
        }

        // ── Latest NVRAM config (mirrors ConfigRouteHandler's selection) ──
        private string FindLatestConfigFile()
        {
            if (!SafeExists(NvramDir)) return null;

            string[] files;
            try { files = Directory.GetFiles(NvramDir, "*.json"); }
            catch { return null; }

            var versionRegex = new Regex(@"[vV](\d+)", RegexOptions.Compiled);
            string best = null;
            int bestV = -1;
            foreach (var file in files)
            {
                string name = Path.GetFileName(file);
                if (!name.ToLower().Contains("acsconfig")) continue;

                int v = 0;
                Match m = versionRegex.Match(name);
                if (m.Success) int.TryParse(m.Groups[1].Value, out v);
                if (v >= bestV) { bestV = v; best = file; }
            }
            return best;
        }

        // ── Small IO helpers (stream-based, matching ConfigRouteHandler's style) ──
        private string ReadAllText(string path)
        {
            using (var r = new StreamReader(path))
                return r.ReadToEnd();
        }

        private void WriteAllText(string path, string contents)
        {
            using (var s = new FileStream(path, FileMode.Create))
            using (var w = new StreamWriter(s))
                w.Write(contents);
        }
    }
}
