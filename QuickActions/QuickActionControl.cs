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
                CrestronConsole.PrintLine("TP-{0} refresh quick action #ofQuick-{1} eiscpos-{2}", TPNumber, _parent.subsystemControlEISC.UShortOutput[eiscPos].UShortValue, eiscPos);
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
        public void RecallClimatePreset(ushort presetNumber)
        {
            foreach (var rm in _parent.manager.RoomZ)
            {
                ushort zone = rm.Value.ClimateID;
                if (zone > 0)
                {
                    ushort zoneChecked = _parent.quickActionXML.HVACZoneChecked[presetNumber - 1, zone - 1];
                    if (zoneChecked > 0)
                    {
                        ushort modeToSend = _parent.quickActionXML.HVACModes[presetNumber - 1, zone - 1];
                        ushort heatSetpointToSend = _parent.quickActionXML.HVACHeatSetpoints[presetNumber - 1, zone - 1];
                        ushort coolSetpointToSend = _parent.quickActionXML.HVACCoolSetpoints[presetNumber - 1, zone - 1];
                        //modes are 1:auto 2:heat 3:cool 4:off
                        switch (modeToSend)
                        {
                            case 1://auto
                                {
                                    _parent.HVACEISC.BooleanInput[zone].BoolValue = true;
                                    _parent.HVACEISC.UShortInput[(ushort)(zone + 100)].UShortValue = (ushort)(heatSetpointToSend * 10);
                                    if (!rm.Value.ClimateAutoModeIsSingleSetpoint)
                                    {
                                        _parent.HVACEISC.UShortInput[(ushort)(zone + 200)].UShortValue = (ushort)(coolSetpointToSend * 10);
                                    }
                                    _parent.HVACEISC.BooleanInput[zone].BoolValue = false;
                                    break;
                                }
                            case 2://heat
                                {
                                    _parent.HVACEISC.BooleanInput[(ushort)(zone + 100)].BoolValue = true;
                                    _parent.HVACEISC.UShortInput[(ushort)(zone + 100)].UShortValue = (ushort)(heatSetpointToSend * 10);
                                    _parent.HVACEISC.BooleanInput[(ushort)(zone + 100)].BoolValue = false;
                                    break;
                                }
                            case 3://cool
                                {
                                    _parent.HVACEISC.BooleanInput[(ushort)(zone + 200)].BoolValue = true;
                                    _parent.HVACEISC.UShortInput[(ushort)(zone + 200)].UShortValue = (ushort)(coolSetpointToSend * 10);
                                    _parent.HVACEISC.BooleanInput[(ushort)(zone + 200)].BoolValue = false;
                                    break;
                                }
                            case 4://off
                                {
                                    _parent.HVACEISC.BooleanInput[(ushort)(zone + 300)].BoolValue = true;
                                    _parent.HVACEISC.BooleanInput[(ushort)(zone + 300)].BoolValue = false;
                                    break;
                                }
                            default: break;
                        }

                    }
                }
            }
        }

    }
}
