using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using HedgeHarvest.Domain.Instruments;
using StardewModdingAPI;

namespace HedgeHarvest.Services
{
    public class WebServer
    {
        private readonly HttpListener _listener;
        private readonly IMonitor _monitor;
        private readonly MarketManager _marketManager;
        private readonly string _webRoot;
        private bool _isRunning;

        public WebServer(IMonitor monitor, MarketManager marketManager, string modDirectory)
        {
            _monitor = monitor;
            _marketManager = marketManager;
            _webRoot = Path.Combine(modDirectory, "Assets", "Web");
            _listener = new HttpListener();
            _listener.Prefixes.Add("http://localhost:5000/");
        }

        public void Start()
        {
            try
            {
                _listener.Start();
                _isRunning = true;
                _monitor.Log("[WebServer] Started at http://localhost:5000/", LogLevel.Info);
                Task.Run(ListenLoop);
            }
            catch (Exception ex)
            {
                _monitor.Log($"[WebServer] Failed to start: {ex.Message}", LogLevel.Error);
            }
        }

        public void Stop()
        {
            _isRunning = false;
            _listener.Stop();
            _listener.Close();
        }

        private async Task ListenLoop()
        {
            while (_isRunning && _listener.IsListening)
            {
                try
                {
                    var context = await _listener.GetContextAsync();
                    // Handle request on a background thread to avoid blocking the listener loop
                    _ = Task.Run(() => ProcessRequest(context));
                }
                catch (HttpListenerException)
                {
                    // Listener stopped
                }
                catch (Exception ex)
                {
                    _monitor.Log($"[WebServer] Error accepting request: {ex.Message}", LogLevel.Error);
                }
            }
        }

        private void ProcessRequest(HttpListenerContext context)
{
    try
    {
        // 1. 强制显示请求日志
        string path = context.Request.Url?.AbsolutePath.ToLower() ?? "/";
        _monitor.Log($"[WebServer] -> Incoming Request: {path}", LogLevel.Info); 

        context.Response.Headers.Add("Access-Control-Allow-Origin", "*");

        if (path.StartsWith("/api/"))
        {
            HandleApiRequest(context, path);
            // 2. 确认 API 处理完成
            _monitor.Log($"[WebServer] API Handled: {path}", LogLevel.Info);
        }
        else
        {
            HandleStaticFileRequest(context, path);
        }
    }
    catch (Exception ex)
    {
        // 3. 确保错误能被看见
        _monitor.Log($"[WebServer] Error: {ex}", LogLevel.Error);
        context.Response.StatusCode = 500;
    }
    finally
    {
        context.Response.Close();
    }
}

        private void HandleApiRequest(HttpListenerContext context, string path)
        {
            string jsonResponse = "{}";
            
            if (path == "/api/ticker")
            {
                var instruments = _marketManager.GetInstruments();
                var data = new List<object>();
                
                var sb = new StringBuilder();
                sb.Append("[");
                for(int i=0; i<instruments.Count; i++)
                {
                    var inst = instruments[i];
                    // Use InvariantCulture to ensure dot separator for decimals
                    sb.Append($"{{\"symbol\":\"{inst.Symbol}\",\"price\":{inst.CurrentPrice.ToString(System.Globalization.CultureInfo.InvariantCulture)},\"name\":\"{inst.Name}\"}}");
                    if (i < instruments.Count - 1) sb.Append(",");
                }
                sb.Append("]");
                jsonResponse = sb.ToString();
            }

            byte[] buffer = Encoding.UTF8.GetBytes(jsonResponse);
            context.Response.ContentType = "application/json";
            context.Response.ContentLength64 = buffer.Length;
            context.Response.OutputStream.Write(buffer, 0, buffer.Length);
        }

        private void HandleStaticFileRequest(HttpListenerContext context, string path)
        {
            if (path == "/") path = "/index.html";
            
            string filePath = Path.Combine(_webRoot, path.TrimStart('/'));
            
            if (File.Exists(filePath))
            {
                byte[] buffer = File.ReadAllBytes(filePath);
                context.Response.ContentType = GetContentType(Path.GetExtension(filePath));
                context.Response.ContentLength64 = buffer.Length;
                context.Response.OutputStream.Write(buffer, 0, buffer.Length);
            }
            else
            {
                context.Response.StatusCode = 404;
            }
        }

        private string GetContentType(string extension)
        {
            return extension switch
            {
                ".html" => "text/html",
                ".js" => "application/javascript",
                ".css" => "text/css",
                ".json" => "application/json",
                _ => "application/octet-stream"
            };
        }
    }
}
