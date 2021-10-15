using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Crestron.SimplSharp;

namespace ACS_4Series_Template_V1.MusicSources
{
    public class MusicSourceConfig
    {
        public MusicSourceConfig(ushort number, string name, string iconSerial, string iconHTML, ushort analogModeNumber, ushort switcherInputNumber, ushort flipsToPageNumber, ushort equipID)
        {
            this.Number = number;
            this.Name = name;
            this.IconSerial = iconSerial;
            this.IconHTML = iconHTML;
            this.AnalogModeNumber = analogModeNumber;
            this.SwitcherInputNumber = switcherInputNumber;
            this.FlipsToPageNumber = flipsToPageNumber;
            this.EquipID = equipID;
        }
        public ushort Number { get; set; }
        public string Name { get; set; }
        public string IconSerial { get; set; }

        public string IconHTML { get; set; }
        public ushort AnalogModeNumber { get; set; }
        public ushort SwitcherInputNumber { get; set; }
        public ushort FlipsToPageNumber { get; set; }
        public ushort EquipID { get; set; }
        public bool InUse { get; set; }
    }
}