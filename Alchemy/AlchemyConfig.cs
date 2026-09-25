using System;
using System.Collections.Generic;
using System.Text;
using Crestron.SimplSharp;
using Crestron.SimplSharp.CrestronIO;
using Newtonsoft.Json;

namespace ACS_4Series_Template_V3.Alchemy
{
    /// <summary>
    /// Optional site settings for the Barco Alchemy bridge, in \NVRAM\alchemyConfig.json --
    /// same pattern as cameraConfig.json and intercomConfig.json, and in NVRAM for the same
    /// reason: loading a .cpz replaces the program directory and would take site edits with it.
    ///
    /// **Every field is optional and every default is derived from the main config**, so a new
    /// job needs no file at all. A theater that is room 8 on display 32 works untouched, because
    /// nothing here is a room or display number until someone chooses to pin one down. The file
    /// exists for the cases the config cannot answer:
    ///
    ///  - a theater whose video source is named something this does not recognise
    ///  - a DCI server feeding a flat panel, where projector power would be wrong
    ///  - a second projector, or a projector with no DCI server
    ///
    /// Re-read live with the `alchemyconfig` console command.
    /// </summary>
    public sealed class AlchemyConfig
    {
        public const string FilePath = @"\NVRAM\alchemyConfig.json";

        /// <summary>
        /// Substrings (case-insensitive) that mark a video source as the DCI server. Matched
        /// against the source's Name, never DisplayName -- Name stays descriptive while
        /// DisplayName is whatever reads well on screen ("Movies"), which carries no device type.
        ///
        /// Everything else keys off this: which rooms count as theaters, which displays are
        /// projectors, and when the player state is worth publishing. Override it and a job that
        /// calls the source something else works with no code change.
        /// </summary>
        public List<string> DciSourceNames { get; set; }

        /// <summary>
        /// Display numbers whose power should command the projector over the EISC. Null or empty
        /// means derive: every display whose source scenario includes a DCI source.
        ///
        /// Set it when the derivation would be wrong -- a DCI server feeding a flat panel is the
        /// case that matters, because there the projector commands would go to a projector
        /// nobody is watching.
        /// </summary>
        public List<ushort> ProjectorDisplays { get; set; }

        /// <summary>
        /// Whether selecting a source in the theater powers the projector, and turning the
        /// display off powers it down. True by default.
        ///
        /// Set false to hand projector power back to something else -- a SIMPL Windows program,
        /// or nothing at all -- without giving up the DCI page, which keeps working either way.
        /// </summary>
        public bool DrivePower { get; set; }

        /// <summary>
        /// barcoAlchemy's media EISC. 0xA1 by agreement with that program; its projector mirror
        /// for SIMPL Windows is 0xA2 and is not ours to drive.
        /// </summary>
        public uint EiscIpid { get; set; }

        public AlchemyConfig()
        {
            // The names seen in the field so far. CINEMA_* deliberately absent: those are
            // projector macros, not source names, and a source called "Cinema Room TV" is not a
            // DCI server.
            DciSourceNames = new List<string>
            {
                "ALCHEMY", "DOREMI", "D-CINEMA", "DCINEMA", "DCI SERVER", "DCISERVER"
            };
            ProjectorDisplays = new List<ushort>();
            DrivePower = true;
            EiscIpid = 0xA1;
        }

        /// <summary>
        /// Reads the file if it is there, otherwise returns defaults. A missing or broken file
        /// must never stop the bridge: losing a site override is a nuisance, losing the theater
        /// page and projector control is an outage.
        /// </summary>
        public static AlchemyConfig Load()
        {
            try
            {
                if (!File.Exists(FilePath))
                {
                    CrestronConsole.PrintLine(
                        "AlchemyRelay: no {0} - deriving everything from the main config", FilePath);
                    return new AlchemyConfig();
                }

                string json = File.ReadToEnd(FilePath, Encoding.UTF8);
                var loaded = JsonConvert.DeserializeObject<AlchemyConfig>(json);
                if (loaded == null) throw new Exception("file parsed to null");

                // A partial file is normal and must not blank the rest. Absent stays absent;
                // present-but-empty is an explicit "derive this one".
                if (loaded.DciSourceNames == null || loaded.DciSourceNames.Count == 0)
                    loaded.DciSourceNames = new AlchemyConfig().DciSourceNames;
                if (loaded.ProjectorDisplays == null)
                    loaded.ProjectorDisplays = new List<ushort>();
                if (loaded.EiscIpid == 0)
                    loaded.EiscIpid = 0xA1;

                CrestronConsole.PrintLine("AlchemyRelay: loaded {0} - {1}", FilePath, loaded.Describe());
                return loaded;
            }
            catch (Exception ex)
            {
                ErrorLog.Error("AlchemyRelay: failed to read {0} ({1}); using defaults",
                    FilePath, ex.Message);
                return new AlchemyConfig();
            }
        }

        public string Describe()
        {
            var sb = new StringBuilder();
            sb.AppendFormat("eisc 0x{0:X2}, drivePower={1}, projectorDisplays=", EiscIpid, DrivePower);
            if (ProjectorDisplays == null || ProjectorDisplays.Count == 0) sb.Append("(derived)");
            else
            {
                for (int i = 0; i < ProjectorDisplays.Count; i++)
                    sb.Append(i > 0 ? "," : "").Append(ProjectorDisplays[i]);
            }
            sb.Append(", names=");
            for (int i = 0; i < DciSourceNames.Count; i++)
                sb.Append(i > 0 ? "/" : "").Append(DciSourceNames[i]);
            return sb.ToString();
        }

        /// <summary>True when this source Name marks the DCI server, per the configured list.</summary>
        public bool IsDciSource(string sourceName)
        {
            if (string.IsNullOrEmpty(sourceName) || DciSourceNames == null) return false;
            string n = sourceName.ToUpper();
            foreach (string token in DciSourceNames)
            {
                if (!string.IsNullOrEmpty(token) && n.Contains(token.ToUpper())) return true;
            }
            return false;
        }
    }
}
