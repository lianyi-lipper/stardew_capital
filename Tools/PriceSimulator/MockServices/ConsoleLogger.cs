using System;
using StardewCapital.Core.Common.Logging;

namespace StardewCapital.Simulator.MockServices
{
    /// <summary>
    /// 控制台日志实现
    /// 用于 SimulatorConsole 的日志输出
    /// </summary>
    public class ConsoleLogger : ILogger
    {
        private readonly bool _enableTrace;

        public ConsoleLogger(bool enableTrace = false)
        {
            _enableTrace = enableTrace;
        }

        public void Log(string message, LogLevel level = LogLevel.Info)
        {
            // 跳过 Trace 级别日志（除非显式启用）
            if (level == LogLevel.Trace && !_enableTrace)
                return;

            var color = level switch
            {
                LogLevel.Error => ConsoleColor.Red,
                LogLevel.Warn => ConsoleColor.Yellow,
                LogLevel.Debug => ConsoleColor.Cyan,
                LogLevel.Trace => ConsoleColor.DarkGray,
                _ => ConsoleColor.White
            };

            var prefix = level switch
            {
                LogLevel.Error => "[错误]",
                LogLevel.Warn => "[警告]",
                LogLevel.Debug => "[调试]",
                LogLevel.Trace => "[追踪]",
                _ => "[信息]"
            };

            Console.ForegroundColor = color;
            Console.WriteLine($"{prefix} {message}");
            Console.ResetColor();
        }
    }
}

