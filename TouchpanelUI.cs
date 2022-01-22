//-----------------------------------------------------------------------
// <copyright file="TouchpanelUI.cs" company="Crestron">
//     Copyright (c) Crestron Electronics. All rights reserved.
// </copyright>
//-----------------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
//using System.Threading.Tasks;
using Crestron.SimplSharp;                       // For Basic SIMPL# Classes
using Crestron.SimplSharp.CrestronIO;            // For Directory
using Crestron.SimplSharp.Reflection;
using Crestron.SimplSharpPro;                    // For Basic SIMPL#Pro classes
using Crestron.SimplSharpPro.CrestronThread;     // For Threading
using Crestron.SimplSharpPro.DeviceSupport;      // For Generic Device Support
using Crestron.SimplSharpPro.Diagnostics;        // For System Monitor Access

namespace ACS_4Series_Template_V1.UI
{
    /// <summary>
    /// Allows us to instantiate and register a touchpanel dynamically
    /// </summary>
    public class TouchpanelUI
    {
        /// <summary>
        /// Hardware interface for touchpanel
        /// </summary>
        public BasicTriListWithSmartObject UserInterface;

        /// <summary>
        /// Used by reflection to load Crestron.SimplSharpPro.UI.dll
        /// </summary>
        public Assembly TpAssembly;

        /// <summary>
        /// CrestronControlSystem
        /// </summary>
        public CrestronControlSystem Cs;

        /// <summary>
        /// Used for logging information to error log
        /// </summary>
        private const string LogHeader = "[UI] ";

        /// <summary>
        /// Initializes a new instance of the TouchpanelUI class
        /// </summary>
        /// <param name="type">Touchpanel type (ie. Tsw760)</param>
        /// <param name="id">IPID</param>
        /// <param name="label">Label you want to show up in the IPTable</param>
        /// <param name="cs">CrestronControlSystem</param>
        public TouchpanelUI(ushort number, ushort ipid, string type, string name, bool HTML_UI, ushort homePageScenario, ushort subSystemScenario, ushort floorScenario, ushort defaultRoom, bool changeRoomButtonEnable, string changeRoomButtonText, bool useAnalogModes, bool dontInheritSubsystemScenario)
        {
            this.Number = number;
            this.Ipid = ipid;
            this.Type = type;
            this.Name = name;
            this.HTML_UI = HTML_UI;
            this.HomePageScenario = homePageScenario;
            this.SubSystemScenario = subSystemScenario;
            this.FloorScenario = floorScenario;
            this.DefaultRoom = defaultRoom;
            this.ChangeRoomButtonEnable = changeRoomButtonEnable;
            this.ChangeRoomButtonText = changeRoomButtonText;
            this.UseAnalogModes = useAnalogModes;
            this.DontInheritSubsystemScenario = dontInheritSubsystemScenario;
        }
        public ushort Number { get; set; }
        /// <summary>
        /// Gets or sets IPID of the touchpanel
        /// </summary>
        public ushort Ipid { get; set; }
        /// <summary>
        /// Gets or sets type of touchpanel. Can be "Tsw760", "Tsw1060", etc
        /// Keep in mind that this will not be checked before trying to load
        /// Yes, that can be improved ;)
        /// </summary>
        public string Type { get; set; }



        /// <summary>
        /// Gets or sets label you want to show in IPTable
        /// </summary>
        public string Name { get; set; }

        public bool HTML_UI { get; set; }
        public ushort HomePageScenario { get; set; }
        public ushort SubSystemScenario { get; set; }
        public ushort FloorScenario { get; set; }
        public ushort DefaultRoom { get; set; }
        public ushort CurrentFloorNum { get; set; }
        public ushort CurrentRoomNum { get; set; }
        public bool ChangeRoomButtonEnable { get; set; }
        public string ChangeRoomButtonText { get; set; }
        public bool UseAnalogModes { get; set; }
        public bool DontInheritSubsystemScenario { get; set; }

        public ushort CurrentVSrcGroupNum { get; set; }
        public ushort CurrentASrcGroupNum { get; set; }

        public ushort CurrentPageNumber { get; set; }// 0 = HOME, 1 = RoomList, 2 = RoomSubsystemList
        public ushort CurrentSubsystemNumber { get; set; }


