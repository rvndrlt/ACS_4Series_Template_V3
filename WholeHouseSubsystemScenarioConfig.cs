using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Crestron.SimplSharp;

namespace ACS_4Series_Template_V2.WholeHouseSubsystemScenarios
{

    public class WholeHouseSubsystemScenarioConfig
    {

        public List<WholeHouseSubsysScenario> WholeHouseSubsysScenarios { get; set; }
    }

    public class WholeHouseSubsysScenario
    {
        public WholeHouseSubsysScenario(ushort scenarioNumber, List<WholeHouseSubsystem> wholeHouseSubsystems)
        {
            this.ScenarioNumber = scenarioNumber;
            this.WholeHouseSubsystems = wholeHouseSubsystems;
        }

        public ushort ScenarioNumber { get; set; }
        public List<WholeHouseSubsystem> WholeHouseSubsystems { get; set; }
    }
    public class WholeHouseSubsystem
    {
        public WholeHouseSubsystem(ushort subsystemNumber, List<ushort> includedRooms)
        {
            this.SubsystemNumber = subsystemNumber;
            this.IncludedRooms = includedRooms;
        }
        public ushort SubsystemNumber { get; set; }
        public List<ushort> IncludedRooms { get; set; }
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
