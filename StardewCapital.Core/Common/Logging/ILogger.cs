namespace StardewCapital.Core.Common.Logging
{
    /// <summary>
    /// 日志级别枚举
    /// </summary>
    public enum LogLevel
    {
        Trace,
        Debug,
        Info,
        Warn,
        Error
    }

    /// <summary>
    /// 通用日志接口
    /// 用于替代 SMAPI 的 IMonitor，实现计算核心与游戏框架的解耦
    /// </summary>
    public interface ILogger
    {
        /// <summary>
        /// 记录日志消息
        /// </summary>
        /// <param name="message">日志消息</param>
        /// <param name="level">日志级别</param>
        void Log(string message, LogLevel level = LogLevel.Info);
    }
}