        /// <summary>
        /// Register the touchpanel using the proper information
        /// </summary>
        /// <returns>true or false, depending on if the registration succeeded</returns>
        public bool Register()
        {
            try
            {
                this.UserInterface = this.RetrieveUiObject(this.Type, this.Ipid);
                CrestronConsole.PrintLine("register retrieved {0}", this.Type);
                if (this.UserInterface == null)
                {
                    return false;
                }
                CrestronConsole.PrintLine("not null {0}", this.Type);
                this.UserInterface.Description = this.Name;

                this.UserInterface.SigChange += this.UserInterfaceObject_SigChange;

                // load smart objects
                /*string sgdPath = Path.Combine(Directory.GetApplicationDirectory(), "XPanel_v1.sgd");

                this.UserInterface.LoadSmartObjects(sgdPath);

                ErrorLog.Error(string.Format(LogHeader + "Loaded SmartObjects: {0}", this.UserInterface.SmartObjects.Count));
                foreach (KeyValuePair<uint, SmartObject> smartObject in this.UserInterface.SmartObjects)
                {
                    smartObject.Value.SigChange += new Crestron.SimplSharpPro.SmartObjectSigChangeEventHandler(this.SO_SigChange);
                }*/

                if (this.UserInterface.Register() != Crestron.SimplSharpPro.eDeviceRegistrationUnRegistrationResponse.Success)
                {
                    ErrorLog.Error(LogHeader + "Error registring UI {0}", this.Name);
                    return false;
                }
                else
                {
                    return true;
                }
            }
            catch (Exception e)
            {
                ErrorLog.Error(LogHeader + "Excepting when trying to register UI {0}: {1}", this.Name, e.Message);
                CrestronConsole.PrintLine(LogHeader + "Excepting when trying to register UI {0}: {1}", this.Name, e.Message);
                return false;
            }
        }

        /// <summary>
        /// Instantiates the touchpanel properly
        /// </summary>
        /// <param name="touchpanelType">Type of touchpanel (Tsw760, Tsw1060,etc)</param>
        /// <param name="deviceId">IPID of touchpanel</param>
        /// <returns>BasicTriListWithSmartObject to be used genericly</returns>
        public BasicTriListWithSmartObject RetrieveUiObject(string touchpanelType, uint deviceId)
        {
            try
            {
                // always load from UI assembly
                
                this.TpAssembly = Assembly.LoadFrom(Path.Combine(Directory.GetApplicationDirectory(), "Crestron.SimplSharpPro.UI.dll"));

                // add the correct device type that we want to reflect into
                string assembly = string.Format("Crestron.SimplSharpPro.UI.{0}", touchpanelType);
                CType cswitcher = this.TpAssembly.GetType(assembly);
                // get the correct constructor for this type
                CType[] constructorTypes = new CType[] { typeof(uint), typeof(CrestronControlSystem) };
                // get info for the previously found constructor
                ConstructorInfo cinfo = cswitcher.GetConstructor(constructorTypes);
                if (cinfo != null) CrestronConsole.PrintLine("cinfo.Attributes:{0} name:{1} reflected:{2}", cinfo.Attributes, cinfo.Name, cinfo.ReflectedType);
                else CrestronConsole.PrintLine("cinfo NULL");
                // create the object with all the information
                CrestronConsole.PrintLine("retrieved {0}", touchpanelType);
                return (BasicTriListWithSmartObject)cinfo.Invoke(new object[] { deviceId, this.Cs });
            }
            catch (MissingMethodException e)
            {
                ErrorLog.Error(LogHeader + "Unable to create TP. No constructor: {0}", e.Message);
            }
            catch (ArgumentException e)
            {
                ErrorLog.Error(LogHeader + "Unable to create TP. No type: {0}", e.Message);
            }
            catch (NullReferenceException e)
            {
                ErrorLog.Error(LogHeader + "Unable to create TP. No match: {0}", e.Message);
            }

            return null;
        }

        /// <summary>
        /// SigChange for SmartObjects
        /// There are some other / better ways to do this potentially
        /// </summary>
        /// <param name="currentDevice">The device that triggered the event</param>
        /// <param name="args">Contains the SigType, Sig.Number and Sig.Value and more</param>
        private void SO_SigChange(GenericBase currentDevice, SmartObjectEventArgs args)
        {
            ErrorLog.Notice(LogHeader + "Device {0} - Pressed {1}\n", currentDevice.Description, args.SmartObjectArgs.ID);
        }

        /// <summary>
        /// Eventhandler for boolean/ushort/string sigs
        /// </summary>
        /// <param name="currentDevice">The device that triggered the event</param>
        /// <param name="args">Contains the SigType, Sig.Number and Sig.Value and more</param>
        private void UserInterfaceObject_SigChange(BasicTriList currentDevice, SigEventArgs args)
        {
            ErrorLog.Notice(LogHeader + "Device {0} - Pressed {1}\n", currentDevice.Description, args.Sig.Number);
        }
    }
}
