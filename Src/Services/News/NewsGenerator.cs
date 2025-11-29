// ============================================================================
// 星露谷资本 (Stardew Capital)
// 模块：新闻生成器服务
// 作者：Stardew Capital Team
// 用途：基于JSON配置随机生成新闻事件
// ============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using StardewModdingAPI;
using StardewCapital.Domain.Market;

namespace StardewCapital.Services.News
{
    /// <summary>
    /// 新闻生成器
    /// 基于JSON配置随机生成新闻事件
    /// </summary>
    public class NewsGenerator
    {
        private readonly IMonitor _monitor;
        private readonly Random _random;
        private List<NewsTemplate> _newsTemplates = new();

        public NewsGenerator(IModHelper helper, IMonitor monitor)
        {
            _monitor = monitor;
            _random = new Random();
            LoadNewsConfigs(helper);
        }

        /// <summary>
        /// 获取已加载的新闻模板列表
        /// </summary>
        /// <returns>新闻模板列表</returns>
        public List<NewsTemplate> GetNewsTemplates()
        {
            return new List<NewsTemplate>(_newsTemplates);
        }

        /// <summary>
        /// 从JSON加载新闻配置
        /// </summary>
        private void LoadNewsConfigs(IModHelper helper)
        {
            try
            {
                var configPath = Path.Combine(helper.DirectoryPath, "Assets", "data", "news_config.json");
                
                if (!File.Exists(configPath))
                {
                    _monitor.Log($"[NewsGenerator] news_config.json not found at {configPath}", LogLevel.Error);
                    _newsTemplates = new List<NewsTemplate>();
                    return;
                }
                
                var json = File.ReadAllText(configPath);
                var options = new JsonSerializerOptions 
                { 
                    PropertyNameCaseInsensitive = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                };
                var config = JsonSerializer.Deserialize<NewsConfigFile>(json, options);
                
                _newsTemplates = config?.NewsTemplates ?? new List<NewsTemplate>();
                _monitor.Log($"[NewsGenerator] Loaded {_newsTemplates.Count} news templates", LogLevel.Info);
            }
            catch (Exception ex)
            {
                _monitor.Log($"[NewsGenerator] Failed to load news_config.json: {ex.Message}", LogLevel.Error);
                _newsTemplates = new List<NewsTemplate>();
            }
        }

        /// <summary>
        /// 生成每日新闻（基于配置概率）
        /// </summary>
        /// <param name="currentDay">当前游戏日期（绝对日期）</param>
        /// <param name="availableCommodities">可用商品列表</param>
        /// <returns>生成的新闻事件列表</returns>
        public List<NewsEvent> GenerateDailyNews(int currentDay, List<string> availableCommodities)
        {
            var generatedNews = new List<NewsEvent>();
            
            if (availableCommodities.Count == 0)
            {
                _monitor.Log("[NewsGenerator] No available commodities, skipping news generation", LogLevel.Warn);
                return generatedNews;
            }
            
            foreach (var template in _newsTemplates)
            {
                // 1. 检查概率
                if (_random.NextDouble() > template.Conditions.Probability)
                    continue;
                
                // 2. 检查前置条件（未来扩展）
                if (!CheckPrerequisites(template.Conditions.Prerequisites))
                    continue;
                
                // 3. 选择受影响商品
                var affectedCommodity = SelectAffectedCommodity(template.Scope, availableCommodities);
                if (affectedCommodity == null)
                    continue;
                
                // 4. 生成实例（应用随机范围）
                var newsInstance = CreateNewsInstance(template, currentDay, affectedCommodity);
                generatedNews.Add(newsInstance);
                
                _monitor.Log(
                    $"[News] Generated: {newsInstance.Title} ({affectedCommodity}) " +
                    $"D:{newsInstance.Impact.DemandImpact:+0;-0;0} S:{newsInstance.Impact.SupplyImpact:+0;-0;0}",
                    LogLevel.Info
                );
            }
            
            return generatedNews;
        }

