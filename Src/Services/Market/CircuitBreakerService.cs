using System;
using StardewCapital.Core.Futures.Domain.Instruments;
using StardewCapital.Core.Futures.Config;
using StardewModdingAPI;

namespace StardewCapital.Services.Market
{
    /// <summary>
    /// 熔断机制服务
    /// 防止尾盘价格剧烈波动导致 K 线崩盘
    /// </summary>
    public class CircuitBreakerService
    {
        private readonly IMonitor _monitor;
        private readonly MarketRules _rules;

        public CircuitBreakerService(IMonitor monitor, MarketRules rules)
        {
            _monitor = monitor;
            _rules = rules;
        }

        /// <summary>
        /// 检查并应用熔断机制
        /// 防止尾盘价格剧烈波动导致 K 线崩盘
        /// </summary>
        public void CheckAndApplyCircuitBreaker(
            CommodityFutures futures,
            double target,
            double timeRatio,
            System.Collections.Generic.Dictionary<string, double> dailyTargets)
        {
            // 1. 检查是否启用熔断
            if (!_rules.CircuitBreaker.Enabled)
                return;
            
            // 2. 检查是否已触发熔断（防止重复触发）
            if (futures.CircuitBreakerActive)
                return;
            
            // 3. 检查时间条件：剩余时间是否 < 阈值
            if (timeRatio < _rules.CircuitBreaker.TimeThreshold)
                return;
            
            // 4. 检查价格条件：跌幅是否 > MaxMove
            double priceDiff = target - futures.CurrentPrice;
            double absDiff = Math.Abs(priceDiff);
            
            if (absDiff <= _rules.CircuitBreaker.MaxMove)
                return;
            
            // ========== 触发熔断 ==========
            
            // 5. 锁定当日收盘目标价
            double lockedClosePrice;
            if (priceDiff > 0)
            {
                // 目标价远高于当前价，锁定为 P_τ + MaxMove
                lockedClosePrice = futures.CurrentPrice + _rules.CircuitBreaker.MaxMove;
            }
            else
            {
                // 目标价远低于当前价，锁定为 P_τ - MaxMove
                lockedClosePrice = futures.CurrentPrice - _rules.CircuitBreaker.MaxMove;
            }
            
            // 6. 计算未消化的价差（Gap）
            futures.Gap = target - lockedClosePrice;
            
            // 7. 更新当日目标价为锁定价
            dailyTargets[futures.Symbol] = lockedClosePrice;
            
            // 8. 标记熔断状态
            futures.CircuitBreakerActive = true;
            
            // 9. 日志输出
            _monitor.Log(
                $"[CIRCUIT BREAKER] {futures.Symbol} | " +
                $"Current={futures.CurrentPrice:F2}g, Target={target:F2}g, Locked={lockedClosePrice:F2}g | " +
                $"Gap={futures.Gap:+0.00;-0.00}g (will apply tomorrow)",
                LogLevel.Warn
            );
        }
    }
}

