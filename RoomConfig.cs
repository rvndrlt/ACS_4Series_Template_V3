using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Crestron.SimplSharp;

namespace ACS_4Series_Template_V3.Room
{

    public class RoomConfig
    {
        public ushort CurrentTemperature
        {
            get => _currentTemperature;
            set
            {
                _currentTemperature = value;
                UpdateHVACStatusText();//current temp
            }
        }

        public ushort CurrentHeatSetpoint
        {
            get => _currentHeatSetpoint;
            set
            {
                _currentHeatSetpoint = value;
                UpdateHVACStatusText();//heat setpoint
            }
        }

        public ushort CurrentCoolSetpoint
        {
            get => _currentCoolSetpoint;
            set
            {
                _currentCoolSetpoint = value;
                UpdateHVACStatusText();//cool setpoint
            }
        }

        public ushort CurrentAutoSingleSetpoint
        {
            get => _currentAutoSingleSetpoint;
            set
            {
                _currentAutoSingleSetpoint = value;
                UpdateHVACStatusText();//auto setpoint
            }
        }

        public string ClimateMode
        {
            get => _climateMode;
            set
            {
                _climateMode = value;
                UpdateHVACStatusText();//climate mode
            }
        }
        public string HVACStatusText
        {
            get => _hvacStatusText;
            private set
            {
                if (_hvacStatusText != value)
                {
                    _hvacStatusText = value;
                    HVACStatusChanged?.Invoke(Number, _hvacStatusText); // Notify subscribers
                }
            }
        }
        public string LightStatusText
        {
            get => _lightStatusText;
            private set
            {
                if (_lightStatusText != value)
                {
                    _lightStatusText = value;
                    CrestronConsole.PrintLine("LightStatusText: " + _lightStatusText);
                    LightStatusChanged?.Invoke(Number, _lightStatusText); // Notify subscribers
                }
            }
        }

