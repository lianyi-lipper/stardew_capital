using System;
using System.Collections.Generic;
using System.Linq;

namespace StardewCapital.Core.Futures.Domain.Account
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
        
        /// <summary>
        /// 冻结保证金（限价单占用的保证金）
        /// 当玩家下限价单时，需要预先冻结保证金，成交后转为仓位保证金，撤单后释放
        /// </summary>
        public decimal FrozenMargin { get; private set; }
        
        /// <summary>所有开仓的交易仓位列表</summary>
        public List<Position> Positions { get; private set; }

        public TradingAccount()
        {
            Cash = 0;
            FrozenMargin = 0;
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
        /// 已占用总保证金（基于开仓成本）
        /// </summary>
        public decimal UsedMargin => Positions.Sum(p => p.UsedMargin);

        /// <summary>
        /// 计算账户总资产（净值）
        /// </summary>
        /// <param name="currentPrices">当前市场价格字典（Symbol -> Price）</param>
        /// <returns>总资产 = 现金 + 所有仓位的未实现盈亏</returns>
        public decimal GetTotalEquity(Dictionary<string, decimal> currentPrices)
        {
            return Cash + GetTotalUnrealizedPnL(currentPrices);
        }

        /// <summary>
        /// 获取账户权益 (Equity) - GetTotalEquity 的别名
        /// </summary>
        public decimal GetEquity(Dictionary<string, decimal> currentPrices)
        {
            return GetTotalEquity(currentPrices);
        }

        /// <summary>
        /// 获取账户总未实现盈亏
        /// </summary>
        public decimal GetTotalUnrealizedPnL(Dictionary<string, decimal> currentPrices)
        {
            decimal unrealizedPnL = 0;
            foreach (var pos in Positions)
            {
                if (currentPrices.TryGetValue(pos.Symbol, out decimal price))
                {
                    unrealizedPnL += pos.GetUnrealizedPnL(price);
                }
            }
            return unrealizedPnL;
        }

        /// <summary>
        /// 计算已占用的保证金（基于当前价格 - 维持保证金）
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
                    usedMargin += pos.GetMarginUsed(price);
                }
            }
            return usedMargin;
        }

        /// <summary>
        /// 计算可用保证金（可以用来开新仓或提取的资金）
        /// </summary>
        /// <param name="currentPrices">当前市场价格字典（可为null）</param>
        /// <returns>可用保证金 = 总资产 - 已占用保证金 - 冻结保证金</returns>
        /// <remarks>
        /// WHY（为什么要扣除冻结保证金）:
        /// 限价单虽然未成交，但已承诺在特定价格成交，必须预留保证金。
        /// 否则玩家可能挂单后资金不足，导致成交失败。
        /// </remarks>
        public decimal GetFreeMargin(Dictionary<string, decimal>? currentPrices = null)
        {
            // 如果没有价格数据，近似返回现金 - 冻结保证金
            if (currentPrices == null) return Cash - FrozenMargin;
            
            // 可用保证金 = 权益 - 已用保证金(基于成本) - 冻结保证金
            // 注意：这里使用 UsedMargin (基于成本) 还是 GetUsedMargin (基于市值)？
            // 通常开仓检查使用基于成本的保证金。
            // 权益包含了浮动盈亏。
            // Free Margin = Equity - UsedMargin - Frozen
            return GetTotalEquity(currentPrices) - UsedMargin - FrozenMargin;
        }

        /// <summary>
        /// 冻结保证金（用于限价单）
        /// </summary>
        /// <param name="amount">冻结金额</param>
        /// <remarks>
        /// WHY（为什么需要冻结机制）:
        /// 限价单成交前，保证金处于"预留"状态，不能用于其他交易，
        /// 但也不能立即扣除（因为订单可能不成交或被撤销）。
        /// </remarks>
        public void FreezeMargin(decimal amount)
        {
            if (amount <= 0) throw new ArgumentException("Freeze amount must be positive.");
            if (amount > Cash - FrozenMargin)
                throw new InvalidOperationException("Insufficient free margin to freeze.");
            FrozenMargin += amount;
        }

        /// <summary>
        /// 释放冻结的保证金（撤单时使用）
        /// </summary>
        /// <param name="amount">释放金额</param>
        public void ReleaseMargin(decimal amount)
        {
            if (amount <= 0) throw new ArgumentException("Release amount must be positive.");
            if (amount > FrozenMargin)
                throw new InvalidOperationException("Cannot release more than frozen margin.");
            FrozenMargin -= amount;
        }

        /// <summary>
        /// 加载存档数据到账户
        /// </summary>
        /// <param name="cash">现金余额</param>
        /// <param name="positions">仓位列表</param>
        /// <param name="frozenMargin">冻结保证金（用于限价单）</param>
        public void Load(decimal cash, List<Position> positions, decimal frozenMargin = 0)
        {
            Cash = cash;
            FrozenMargin = frozenMargin;
            Positions = positions ?? new List<Position>();
        }
    }
}

