// ============================================================================
// 星露谷资本 (Stardew Capital)
// 模块：独立新闻模拟器
// 作者：Stardew Capital Team
// 用途：生成新闻事件（无游戏依赖版本）
// ============================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using StardewCapital.Core.Futures.Models;
using StardewCapital.Core.Futures.Domain.Market;
using StardewCapital.Core.Futures.Data;

namespace StardewCapital.Core.Calculation
{
    /// <summary>
    /// 独立新闻模拟器（无游戏依赖版本）
    /// 基于JSON配置随机生成新闻事件
    /// </summary>
    public class StandaloneNewsSimulator
    {
        private readonly List<NewsTemplate> _newsTemplates;
        private readonly Random _random;
        private readonly LogCallback? _log;
        
        public StandaloneNewsSimulator(
            List<NewsTemplate> newsTemplates,
            Random random,
            LogCallback? log = null)
        {
            _newsTemplates = newsTemplates ?? new List<NewsTemplate>();
            _random = random;
            _log = log;
            
            // 调试日志：检查模板是否正确加载
            Log($"[StandaloneNewsSimulator] Initialized with {_newsTemplates.Count} news templates", SimpleLogLevel.Info);
        }
        
        /// <summary>
        /// 生成每日新闻（基于配置概率）
        /// </summary>
        public List<NewsEvent> GenerateDailyNews(int currentDay, string commodityName)
        {
            var generatedNews = new List<NewsEvent>();
            
            Log($"[StandaloneNewsSimulator] Day {currentDay}: Checking {_newsTemplates.Count} templates for {commodityName}", SimpleLogLevel.Debug);
            
            foreach (var template in _newsTemplates)
            {
                // 1. 检查概率
                double roll = _random.NextDouble();
                if (roll > template.Conditions.Probability)
                {
                    Log($"[StandaloneNewsSimulator]   {template.Title}: Failed probability check ({roll:F3} > {template.Conditions.Probability})", SimpleLogLevel.Debug);
                    continue;
                }
                
                // 2. 选择受影响商品
                if (!IsRelevantToCommodity(template.Scope, commodityName))
                {
                    Log($"[StandaloneNewsSimulator]   {template.Title}: Not relevant to {commodityName}", SimpleLogLevel.Debug);
                    continue;
                }
                
                // 3. 生成实例
                var newsInstance = CreateNewsInstance(template, currentDay, commodityName);
                generatedNews.Add(newsInstance);
                
                Log($"[News] Day {currentDay}: {newsInstance.Title} ({commodityName}) " +
                    $"D:{newsInstance.Impact.DemandImpact:+0;-0;0} S:{newsInstance.Impact.SupplyImpact:+0;-0;0}",
                    SimpleLogLevel.Info);
            }
            
            return generatedNews;
        }
        
        /// <summary>
        /// 生成盘中突发新闻
        /// </summary>
        public NewsEvent? GenerateIntradayNews(
            int currentDay,
            double currentTimeRatio,
            string commodityName,
            double dailyProbability)
        {
            // 每日概率检查
            if (_random.NextDouble() > dailyProbability)
                return null;
            
            if (_newsTemplates.Count == 0)
                return null;
            
            // 筛选高严重度新闻
            var candidates = _newsTemplates
                .Where(t => t.Severity == "high" || t.Severity == "critical")
                .ToList();
            
            if (candidates.Count == 0)
                return null;
            
            // 随机选择模板
            var template = candidates[_random.Next(candidates.Count)];
            
            if (!IsRelevantToCommodity(template.Scope, commodityName))
                return null;
            
            var newsInstance = CreateNewsInstance(template, currentDay, commodityName);
            newsInstance.Timing.AnnouncementDay = currentDay;
            newsInstance.Timing.EffectiveDays = new int[] { currentDay, currentDay };
            
            Log($"[IntradayNews] Day {currentDay} @ {currentTimeRatio:F2}: {newsInstance.Title}",
                SimpleLogLevel.Info);
            
            return newsInstance;
        }
        
