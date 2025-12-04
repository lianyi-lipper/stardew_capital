// ============================================================================
// 星露谷资本 (Stardew Capital)
// 模块：市场冲击计算引擎
// 作者：Stardew Capital Team
// 用途：实现模型五 - 市场微观博弈与冲击层
// ============================================================================

using StardewCapital.Core.Futures.Domain.Market;
using StardewCapital.Core.Common.Logging;
using System;
using StardewCapital.Core.Futures.Config;
using System.Collections.Generic;
using System.Linq;

namespace StardewCapital.Services.Market
{
    /// <summary>
    /// 市场冲击计算服务
    /// 
    /// 核心职责：
    /// 实现期货.md中的模型五：I(t) 冲击系统
    /// 
    /// 演化方程：
    /// I(t+1) = I(t) × DecayRate + ΔI_Player + ΔI_Smart + ΔI_Trend + ΔI_FOMO
    /// 
    /// 最终价格叠加：
    /// P_Final(t) = P_Model(t) + I(t)
    /// </summary>
    public class ImpactService
    {
        private readonly ILogger _logger;

        // ========================================
        // 状态存储
        // ========================================

        /// <summary>
        /// 当前冲击值（每个商品独立）
        /// Key: CommodityId (例如 "Parsnip")
        /// Value: 冲击值 (单位: 金币)
        /// </summary>
        private readonly Dictionary<string, double> _currentImpact;

        /// <summary>
        /// 历史冲击值（用于计算动量和MA）
        /// Key: CommodityId
        /// Value: 历史队列（最近N个值）
        /// </summary>
        private readonly Dictionary<string, Queue<double>> _impactHistory;

        /// <summary>
        /// 历史价格（用于计算MA）
        /// Key: CommodityId
        /// Value: 价格队列
        /// </summary>
        private readonly Dictionary<string, Queue<double>> _priceHistory;

        // ========================================
        // 可配置参数
        // ========================================

        /// <summary>
        /// 冲击衰减率（每帧）
        /// 默认值：0.95
        /// 含义：每帧冲击值自然衰减5%，防止永久累积
        /// </summary>
        private double _decayRate = 0.95;

        /// <summary>
        /// 移动平均线周期（用于趋势派计算）
        /// 默认值：20
        /// </summary>
        private int _movingAveragePeriod = 20;

        /// <summary>
        /// 冲击值上限（防止失控）
        /// 默认值：30.0g
        /// 含义：冲击值被限制在 [-30, +30] 范围内
        /// </summary>
        private double? _maxImpactClamp = 30.0;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="logger">日志接口</param>
        public ImpactService(ILogger logger)
        {
            _logger = logger;

            _currentImpact = new Dictionary<string, double>();
            _impactHistory = new Dictionary<string, Queue<double>>();
            _priceHistory = new Dictionary<string, Queue<double>>();
        }

        /// <summary>
        /// 配置市场微观结构参数
        /// </summary>
        public void Configure(MarketMicrostructureConfig config)
        {
            _decayRate = config.DecayRate;
            _movingAveragePeriod = config.MovingAveragePeriod;
            _maxImpactClamp = config.MaxImpactClamp;
            // Scenarios are handled by ScenarioManager, but we use the parameters passed in UpdateImpact
        }
        /// <summary>
        /// 记录玩家交易（由 BrokerageService 调用）
        /// 
        /// 注意：此方法仅记录交易量，不立即更新冲击值
        /// 实际冲击值更新发生在 UpdateImpact() 中
        /// </summary>
        /// <param name="commodityId">商品ID</param>
        /// <param name="quantity">交易数量（正=买入，负=卖出）</param>
        /// <param name="liquiditySensitivity">流动性敏感度（η）</param>
        public double RecordPlayerTrade(string commodityId, int quantity, double liquiditySensitivity)
        {
            // 计算玩家冲击：ΔI_Player = Q × η
            double playerImpact = quantity * liquiditySensitivity;

            // 立即叠加到当前冲击值
            EnsureInitialized(commodityId);
            _currentImpact[commodityId] += playerImpact;

            _logger?.Log(
                $"[ImpactService] 玩家交易: {commodityId} × {quantity}, η={liquiditySensitivity:F4}, ΔI={playerImpact:F2}g",
                LogLevel.Debug
            );

            return playerImpact;
        }

        /// <summary>
        /// 更新冲击值（每帧调用）
        /// 
        /// 由 MarketManager.Update() 或 PriceEngine.UpdatePrice() 调用
        /// </summary>
        /// <param name="commodityId">商品ID</param>
        /// <param name="currentPrice">当前模型价格（P_Model）</param>
        /// <param name="fundamentalPrice">基本面价值（S_T）</param>
        /// <param name="scenario">当前市场剧本参数</param>
        public void UpdateImpact(string commodityId, double currentPrice, double fundamentalPrice, ScenarioParameters scenario)
        {
            EnsureInitialized(commodityId);

            // 1. 获取旧冲击值
            double oldImpact = _currentImpact[commodityId];

            // 2. 计算各类冲击分量
            double deltaSmart = CalculateSmartMoney(commodityId, currentPrice, fundamentalPrice, scenario.SmartMoneyStrength);
            double deltaTrend = CalculateTrendFollower(commodityId, currentPrice, scenario.TrendFollowerStrength);
            double deltaFOMO = CalculateFOMO(commodityId, oldImpact, scenario);

            // 3. 演化方程：I(t+1) = I(t) × decay + Σ新增冲击
            double newImpact = oldImpact * _decayRate + deltaSmart + deltaTrend + deltaFOMO;

            // 4. 应用上限保护
            if (_maxImpactClamp.HasValue)
            {
                newImpact = Math.Clamp(newImpact, -_maxImpactClamp.Value, _maxImpactClamp.Value);
            }

            // 5. 更新状态
            _currentImpact[commodityId] = newImpact;
            _impactHistory[commodityId].Enqueue(newImpact);
            _priceHistory[commodityId].Enqueue(currentPrice);

            // 保持队列长度
            if (_impactHistory[commodityId].Count > _movingAveragePeriod)
                _impactHistory[commodityId].Dequeue();
            if (_priceHistory[commodityId].Count > _movingAveragePeriod)
                _priceHistory[commodityId].Dequeue();
        }

