using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Crestron.SimplSharp;

namespace ACS_4Series_Template_V3.VideoSrcScenarios
{
    public class VideoSrcScenariosConfig
    {
        public VideoSrcScenariosConfig(ushort number, List<ushort> includedSources, List<ushort> displayInputs, List<ushort> receiverInputs, List<ushort> altSwitcherInputs)
        {
            this.Number = number;
            this.IncludedSources = includedSources;
            this.DisplayInputs = displayInputs;
            this.ReceiverInputs = receiverInputs;
            this.AltSwitcherInputs = altSwitcherInputs;

        }
        public ushort Number { get; set; }
        public List<ushort> IncludedSources { get; set; }
        public List<ushort> DisplayInputs { get; set; }
        public List<ushort> ReceiverInputs { get; set; }
        public List<ushort> AltSwitcherInputs { get; set; }
    }
}