        /// <summary>
        /// 创建新闻实例
        /// </summary>
        private NewsEvent CreateNewsInstance(NewsTemplate template, int day, string commodity)
        {
            var instance = new NewsEvent
            {
                Id = $"{template.Id}_{day}_{commodity}",
                Version = "1.0",
                Day = day,
                Title = template.Title,
                Description = string.Format(template.Description, commodity),
                Severity = template.Severity,
                Type = ParseNewsType(template.NewsTypeString)
            };
            
            // 应用随机范围
            instance.Impact = new NewsImpact
            {
                DemandImpact = System.Math.Round(
                    template.Impact.DemandImpact + GetRandomOffset(template.Conditions.RandomRange)),
                SupplyImpact = System.Math.Round(
                    template.Impact.SupplyImpact + GetRandomOffset(template.Conditions.RandomRange)),
                PriceMultiplier = template.Impact.PriceMultiplier,
                ConfidenceImpact = template.Impact.ConfidenceImpact,
                VolatilityImpact = template.Impact.VolatilityImpact
            };
            
            instance.Scope = new NewsScope
            {
                AffectedItems = new List<string> { commodity },
                AffectedCategories = new List<string>(template.Scope.AffectedCategories),
                IsGlobal = template.Scope.IsGlobal,
                Regions = new List<string>(template.Scope.Regions)
            };
            
            instance.Timing = new NewsTiming
            {
                AnnouncementDay = day,
                EffectiveDays = new int[] { day, day + 28 }
            };
            
            instance.Conditions = new NewsConditions
            {
                Probability = template.Conditions.Probability,
                Prerequisites = new List<string>(template.Conditions.Prerequisites),
                RandomRange = new double[] { 
                    template.Conditions.RandomRange[0], 
                    template.Conditions.RandomRange[1] 
                }
            };
            
            return instance;
        }
        
        private double GetRandomOffset(double[] randomRange)
        {
            if (randomRange == null || randomRange.Length < 2)
                return 0;
            
            double min = randomRange[0];
            double max = randomRange[1];
            return min + _random.NextDouble() * (max - min);
        }
        
        private bool IsRelevantToCommodity(NewsScope scope, string commodityName)
        {
            if (scope.IsGlobal)
                return true;
            
            // 检查是否直接指定了商品名称
            if (scope.AffectedItems != null && scope.AffectedItems.Count > 0)
            {
                bool matchedByName = scope.AffectedItems.Any(item => 
                    item.Equals(commodityName, StringComparison.OrdinalIgnoreCase) ||
                    item.Equals("ALL", StringComparison.OrdinalIgnoreCase));
                
                if (matchedByName)
                    return true;
            }
            
            // 检查是否通过类别匹配
            if (scope.AffectedCategories != null && scope.AffectedCategories.Count > 0)
            {
                string commodityCategory = GetCommodityCategory(commodityName);
                if (!string.IsNullOrEmpty(commodityCategory))
                {
                    return scope.AffectedCategories.Any(category =>
                        category.Equals(commodityCategory, StringComparison.OrdinalIgnoreCase));
                }
            }
            
            return false;
        }
        
        /// <summary>
        /// 获取商品的类别（vegetables, fruits, grains等）
        /// </summary>
        private string GetCommodityCategory(string commodityName)
        {
            // TODO: 将来可以从CommodityConfig中读取，目前使用硬编码映射
            return commodityName.ToLower() switch
            {
                // 蔬菜类
                "parsnip" => "vegetables",
                "cauliflower" => "vegetables",
                "potato" => "vegetables",
                "kale" => "vegetables",
                "rhubarb" => "vegetables",
                "tomato" => "vegetables",
                "hot pepper" => "vegetables",
                "corn" => "vegetables",
                "eggplant" => "vegetables",
                "pumpkin" => "vegetables",
                "bok choy" => "vegetables",
                "yam" => "vegetables",
                "artichoke" => "vegetables",
                
                // 水果类
                "strawberry" => "fruits",
                "blueberry" => "fruits",
                "melon" => "fruits",
                "starfruit" => "fruits",
                "cranberry" => "fruits",
                "grape" => "fruits",
                
                // 谷物类
                "wheat" => "grains",
                "hops" => "grains",
                
                // 花卉类
                "tulip" => "flowers",
                "jazz" => "flowers",
                "poppy" => "flowers",
                "sunflower" => "flowers",
                "fairy rose" => "flowers",
                
                _ => "vegetables" // 默认归类为蔬菜
            };
        }
        
        private NewsType ParseNewsType(string newsTypeString)
        {
            return newsTypeString switch
            {
                "PestCrisis" => NewsType.PestCrisis,
                "BumperHarvest" => NewsType.BumperHarvest,
                "ZuzuCityOrder" => NewsType.ZuzuCityOrder,
                "Drought" => NewsType.Drought,
                "Flood" => NewsType.Flood,
                "Festival" => NewsType.Festival,
                "MayorPromotion" => NewsType.MayorPromotion,
                "StorageSpoilage" => NewsType.StorageSpoilage,
                _ => NewsType.Festival
            };
        }
        
        private void Log(string message, SimpleLogLevel level)
        {
            _log?.Invoke(message, level);
        }
    }
}

