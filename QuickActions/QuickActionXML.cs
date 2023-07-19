using System;
using System.Collections.Generic;
using System.Threading;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Crestron.SimplSharp;
using Crestron.SimplSharp.CrestronXml;
using Crestron.SimplSharp.CrestronXmlLinq;
using Crestron.SimplSharp.CrestronIO;
using Newtonsoft.Json.Linq;


namespace ACS_4Series_Template_V2.QuickActions
{
    public class QuickActionXML
    {
        private ControlSystem _parent;

        public QuickActionXML(ControlSystem parent)
        {
            _parent = parent;
        }

        public ushort NumberOfPresets;//number of quick actions
        private string[] presetName = new string[100];
        public ushort[] AvailableSubsystems = new ushort[10];//availableSubsystems
        public ushort NumberOfAvailableSubsystems;
        public ushort[] NumberOfIncludedSubsystems = new ushort[100];//each quick action will have its own subsystems it affects. this is the number of them.
        public ushort[,] IncludedSubsystems = new ushort[100, 100];
        public ushort[] PresetNum = new ushort[100];

        //music
        public ushort[] NumberOfMusicZones = new ushort[100];
        public ushort[,] MusicZoneChecked = new ushort[100, 100];
        public ushort[,] Sources = new ushort[100, 100];
        public ushort[,] Volumes = new ushort[100, 100];
        //lights
        public ushort[] NumberOfLightZones = new ushort[100];
        public ushort[,] LightScenes = new ushort[100, 100];
        //shades
        public ushort[] NumberOfShadeZones = new ushort[100];
        public ushort[,] ShadeLevels = new ushort[100, 100];
        //hvac
        public ushort[] NumberOfHVACZones = new ushort[100];

        public ushort[,] HVACZoneChecked = new ushort[100, 100];
        public ushort[,] HVACModes = new ushort[100, 100];
        public ushort[,] HVACSecondaryModes = new ushort[100, 100];
        public ushort[,] HVACHeatSetpoints = new ushort[100, 100];
        public ushort[,] HVACCoolSetpoints = new ushort[100, 100];

        //variables for local use
        public ushort quickActionToRecallOrSave;
        public bool saving;
        public string newQuickActionPresetName;
        public bool[] musicCheckboxes = new bool[100];
        public bool currentSubsysIsMusic = false;
        public bool currentSubsystemIsClimate = false;
        public bool[] climateCheckboxes = new bool[100];
        //public List<bool> climateCheckboxes = new List<bool>();
        private string path;
        private ushort currentSelectedSubsysNumber = 0;
        private string currentSelectedSubsysName = "";
        public bool musicIsChecked = false;
        public bool climateIsChecked = false;
        public string[] PresetName
        {
            get
            {
                return presetName;
            }
            set
            {
                presetName = value;
            }
        }

