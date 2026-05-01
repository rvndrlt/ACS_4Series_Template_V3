using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Crestron.SimplSharp;
using Crestron.SimplSharp.CrestronIO;
using Crestron.SimplSharp.WebScripting;
using Newtonsoft.Json;

namespace ACS_4Series_Template_V3.ConfigEditor
{
    /// <summary>
    /// Handles GET requests for /api/configeditor/files
    /// Returns a list of all acsconfig files in NVRAM with their version numbers.
    /// Useful for the frontend to show backup history.
    /// </summary>
    public class FilesRouteHandler : IHttpCwsHandler
    {
        private readonly ControlSystem _cs;

        public FilesRouteHandler(ControlSystem cs)
        {
            _cs = cs;
        }

        public void ProcessRequest(HttpCwsContext context)
        {
            if (context.Request.HttpMethod != "GET")
            {
                context.Response.StatusCode = 405;
                context.Response.ContentType = "application/json";
                context.Response.Write("{\"error\":\"Method not allowed. Use GET.\"}", true);
                return;
            }

            try
            {
                string[] files = Directory.GetFiles(@"\nvram\", "*.json");
                var result = new List<object>();
                var versionRegex = new Regex(@"[vV](\d+)");

                foreach (string file in files)
                {
                    string fileName = Path.GetFileName(file);
                    if (!fileName.ToLower().Contains("acsconfig")) continue;

                    int version = 0;
                    var match = versionRegex.Match(fileName);
                    if (match.Success)
                        int.TryParse(match.Groups[1].Value, out version);

                    var fileInfo = new FileInfo(file);
                    result.Add(new
                    {
                        name = fileName,
                        version = version,
                        size = fileInfo.Length,
                        modified = fileInfo.LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss")
                    });
                }

                context.Response.StatusCode = 200;
                context.Response.ContentType = "application/json";
                context.Response.Write(
                    JsonConvert.SerializeObject(new { files = result }), true);
            }
            catch (Exception ex)
            {
                ErrorLog.Error("[ConfigEditor] Files list failed: {0}", ex.Message);
                context.Response.StatusCode = 500;
                context.Response.ContentType = "application/json";
                context.Response.Write(
                    string.Format("{{\"error\":\"{0}\"}}", ex.Message), true);
            }
        }
    }
}
