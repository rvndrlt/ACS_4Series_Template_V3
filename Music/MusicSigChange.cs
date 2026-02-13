using ACS_4Series_Template_V3.QuickActions;
using Crestron.SimplSharp;
using Crestron.SimplSharpPro;
using Crestron.SimplSharpPro.AudioDistribution;
using Crestron.SimplSharpPro.DeviceSupport;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ACS_4Series_Template_V3.Music
{
    public class MusicSigChange
    {
        private ControlSystem _parent;
        public MusicSigChange(ControlSystem parent)
        {
            _parent = parent;
        }
        public void Music1SigChangeHandler(GenericBase currentDevice, SigEventArgs args)
        {
            if (args.Event == eSigEvent.BoolChange)
            {
                if (args.Sig.Number <= 300 && args.Sig.Number > 200)//mute
                {
                    ushort zoneNumber = (ushort)(args.Sig.Number - 200);
                    CrestronConsole.PrintLine("music mute zone {0} to {1}", zoneNumber, args.Sig.BoolValue);
                    
                    ushort roomNumber = 0;
                    foreach (var room in _parent.manager.RoomZ)
                    {
                        if (room.Value.AudioID == zoneNumber)
                        {
                            roomNumber = room.Value.Number;
                            room.Value.MusicMuted = args.Sig.BoolValue;//update the room with the status of the mute
                        }
                    }
                    
                    // Update HomePageMusic UI directly for responsiveness
                    if (roomNumber > 0)
                    {
                        for (int slotIndex = 0; slotIndex < _parent.musicSystemControl.ActiveMusicRoomsList.Count; slotIndex++)
                        {
                            if (_parent.musicSystemControl.ActiveMusicRoomsList[slotIndex] == roomNumber)
                            {
                                foreach (var panel in _parent.manager.touchpanelZ)
                                {
                                    if (panel.Value.HTML_UI && slotIndex < panel.Value._HTMLContract.HomeMusicZone.Length)
                                    {
                                        int capturedSlot = slotIndex;
                                        panel.Value._HTMLContract.HomeMusicZone[capturedSlot].isMuted(
                                            (sig, wh) => sig.BoolValue = args.Sig.BoolValue);
                                    }
                                }
                                break;
                            }
                        }
                    }
                    
                    foreach (var TP in _parent.manager.touchpanelZ)
                    {
                        if (_parent.manager.RoomZ[TP.Value.CurrentRoomNum].AudioID == zoneNumber)
                        {
                            TP.Value.UserInterface.BooleanInput[1009].BoolValue = args.Sig.BoolValue;
                        }
                    }
                }
                else if (args.Sig.BoolValue == true)
                {
                    if (args.Sig.Number <= 100)
                    {
                        ushort TPNumber = (ushort)(args.Sig.Number);
                        _parent.manager.touchpanelZ[TPNumber].CurrentASrcGroupNum++;
                        _parent.SetASRCGroup(TPNumber, _parent.manager.touchpanelZ[TPNumber].CurrentASrcGroupNum);
                    }
                    else if (args.Sig.Number <= 200)
                    {
                        ushort actionNumber = (ushort)(args.Sig.Number - 100);
                        _parent.musicSystemControl.AudioFloorOff(actionNumber); // HA ALL OFF or floor off
                    }

                }
            }
            if (args.Event == eSigEvent.UShortChange)
            {
                if (args.Sig.Number <= 100)//select a music source
                {
                    ushort TPNumber = (ushort)args.Sig.Number;
                    ushort asrc = _parent.TranslateButtonNumberToASrc((ushort)args.Sig.Number, args.Sig.UShortValue);//get the music source from the button number press
                    ushort currentRoomNum = _parent.manager.touchpanelZ[TPNumber].CurrentRoomNum;
                    ushort switcherOutputNum = _parent.manager.RoomZ[currentRoomNum].AudioID;

                    _parent.musicSystemControl.SwitcherSelectMusicSource(switcherOutputNum, asrc);//from sigchangehandler
                    _parent.musicSystemControl.PanelSelectMusicSource(TPNumber, asrc);
                    if (asrc == 0)
                    {
                        _parent.manager.touchpanelZ[TPNumber].subsystemPageFlips(0);//clear the music subpage. show the subsystem list
                        _parent.musicEISC1.UShortInput[(ushort)(TPNumber + 200)].UShortValue = 0;//clear the music source subpage
                        _parent.musicEISC3.UShortInput[(ushort)(switcherOutputNum + 100)].UShortValue = 0;//set the volume to 0
                    }
                }
                else if (args.Sig.Number <= 200)
                {
                    /*                    ushort TPNumber = (ushort)(args.Sig.Number - 100);
                                        SelectMusicFloor(TPNumber, args.Sig.UShortValue);*/
                }

                else if (args.Sig.Number > 500 && args.Sig.Number <= 600)
                {
                    if (ControlSystem.NAXsystem)
                    {
                        if (!_parent.nax.NAXAllOffBusy && !_parent.musicSystemControl.RecallMusicPresetTimerBusy)
                        {
                            ushort switcherOutputNumber = (ushort)(args.Sig.Number - 500);
                            CrestronConsole.PrintLine("nax output changed outputnum{0} value{1}", switcherOutputNumber, args.Sig.UShortValue);
                            _parent.nax.NAXOutputSrcChanged(switcherOutputNumber, args.Sig.UShortValue);
                        }
                    }
                    else
                    {
                        _parent.swamp.SwampOutputSrcChanged((ushort)args.Sig.Number, args.Sig.UShortValue);
                    }
                }
                else if (args.Sig.Number <= 700)
                {
                    ushort playerNumber = (ushort)(args.Sig.Number - 600);
                    _parent.nax.StreamingPlayerProviderChanged(playerNumber, args.Sig.UShortValue);
                }

            }
        }
        public void Music2SigChangeHandler(GenericBase currentDevice, SigEventArgs args)
        {
            if (args.Event == eSigEvent.UShortChange)
            {
                if (args.Sig.Number <= 100)//select a music source
                {
                    CrestronConsole.PrintLine("share-num{0} , value{1}", args.Sig.Number, args.Sig.UShortValue);
                    _parent.musicSystemControl.SelectShareSource((ushort)args.Sig.Number, args.Sig.UShortValue);

                }
            }
            if (args.Event == eSigEvent.BoolChange)
            {
                if (args.Sig.Number <= 100)
                {
                    //updateSharingSrc((ushort)args.Sig.Number);
                }
            }
        }
        public void Music3SigChangeHandler(BasicTriList currentDevice, SigEventArgs args)
        {
            if (args.Sig.Type == eSigType.UShort)
            {

                ushort switcherOutNum = (ushort)(args.Sig.Number - 100);
                _parent.volumes[switcherOutNum - 1] = args.Sig.UShortValue;//this stores the zones current volume
                CrestronConsole.PrintLine("!!!!volume changed {0} {1}", args.Sig.Number, args.Sig.UShortValue);
                //store the volume in the room object
                foreach (var room in _parent.manager.RoomZ)
                {
                    if (room.Value.AudioID == switcherOutNum)
                    {
                        room.Value.MusicVolume = args.Sig.UShortValue;
                    }
                }
                //update the volume on the touchpanel
                foreach (var TP in _parent.manager.touchpanelZ)
                {

                    if (_parent.manager.RoomZ[TP.Value.CurrentRoomNum].AudioID == switcherOutNum)
                    {
                        CrestronConsole.PrintLine("!!!!updating volume on TP{0} {1}", TP.Value.Number, args.Sig.UShortValue);
                        TP.Value.UserInterface.UShortInput[2].UShortValue = args.Sig.UShortValue;
                    }
                }
            }
            else if (args.Event == eSigEvent.BoolChange && args.Sig.BoolValue == true)
            {
                if (args.Sig.Number == 1)
                {
                    _parent.musicSystemControl.RequestMusicSources();
                }
                else if (args.Sig.Number == 2)
                {

                }
                else if (args.Sig.Number == 3)
                {

                }
            }
            else if (args.Event == eSigEvent.StringChange)
            {
                if (args.Sig.Number == 1)
                {
                    _parent.quickActionXML.newQuickActionPresetName = args.Sig.StringValue;
                    CrestronConsole.PrintLine("-{0}", _parent.quickActionXML.newQuickActionPresetName);
                }
                else if (args.Sig.Number > 300)
                {
                    ushort switcherOutNum = (ushort)(args.Sig.Number - 300);
                    _parent.musicSystemControl.multis[switcherOutNum] = args.Sig.StringValue;
                    _parent.nax.NAXZoneMulticastChanged(switcherOutNum, args.Sig.StringValue);
                }
            }
        }
        public void Volume_Sigchange(BasicTriList currentDevice, SigEventArgs args)
        {
            CrestronConsole.PrintLine("volume sig change number {0} value {1} isRamping {2}", args.Sig.Number, args.Sig.UShortValue, args.Sig.IsRamping);
            //if (args.Sig is { IsInput: false, Type: eSigType.UShort, Number: 1 })
            if (!args.Sig.IsInput && args.Sig.Type == eSigType.UShort)
            {
                ushort audioID = (ushort)args.Sig.Number;
                ushort roomNumber = 0;
                //find the room that corresponds to the audioID
                foreach (var room in _parent.manager.RoomZ)
                {
                    if (room.Value.AudioID == audioID)
                    {
                        roomNumber = room.Value.Number;
                        room.Value.MusicVolume = args.Sig.UShortValue;
                    }
                }
                if (roomNumber > 0)
                {
                    var room = _parent.manager.RoomZ[roomNumber];
                    
                    // Update HomePageMusic UI directly for responsiveness
                    for (int slotIndex = 0; slotIndex < _parent.musicSystemControl.ActiveMusicRoomsList.Count; slotIndex++)
                    {
                        if (_parent.musicSystemControl.ActiveMusicRoomsList[slotIndex] == roomNumber)
                        {
                            foreach (var panel in _parent.manager.touchpanelZ)
                            {
                                if (panel.Value.HTML_UI && slotIndex < panel.Value._HTMLContract.HomeMusicZone.Length)
                                {
                                    int capturedSlot = slotIndex;
                                    panel.Value._HTMLContract.HomeMusicZone[capturedSlot].Volume(
                                        (sig, wh) => sig.UShortValue = args.Sig.UShortValue);
                                }
                            }
                            break;
                        }
                    }
                    
                    if (args.Sig.IsRamping)
                    {
                        room.MusicVolRamping = true;
                        ushort targetVolume = args.Sig.UShortValue;

                        // Stop previous timer if it exists
                        room.RampTimer?.Stop();

                        // Start a new ramping timer
                        room.RampTimer = new CTimer(o =>
                        {
                            if (!room.MusicVolRamping)
                            {
                                room.RampTimer?.Stop();
                                room.RampTimer = null;
                                return;
                            }

                            ushort latestVolume = args.Sig.UShortValue;
                            room.MusicVolume = latestVolume;
                        }, null, 0, 50); // Adjust step interval for smoothness
                    }

                    else
                    {
                        room.MusicVolRamping = false;
                        _parent.manager.touchpanelZ[1].UserInterface.UShortInput[2].StopRamp();
                        room.RampTimer?.Stop();
                        room.RampTimer = null;
                        room.MusicVolume = args.Sig.UShortValue;
                    }
                }
                //update the volume on the touchpanel
                foreach (var TP in _parent.manager.touchpanelZ)
                {

                    if (_parent.manager.RoomZ[TP.Value.CurrentRoomNum].AudioID == audioID)
                    {
                        CrestronConsole.PrintLine("!!!!updating volume on TP{0} {1}", TP.Value.Number, args.Sig.UShortValue);
                        TP.Value.UserInterface.UShortInput[2].UShortValue = args.Sig.UShortValue;
                    }
                }
            }
        }
    }
}
