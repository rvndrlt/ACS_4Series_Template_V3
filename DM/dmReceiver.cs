using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Crestron.SimplSharp;
using Crestron.SimplSharpPro;
using Crestron.SimplSharpPro.DM.Streaming;
using Crestron.SimplSharp.Reflection;
using Crestron.SimplSharp.CrestronIO;
using ACS_4Series_Template_V3.Configuration;

namespace ACS_4Series_Template_V3.DmReceiver
{
    public class DmNVXreceiver
    {
        public DmNvx35x DmNvx35X;
        public GenericBase DmDevice;
        public Assembly DmNVXAssembly;
        public CrestronControlSystem CS;
        private const string LogHeader = "[DMreceiver] ";
        private string _loadedDriverPath = null;
        private readonly HashSet<string> _missingDriversLogged = new HashSet<string>();
        private readonly HashSet<string> _missingVolumeCommandsLogged = new HashSet<string>();
        private string _heldCommand = null;
        private string _heldCommandValue = null;
        private CTimer _holdTimer = null;
        private bool _ramping = false;
        private bool _tapAsserted = false;
        private CTimer _tapReleaseTimer = null;
        private DateTime _tapPressedAt = DateTime.MinValue;

        /// <summary>
        /// Live override for the tap length, set by the "irtap" console command so the value can be
        /// swept against a real TV without a reloadjson between each try. 0 = use the config value.
        /// Static: one sweep applies to every receiver at once.
        /// </summary>
        public static ushort TapMsOverride = 0;

        // Fallbacks when the config leaves the volume timings at 0. See DisplayControlItem for the
        // frame-timing reasoning behind these numbers.
        private const ushort DefaultVolumeTapMs = 80;
        private const uint DefaultVolumeHoldMs = 400;
        private const uint DefaultVolumeMaxHoldMs = 10000;
        public DmNVXreceiver(uint dmOutputNumber, string name, uint ipid, string type, string multiCastAddress, CrestronControlSystem cs)
        {
            this.DmOutputNumber = dmOutputNumber;
            this.MultiCastAddress = multiCastAddress;
            this.Type = type;
            this.Ipid = ipid;
            this.Name = name;
            this.CS = cs;
        }
        public string Type { get; set; }
        public uint Ipid { get; set; }
        public uint DmOutputNumber { get; set; }
        public string MultiCastAddress { get; set; }
        public string Name { get; set; }
        public ConfigData.DisplayControlItem DisplayControl { get; set; }

        /// <summary>
        /// Sets up display control (IR or serial) after the NVX has been registered.
        /// Call this after Register() returns true.
        /// </summary>
        public void SetupDisplayControl()
        {
            if (DisplayControl == null || DmDevice == null) return;

            try
            {
                if (DisplayControl.Method.Equals("ir", StringComparison.OrdinalIgnoreCase))
                {
                    // Load through EnsureDriverLoaded so _loadedDriverPath is tracked from the
                    // start; swapping to a volumeDriver later depends on knowing what is loaded.
                    EnsureDriverLoaded(GetIROutputPort(DisplayControl.Port), DisplayControl.Driver);
                }
                else if (DisplayControl.Method.Equals("serial", StringComparison.OrdinalIgnoreCase))
                {
                    var comPort = GetComPort(DisplayControl.Port);
                    if (comPort != null)
                    {
                        var spec = DisplayControl.Spec ?? new ConfigData.DisplayControlSerialSpec();
                        comPort.SetComPortSpec(
                            ParseBaudRate(spec.BaudRate),
                            ParseDataBits(spec.DataBits),
                            ParseParity(spec.Parity),
                            ParseStopBits(spec.StopBits),
                            ComPort.eComProtocolType.ComspecProtocolRS232,
                            ParseHardwareHandshake(spec.HardwareHandshake),
                            ParseSoftwareHandshake(spec.SoftwareHandshake),
                            false);
                        CrestronConsole.PrintLine(LogHeader + "Configured COM port {0} for {1} ({2} baud)",
                            DisplayControl.Port, Name, spec.BaudRate);
                    }
                }
            }
            catch (Exception e)
            {
                ErrorLog.Error(LogHeader + "Error setting up display control for {0}: {1}", Name, e.Message);
            }
        }