        public string VideoSrcStatusText
        {
            get => _videoStatusText;
            private set
            {
                if (_videoStatusText != value)
                {
                    _videoStatusText = value;
                    VideoStatusTextChanged?.Invoke(Number, _videoStatusText); // Notify subscribers
                }
            }
        }
        public string VideoStatusTextOff
        {
            get => _videoStatusTextOff;
            set
            {
                if (_videoStatusTextOff != value)
                {
                    _videoStatusTextOff = value;
                    VideoStatusTextOffChanged?.Invoke(Number, _videoStatusTextOff);
                }
            }
        }
        /// <summary>
        /// Whenever CurrentDisplayNumber changes we unhook the old display
        /// and hook the new one so UpdateVideoSrcStatus always reflects the
        /// "current" display.
        /// </summary>
        public void BindToCurrentDisplay()
        {
            // unhook old
            if (_boundDisplay != null)
                _boundDisplay.VideoStatusChanged -= OnDisplayStatusChanged;

            // hook new
            if (CurrentDisplayNumber > 0
                && _parent.manager.VideoDisplayZ.TryGetValue(CurrentDisplayNumber, out var disp))
            {
                _boundDisplay = disp;
                _boundDisplay.VideoStatusChanged += OnDisplayStatusChanged;
                // initialize room text immediately from that display
                OnDisplayStatusChanged(_boundDisplay.Number, _boundDisplay.VideoStatusText);
            }
            else
            {
                _boundDisplay = null;
                UpdateVideoSrcStatus(0);      // force “off”
            }
        }
        void OnDisplayStatusChanged(ushort displayNumber, string newStatusText)
        {
            // this is the same text that used to live in _videoStatusText
            VideoSrcStatusText = newStatusText;
            VideoStatusTextOff = newStatusText == "" ? "Off!" : newStatusText;
            // fire the button‐feedback event
            NotifyVideoSourceChanged();//from OnDisplayStatusChanged
            // re-compute your combined RoomStatusText
            updateRoomStatusText();
        }
        public void UpdateVideoSrcStatus(ushort newVideoSrc)
        {
            CurrentVideoSrc = newVideoSrc;
            if (CurrentVideoSrc > 0 && _parent.manager.VideoSourceZ.ContainsKey(CurrentVideoSrc))
            {
                CrestronConsole.PrintLine("UpdateVideoSrcStatus CurrentVideoSrc#: " + CurrentVideoSrc + " " + _parent.manager.VideoSourceZ[CurrentVideoSrc].DisplayName);
                VideoSrcStatusText = _parent.manager.VideoSourceZ[CurrentVideoSrc].DisplayName + " is on. ";
                VideoStatusTextOff = _parent.manager.VideoSourceZ[CurrentVideoSrc].DisplayName + " is on. ";
            }
            else
            {
                CrestronConsole.PrintLine("UpdateVideoSrcStatus: Video source off (CurrentVideoSrc = " + CurrentVideoSrc + ")");
                VideoSrcStatusText = "";
                VideoStatusTextOff = "Off";
            }
            NotifyVideoSourceChanged();
            updateRoomStatusText();
        }
        public void NotifyVideoSourceChanged()
        {
            if (CurrentVideoSrc > 0 && _parent.manager.VideoSourceZ.ContainsKey(CurrentVideoSrc))
            {
                var videoSource = _parent.manager.VideoSourceZ[CurrentVideoSrc];
                //this is for the button feedback
                ushort numSrcs = (ushort)_parent.manager.VideoSrcScenarioZ[VideoSrcScenario].IncludedSources.Count;
                ushort buttonNum = 0;
                for (ushort i = 0; i < numSrcs; i++)//loop through all video sources in this scenario
                {
                    ushort srcNum = _parent.manager.VideoSrcScenarioZ[VideoSrcScenario].IncludedSources[i];
                    if (srcNum == this.CurrentVideoSrc)
                    {
                        buttonNum = (ushort)(i + 1);//video button fb
                        
                    }
                }
                // Invoke the event with all required properties
                VideoSrcStatusChanged?.Invoke(
                    videoSource.FlipsToPageNumber, // FlipsToPageNumber
                    videoSource.EquipID,      // Equipment ID
                    videoSource.DisplayName,         // Video Source Name
                    buttonNum                      // No button number for video source

                );
            }
            else
            {
                VideoSrcStatusChanged?.Invoke(
                    0,           // No page flip
                    0,           // No equipment ID
                    "Off",       // Empty string for name
                    0            // No button number for video source
                );
            }
        }
        public string RoomStatusText
        {
            get => _roomStatusText;
            private set
            {
                if (_roomStatusText != value)
                {
                    _roomStatusText = value;
                    RoomStatusTextChanged?.Invoke(Number, _roomStatusText); // Notify subscribers
                }
            }
        }
        public string MusicSrcStatusText
        {
            get => _musicStatusText;
            private set
            {
                if (_musicStatusText != value)
                {
                    //CrestronConsole.PrintLine("MusicSrcStatusText: " + value);
                    _musicStatusText = value;
                    MusicStatusTextChanged?.Invoke(Number, _musicStatusText);
                }
            }
        }
        public string MusicStatusTextOff
        {
            get => _musicStatusTextOff;
            set
            {
                if (_musicStatusTextOff != value)
                {
                    //CrestronConsole.PrintLine("MusicStatusTextOff: " + value);
                    _musicStatusTextOff = value;
                    MusicStatusTextOffChanged?.Invoke(Number, _musicStatusTextOff);
                }
            }
        }