        public void readXML(string xmlFilePath)
        {
            using (XmlTextReader reader = new XmlTextReader(xmlFilePath))
            {
                ushort i = 0;
                path = xmlFilePath;
                while (reader.Read())
                {
                    if (reader.IsStartElement())
                    {
                        reader.ReadToFollowing("availableSubsystems");
                        string[] availableSubsystems = new string[10];
                        string[] includedSubsystems = new string[10];
                        string subs = reader.GetAttribute("subsystems");
                        string includedSubs = "";
                        char comma = ',';
                        NumberOfAvailableSubsystems = (ushort)(subs.Where(x => (x == comma)).Count() + 1);
                        availableSubsystems = reader.GetAttribute("subsystems").Split(',');
                        for (ushort j = 0; j < NumberOfAvailableSubsystems; j++)
                        {

                            if (!String.IsNullOrEmpty(availableSubsystems[j]))
                            {
                                AvailableSubsystems[j] = Convert.ToUInt16(availableSubsystems[j]);
                            }
                        }
                        reader.ReadToFollowing("quickAction");
                        do
                        {
                            PresetNum[i] = Convert.ToUInt16(reader.GetAttribute("number"));
                            PresetName[i] = reader.GetAttribute("name");
                            //get included subsystems for this preset
                            includedSubs = reader.GetAttribute("includedSubsystems");
                            NumberOfIncludedSubsystems[i] = (ushort)(includedSubs.Where(x => (x == comma)).Count() + 1);
                            includedSubsystems = reader.GetAttribute("includedSubsystems").Split(',');
                            for (ushort j = 0; j < NumberOfIncludedSubsystems[i]; j++)
                            {
                                if (!String.IsNullOrEmpty(includedSubsystems[j]))
                                {
                                    IncludedSubsystems[i, j] = Convert.ToUInt16(includedSubsystems[j]);
                                }
                            }
                            i++;
                        }

                        while (reader.ReadToNextSibling("quickAction"));
                        NumberOfPresets = i;//number of quick actions
                        i = 0;

                        //music presets
                        reader.ReadToFollowing("musicPreset");
                        do
                        {
                            List<string> checkedTemp = reader.GetAttribute("checked").Split(',').ToList();
                            List<string> srcTemp = reader.GetAttribute("sources").Split(',').ToList();
                            NumberOfMusicZones[i] = (ushort)srcTemp.Count;
                            CrestronConsole.PrintLine("NumberOfMusicZones{0}", NumberOfMusicZones[i]);

                            List<string> volTemp = reader.GetAttribute("volumes").Split(',').ToList();
                            for (ushort j = 0; j < srcTemp.Count; j++)
                            {
                                MusicZoneChecked[i, j] = Convert.ToUInt16(checkedTemp[j]);
                                Sources[i, j] = Convert.ToUInt16(srcTemp[j]);
                            }
                            for (ushort j = 0; j < volTemp.Count; j++)
                            {
                                Volumes[i, j] = Convert.ToUInt16(volTemp[j]);
                            }
                            i++;
                        }
                        while (reader.ReadToNextSibling("musicPreset"));

                        //lights presets
                        i = 0;
                        reader.ReadToFollowing("lightsPreset");
                        do
                        {
                            List<string> scenesTemp = reader.GetAttribute("scenes").Split(',').ToList();
                            NumberOfLightZones[i] = (ushort)scenesTemp.Count;
                            CrestronConsole.PrintLine("number of light zones {0}", NumberOfLightZones[i]);
                            for (ushort j = 0; j < scenesTemp.Count; j++)
                            {
                                LightScenes[i, j] = Convert.ToUInt16(scenesTemp[j]);
                            }
                            i++;
                        }
                        while (reader.ReadToNextSibling("lightsPreset"));

                        //shades presets
                        i = 0;
                        reader.ReadToFollowing("shadesPreset");
                        do
                        {
                            List<string> levelsTemp = reader.GetAttribute("levels").Split(',').ToList();
                            NumberOfShadeZones[i] = (ushort)levelsTemp.Count;
                            for (ushort j = 0; j < levelsTemp.Count; j++)
                            {
                                ShadeLevels[i, j] = Convert.ToUInt16(levelsTemp[j]);
                            }
                            i++;
                        }
                        while (reader.ReadToNextSibling("shadesPreset"));

                        //hvacPresets
                        i = 0;
                        reader.ReadToFollowing("hvacPreset");
                        do
                        {
                            List<string> checkedTemp = reader.GetAttribute("checked").Split(',').ToList();
                            List<string> modeTemp = reader.GetAttribute("modes").Split(',').ToList();
                            CrestronConsole.PrintLine("modestemp {0} {1} {2} {3}", modeTemp.Count, modeTemp[0], modeTemp[1], modeTemp[2]);
                            List<string> secondModeTemp = reader.GetAttribute("secondarymodes").Split(',').ToList();
                            List<string> heatTemp = reader.GetAttribute("heatsetpoints").Split(',').ToList();
                            List<string> coolTemp = reader.GetAttribute("coolsetpoints").Split(',').ToList();

                            NumberOfHVACZones[i] = (ushort)(modeTemp.Count);
                            for (ushort j = 0; j < NumberOfHVACZones[i]; j++)
                            {
                                HVACZoneChecked[i, j] = Convert.ToUInt16(checkedTemp[j]);
                                HVACModes[i, j] = Convert.ToUInt16(modeTemp[j]);
                                HVACSecondaryModes[i, j] = Convert.ToUInt16(secondModeTemp[j]);
                                HVACHeatSetpoints[i, j] = Convert.ToUInt16(heatTemp[j]);
                                HVACCoolSetpoints[i, j] = Convert.ToUInt16(coolTemp[j]);
                            }
                            i++;
                        }
                        while (reader.ReadToNextSibling("hvacPreset"));
                    }
                    break;
                }
                reader.Dispose(true);
            }
        }
        public void writeSubsystems(ushort presetNumberToWrite)
        {
            CrestronConsole.PrintLine("STARTING WRITE TO XML {0}:{1}", DateTime.Now.Second, DateTime.Now.Millisecond);
            musicIsChecked = false;
            climateIsChecked = false;
            //get the subsystems that are checked.
            List<ushort> checkedSubsystems = new List<ushort>();
            for (ushort i = 0; i < NumberOfAvailableSubsystems; i++)
            {
                if (_parent.imageEISC.BooleanInput[(ushort)(201 + i)].BoolValue)//if this subsystem is checked
                {
                    ushort subsysNumber = AvailableSubsystems[i];
                    checkedSubsystems.Add(AvailableSubsystems[i]);
                    if (_parent.manager.SubsystemZ[subsysNumber].Name.ToUpper() == "AUDIO" || _parent.manager.SubsystemZ[subsysNumber].Name.ToUpper() == "MUSIC")
                    {
                        musicIsChecked = true;
                    }
                    if (_parent.manager.SubsystemZ[subsysNumber].Name.ToUpper() == "CLIMATE" || _parent.manager.SubsystemZ[subsysNumber].Name.ToUpper() == "HVAC")
                    {
                        climateIsChecked = true;
                    }
                }
            }
            //make sure that at least 1 subsystem is selected
            string errorMessage = CheckForAtLeastOneCheckbox(checkedSubsystems);
            if (string.IsNullOrEmpty(errorMessage))
            {
                XDocument loaded = XDocument.Parse(File.ReadToEnd(path, Encoding.UTF8));
                var targetPreset = loaded
                    .Element("ROOT")
                    .Element("quickActions")
                    .Elements("quickAction")
                    .Where(e => e.Attribute("number").Value == Convert.ToString(presetNumberToWrite))
                    .Single();
                targetPreset.Attribute("includedSubsystems").Value = string.Join(",", checkedSubsystems);
                //update the name
                if (!String.IsNullOrEmpty(newQuickActionPresetName))
                {
                    presetName[presetNumberToWrite - 1] = newQuickActionPresetName;
                    _parent.imageEISC.StringInput[(ushort)(presetNumberToWrite + 3100)].StringValue = newQuickActionPresetName;
                    newQuickActionPresetName = null;
                }

                targetPreset.Attribute("name").Value = presetName[presetNumberToWrite - 1];
                //we need to update the currently loaded version of the xml as well 
                for (ushort i = 0; i < 10; i++)
                {
                    if (i < checkedSubsystems.Count) { IncludedSubsystems[presetNumberToWrite - 1, i] = checkedSubsystems[i]; }
                    else { IncludedSubsystems[presetNumberToWrite - 1, i] = 0; }//clear out any previous subsystems
                }
                NumberOfIncludedSubsystems[presetNumberToWrite - 1] = (ushort)checkedSubsystems.Count;

                if (musicIsChecked)
                {
                    //music
                    var musicPreset = loaded
                        .Element("ROOT")
                        .Element("musicPresets")
                        .Elements("musicPreset")
                        .Where(e => e.Attribute("number").Value == Convert.ToString(presetNumberToWrite))
                        .Single();
                    string[] checkedTemp = new string[NumberOfMusicZones[presetNumberToWrite - 1]];
                    string[] srcTemp = new string[NumberOfMusicZones[presetNumberToWrite - 1]];
                    string[] volTemp = new string[NumberOfMusicZones[presetNumberToWrite - 1]];
                    //initialize the strings
                    for (ushort i = 0; i < NumberOfMusicZones[presetNumberToWrite - 1]; i++)
                    {
                        checkedTemp[i] = "0";
                        srcTemp[i] = "0";
                        volTemp[i] = "0";
                    }
                    foreach (var rm in _parent.manager.RoomZ)
                    {
                        if (rm.Value.AudioID > 0)
                        {
                            //see if zone is checked
                            if (musicCheckboxes[rm.Value.AudioID - 1])
                            {
                                checkedTemp[rm.Value.AudioID - 1] = "1";
                                MusicZoneChecked[presetNumberToWrite - 1, rm.Value.AudioID - 1] = 1;
                                //we want the source number not the switcher input number.
                                Sources[presetNumberToWrite - 1, rm.Value.AudioID - 1] = rm.Value.CurrentMusicSrc;
                                Volumes[presetNumberToWrite - 1, rm.Value.AudioID - 1] = _parent.musicEISC3.UShortOutput[(ushort)(rm.Value.AudioID + 100)].UShortValue;

                            }
                            else
                            {
                                checkedTemp[rm.Value.AudioID - 1] = "0";
                                MusicZoneChecked[presetNumberToWrite - 1, rm.Value.AudioID - 1] = 0;
                                Sources[presetNumberToWrite - 1, rm.Value.AudioID - 1] = 0;
                                Volumes[presetNumberToWrite - 1, rm.Value.AudioID - 1] = 0;
                            }
                            srcTemp[rm.Value.AudioID - 1] = Convert.ToString(Sources[presetNumberToWrite - 1, rm.Value.AudioID - 1]);
                            volTemp[rm.Value.AudioID - 1] = Convert.ToString(Volumes[presetNumberToWrite - 1, rm.Value.AudioID - 1]);
                        }
                    }
                    string checkJoined, srcJoined, volJoined;
                    checkJoined = String.Join(",", checkedTemp);
                    srcJoined = String.Join(",", srcTemp);
                    volJoined = String.Join(",", volTemp);
                    CrestronConsole.PrintLine(" srcJoined {0}", srcJoined);
                    //update the sources/volumes
                    musicPreset.Attribute("checked").Value = checkJoined;
                    musicPreset.Attribute("sources").Value = srcJoined;
                    musicPreset.Attribute("volumes").Value = volJoined;
                }
                if (climateIsChecked)
                {

                    //climate
                    var climatePreset = loaded
                        .Element("ROOT")
                        .Element("hvacPresets")
                        .Elements("hvacPreset")
                        .Where(e => e.Attribute("number").Value == Convert.ToString(presetNumberToWrite))
                        .Single();
                    string[] checkedTemp = new string[NumberOfHVACZones[presetNumberToWrite - 1]];
                    string[] modesTemp = new string[NumberOfHVACZones[presetNumberToWrite - 1]];
                    string[] secondaryModesTemp = new string[NumberOfHVACZones[presetNumberToWrite - 1]];
                    string[] heatSetptsTemp = new string[NumberOfHVACZones[presetNumberToWrite - 1]];
                    string[] coolSetptsTemp = new string[NumberOfHVACZones[presetNumberToWrite - 1]];
                    //initialize the strings
                    for (ushort i = 0; i < NumberOfHVACZones[presetNumberToWrite - 1]; i++)
                    {
                        checkedTemp[i] = "0";
                        modesTemp[i] = "0";
                        secondaryModesTemp[i] = "0";
                        heatSetptsTemp[i] = "0";
                        coolSetptsTemp[i] = "0";
                    }

                    foreach (var rm in _parent.manager.RoomZ)
                    {
                        if (rm.Value.ClimateID > 0)
                        {

                            //see if zone is checked
                            if (climateCheckboxes[rm.Value.ClimateID - 1])
                            {
                                checkedTemp[rm.Value.ClimateID - 1] = "1";
                                HVACZoneChecked[presetNumberToWrite - 1, rm.Value.ClimateID - 1] = 1;
                                HVACModes[presetNumberToWrite - 1, rm.Value.ClimateID - 1] = rm.Value.ClimateModeNumber;
                                HVACSecondaryModes[presetNumberToWrite - 1, rm.Value.ClimateID - 1] = 0; //NOT IMPLEMENTED
                                HVACHeatSetpoints[presetNumberToWrite - 1, rm.Value.ClimateID - 1] = rm.Value.CurrentHeatSetpoint;
                                HVACCoolSetpoints[presetNumberToWrite - 1, rm.Value.ClimateID - 1] = rm.Value.CurrentCoolSetpoint;
                                CrestronConsole.PrintLine("{0} cool {1} heat {2}", rm.Value.Name, rm.Value.CurrentCoolSetpoint, rm.Value.CurrentHeatSetpoint);
                            }
                            else
                            {
                                checkedTemp[rm.Value.ClimateID - 1] = "0";
                                HVACZoneChecked[presetNumberToWrite - 1, rm.Value.ClimateID - 1] = 0;
                                HVACModes[presetNumberToWrite - 1, rm.Value.ClimateID - 1] = 0;
                                HVACSecondaryModes[presetNumberToWrite - 1, rm.Value.ClimateID - 1] = 0;
                                HVACHeatSetpoints[presetNumberToWrite - 1, rm.Value.ClimateID - 1] = 0;
                                HVACCoolSetpoints[presetNumberToWrite - 1, rm.Value.ClimateID - 1] = 0;
                            }
                            modesTemp[rm.Value.ClimateID - 1] = Convert.ToString(HVACModes[presetNumberToWrite - 1, rm.Value.ClimateID - 1]);
                            secondaryModesTemp[rm.Value.ClimateID - 1] = Convert.ToString(HVACSecondaryModes[presetNumberToWrite - 1, rm.Value.ClimateID - 1]);
                            heatSetptsTemp[rm.Value.ClimateID - 1] = Convert.ToString(HVACHeatSetpoints[presetNumberToWrite - 1, rm.Value.ClimateID - 1]);
                            coolSetptsTemp[rm.Value.ClimateID - 1] = Convert.ToString(HVACCoolSetpoints[presetNumberToWrite - 1, rm.Value.ClimateID - 1]);
                        }
                    }
                    string checkJoined, modesJoined, secondaryModesJoined, heatSetptsJoined, coolSetptsJoined;
                    checkJoined = String.Join(",", checkedTemp);
                    modesJoined = String.Join(",", modesTemp);
                    secondaryModesJoined = String.Join(",", secondaryModesTemp);
                    heatSetptsJoined = String.Join(",", heatSetptsTemp);
                    coolSetptsJoined = String.Join(",", coolSetptsTemp);
                    CrestronConsole.PrintLine(" modes {0}", modesJoined);
                    //update the modes/setpts
                    climatePreset.Attribute("checked").Value = checkJoined;
                    climatePreset.Attribute("modes").Value = modesJoined;
                    climatePreset.Attribute("secondarymodes").Value = secondaryModesJoined;
                    climatePreset.Attribute("heatsetpoints").Value = heatSetptsJoined;
                    climatePreset.Attribute("coolsetpoints").Value = coolSetptsJoined;
                }

                //write changes to file
                using (var writer = new XmlTextWriter(path, new UTF8Encoding(false)))
                {
                    writer.Formatting = Formatting.Indented;
                    loaded.Save(writer);
                }
                //pulse save success
                _parent.imageEISC.BooleanInput[(ushort)(231)].BoolValue = true;
                TimeSpan delay = TimeSpan.FromMilliseconds(10); // 0.01 seconds
                Thread.Sleep(delay);
                _parent.imageEISC.BooleanInput[(ushort)(231)].BoolValue = false;
            }
            else //pulse the error message
            {
                _parent.imageEISC.BooleanInput[(ushort)(232)].BoolValue = true;
                TimeSpan delay = TimeSpan.FromMilliseconds(10); // 0.01 seconds
                Thread.Sleep(delay);
                _parent.imageEISC.BooleanInput[(ushort)(232)].BoolValue = false;
                //display the message
                _parent.imageEISC.StringInput[3111].StringValue = errorMessage;
            }
            CrestronConsole.PrintLine("FINISHED WRITE TO XML {0}:{1}", DateTime.Now.Second, DateTime.Now.Millisecond);
        }
        public void SetQuickActionSubsystemVisibility()
        {
            //disable visibility on all subsystem buttons 
            for (ushort j = 0; j < 10; j++)
            {
                _parent.imageEISC.BooleanInput[(ushort)(j + 201)].BoolValue = false;//clear out all the visibility of subsystem buttons
            }
            //then enable appropriate ones
            for (ushort j = 0; j < NumberOfIncludedSubsystems[quickActionToRecallOrSave - 1]; j++)
            {
                ushort subsystemNumber = IncludedSubsystems[quickActionToRecallOrSave - 1, j];
                int index = Array.FindIndex(AvailableSubsystems, w => w == subsystemNumber);
                _parent.imageEISC.BooleanInput[(ushort)(index + 201)].BoolValue = true;
            }
        }

