using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Crestron.SimplSharp;

namespace ACS_4Series_Template_V2.Subsystem
{
    public class SubsystemConfig
    {
        public SubsystemConfig(ushort number, string name, string displayName, string iconSerial, string iconHTML, ushort analogModeNumber, ushort flipsToPageNumber, ushort equipID)
        {
            this.Number = number;
            this.Name = name;
            this.DisplayName = displayName;
            this.IconSerial = iconSerial;
            this.IconHTML = iconHTML;
            this.AnalogModeNumber = analogModeNumber;
            this.FlipsToPageNumber = flipsToPageNumber;
            this.EquipID = equipID;
           
        }
        public ushort Number { get; set; }
        public string Name { get; set; }
        public string DisplayName { get; set; }
        public string IconSerial { get; set; }

        public string IconHTML { get; set; }
        public ushort AnalogModeNumber { get; set; }
        public ushort FlipsToPageNumber { get; set; }
        public ushort EquipID { get; set; }
    }
}