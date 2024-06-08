using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ACS_4Series_Template_V3.ProjectInfo
{
    public class ProjectInfoConfig
    {
        public ProjectInfoConfig(string projectName, string ddnsAddress)
        {
            this.ProjectName = projectName;
            this.DDNSAdress = ddnsAddress;
        }
        public string ProjectName { get; set; }
        public string DDNSAdress { get; set; }

    }
}
