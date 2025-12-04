// ============================================================================
// 星露谷资本 (Stardew Capital)
// 模块：SMAPI日志适配器
// 作者：Stardew Capital Team
// 用途：将SMAPI的IMonitor适配为独立计算器的LogCallback
// ============================================================================

using StardewCapital.Core.Futures.Models;
using StardewModdingAPI;

namespace StardewCapital.Services.Adapters
{
    /// <summary>
    /// SMAPI日志适配器
    /// 将StardewModdingAPI.IMonitor适配为Core.Models.LogCallback
    /// </summary>
    public static class SmapiLoggerAdapter
    {
        /// <summary>
        /// 创建日志回调
        /// </summary>
        /// <param name="monitor">SMAPI监视器</param>
        /// <returns>日志回调函数</returns>
        public static LogCallback CreateLogCallback(IMonitor monitor)
        {
            return (message, level) =>
            {
                var smapiLevel = ConvertLogLevel(level);
                monitor.Log(message, smapiLevel);
            };
        }
        
        /// <summary>
        /// 将简单日志级别转换为SMAPI日志级别
        /// </summary>
        private static LogLevel ConvertLogLevel(SimpleLogLevel level)
        {
            return level switch
            {
                SimpleLogLevel.Debug => LogLevel.Debug,
                SimpleLogLevel.Info => LogLevel.Info,
                SimpleLogLevel.Warn => LogLevel.Warn,
                SimpleLogLevel.Error => LogLevel.Error,
                _ => LogLevel.Info
            };
        }
    }
}

