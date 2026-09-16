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
        public SubsystemConfig(ushort number, string name, string displayName, string iconSerial, string iconHTML, ushort analogModeNumber, ushort flipsToPageNumber, ushort equipID, string iPaddress, string eiscIpIdHex = null, ushort guiScenarioNumber = 0, bool quickActionsEnabled = true, string[] eiscExtraIpIdsHex = null, string shadesEiscIpIdHex = null, string[] shadesEiscExtraIpIdsHex = null)
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
            this.GuiScenarioNumber = guiScenarioNumber;
            this.QuickActionsEnabled = quickActionsEnabled;
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

            ExtraIPIDs = ParseIpIdList(eiscExtraIpIdsHex);
            ShadesIPID = ParseIpId(shadesEiscIpIdHex);
            ShadesExtraIPIDs = ParseIpIdList(shadesEiscExtraIpIdsHex);
        }

        /// <summary>A single hex IPID, with or without the 0x prefix. 0 when absent or junk.</summary>
        private static uint ParseIpId(string hexId)
        {
            if (string.IsNullOrWhiteSpace(hexId)) return 0;
            var hex = hexId.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
                ? hexId.Substring(2)
                : hexId;
            uint parsed;
            if (!uint.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out parsed))
            {
                ErrorLog.Error("SubsystemConfig: \"{0}\" is not a hex IPID - ignoring it", hexId);
                return 0;
            }
            return parsed;
        }

        /// <summary>
        /// Order is the contract here, not just a list: the nth entry carries panel slots
        /// 20n..20n+19, so an unparseable entry is dropped rather than silently shifting every
        /// later bank down one.
        /// </summary>
        private static uint[] ParseIpIdList(string[] hexIds)
        {
            if (hexIds == null) return new uint[0];
            var ids = new List<uint>();
            foreach (var hexId in hexIds)
            {
                uint parsed = ParseIpId(hexId);
                if (parsed != 0) ids.Add(parsed);
            }
            return ids.ToArray();
        }
        public ushort Number { get; set; }
        public string Name { get; set; }
        public string DisplayName { get; set; }
        public string IconSerial { get; set; }

        public string IconHTML { get; set; }
        public ushort AnalogModeNumber { get; set; }
        public ushort FlipsToPageNumber { get; set; }
        public ushort GuiScenarioNumber { get; set; }
        public ushort EquipID { get; set; }
        public string IPaddress { get; set; }
        public uint IPID { get; set; }

        /// <summary>Extra EISC IPIDs for this subsystem, twenty panel slots each.</summary>
        public uint[] ExtraIPIDs { get; set; }

        /// <summary>Room-based shades EISC IPID, or 0 to take the 0xB4 default.</summary>
        public uint ShadesIPID { get; set; }

        /// <summary>Extra shade EISC IPIDs, twenty panel slots each.</summary>
        public uint[] ShadesExtraIPIDs { get; set; }
        /// <summary>When false, panels cannot save Quick Actions for this subsystem.</summary>
        public bool QuickActionsEnabled { get; set; }
    }
}