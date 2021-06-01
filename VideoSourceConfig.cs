using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Crestron.SimplSharp;

namespace ACS_4Series_Template_V1.VideoSources
{
    public class VideoSourceConfig
    {
        public VideoSourceConfig(ushort number, string name, string displayName, string iconSerial, ushort analogModeNumber, ushort vidSwitcherInputNumber, ushort audSwitcherInputNumber, ushort flipsToPageNumber, ushort equipID)
        {
            this.Number = number;
            this.Name = name;
            this.DisplayName = displayName;
            this.IconSerial = iconSerial;
            this.AnalogModeNumber = analogModeNumber;
            this.VidSwitcherInputNumber = vidSwitcherInputNumber;
            this.AudSwitcherInputNumber = audSwitcherInputNumber;
            this.FlipsToPageNumber = flipsToPageNumber;
            this.EquipID = equipID;
        }
        public ushort Number { get; set; }
        public string Name { get; set; }
        public string DisplayName { get; set; }
        public string IconSerial { get; set; }
        public ushort AnalogModeNumber { get; set; }
        public ushort VidSwitcherInputNumber { get; set; }
        public ushort AudSwitcherInputNumber { get; set; }
        public ushort FlipsToPageNumber { get; set; }
        public ushort EquipID { get; set; }
        public bool InUse { get; set; }
    }
}