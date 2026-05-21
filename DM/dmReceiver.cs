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
        private bool _volumeDriverLoaded = false;
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
                    if (!File.Exists(DisplayControl.Driver))
                    {
                        var errMsg = string.Format(LogHeader + "IR driver file not found: '{0}' for {1}", DisplayControl.Driver, Name);
                        ErrorLog.Error(errMsg);
                        CrestronConsole.PrintLine(errMsg);
                        return;
                    }
                    var irPort = GetIROutputPort(DisplayControl.Port);
                    if (irPort != null)
                    {
                        irPort.LoadIRDriver(DisplayControl.Driver);
                        CrestronConsole.PrintLine(LogHeader + "Loaded IR driver '{0}' on port {1} for {2}",
                            DisplayControl.Driver, DisplayControl.Port, Name);
                    }
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
                        // If a different volume driver was loaded, reload the main driver first
                        if (!string.IsNullOrEmpty(DisplayControl.VolumeDriver) 
                            && !DisplayControl.VolumeDriver.Equals(DisplayControl.Driver, StringComparison.OrdinalIgnoreCase)
                            && _volumeDriverLoaded)
                        {
                            irPort.LoadIRDriver(DisplayControl.Driver);
                            _volumeDriverLoaded = false;
                        }
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

            // Look up the command - check volumeCommands first, then fall back to commands
            string commandValue = null;
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
                CrestronConsole.PrintLine(LogHeader + "Volume command '{0}' not found for {1}", commandKey, Name);
                return;
            }

            try
            {
                if (DisplayControl.Method.Equals("ir", StringComparison.OrdinalIgnoreCase))
                {
                    var irPort = GetIROutputPort(DisplayControl.Port);
                    if (irPort != null)
                    {
                        // If there's a separate volume driver (different from main driver), load it
                        if (!string.IsNullOrEmpty(DisplayControl.VolumeDriver) 
                            && !DisplayControl.VolumeDriver.Equals(DisplayControl.Driver, StringComparison.OrdinalIgnoreCase)
                            && !_volumeDriverLoaded)
                        {
                            irPort.LoadIRDriver(DisplayControl.VolumeDriver);
                            _volumeDriverLoaded = true;
                        }
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
                ErrorLog.Error(LogHeader + "Error sending volume command '{0}' to {1}: {2}", commandKey, Name, e.Message);
            }
        }

        /// <summary>
        /// Starts sending a volume command continuously (press-and-hold).
        /// Uses the IR port's native Press() which auto-repeats until Release() is called.
        /// If a separate volumeDriver is defined, loads it before pressing.
        /// </summary>
        public void StartVolumeCommand(string commandKey)
        {
            if (DisplayControl == null || DmDevice == null) return;
            if (string.IsNullOrEmpty(DisplayControl.Method)) return;

            // Look up the command - check volumeCommands first, then fall back to commands
            string commandValue = null;
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
                CrestronConsole.PrintLine(LogHeader + "Volume command '{0}' not found for {1}", commandKey, Name);
                return;
            }

            try
            {
                if (DisplayControl.Method.Equals("ir", StringComparison.OrdinalIgnoreCase))
                {
                    var irPort = GetIROutputPort(DisplayControl.Port);
                    if (irPort != null)
                    {
                        // If there's a separate volume driver (different from main driver), load it
                        if (!string.IsNullOrEmpty(DisplayControl.VolumeDriver)
                            && !DisplayControl.VolumeDriver.Equals(DisplayControl.Driver, StringComparison.OrdinalIgnoreCase)
                            && !_volumeDriverLoaded)
                        {
                            irPort.LoadIRDriver(DisplayControl.VolumeDriver);
                            _volumeDriverLoaded = true;
                        }
                        irPort.Press(commandValue);
                    }
                }
                else if (DisplayControl.Method.Equals("serial", StringComparison.OrdinalIgnoreCase))
                {
                    // Serial doesn't support press/hold natively — send once
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
        /// Stops the currently held volume command (releases the IR press).
        /// </summary>
        public void StopVolumeCommand()
        {
            if (DisplayControl == null || DmDevice == null) return;
            if (string.IsNullOrEmpty(DisplayControl.Method)) return;

            try
            {
                if (DisplayControl.Method.Equals("ir", StringComparison.OrdinalIgnoreCase))
                {
                    var irPort = GetIROutputPort(DisplayControl.Port);
                    if (irPort != null)
                    {
                        irPort.Release();
                    }
                }
            }
            catch (Exception e)
            {
                ErrorLog.Error(LogHeader + "Error stopping volume command on {0}: {1}", Name, e.Message);
            }
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