        /// <summary>
        /// Sends a display control command by standard key name (e.g. "powerOn", "powerOff", "inputHdmi1").
        /// </summary>
        public void SendDisplayCommand(string commandKey)
        {
            if (DisplayControl == null || DisplayControl.Commands == null || DmDevice == null) return;

            if (!DisplayControl.Commands.TryGetValue(commandKey, out string commandValue))
            {
                CrestronConsole.PrintLine(LogHeader + "Command '{0}' not found for {1}", commandKey, Name);
                return;
            }

            try
            {
                if (DisplayControl.Method.Equals("ir", StringComparison.OrdinalIgnoreCase))
                {
                    var irPort = GetIROutputPort(DisplayControl.Port);
                    if (irPort != null)
                    {
                        // Power/input always come from the main driver, even if a volume-only
                        // driver is configured and currently loaded.
                        if (!EnsureDriverLoaded(irPort, DisplayControl.Driver)) return;
                        // Power and input selects are discretes, so the two frames 200 ms sends are
                        // harmless here (see the note in SendVolumeCommand).
                        irPort.PressAndRelease(commandValue, 200);
                    }
                }
                else if (DisplayControl.Method.Equals("serial", StringComparison.OrdinalIgnoreCase))
                {
                    var comPort = GetComPort(DisplayControl.Port);
                    if (comPort != null)
                        comPort.Send(commandValue);
                }
            }
            catch (Exception e)
            {
                ErrorLog.Error(LogHeader + "Error sending command '{0}' to {1}: {2}", commandKey, Name, e.Message);
            }
        }

        /// <summary>
        /// Sends a volume command (volumeUp, volumeDown, mute).
        /// If a separate volumeDriver is defined, loads it before sending; otherwise uses the main driver's volumeCommands or commands.
        /// </summary>
        public void SendVolumeCommand(string commandKey)
        {
            if (DisplayControl == null || DmDevice == null) return;
            if (string.IsNullOrEmpty(DisplayControl.Method)) return;

            string commandValue;
            if (!TryGetVolumeCommand(commandKey, out commandValue)) return;

            try
            {
                if (DisplayControl.Method.Equals("ir", StringComparison.OrdinalIgnoreCase))
                {
                    var irPort = GetIROutputPort(DisplayControl.Port);
                    if (irPort == null)
                    {
                        CrestronConsole.PrintLine(LogHeader + "{0}: no IR port {1} for '{2}'", Name, DisplayControl.Port, commandKey);
                        return;
                    }

                    if (!EnsureDriverLoaded(irPort, VolumeDriverPath)) return;
                    CrestronConsole.PrintLine(LogHeader + "{0}: IR PULSE '{1}' -> \"{2}\" (port {3}, driver '{4}')",
                        Name, commandKey, commandValue, DisplayControl.Port, CurrentIRDriver);
                    // NOTE: 200 ms spans TWO complete Samsung frames (~107 ms each, second one
                    // finishing at ~166 ms), so this pulses mute twice. Left alone deliberately -
                    // mute has never misbehaved in the field, so the TV is evidently de-bouncing the
                    // repeat. If mute ever starts looking like a no-op, this is the first suspect:
                    // drop it to DisplayControl.VolumeTapMs. Volume ramps do NOT de-bounce, which is
                    // exactly why they needed the tap/hold split above.
                    irPort.PressAndRelease(commandValue, 200);
                }
                else if (DisplayControl.Method.Equals("serial", StringComparison.OrdinalIgnoreCase))
                {
                    var comPort = GetComPort(DisplayControl.Port);
                    if (comPort != null)
                    {
                        CrestronConsole.PrintLine(LogHeader + "{0}: SERIAL '{1}' -> \"{2}\" (port {3})",
                            Name, commandKey, commandValue, DisplayControl.Port);
                        comPort.Send(commandValue);
                    }
                }
            }
            catch (Exception e)
            {
                ErrorLog.Error(LogHeader + "Error sending volume command '{0}' to {1}: {2}", commandKey, Name, e.Message);
            }
        }

