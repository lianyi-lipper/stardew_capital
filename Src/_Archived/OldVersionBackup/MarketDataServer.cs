// MarketDataServer.cs

using StardewModdingAPI;
using System;
using System.Collections.Concurrent;
using WebSocketSharp;
using WebSocketSharp.Server;

namespace StardewCapital
{
    public class MarketDataServer
    {
        private readonly IMonitor _monitor;
        private WebSocketServer _wssv;
        public readonly ConcurrentQueue<string> CommandQueue = new ConcurrentQueue<string>();

        public MarketDataServer(IMonitor monitor)
        {
            this._monitor = monitor;
        }

        public void Start()
        {
            try
            {
                _wssv = new WebSocketServer("ws://localhost:8080");
                _wssv.AddWebSocketService<MarketDataEndpoint>("/", () => new MarketDataEndpoint(this._monitor, this.CommandQueue));
                _wssv.Start();
                _monitor.Log("WebSocket server started on ws://localhost:8080", StardewModdingAPI.LogLevel.Info);
            }
            catch (Exception ex)
            {
                _monitor.Log($"Failed to start WebSocket server: {ex.Message}", StardewModdingAPI.LogLevel.Error);
            }
        }

        public void Stop()
        {
            if (_wssv != null && _wssv.IsListening)
            {
                _wssv.Stop();
                _monitor.Log("WebSocket server stopped.", StardewModdingAPI.LogLevel.Info);
            }
        }

        public void Broadcast(string data)
        {
            if (_wssv != null && _wssv.IsListening)
            {
                _wssv.WebSocketServices["/"].Sessions.Broadcast(data);
            }
        }
    }

    public class MarketDataEndpoint : WebSocketBehavior
    {
        private readonly IMonitor _monitor;
        private readonly ConcurrentQueue<string> _commandQueue;

        public MarketDataEndpoint(IMonitor monitor, ConcurrentQueue<string> commandQueue)
        {
            this._monitor = monitor;
            this._commandQueue = commandQueue;
        }

        protected override void OnOpen()
        {
            _monitor.Log("WebSocket client connected.", StardewModdingAPI.LogLevel.Info);
        }

        protected override void OnMessage(MessageEventArgs e)
        {
            _monitor.Log($"Received command from client: {e.Data}", StardewModdingAPI.LogLevel.Info);
            _commandQueue.Enqueue(e.Data);
        }

        protected override void OnClose(CloseEventArgs e)
        {
            _monitor.Log("WebSocket client disconnected.", StardewModdingAPI.LogLevel.Info);
        }

        protected override void OnError(ErrorEventArgs e)
        {
            _monitor.Log($"WebSocket error: {e.Message}", StardewModdingAPI.LogLevel.Error);
        }
    }
}
