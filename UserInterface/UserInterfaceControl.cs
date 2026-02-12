using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ACS_4Series_Template_V3.UI;

namespace ACS_4Series_Template_V3.UserInterface
{
    public class UserInterfaceControl
    {
        private ControlSystem _parentCS;

        public UserInterfaceControl(ControlSystem parentCS)
        {
            _parentCS = parentCS;
        }

        public void SubscribeToContractEvents(TouchpanelUI tp)
        {
            // Floor selection
            for (int i = 0; i < tp._HTMLContract.FloorSelect.Length; i++)
            {
                int capturedIndex = i;
                tp._HTMLContract.FloorSelect[i].SelectFloor += (sender, args) =>
                {
                    if (args.SigArgs.Sig.BoolValue)
                    {
                        ushort floorButtonNumber = (ushort)(capturedIndex + 1);
                        if (tp.CurrentPageNumber == 0)
                        {
                            _parentCS.SelectWholeHouseFloor(tp.Number, floorButtonNumber);//From HTML Contract
                        }
                        else
                        {
                            _parentCS.SelectFloor(tp.Number, floorButtonNumber);//From HTML Contract
                        }
                    }
                };
            }
            //Lights
            for (int i = 0; i < tp._HTMLContract.LightButton.Length; i++)
            {
                int capturedIndex = i;
                tp._HTMLContract.LightButton[i].LightButtonSelect += (sender, args) =>
                {

                    ushort buttonNumber = (ushort)(capturedIndex + 1);
                    _parentCS.subsystemControlEISC.BooleanInput[(ushort)((tp.Number - 1) * 200 + buttonNumber)].BoolValue = args.SigArgs.Sig.BoolValue;

                };
            }
            //Zone/Room Select
            for (int i = 0; i < tp._HTMLContract.roomButton.Length; i++)
            {
                int capturedIndex = i;
                tp._HTMLContract.roomButton[i].selectZone += (sender, args) =>
                {
                    if (args.SigArgs.Sig.BoolValue)
                    {
                        ushort roomButtonNumber = (ushort)(capturedIndex + 1);
                        tp.CurrentPageNumber = 2; // 2 = roomSubsystemList
                        _parentCS.SelectZone((tp.Number), roomButtonNumber, true);//from select zone
                    }
                };
            }
            //Whole House Subsystems
            for (int i = 0; i < tp._HTMLContract.WholeHouseSubsystem.Length; i++)
            {
                int capturedIndex = i;
                tp._HTMLContract.WholeHouseSubsystem[i].SelectSubsystem += (sender, args) =>
                {
                    if (args.SigArgs.Sig.BoolValue)
                    {
                        ushort subsystemButtonNumber = (ushort)(capturedIndex + 1);
                        _parentCS.SelectSubsystem(tp.Number, subsystemButtonNumber);//HTML from whole house subsystem list
                    }

                };
            }
            //Whole House Zone List
            for (int i = 0; i < tp._HTMLContract.WholeHouseZone.Length; i++)
            {
                int capturedIndex = i;
                tp._HTMLContract.WholeHouseZone[i].SelectWholeHouseZone += (sender, args) =>
                {
                    if (args.SigArgs.Sig.BoolValue) // Only on press, not release
                    {

                        tp.CurrentPageNumber = 0; // 0 = HOME
                        ushort subsystemNumber = tp.CurrentSubsystemNumber;
                        ushort currentRoomNumber = 0;
                        if (tp.WholeHouseRoomList.Count > 0)
                        {
                            currentRoomNumber = tp.WholeHouseRoomList[capturedIndex];
                            tp.CurrentRoomNum = currentRoomNumber;
                            tp.UserInterface.StringInput[1].StringValue = _parentCS.manager.RoomZ[currentRoomNumber].Name;
                        }
                        if (subsystemNumber > 0)
                        {
                            tp.subsystemPageFlips(_parentCS.manager.SubsystemZ[subsystemNumber].FlipsToPageNumber);//HTML

                            if (_parentCS.manager.SubsystemZ[subsystemNumber].EquipID > 99)
                            {
                                _parentCS.subsystemEISC.UShortInput[(ushort)(tp.Number + 200)].UShortValue = (ushort)(_parentCS.manager.SubsystemZ[subsystemNumber].EquipID + tp.Number); //get the equipID for the subsystem
                            }
                            else
                            {
                                _parentCS.subsystemEISC.UShortInput[(ushort)(tp.Number + 200)].UShortValue = (ushort)(_parentCS.manager.SubsystemZ[subsystemNumber].EquipID);
                            }
                        }
                        if (currentRoomNumber > 0)
                        {
                            _parentCS.subsystemEISC.UShortInput[(ushort)((tp.Number - 1) * 10 + 303)].UShortValue = _parentCS.manager.RoomZ[currentRoomNumber].LightsID;
                            _parentCS.subsystemEISC.UShortInput[(ushort)((tp.Number - 1) * 10 + 304)].UShortValue = _parentCS.manager.RoomZ[currentRoomNumber].ShadesID;

                            tp.CurrentClimateID = _parentCS.manager.RoomZ[currentRoomNumber].ClimateID;
                            _parentCS.climateControl.SyncPanelToClimateZone(tp.Number);//from whole house select zone
                        }
                    }
                };
            }
            //Music Source Selection
            for (int i = 0; i < tp._HTMLContract.musicSourceSelect.Length; i++)
            {
                int capturedIndex = i;
                tp._HTMLContract.musicSourceSelect[i].selectMusicSource += (sender, args) =>
                {
                    if (args.SigArgs.Sig.BoolValue) // Only on press, not release
                    {
                        ushort TPNumber = tp.Number;
                        ushort asrcButtonNumber = (ushort)(capturedIndex + 1);
                        //translate button number to music source number
                        ushort asrcScenario = _parentCS.manager.RoomZ[tp.CurrentRoomNum].AudioSrcScenario;
                        ushort asrcNumberToSend = _parentCS.manager.AudioSrcScenarioZ[asrcScenario].IncludedSources[asrcButtonNumber - 1];
                        _parentCS.musicSystemControl.PanelSelectMusicSource(TPNumber, asrcNumberToSend);

                        //if the music source sharing page is visible and there are zones checked, then update the zones with the new source
                        if (tp.UserInterface.BooleanInput[1002].BoolValue == true)
                        {
                            for (int j = 0; j < tp.MusicRoomsToShareSourceTo.Count; j++)
                            {
                                if (tp.MusicRoomsToShareCheckbox[j] == true)
                                {
                                    _parentCS.musicSystemControl.SwitcherSelectMusicSource(_parentCS.manager.RoomZ[tp.MusicRoomsToShareSourceTo[j]].AudioID, asrcNumberToSend);
                                    tp._HTMLContract.MusicRoomControl[j].musicZoneSource(
                                        (sig, wh) => sig.StringValue = _parentCS.manager.MusicSourceZ[asrcNumberToSend].Name);


                                }
                            }
                        }
                    }
                };
            }
            //Home Music Control
            for (int i = 0; i < tp._HTMLContract.HomeMusicZone.Length; i++)
            {
                int capturedIndex = i;
                tp._HTMLContract.HomeMusicZone[i].VolumeUp += (sender, args) =>
                {
                    if (capturedIndex >= _parentCS.HomePageMusicRooms.Count) return;
                    ushort roomListPosition = (ushort)(capturedIndex + 1);
                    ushort roomNumber = _parentCS.HomePageMusicRooms[roomListPosition - 1];
                    ushort audioID = _parentCS.manager.RoomZ[roomNumber].AudioID;
                    _parentCS.musicEISC1.BooleanInput[(ushort)(audioID)].BoolValue = args.SigArgs.Sig.BoolValue;
                };
                tp._HTMLContract.HomeMusicZone[i].VolumeDown += (sender, args) =>
                {
                    if (capturedIndex >= _parentCS.HomePageMusicRooms.Count) return;
                    ushort roomListPosition = (ushort)(capturedIndex + 1);
                    ushort roomNumber = _parentCS.HomePageMusicRooms[roomListPosition - 1];
                    ushort audioID = _parentCS.manager.RoomZ[roomNumber].AudioID;
                    _parentCS.musicEISC1.BooleanInput[(ushort)(audioID + 100)].BoolValue = args.SigArgs.Sig.BoolValue;
                };
                tp._HTMLContract.HomeMusicZone[i].SendMute += (sender, args) =>
                {
                    if (args.SigArgs.Sig.BoolValue)
                    {
                        if (capturedIndex >= _parentCS.HomePageMusicRooms.Count) return;
                        ushort roomListPosition = (ushort)(capturedIndex + 1);
                        ushort roomNumber = _parentCS.HomePageMusicRooms[roomListPosition - 1];
                        ushort audioID = _parentCS.manager.RoomZ[roomNumber].AudioID;
                        _parentCS.musicEISC1.BooleanInput[(ushort)(audioID + 200)].BoolValue = true;
                        _parentCS.musicEISC1.BooleanInput[(ushort)(audioID + 200)].BoolValue = false;
                    }
                };
                tp._HTMLContract.HomeMusicZone[i].SendPowerOff += (sender, args) =>
                {
                    if (args.SigArgs.Sig.BoolValue)
                    {
                        if (capturedIndex >= _parentCS.HomePageMusicRooms.Count) return;
                        ushort roomListPosition = (ushort)(capturedIndex + 1);
                        ushort roomNumber = _parentCS.HomePageMusicRooms[roomListPosition - 1];
                        ushort audioID = _parentCS.manager.RoomZ[roomNumber].AudioID;
                        _parentCS.musicEISC1.UShortInput[(ushort)(audioID + 500)].UShortValue = 0;
                        _parentCS.manager.RoomZ[roomNumber].CurrentMusicSrc = 0;
                    }
                };

            }
            //Music Control
            for (int i = 0; i < tp._HTMLContract.MusicRoomControl.Length; i++)
            {
                int capturedIndex = i;
                //Toggle Checkbox
                tp._HTMLContract.MusicRoomControl[i].selectMusicZone += (sender, args) =>
                {
                    if (args.SigArgs.Sig.BoolValue) // Only on press, not release
                    {
                        if (capturedIndex >= tp.MusicRoomsToShareSourceTo.Count) return;
                        ushort roomListPosition = (ushort)(capturedIndex + 1);
                        ushort roomNumber = tp.MusicRoomsToShareSourceTo[roomListPosition - 1];
                        ushort audioID = _parentCS.manager.RoomZ[roomNumber].AudioID;
                        //SET THE STATE OF THE CHECKBOX
                        tp.MusicRoomsToShareCheckbox[roomListPosition - 1] = !tp.MusicRoomsToShareCheckbox[roomListPosition - 1];
                        tp._HTMLContract.MusicRoomControl[capturedIndex].musicZoneSelected(
                            (sig, wh) => sig.BoolValue = tp.MusicRoomsToShareCheckbox[roomListPosition - 1]);
                        //SHOW OR HIDE THE VOLUME BUTTONS
                        tp._HTMLContract.MusicRoomControl[capturedIndex].musicVolEnable(
                            (sig, wh) => sig.BoolValue = tp.MusicRoomsToShareCheckbox[roomListPosition - 1]);
                        //if the checkbox is selected, then send the source to the room
                        if (tp.MusicRoomsToShareCheckbox[roomListPosition - 1])
                        {
                            ushort audioSrcNum = _parentCS.manager.RoomZ[tp.CurrentRoomNum].CurrentMusicSrc;
                            string audioSrcName = _parentCS.manager.MusicSourceZ[audioSrcNum].Name;
                            tp._HTMLContract.MusicRoomControl[capturedIndex].musicZoneSource(
                                (sig, wh) => sig.StringValue = audioSrcName);
                            tp._HTMLContract.MusicRoomControl[capturedIndex].musicVolume(
                                (sig, wh) => sig.UShortValue = _parentCS.manager.RoomZ[roomNumber].MusicVolume);
                            _parentCS.musicSystemControl.SwitcherSelectMusicSource(audioID, audioSrcNum);
                        }
                        else
                        {
                            tp._HTMLContract.MusicRoomControl[capturedIndex].musicZoneSource(
                                (sig, wh) => sig.StringValue = "Off");
                            _parentCS.musicSystemControl.SwitcherSelectMusicSource(audioID, 0);
                        }

                    }
                };
                tp._HTMLContract.MusicRoomControl[i].musicVolDown += (sender, args) =>
                {
                    if (capturedIndex >= tp.MusicRoomsToShareSourceTo.Count) return;
                    ushort roomListPosition = (ushort)(capturedIndex + 1);
                    ushort roomNumber = tp.MusicRoomsToShareSourceTo[roomListPosition - 1];
                    ushort audioID = _parentCS.manager.RoomZ[roomNumber].AudioID;
                    _parentCS.musicEISC1.BooleanInput[(ushort)(audioID + 100)].BoolValue = args.SigArgs.Sig.BoolValue;
                };
                tp._HTMLContract.MusicRoomControl[i].musicVolUp += (sender, args) =>
                {
                    if (capturedIndex >= tp.MusicRoomsToShareSourceTo.Count) return;
                    ushort roomListPosition = (ushort)(capturedIndex + 1);
                    ushort roomNumber = tp.MusicRoomsToShareSourceTo[roomListPosition - 1];
                    ushort audioID = _parentCS.manager.RoomZ[roomNumber].AudioID;
                    _parentCS.musicEISC1.BooleanInput[(ushort)(audioID)].BoolValue = args.SigArgs.Sig.BoolValue;
                };
                //MUTE
                tp._HTMLContract.MusicRoomControl[i].muteMusicZone += (sender, args) => {
                    if (args.SigArgs.Sig.BoolValue)
                    {
                        if (capturedIndex >= tp.MusicRoomsToShareSourceTo.Count) return;
                        ushort roomListPosition = (ushort)(capturedIndex + 1);
                        ushort roomNumber = tp.MusicRoomsToShareSourceTo[roomListPosition - 1];
                        ushort audioID = _parentCS.manager.RoomZ[roomNumber].AudioID;
                        _parentCS.musicEISC1.BooleanInput[(ushort)(audioID + 200)].BoolValue = true;
                        _parentCS.musicEISC1.BooleanInput[(ushort)(audioID + 200)].BoolValue = false;
                    }
                };
                //ZONE OFF - NOT currently used. the checkbox turns off the zone
                tp._HTMLContract.MusicRoomControl[i].turnMusicZoneOff += (sender, args) => {
                    if (args.SigArgs.Sig.BoolValue)
                    {
                        if (capturedIndex >= tp.MusicRoomsToShareSourceTo.Count) return;
                        ushort roomListPosition = (ushort)(capturedIndex + 1);
                        ushort roomNumber = tp.MusicRoomsToShareSourceTo[roomListPosition - 1];
                        ushort audioID = _parentCS.manager.RoomZ[roomNumber].AudioID;
                        _parentCS.musicEISC1.BooleanInput[(ushort)(audioID + 300)].BoolValue = true;
                        _parentCS.musicEISC1.BooleanInput[(ushort)(audioID + 300)].BoolValue = false;
                    }
                };
            }
            //Subsystem Select
            for (int i = 0; i < tp._HTMLContract.SubsystemButton.Length; i++)
            {
                int capturedIndex = i;
                tp._HTMLContract.SubsystemButton[i].SelectSubsystem += (sender, args) =>
                {
                    if (args.SigArgs.Sig.BoolValue)
                    {
                        ushort subsystemButtonNumber = (ushort)(capturedIndex + 1);
                        _parentCS.SelectSubsystem(tp.Number, subsystemButtonNumber);//from room subsystem
                    }
                };
            }
            //Video Source Selection
            for (int i = 0; i < tp._HTMLContract.vsrcButton.Length; i++)
            {
                int capturedIndex = i;
                tp._HTMLContract.vsrcButton[i].vidSelectSource += (sender, args) =>
                {
                    if (args.SigArgs.Sig.BoolValue) // Only on press, not release
                    {
                        ushort vsrcButtonNumber = (ushort)(capturedIndex + 1);
                        _parentCS.videoSystemControl.SelectVideoSourceFromTP(tp.Number, vsrcButtonNumber);
                    }
                };
            }
            //DVR Tab
            for (int i = 0; i < tp._HTMLContract.TabButton.Length; i++)
            {
                int capturedIndex = i;
                tp._HTMLContract.TabButton[i].TabSelected((sig, wh) => sig.BoolValue = (capturedIndex == 0));

                tp._HTMLContract.TabButton[i].TabSelect += (sender, args) =>
                {
                    if (args.SigArgs.Sig.BoolValue)
                    {
                        ushort buttonNumber = (ushort)(capturedIndex + 1);
                        for (int j = 0; j < tp._HTMLContract.TabButton.Length; j++)
                        {
                            tp._HTMLContract.TabButton[j].TabSelected((sig, wh) => sig.BoolValue = false);
                        }
                        tp._HTMLContract.TabButton[capturedIndex].TabSelected((sig, wh) => sig.BoolValue = true);
                        if (buttonNumber == 1)
                        {
                            _parentCS.manager.VideoSourceZ[tp.CurrentVSrcNum].CurrentSubpageScenario = 1;
                            tp.UserInterface.BooleanInput[141].BoolValue = true;
                            tp.UserInterface.BooleanInput[142].BoolValue = false;
                        }
                        else if (buttonNumber == 2)
                        {
                            _parentCS.manager.VideoSourceZ[tp.CurrentVSrcNum].CurrentSubpageScenario = 2;
                            tp.UserInterface.BooleanInput[141].BoolValue = false;
                            tp.UserInterface.BooleanInput[142].BoolValue = true;
                        }
                    }
                };
            }
            //Security Bypass
            for (int i = 0; i < tp._HTMLContract.SecurityZone.Length; i++)
            {
                int capturedIndex = i;
                tp._HTMLContract.SecurityZone[i].ZoneBypassTog += (sender, args) =>
                {
                    if (args.SigArgs.Sig.BoolValue) // Only on press, not release
                    {
                        ushort zoneButtonNumber = (ushort)(capturedIndex + 1);
                        _parentCS.securityEISC.BooleanInput[(ushort)(zoneButtonNumber + 85)].BoolValue = true;
                        _parentCS.securityEISC.BooleanInput[(ushort)(zoneButtonNumber + 85)].BoolValue = false;
                    }
                };
            }
            //Shades
            for (int i = 0; i < tp._HTMLContract.ShadeButtons.Length; i++)
            {
                //there are 200 button presses per panel on the subsystemControlEISC
                int capturedIndex = i;
                tp._HTMLContract.ShadeButtons[i].ShadeOpen += (sender, args) =>
                {
                    ushort buttonNumber = (ushort)(capturedIndex * 3 + 1);
                    _parentCS.subsystemControlEISC.BooleanInput[(ushort)((tp.Number - 1) * 200 + buttonNumber)].BoolValue = args.SigArgs.Sig.BoolValue;
                };
                tp._HTMLContract.ShadeButtons[i].ShadeClose += (sender, args) =>
                {
                    ushort buttonNumber = (ushort)(capturedIndex * 3 + 3);
                    _parentCS.subsystemControlEISC.BooleanInput[(ushort)((tp.Number - 1) * 200 + buttonNumber)].BoolValue = args.SigArgs.Sig.BoolValue;
                };
                tp._HTMLContract.ShadeButtons[i].ShadeStop += (sender, args) =>
                {
                    ushort buttonNumber = (ushort)(capturedIndex * 3 + 2);
                    _parentCS.subsystemControlEISC.BooleanInput[(ushort)((tp.Number - 1) * 200 + buttonNumber)].BoolValue = args.SigArgs.Sig.BoolValue;
                };
            }
        }
    }
}
