using System;
using System.Collections.Generic;
using System.Linq;
using StardewCapital.Core.Futures.Config;
using StardewCapital.Core.Futures.Domain.Market;
using StardewCapital.Core.Common.Logging;

namespace StardewCapital.Services.Market
{
    /// <summary>
    /// NPC 代理管理器
    /// 负责模拟不同类型的市场参与者（Smart Money, Trend Followers, FOMO Traders）
    /// 并生成虚拟订单流。
    /// </summary>
    public class NPCAgentManager
    {
        private readonly ILogger _logger;
        private readonly MarketRules _rules;

        // 状态缓存（用于计算动量等）
        private readonly Dictionary<string, Queue<double>> _priceHistory = new();
        private readonly Dictionary<string, double> _lastImpact = new();

        public NPCAgentManager(ILogger logger, MarketRules rules)
        {
            _logger = logger;
            _rules = rules;
        }

        /// <summary>
        /// 存储每个合约上一帧的各派系力量值
        /// </summary>
        public Dictionary<string, AgentForces> LastForces { get; } = new();

        /// <summary>
        /// 计算所有NPC代理产生的总虚拟流量
        /// </summary>
        /// <param name="symbol">合约代码</param>
        /// <param name="currentPrice">当前撮合价格 (P_Match)</param>
        /// <param name="shadowPrice">影子价格 (P_Shadow)</param>
        /// <param name="fundamentalValue">基本面价值 (S_T)</param>
        /// <param name="scenario">当前剧本参数</param>
        /// <returns>净虚拟流量（正数=买单，负数=卖单）</returns>
        public int CalculateNetVirtualFlow(
            string symbol, 
            double currentPrice, 
            double shadowPrice, 
            double fundamentalValue,
            ScenarioParameters scenario)
        {
            // 1. 基础势能流量 (Gap-driven Flow)
            // 这是最主要的力量，推动价格向影子价格靠拢
            // Flow = Gap * LiquidityCoefficient
            double gap = shadowPrice - currentPrice;
            int baseFlow = (int)(gap * _rules.VirtualFlow.LiquidityCoefficient);

            // 2. 聪明钱 (Smart Money)
            // 基于基本面价值回归
            // ΔI_Smart = k_smart * (Fundamental - Current)
            // Final Strength = Global Base * Scenario Multiplier
            double smartStrength = _rules.NpcAgents.SmartMoney.BaseStrength * scenario.SmartMoneyStrength;
            double smartFlow = CalculateSmartMoneyFlow(currentPrice, fundamentalValue, smartStrength);

            // 3. 趋势派 (Trend Followers)
            // 基于移动平均线
            double trendStrength = _rules.NpcAgents.TrendFollowers.BaseStrength * scenario.TrendFollowerStrength;
            double trendFlow = CalculateTrendFlow(symbol, currentPrice, trendStrength);

            // 4. FOMO韭菜 (FOMO Traders)
            // 基于价格动量
            double fomoStrength = _rules.NpcAgents.FomoTraders.BaseStrength * scenario.FOMOStrength;
            double fomoFlow = CalculateFOMOFlow(symbol, currentPrice, fomoStrength, scenario.AsymmetricDown);

            // 5. 汇总流量
            double totalFlow = baseFlow + smartFlow + trendFlow + fomoFlow;

            // 6. 更新历史状态
            UpdateHistory(symbol, currentPrice, totalFlow);

            // 7. 记录力量分布 (用于UI显示)
            LastForces[symbol] = new AgentForces
            {
                BaseFlow = baseFlow,
                SmartMoneyFlow = smartFlow,
                TrendFlow = trendFlow,
                FomoFlow = fomoFlow,
                TotalFlow = totalFlow
            };

            // 8. 限制最大流量
            int clampedFlow = Math.Clamp((int)totalFlow, -_rules.VirtualFlow.MaxFlowPerTick, _rules.VirtualFlow.MaxFlowPerTick);

            return clampedFlow;
        }

        private double CalculateSmartMoneyFlow(double currentPrice, double fundamentalValue, double strength)
        {
            // 聪明钱推动价格回归基本面
            // 强度系数通常较大，代表机构力量
            // 这里我们需要将 strength (k_smart) 转换为流量单位
            // 假设 k_smart 是价格敏感度，即每偏离1g产生的流量
            // 注意：ScenarioData 中的 strength 可能是之前的 Impact 系数，需要重新校准
            // 为了保持兼容，我们假设 strength * 1000 是流量系数 (Base 0.05 * 1.0 * 1000 = 50 flow/g)
            
            double deviation = fundamentalValue - currentPrice;
            return deviation * strength * 1000.0;
        }

        private double CalculateTrendFlow(string symbol, double currentPrice, double strength)
        {
            if (strength == 0) return 0;

            // 计算移动平均线
            double ma = CalculateMovingAverage(symbol);
            if (double.IsNaN(ma)) return 0;

            // 价格在均线上方 -> 买入
            // 价格在均线下方 -> 卖出
            double signal = Math.Sign(currentPrice - ma);
            
            // 强度系数 * 信号 * 固定基数
            return signal * strength * 500.0;
        }

        private double CalculateFOMOFlow(string symbol, double currentPrice, double strength, double asymmetricDown)
        {
            if (strength == 0) return 0;

            // 计算动量 (当前价 - 上一次价)
            // 注意：这里需要更精细的动量计算，比如过去N帧的变化
            // 简化起见，使用上一帧记录的价格（如果有）
            // 但由于我们每帧都调用，currentPrice - lastPrice 可能很小
            // 更好的方式是使用 ImpactService 中的动量逻辑，或者这里维护一个短周期动量
            
            // 暂时简化：假设动量与 Gap 方向一致（追涨杀跌）
            // 或者使用 _priceHistory 计算短期斜率
            
            var history = GetHistory(symbol);
            if (history.Count < 2) return 0;

            double lastPrice = history.Last();
            double momentum = currentPrice - lastPrice;

            // 阈值检查
            if (Math.Abs(momentum) < _rules.NpcAgents.FomoTraders.Threshold)
                return 0;

            // 不对称处理 (恐慌踩踏)
            if (momentum < 0 && asymmetricDown > 1.0)
            {
                strength *= asymmetricDown;
            }

            return momentum * strength * 2000.0; // 动量通常很小，需要较大系数
        }

        private void UpdateHistory(string symbol, double currentPrice, double impact)
        {
            var history = GetHistory(symbol);
            history.Enqueue(currentPrice);
            
            if (history.Count > _rules.NpcAgents.TrendFollowers.MovingAveragePeriod)
            {
                history.Dequeue();
            }

            _lastImpact[symbol] = impact;
        }

        private Queue<double> GetHistory(string symbol)
        {
            if (!_priceHistory.ContainsKey(symbol))
            {
                _priceHistory[symbol] = new Queue<double>();
            }
            return _priceHistory[symbol];
        }

        private double CalculateMovingAverage(string symbol)
        {
            var history = GetHistory(symbol);
            if (history.Count < 5) return double.NaN;
            return history.Average();
        }
    }

    /// <summary>
    /// 记录各派系产生的虚拟流量
    /// </summary>
    public struct AgentForces
    {
        public double BaseFlow { get; set; }
        public double SmartMoneyFlow { get; set; }
        public double TrendFlow { get; set; }
        public double FomoFlow { get; set; }
        public double TotalFlow { get; set; }
    }
}

