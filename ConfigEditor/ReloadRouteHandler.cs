using System;
using Crestron.SimplSharp;
using Crestron.SimplSharp.WebScripting;

namespace ACS_4Series_Template_V3.ConfigEditor
{
    /// <summary>
    /// Handles POST requests for /api/configeditor/reload
    /// Triggers the program to re-read the config from NVRAM.
    /// </summary>
    public class ReloadRouteHandler : IHttpCwsHandler
    {
        private readonly ControlSystem _cs;

        public ReloadRouteHandler(ControlSystem cs)
        {
            _cs = cs;
        }

        public void ProcessRequest(HttpCwsContext context)
        {
            if (context.Request.HttpMethod != "POST")
            {
                context.Response.StatusCode = 405;
                context.Response.ContentType = "application/json";
                context.Response.Write("{\"error\":\"Method not allowed. Use POST.\"}", true);
                return;
            }

            try
            {
                CrestronConsole.PrintLine("[ConfigEditor] Reload requested via API");

                // Call the same initialization that reloadjson console command uses
                _cs.InitializeSystem();

                context.Response.StatusCode = 200;
                context.Response.ContentType = "application/json";
                context.Response.Write("{\"ok\":true,\"message\":\"Config reloaded\"}", true);
            }
            catch (Exception ex)
            {
                ErrorLog.Error("[ConfigEditor] Reload failed: {0}", ex.Message);
                context.Response.StatusCode = 500;
                context.Response.ContentType = "application/json";
                context.Response.Write(
                    string.Format("{{\"error\":\"Reload failed: {0}\"}}", ex.Message), true);
            }
        }
    }
}
