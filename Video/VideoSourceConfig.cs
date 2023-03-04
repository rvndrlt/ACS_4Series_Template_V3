﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Crestron.SimplSharp;

namespace ACS_4Series_Template_V2.VideoSources
{
    public class VideoSourceConfig
    {
        public VideoSourceConfig(ushort number, string name, string displayName, string iconSerial, string iconHTML, ushort analogModeNumber, ushort vidSwitcherInputNumber, ushort audSwitcherInputNumber, string streamLocation, string multiCastAddress, string aes67SessionName, ushort flipsToPageNumber, ushort equipID)
        {
            this.Number = number;
            this.Name = name;
            this.DisplayName = displayName;
            this.IconSerial = iconSerial;
            this.IconHTML = iconHTML;
            this.AnalogModeNumber = analogModeNumber;
            this.VidSwitcherInputNumber = vidSwitcherInputNumber;
            this.AudSwitcherInputNumber = audSwitcherInputNumber;
            this.StreamLocation = streamLocation;
            this.MultiCastAddress = multiCastAddress;
            this.AES67SessionName = aes67SessionName;
            this.FlipsToPageNumber = flipsToPageNumber;
            this.EquipID = equipID;
        }
        public ushort Number { get; set; }
        public string Name { get; set; }
        public string DisplayName { get; set; }
        public string IconSerial { get; set; }

        public string IconHTML { get; set; }
        public ushort AnalogModeNumber { get; set; }
        public ushort VidSwitcherInputNumber { get; set; }
        public ushort AudSwitcherInputNumber { get; set; }
        public string StreamLocation { get; set; }
        public string MultiCastAddress { get; set; }
        public string AES67SessionName { get; set; }
        public ushort FlipsToPageNumber { get; set; }
        public ushort EquipID { get; set; }
        public bool InUse { get; set; }
    }
}