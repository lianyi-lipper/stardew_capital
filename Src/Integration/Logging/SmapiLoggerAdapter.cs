using StardewModdingAPI;
using CoreLogging = StardewCapital.Core.Common.Logging;

namespace StardewCapital.Integration.Logging
{
    /// <summary>
    /// SMAPI 日志适配器
    /// 将通用 ILogger 接口适配到 SMAPI 的 IMonitor
    /// </summary>
    public class SmapiLoggerAdapter : CoreLogging.ILogger
    {
        private readonly IMonitor _monitor;

        public SmapiLoggerAdapter(IMonitor monitor)
        {
            _monitor = monitor;
        }

        public void Log(string message, CoreLogging.LogLevel level = CoreLogging.LogLevel.Info)
        {
            var smapiLevel = level switch
            {
                CoreLogging.LogLevel.Trace => LogLevel.Trace,
                CoreLogging.LogLevel.Debug => LogLevel.Debug,
                CoreLogging.LogLevel.Warn => LogLevel.Warn,
                CoreLogging.LogLevel.Error => LogLevel.Error,
                _ => LogLevel.Info
            };

            _monitor.Log(message, smapiLevel);
        }
    }
}
