using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Crestron.SimplSharp;

namespace ACS_4Series_Template_V3.FloorScenarios
{
    public class FloorScenariosConfig
    {
        public FloorScenariosConfig(ushort number, List<ushort> includedFloors)
        {
            this.Number = number;
            this.IncludedFloors = includedFloors;

        }
        public ushort Number { get; set; }
        
        public List<ushort> IncludedFloors { get; set; }
        

    }
    public class FloorConfig
    {
        public FloorConfig(ushort number, string name, List<ushort> includedRooms)
        {
            this.FloorNumber = number;
            this.Name = name;
            this.IncludedRooms = includedRooms;
        }
        public ushort FloorNumber { get; set; }
        public string Name { get; set; }
        public ushort NumberOfZones { get; set; }
        public List<ushort> IncludedRooms { get; set; }

    }

}