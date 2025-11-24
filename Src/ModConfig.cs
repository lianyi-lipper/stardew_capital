namespace StardewCapital
{
    /// <summary>
    /// Mod配置类
    /// 定义用户可配置的参数，通过config.json文件进行设置。
    /// 
    /// 配置项：
    /// - OpeningTime：市场开盘时间
    /// - ClosingTime：市场收盘时间
    /// 
    /// 未来可扩展：
    /// - 价格波动率参数
    /// - 更新频率设置
    /// - 初始资金配置
    /// </summary>
    public class ModConfig
    {
        /// <summary>
        /// 市场开盘时间（Stardew Valley时间格式）
        /// 默认值：600（早上6:00）
        /// </summary>
        public int OpeningTime { get; set; } = 600;

        /// <summary>
        /// 市场收盘时间（Stardew Valley时间格式）
        /// 默认值：2600（次日凌晨2:00）
        /// 注：Stardew的一天从600开始，到2600结束（跨越到次日）
        /// </summary>
        public int ClosingTime { get; set; } = 2600;

    }
}
