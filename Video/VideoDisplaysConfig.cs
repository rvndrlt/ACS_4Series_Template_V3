using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Crestron.SimplSharp;

namespace ACS_4Series_Template_V3.VideoDisplays
{
    public class VideoDisplaysConfig
    {
        public event Action<ushort, string> VideoStatusChanged;
        private readonly ControlSystem _parent;
        public VideoDisplaysConfig(ControlSystem parent, ushort number, string displayName, ushort assignedToRoomNum, ushort videoOutputNum, ushort videoSrcScenario, ushort vidConfigurationScenario, ushort liftScenario, ushort formatScenario, ushort tvOutToAudioInputNumber, List<ushort> tieToDisplayNumbers)
        {
            _parent = parent ?? throw new ArgumentNullException(nameof(parent), "Parent cannot be null.");
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
        // Must never be null. A display whose CurrentVideoSrc has not been assigned yet would
        // otherwise report null status, and RoomConfig.OnDisplayStatusChanged propagates that
        // straight into the room's VideoStatusTextOff -> SetSubsystemStatus -> sig.StringValue,
        // which throws and aborts the whole subsystem-subscribe loop — blanking EVERY subsystem
        // button, not just Video. Seeded with the same text updateVideoStatusText() produces
        // for "no source".
        private string _videoStatusText = "Off ";
        private string _videoStatusTextInternal = "";
        private ushort _currentVideoSrc;
        public ushort Number { get; set; }
        public string DisplayName { get; set; }
        public ushort AssignedToRoomNum { get; set; }
        public ushort VideoOutputNum { get; set; }
        public ushort VideoSourceScenario { get; set; }
        public ushort CurrentVideoSrc
        {
            get => _currentVideoSrc;
            set
            {
                _currentVideoSrc = value;
                updateVideoStatusText();
            }
        }
        public string CurrentSourceText { get; set; }
        public ushort VidConfigurationScenario { get; set; }
        public ushort LiftScenario { get; set; }
        public ushort FormatScenario { get; set; }
        public ushort TvOutToAudioInputNumber { get; set; }

        public List<ushort> TieToDisplayNumbers { get; set; }

        public string VideoStatusText
        {
            get => _videoStatusText;
            private set
            {
                if (_videoStatusText != value)
                {
                    _videoStatusText = value;
                    VideoStatusChanged?.Invoke(Number, _videoStatusText); // Notify subscribers
                }
            }
        }
        private void updateVideoStatusText()
        {
            if (CurrentVideoSrc == 0)
            {
                VideoStatusText = "Off ";
                _videoStatusTextInternal = "";
            }
            else
            {
                VideoStatusText = _parent.manager.VideoSourceZ[CurrentVideoSrc].DisplayName + " is on. ";
                _videoStatusTextInternal = _parent.manager.VideoSourceZ[CurrentVideoSrc].DisplayName + " is on. ";
            }
        }

    }
}
