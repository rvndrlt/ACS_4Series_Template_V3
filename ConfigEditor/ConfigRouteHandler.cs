using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Crestron.SimplSharp;
using Crestron.SimplSharp.CrestronIO;
using Crestron.SimplSharp.WebScripting;
using Newtonsoft.Json;

namespace ACS_4Series_Template_V3.ConfigEditor
{
    /// <summary>
    /// Handles GET and POST requests for /api/configeditor/config
    /// GET: Returns the current config JSON from NVRAM (highest version)
    /// POST: Writes new config with incremented version, keeps last 10 backups
    /// </summary>
    public class ConfigRouteHandler : IHttpCwsHandler
    {
        private readonly ControlSystem _cs;
        private const string NvramDir = @"\nvram\";
        private const int MaxBackups = 10;

        public ConfigRouteHandler(ControlSystem cs)
        {
            _cs = cs;
        }

        public void ProcessRequest(HttpCwsContext context)
        {
            try
            {
                if (context.Request.HttpMethod == "GET")
                {
                    HandleGet(context);
                }
                else if (context.Request.HttpMethod == "POST")
                {
                    HandlePost(context);
                }
                else
                {
                    context.Response.StatusCode = 405;
                    context.Response.ContentType = "application/json";
                    context.Response.Write("{\"error\":\"Method not allowed\"}", true);
                }
            }
            catch (Exception ex)
            {
                ErrorLog.Error("[ConfigEditor] Error in ConfigRouteHandler: {0}", ex.Message);
                context.Response.StatusCode = 500;
                context.Response.ContentType = "application/json";
                context.Response.Write(
                    JsonConvert.SerializeObject(new { error = ex.Message }), true);
            }
        }

        private void HandleGet(HttpCwsContext context)
        {
            string configPath = FindLatestConfigFile();

            if (string.IsNullOrEmpty(configPath) || !File.Exists(configPath))
            {
                context.Response.StatusCode = 404;
                context.Response.ContentType = "application/json";
                context.Response.Write("{\"error\":\"No config file found in NVRAM\"}", true);
                return;
            }

            string json;
            using (var reader = new StreamReader(configPath))
            {
                json = reader.ReadToEnd();
            }

            context.Response.StatusCode = 200;
            context.Response.ContentType = "application/json";
            context.Response.AppendHeader("X-Config-File", Path.GetFileName(configPath));
            context.Response.Write(json, true);
        }

        private void HandlePost(HttpCwsContext context)
        {
            // Read request body
            string body;
            using (var reader = new StreamReader(context.Request.InputStream))
            {
                body = reader.ReadToEnd();
            }

            if (string.IsNullOrEmpty(body))
            {
                context.Response.StatusCode = 400;
                context.Response.ContentType = "application/json";
                context.Response.Write("{\"error\":\"Empty request body\"}", true);
                return;
            }

            // Validate JSON
            try
            {
                JsonConvert.DeserializeObject(body);
            }
            catch (JsonReaderException ex)
            {
                context.Response.StatusCode = 400;
                context.Response.ContentType = "application/json";
                context.Response.Write(
                    JsonConvert.SerializeObject(new { error = "Invalid JSON: " + ex.Message }), true);
                return;
            }

            // Find current version number
            int currentVersion = GetCurrentVersion();
            int newVersion = currentVersion + 1;
            string newFileName = string.Format("ACSconfig-HTML-V{0}.json", newVersion);
            string newFilePath = NvramDir + newFileName;

            // Write new config file
            using (var stream = new FileStream(newFilePath, FileMode.Create))
            {
                using (var writer = new StreamWriter(stream))
                {
                    writer.Write(body);
                }
            }

            CrestronConsole.PrintLine("[ConfigEditor] Saved config as {0}", newFileName);

            // Cleanup old backups (keep last 10)
            CleanupOldBackups();

            context.Response.StatusCode = 200;
            context.Response.ContentType = "application/json";
            context.Response.Write(
                JsonConvert.SerializeObject(new { ok = true, file = newFileName, version = newVersion }), true);
        }

        /// <summary>
        /// Finds the latest acsconfig file in NVRAM (highest version number)
        /// </summary>
        private string FindLatestConfigFile()
        {
            var files = GetConfigFiles();
            if (files.Count == 0) return null;

            return files.OrderByDescending(f => f.Value).First().Key;
        }

        /// <summary>
        /// Gets current highest version number
        /// </summary>
        private int GetCurrentVersion()
        {
            var files = GetConfigFiles();
            if (files.Count == 0) return 0;

            return files.Values.Max();
        }

        /// <summary>
        /// Gets all acsconfig files with their version numbers
        /// </summary>
        private Dictionary<string, int> GetConfigFiles()
        {
            var result = new Dictionary<string, int>();

            if (!Directory.Exists(NvramDir)) return result;

            string[] files = Directory.GetFiles(NvramDir, "*.json");
            Regex versionRegex = new Regex(@"[vV](\d+)", RegexOptions.Compiled);

            foreach (string file in files)
            {
                string fileName = Path.GetFileName(file);
                if (!fileName.ToLower().Contains("acsconfig")) continue;

                int version = 0;
                Match match = versionRegex.Match(fileName);
                if (match.Success)
                {
                    int.TryParse(match.Groups[1].Value, out version);
                }

                result[file] = version;
            }

            return result;
        }

        /// <summary>
        /// Keeps only the MaxBackups most recent config files, deletes older ones
        /// </summary>
        private void CleanupOldBackups()
        {
            var files = GetConfigFiles();
            if (files.Count <= MaxBackups) return;

            var toDelete = files.OrderByDescending(f => f.Value)
                                .Skip(MaxBackups)
                                .Select(f => f.Key)
                                .ToList();

            foreach (string file in toDelete)
            {
                try
                {
                    File.Delete(file);
                    CrestronConsole.PrintLine("[ConfigEditor] Deleted old backup: {0}", Path.GetFileName(file));
                }
                catch (Exception ex)
                {
                    ErrorLog.Error("[ConfigEditor] Failed to delete {0}: {1}", file, ex.Message);
                }
            }
        }
    }
}
