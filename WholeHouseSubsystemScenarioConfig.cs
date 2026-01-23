using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Crestron.SimplSharp;

namespace ACS_4Series_Template_V3.WholeHouseSubsystemScenarios
{
    public class WholeHouseSubsystemScenarioConfig
    {
        public WholeHouseSubsystemScenarioConfig(ushort scenarioNumber, List<WholeHouseSubsystem> wholeHouseSubsystems)
        {
            this.ScenarioNumber = scenarioNumber;
            this.WholeHouseSubsystems = wholeHouseSubsystems;
        }

        public ushort ScenarioNumber { get; set; }
        public List<WholeHouseSubsystem> WholeHouseSubsystems { get; set; }
    }
    public class WholeHouseSubsystem
    {
        public WholeHouseSubsystem(ushort subsystemNumber, List<ushort> includedFloors)
        {
            this.SubsystemNumber = subsystemNumber;
            this.IncludedFloors = includedFloors;
        }
        public ushort SubsystemNumber { get; set; }
        public List<ushort> IncludedFloors { get; set; }
    }

    /*public class WholeHouseSubsystemScenarioConfig
    {
        public WholeHouseSubsystemScenarioConfig(ushort number, List<ushort> includedSubsystems)
        {
            this.Number = number;
            this.IncludedSubsystems = includedSubsystems;

        }
        public ushort Number { get; set; }
        public List<ushort> IncludedSubsystems { get; set; }
    }*/
}