        /// <summary>
        /// Press edge of a volume ramp button.
        ///
        /// A tap and a hold are the same button, told apart here rather than on the panel:
        ///   - the tap fires immediately as a fixed-length PressAndRelease, so it is exactly one IR
        ///     frame = one volume step no matter how long the finger lingers or how much latency the
        ///     CIP round trip adds. This is the whole point: the old code called Press() on this edge
        ///     and Release() on the release edge, so the IR ran for the full duration of the press
        ///     plus network jitter, which on a Samsung (~107 ms per frame) is 4-6 steps per tap.
        ///   - if the button is still down VolumeHoldMs later, it becomes a continuous ramp via the
        ///     IR port's native Press(), which auto-repeats until Release().
        ///
        /// Ramp commands only - a discrete such as mute must go through SendVolumeCommand, because
        /// nothing releases a Press that has no matching release edge.
        /// </summary>
        public void StartVolumeCommand(string commandKey)
        {
            if (DisplayControl == null || DmDevice == null) return;
            if (string.IsNullOrEmpty(DisplayControl.Method)) return;

            string commandValue;
            if (!TryGetVolumeCommand(commandKey, out commandValue)) return;

            try
            {
                if (DisplayControl.Method.Equals("ir", StringComparison.OrdinalIgnoreCase))
                {
                    var irPort = GetIROutputPort(DisplayControl.Port);
                    if (irPort == null)
                    {
                        CrestronConsole.PrintLine(LogHeader + "{0}: no IR port {1} for '{2}'", Name, DisplayControl.Port, commandKey);
                        return;
                    }

                    // A second press edge without an intervening release must not stack timers or
                    // leave an earlier press asserted.
                    StopVolumeCommand();

                    if (!EnsureDriverLoaded(irPort, VolumeDriverPath)) return;

                    ushort tapMs = TapMsOverride > 0 ? TapMsOverride : DisplayControl.VolumeTapMs;
                    if (tapMs == 0) tapMs = DefaultVolumeTapMs;
                    uint holdMs = DisplayControl.VolumeHoldMs;
                    if (holdMs == 0) holdMs = DefaultVolumeHoldMs;

                    _heldCommand = commandKey;
                    _heldCommandValue = commandValue;

                    CrestronConsole.PrintLine(LogHeader + "{0}: IR TAP '{1}' -> \"{2}\" ({3}ms, port {4}, driver '{5}')",
                        Name, commandKey, commandValue, tapMs, DisplayControl.Port, CurrentIRDriver);

                    // Deliberately NOT irPort.PressAndRelease(cmd, tapMs). Disassembling
                    // Crestron.SimplSharpPro shows that overload is literally
                    //     Press(id, cmd); Thread.Sleep(TimeOutInMS); Release();
                    // run on the CALLING thread, inside the port's critical section. On a volume
                    // button that blocks a sig-change handler thread for the whole tap and holds the
                    // IR port lock while doing it - both add jitter to exactly the interval we are
                    // trying to control. Press + CTimer(Release) sends the same two CIP messages to
                    // the NVX without blocking anything.
                    irPort.Press(commandValue);
                    _tapAsserted = true;
                    _tapPressedAt = DateTime.Now;
                    _tapReleaseTimer = new CTimer(ReleaseTap, tapMs);

                    // Still down holdMs later? Re-press and leave it asserted: that is the ramp.
                    _holdTimer = new CTimer(BeginVolumeRamp, holdMs);
                }
                else if (DisplayControl.Method.Equals("serial", StringComparison.OrdinalIgnoreCase))
                {
                    // Serial doesn't support press/hold natively - send once
                    var comPort = GetComPort(DisplayControl.Port);
                    if (comPort != null)
                        comPort.Send(commandValue);
                }
            }
            catch (Exception e)
            {
                ErrorLog.Error(LogHeader + "Error starting volume command '{0}' on {1}: {2}", commandKey, Name, e.Message);
            }
        }

        /// <summary>
        /// Ends the one-shot tap, tapMs after the press edge. Only the tap is released here - if the
        /// button was held long enough to escalate into a ramp, that ramp owns the port and is
        /// released by StopVolumeCommand instead.
        /// </summary>
        private void ReleaseTap(object unused)
        {
            try
            {
                if (!_tapAsserted || _ramping) return;
                var irPort = GetIROutputPort(DisplayControl.Port);
                if (irPort != null) irPort.Release();
                _tapAsserted = false;
                // Logged so the ACTUAL press->release interval is visible, not the one we asked for.
                // If this reads ~40ms but the TV still moves several steps, the interval is not what
                // is driving the step count and the NVX is transmitting a minimum burst of its own.
                CrestronConsole.PrintLine(LogHeader + "{0}: IR tap released after {1:F0}ms (asked {2}ms)",
                    Name, (DateTime.Now - _tapPressedAt).TotalMilliseconds,
                    TapMsOverride > 0 ? TapMsOverride : (DisplayControl.VolumeTapMs == 0 ? DefaultVolumeTapMs : DisplayControl.VolumeTapMs));
            }
            catch (Exception e)
            {
                ErrorLog.Error(LogHeader + "Error releasing volume tap on {0}: {1}", Name, e.Message);
            }
        }

