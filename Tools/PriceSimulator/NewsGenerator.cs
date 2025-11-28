using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using StardewCapital.Config;
using StardewCapital.Domain.Market;

namespace StardewCapital.Simulator.Services
{
    /// <summary>
    /// 新闻生成器（模拟器版本 - 无需SMAPI）
    /// </summary>
    public class NewsGenerator
    {
        private readonly Random _random;
        private List<NewsTemplate> _newsTemplates = new();

        public NewsGenerator(string newsConfigPath)
        {
            _random = new Random();
            LoadNewsConfigs(newsConfigPath);
        }

        private void LoadNewsConfigs(string filePath)
        {
            try
            {
                if (!File.Exists(filePath))
                {
                    Console.WriteLine($"警告: news_config.json未找到: {filePath}");
                    _newsTemplates = new List<NewsTemplate>();
                    return;
                }

                var json = File.ReadAllText(filePath);
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    ReadCommentHandling = JsonCommentHandling.Skip,
                    AllowTrailingCommas = true
                };
                var config = JsonSerializer.Deserialize<NewsConfigFile>(json, options);

                _newsTemplates = config?.news_items ?? new List<NewsTemplate>();
                Console.WriteLine($"✓ 加载了 {_newsTemplates.Count} 个新闻模板");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"错误: 加载news_config.json失败: {ex.Message}");
                _newsTemplates = new List<NewsTemplate>();
            }
        }

        public List<NewsEvent> GenerateDailyNews(int currentDay, List<string> availableCommodities)
        {
            var generatedNews = new List<NewsEvent>();

            if (availableCommodities.Count == 0 || _newsTemplates.Count == 0)
                return generatedNews;

            foreach (var template in _newsTemplates)
            {
                // 检查概率
                if (_random.NextDouble() > template.conditions.probability)
                    continue;

                // 选择受影响商品
                var affectedCommodity = SelectAffectedCommodity(template.scope, availableCommodities);
                if (affectedCommodity == null)
                    continue;

                // 创建新闻实例
                var newsInstance = CreateNewsInstance(template, currentDay, affectedCommodity);
                generatedNews.Add(newsInstance);
            }

            return generatedNews;
        }

        public NewsEvent? GenerateIntradayNews(
            int currentDay,
            double currentTimeRatio,
            List<string> availableCommodities,
            double dailyProbability = 0.1)
        {
            if (_random.NextDouble() > dailyProbability)
                return null;

            if (availableCommodities.Count == 0 || _newsTemplates.Count == 0)
                return null;

            // 筛选高严重度新闻
            var intradayCandidates = _newsTemplates
                .Where(t => t.severity == "high" || t.severity == "critical")
                .ToList();

            if (intradayCandidates.Count == 0)
                return null;

            var template = intradayCandidates[_random.Next(intradayCandidates.Count)];
            var affectedCommodity = SelectAffectedCommodity(template.scope, availableCommodities);
            
            if (affectedCommodity == null)
                return null;

            var newsInstance = CreateNewsInstance(template, currentDay, affectedCommodity);
            newsInstance.Timing.AnnouncementDay = currentDay;
            newsInstance.Timing.EffectiveDays = new int[] { currentDay, currentDay };

            return newsInstance;
        }

        private NewsEvent CreateNewsInstance(NewsTemplate template, int day, string commodity)
        {
            var instance = new NewsEvent
            {
                Id = $"{template.id}_{day}_{commodity}",
                Version = "1.0",
                Day = day,
                Title = template.title,
                Description = string.Format(template.description, commodity),
                Severity = template.severity,
                Type = ParseNewsType(template.news_type)
            };

            instance.Impact = new NewsImpact
            {
                DemandImpact = Math.Round(template.impact.demand_impact + GetRandomOffset(template.conditions.random_range)),
                SupplyImpact = Math.Round(template.impact.supply_impact + GetRandomOffset(template.conditions.random_range)),
                PriceMultiplier = template.impact.price_multiplier,
                ConfidenceImpact = template.impact.confidence_impact,
                VolatilityImpact = template.impact.volatility_impact
            };

            instance.Scope = new NewsScope
            {
                AffectedItems = new List<string> { commodity },
                AffectedCategories = new List<string>(template.scope.affected_categories ?? new List<string>()),
                IsGlobal = template.scope.is_global,
                Regions = new List<string>(template.scope.regions ?? new List<string>())
            };

            instance.Timing = new NewsTiming
            {
                AnnouncementDay = day,
                EffectiveDays = new int[] { day, day + 28 }
            };

            instance.Conditions = new NewsConditions
            {
                Probability = template.conditions.probability,
                Prerequisites = new List<string>(template.conditions.prerequisites ?? new List<string>()),
                RandomRange = new double[] { template.conditions.random_range?[0] ?? 0, template.conditions.random_range?[1] ?? 0 }
            };

            return instance;
        }

        private double GetRandomOffset(double[]? randomRange)
        {
            if (randomRange == null || randomRange.Length < 2)
                return 0;

            double min = randomRange[0];
            double max = randomRange[1];

            return min + _random.NextDouble() * (max - min);
        }

        private string? SelectAffectedCommodity(NewsScopeTemplate scope, List<string> available)
        {
            if (scope.is_global)
                return "ALL";

            if (scope.affected_items != null && scope.affected_items.Count > 0)
            {
                var validItems = scope.affected_items.Intersect(available).ToList();
                if (validItems.Count > 0)
                    return validItems[_random.Next(validItems.Count)];
            }

            return available[_random.Next(available.Count)];
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
    }

    // JSON配置类
    public class NewsConfigFile
    {
        public List<NewsTemplate> news_items { get; set; } = new();
    }

    public class NewsTemplate
    {
        public string id { get; set; } = string.Empty;
        public string title { get; set; } = string.Empty;
        public string description { get; set; } = string.Empty;
        public string severity { get; set; } = string.Empty;
        public string news_type { get; set; } = string.Empty;
        public NewsImpactTemplate impact { get; set; } = new();
        public NewsScopeTemplate scope { get; set; } = new();
        public NewsTimingTemplate timing { get; set; } = new();
        public NewsConditionsTemplate conditions { get; set; } = new();
    }

    public class NewsImpactTemplate
    {
        public double demand_impact { get; set; }
        public double supply_impact { get; set; }
        public double price_multiplier { get; set; }
        public double confidence_impact { get; set; }
        public double volatility_impact { get; set; }
    }

    public class NewsScopeTemplate
    {
        public List<string>? affected_items { get; set; }
        public List<string>? affected_categories { get; set; }
        public bool is_global { get; set; }
        public List<string>? regions { get; set; }
    }

    public class NewsTimingTemplate
    {
        public List<int>? announcement_days { get; set; }
        public List<int>? effective_days { get; set; }
        public int decay_period { get; set; }
    }

    public class NewsConditionsTemplate
    {
        public double probability { get; set; }
        public List<string>? prerequisites { get; set; }
        public double[]? random_range { get; set; }
    }
}
