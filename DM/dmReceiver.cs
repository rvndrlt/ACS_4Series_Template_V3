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
        public Assembly DmNVXAssembly;
        public CrestronControlSystem CS;
        private const string LogHeader = "[DMreceiver] ";
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
            if (DisplayControl == null || DmNvx35X == null) return;

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
                    var irPort = DmNvx35X.IROutputPorts[DisplayControl.Port];
                    irPort.LoadIRDriver(DisplayControl.Driver);
                    CrestronConsole.PrintLine(LogHeader + "Loaded IR driver '{0}' on port {1} for {2}",
                        DisplayControl.Driver, DisplayControl.Port, Name);
                }
                else if (DisplayControl.Method.Equals("serial", StringComparison.OrdinalIgnoreCase))
                {
                    var comPort = DmNvx35X.ComPorts[DisplayControl.Port];
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
            if (DisplayControl == null || DisplayControl.Commands == null || DmNvx35X == null) return;

            if (!DisplayControl.Commands.TryGetValue(commandKey, out string commandValue))
            {
                CrestronConsole.PrintLine(LogHeader + "Command '{0}' not found for {1}", commandKey, Name);
                return;
            }

            try
            {
                if (DisplayControl.Method.Equals("ir", StringComparison.OrdinalIgnoreCase))
                {
                    DmNvx35X.IROutputPorts[DisplayControl.Port].PressAndRelease(commandValue, 200);
                }
                else if (DisplayControl.Method.Equals("serial", StringComparison.OrdinalIgnoreCase))
                {
                    DmNvx35X.ComPorts[DisplayControl.Port].Send(commandValue);
                }
            }
            catch (Exception e)
            {
                ErrorLog.Error(LogHeader + "Error sending command '{0}' to {1}: {2}", commandKey, Name, e.Message);
            }
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
            if (DmNvx35X == null) return;

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

        public bool Register() {
            try { 
                this.DmNvx35X = this.RetrieveUiObject(this.Type, this.Ipid);
                if (this.DmNvx35X == null || this.Ipid == 0)
                {
                    return false;
                }
                this.DmNvx35X.Description = this.Name;

                this.DmNvx35X.Control.DeviceMode = eDeviceMode.Receiver;
                this.DmNvx35X.BaseEvent += DmNvx35XEventHandler;
                this.DmNvx35X.HdmiOut.StreamChange += DmNvx35X_StreamChangeEventHandler;
                this.DmNvx35X.SourceReceive.StreamChange += DmNvx35X_StreamChangeEventHandler;
                this.DmNvx35X.SourceTransmit.StreamChange += DmNvx35X_StreamChangeEventHandler;
                if (this.DmNvx35X.Register() != Crestron.SimplSharpPro.eDeviceRegistrationUnRegistrationResponse.Success)
                {
                    ErrorLog.Error(LogHeader + "Error registring receiver {0}", this.Name);
                    return false;
                }
                else
                {
                    return true;
                }

            }
            catch (Exception e)
            {
                ErrorLog.Error(LogHeader + "Excepting when trying to register DM {0}: {1}", this.Name, e.Message);
                return false;
            }
        }


        public DmNvx35x RetrieveUiObject(string DMBoxType, uint deviceId)
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

                // create the object with all the information
                CrestronConsole.PrintLine("retrieved {0} {1}", DMBoxType, deviceId);
                return (DmNvx35x)cinfo.Invoke(new object[] { deviceId, this.CS});
            }
            catch (MissingMethodException e)
            {
                ErrorLog.Error(LogHeader + "Unable to create dmbox. No constructor: {0}", e.Message);
            }
            catch (ArgumentException e)
            {
                ErrorLog.Error(LogHeader + "Unable to create dmbox. No type: {0}", e.Message);
            }
            catch (NullReferenceException e)
            {
                CrestronConsole.PrintLine(LogHeader + "Unable to create dmbox. No match: {0}", e.Message);
                ErrorLog.Error(LogHeader + "Unable to create dmbox. No match: {0}", e.Message);
            }

            return null;
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
