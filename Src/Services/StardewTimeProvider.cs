using StardewCapital.Core.Time;
using StardewValley;

namespace StardewCapital.Services
{
    /// <summary>
    /// 星露谷时间提供者
    /// 作为Stardew Valley游戏循环与Core时间系统之间的桥梁。
    /// 
    /// 职责：
    /// - 从Game1获取游戏时间并转换为标准化格式
    /// - 将Stardew的时间格式（600-2600）转换为分钟和归一化比例
    /// - 检测游戏暂停状态（菜单打开或游戏暂停）
    /// </summary>
    public class StardewTimeProvider : IGameTimeProvider
    {
        private readonly ModConfig _config;

        public StardewTimeProvider(ModConfig config)
        {
            _config = config;
        }

        public int CurrentTimeOfDay => Game1.timeOfDay;

        public double TimeRatio
        {
            get
            {
                // Convert Stardew time (600 to 2600) to 0.0 - 1.0 range
                // Note: Stardew time is not linear (650 -> 700). We need to convert to minutes first.
                int minutes = ToMinutes(Game1.timeOfDay);
                int startMinutes = ToMinutes(_config.OpeningTime);
                int endMinutes = ToMinutes(_config.ClosingTime);
                
                double totalDuration = endMinutes - startMinutes;
                double elapsed = minutes - startMinutes;

                return System.Math.Clamp(elapsed / totalDuration, 0.0, 1.0);
            }
        }

        public bool IsPaused => Game1.paused || Game1.activeClickableMenu != null;

        public int TotalMinutesToday => ToMinutes(Game1.timeOfDay);

        /// <summary>
        /// 辅助方法：将Stardew时间格式转换为总分钟数
        /// Stardew时间格式：600 = 6:00, 630 = 6:30, 700 = 7:00
        /// </summary>
        /// <param name="time">Stardew时间（例：1350代表13:50）</param>
        /// <returns>从午夜0点开始的总分钟数</returns>
        private int ToMinutes(int time)
        {
            return (time / 100) * 60 + (time % 100);
        }
    }
}