        public void SelectQuickActionToView()
        {
            if (quickActionToRecallOrSave > 0)
            {
                CrestronConsole.PrintLine("preparing to recall preset{0} number of subsystems ={1}", quickActionToRecallOrSave, NumberOfIncludedSubsystems[quickActionToRecallOrSave - 1]);
                _parent.imageEISC.UShortInput[104].UShortValue = NumberOfAvailableSubsystems;
                _parent.imageEISC.StringInput[3100].StringValue = PresetName[quickActionToRecallOrSave - 1];//send the current name
                SetQuickActionSubsystemVisibility(); //set the visibility status of the subsystem buttons in the list
                //next select the first subsystem in the list
                ushort firstsub = IncludedSubsystems[quickActionToRecallOrSave - 1, 0];
                ushort idx = (ushort)Array.FindIndex(AvailableSubsystems, w => w == firstsub);
                SelectQuickActionSubsystem((ushort)(idx + 1));
            }
        }
        public string CheckForAtLeastOneCheckbox(List<ushort> checkedSubsystems)
        {
            string errorMessage = string.Empty;


            if (checkedSubsystems.Count == 0)
            {
                errorMessage = "No subsystems are selected";
                return errorMessage;
            }
            else if (checkedSubsystems.Count > 0)
            {
                if (climateIsChecked)
                {
                    if (!climateCheckboxes.Any(b => b))
                    {
                        errorMessage = "No climate zones are selected";
                        return errorMessage;
                    }
                }
                if (musicIsChecked)
                {
                    if (!musicCheckboxes.Any(b => b))
                    {
                        errorMessage = "No music zones are selected";
                        return errorMessage;
                    }
                }
            }
            return errorMessage;
        }
        public void EnableSubpageReferences()
        {
            //disable all zones
            for (ushort i = 1; i <= 100; i++)
            {
                _parent.imageEISC.BooleanInput[(ushort)(i + 300)].BoolValue = false;//clear the subpage references
            }
            if (currentSubsysIsMusic)
            {
                foreach (var rm in _parent.manager.RoomZ)
                {
                    if (rm.Value.AudioID > 0)
                    {
                        //this should use the enable digitals so that if a zone (audioID) was skipped if doesn't screw everything up.
                        _parent.imageEISC.BooleanInput[(ushort)(rm.Value.AudioID + 300)].BoolValue = true;//enable the subpage ref for room name / current source / volume 
                    }
                }
                //select the checkboxes
                for (ushort i = 0; i < 100; i++)
                {
                    _parent.imageEISC.BooleanInput[(ushort)(i + 401)].BoolValue = musicCheckboxes[i];
                }
            }
            else if (currentSubsystemIsClimate)
            {
                foreach (var rm in _parent.manager.RoomZ)
                {
                    if (rm.Value.ClimateID > 0)
                    {
                        //this should use the enable digitals so that if a zone (audioID) was skipped if doesn't screw everything up.
                        _parent.imageEISC.BooleanInput[(ushort)(rm.Value.ClimateID + 300)].BoolValue = true;//enable the subpage ref for room name / current source / volume 
                    }
                }
                //select the checkboxes
                for (ushort i = 0; i < 100; i++)
                {
                    _parent.imageEISC.BooleanInput[(ushort)(i + 401)].BoolValue = climateCheckboxes[i];
                }
            }
        }
        public void SelectSubsystemCurrentStatusToSave(ushort buttonNumber)
        {
            currentSelectedSubsysNumber = AvailableSubsystems[buttonNumber - 1];
            currentSelectedSubsysName = _parent.manager.SubsystemZ[currentSelectedSubsysNumber].DisplayName;
            _parent.imageEISC.BooleanInput[233].BoolValue = false;//clear the climate subpage
            _parent.imageEISC.BooleanInput[234].BoolValue = false;//clear the music subpage
            //first clear out all the button feedback
            for (ushort i = 0; i < 10; i++) { _parent.imageEISC.BooleanInput[(ushort)(i + 211)].BoolValue = false; }
            //then highlight the right button
            _parent.imageEISC.BooleanInput[(ushort)(buttonNumber + 210)].BoolValue = true;
            if (currentSelectedSubsysName.ToUpper() == "AUDIO" || currentSelectedSubsysName.ToUpper() == "MUSIC")
            {
                _parent.imageEISC.BooleanInput[234].BoolValue = true;//show the music subpage
                currentSubsysIsMusic = true;
                currentSubsystemIsClimate = false;
                EnableSubpageReferences();
            }
            else if (currentSelectedSubsysName.ToUpper() == "CLIMATE" || currentSelectedSubsysName.ToUpper() == "HVAC")
            {
                _parent.imageEISC.BooleanInput[233].BoolValue = true;//show the climate subpage
                currentSubsysIsMusic = false;
                currentSubsystemIsClimate = true;
                EnableSubpageReferences();
            }
        }

