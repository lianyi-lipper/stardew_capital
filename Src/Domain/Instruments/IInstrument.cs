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
        /// Unique symbol, e.g., "PARSNIP-SPR-28".
        /// </summary>
        string Symbol { get; }

        /// <summary>
        /// Display name, e.g., "Parsnip Futures (Spring 28)".
        /// </summary>
        string Name { get; }

        /// <summary>
        /// The underlying item ID in Stardew Valley (e.g., "24" for Parsnip).
        /// </summary>
        string UnderlyingItemId { get; }

        /// <summary>
        /// Current market price per unit.
        /// </summary>
        double CurrentPrice { get; set; }

        /// <summary>
        /// Margin requirement ratio (0.0 to 1.0).
        /// Futures might be 0.1 (10%), Stocks 1.0 (100%).
        /// </summary>
        double MarginRatio { get; }
    }
}
