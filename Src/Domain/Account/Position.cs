using System;

namespace StardewCapital.Domain.Account
{
    /// <summary>
    /// 交易仓位
    /// 代表玩家对某个金融产品的持仓，支持多头和空头交易。
    /// 
    /// 核心概念：
    /// - Quantity > 0：多头仓位（看涨，价格上涨时盈利）
    /// - Quantity < 0：空头仓位（看跌，价格下跌时盈利）
    /// - Leverage：杠杆倍数，决定保证金占用 = |价值| / Leverage
    /// </summary>
    public class Position
    {
        /// <summary>合约代码（例：PARSNIP-SPR-28）</summary>
        public string Symbol { get; set; }
        
        /// <summary>
        /// 持仓数量
        /// 正数：多头仓位（Long）
        /// 负数：空头仓位（Short）
        /// </summary>
        public int Quantity { get; set; }
        
        /// <summary>平均成本价（建仓时的平均价格）</summary>
        public decimal AverageCost { get; set; }
        
        /// <summary>杠杆倍数（1x, 5x, 10x 等）</summary>
        public int Leverage { get; set; }

        /// <summary>
        /// 创建交易仓位
        /// </summary>
        /// <param name="symbol">合约代码</param>
        /// <param name="quantity">持仓数量（正数=多头，负数=空头）</param>
        /// <param name="averageCost">平均成本价</param>
        /// <param name="leverage">杠杆倍数</param>
        public Position(string symbol, int quantity, decimal averageCost, int leverage)
        {
            Symbol = symbol;
            Quantity = quantity;
            AverageCost = averageCost;
            Leverage = leverage;
        }

        /// <summary>
        /// 计算仓位的市值（按当前价格计算）
        /// </summary>
        /// <param name="currentPrice">当前市场价格</param>
        /// <returns>仓位总市值（正负取决于多空方向）</returns>
        public decimal GetMarketValue(decimal currentPrice)
        {
            return Quantity * currentPrice;
        }

        /// <summary>
        /// 计算未实现盈亏（PnL = Profit and Loss）
        /// </summary>
        /// <param name="currentPrice">当前市场价格</param>
        /// <returns>
        /// 未实现盈亏
        /// - 多头：(currentPrice - averageCost) * quantity
        /// - 空头：(averageCost - currentPrice) * |quantity| = (currentPrice - averageCost) * quantity (因为quantity为负)
        /// </returns>
        public decimal GetUnrealizedPnL(decimal currentPrice)
        {
            return (currentPrice - AverageCost) * Quantity;
        }

        /// <summary>
        /// 已占用保证金（基于开仓成本）
        /// </summary>
        public decimal UsedMargin => (AverageCost * Math.Abs(Quantity)) / Leverage;

        /// <summary>
        /// 计算已占用的保证金（基于当前价格 - 维持保证金）
        /// </summary>
        /// <param name="currentPrice">当前市场价格</param>
        /// <returns>已占用保证金 = |市值| / 杠杆倍数</returns>
        public decimal GetMarginUsed(decimal currentPrice)
        {
            // 保证金 = |市值| / 杠杆倍数
            // 例：10000元市值，10倍杠杆，需褁1000元保证金
            return Math.Abs(GetMarketValue(currentPrice)) / Leverage;
        }
    }
}
