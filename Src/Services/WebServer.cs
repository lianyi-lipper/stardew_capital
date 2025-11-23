using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using StardewCapital.Domain.Instruments;
using StardewModdingAPI;

namespace StardewCapital.Services
{
    /// <summary>
    /// Web服务器（可选功能）
    /// 提供HTTP服务器，允许通过浏览器查看市场数据和进行交易。
    /// 
    /// 功能：
    /// - REST API：提供市场行情、订单簿、持仓查询、下单等端点
    /// - 静态文件服务：提供HTML/JS/CSS文件
    /// - CORS支持：允许跨域访问
    /// 
    /// 端口：http://localhost:5000
    /// 
    /// 使用场景：
    /// - 在浏览器中查看K线图和订单簿
    /// - Web界面下单和管理持仓
    /// - 多屏幕显示（游戏+Web终端）
    /// </summary>
    public class WebServer
    {
        private readonly HttpListener _listener;
        private readonly IMonitor _monitor;
        private readonly MarketManager _marketManager;
        private readonly BrokerageService _brokerageService;
        private readonly string _webRoot;
        private bool _isRunning;

        public WebServer(IMonitor monitor, MarketManager marketManager, BrokerageService brokerageService, string modDirectory)
        {
            _monitor = monitor;
            _marketManager = marketManager;
            _brokerageService = brokerageService;
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
                _monitor.Log($"[WebServer] Request: {context.Request.HttpMethod} {path}", LogLevel.Trace);

                // 添加CORS头，允许跨域访问
                context.Response.Headers.Add("Access-Control-Allow-Origin", "*");
                context.Response.Headers.Add("Access-Control-Allow-Methods", "GET, POST, OPTIONS");
                context.Response.Headers.Add("Access-Control-Allow-Headers", "Content-Type");

                // 处理OPTIONS请求（CORS预检）
                if (context.Request.HttpMethod == "OPTIONS")
                {
                    context.Response.StatusCode = 200;
                    context.Response.Close();
                    return;
                }

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
            
            // GET /api/ticker - 获取市场行情
            if (path == "/api/ticker")
            {
                var instruments = _marketManager.GetInstruments();
                var sb = new StringBuilder();
                sb.Append("[");
                
                for (int i = 0; i < instruments.Count; i++)
                {
                    var inst = instruments[i];
                    sb.Append($"{{\"symbol\":\"{inst.Symbol}\",\"price\":{inst.CurrentPrice.ToString(System.Globalization.CultureInfo.InvariantCulture)},\"name\":\"{inst.Name}\"}}");
                    if (i < instruments.Count - 1) sb.Append(",");
                }
                
                sb.Append("]");
                jsonResponse = sb.ToString();
            }
            // GET /api/orderbook?symbol=XXX - 获取订单簿
            else if (path.StartsWith("/api/orderbook"))
            {
                var query = context.Request.Url?.Query;
                string symbol = "PARSNIP-SPR-28"; // 默认值
                
                if (!string.IsNullOrEmpty(query))
                {
                    var parts = query.TrimStart('?').Split('&');
                    foreach (var part in parts)
                    {
                        var kv = part.Split('=');
                        if (kv.Length == 2 && kv[0] == "symbol")
                            symbol = Uri.UnescapeDataString(kv[1]);
                    }
                }
                
                var orderBook = _marketManager.GetOrderBook(symbol);
                if (orderBook != null)
                {
                    var sb = new StringBuilder();
                    sb.Append("{");
                    
                    // 中间价
                    var midPrice = orderBook.GetMidPrice();
                    sb.Append($"\"midPrice\":{midPrice.ToString(System.Globalization.CultureInfo.InvariantCulture)},");
                    
                    // 卖盘 (Asks)
                    sb.Append("\"asks\":[");
                    var asks = orderBook.Asks.Take(10).ToList();
                    for (int i = 0; i < asks.Count; i++)
                    {
                        var order = asks[i];
                        sb.Append($"{{\"price\":{order.Price.ToString(System.Globalization.CultureInfo.InvariantCulture)},");
                        sb.Append($"\"quantity\":{order.RemainingQuantity},");
                        sb.Append($"\"isPlayerOrder\":{order.IsPlayerOrder.ToString().ToLower()}}}");
                        if (i < asks.Count - 1) sb.Append(",");
                    }
                    sb.Append("],");
                    
                    // 买盘 (Bids)
                    sb.Append("\"bids\":[");
                    var bids = orderBook.Bids.Take(10).ToList();
                    for (int i = 0; i < bids.Count; i++)
                    {
                        var order = bids[i];
                        sb.Append($"{{\"price\":{order.Price.ToString(System.Globalization.CultureInfo.InvariantCulture)},");
                        sb.Append($"\"quantity\":{order.RemainingQuantity},");
                        sb.Append($"\"isPlayerOrder\":{order.IsPlayerOrder.ToString().ToLower()}}}");
                        if (i < bids.Count - 1) sb.Append(",");
                    }
                    sb.Append("]");
                    
                    sb.Append("}");
                    jsonResponse = sb.ToString();
                }
            }
            // GET /api/positions - 获取持仓
            else if (path == "/api/positions")
            {
                var positions = _brokerageService.Account.Positions;
                var prices = GetCurrentPrices();
                
                var sb = new StringBuilder();
                sb.Append("[");
                
                for (int i = 0; i < positions.Count; i++)
                {
                    var pos = positions[i];
                    decimal unrealizedPnL = 0;
                    if (prices.TryGetValue(pos.Symbol, out decimal currentPrice))
                    {
                        unrealizedPnL = pos.GetUnrealizedPnL(currentPrice);
                    }
                    
                    sb.Append("{");
                    sb.Append($"\"symbol\":\"{pos.Symbol}\",");
                    sb.Append($"\"quantity\":{pos.Quantity},");
                    sb.Append($"\"averageCost\":{pos.AverageCost.ToString(System.Globalization.CultureInfo.InvariantCulture)},");
                    sb.Append($"\"unrealizedPnL\":{unrealizedPnL.ToString(System.Globalization.CultureInfo.InvariantCulture)}");
                    sb.Append("}");
                    if (i < positions.Count - 1) sb.Append(",");
                }
                
                sb.Append("]");
                jsonResponse = sb.ToString();
            }
            // POST /api/order/market - 市价单
            else if (path == "/api/order/market" && context.Request.HttpMethod == "POST")
            {
                try
                {
                    using (var reader = new StreamReader(context.Request.InputStream))
                    {
                        var body = reader.ReadToEnd();
                        var symbol = ExtractJsonValue(body, "symbol");
                        var quantity = int.Parse(ExtractJsonValue(body, "quantity"));
                        var leverage = int.Parse(ExtractJsonValue(body, "leverage"));
                        
                        _brokerageService.ExecuteOrder(symbol, quantity, leverage);
                        jsonResponse = "{\"success\":true}";
                    }
                }
                catch (Exception ex)
                {
                    jsonResponse = $"{{\"success\":false,\"error\":\"{ex.Message}\"}}";
                }
            }
            // POST /api/order/limit - 限价单
            else if (path == "/api/order/limit" && context.Request.HttpMethod == "POST")
            {
                try
                {
                    using (var reader = new StreamReader(context.Request.InputStream))
                    {
                        var body = reader.ReadToEnd();
                        var symbol = ExtractJsonValue(body, "symbol");
                        var isBuy = bool.Parse(ExtractJsonValue(body, "isBuy"));
                        var price = decimal.Parse(ExtractJsonValue(body, "price"), System.Globalization.CultureInfo.InvariantCulture);
                        var quantity = int.Parse(ExtractJsonValue(body, "quantity"));
                        var leverage = int.Parse(ExtractJsonValue(body, "leverage"));
                        
                        var orderId = _brokerageService.PlaceLimitOrder(symbol, isBuy, price, quantity, leverage);
                        jsonResponse = $"{{\"success\":true,\"orderId\":\"{orderId}\"}}";
                    }
                }
                catch (Exception ex)
                {
                    jsonResponse = $"{{\"success\":false,\"error\":\"{ex.Message}\"}}";
                }
            }
            // POST /api/positions/closeall - 平仓所有
            else if (path == "/api/positions/closeall" && context.Request.HttpMethod == "POST")
            {
                try
                {
                    using (var reader = new StreamReader(context.Request.InputStream))
                    {
                        var body = reader.ReadToEnd();
                        var symbol = ExtractJsonValue(body, "symbol");
                        
                        // 查找该symbol的持仓并平仓
                        var pos = _brokerageService.Account.Positions.FirstOrDefault(p => p.Symbol == symbol);
                        if (pos != null)
                        {
                            // 平仓 = 反向开单
                            _brokerageService.ExecuteOrder(symbol, -pos.Quantity, pos.Leverage);
                            jsonResponse = "{\"success\":true}";
                        }
                        else
                        {
                            jsonResponse = "{\"success\":false,\"error\":\"No position found\"}";
                        }
                    }
                }
                catch (Exception ex)
                {
                    jsonResponse = $"{{\"success\":false,\"error\":\"{ex.Message}\"}}";
                }
            }

            // 发送JSON响应
            byte[] buffer = Encoding.UTF8.GetBytes(jsonResponse);
            context.Response.ContentType = "application/json";
            context.Response.ContentLength64 = buffer.Length;
            context.Response.OutputStream.Write(buffer, 0, buffer.Length);
        }
        
        /// <summary>
        /// 简单的JSON值提取（仅用于基本类型）
        /// WARNING: 生产环境应使用proper JSON库
        /// </summary>
        private string ExtractJsonValue(string json, string key)
        {
            var pattern = $"\"{key}\":";
            var start = json.IndexOf(pattern);
            if (start == -1) return "";
            
            start += pattern.Length;
            while (start < json.Length && (json[start] == ' ' || json[start] == '\"')) start++;
            
            var end = start;
            while (end < json.Length && json[end] != ',' && json[end] != '}' && json[end] != '\"') end++;
            
            return json.Substring(start, end - start);
        }
        
        /// <summary>
        /// 获取当前所有产品的市场价格
        /// </summary>
        private Dictionary<string, decimal> GetCurrentPrices()
        {
            var dict = new Dictionary<string, decimal>();
            foreach (var inst in _marketManager.GetInstruments())
            {
                var futures = inst as CommodityFutures;
                decimal price = futures != null ? (decimal)futures.FuturesPrice : (decimal)inst.CurrentPrice;
                dict[inst.Symbol] = price;
            }
            return dict;
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