        /// <summary>
        /// 获取当前冲击值
        /// </summary>
        public double GetCurrentImpact(string commodityId)
        {
            return _currentImpact.GetValueOrDefault(commodityId, 0.0);
        }

        // ========================================
        // 冲击分量计算（模型五核心算法）
        // ========================================

        /// <summary>
        /// 计算聪明钱回归力
        /// 
        /// 公式：ΔI_Smart = k_smart × (S_T - P_Final)
        /// 
        /// 逻辑：
        /// - 当价格高于基本面 → 聪明钱做空 → 负冲击（压价）
        /// - 当价格低于基本面 → 聪明钱抄底 → 正冲击（托价）
        /// - 特殊：k_smart < 0 时（轧空），逻辑反转
        /// </summary>
        private double CalculateSmartMoney(string commodityId, double currentPrice, double fundamentalPrice, double kSmart)
        {
            // 计算当前总价格（含冲击）
            double currentImpact = GetCurrentImpact(commodityId);
            double pFinal = currentPrice + currentImpact;

            // 回归力 = k × (目标价 - 当前价)
            double deltaSmart = kSmart * (fundamentalPrice - pFinal);

            return deltaSmart;
        }

        /// <summary>
        /// 计算趋势跟随者惯性
        /// 
        /// 公式：ΔI_Trend = k_trend × sign(P_Final - MA)
        /// 
        /// 逻辑：
        /// - 价格在均线上方 → 追涨 → 正冲击
        /// - 价格在均线下方 → 杀跌 → 负冲击
        /// </summary>
        private double CalculateTrendFollower(string commodityId, double currentPrice, double kTrend)
        {
            if (kTrend == 0.0)
                return 0.0;

            // 计算移动平均线
            double ma = CalculateMovingAverage(commodityId);
            if (double.IsNaN(ma))
                return 0.0; // 数据不足

            // 判断趋势方向
            double currentImpact = GetCurrentImpact(commodityId);
            double pFinal = currentPrice + currentImpact;

            double direction = Math.Sign(pFinal - ma);
            double deltaTrend = kTrend * direction;

            return deltaTrend;
        }

        /// <summary>
        /// 计算FOMO情绪放大
        /// 
        /// 公式：ΔI_FOMO = k_fomo × (I(t) - I(t-1))
        /// 
        /// 逻辑：
        /// - 刚才涨了 → 跟风买入 → 继续推高
        /// - 刚才跌了 → 恐慌卖出 →继续砸盘
        /// - 恐慌踩踏剧本：下跌时系数加倍
        /// </summary>
        private double CalculateFOMO(string commodityId, double currentImpact, ScenarioParameters scenario)
        {
            double kFOMO = scenario.FOMOStrength;
            if (kFOMO == 0.0)
                return 0.0;

            // 获取上一帧冲击值
            var history = _impactHistory[commodityId];
            if (history.Count == 0)
                return 0.0;

            double previousImpact = history.Last();
            double momentum = currentImpact - previousImpact;

            // 不对称处理（恐慌踩踏）
            if (momentum < 0 && scenario.AsymmetricDown > 1.0)
            {
                kFOMO *= scenario.AsymmetricDown;
            }

            double deltaFOMO = kFOMO * momentum;
            return deltaFOMO;
        }

        // ========================================
        // 辅助方法
        // ========================================

        /// <summary>
        /// 计算移动平均线
        /// </summary>
        private double CalculateMovingAverage(string commodityId)
        {
            if (!_priceHistory.ContainsKey(commodityId))
                return double.NaN;

            var prices = _priceHistory[commodityId];
            if (prices.Count < 5) // 最少需要5个数据点
                return double.NaN;

            return prices.Average();
        }

        /// <summary>
        /// 确保商品已初始化
        /// </summary>
        private void EnsureInitialized(string commodityId)
        {
           if (!_currentImpact.ContainsKey(commodityId))
            {
                _currentImpact[commodityId] = 0.0;
                _impactHistory[commodityId] = new Queue<double>();
                _priceHistory[commodityId] = new Queue<double>();
            }
        }

        /// <summary>
        /// 获取历史冲击值列表（只读副本）
        /// </summary>
        public List<double> GetImpactHistory(string commodityId)
        {
            if (_impactHistory.TryGetValue(commodityId, out var queue))
            {
                return queue.ToList();
            }
            return new List<double>();
        }

        /// <summary>
        /// 重置所有冲击值（测试用）
        /// </summary>
        public void ResetAllImpacts()
        {
            _currentImpact.Clear();
            _impactHistory.Clear();
            _priceHistory.Clear();
            _logger?.Log("[ImpactService] 所有冲击值已重置", LogLevel.Info);
        }
    }
}

