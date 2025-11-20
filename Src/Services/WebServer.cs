using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using StardewCapital.Domain.Instruments;
using StardewModdingAPI;

namespace StardewCapital.Services
{
    /// <summary>
    /// Web服务器（可选功能）
    /// 提供HTTP服务器，允许通过浏览器查看市场数据。
    /// 
    /// 功能：
    /// - REST API：提供 /api/ticker 端点获取实时价格
    /// - 静态文件服务：提供HTML/JS/CSS文件
    /// - CORS支持：允许跨域访问
    /// 
    /// 端口：http://localhost:5000
    /// 
    /// 使用场景：
    /// - 在浏览器中查看K线图
    /// - 使用外部工具接入市场数据
    /// - 多屏幕显示（游戏+Web图表）
    /// </summary>
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

        /// <summary>
        /// 启动Web服务器
        /// 在后台线程中运行，不阻塞游戏主线程
        /// </summary>
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

        /// <summary>
        /// 停止Web服务器
        /// 在Mod卸载时调用
        /// </summary>
        public void Stop()
        {
            _isRunning = false;
            _listener.Stop();
            _listener.Close();
        }

        /// <summary>
        /// 主监听循环
        /// 异步接收HTTP请求并转发到独立线程处理
        /// </summary>
        private async Task ListenLoop()
        {
            while (_isRunning && _listener.IsListening)
            {
                try
                {
                    var context = await _listener.GetContextAsync();
                    
                    // 在后台线程处理请求，避免阻塞监听循环
                    _ = Task.Run(() => ProcessRequest(context));
                }
                catch (HttpListenerException)
                {
                    // 监听器已停止，正常退出
                }
                catch (Exception ex)
                {
                    _monitor.Log($"[WebServer] Error accepting request: {ex.Message}", LogLevel.Error);
                }
            }
        }

        /// <summary>
        /// 处理单个HTTP请求
        /// </summary>
        /// <param name="context">HTTP上下文</param>
        private void ProcessRequest(HttpListenerContext context)
        {
            try
            {
                string path = context.Request.Url?.AbsolutePath.ToLower() ?? "/";
                _monitor.Log($"[WebServer] Request: {path}", LogLevel.Trace);

                // 添加CORS头，允许跨域访问
                context.Response.Headers.Add("Access-Control-Allow-Origin", "*");

                if (path.StartsWith("/api/"))
                {
                    HandleApiRequest(context, path);
                }
                else
                {
                    HandleStaticFileRequest(context, path);
                }
            }
            catch (Exception ex)
            {
                _monitor.Log($"[WebServer] Error processing request: {ex.Message}", LogLevel.Error);
                context.Response.StatusCode = 500;
            }
            finally
            {
                context.Response.Close();
            }
        }

        /// <summary>
        /// 处理API请求
        /// </summary>
        /// <param name="context">HTTP上下文</param>
        /// <param name="path">请求路径</param>
        private void HandleApiRequest(HttpListenerContext context, string path)
        {
            string jsonResponse = "{}";
            
            if (path == "/api/ticker")
            {
                // 构建市场数据JSON
                var instruments = _marketManager.GetInstruments();
                var sb = new StringBuilder();
                sb.Append("[");
                
                for (int i = 0; i < instruments.Count; i++)
                {
                    var inst = instruments[i];
                    // 使用InvariantCulture确保小数点格式正确（避免逗号分隔符）
                    sb.Append($"{{\"symbol\":\"{inst.Symbol}\",\"price\":{inst.CurrentPrice.ToString(System.Globalization.CultureInfo.InvariantCulture)},\"name\":\"{inst.Name}\"}}");
                    if (i < instruments.Count - 1) sb.Append(",");
                }
                
                sb.Append("]");
                jsonResponse = sb.ToString();
            }

            // 发送JSON响应
            byte[] buffer = Encoding.UTF8.GetBytes(jsonResponse);
            context.Response.ContentType = "application/json";
            context.Response.ContentLength64 = buffer.Length;
            context.Response.OutputStream.Write(buffer, 0, buffer.Length);
        }

        /// <summary>
        /// 处理静态文件请求
        /// 从 Assets/Web 目录提供HTML、JS、CSS等文件
        /// </summary>
        /// <param name="context">HTTP上下文</param>
        /// <param name="path">请求路径</param>
        private void HandleStaticFileRequest(HttpListenerContext context, string path)
        {
            // 默认首页
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

        /// <summary>
        /// 根据文件扩展名获取MIME类型
        /// </summary>
        /// <param name="extension">文件扩展名（包含点）</param>
        /// <returns>MIME类型字符串</returns>
        private string GetContentType(string extension)
        {
            return extension switch
            {
                ".html" => "text/html",
                ".js" => "application/javascript",
                ".css" => "text/css",
                ".json" => "application/json",
                ".png" => "image/png",
                ".jpg" => "image/jpeg",
                ".gif" => "image/gif",
                _ => "application/octet-stream"
            };
        }
    }
}
