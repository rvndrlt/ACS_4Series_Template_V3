using Crestron.SimplSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ACS_4Series_Template_V3.Music
{
    public class SWAMP
    {
        private ControlSystem _parent;

        public SWAMP(ControlSystem parent)
        {
            _parent = parent;
        }
        public void SwampOutputSrcChanged(ushort zoneNumber, ushort switcherInputNumber)
        {

            ushort switcherOutputNumber = (ushort)(zoneNumber - 500); //switcher output number
            ushort sourceNumber = 0;

            //translate source number from switcher input number if this is a swamp
            //then update the 'in use' status of the source
            for (ushort i = 1; i <= _parent.manager.MusicSourceZ.Count; i++)
            {
                if (_parent.manager.MusicSourceZ[i].SwitcherInputNumber == switcherInputNumber)
                {
                    sourceNumber = i;
                }
            }            //turn the receiver on or off
            foreach (var room in _parent.manager.RoomZ)
            {
                if (room.Value.AudioID == switcherOutputNumber)
                {
                    _parent.musicSystemControl.ReceiverOnOffFromDistAudio(room.Value.Number, sourceNumber);//on from swamp
                    room.Value.UpdateMusicSrcStatus(sourceNumber);//from SwampOutputSrcChanged
                }
            }
            CrestronConsole.PrintLine("SWAMP zoneNumber {0} switcherInputNumber {1}", switcherOutputNumber, switcherInputNumber);
            _parent.musicSystemControl.updateMusicSourceInUse(sourceNumber, switcherInputNumber, switcherOutputNumber);
        }
    }
}