        public void UpdateMusicSrcStatus(ushort newMusicSrc)
        {
            //CrestronConsole.PrintLine("UpdateMusicSrcStatus CurrentMusicSrc#: " + newMusicSrc);
            CurrentMusicSrc = newMusicSrc;
            if (CurrentMusicSrc > 0)
            {
                MusicSrcStatusText = _parent.manager.MusicSourceZ[CurrentMusicSrc].Name + " is playing. ";//from UpdateMusicSrcStatus
                MusicStatusTextOff = _parent.manager.MusicSourceZ[CurrentMusicSrc].Name + " is playing. ";
            }
            else
            {
                MusicSrcStatusText = "";
                MusicStatusTextOff = "Off";
            }
            NotifyMusicSourceChanged();//updateMusicSrcStatus
            updateRoomStatusText();
        }
        private void NotifyMusicSourceChanged()
        {
            //CrestronConsole.PrintLine("NotifyMusicSourceChanged CurrentMusicSrc#: " + CurrentMusicSrc);
            if (CurrentMusicSrc > 0 && _parent.manager.MusicSourceZ.ContainsKey(CurrentMusicSrc))
            {
                //this is for the button feedback
                ushort numSrcs = (ushort)_parent.manager.AudioSrcScenarioZ[AudioSrcScenario].IncludedSources.Count;
                ushort buttonNum = 0;
                for (ushort i = 0; i < numSrcs; i++)//loop through all music sources in this scenario
                {
                    ushort srcNum = _parent.manager.AudioSrcScenarioZ[AudioSrcScenario].IncludedSources[i];
                    if (srcNum == this.CurrentMusicSrc)
                    {
                        buttonNum = (ushort)(i + 1);//music button fb
                    }
                }

                var musicSource = _parent.manager.MusicSourceZ[CurrentMusicSrc];
                // Invoke the event with all required properties
                MusicSrcStatusChanged?.Invoke(
                    CurrentMusicSrc,          // Updated Music Source
                    musicSource.FlipsToPageNumber, // FlipsToPageNumber
                    musicSource.EquipID,      // Equipment ID
                    musicSource.Name,          // Music Source Name
                    buttonNum
                );
                //CrestronConsole.PrintLine("MusicSrcStatusChanged: " + musicSource.Name + " is playing. Button number: " + buttonNum);
            }
            else
            {
                MusicSrcStatusChanged?.Invoke(
                    0,           // Music source cleared
                    0,           // No page flip
                    0,           // No equipment ID
                    "Off",           // Empty string for name
                    0
            );
            }
        }
        private void updateRoomStatusText()
        {
            //first clear out old room status text
            RoomStatusText = "";
            if (LightsID > 0)
            {
                RoomStatusText = LightStatusText;
            }
            if (VideoSrcScenario > 0)
            {
                RoomStatusText += _videoStatusText;
            }
            if (AudioID > 0)
            {
                RoomStatusText += _musicStatusText;
            }

        }
        public RoomConfig(ControlSystem parent, ushort number, string name, ushort subSystemScenario, ushort audioSrcScenario, ushort audioSrcSharingScenario, ushort sleepScenario, ushort naxBoxNumber, ushort audioID, ushort lightsID, ushort shadesID, ushort climateID, ushort miscID, ushort openSubsysNumOnRmSelect, string imageURL)
        {
            _parent = parent;
            this.Number = number;
            this.Name = name;
            this.SubSystemScenario = subSystemScenario;
            this.AudioSrcScenario = audioSrcScenario;
            this.AudioSrcSharingScenario = audioSrcSharingScenario;
            this.SleepScenario = sleepScenario;
            this.NAXBoxNumber = naxBoxNumber;
            this.AudioID = audioID;
            this.LightsID = lightsID;
            this.ShadesID = shadesID;
            this.ClimateID = climateID;
            this.MiscID = miscID;
            this.OpenSubsysNumOnRmSelect = openSubsysNumOnRmSelect;
            this.ImageURL = imageURL;
        }
        //private
        private bool musicMuted;
        private ushort musicVolume;
        //public event Action<ushort, ushort> MusicVolumeChanged;
        public event EventHandler MusicVolumeChanged;
        public CTimer RampTimer { get; set; }
        private CTimer _sleepTimer;
        private CTimer _sleepProgressTimer;
        private ushort _sleepTotalMinutes;
        private ushort _sleepElapsedMinutes;
        public ushort SleepTimerProgress { get; private set; } // 0-65535
        public event EventHandler SleepTimerProgressChanged;
        
