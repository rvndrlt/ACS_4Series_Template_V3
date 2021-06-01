using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Crestron.SimplSharp;

namespace ACS_4Series_Template_V1.AudioSrcSharingScenarios
{
    public class AudioSrcSharingScenariosConfig
    {
        public AudioSrcSharingScenariosConfig(ushort number, List<ushort> includedZones)
        {
            this.Number = number;
            this.IncludedZones = includedZones;
           
        }
        public ushort Number { get; set; }
        public List<ushort> IncludedZones { get; set; }
    }
}