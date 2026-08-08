using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Crestron.SimplSharp;

namespace ACS_4Series_Template_V3.MusicSources
{
    public class MusicSourceConfig
    {
        public MusicSourceConfig(ushort number, string name, string iconSerial, string iconHTML, ushort analogModeNumber, ushort naxBoxNumber, ushort switcherInputNumber, ushort streamingProviderNumber, string multiCastAddress, ushort flipsToPageNumber, ushort equipID)
        {
            this.Number = number;
            this.Name = name;
            this.IconSerial = iconSerial;
            this.IconHTML = iconHTML;
            this.AnalogModeNumber = analogModeNumber;
            this.NaxBoxNumber = naxBoxNumber;
            this.SwitcherInputNumber = switcherInputNumber;
            this.StreamingProviderNumber = streamingProviderNumber;
            this.MultiCastAddress = multiCastAddress;
            this.FlipsToPageNumber = flipsToPageNumber;
            this.EquipID = equipID;
        }
        public ushort Number { get; set; }
        public string Name { get; set; }
        public string IconSerial { get; set; }

        public string IconHTML { get; set; }
        public ushort AnalogModeNumber { get; set; }
        public ushort NaxBoxNumber { get; set; }
        public ushort SwitcherInputNumber { get; set; }
        public ushort StreamingProviderNumber { get; set; }
        public string MultiCastAddress { get; set; }
        public ushort FlipsToPageNumber { get; set; }
        public ushort EquipID { get; set; }

        /// <summary>
        /// Which GUI layout set this source's page uses on HTML panels — "media player
        /// scenario 2". Raw config value: 0 means the key was absent from an older config
        /// file. Read it through <see cref="EffectiveGuiScenario"/>, never directly.
        /// Mirrors VideoSourceConfig.GuiScenarioNumber.
        /// </summary>
        public ushort GuiScenarioNumber { get; set; }

        /// <summary>
        /// GuiScenarioNumber with the back-compat default applied: unset (0) means scenario 1.
        /// The ONE place that fallback lives, so a config file predating the field behaves
        /// exactly as it did before.
        /// </summary>
        public ushort EffectiveGuiScenario
        {
            get { return this.GuiScenarioNumber > 0 ? this.GuiScenarioNumber : (ushort)1; }
        }

        public bool InUse { get; set; }
    }
}