        /// <summary>
        /// Fires VolumeHoldMs after the press edge when the button is still down: turns the one-shot
        /// tap into a continuous ramp. Press() keeps regenerating the IR frame until Release().
        /// </summary>
        private void BeginVolumeRamp(object unused)
        {
            try
            {
                if (_heldCommand == null || _ramping) return;

                var irPort = GetIROutputPort(DisplayControl.Port);
                if (irPort == null) return;

                CrestronConsole.PrintLine(LogHeader + "{0}: IR PRESS (hold) '{1}' -> \"{2}\" (port {3}, driver '{4}')",
                    Name, _heldCommand, _heldCommandValue, DisplayControl.Port, CurrentIRDriver);
                irPort.Press(_heldCommandValue);
                _ramping = true;
                _tapAsserted = false;

                // If the release edge is ever lost the ramp would run forever, so bound it.
                uint maxHoldMs = DisplayControl.VolumeMaxHoldMs;
                if (maxHoldMs == 0) maxHoldMs = DefaultVolumeMaxHoldMs;
                DisposeHoldTimer();
                _holdTimer = new CTimer(ForceReleaseStuckRamp, maxHoldMs);
            }
            catch (Exception e)
            {
                ErrorLog.Error(LogHeader + "Error starting volume ramp on {0}: {1}", Name, e.Message);
            }
        }

        /// <summary>
        /// Watchdog for a ramp whose release edge never arrived (panel dropped mid-press).
        /// </summary>
        private void ForceReleaseStuckRamp(object unused)
        {
            if (!_ramping) return;
            CrestronConsole.PrintLine(LogHeader + "{0}: IR ramp '{1}' exceeded max hold - forcing release", Name, _heldCommand ?? "none");
            StopVolumeCommand();
        }

        /// <summary>
        /// Release edge of a volume ramp button. Cancels both pending timers and releases the IR port
        /// if anything is still asserted - the tap if the finger came up inside tapMs, the ramp if it
        /// had escalated.
        /// </summary>
        public void StopVolumeCommand()
        {
            if (DisplayControl == null || DmDevice == null) return;
            if (string.IsNullOrEmpty(DisplayControl.Method)) return;

            try
            {
                DisposeTapTimer();
                DisposeHoldTimer();

                if (DisplayControl.Method.Equals("ir", StringComparison.OrdinalIgnoreCase) && (_ramping || _tapAsserted))
                {
                    var irPort = GetIROutputPort(DisplayControl.Port);
                    if (irPort != null)
                    {
                        CrestronConsole.PrintLine(LogHeader + "{0}: IR RELEASE ({1} '{2}', port {3})",
                            Name, _ramping ? "was ramping" : "tap still asserted", _heldCommand ?? "none", DisplayControl.Port);
                        irPort.Release();
                    }
                }

                _ramping = false;
                _tapAsserted = false;
                _heldCommand = null;
                _heldCommandValue = null;
            }
            catch (Exception e)
            {
                ErrorLog.Error(LogHeader + "Error stopping volume command on {0}: {1}", Name, e.Message);
            }
        }

        private void DisposeHoldTimer()
        {
            if (_holdTimer == null) return;
            _holdTimer.Stop();
            _holdTimer.Dispose();
            _holdTimer = null;
        }

        private void DisposeTapTimer()
        {
            if (_tapReleaseTimer == null) return;
            _tapReleaseTimer.Stop();
            _tapReleaseTimer.Dispose();
            _tapReleaseTimer = null;
        }

        /// <summary>
        /// Makes <paramref name="driverPath"/> the driver the port will actually use, unloading
        /// whatever was there first.
        ///
        /// This exists because IROutputPort.Press(string) / PressAndRelease(string, ushort) do NOT
        /// use the most recently loaded driver - disassembly shows both resolve the driver id with
        /// Keys.First() over the port's driver dictionary, i.e. the FIRST one still loaded. The old
        /// code just called LoadIRDriver() again to "switch", which left both loaded and kept
        /// sending every command through the original file. Anything using volumeDriver was
        /// silently transmitting from the main driver.
        /// </summary>
        private bool EnsureDriverLoaded(IROutputPort irPort, string driverPath)
        {
            if (irPort == null || string.IsNullOrEmpty(driverPath)) return false;

            if (string.Equals(_loadedDriverPath, driverPath, StringComparison.OrdinalIgnoreCase))
                return true;

            if (!File.Exists(driverPath))
            {
                if (!_missingDriversLogged.Contains(driverPath))
                {
                    _missingDriversLogged.Add(driverPath);
                    ErrorLog.Error(LogHeader + "IR driver file not found: '{0}' for {1}", driverPath, Name);
                    CrestronConsole.PrintLine(LogHeader + "IR driver file not found: '{0}' for {1}", driverPath, Name);
                }
                return false;
            }

            try
            {
                if (_loadedDriverPath != null) irPort.UnloadAllIRDrivers();
                irPort.LoadIRDriver(driverPath);
                _loadedDriverPath = driverPath;
                CrestronConsole.PrintLine(LogHeader + "{0}: IR driver now '{1}' (port {2})",
                    Name, driverPath, DisplayControl.Port);
                return true;
            }
            catch (Exception e)
            {
                ErrorLog.Error(LogHeader + "Error loading IR driver '{0}' on {1}: {2}", driverPath, Name, e.Message);
                return false;
            }
        }