        VideoDisplays.VideoDisplaysConfig _boundDisplay;
        public event Action<ushort, string> HVACStatusChanged;
        public event Action<ushort, string> DisplayChanged;
        public event Action<ushort, string> LightStatusChanged;
        public event Action<ushort, string> MusicStatusTextChanged;
        public event Action<ushort, string> MusicStatusTextOffChanged;
        public event Action<ushort, ushort, ushort, string, ushort> MusicSrcStatusChanged;
        public event Action<ushort, ushort, string, ushort> VideoSrcStatusChanged;
        public event Action<ushort, string> VideoStatusTextChanged;
        public event Action<ushort, string> VideoStatusTextOffChanged;
        public event Action<ushort, string> RoomStatusTextChanged;

        private readonly ControlSystem _parent;
        private ushort _currentMusicSrc;
        private string _roomStatusText;
        private ushort _currentDisplayNumber;
        private string _videoStatusText;
        private string _videoStatusTextOff;
        // HVAC Properties
        private ushort _currentTemperature;
        private ushort _currentHeatSetpoint;
        private ushort _currentCoolSetpoint;
        private ushort _currentAutoSingleSetpoint;
        private string _climateMode;
        private string _hvacStatusText;
        private string _lightStatusText;
        private string _musicStatusText;
        private string _musicStatusTextOff;
        private bool _lightsAreOff;

        //defined from json
        public ushort Number { get; set; }
        public string Name { get; set; }
        public ushort SubSystemScenario { get; set; }
        public ushort VideoSrcScenario { get; set; }
        public ushort AudioSrcScenario { get; set; }
        public ushort AudioSrcSharingScenario { get; set; }
        public ushort ConfigurationScenario { get; set; }
        public ushort LiftScenario { get; set; }
        public ushort SleepScenario { get; set; }
        public ushort FormatScenario { get; set; }

        public ushort NAXBoxNumber { get; set; }
        public ushort HVACScenario { get; set; }
        public ushort AudioID { get; set; }
        public ushort VideoOutputNum { get; set; }

        public List<ushort> ListOfDisplays = new List<ushort>();
        public ushort LightsID { get; set; }
        public ushort ShadesID { get; set; }
        public ushort ClimateID { get; set; }
        public ushort MiscID { get; set; }
        public ushort OpenSubsysNumOnRmSelect { get; set; }
        public ushort TvOutToAudioInputNumber { get; set; }
        public string ImageURL { get; set; }
        //defined by program
        public ushort CurrentVideoSrc { get; set; }
        public ushort NumberOfDisplays { get; set; }
        public ushort CurrentDisplayNumber
        {
            get => _currentDisplayNumber;
            set
            {
                if (_currentDisplayNumber == value) return;
                _currentDisplayNumber = value;
                // look up the new display’s name
                if (_parent.manager.VideoDisplayZ.TryGetValue(value, out var disp))
                {
                    DisplayChanged?.Invoke(this.Number, disp.DisplayName);
                }
            }
        }
        public ushort CurrentMusicSrc
        {
            get => _currentMusicSrc;
            set
            {
                if (_currentMusicSrc != value)
                {
                    _currentMusicSrc = value;
                    // Add null checks to prevent NullReferenceException
                    if (_parent != null && _parent.manager != null)
                    {
                        // When changed directly, update the status text
                        if (value > 0 && _parent.manager.MusicSourceZ != null && _parent.manager.MusicSourceZ.ContainsKey(value))
                        {
                            MusicSrcStatusText = _parent.manager.MusicSourceZ[value].Name + " is playing. ";
                            MusicStatusTextOff = _parent.manager.MusicSourceZ[value].Name + " is playing. ";
                        }
                        else
                        {
                            MusicSrcStatusText = "";
                            MusicStatusTextOff = "Off";
                        }
                        NotifyMusicSourceChanged();//currentMusicSrc
                        updateRoomStatusText();
                    }
                    else
                    {
                        // Just set the value without doing anything else if _parent or manager is null
                        CrestronConsole.PrintLine("Warning: Cannot update music status texts, parent or manager is null");
                    }
                }
            }
        }
        public ushort CurrentSubsystem { get; set; }
        public bool LastSystemVid { get; set; }

