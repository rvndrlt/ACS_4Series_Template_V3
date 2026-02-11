using Crestron.SimplSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ACS_4Series_Template_V3.Music
{

    public class NAX
    {
        public CTimer NAXoutputChangedTimer;
        public CTimer NAXoffTimer;
        public bool NAXAllOffBusy = false;
        private ControlSystem _parent;
        public string[] currentProviders = new string[100];
        public NAX(ControlSystem parent)
        {
            _parent = parent;
        }

        public void NAXOutputSrcChanged(ushort switcherOutputNumber, ushort switcherInputNumber)
        {
            if (switcherInputNumber != 17 && !NAXAllOffBusy)
            {
                CrestronConsole.PrintLine("!!!!!!start NAX outputsrc chagnged {0}:{1} INPUT#{2}-OUTPUT#{3}------------------", DateTime.Now.Second, DateTime.Now.Millisecond, switcherInputNumber, switcherOutputNumber);
                ushort currentMusicSource = 0;
                ushort currentRmNum = 0;

                //zone source is off
                if (switcherInputNumber == 0) { _parent.musicEISC3.StringInput[(ushort)(switcherOutputNumber + 500)].StringValue = "Off"; }
                //GET THE CURRENT SOURCE
                else
                {
                    //get the box number
                    int boxNumber = ((switcherOutputNumber - 1) / 8) + 1;
                    CrestronConsole.PrintLine("FB FROM NAX box {0} zone {1} input {2}", boxNumber, switcherOutputNumber, switcherInputNumber);
                    //now find out which source was selected
                    foreach (var src in _parent.manager.MusicSourceZ)
                    {
                        //if the nax box number and the input number match
                        if (src.Value.NaxBoxNumber == boxNumber && src.Value.SwitcherInputNumber == switcherInputNumber)
                        {
                            currentMusicSource = src.Value.Number; //we found the source
                        }
                    }
                }

                //send the source name to the audio zone module
                if (currentMusicSource > 0)
                {
                    _parent.musicEISC3.StringInput[(ushort)(switcherOutputNumber + 500)].StringValue = _parent.manager.MusicSourceZ[currentMusicSource].Name;
                }



                //update the room to reflect the current source
                foreach (var rm in _parent.manager.RoomZ)
                {
                    if (rm.Value.AudioID == switcherOutputNumber)
                    {
                        rm.Value.UpdateMusicSrcStatus(currentMusicSource);//from NAXOutputSrcChanged
                    }
                }
                if (currentRmNum > 0) { _parent.musicSystemControl.ReceiverOnOffFromDistAudio(currentRmNum, currentMusicSource); } //from NAX output changed
                //UpdateMusicTextForPanelsOnSwitcherOutputNumber(switcherOutputNumber);//from NAX output changed
                CrestronConsole.PrintLine("!!!!!END NAXoutputsrcchanged {0}:{1}--------------------", DateTime.Now.Second, DateTime.Now.Millisecond);
            }
        }
        public void StreamingPlayerProviderChanged(ushort playerNumber, ushort providerNumber)
        {
            switch (providerNumber)
            {

                case (0):
                    {
                        currentProviders[playerNumber] = "";
                        break;
                    }
                case (1): break;
                case (2):
                    {
                        currentProviders[playerNumber] = "Airplay";
                        break;
                    }
                case (3):
                    {
                        currentProviders[playerNumber] = "Spotify Connect";
                        break;
                    }
                case (4):
                    {
                        currentProviders[playerNumber] = "Sirius XM";
                        break;
                    }
                case (5):
                    {
                        currentProviders[playerNumber] = "Pandora";
                        break;
                    }
                case (6):
                    {
                        currentProviders[playerNumber] = "iHeart Radio";
                        break;
                    }
                case (7):
                    {
                        currentProviders[playerNumber] = "Internet Radio";
                        break;
                    }
                case (8):
                    {
                        currentProviders[playerNumber] = "Podcasts";
                        break;
                    }
                default:
                    break;
            }

        }

        public void NAXZoneMulticastChanged(ushort switcherOutputNumber, string multiAddress)
        {
            if (multiAddress != "0.0.0.0" && multiAddress != "" && multiAddress != null) //we don't want to do anything if the zone is turned off. the input will handle that case 
            {
                ushort currentMusicSource = 0;
                CrestronConsole.PrintLine("NAXZoneMulticastChanged - zone {1} multi address changed to = {0} {2}:{3}", multiAddress, switcherOutputNumber, DateTime.Now.Second, DateTime.Now.Millisecond);

                //figure out which music source this is
                foreach (var src in _parent.manager.MusicSourceZ)
                {
                    if (src.Value.MultiCastAddress == multiAddress) { currentMusicSource = src.Value.Number; }
                }
                foreach (var rm in _parent.manager.RoomZ)
                {
                    if (rm.Value.AudioID == switcherOutputNumber)
                    {
                        rm.Value.UpdateMusicSrcStatus(currentMusicSource);//from NAXZoneMulticastChanged
                    }
                }
                if (currentMusicSource > 0)
                {
                    _parent.musicEISC3.StringInput[(ushort)(switcherOutputNumber + 500)].StringValue = _parent.manager.MusicSourceZ[currentMusicSource].Name;//update the current source to the zone module which also updates the sharing page
                    _parent.musicSystemControl.updateMusicSourceInUse(currentMusicSource, _parent.manager.MusicSourceZ[currentMusicSource].SwitcherInputNumber, switcherOutputNumber);
                }
                CrestronConsole.PrintLine("END NAXZoneMulticastChanged {0}:{1}", DateTime.Now.Second, DateTime.Now.Millisecond);
            }
        }
        public void NAXAllOffCallback(object obj)
        {
            NAXoffTimer.Stop();
            NAXoffTimer.Dispose();
            NAXAllOffBusy = false;
            CrestronConsole.PrintLine("##############     HA FLOOR / ALL OFF CALLBACK {0}:{1}", DateTime.Now.Second, DateTime.Now.Millisecond);
        }
        //TO DO !!!! add a lambda to send the preset number to recall and attach it to the callback
    }
}
