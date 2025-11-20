namespace StardewCapital.Core.Time
{
    /// <summary>
    /// 时间常量定义
    /// 定义市场开放时间、一天的时长等关键时间参数。
    /// </summary>
    public static class TimeConstants
    {
        /// <summary>开盘时间：早上6点 (Stardew Valley 时间格式：600)</summary>
        public const int OpeningTime = 600;
        
        /// <summary>收盘时间：次日凌晨2点 (Stardew Valley 时间格式：2600)</summary>
        public const int ClosingTime = 2600;
        
        /// <summary>
        /// Stardew Valley 一天的总分钟数
        /// 从早上6点到次日凌晨2点 = 20小时 = 1200分钟
        /// </summary>
        public const int MinutesPerDay = 20 * 60;

        /// <summary>
        /// 市场更新的真实时间间隔（秒）
        /// 控制价格刷新频率，避免过于频繁的更新
        /// </summary>
        public const double RealTimeTickInterval = 0.7;
    }
}