        public bool MusicMuted
        {
            get { return musicMuted; }
            set
            {
                if (musicMuted != value)
                {
                    musicMuted = value;
                    OnMusicMutedChanged();
                }
            }
        }
        public bool VideoMuted { get; set; }

        public ushort MusicVolume
        {
            get { return musicVolume; }
            set
            {
                if (musicVolume != value)
                {
                    musicVolume = value;
                    //MusicVolumeChanged?.Invoke(Number, musicVolume);
                    OnMusicVolumeChanged();
                }
            }
        }
        public bool MusicVolRamping { get; set; }
        public bool VideoVolRamping { get; set; }
        public ushort VideoVolume { get; set; }

        public bool LightsAreOff
        {
            get => _lightsAreOff;
            set
            {
                _lightsAreOff = value;
                updateLightStatusText();

            }
        }
        public bool LiftGoWithOff { get; set; }


        public string MusicStatusText { get; set; }
        //public string VideoStatusText { get; set; }



        public ushort ClimateModeNumber { get; set; }
        public bool ClimateAutoModeIsSingleSetpoint { get; set; }
        public event EventHandler MusicMutedChanged;
        //public event EventHandler MusicVolumeChanged;
        protected virtual void OnMusicMutedChanged()
        {
            MusicMutedChanged?.Invoke(this, EventArgs.Empty);
        }
        protected virtual void OnMusicVolumeChanged()
        {
            MusicVolumeChanged?.Invoke(this, EventArgs.Empty);
        }
        protected virtual void OnSleepTimerProgressChanged()
        {
            SleepTimerProgressChanged?.Invoke(this, EventArgs.Empty);
        }
        public void StartSleepTimer(ushort minutes, ControlSystem _parent, ushort tpNumber)
        {
            // Stop and dispose any existing timer
            if (_sleepTimer != null)
            {
                _sleepTimer.Stop();
                _sleepTimer.Dispose();
                _sleepTimer = null;
            }
            if (_sleepProgressTimer != null)
            {
                _sleepProgressTimer.Stop();
                _sleepProgressTimer.Dispose();
                _sleepProgressTimer = null;
            }

            SleepTimerProgress = 0;
            _sleepTotalMinutes = minutes;
            _sleepElapsedMinutes = 0;

            // Only start if minutes > 0
            if (minutes > 0)
            {
                // CTimer expects milliseconds
                _sleepTimer = new CTimer(_ =>
                {
                    // Timer expired: turn off video
                    _parent.SelectVideoSourceFromTP(tpNumber, 0);//from sleep timer
                    for (ushort i = 0; i < 5; i++)
                    {
                        _parent.manager.touchpanelZ[tpNumber].UserInterface.BooleanInput[(ushort)(161 + i)].BoolValue = false;
                    }
                    SleepTimerProgress = 65535; // 100% complete
                    CrestronConsole.PrintLine("Sleep timer expired for TP-{0}, room {1}", tpNumber, this.Name);
                    // Clean up timer
                    if (_sleepProgressTimer != null)
                    {
                        _sleepProgressTimer.Stop();
                        _sleepProgressTimer.Dispose();
                        _sleepProgressTimer = null;
                    }

                    _sleepTimer.Stop();
                    _sleepTimer.Dispose();
                    _sleepTimer = null;
                }, minutes * 60 * 1000);
                // Timer for progress update (every minute)
                _sleepProgressTimer = new CTimer(_ =>
                {
                    _sleepElapsedMinutes++;
                    if (_sleepElapsedMinutes > _sleepTotalMinutes)
                        _sleepElapsedMinutes = _sleepTotalMinutes;

                    SleepTimerProgress = (ushort)((_sleepElapsedMinutes * 65535) / _sleepTotalMinutes);

                    OnSleepTimerProgressChanged();

                    // Stop progress timer if done
                    if (_sleepElapsedMinutes >= _sleepTotalMinutes)
                    {
                        _sleepProgressTimer.Stop();
                        _sleepProgressTimer.Dispose();
                        _sleepProgressTimer = null;
                    }
                }, null, 60000, 60000); // 60,000 ms = 1 minute
            }
            else
            {
                SleepTimerProgress = 0;
            }
        }

