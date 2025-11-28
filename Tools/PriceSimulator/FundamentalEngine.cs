using System;
using System.Collections.Generic;
using System.Linq;
using StardewCapital.Config;
using StardewCapital.Domain.Market;

namespace StardewCapital.Simulator.Services
{
    /// <summary>
    /// 基本面价值计算引擎（模拟器版本 - 无需SMAPI）
    /// </summary>
    public class FundamentalEngine
    {
        private readonly Dictionary<string, CommodityConfig> _commodityConfigs;

        public FundamentalEngine(List<CommodityConfig> commodityConfigs)
        {
            _commodityConfigs = new Dictionary<string, CommodityConfig>();
            foreach (var config in commodityConfigs)
            {
                _commodityConfigs[config.name] = config;
            }
            Console.WriteLine($"✓ FundamentalEngine已加载 {_commodityConfigs.Count} 种商品配置");
        }

        public CommodityConfig? GetCommodityConfig(string commodityName)
        {
            return _commodityConfigs.TryGetValue(commodityName, out var config) ? config : null;
        }

        public List<CommodityConfig> GetAllCommodityConfigs()
        {
            return _commodityConfigs.Values.ToList();
        }

        public double CalculateFundamentalValue(
            string commodityName,
            Season currentSeason,
            List<NewsEvent> newsHistory)
        {
            var config = GetCommodityConfig(commodityName);
            if (config == null)
            {
                Console.WriteLine($"警告: 商品 {commodityName} 配置不存在，使用默认值");
                return 100.0;
            }

            // 计算季节性乘数
            double seasonalMultiplier = GetSeasonalMultiplier(config, currentSeason);

            // 计算总需求和供给
            double totalDemand = CalculateTotalDemand(config.baseDemand, commodityName, newsHistory);
            double totalSupply = CalculateTotalSupply(config.baseSupply, commodityName, newsHistory);

            // 异常处理
            if (totalSupply <= 0)
            {
                return config.basePrice * seasonalMultiplier * 10.0;
            }

            if (totalDemand <= 0)
            {
                return config.basePrice * seasonalMultiplier * 0.1;
            }

            // 应用公式：S_T = P_base × λ_s × (D / S)
            double fundamentalValue = config.basePrice * seasonalMultiplier * (totalDemand / totalSupply);

            // 限制价格范围
            double baselinePrice = config.basePrice * seasonalMultiplier;
            double maxPrice = baselinePrice * 3.0;
            double minPrice = baselinePrice * 0.3;

            fundamentalValue = Math.Clamp(fundamentalValue, minPrice, maxPrice);

            return fundamentalValue;
        }

        private double CalculateTotalDemand(
            double baseDemand,
            string commodityName,
            List<NewsEvent> newsHistory)
        {
            if (newsHistory == null || newsHistory.Count == 0)
                return baseDemand;

            double totalDemandImpact = newsHistory
                .Where(news => IsNewsRelevant(news, commodityName))
                .Sum(news => news.Impact.DemandImpact);

            return baseDemand + totalDemandImpact;
        }

        private double CalculateTotalSupply(
            double baseSupply,
            string commodityName,
            List<NewsEvent> newsHistory)
        {
            if (newsHistory == null || newsHistory.Count == 0)
                return baseSupply;

            double totalSupplyImpact = newsHistory
                .Where(news => IsNewsRelevant(news, commodityName))
                .Sum(news => news.Impact.SupplyImpact);

            return baseSupply + totalSupplyImpact;
        }

        private bool IsNewsRelevant(NewsEvent news, string commodityName)
        {
            if (news.Scope == null)
                return false;

            if (news.Scope.IsGlobal)
                return true;

            if (news.Scope.AffectedItems != null && news.Scope.AffectedItems.Count > 0)
            {
                return news.Scope.AffectedItems.Any(item =>
                    item.Equals(commodityName, StringComparison.OrdinalIgnoreCase) ||
                    item.Equals("ALL", StringComparison.OrdinalIgnoreCase));
            }

            return false;
        }

        private double GetSeasonalMultiplier(CommodityConfig config, Season currentSeason)
        {
            string crescentSeasonStr = config.growingSeason;
            Season growingSeason = crescentSeasonStr.ToLower() switch  
            {
                "spring" => Season.Spring,
                "summer" => Season.Summer,
                "fall" => Season.Fall,
                "winter" => Season.Winter,
                _ => Season.Spring
            };

            // 如果当前季节是生长季节，或是温室作物，使用基础价格
            if (currentSeason == growingSeason || config.isGreenhouseCrop)
            {
                return 1.0;
            }

            // 否则应用淡季乘数
            return config.offSeasonMultiplier;
        }

        public double GetSeasonalMultiplier(string commodityName, Season currentSeason)
        {
            var config = GetCommodityConfig(commodityName);
            return config != null ? GetSeasonalMultiplier(config, currentSeason) : 1.0;
        }
    }
}
