// ============================================================================
// 星露谷资本 (Stardew Capital)
// 模块：独立基本面计算器
// 作者：Stardew Capital Team
// 用途：计算商品基本面价值（无游戏依赖版本）
// ============================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using StardewCapital.Core.Futures.Models;
using StardewCapital.Core.Futures.Domain.Market;

namespace StardewCapital.Core.Calculation
{
    /// <summary>
    /// 独立基本面计算器（无游戏依赖版本）
    /// 实现公式：S_T = P_base × λ_s × (D / S)
    /// </summary>
    public class StandaloneFundamentalCalculator
    {
        private readonly CommodityConfig _config;
        private readonly LogCallback? _log;
        
        public StandaloneFundamentalCalculator(
            CommodityConfig config,
            LogCallback? log = null)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _log = log;
        }
        
        /// <summary>
        /// 计算基本面价值（S_T）
        /// </summary>
        /// <param name="currentSeason">当前季节</param>
        /// <param name="newsHistory">历史新闻事件列表</param>
        /// <returns>基本面价值（金币）</returns>
        public double Calculate(Season currentSeason, List<NewsEvent> newsHistory)
        {
            // 1. 计算季节性乘数
            double seasonalMultiplier = _config.GetSeasonalMultiplier(currentSeason);
            
            // 2. 计算总需求
            double totalDemand = CalculateTotalDemand(newsHistory);
            
            // 3. 计算总供给
            double totalSupply = CalculateTotalSupply(newsHistory);
            
            // 4. 异常处理
            if (totalSupply <= 0)
            {
                Log($"[Fundamental] {_config.Name} supply exhausted! Price surge", SimpleLogLevel.Warn);
                return _config.BasePrice * seasonalMultiplier * 10.0;
            }
            
            if (totalDemand <= 0)
            {
                Log($"[Fundamental] {_config.Name} demand collapsed! Price crash", SimpleLogLevel.Warn);
                return _config.BasePrice * seasonalMultiplier * 0.1;
            }
            
            // 5. 应用公式：S_T = P_base × λ_s × (D / S)
            double fundamentalValue = _config.BasePrice * seasonalMultiplier * (totalDemand / totalSupply);
            
            // 6. 限制波动范围
            double baselinePrice = _config.BasePrice * seasonalMultiplier;
            double maxPrice = baselinePrice * 3.0;
            double minPrice = baselinePrice * 0.3;
            
            fundamentalValue = System.Math.Clamp(fundamentalValue, minPrice, maxPrice);
            
            return fundamentalValue;
        }
        
        /// <summary>
        /// 计算总需求（D_base + ΣD_news）
        /// </summary>
        private double CalculateTotalDemand(List<NewsEvent> newsHistory)
        {
            if (newsHistory == null || newsHistory.Count == 0)
                return _config.BaseDemand;
            
            double totalDemandImpact = newsHistory
                .Where(news => IsNewsRelevant(news))
                .Sum(news => news.Impact.DemandImpact);
            
            return _config.BaseDemand + totalDemandImpact;
        }
        
        /// <summary>
        /// 计算总供给（S_base + ΣS_news）
        /// </summary>
        private double CalculateTotalSupply(List<NewsEvent> newsHistory)
        {
            if (newsHistory == null || newsHistory.Count == 0)
                return _config.BaseSupply;
            
            double totalSupplyImpact = newsHistory
                .Where(news => IsNewsRelevant(news))
                .Sum(news => news.Impact.SupplyImpact);
            
            return _config.BaseSupply + totalSupplyImpact;
        }
        
        /// <summary>
        /// 判断新闻是否影响当前商品
        /// </summary>
        private bool IsNewsRelevant(NewsEvent news)
        {
            if (news.Scope == null)
                return false;
            
            if (news.Scope.IsGlobal)
                return true;
            
            if (news.Scope.AffectedItems != null && news.Scope.AffectedItems.Count > 0)
            {
                return news.Scope.AffectedItems.Any(item =>
                    item.Equals(_config.Name, StringComparison.OrdinalIgnoreCase) ||
                    item.Equals("ALL", StringComparison.OrdinalIgnoreCase));
            }
            
            return false;
        }
        
        private void Log(string message, SimpleLogLevel level)
        {
            _log?.Invoke(message, level);
        }
    }
}

