using System;
using Crestron.SimplSharp;

namespace ACS_4Series_Template_V3
{
    public partial class ControlSystem
    {
        #region Console Commands

        public void ReinitializeSystem(string parms)
        {
            if (parms == "?")
            {
                CrestronConsole.ConsoleCommandResponse("reloadjson\n\r\treloads the configuration from the json file.\n\r");
            }
            else
            {
                InitializeSystem();
            }
        }

        public void TestingPageNumber(string parms)
        {
            ushort pageNum = 0;
            if (parms == "?")
            {
                foreach (var tp in manager.touchpanelZ)
                {
                    CrestronConsole.PrintLine("TP-{0} page {1}", tp.Value.Number, tp.Value.CurrentPageNumber);
                }
            }
            else
            {
                if (parms == "1") { pageNum = 1; }
                else if (parms == "2") { pageNum = 2; }
                foreach (var tp in manager.touchpanelZ)
                {
                    tp.Value.CurrentPageNumber = pageNum;
                }
                CrestronConsole.PrintLine("set all panels to page {0}", pageNum);
            }
        }

        public void EnableLogging(string parms)
        {
            if (parms == "?")
            {
                CrestronConsole.ConsoleCommandResponse("logging on\n\r\tturns on logging\n\rlogging off\n\r\tturns off logging\n\r");
            }
            else if (parms == "on")
            {
                logging = true;
                CrestronConsole.PrintLine("logging enabled");
            }
            else if (parms == "off")
            {
                logging = false;
                CrestronConsole.PrintLine("logging disabled");
            }
        }

        public void ReportHVAC(string parms)
        {
            foreach (var rm in manager.RoomZ)
            {
                CrestronConsole.PrintLine("{0} {1}", rm.Value.Name, rm.Value.CurrentTemperature);
            }
        }

        public void numFloors(string parms)
        {
            if (manager.touchpanelZ[1].HTML_UI)
            {
            }
            else
            {
                manager.touchpanelZ[1].UserInterface.SmartObjects[3].UShortInput[4].UShortValue = Convert.ToUInt16(parms);
            }
            CrestronConsole.PrintLine("setting floors to {0}", Convert.ToUInt16(parms));
        }

        public void numZones(string parms)
        {
            if (manager.touchpanelZ[1].HTML_UI)
            {
            }
            else
            {
                manager.touchpanelZ[1].UserInterface.SmartObjects[4].UShortInput[1].UShortValue = Convert.ToUInt16(parms);
                manager.touchpanelZ[1].UserInterface.SmartObjects[4].UShortInput[2].UShortValue = Convert.ToUInt16(parms);
                manager.touchpanelZ[1].UserInterface.SmartObjects[4].UShortInput[3].UShortValue = Convert.ToUInt16(parms);
            }
            CrestronConsole.PrintLine("setting zones to {0}", Convert.ToUInt16(parms));
        }

        public void ReportQuickAction(string parms)
        {
            CrestronConsole.PrintLine("quick");
            ushort preset = Convert.ToUInt16(parms);
            CrestronConsole.PrintLine("{0}", quickActionXML.PresetName[preset - 1]);
        }

        public void TestWriteXML(string parms)
        {
            this.quickActionXML.PresetName[2] = Convert.ToString(parms);
            //not implemented
        }

        public void ReportIP(string parms)
        {
            CrestronConsole.PrintLine("ipaddress {0} httpPort {1} httpsPort {2}", IPaddress, httpPort, httpsPort);
        }

        public void ReportHome(string parms)
        {
            CrestronConsole.PrintLine("url {0}", string.Format("https://{0}:{1}/HOME.JPG", IPaddress, httpsPort));
        }

        public void QueryLights(string parms)
        {
            foreach (var rm in manager.RoomZ)
            {
                if (rm.Value.LightsID > 0)
                {
                    CrestronConsole.PrintLine("querying lights {0} offBool{1} {2}", rm.Value.Name, rm.Value.LightsAreOff, rm.Value.LightStatusText);
                }
            }
        }

        #endregion
    }
}
