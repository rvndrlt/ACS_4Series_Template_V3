using System;
using Crestron.SimplSharp;
using Crestron.SimplSharp.WebScripting;

namespace ACS_4Series_Template_V3.ConfigEditor
{
    /// <summary>
    /// Registers the HttpCwsServer for the Config Editor API.
    /// Call ConfigEditorServer.Start() from ControlSystem after InitializeSystem.
    /// </summary>
    public class ConfigEditorServer
    {
        private HttpCwsServer _server;
        private readonly ControlSystem _cs;

        public ConfigEditorServer(ControlSystem cs)
        {
            _cs = cs;
        }

        public void Start()
        {
            try
            {
                _server = new HttpCwsServer("/api/configeditor");

                // GET & POST /api/configeditor/config
                var configRoute = new HttpCwsRoute("config");
                configRoute.RouteHandler = new ConfigRouteHandler(_cs);
                _server.Routes.Add(configRoute);

                // POST /api/configeditor/reload
                var reloadRoute = new HttpCwsRoute("reload");
                reloadRoute.RouteHandler = new ReloadRouteHandler(_cs);
                _server.Routes.Add(reloadRoute);

                // GET /api/configeditor/files
                var filesRoute = new HttpCwsRoute("files");
                filesRoute.RouteHandler = new FilesRouteHandler(_cs);
                _server.Routes.Add(filesRoute);

                // Static file routes for the frontend UI
                string[] staticFiles = new string[] { "index.html", "app.js", "api.js", "sections.js", "validate.js", "diff.js", "style.css" };
                foreach (string file in staticFiles)
                {
                    var route = new HttpCwsRoute(file);
                    route.RouteHandler = new StaticFileHandler(file);
                    _server.Routes.Add(route);
                }

                _server.Register();
                CrestronConsole.PrintLine("[ConfigEditor] HTTP API registered at /cws/api/configeditor");
            }
            catch (Exception ex)
            {
                ErrorLog.Error("[ConfigEditor] Failed to start server: {0}", ex.Message);
            }
        }

        public void Stop()
        {
            if (_server != null)
            {
                _server.Unregister();
                _server = null;
                CrestronConsole.PrintLine("[ConfigEditor] HTTP API unregistered");
            }
        }
    }
}
