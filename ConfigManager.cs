//-----------------------------------------------------------------------
// <copyright file="ConfigManager.cs" company="Crestron">
//     Copyright (c) Crestron Electronics. All rights reserved.
// </copyright>
//-----------------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Crestron.SimplSharp;
using Crestron.SimplSharp.CrestronIO;
using Newtonsoft.Json;

namespace ACS_4Series_Template_V3.Configuration
{
    /// <summary>
    /// Reads/Writes data from config.json
    /// </summary>
    public class ConfigManager
    {
        /// <summary>
        /// Configuration object for this system
        /// </summary>
        public ConfigData.Configuration RoomConfig;

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
        /// Initializes a new instance of the ConfigManager class
        /// </summary>
        public ConfigManager()
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

                        this.RoomConfig = JsonConvert.DeserializeObject<ConfigData.Configuration>(configData);
                   
                    CrestronConsole.PrintLine("ReadConfig file loaded! {0}", this.RoomConfig.SubSystemScenarios[0].IncludedSubsystems[0]);
                    
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
        // Add to ConfigManager.cs
        /// <summary>
        /// Finds the latest configuration file based on naming convention
        /// Looks for files with "acsconfig" in the name and selects the highest version
        /// </summary>
        /// <param name="directory">Directory to search in</param>
        /// <returns>Full path of the latest config file, or null if none found</returns>
        public string FindLatestConfigFile(string directory = @"\nvram\")
        {
            try
            {
                // Check if directory exists
                if (!Directory.Exists(directory))
                {
                    ErrorLog.Error(LogHeader + "Directory doesn't exist: " + directory);
                    return null;
                }

                // Get all JSON files in the directory
                string[] files = Directory.GetFiles(directory, "*.json");

                // Filter files containing "acsconfig"
                List<string> configFiles = new List<string>();
                foreach (string file in files)
                {
                    string fileName = Path.GetFileName(file);
                    if (fileName.ToLower().Contains("acsconfig"))
                    {
                        configFiles.Add(file);
                        CrestronConsole.PrintLine("Found config file: {0}", fileName);
                    }
                }

                if (configFiles.Count == 0)
                {
                    CrestronConsole.PrintLine("No configuration files found with 'acsconfig' in the name");
                    return null;
                }
                else if (configFiles.Count == 1)
                {
                    // Only one file found, return it
                    CrestronConsole.PrintLine("Using config file: {0}", Path.GetFileName(configFiles[0]));
                    return configFiles[0];
                }

                // Multiple files found, check for version numbers
                Dictionary<string, int> fileVersions = new Dictionary<string, int>();
                Regex versionRegex = new Regex(@"[vV](\d+)|[-](\d+)", RegexOptions.Compiled);

                foreach (string file in configFiles)
                {
                    string fileName = Path.GetFileName(file);
                    Match match = versionRegex.Match(fileName);

                    int version = 0;
                    if (match.Success)
                    {
                        // Version could be in group 1 (v01) or group 2 (-01)
                        string versionStr = match.Groups[1].Value;
                        if (string.IsNullOrEmpty(versionStr))
                            versionStr = match.Groups[2].Value;

                        int.TryParse(versionStr, out version);
                    }

                    fileVersions[file] = version;
                    CrestronConsole.PrintLine("File: {0}, Version: {1}", fileName, version);
                }

                // Get the file with highest version number
                string latestFile = fileVersions.OrderByDescending(f => f.Value).First().Key;
                CrestronConsole.PrintLine("Using latest config file: {0}, Version: {1}",
                    Path.GetFileName(latestFile), fileVersions[latestFile]);

                return latestFile;
            }
            catch (Exception e)
            {
                ErrorLog.Error(LogHeader + "Exception finding config file: {0}", e.Message);
                return null;
            }
        }
        /// <summary>
        /// Update a running configuration
        /// Most likely to happen through the API
        /// </summary>
        /// <param name="roomConfig">New config file location and file name</param>
        public void UpdateConfiguration(Configuration.ConfigData.Configuration roomConfig)
        {
            this.appId = InitialParametersClass.ApplicationNumber;

            string filePath = string.Empty;

            // Add current date and time to config file
            // Not used at this point, for future use
            roomConfig.LastUpdate = DateTime.Now.ToString();

            string json = JsonConvert.SerializeObject(roomConfig, Formatting.Indented);

            // check which platfrom we are running on
            if (CrestronEnvironment.DevicePlatform == eDevicePlatform.Appliance)
            {
                //filePath = string.Format(@"\User\App{0:D2}\config.json", this.appId);
                filePath = string.Format(@"\NVRAM\Config.json");
            }
            else if (CrestronEnvironment.DevicePlatform == eDevicePlatform.Server)
            {
                filePath = string.Format(@"{0}/User/config.json", Directory.GetApplicationRootDirectory());
            }

            using (var streamToWrite = new FileStream(filePath, FileMode.OpenOrCreate))
            {
                using (var writer = new StreamWriter(streamToWrite))
                {
                    writer.Write(json);
                    CrestronConsole.PrintLine("file {0}", filePath);
                }
            }
        }
    }
}