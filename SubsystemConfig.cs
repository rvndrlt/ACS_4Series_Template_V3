using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Text;
using Crestron.SimplSharp;

namespace ACS_4Series_Template_V3.Subsystem
{
    public class SubsystemConfig
    {
        public SubsystemConfig(ushort number, string name, string displayName, string iconSerial, string iconHTML, ushort analogModeNumber, ushort flipsToPageNumber, ushort equipID, string iPaddress, string eiscIpIdHex = null)
        {
            this.Number = number;
            this.Name = name;
            this.DisplayName = displayName;
            this.IconSerial = iconSerial;
            this.IconHTML = iconHTML;
            this.AnalogModeNumber = analogModeNumber;
            this.FlipsToPageNumber = flipsToPageNumber;
            this.EquipID = equipID;
            this.IPaddress = iPaddress;
            if (!string.IsNullOrWhiteSpace(eiscIpIdHex))
            {
                var hex = eiscIpIdHex.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
                            ? eiscIpIdHex.Substring(2)
                            : eiscIpIdHex;

                if (!uint.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var parsed))
                    parsed = 0;

                IPID = parsed;
            }
            else
            {
                IPID = 0;
            }
        }
        public ushort Number { get; set; }
        public string Name { get; set; }
        public string DisplayName { get; set; }
        public string IconSerial { get; set; }

        public string IconHTML { get; set; }
        public ushort AnalogModeNumber { get; set; }
        public ushort FlipsToPageNumber { get; set; }
        public ushort EquipID { get; set; }
        public string IPaddress { get; set; }
        public uint IPID { get; set; }
    }
}