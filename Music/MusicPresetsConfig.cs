
using System.Collections.Generic;


namespace ACS_4Series_Template_V1.MusicPresets
{
    public class MusicPresetsConfig
    {
        public MusicPresetsConfig(ushort number, string name, List<ushort> sources, List<ushort> volumes)
        {
            this.Number = number;
            this.Name = name;
            this.Sources = sources;
            this.Volumes = volumes;

        }
        public ushort Number { get; set; }
        public string Name { get; set; }
        public List<ushort> Sources { get; set; }
        public List<ushort> Volumes { get; set; }
    }
}
