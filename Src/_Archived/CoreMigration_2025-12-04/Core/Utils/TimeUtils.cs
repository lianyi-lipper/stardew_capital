// ============================================================================
// 星露谷资本 (Stardew Capital)
// 模块：时间转换工具类
// 作者：Stardew Capital Team
// 用途：提供不依赖游戏API的时间格式转换功能
// ============================================================================

namespace StardewCapital.Core.Utils
{
    /// <summary>
    /// 时间转换工具类（完全无游戏依赖）
    /// 提供Stardew Valley时间格式与标准分钟数之间的转换
    /// </summary>
    public static class TimeUtils
    {
        /// <summary>
        /// 将Stardew Valley时间格式（HHMM）转换为从午夜开始的总分钟数
        /// </summary>
        /// <param name="time">Stardew时间（例：600 代表 6:00，1350 代表 13:50）</param>
        /// <returns>从午夜0点开始的总分钟数</returns>
        /// <remarks>
        /// Stardew Valley的时间格式是HHMM：
        /// - 前1-2位代表小时
        /// - 后2位代表分钟（0-59）
        /// - 例如：600 = 6:00 = 360分钟，1350 = 13:50 = 830分钟
        /// </remarks>
        /// <example>
        /// <code>
        /// TimeUtils.ToMinutes(600);   // 返回 360  (6:00 = 6*60)
        /// TimeUtils.ToMinutes(1350);  // 返回 830  (13:50 = 13*60 + 50)
        /// TimeUtils.ToMinutes(2600);  // 返回 1560 (26:00 = 26*60)
        /// </code>
        /// </example>
        public static int ToMinutes(int time)
        {
            int hours = time / 100;
            int minutes = time % 100;
            return hours * 60 + minutes;
        }
        
        /// <summary>
        /// 计算一天的时间步数
        /// </summary>
        /// <param name="openingTime">开盘时间（HHMM格式）</param>
        /// <param name="closingTime">收盘时间（HHMM格式）</param>
        /// <param name="minutesPerStep">每步的分钟数（默认10分钟）</param>
        /// <returns>时间步数（至少为2）</returns>
        /// <example>
        /// <code>
        /// // 6:00 到 26:00，每10分钟一步
        /// int steps = TimeUtils.CalculateStepsPerDay(600, 2600, 10);
        /// // 返回 72 步（1200分钟 / 10 = 120步）
        /// </code>
        /// </example>
        public static int CalculateStepsPerDay(int openingTime, int closingTime, int minutesPerStep = 10)
        {
            int startMinutes = ToMinutes(openingTime);
            int endMinutes = ToMinutes(closingTime);
            int totalMinutes = endMinutes - startMinutes;
            
            int steps = totalMinutes / minutesPerStep;
            
            // 确保至少有2个步长（开盘和收盘）
            return steps < 2 ? 2 : steps;
        }
    }
}
