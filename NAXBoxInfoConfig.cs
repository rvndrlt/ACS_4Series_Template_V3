using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ACS_4Series_Template_V3.NAXBoxInfo
{
    public class NAXBoxInfoConfig
    {
        public NAXBoxInfoConfig(ushort boxNumber, string type)
        {
            this.BoxNumber = boxNumber;
            this.Type = type;

        }
        public ushort BoxNumber { get; set; }
        public string Type { get; set; }
    }
}
