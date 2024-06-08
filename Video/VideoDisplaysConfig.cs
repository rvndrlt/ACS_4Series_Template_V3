using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Crestron.SimplSharp;

namespace ACS_4Series_Template_V3.VideoDisplays
{
    public class VideoDisplaysConfig
    {
        public VideoDisplaysConfig(ushort number, string displayName, ushort assignedToRoomNum, ushort videoOutputNum, ushort videoSrcScenario, ushort vidConfigurationScenario, ushort liftScenario, ushort formatScenario, ushort tvOutToAudioInputNumber, List<ushort> tieToDisplayNumbers)
        {
            this.Number = number;
            this.DisplayName = displayName;
            this.AssignedToRoomNum = assignedToRoomNum;
            this.VideoOutputNum = videoOutputNum;
            this.VideoSourceScenario = videoSrcScenario;
            this.VidConfigurationScenario = vidConfigurationScenario;
            this.LiftScenario = liftScenario;
            this.FormatScenario = formatScenario;
            this.TvOutToAudioInputNumber = tvOutToAudioInputNumber;
            this.TieToDisplayNumbers = tieToDisplayNumbers;
        }
        public ushort Number { get; set; }
        public string DisplayName { get; set; }
        public ushort AssignedToRoomNum { get; set; }
        public ushort VideoOutputNum { get; set; }
        public ushort VideoSourceScenario { get; set; }
        public ushort CurrentVideoSrc { get; set; }
        public string CurrentSourceText { get; set; }
        public ushort VidConfigurationScenario { get; set; }
        public ushort LiftScenario { get; set; }
        public ushort FormatScenario { get; set; }
        public ushort TvOutToAudioInputNumber { get; set; }

        public List<ushort> TieToDisplayNumbers { get; set; }

    }
}