        /// <summary>
        /// 生成盘中突发新闻 (Breaking News)
        /// 从配置中筛选高严重度、短持续时间的新闻
        /// </summary>
        /// <param name="currentDay">当前日期（绝对日期）</param>
        /// <param name="currentTimeRatio">当前时间进度（0.0-1.0）</param>
        /// <param name="availableCommodities">可用商品列表</param>
        /// <returns>突发新闻事件，如果未触发则返回null</returns>
        public NewsEvent? GenerateIntradayNews(
            int currentDay, 
            double currentTimeRatio, 
            List<string> availableCommodities,
            double dailyProbability = 0.1)  // 新增：每日发生概率
        {
            // 1. 每日概率检查 ← 新增！
            if (_random.NextDouble() > dailyProbability)
            {
                return null;  // 未触发概率，今天不生成盘中新闻
            }
            
            if (availableCommodities.Count == 0 || _newsTemplates.Count == 0)
                return null;
            
            // 2. 筛选适合盘中触发的新闻模板（高严重度）
            var intradayCandidates = _newsTemplates
                .Where(t => t.Severity == "high" || t.Severity == "critical")
                .ToList();
            
            if (intradayCandidates.Count == 0)
                return null;
            
            // 3. 随机选择一个模板
            var template = intradayCandidates[_random.Next(intradayCandidates.Count)];
            
            // 4. 选择受影响商品
            var affectedCommodity = SelectAffectedCommodity(template.Scope, availableCommodities);
            if (affectedCommodity == null)
                return null;
            
            // 5. 创建新闻实例
            var newsInstance = CreateNewsInstance(template, currentDay, affectedCommodity);
            
            // 6. 设置盘中新闻的特殊时间参数（仅当天有效）
            newsInstance.Timing.AnnouncementDay = currentDay;
            newsInstance.Timing.EffectiveDays = new int[] { currentDay, currentDay };
            
            _monitor.Log(
                $"[IntradayNews] Generated: {newsInstance.Title} ({affectedCommodity}) " +
                $"D:{newsInstance.Impact.DemandImpact:+0;-0;0} S:{newsInstance.Impact.SupplyImpact:+0;-0;0}",
                LogLevel.Info
            );
            
            return newsInstance;
        }

        /// <summary>
        /// 创建新闻实例（应用随机化）
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

            // 克隆影响参数并应用随机范围
            instance.Impact = new NewsImpact
            {
                DemandImpact = Math.Round(template.Impact.DemandImpact + GetRandomOffset(template.Conditions.RandomRange)),
SupplyImpact = Math.Round(template.Impact.SupplyImpact + GetRandomOffset(template.Conditions.RandomRange)),
                PriceMultiplier = template.Impact.PriceMultiplier,
                ConfidenceImpact = template.Impact.ConfidenceImpact,
                VolatilityImpact = template.Impact.VolatilityImpact
            };

            // 克隆范围参数
            instance.Scope = new NewsScope
            {
                AffectedItems = new List<string> { commodity },
                AffectedCategories = new List<string>(template.Scope.AffectedCategories),
                IsGlobal = template.Scope.IsGlobal,
                Regions = new List<string>(template.Scope.Regions)
            };

            // 设置时间参数
            instance.Timing = new NewsTiming
            {
                AnnouncementDay = day,
                EffectiveDays = new int[] { day, day + 28 } // 默认本季有效
            };

            // 克隆条件参数
            instance.Conditions = new NewsConditions
            {
                Probability = template.Conditions.Probability,
                Prerequisites = new List<string>(template.Conditions.Prerequisites),
                RandomRange = new double[] { template.Conditions.RandomRange[0], template.Conditions.RandomRange[1] }
            };

            return instance;
        }

        /// <summary>
        /// 获取随机偏移量
        /// </summary>
        private double GetRandomOffset(double[] randomRange)
        {
            if (randomRange == null || randomRange.Length < 2)
                return 0;
            
            double min = randomRange[0];
            double max = randomRange[1];
            
            return min + _random.NextDouble() * (max - min);
        }

        /// <summary>
        /// 选择受影响的商品
        /// </summary>
        private string SelectAffectedCommodity(NewsScope scope, List<string> available)
        {
            if (scope.IsGlobal)
                return "ALL";
            
            // 优先使用指定物品
            if (scope.AffectedItems != null && scope.AffectedItems.Count > 0)
            {
                var validItems = scope.AffectedItems.Intersect(available).ToList();
                if (validItems.Count > 0)
                    return validItems[_random.Next(validItems.Count)];
            }
            
            // 使用分类过滤
            if (scope.AffectedCategories != null && scope.AffectedCategories.Count > 0)
            {
                var matchingCommodities = new List<string>();
                
                foreach (var categoryName in scope.AffectedCategories)
                {
                    var category = CommodityCategoryManager.ParseCategory(categoryName);
                    if (category.HasValue)
                    {
                        var commoditiesInCategory = CommodityCategoryManager.GetCommoditiesInCategory(category.Value)
                            .Intersect(available)
                            .ToList();
                        matchingCommodities.AddRange(commoditiesInCategory);
                    }
                }
                
                // 去重
                matchingCommodities = matchingCommodities.Distinct().ToList();
                
                if (matchingCommodities.Count > 0)
                    return matchingCommodities[_random.Next(matchingCommodities.Count)];
            }
            
            // 回退到随机商品
            return available[_random.Next(available.Count)];
        }

        /// <summary>
        /// 检查前置条件（未来扩展）
        /// </summary>
        private bool CheckPrerequisites(List<string> prerequisites)
        {
            // TODO: 检查历史事件
            return prerequisites == null || prerequisites.Count == 0;
        }

        /// <summary>
        /// 解析新闻类型字符串
        /// </summary>
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
                _ => NewsType.Festival // 默认值
            };
        }
    }
}
