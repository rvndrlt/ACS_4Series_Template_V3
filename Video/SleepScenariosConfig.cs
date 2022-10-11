using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Crestron.SimplSharp;

namespace ACS_4Series_Template_V2.SleepScenarios
{
    public class SleepScenariosConfig
    {
        public SleepScenariosConfig(ushort number, string buttonLabel, List<ushort> sleepCmds)
        {
            this.Number = number;
            this.ButtonLabel = buttonLabel;
            this.SleepCmds = sleepCmds;

        }
        public ushort Number { get; set; }
        public string ButtonLabel { get; set; }
        //public SleepCmdConfig[] SleepCmdConfigs { get; set; }
        public List<ushort> SleepCmds { get; set; }


    }
    public class SleepCmdConfig
    {
        public SleepCmdConfig(ushort cmdNum, string name, ushort length)
        {
            this.CmdNum = cmdNum;
            this.Name = name;
            this.Length = length;
        }
        public ushort CmdNum { get; set; }
        public string Name { get; set; }
        public ushort Length { get; set; }

    }
}