        /// <summary>
        ///this function translates the button number pressed to the subsystem number, then displays the settings for that subsystem
        /// </summary>
        public void SelectQuickActionSubsystem(ushort buttonNumber)
        {
            if (buttonNumber > 0 && quickActionToRecallOrSave > 0)
            {
                //first clear out all the button feedback
                for (ushort i = 0; i < 10; i++) { _parent.imageEISC.BooleanInput[(ushort)(i + 211)].BoolValue = false; }
                //then highlight the right button
                _parent.imageEISC.BooleanInput[(ushort)(buttonNumber + 210)].BoolValue = true;
                ushort j = 0;
                currentSelectedSubsysNumber = AvailableSubsystems[buttonNumber - 1];
                currentSelectedSubsysName = _parent.manager.SubsystemZ[currentSelectedSubsysNumber].DisplayName;//change this to current subsystem
                CrestronConsole.PrintLine("{0}", currentSelectedSubsysName);
                //first clear out the old text
                for (ushort i = 0; i < 100; i++)
                {
                    _parent.imageEISC.StringInput[(ushort)(3151 + i)].StringValue = "";
                }
                if (currentSelectedSubsysName.ToUpper() == "AUDIO" || currentSelectedSubsysName.ToUpper() == "MUSIC")
                {

                    for (ushort i = 0; i < NumberOfMusicZones[quickActionToRecallOrSave - 1]; i++)
                    {
                        ushort src = Sources[quickActionToRecallOrSave - 1, i];
                        ushort zoneChecked = MusicZoneChecked[quickActionToRecallOrSave - 1, i];
                        string srcName;
                        if (zoneChecked > 0)
                        {
                            if (src > 0) { srcName = _parent.manager.MusicSourceZ[src].Name; }
                            else { srcName = "Off"; }

                            float vol = Volumes[quickActionToRecallOrSave - 1, i];
                            string volume = "0";
                            //go to the view and instead of going by source that is non zero to display the zones, use the checkbox
                            if (vol > 0) { volume = Math.Round(vol / 65535 * 100).ToString(); }//convert to %
                            string rm = _parent.manager.RoomZ.FirstOrDefault(x => x.Value.AudioID == (i + 1)).Value?.Name;//the xml src/vol list is ordered 0 to whatever by room number so 'i + 1' is the audio id
                            if (rm != null)
                            {
                                string status = string.Format(@"{0} = {1} volume = {2}%", rm, srcName, volume);
                                _parent.imageEISC.StringInput[(ushort)(3151 + j)].StringValue = status;
                                j++;
                            }
                        }
                    }

                }
                else if (currentSelectedSubsysName.ToUpper() == "LIGHTS" || currentSelectedSubsysName.ToUpper() == "LIGHTING")
                {
                    j = 0;

                    for (ushort i = 0; i < NumberOfLightZones[quickActionToRecallOrSave - 1]; i++)
                    {
                        ushort sceneNumber = LightScenes[quickActionToRecallOrSave - 1, i];
                        if (sceneNumber > 0)
                        {
                            string rm = _parent.manager.RoomZ.FirstOrDefault(x => x.Value.LightsID == (i + 1)).Value?.Name;
                            if (rm != null)
                            {
                                string status = string.Format(@"{0} = Scene {1}", rm, Convert.ToString(sceneNumber));
                                _parent.imageEISC.StringInput[(ushort)(3151 + j)].StringValue = status;
                                j++;
                            }
                        }
                    }
                }
                else if (currentSelectedSubsysName.ToUpper() == "SHADES" || currentSelectedSubsysName.ToUpper() == "DRAPES")
                {
                    j = 0;
                    for (ushort i = 0; i < NumberOfShadeZones[quickActionToRecallOrSave - 1]; i++)
                    {
                        ushort shadeLevel = ShadeLevels[quickActionToRecallOrSave - 1, i];
                        if (shadeLevel > 0)
                        {
                            string rm = _parent.manager.RoomZ.FirstOrDefault(x => x.Value.ShadesID == (i + 1)).Value?.Name;
                            if (rm != null)
                            {
                                string status = string.Format(@"{0} shades {1}% closed.", rm, Convert.ToString(shadeLevel));
                                _parent.imageEISC.StringInput[(ushort)(3151 + j)].StringValue = status;
                                j++;
                            }
                        }
                    }
                }
                else if (currentSelectedSubsysName.ToUpper() == "HVAC" || currentSelectedSubsysName.ToUpper() == "CLIMATE")
                {
                    j = 0;

                    for (ushort i = 0; i < NumberOfHVACZones[quickActionToRecallOrSave - 1]; i++)
                    {
                        ushort mode = HVACModes[quickActionToRecallOrSave - 1, i];
                        if (mode > 0 && mode < 4)
                        {
                            ushort heat = HVACHeatSetpoints[quickActionToRecallOrSave - 1, i];
                            ushort cool = HVACCoolSetpoints[quickActionToRecallOrSave - 1, i];
                            string rm = _parent.manager.RoomZ.FirstOrDefault(x => x.Value.ClimateID == (i + 1)).Value?.Name;
                            string ht = Convert.ToString(heat);
                            string cl = Convert.ToString(cool);
                            string status = "";
                            //1 = auto, 2 = heat, 3 = cool, 4 = Off
                            if (rm != null)
                            {
                                switch (mode)
                                {
                                    case (1):
                                        {
                                            status = string.Format(@"{0} Mode: Auto: Heat to {1} Cool to {2}", rm, heat, cool);
                                            break;
                                        }
                                    case (2): status = string.Format(@"{0} Heat to {1}", rm, ht); break;
                                    case (3): status = string.Format(@"{0} Cool to {1}", rm, cl); break;
                                    default: break;
                                }
                                _parent.imageEISC.StringInput[(ushort)(3151 + j)].StringValue = status;
                                j++;
                            }
                        }
                    }
                }

                _parent.imageEISC.UShortInput[103].UShortValue = j;//set number of zones
            }
        }
    }
}
