//-----------------------------------------------------------------------
// <copyright file="ConfigManager.cs" company="Crestron">
//     Copyright (c) Crestron Electronics. All rights reserved.
// </copyright>
//-----------------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Crestron.SimplSharp;
using Crestron.SimplSharp.CrestronIO;
using Newtonsoft.Json;

namespace ACS_4Series_Template_V1.QuickConfiguration
{
    /// <summary>
    /// Reads/Writes data from config.json
    /// </summary>
    public class QuickConfigManager
    {
        /// <summary>
        /// Configuration object for this system
        /// </summary>
        public QuickActionConfigData.QuickConfiguration QuickConfig;

        /// <summary>
        /// Used for logging information to error log
        /// </summary>
        private const string LogHeader = "[Configuration] ";

        /// <summary>
        /// Locking object for config
        /// </summary>
        private static CCriticalSection configLock = new CCriticalSection();

        /// <summary>
        /// Was the read succesfull
        /// </summary>
        private bool readSuccess;

        /// <summary>
        /// Can be used to identify individual program.
        /// Mostly usefull on an appliance with multiple programs running
        /// Not used in this exercise
        /// </summary>
        private uint appId;

        /// <summary>
        /// Initializes a new instance of the QUICKConfigManager class
        /// </summary>
        public QuickConfigManager()
        {
        }

        /// <summary>
        /// Reads a JSON formatted configuration from disc
        /// </summary>
        /// <param name="configFile">Location and name of the config file</param>
        /// <returns>True or False depending on read success</returns>
        public bool ReadConfig(string configFile, bool readACSConfig)
        {
            // string for file contents
            string configData = string.Empty;

            ErrorLog.Notice(LogHeader + "Started loading config file: {0}", configFile);
            if (string.IsNullOrEmpty(configFile))
            {
                this.readSuccess = false;
                ErrorLog.Error(LogHeader + "No File?!?");
            }

            if (!File.Exists(configFile))
            {
                this.readSuccess = false;
                ErrorLog.Error(LogHeader + "Config file doesn't exist");
            }
            else if (File.Exists(configFile))
            {
                configLock.Enter();

                // Open, read and close the file
                using (StreamReader file = new StreamReader(configFile))
                {
                    configData = file.ReadToEnd();
                    file.Close();
                }

                try
                {
                    // Try to deserialize into a Room object. If this fails, the JSON file is probably malformed

                    this.QuickConfig = JsonConvert.DeserializeObject<QuickActionConfigData.QuickConfiguration>(configData.ToString());
                    
                    CrestronConsole.PrintLine("ReadQuickConfig file loaded!");
                    CrestronConsole.PrintLine("!==={0}", configData.ToString());

                    this.readSuccess = true;
                }
                catch (Exception e)
                {
                    this.readSuccess = false;
                    ErrorLog.Error(LogHeader + "Exception in reading config file: {0}", e.Message);
                }
                finally
                {
                    configLock.Leave();
                }
            }

            return this.readSuccess;
        }

        /// <summary>
        /// Update a running configuration
        /// Most likely to happen through the API
        /// </summary>
        /// <param name="QuickConfig">New config file location and file name</param>
        public void UpdateConfiguration(QuickConfiguration.QuickActionConfigData.QuickConfiguration QuickConfig)
        {
            this.appId = InitialParametersClass.ApplicationNumber;

            string filePath = string.Empty;

            // Add current date and time to config file
            // Not used at this point, for future use
            //roomConfig.LastUpdate = DateTime.Now.ToString();
            CrestronConsole.PrintLine("^^^^{0}", QuickConfig.MusicPresets[0].MusicPresetName);
            CrestronConsole.PrintLine("^^^^{0}", QuickConfig.MusicPresets[1].MusicPresetName);
            CrestronConsole.PrintLine("^^^^{0}", QuickConfig.MusicPresets[2].MusicPresetName);
            string json = JsonConvert.SerializeObject(QuickConfig, Formatting.Indented);
            
            // check which platfrom we are running on
            if (CrestronEnvironment.DevicePlatform == eDevicePlatform.Appliance)
            {
                //filePath = string.Format(@"\User\App{0:D2}\config.json", this.appId);
                filePath = string.Format(@"\NVRAM\quickActionConfig.json");
            }
            else if (CrestronEnvironment.DevicePlatform == eDevicePlatform.Server)
            {
                filePath = string.Format(@"{0}/User/config.json", Directory.GetApplicationRootDirectory());
            }


            //FileMode.Create overwrites the existing file so make sure that what we are writing is not null or errored
            //TO DO check for null
            using (var streamToWrite = new FileStream(filePath, FileMode.Create))
            {
                using (TextWriter writer = new StreamWriter(streamToWrite))
                {
                    
                    CrestronConsole.PrintLine("@=={0}==", json.ToString());
                    //string newJson = json;
                    //if (json.EndsWith("}")) {
                    //    CrestronConsole.PrintLine("last chacter {0} {1}", json[json.Length - 1], json.Length - 1);
                    //    newJson = json.Remove(json.Length - 1);
                    //    CrestronConsole.PrintLine("new last chacter {0} {1}", newJson[newJson.Length - 1], newJson.Length - 1);
                    //}
                    writer.Write(json);

                    CrestronConsole.PrintLine("file {0}", filePath);
                }
            }
        }
    }
}