        private void updateLightStatusText()
        {
            LightStatusText = LightsAreOff ? "Lights are off. " : "Lights are on. ";

            if (Name.ToUpper() == "GLOBAL" || LightsID == 0)
            {
                LightStatusText = "";
            }
            updateRoomStatusText();
        }
        private void UpdateHVACStatusText()
        {
            const string bold = "<b>";
            const string boldEnd = "</b>";

            // Debug information
            CrestronConsole.PrintLine($"UpdateHVACStatusText called room-{Name}: Mode={ClimateMode}, CurrentTemp={CurrentTemperature}, HeatSetpoint={CurrentHeatSetpoint}, CoolSetpoint={CurrentCoolSetpoint}, AutoSetpoint={CurrentAutoSingleSetpoint}");

            // First check if we have a valid temperature - if so, at least show that
            if (CurrentTemperature > 0)
            {
                // Always show the temperature if we have it
                string tempDisplay = bold + CurrentTemperature.ToString() + "°" + boldEnd;

                // Initialize with just the temperature
                HVACStatusText = tempDisplay;

                // Then add more information if we have setpoints
                if (!string.IsNullOrEmpty(ClimateMode))
                {
                    switch (ClimateMode)
                    {
                        case "Auto":
                            if (ClimateAutoModeIsSingleSetpoint && CurrentAutoSingleSetpoint > 0)
                            {
                                HVACStatusText = tempDisplay + " - Auto Setpoint " + CurrentAutoSingleSetpoint + "°";
                            }
                            else if (CurrentHeatSetpoint > 0 && CurrentCoolSetpoint > 0)
                            {
                                HVACStatusText = tempDisplay + " - Auto Heat " + CurrentHeatSetpoint + "° Cool " + CurrentCoolSetpoint + "°";
                            }
                            else
                            {
                                // Just show temperature and mode
                                HVACStatusText = tempDisplay + " - Auto Mode";
                            }
                            break;

                        case "Heat":
                            if (CurrentHeatSetpoint > 0)
                            {
                                HVACStatusText = tempDisplay + " - Heating to " + CurrentHeatSetpoint + "°";
                            }
                            else
                            {
                                // Just show temperature and mode
                                HVACStatusText = tempDisplay + " - Heating";
                            }
                            break;

                        case "Cool":
                            if (CurrentCoolSetpoint > 0)
                            {
                                HVACStatusText = tempDisplay + " - Cooling to " + CurrentCoolSetpoint + "°";
                            }
                            else
                            {
                                // Just show temperature and mode
                                HVACStatusText = tempDisplay + " - Cooling";
                            }
                            break;

                        default:
                            // Use the climate mode name if available
                            if (!string.IsNullOrEmpty(ClimateMode))
                            {
                                HVACStatusText = tempDisplay;
                            }
                            break;
                    }
                }
            }
            else if (!string.IsNullOrEmpty(ClimateMode))
            {
                // No temperature but we have a mode
                HVACStatusText = bold + ClimateMode + " Mode" + boldEnd;
            }
            else
            {
                // Nothing valid to show
                HVACStatusText = bold + "N/A" + boldEnd;
            }

            CrestronConsole.PrintLine("HVACStatusText updated: " + HVACStatusText);
        }

    }
}