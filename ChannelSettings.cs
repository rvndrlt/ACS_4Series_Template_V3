using System;
using System.Collections.Generic;
using System.Xml;
using Crestron.SimplSharp;
using Crestron.SimplSharp.CrestronIO;
using ACS_4Series_Template_V3.UI;

namespace ACS_4Series_Template_V3
{
    public class ChannelSettings
    {
        private readonly ControlSystem _parent;
        private readonly Dictionary<ushort, List<ChannelEntry>> _scenarios = new Dictionary<ushort, List<ChannelEntry>>();

        public ChannelSettings(ControlSystem parent)
        {
            _parent = parent;
            LoadFromFile();
        }

        private void LoadFromFile()
        {
            string filePath = @"\nvram\channelSettings.xml";
            try
            {
                if (!File.Exists(filePath))
                {
                    CrestronConsole.PrintLine("[ChannelSettings] File not found: {0}", filePath);
                    return;
                }

                XmlDocument doc = new XmlDocument();
                doc.Load(filePath);

                XmlNodeList scenarioNodes = doc.SelectNodes("/ROOT/scenarios/scenario");
                if (scenarioNodes == null) return;

                foreach (XmlNode scenarioNode in scenarioNodes)
                {
                    ushort scenarioNumber = ushort.Parse(scenarioNode.Attributes["number"].Value);
                    var channels = new List<ChannelEntry>();

                    XmlNodeList channelNodes = scenarioNode.SelectNodes("channel");
                    if (channelNodes != null)
                    {
                        foreach (XmlNode channelNode in channelNodes)
                        {
                            channels.Add(new ChannelEntry
                            {
                                Number = ushort.Parse(channelNode.Attributes["number"].Value),
                                Name = channelNode.Attributes["Name"].Value,
                                AnalogMode = ushort.Parse(channelNode.Attributes["analogMode"].Value)
                            });
                        }
                    }

                    _scenarios[scenarioNumber] = channels;
                    CrestronConsole.PrintLine("[ChannelSettings] Loaded scenario {0}: {1} channels", scenarioNumber, channels.Count);
                }
            }
            catch (Exception ex)
            {
                CrestronConsole.PrintLine("[ChannelSettings] Error loading XML: {0}", ex.Message);
            }
        }

        /// <summary>
        /// Updates the TSR-310 channel buttons for the current group of 6.
        /// </summary>
        public void UpdateChannelButtons(ushort tpNumber)
        {
            var tp = _parent.manager.touchpanelZ[tpNumber];
            ushort scenario = GetScenarioForTP(tpNumber);
            //CrestronConsole.PrintLine("[ChannelSettings] UpdateChannelButtons TP-{0} scenario={1}", tpNumber, scenario);
            if (scenario == 0 || !_scenarios.ContainsKey(scenario)) return;

            var channels = _scenarios[scenario];
            ushort group = tp.CurrentChannelGroupNum;
            ushort startIndex = (ushort)((group - 1) * 6);
            //CrestronConsole.PrintLine("[ChannelSettings] group={0} startIndex={1} channelCount={2}", group, startIndex, channels.Count);

            // Set visibility for each of 6 buttons
            for (ushort i = 0; i < 6; i++)
            {
                if (startIndex + i < channels.Count && channels[startIndex + i].Number > 0)
                {
                    tp.UserInterface.BooleanInput[(ushort)(361 + i)].BoolValue = true;
                    tp.UserInterface.UShortInput[(ushort)(251 + i)].UShortValue = channels[startIndex + i].AnalogMode;
                }
                else
                {
                    tp.UserInterface.BooleanInput[(ushort)(361 + i)].BoolValue = false;
                    tp.UserInterface.UShortInput[(ushort)(251 + i)].UShortValue = 0;
                }
            }

            // More button visibility (join 358) - true if scenario has more than 6 channels total
            bool hasMore = channels.Count > 6;
            //CrestronConsole.PrintLine("[ChannelSettings] hasMore={0} (count={1})", hasMore, channels.Count);
            tp.UserInterface.BooleanInput[358].BoolValue = hasMore;
        }

        /// <summary>
        /// Handles a channel button press (351-356). Sends channel number as string to subsystem EISC.
        /// </summary>
        public void HandleChannelButtonPress(ushort tpNumber, ushort buttonNumber)
        {
            var tp = _parent.manager.touchpanelZ[tpNumber];
            ushort scenario = GetScenarioForTP(tpNumber);
            if (scenario == 0 || !_scenarios.ContainsKey(scenario)) return;

            var channels = _scenarios[scenario];
            ushort group = tp.CurrentChannelGroupNum;
            ushort index = (ushort)((group - 1) * 6 + (buttonNumber - 351));

            if (index >= channels.Count) return;

            string channelNumber = channels[index].Number.ToString();
            ushort eiscJoin = (ushort)((tpNumber - 1) * 100 + 1);

            //CrestronConsole.PrintLine("[ChannelSettings] TP-{0} channel {1} -> EISC serial {2}", tpNumber, channelNumber, eiscJoin);

            if (tpNumber <= 20)
            {
                _parent.subsystemControlEISC.StringInput[eiscJoin].StringValue = channelNumber;
            }
            else
            {
                ushort adjustedJoin = (ushort)(eiscJoin - (20 * 100));
                _parent.subsystemControlEISC2.StringInput[adjustedJoin].StringValue = channelNumber;
            }
        }

        /// <summary>
        /// Handles the "More" button press (357). Cycles to next group or wraps to 1.
        /// </summary>
        public void HandleMoreButtonPress(ushort tpNumber)
        {
            var tp = _parent.manager.touchpanelZ[tpNumber];
            ushort scenario = GetScenarioForTP(tpNumber);
            if (scenario == 0 || !_scenarios.ContainsKey(scenario)) return;

            var channels = _scenarios[scenario];
            ushort totalGroups = (ushort)((channels.Count + 5) / 6);

            tp.CurrentChannelGroupNum++;
            if (tp.CurrentChannelGroupNum > totalGroups)
            {
                tp.CurrentChannelGroupNum = 1;
            }

            UpdateChannelButtons(tpNumber);
        }

        private ushort GetScenarioForTP(ushort tpNumber)
        {
            var tp = _parent.manager.touchpanelZ[tpNumber];
            ushort currentVSrc = _parent.manager.RoomZ[tp.CurrentRoomNum].CurrentVideoSrc;
            if (currentVSrc == 0 || !_parent.manager.VideoSourceZ.ContainsKey(currentVSrc)) return 0;
            return _parent.manager.VideoSourceZ[currentVSrc].FavoriteScenario;
        }
    }

    public class ChannelEntry
    {
        public ushort Number { get; set; }
        public string Name { get; set; }
        public ushort AnalogMode { get; set; }
    }
}
