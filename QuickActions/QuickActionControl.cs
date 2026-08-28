using Crestron.SimplSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ACS_4Series_Template_V3.QuickActions
{
    public class QuickActionControl
    {
        private ControlSystem _parent;
        public QuickActionControl(ControlSystem parent)
        {
            _parent = parent;
        }
        public void updateQuickActionMusicSource(ushort zoneNumber, string srcName)
        {
            if (zoneNumber > 0)
            {
                foreach (var tp in _parent.manager.touchpanelZ)
                {
                    if (tp.Value.HTML_UI)
                    {
                        //TODO - build HTML Contract for quick action music
                    }
                    else
                    {
                        tp.Value.UserInterface.SmartObjects[30].StringInput[(ushort)(2 * zoneNumber)].StringValue = srcName;
                    }
                }
            }
        }
        public void RefreshQuickAction(ushort TPNumber)
        {
            ushort eiscPos = (ushort)(((TPNumber - 1) * 100) + 1);//1, 101, 201
            if (_parent.manager.touchpanelZ[TPNumber].HTML_UI)
            {
                //TODO - build HTML contract for quick action
            }
            else
            {
                _parent.manager.touchpanelZ[TPNumber].UserInterface.SmartObjects[15].UShortInput[4].UShortValue = _parent.subsystemControlEISC.UShortOutput[eiscPos].UShortValue;//#of items
                //CrestronConsole.PrintLine("TP-{0} refresh quick action #ofQuick-{1} eiscpos-{2}", TPNumber, _parent.subsystemControlEISC.UShortOutput[eiscPos].UShortValue, eiscPos);
                for (ushort i = 1; i < 100; i++)
                {
                    if (i <= _parent.subsystemControlEISC.UShortOutput[eiscPos].UShortValue)//this is the number of quick actions
                    {
                        _parent.manager.touchpanelZ[TPNumber].UserInterface.SmartObjects[15].BooleanInput[(ushort)(4015 + i)].BoolValue = true; //set visibility for buttons
                        _parent.manager.touchpanelZ[TPNumber].UserInterface.SmartObjects[15].StringInput[(ushort)(i + 15)].StringValue = _parent.subsystemControlEISC.StringOutput[(ushort)(eiscPos + i - 1)].StringValue;//text

                    }
                    else
                    { _parent.manager.touchpanelZ[TPNumber].UserInterface.SmartObjects[15].BooleanInput[(ushort)(4015 + i)].BoolValue = false; }// clear visibility
                    if (i > 50)
                    {
                        _parent.manager.touchpanelZ[TPNumber].UserInterface.SmartObjects[15].StringInput[(ushort)(i + 1965)].StringValue = _parent.subsystemControlEISC.StringOutput[(ushort)(eiscPos + i - 1)].StringValue;//icon
                    }
                }
            }
        }
        public void SelectQuickActionIncludedSubsystem(ushort buttonNumber)
        {
            CrestronConsole.PrintLine("quick action to save {0}", _parent.quickActionXML.quickActionToRecallOrSave);
            if (buttonNumber > 0 && _parent.quickActionXML.quickActionToRecallOrSave > 0)
            {
                //toggle the button feedback
                _parent.imageEISC.BooleanInput[(ushort)(buttonNumber + 220)].BoolValue = !_parent.imageEISC.BooleanInput[(ushort)(buttonNumber + 220)].BoolValue;
            }
        }
        /// <summary>
        /// Native-panel (XML preset) climate recall. Translates the zone-indexed preset arrays
        /// into a ClimatePayload and hands it to QuickActionManager's paced sweep, which is the
        /// single implementation of the mode-then-setpoint stagger both quick-action systems
        /// share. This used to write every zone's mode pulse + setpoints in one synchronous
        /// pass, which floods the CoolMaster gateway on a whole-house recall.
        /// </summary>
        public void RecallClimatePreset(ushort presetNumber)
        {
            if (_parent.quickActionManager == null)
            {
                ErrorLog.Error("RecallClimatePreset: quickActionManager missing, climate preset {0} not recalled", presetNumber);
                return;
            }

            // Rooms can share a Climate ID (one HVAC zone covering several rooms). The
            // preset arrays are indexed by zone, so without this guard a shared zone would
            // get the same mode pulse and setpoint sent once per room.
            var payload = new ClimatePayload();
            var zonesSent = new HashSet<ushort>();
            foreach (var rm in _parent.manager.RoomZ)
            {
                ushort zone = rm.Value.ClimateID;
                if (zone > 0 && zonesSent.Add(zone))
                {
                    ushort zoneChecked = _parent.quickActionXML.HVACZoneChecked[presetNumber - 1, zone - 1];
                    if (zoneChecked > 0)
                    {
                        ushort heatSetpointToSend = _parent.quickActionXML.HVACHeatSetpoints[presetNumber - 1, zone - 1];
                        payload.Zones.Add(new ClimateZoneSetting
                        {
                            ClimateId = zone,
                            //modes are 1:auto 2:heat 3:cool 4:off
                            Mode = _parent.quickActionXML.HVACModes[presetNumber - 1, zone - 1],
                            HeatSp = heatSetpointToSend,
                            CoolSp = _parent.quickActionXML.HVACCoolSetpoints[presetNumber - 1, zone - 1],
                            // The XML save path stores a single-setpoint zone's one value in the
                            // HEAT slot (see QuickActionXML.writeSubsystems), so that same value
                            // is what AutoSp means here.
                            AutoSp = heatSetpointToSend,
                            SingleSetpoint = rm.Value.ClimateAutoModeIsSingleSetpoint
                        });
                    }
                }
            }
            _parent.quickActionManager.RecallClimatePayload(payload);
        }

    }
}
