using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Crestron.SimplSharp;

namespace ACS_4Series_Template_V3.FormatScenarios
{
    public class FormatScenariosConfig
    {
        public FormatScenariosConfig(ushort number, string buttonLabel, List<ushort> formatCmds)
        {
            this.Number = number;
            this.ButtonLabel = buttonLabel;
            this.FormatCmds = formatCmds;

        }
        public ushort Number { get; set; }
        public string ButtonLabel { get; set; }
        public List<ushort> FormatCmds { get; set; }
    }
    public class FormatCmdConfig
    {
        public FormatCmdConfig(ushort cmdNum, string name)
        {
            this.CmdNum = cmdNum;
            this.Name = name;
        }
        public ushort CmdNum { get; set; }
        public string Name { get; set; }

    }
}