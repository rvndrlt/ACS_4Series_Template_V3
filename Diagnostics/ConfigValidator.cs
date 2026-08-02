using System;
using System.Collections.Generic;
using Crestron.SimplSharp;

namespace ACS_4Series_Template_V3.Diagnostics
{
    /// <summary>
    /// Reports config numbers that don't resolve to anything, instead of letting them surface as a
    /// KeyNotFoundException halfway through initialization.
    ///
    /// Some dangling references are DELIBERATE — assigning a display to a room number that doesn't
    /// exist is how AVRs and tied DM outputs are modelled. So this is a report, never an error: the
    /// program is expected to tolerate every case listed here. Treat the output as "here is what the
    /// config asks for that isn't defined", then decide per line whether it was intentional.
    ///
    /// Runs at the end of InitializeSystem and on demand via the `checkconfig` console command.
    /// </summary>
    public static class ConfigValidator
    {
        public static void Report(ControlSystem cs, Action<string> write)
        {
            if (write == null) write = s => CrestronConsole.PrintLine(s);

            try
            {
                if (cs == null || cs.manager == null)
                {
                    write("checkconfig: manager not ready.");
                    return;
                }

                var m = cs.manager;
                var findings = new List<string>();

                // Displays -> rooms. Commonly intentional (AVRs, tied outputs).
                if (m.VideoDisplayZ != null && m.RoomZ != null)
                {
                    foreach (var kv in m.VideoDisplayZ)
                    {
                        if (kv.Value == null) continue;
                        ushort r = kv.Value.AssignedToRoomNum;
                        if (!m.RoomZ.ContainsKey(r))
                            findings.Add(string.Format(
                                "display {0} \"{1}\" -> assignedToRoomNum {2} (no such room; room-level updates are skipped for it)",
                                kv.Key, kv.Value.DisplayName, r));
                    }
                }

                // Rooms -> scenarios.
                if (m.RoomZ != null)
                {
                    foreach (var kv in m.RoomZ)
                    {
                        var room = kv.Value;
                        if (room == null) continue;

                        if (m.SubsystemScenarioZ != null && !m.SubsystemScenarioZ.ContainsKey(room.SubSystemScenario))
                            findings.Add(string.Format("room {0} \"{1}\" -> subSystemScenario {2} (no such scenario; room skipped in StartupRooms)",
                                kv.Key, room.Name, room.SubSystemScenario));

                        if (room.AudioSrcScenario > 0 && m.AudioSrcScenarioZ != null && !m.AudioSrcScenarioZ.ContainsKey(room.AudioSrcScenario))
                            findings.Add(string.Format("room {0} \"{1}\" -> audioSrcScenario {2} (no such scenario)",
                                kv.Key, room.Name, room.AudioSrcScenario));

                        if (room.AudioSrcSharingScenario > 0 && m.AudioSrcSharingScenarioZ != null && !m.AudioSrcSharingScenarioZ.ContainsKey(room.AudioSrcSharingScenario))
                            findings.Add(string.Format("room {0} \"{1}\" -> audioSrcSharingScenario {2} (no such scenario)",
                                kv.Key, room.Name, room.AudioSrcSharingScenario));
                    }
                }

                // Floors -> rooms.
                if (m.Floorz != null && m.RoomZ != null)
                {
                    foreach (var kv in m.Floorz)
                    {
                        if (kv.Value == null || kv.Value.IncludedRooms == null) continue;
                        foreach (ushort r in kv.Value.IncludedRooms)
                        {
                            if (!m.RoomZ.ContainsKey(r))
                                findings.Add(string.Format("floor {0} \"{1}\" -> includedRooms contains {2} (no such room)",
                                    kv.Key, kv.Value.Name, r));
                        }
                    }
                }

                // Touchpanels -> floor scenario, default room. A default room that is not reachable
                // from the panel's floorScenario is the specific case that makes a panel silently
                // fall back to its home page on startup and on every idle timeout.
                if (m.touchpanelZ != null)
                {
                    foreach (var kv in m.touchpanelZ)
                    {
                        var tp = kv.Value;
                        if (tp == null) continue;

                        if (m.FloorScenarioZ != null && !m.FloorScenarioZ.ContainsKey(tp.FloorScenario))
                        {
                            findings.Add(string.Format("TP-{0} \"{1}\" -> floorScenario {2} (no such scenario)",
                                kv.Key, tp.Name, tp.FloorScenario));
                            continue;
                        }

                        if (tp.DefaultRoom == 0) continue;

                        if (m.RoomZ != null && !m.RoomZ.ContainsKey(tp.DefaultRoom))
                        {
                            findings.Add(string.Format("TP-{0} \"{1}\" -> defaultRoom {2} (no such room)",
                                kv.Key, tp.Name, tp.DefaultRoom));
                            continue;
                        }

                        bool reachable = false;
                        if (m.FloorScenarioZ != null && m.Floorz != null)
                        {
                            var scenario = m.FloorScenarioZ[tp.FloorScenario];
                            if (scenario != null && scenario.IncludedFloors != null)
                            {
                                foreach (ushort f in scenario.IncludedFloors)
                                {
                                    if (m.Floorz.ContainsKey(f) &&
                                        m.Floorz[f].IncludedRooms != null &&
                                        m.Floorz[f].IncludedRooms.Contains(tp.DefaultRoom))
                                    {
                                        reachable = true;
                                        break;
                                    }
                                }
                            }
                        }

                        if (!reachable)
                            findings.Add(string.Format(
                                "TP-{0} \"{1}\" -> defaultRoom {2} is not on any floor in floorScenario {3} (panel falls back to its home page)",
                                kv.Key, tp.Name, tp.DefaultRoom, tp.FloorScenario));
                    }
                }

                write("--- checkconfig ---");
                if (findings.Count == 0)
                {
                    write("No dangling config references.");
                }
                else
                {
                    write(string.Format("{0} unresolved reference(s). Each is tolerated at runtime - confirm whether it was intended:", findings.Count));
                    foreach (string f in findings) write("  " + f);
                }
                write("-------------------");
            }
            catch (Exception ex)
            {
                write("checkconfig error: " + ex.Message);
            }
        }
    }
}
