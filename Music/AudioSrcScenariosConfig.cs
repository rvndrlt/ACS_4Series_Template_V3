using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Crestron.SimplSharp;

namespace ACS_4Series_Template_V1.AudioSrcScenarios
{
    public class AudioSrcScenariosConfig
    {
        public AudioSrcScenariosConfig(ushort number, List<ushort> includedSources, List<ushort> receiverInputs)
        {
            this.Number = number;
            this.IncludedSources = includedSources;
            this.ReceiverInputs = receiverInputs;    
        }
        public ushort Number { get; set; }
        public List<ushort> IncludedSources { get; set; }
        public List<ushort> ReceiverInputs { get; set; }
    }
}