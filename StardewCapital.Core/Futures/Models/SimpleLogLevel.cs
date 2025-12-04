// ============================================================================
// 星露谷资本 (Stardew Capital)
// 模块：简单日志级别枚举
// 作者：Stardew Capital Team
// 用途：为独立计算器提供日志级别定义（不依赖SMAPI）
// ============================================================================

namespace StardewCapital.Core.Futures.Models
{
    /// <summary>
    /// 简化的日志级别枚举
    /// 用于独立计算器的日志回调，不依赖StardewModdingAPI.LogLevel
    /// </summary>
    public enum SimpleLogLevel
    {
        /// <summary>调试信息</summary>
        Debug,
        
        /// <summary>一般信息</summary>
        Info,
        
        /// <summary>警告信息</summary>
        Warn,
        
        /// <summary>错误信息</summary>
        Error
    }
    
    /// <summary>
    /// 日志回调委托
    /// </summary>
    /// <param name="message">日志消息</param>
    /// <param name="level">日志级别</param>
    public delegate void LogCallback(string message, SimpleLogLevel level);
}