        /// <summary>
        /// The driver a volume command should use: the dedicated volumeDriver when one is configured,
        /// otherwise the main driver. A volume-only copy of the file is the supported way to give
        /// volume different IR repeat settings from power/input without making those less reliable.
        /// </summary>
        private string VolumeDriverPath
        {
            get
            {
                return string.IsNullOrEmpty(DisplayControl.VolumeDriver)
                    ? DisplayControl.Driver
                    : DisplayControl.VolumeDriver;
            }
        }

        /// <summary>
        /// The IR driver file currently loaded on the port, for logging.
        /// </summary>
        private string CurrentIRDriver
        {
            get { return _loadedDriverPath ?? "(none)"; }
        }

        /// <summary>
        /// Looks up a volume command - volumeCommands first, then the main commands.
        /// Returns false when the key is missing or blank, logging that once per key so a display that is
        /// only partially configured for volume does not flood the console on every button press.
        /// </summary>
        private bool TryGetVolumeCommand(string commandKey, out string commandValue)
        {
            commandValue = null;
            if (DisplayControl.VolumeCommands != null && DisplayControl.VolumeCommands.TryGetValue(commandKey, out commandValue))
            {
                // found in volumeCommands
            }
            else if (DisplayControl.Commands != null && DisplayControl.Commands.TryGetValue(commandKey, out commandValue))
            {
                // found in main commands
            }

            if (string.IsNullOrEmpty(commandValue))
            {
                if (!_missingVolumeCommandsLogged.Contains(commandKey))
                {
                    _missingVolumeCommandsLogged.Add(commandKey);
                    CrestronConsole.PrintLine(LogHeader + "Volume command '{0}' not found for {1}", commandKey, Name);
                }
                return false;
            }
            return true;
        }

        /// <summary>
        /// Returns true if this receiver has volume control capability (either volumeCommands defined or volume keys in main commands).
        /// </summary>
        public bool HasVolumeControl
        {
            get
            {
                if (DisplayControl == null || string.IsNullOrEmpty(DisplayControl.Method)) return false;
                if (DisplayControl.VolumeCommands != null && DisplayControl.VolumeCommands.Count > 0) return true;
                if (DisplayControl.Commands != null && DisplayControl.Commands.ContainsKey("volumeUp")) return true;
                return false;
            }
        }

        private IROutputPort GetIROutputPort(uint port)
        {
            if (DmNvx35X != null)
                return DmNvx35X.IROutputPorts[port];

            // Use reflection for non-35x devices
            try
            {
                var irPortsProp = DmDevice.GetType().GetCType().GetProperty("IROutputPorts");
                if (irPortsProp != null)
                {
                    var irPorts = irPortsProp.GetValue(DmDevice, null);
                    if (irPorts != null)
                    {
                        var indexer = irPorts.GetType().GetCType().GetProperty("Item");
                        if (indexer != null)
                            return indexer.GetValue(irPorts, new object[] { port }) as IROutputPort;
                    }
                }
            }
            catch (Exception e)
            {
                CrestronConsole.PrintLine(LogHeader + "Could not get IR port via reflection: {0}", e.Message);
            }
            return null;
        }

        private ComPort GetComPort(uint port)
        {
            if (DmNvx35X != null)
                return DmNvx35X.ComPorts[port];

            // Use reflection for non-35x devices
            try
            {
                var comPortsProp = DmDevice.GetType().GetCType().GetProperty("ComPorts");
                if (comPortsProp != null)
                {
                    var comPorts = comPortsProp.GetValue(DmDevice, null);
                    if (comPorts != null)
                    {
                        var indexer = comPorts.GetType().GetCType().GetProperty("Item");
                        if (indexer != null)
                            return indexer.GetValue(comPorts, new object[] { port }) as ComPort;
                    }
                }
            }
            catch (Exception e)
            {
                CrestronConsole.PrintLine(LogHeader + "Could not get COM port via reflection: {0}", e.Message);
            }
            return null;
        }

