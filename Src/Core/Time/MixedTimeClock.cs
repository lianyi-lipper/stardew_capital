using System;

namespace StardewCapital.Core.Time
{
    /// <summary>
    /// 混合时间时钟 - 市场模拟的心脏
    /// 处理真实时间（控制更新频率）与游戏时间（控制价格趋势）之间的转换。
    /// 
    /// 核心功能：
    /// - 提供归一化的时间进度 (0.0 - 1.0)
    /// - 判断市场开放/关闭状态
    /// - 检测游戏暂停状态
    /// </summary>
    public class MixedTimeClock
    {
        private readonly IGameTimeProvider _timeProvider;
        private readonly ModConfig _config;

        public MixedTimeClock(IGameTimeProvider timeProvider, ModConfig config)
        {
            _timeProvider = timeProvider;
            _config = config;
        }

        /// <summary>
        /// Gets the current normalized time progress (0.0 to 1.0) for the trading day.
        /// </summary>
        public double GetDayProgress()
        {
            return _timeProvider.TimeRatio;
        }

        /// <summary>
        /// Calculates the remaining time (T - t) for the day, normalized.
        /// </summary>
        public double GetTimeRemaining()
        {
            return 1.0 - _timeProvider.TimeRatio;
        }

        /// <summary>
        /// Checks if the market is currently open.
        /// </summary>
        public bool IsMarketOpen()
        {
            int time = _timeProvider.CurrentTimeOfDay;
            return time >= _config.OpeningTime && time < _config.ClosingTime;
        }

        /// <summary>
        /// Checks if the game time is paused.
        /// </summary>
        public bool IsPaused()
        {
            return _timeProvider.IsPaused;
        }
    }
}
