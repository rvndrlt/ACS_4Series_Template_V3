using System;
using System.Collections.Generic;
using Crestron.SimplSharp;
using Crestron.SimplSharp.CrestronIO;
using Crestron.SimplSharp.WebScripting;

namespace ACS_4Series_Template_V3.ConfigEditor
{
    /// <summary>
    /// Serves static files (HTML, JS, CSS) for the Config Editor frontend.
    /// Reads files from /html/configeditor/ on the processor filesystem.
    /// </summary>
    public class StaticFileHandler : IHttpCwsHandler
    {
        private readonly string _fileName;
        private readonly string _contentType;
        private static readonly string BasePath = @"\html\configeditor";

        private static readonly Dictionary<string, string> MimeTypes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { ".html", "text/html; charset=utf-8" },
            { ".js", "application/javascript; charset=utf-8" },
            { ".css", "text/css; charset=utf-8" },
            { ".json", "application/json; charset=utf-8" }
        };

        public StaticFileHandler(string fileName)
        {
            _fileName = fileName;
            string ext = Path.GetExtension(fileName);
            _contentType = MimeTypes.ContainsKey(ext) ? MimeTypes[ext] : "application/octet-stream";
        }

        public void ProcessRequest(HttpCwsContext context)
        {
            if (context.Request.HttpMethod != "GET")
            {
                context.Response.StatusCode = 405;
                context.Response.Write("{\"error\":\"Method not allowed\"}", true);
                return;
            }

            try
            {
                string filePath = Path.Combine(BasePath, _fileName);

                if (!File.Exists(filePath))
                {
                    context.Response.StatusCode = 404;
                    context.Response.ContentType = "text/plain";
                    context.Response.Write("File not found: " + _fileName, true);
                    return;
                }

                string content;
                using (var reader = new StreamReader(filePath))
                {
                    content = reader.ReadToEnd();
                }

                context.Response.StatusCode = 200;
                context.Response.ContentType = _contentType;
                context.Response.AppendHeader("Cache-Control", "no-cache");
                context.Response.Write(content, true);
            }
            catch (Exception ex)
            {
                ErrorLog.Error("[ConfigEditor] Static file error for {0}: {1}", _fileName, ex.Message);
                context.Response.StatusCode = 500;
                context.Response.Write("Internal error", true);
            }
        }
    }
}