        #region Serial Spec Parsing Helpers

        private static ComPort.eComBaudRates ParseBaudRate(int baud)
        {
            switch (baud)
            {
                case 2400: return ComPort.eComBaudRates.ComspecBaudRate2400;
                case 4800: return ComPort.eComBaudRates.ComspecBaudRate4800;
                case 9600: return ComPort.eComBaudRates.ComspecBaudRate9600;
                case 19200: return ComPort.eComBaudRates.ComspecBaudRate19200;
                case 38400: return ComPort.eComBaudRates.ComspecBaudRate38400;
                case 57600: return ComPort.eComBaudRates.ComspecBaudRate57600;
                case 115200: return ComPort.eComBaudRates.ComspecBaudRate115200;
                default: return ComPort.eComBaudRates.ComspecBaudRate9600;
            }
        }

        private static ComPort.eComDataBits ParseDataBits(int bits)
        {
            switch (bits)
            {
                case 7: return ComPort.eComDataBits.ComspecDataBits7;
                case 8: return ComPort.eComDataBits.ComspecDataBits8;
                default: return ComPort.eComDataBits.ComspecDataBits8;
            }
        }

        private static ComPort.eComParityType ParseParity(string parity)
        {
            switch ((parity ?? "none").ToLower())
            {
                case "odd": return ComPort.eComParityType.ComspecParityOdd;
                case "even": return ComPort.eComParityType.ComspecParityEven;
                default: return ComPort.eComParityType.ComspecParityNone;
            }
        }

        private static ComPort.eComStopBits ParseStopBits(int bits)
        {
            switch (bits)
            {
                case 2: return ComPort.eComStopBits.ComspecStopBits2;
                default: return ComPort.eComStopBits.ComspecStopBits1;
            }
        }

        private static ComPort.eComHardwareHandshakeType ParseHardwareHandshake(string hs)
        {
            switch ((hs ?? "none").ToLower())
            {
                case "cts": return ComPort.eComHardwareHandshakeType.ComspecHardwareHandshakeCTS;
                case "rts": return ComPort.eComHardwareHandshakeType.ComspecHardwareHandshakeRTS;
                case "rtscts": return ComPort.eComHardwareHandshakeType.ComspecHardwareHandshakeRTSCTS;
                default: return ComPort.eComHardwareHandshakeType.ComspecHardwareHandshakeNone;
            }
        }

        private static ComPort.eComSoftwareHandshakeType ParseSoftwareHandshake(string hs)
        {
            switch ((hs ?? "none").ToLower())
            {
                case "xon": return ComPort.eComSoftwareHandshakeType.ComspecSoftwareHandshakeXON;
                case "xont": return ComPort.eComSoftwareHandshakeType.ComspecSoftwareHandshakeXONT;
                case "xonr": return ComPort.eComSoftwareHandshakeType.ComspecSoftwareHandshakeXONR;
                default: return ComPort.eComSoftwareHandshakeType.ComspecSoftwareHandshakeNone;
            }
        }

        #endregion

