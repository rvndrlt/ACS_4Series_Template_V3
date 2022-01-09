
using System.Collections.Generic;


namespace ACS_4Series_Template_V1.MusicPresets
{
    public class MusicPresetsConfig
    {
        public MusicPresetsConfig(ushort number, List<ushort> sources, List<ushort> volumes)
        {
            this.Number = number;
            this.Sources = sources;
            this.Volumes = volumes;

        }
        public ushort Number { get; set; }
        public List<ushort> Sources { get; set; }
        public List<ushort> Volumes { get; set; }
    }
}
