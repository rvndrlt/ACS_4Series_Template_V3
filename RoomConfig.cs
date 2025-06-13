using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Crestron.SimplSharp;

namespace ACS_4Series_Template_V3.Room
{
    public class RoomConfig
    {
        public RoomConfig(ushort number, string name, ushort subSystemScenario, ushort audioSrcScenario, ushort audioSrcSharingScenario, ushort sleepScenario, ushort audioID, ushort lightsID, ushort shadesID, ushort climateID, ushort miscID, ushort openSubsysNumOnRmSelect, string imageURL)
        {
            this.Number = number;
            this.Name = name;
            this.SubSystemScenario = subSystemScenario;
            this.AudioSrcScenario = audioSrcScenario;
            this.AudioSrcSharingScenario = audioSrcSharingScenario;
            this.SleepScenario = sleepScenario;
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
        public ushort CurrentDisplayNumber { get; set; }
        public ushort CurrentMusicSrc { get; set; }
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

        public bool LightsAreOff { get; set; }

        public string LightStatusText { get; set; }
        public string MusicStatusText { get; set; }
        public string VideoStatusText { get; set; }
        public string HVACStatusText { get; set; }
        public ushort CurrentTemperature { get; set; }
        public ushort CurrentHeatSetpoint { get; set; }
        public ushort CurrentCoolSetpoint { get; set; }
        public ushort CurrentAutoSingleSetpoint { get; set; }
        public string ClimateMode { get; set; }

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
    }
}