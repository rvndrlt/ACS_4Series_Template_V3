using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Crestron.SimplSharp;

namespace ACS_4Series_Template_V3.VideoSources
{
    public class VideoSourceConfig
    {
        public VideoSourceConfig(ushort number, string name, string displayName, string iconSerial, string iconHTML, ushort analogModeNumber, ushort vidSwitcherInputNumber, ushort audSwitcherInputNumber, string streamLocation, string multiCastAddress, string aes67SessionName, ushort flipsToPageNumber, ushort equipID)
        {
            this.Number = number;
            this.Name = name;
            this.DisplayName = displayName;
            this.IconSerial = iconSerial;
            this.IconHTML = iconHTML;
            this.AnalogModeNumber = analogModeNumber;
            this.VidSwitcherInputNumber = vidSwitcherInputNumber;
            this.AudSwitcherInputNumber = audSwitcherInputNumber;
            this.StreamLocation = streamLocation;
            this.MultiCastAddress = multiCastAddress;
            this.AES67SessionName = aes67SessionName;
            this.FlipsToPageNumber = flipsToPageNumber;
            this.EquipID = equipID;
        }
        public ushort Number { get; set; }
        public string Name { get; set; }
        public string DisplayName { get; set; }
        public string IconSerial { get; set; }

        public string IconHTML { get; set; }
        public ushort AnalogModeNumber { get; set; }
        public ushort VidSwitcherInputNumber { get; set; }
        public ushort AudSwitcherInputNumber { get; set; }
        public string StreamLocation { get; set; }
        public string MultiCastAddress { get; set; }
        public string AES67SessionName { get; set; }
        public ushort FlipsToPageNumber { get; set; }
        public ushort EquipID { get; set; }
        /// <summary>
        /// Which set of channel favorites to read from the favorites JSON and show on the
        /// page. CONTENT, not navigation — unrelated to GuiScenarioNumber despite both
        /// ending in "Scenario".
        /// </summary>
        public ushort FavoriteScenario { get; set; }

        /// <summary>
        /// Which GUI layout set this source's pages use — "Apple TV scenario 3". Raw config
        /// value: 0 means the key was absent from an older config file. Read it through
        /// <see cref="EffectiveGuiScenario"/>, never directly.
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

        /// <summary>
        /// Which sub-page (main/keypad) a dumb panel is showing for this source. HTML panels
        /// do NOT use this — there the view is local UI state owned by pageRouter.js and is
        /// never reported to the program. See PAGE-FLIP-DESCRIPTOR-PLAN.md, Phase 3.
        /// </summary>
        public ushort CurrentSubpageScenario { get; set; } = 1;
        public bool InUse { get; set; }
    }
}