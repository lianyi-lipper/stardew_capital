using System;
using StardewCapital.Core.Common.Time;
using StardewValley;

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
        /// 获取交易日的当前归一化时间进度（0.0到1.0）
        /// </summary>
        public double GetDayProgress()
        {
            return _timeProvider.TimeRatio;
        }

        /// <summary>
        /// 计算当日剩余时间（T - t），归一化后的值
        /// </summary>
        public double GetTimeRemaining()
        {
            return 1.0 - _timeProvider.TimeRatio;
        }

        /// <summary>
        /// 检查市场当前是否开放
        /// </summary>
        public bool IsMarketOpen()
        {
            int time = _timeProvider.CurrentTimeOfDay;
            return time >= _config.OpeningTime && time < _config.ClosingTime;
        }

        /// <summary>
        /// 检查游戏时间是否暂停
        /// 特殊处理：TradingMenu打开时，时间不应被视为暂停
        /// </summary>
        public bool IsPaused()
        {
            // 如果TradingMenu打开，时间不暂停
            if (Game1.activeClickableMenu != null && 
                Game1.activeClickableMenu.GetType().Name == "TradingMenu")
            {
                return false;
            }
            
            return _timeProvider.IsPaused;
        }
    }
}

