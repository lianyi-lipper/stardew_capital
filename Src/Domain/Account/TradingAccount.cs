using System;
using System.Collections.Generic;
using System.Linq;

namespace StardewCapital.Domain.Account
{
    /// <summary>
    /// 交易账户
    /// 管理玩家的资金和所有交易仓位，支持保证金计算和风险管理。
    /// 
    /// 账户结构：
    /// - Cash：账户现金余额
    /// - Positions：所有开仓的交易仓位
    /// - Equity：总资产 = Cash + 未实现盈亏
    /// - Free Margin：可用保证金 = Equity - Used Margin
    /// </summary>
    public class TradingAccount
    {
        /// <summary>账户现金余额（金币）</summary>
        public decimal Cash { get; set; }
        
        /// <summary>所有开仓的交易仓位列表</summary>
        public List<Position> Positions { get; private set; }

        public TradingAccount()
        {
            Cash = 0;
            Positions = new List<Position>();
        }

        /// <summary>
        /// 存入资金到交易账户
        /// </summary>
        /// <param name="amount">存入金额（必须大于0）</param>
        public void Deposit(decimal amount)
        {
            if (amount <= 0) throw new ArgumentException("Deposit amount must be positive.");
            Cash += amount;
        }

        /// <summary>
        /// 从交易账户提取资金
        /// </summary>
        /// <param name="amount">提取金额（必须大于0，且不超过可用保证金）</param>
        public void Withdraw(decimal amount)
        {
            if (amount <= 0) throw new ArgumentException("Withdraw amount must be positive.");
            if (amount > GetFreeMargin()) throw new InvalidOperationException("Insufficient free margin.");
            Cash -= amount;
        }

        /// <summary>
        /// 计算账户总资产（净值）
        /// </summary>
        /// <param name="currentPrices">当前市场价格字典（Symbol -> Price）</param>
        /// <returns>总资产 = 现金 + 所有仓位的未实现盈亏</returns>
        public decimal GetTotalEquity(Dictionary<string, decimal> currentPrices)
        {
            decimal unrealizedPnL = 0;
            foreach (var pos in Positions)
            {
                if (currentPrices.TryGetValue(pos.Symbol, out decimal price))
                {
                    unrealizedPnL += pos.GetUnrealizedPnL(price);
                }
            }
            return Cash + unrealizedPnL;
        }

        /// <summary>
        /// 计算已占用的保证金
        /// </summary>
        /// <param name="currentPrices">当前市场价格字典（Symbol -> Price）</param>
        /// <returns>所有仓位占用的总保证金</returns>
        public decimal GetUsedMargin(Dictionary<string, decimal> currentPrices)
        {
            decimal usedMargin = 0;
            foreach (var pos in Positions)
            {
                if (currentPrices.TryGetValue(pos.Symbol, out decimal price))
                {
                    // TODO: 确认保证金计算基准
                    // - 初始保证金（Initial Margin）：基于开仓价格
                    // - 维持保证金（Maintenance Margin）：基于当前价格
                    // 当前实现：使用当前价格计算（Mark-to-Market）
                    usedMargin += pos.GetMarginUsed(price);
                }
            }
            return usedMargin;
        }

        /// <summary>
        /// 计算可用保证金（可以用来开新仓或提取的资金）
        /// </summary>
        /// <param name="currentPrices">当前市场价格字典（可为null）</param>
        /// <returns>可用保证金 = 总资产 - 已占用保证金</returns>
        public decimal GetFreeMargin(Dictionary<string, decimal>? currentPrices = null)
        {
            // 如果没有价格数据，近似返回现金
            if (currentPrices == null) return Cash;
            return GetTotalEquity(currentPrices) - GetUsedMargin(currentPrices);
        }

        /// <summary>
        /// 加载存档数据到账户
        /// </summary>
        /// <param name="cash">现金余额</param>
        /// <param name="positions">仓位列表</param>
        public void Load(decimal cash, List<Position> positions)
        {
            Cash = cash;
            Positions = positions ?? new List<Position>();
        }
    }
}
