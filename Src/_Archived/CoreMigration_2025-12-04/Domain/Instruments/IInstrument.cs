namespace StardewCapital.Domain.Instruments
{
    /// <summary>
    /// 可交易资产接口
    /// 代表市场中的任何可交易金融产品（期货、股票、期权等）。
    /// 
    /// 设计理念：
    /// 采用面向接口编程，所有交易系统（订单执行、持仓管理、价格引擎）都基于此接口工作。
    /// 这使得未来添加新的金融产品（如股票、期权、债券）时，无需修改核心交易逻辑。
    /// 
    /// 扩展示例：
    /// - CommodityFutures（已实现）：农产品期货合约
    /// - Stock（未来）：公司股票
    /// - Option（未来）：看涨/看跌期权
    /// </summary>
    public interface IInstrument
    {
        /// <summary>
        /// 唯一标识符，例如："PARSNIP-SPR-28"
        /// </summary>
        string Symbol { get; }

        /// <summary>
        /// 显示名称，例如："防风草期货 (春季28号)"
        /// </summary>
        string Name { get; }

        /// <summary>
        /// 标的物品在星露谷中的ID（例如："24"代表防风草）
        /// </summary>
        string UnderlyingItemId { get; }

        /// <summary>
        /// 当前市场价格（每单位）
        /// </summary>
        double CurrentPrice { get; set; }

        /// <summary>
        /// 保证金要求比例（0.0到1.0）
        /// 期货可能是0.1（10%），股票是1.0（100%）
        /// </summary>
        double MarginRatio { get; }
    }
}
