using StardewCapital.Core.Time;
using StardewValley;

namespace StardewCapital.Services.Infrastructure
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
                // 将星露谷时间（600 到 2600）转换为 0.0 - 1.0 的范围
                // 注意：星露谷时间并非线性的。我们需要先将其转换为分钟。
                // 600 代表早上 6:00，650 代表早上 6:50，700 代表早上 7:00（而不是 6:50 之后的 7 小时）。
                // 这个表示法实际上是用前一两位数代表小时，后两位数代表分钟。
                // 但分钟只到 59，下一个时间段就会跳到新的小时，比如 659 之后会变成 700。
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
        /// 辅助方法：将Stardew时间格式（HHMM）转换为从午夜（00:00）开始的线性总分钟数。
        /// </summary>
        /// <param name="time">Stardew时间（例：1350代表13:50）</param>
        /// <returns>从午夜0点开始的总分钟数</returns>
        private int ToMinutes(int time)
        {
            return (time / 100) * 60 + (time % 100);
        }
    }
}
