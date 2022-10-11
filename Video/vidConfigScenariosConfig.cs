using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Crestron.SimplSharp;

namespace ACS_4Series_Template_V2.VidConfigScenarios
{
    public class VidConfigScenariosConfig
    {
        public VidConfigScenariosConfig(ushort number, bool sendToSpeakers, ushort offSubScenarioNum, bool hasReceiver, bool receiverHasVolFB, ushort receiverInputDelay, ushort musicThroughReceiver, bool receiverHasBreakawayAudio, bool musicHasVolFB, bool videoVolThroughDistAudio, ushort displayInputDelay, bool tvHasVolFB)
        {
            this.Number = number;
            this.SendToSpeakers = sendToSpeakers;
            this.OffSubScenarioNum = offSubScenarioNum;
            this.HasReceiver = hasReceiver;
            this.ReceiverHasVolFB = receiverHasVolFB;
            this.ReceiverInputDelay = receiverInputDelay;
            this.MusicThroughReceiver = musicThroughReceiver;
            this.ReceiverHasBreakawayAudio = receiverHasBreakawayAudio;
            this.MusicHasVolFB = musicHasVolFB;
            this.VideoVolThroughDistAudio = videoVolThroughDistAudio;
            this.DisplayInputDelay = displayInputDelay;
            this.TvHasVolFB = tvHasVolFB;
        }
        public ushort Number { get; set; }
        public bool SendToSpeakers { get; set; }
        public ushort OffSubScenarioNum { get; set; }
        public bool HasReceiver { get; set; }
        public bool ReceiverHasVolFB { get; set; }
        public ushort ReceiverInputDelay { get; set; }
        public ushort MusicThroughReceiver { get; set; }
        public bool ReceiverHasBreakawayAudio { get; set; }
        public bool MusicHasVolFB { get; set; }
        public bool VideoVolThroughDistAudio { get; set; }
        public ushort DisplayInputDelay { get; set; }
        public bool TvHasVolFB { get; set; }
    }
}