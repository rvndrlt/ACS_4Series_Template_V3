using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Crestron.SimplSharp;

namespace ACS_4Series_Template_V1.SubsystemScenarios
{
    public class SubsystemScenarioConfig
    {
        public SubsystemScenarioConfig(ushort number, List<ushort> includedSubsystems)
        {
            this.Number = number;
            this.IncludedSubsystems = includedSubsystems;
           
        }
        public ushort Number { get; set; }
        public List<ushort> IncludedSubsystems { get; set; }
    }
}