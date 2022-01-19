using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Crestron.SimplSharp;
using Crestron.SimplSharpPro;
using Crestron.SimplSharpPro.DM.Streaming;
using Crestron.SimplSharp.Reflection;
using Crestron.SimplSharp.CrestronIO;

namespace ACS_4Series_Template_V1.DmTransmitter
{
    public class DmNVXtransmitter
    {
        public DmNvx35x DmNvx35X_BOX;
        public Assembly DmNVXAssembly;
        public CrestronControlSystem CS;
        private const string LogHeader = "[DMtransmitter] ";
        public DmNVXtransmitter(uint dmInputNumber, string name, uint ipid, string type, string multiCastAddress, CrestronControlSystem cs)
        {
            this.DmInputNumber = dmInputNumber;
            this.MultiCastAddress = multiCastAddress;
            this.Type = type;
            this.Ipid = ipid;
            this.Name = name;
            this.CS = cs;
        }
        public string Type { get; set; }
        public uint Ipid { get; set; }
        public uint DmInputNumber { get; set; }
        public string MultiCastAddress { get; set; }
        public string Name { get; set; }

        public bool Register() {
            try {
                this.DmNvx35X_BOX = this.RetrieveUiObject(this.Type, this.Ipid);
                if (this.DmNvx35X_BOX == null || this.Ipid == 0)
                {
                    return false;
                }
                this.DmNvx35X_BOX.Description = this.Name;
                this.DmNvx35X_BOX.Control.DeviceMode = eDeviceMode.Transmitter;
                this.DmNvx35X_BOX.BaseEvent += DmNvx35XEventHandler;
                this.DmNvx35X_BOX.HdmiOut.StreamChange += DmNvx35X_StreamChangeEventHandler;
                this.DmNvx35X_BOX.SourceReceive.StreamChange += DmNvx35X_StreamChangeEventHandler;
                this.DmNvx35X_BOX.SourceTransmit.StreamChange += DmNvx35X_StreamChangeEventHandler;
                if (this.DmNvx35X_BOX.Register() != Crestron.SimplSharpPro.eDeviceRegistrationUnRegistrationResponse.Success)
                {
                    ErrorLog.Error(LogHeader + "Error registring dmnvx {0}", this.Name);
                    CrestronConsole.PrintLine("error registering dmnvx {0}", this.DmNvx35X_BOX.Name) ;
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
                CrestronConsole.PrintLine(LogHeader + "Excepting when trying to register DM {0}: {1}", this.Name, e.Message);
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
                return (DmNvx35x)cinfo.Invoke(new object[] { deviceId, this.CS });
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
