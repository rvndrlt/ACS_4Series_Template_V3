using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Crestron.SimplSharp;

namespace ACS_4Series_Template_V2.LiftScenarios
{
    public class LiftScenariosConfig
    {
        public LiftScenariosConfig(ushort number, string buttonLabel, ushort openWithOnCmdNum, ushort closeWithOffCmdNum, List<ushort> liftCmds)
        {
            this.Number = number;
            this.ButtonLabel = buttonLabel;
            this.OpenWithOnCmdNum = openWithOnCmdNum;
            this.CloseWithOffCmdNum = closeWithOffCmdNum;
            this.LiftCmds = liftCmds;

        }
        public ushort Number { get; set; }
        public string ButtonLabel { get; set; }
        public ushort OpenWithOnCmdNum { get; set; }
        public ushort CloseWithOffCmdNum { get; set; }
        //public LiftCmdConfig[] LiftCmdConfigs { get; set; }
        public List<ushort> LiftCmds { get; set; }


    }
    public class LiftCmdConfig
    {
        public LiftCmdConfig(ushort cmdNum, string name, ushort pulseTime)
        {
            this.CmdNum = cmdNum;
            this.Name = name;
            this.PulseTime = pulseTime;
        }
        public ushort CmdNum { get; set; }
        public string Name { get; set; }
        public ushort PulseTime { get; set; }

    }
}