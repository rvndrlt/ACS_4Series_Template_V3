using Crestron.SimplSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ACS_4Series_Template_V3.Climate
{
    public class ClimateControl
    {
        /// <summary>
        /// Serial join carrying the OTHER rooms served by the panel's current thermostat —
        /// "Bar / Theater" when the selected room is Powder Room and all three share a
        /// ClimateID. Blank when the zone serves only the selected room, which collapses the
        /// line in the HTML (`.shared-rooms-label:empty { display: none }`).
        ///
        /// Raw HTML-reserved 1500+ join, NOT a contract signal — the generated Contract is
        /// wiped on regen. Per-panel: each panel gets its own value for its own current room.
        /// </summary>
        public const uint SharedClimateRoomsJoin = 1580;

        private ControlSystem _parent;
        public ClimateControl(ControlSystem parent)
        {
            _parent = parent;
        }
        public void UpdateRoomClimateMode(ushort zoneNumber, ushort function)
        {
            ushort ClimateRoomNumber = 0;
            foreach (var room in _parent.manager.RoomZ)
            {
                if (room.Value.ClimateID == zoneNumber)
                {
                    ClimateRoomNumber = room.Value.Number;
                    switch (function)
                    {
                        case (1):
                            {
                                room.Value.ClimateMode = "Auto";
                                room.Value.ClimateModeNumber = 1;
                                break;
                            }
                        case (2):
                            {
                                room.Value.ClimateMode = "Heat";
                                room.Value.ClimateModeNumber = 2;
                                break;
                            }
                        case (3):
                            {
                                room.Value.ClimateMode = "Cool";
                                room.Value.ClimateModeNumber = 3;
                                break;
                            }
                        case (4):
                            {
                                room.Value.ClimateMode = "Off";
                                room.Value.ClimateModeNumber = 4;
                                break;
                            }
                        case (5):
                            {
                                room.Value.ClimateAutoModeIsSingleSetpoint = false;
                                //CrestronConsole.PrintLine("room {0} room.Value.ClimateAutoModeIsSingleSetpoint {1}", ClimateRoomNumber, room.Value.ClimateAutoModeIsSingleSetpoint);
                                break;
                            }
                        default: break;
                    }

                }
            }
        }

        public void SyncPanelToClimateZone(ushort TPNumber)
        {

            // Get the current room and its climate ID
            ushort currentRoom = _parent.manager.touchpanelZ[TPNumber].CurrentRoomNum;
            ushort climateID = _parent.manager.RoomZ[currentRoom].ClimateID;
            _parent.manager.touchpanelZ[TPNumber].CurrentClimateID = climateID;
            //CrestronConsole.PrintLine("SyncPanelToClimateZone TP-{0} rm{1} id{2}", TPNumber, currentRoom, climateID);

            // Ahead of the climateID == 0 bail-out, so a room with no thermostat CLEARS a
            // stale list left over from the previously selected room.
            UpdateSharedClimateRooms(TPNumber, currentRoom, climateID);

            // If there's no climate ID for this room, exit the function
            if (climateID == 0)
                return;

            // First, unsubscribe from any existing climate feedback by clearing prior values
            for (ushort i = 1; i <= 30; i++)
            {
                // Reset all climate-related boolean inputs on the touchpanel
                _parent.manager.touchpanelZ[TPNumber].UserInterface.BooleanInput[(ushort)(i + 600)].BoolValue = false;
            }

            // Initialize all the climate-related boolean inputs based on current state
            for (ushort i = 1; i <= 30; i++)
            {
                // Calculate the HVAC boolean output number based on the formula
                ushort hvacOutputNumber = (ushort)(((climateID - 1) * 30) + i + 500);

                // Calculate the touchpanel input number
                ushort tpInputNumber = (ushort)(i + 600);

                // Initial synchronization - set touchpanel input to current HVAC output state
                _parent.manager.touchpanelZ[TPNumber].UserInterface.BooleanInput[tpInputNumber].BoolValue = _parent.HVACEISC.BooleanOutput[hvacOutputNumber].BoolValue;
            }


            _parent.manager.touchpanelZ[TPNumber].SubscribeToClimateEvents(currentRoom);

            //CrestronConsole.PrintLine("{2} heat-{0}, cool-{1} single-{3} mode-{4}", manager.RoomZ[currentRoom].CurrentHeatSetpoint, manager.RoomZ[currentRoom].CurrentCoolSetpoint, manager.RoomZ[currentRoom].Name, manager.RoomZ[currentRoom].CurrentAutoSingleSetpoint, manager.RoomZ[currentRoom].ClimateModeNumber);
        }

        /// <summary>
        /// Publishes the "this thermostat also serves…" line for one panel: every OTHER room
        /// carrying the same ClimateID as the panel's selected room, in config order, joined
        /// with " / ". One thermostat commonly covers several rooms (Bar / Theater / Powder
        /// Room all on ClimateID 3), and without this the page gives no hint that changing the
        /// setpoint here moves the other rooms too.
        ///
        /// Writes an empty string when the zone is a one-room zone (or the room has no
        /// thermostat) so the HTML row collapses instead of showing a stray separator.
        /// </summary>
        private void UpdateSharedClimateRooms(ushort TPNumber, ushort currentRoom, ushort climateID)
        {
            var tp = _parent.manager.touchpanelZ[TPNumber];
            if (tp.UserInterface == null)
                return;

            string text = "";
            if (climateID > 0)
            {
                var others = new List<string>();
                foreach (var room in _parent.manager.RoomZ)
                {
                    if (room.Value.ClimateID != climateID) continue;
                    if (room.Key == currentRoom) continue;   // the room-name label already names it
                    if (string.IsNullOrEmpty(room.Value.Name)) continue;
                    others.Add(room.Value.Name);
                }
                text = string.Join(" / ", others.ToArray());
            }

            tp.UserInterface.StringInput[SharedClimateRoomsJoin].StringValue = text;
            //CrestronConsole.PrintLine("SharedClimateRooms TP-{0} rm{1} id{2} -> \"{3}\"", TPNumber, currentRoom, climateID, text);
        }
    }
}