        /// <summary>
        /// Sets the multicast stream address the receiver subscribes to.
        /// Pass "0.0.0.0" or empty string to stop receiving.
        /// </summary>
        public void SetStreamLocation(string multicastAddress)
        {
            if (DmNvx35X != null)
            {
                try
                {
                    DmNvx35X.Control.ServerUrl.StringValue = multicastAddress;
                    CrestronConsole.PrintLine(LogHeader + "{0} stream set to {1}", Name, multicastAddress);
                }
                catch (Exception e)
                {
                    ErrorLog.Error(LogHeader + "Error setting stream on {0}: {1}", Name, e.Message);
                }
            }
            else if (DmDevice != null)
            {
                try
                {
                    var deviceType = DmDevice.GetType().GetCType();
                    var controlProp = deviceType.GetProperties().FirstOrDefault(p => p.Name == "Control");
                    if (controlProp != null)
                    {
                        var control = controlProp.GetValue(DmDevice, null);
                        if (control != null)
                        {
                            var controlType = control.GetType().GetCType();
                            var serverUrlProp = controlType.GetProperties().FirstOrDefault(p => p.Name == "ServerUrl");
                            if (serverUrlProp != null)
                            {
                                var serverUrl = serverUrlProp.GetValue(control, null);
                                if (serverUrl != null)
                                {
                                    var stringValueProp = serverUrl.GetType().GetCType().GetProperties().FirstOrDefault(p => p.Name == "StringValue");
                                    if (stringValueProp != null)
                                    {
                                        stringValueProp.SetValue(serverUrl, multicastAddress, null);
                                        CrestronConsole.PrintLine(LogHeader + "{0} stream set to {1}", Name, multicastAddress);
                                    }
                                }
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    ErrorLog.Error(LogHeader + "Error setting stream via reflection on {0}: {1}", Name, e.Message);
                }
            }
        }

        public bool Register() {
            try { 
                this.DmDevice = this.CreateDevice(this.Type, this.Ipid);
                if (this.DmDevice == null || this.Ipid == 0)
                {
                    return false;
                }

                // Try to get as DmNvx35x for full feature access
                this.DmNvx35X = this.DmDevice as DmNvx35x;

                // Check if already registered (some device constructors auto-register)
                if (this.DmDevice.Registered)
                {
                    CrestronConsole.PrintLine(LogHeader + "{0} (IPID {1}) already registered after construction", this.Name, this.Ipid);
                    this.DmDevice.Description = this.Name;
                    this.DmDevice.BaseEvent += DmNvx35XEventHandler;
                    SubscribeStreamEventsViaReflection();
                    return true;
                }

                this.DmDevice.Description = this.Name;
                this.DmDevice.BaseEvent += DmNvx35XEventHandler;

                if (this.DmNvx35X != null)
                {
                    // Full DmNvx35x device - direct property access
                    this.DmNvx35X.Control.DeviceMode = eDeviceMode.Receiver;
                    if (this.DmNvx35X.HdmiOut != null)
                        this.DmNvx35X.HdmiOut.StreamChange += DmNvx35X_StreamChangeEventHandler;
                    if (this.DmNvx35X.SourceReceive != null)
                        this.DmNvx35X.SourceReceive.StreamChange += DmNvx35X_StreamChangeEventHandler;
                    if (this.DmNvx35X.SourceTransmit != null)
                        this.DmNvx35X.SourceTransmit.StreamChange += DmNvx35X_StreamChangeEventHandler;
                }
                else
                {
                    // Non-35x device (e.g. DmNvxD30) - dedicated decoder, no need to set DeviceMode
                    SubscribeStreamEventsViaReflection();
                }

                var regResult = this.DmDevice.Register();
                if (regResult != Crestron.SimplSharpPro.eDeviceRegistrationUnRegistrationResponse.Success)
                {
                    CrestronConsole.PrintLine(LogHeader + "Registration failed for {0} (IPID 0x{1:X2}): {2}", 
                        this.Name, this.Ipid, this.DmDevice.RegistrationFailureReason);
                    CrestronConsole.PrintLine(LogHeader + "  Device type: {0}, Registered: {1}", 
                        this.DmDevice.GetType().Name, this.DmDevice.Registered);
                    ErrorLog.Error(LogHeader + "Error registering receiver {0}: {1}", this.Name, this.DmDevice.RegistrationFailureReason);
                    return false;
                }
                else
                {
                    CrestronConsole.PrintLine(LogHeader + "Successfully registered {0} (IPID 0x{1:X2})", this.Name, this.Ipid);
                    return true;
                }

            }
            catch (Exception e)
            {
                ErrorLog.Error(LogHeader + "Exception when trying to register DM {0}: {1}", this.Name, e.Message);
                CrestronConsole.PrintLine(LogHeader + "Exception when trying to register DM {0}: {1}", this.Name, e.Message);
                return false;
            }
        }

        private void SetDeviceModeViaReflection(eDeviceMode mode)
        {
            try
            {
                var deviceType = this.DmDevice.GetType().GetCType();
                // Use GetProperties() to avoid AmbiguousMatchException when multiple "Control" exist in hierarchy
                var controlProp = deviceType.GetProperties().FirstOrDefault(p => p.Name == "Control");
                if (controlProp != null)
                {
                    var control = controlProp.GetValue(this.DmDevice, null);
                    if (control != null)
                    {
                        var controlType = control.GetType().GetCType();
                        var deviceModeProp = controlType.GetProperties().FirstOrDefault(p => p.Name == "DeviceMode");
                        if (deviceModeProp != null)
                        {
                            deviceModeProp.SetValue(control, mode, null);
                            CrestronConsole.PrintLine(LogHeader + "Set DeviceMode to {0} for {1}", mode, this.Name);
                        }
                        else
                        {
                            CrestronConsole.PrintLine(LogHeader + "No DeviceMode property found for {0}, skipping", this.Name);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                CrestronConsole.PrintLine(LogHeader + "Could not set DeviceMode via reflection for {0}: {1}", this.Name, e.Message);
            }
        }

        private void SubscribeStreamEventsViaReflection()
        {
            try
            {
                var deviceType = this.DmDevice.GetType().GetCType();

                // Try HdmiOut
                var hdmiOutProp = deviceType.GetProperties().FirstOrDefault(p => p.Name == "HdmiOut");
                if (hdmiOutProp != null)
                {
                    var hdmiOut = hdmiOutProp.GetValue(this.DmDevice, null) as Crestron.SimplSharpPro.DeviceSupport.Stream;
                    if (hdmiOut != null)
                        hdmiOut.StreamChange += DmNvx35X_StreamChangeEventHandler;
                }

                // Try SourceReceive
                var srcRecvProp = deviceType.GetProperties().FirstOrDefault(p => p.Name == "SourceReceive");
                if (srcRecvProp != null)
                {
                    var srcRecv = srcRecvProp.GetValue(this.DmDevice, null) as Crestron.SimplSharpPro.DeviceSupport.Stream;
                    if (srcRecv != null)
                        srcRecv.StreamChange += DmNvx35X_StreamChangeEventHandler;
                }
            }
            catch (Exception e)
            {
                CrestronConsole.PrintLine(LogHeader + "Could not subscribe to stream events via reflection for {0}: {1}", this.Name, e.Message);
            }
        }


        public GenericBase CreateDevice(string DMBoxType, uint deviceId)
        {
            try { 
                this.DmNVXAssembly = Assembly.LoadFrom(Path.Combine(Directory.GetApplicationDirectory(), "Crestron.SimplSharpPro.DM.dll"));

                // add the correct device type that we want to reflect into
                string assembly = string.Format("Crestron.SimplSharpPro.DM.Streaming.{0}", DMBoxType);

                CType cswitcher = this.DmNVXAssembly.GetType(assembly);

                // get the correct constructor for this type
                CType[] constructorTypes = new CType[] { typeof(uint), typeof(CrestronControlSystem) };

                // get info for the previously found constructor
                ConstructorInfo cinfo = cswitcher.GetConstructor(constructorTypes);

                if (cinfo == null)
                {
                    CrestronConsole.PrintLine(LogHeader + "No matching constructor found for {0}", DMBoxType);
                    return null;
                }

                // create the object with all the information
                CrestronConsole.PrintLine("retrieved {0} {1}", DMBoxType, deviceId);
                return (GenericBase)cinfo.Invoke(new object[] { deviceId, this.CS});
            }
            catch (Exception e)
            {
                CrestronConsole.PrintLine(LogHeader + "Unable to create DM device {0}: {1}", DMBoxType, e.Message);
                ErrorLog.Error(LogHeader + "Unable to create DM device {0}: {1}\nInner: {2}", DMBoxType, e.Message, 
                    e.InnerException != null ? e.InnerException.Message : "none");
                return null;
            }
        }
        // Method to handle top level sig change events for DM-NVX-351 Device.
        static void DmNvx35XEventHandler(GenericBase device, BaseEventArgs args)
        {
            if (args.EventId == Crestron.SimplSharpPro.DM.DMInputEventIds.StartEventId)
            {
                string name = device.Description;
                //Figure this out!!!!!
                //pull from the dictionary
                //CrestronConsole.PrintLine("Start event occurred on DM-NVX-351, StartFeedback value is {0}", device.Control.StartFeedback.BoolValue);
            }
        }
        //Event handler for Stream sig changes
        static void DmNvx35X_StreamChangeEventHandler(Crestron.SimplSharpPro.DeviceSupport.Stream stream, Crestron.SimplSharpPro.DeviceSupport.StreamEventArgs args)
        {
            //Stream Transmit
            if (args.EventId == Crestron.SimplSharpPro.DM.DMOutputEventIds.StatisticsDisableEventId)
            {
                
                CrestronConsole.PrintLine("{0} device Statistics Disabled Event Occurred\r\n", stream.ToString());
            }

            //HDMI In
            if (args.EventId == Crestron.SimplSharpPro.DM.DMInputEventIds.SourceSyncEventId)
            {
                CrestronConsole.PrintLine("{0} device Sync Detected Feedback Event Occurred\r\n", stream.ToString());
            }

            //HDMI Out
            else if (args.EventId == Crestron.SimplSharpPro.DM.DMOutputEventIds.HotplugDetectedEventId)
            {
                CrestronConsole.PrintLine("{0} device Hot Plug Detected Feedback Event Occurred\r\n", stream.ToString());
            }
        }

    }

}
