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

        // ========== 期货定价参数 (模型三：持有成本模型) ==========

        /// <summary>
        /// 无风险利率（每日），代表资金的时间价值
        /// 默认值：0.002 (每日 0.2%)
        /// 经济含义：今天卖出作物换成金币，可以立即买种子产生收益
        /// </summary>
        public double RiskFreeRate { get; set; } = 0.002;

        /// <summary>
        /// 仓储成本（每日），代表持有现货的腐败/损耗成本
        /// 默认值：0.005 (每日 0.5%)
        /// 经济含义：作物会随时间腐败，低品质作物尤其明显
        /// </summary>
        public double StorageCost { get; set; } = 0.005;

        /// <summary>
        /// 基础便利收益率（每日），代表持有现货的基础好处
        /// 默认值：0.001 (每日 0.1%)
        /// 经济含义：持有现货可以送礼、烹饪、完成任务
        /// 注：NPC生日、社区中心任务等事件会动态提升此值
        /// </summary>
        public double BaseConvenienceYield { get; set; } = 0.001;
    }
}
