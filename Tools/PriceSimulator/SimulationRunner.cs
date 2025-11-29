using System;
using System.Collections.Generic;
using System.Linq;
using StardewCapital.Config;
using StardewCapital.Core.Calculation;
using StardewCapital.Core.Models;
using StardewCapital.Core.Utils;
using StardewCapital.Domain.Market;
using StardewCapital.Domain.Market.MarketState;
using StardewCapital.Data.SaveData;
using StardewCapital.Services.News;

namespace StardewCapital.Simulator
{
    /// <summary>
    /// 核心模拟运行器（重构版，使用独立计算器）
    /// 负责初始化服务并运行价格模拟
    /// </summary>
    public class SimulationRunner
    {
        private readonly SimulatorConfig _config;
        private readonly List<CommodityConfig> _commodityConfigs;
        private readonly MarketRules _marketRules;
        private readonly List<NewsTemplate> _newsTemplates;

        public SimulationRunner(
            SimulatorConfig config,
            List<CommodityConfig> commodityConfigs,
            MarketRules marketRules,
            List<NewsTemplate> newsTemplates)
        {
            _config = config;
            _commodityConfigs = commodityConfigs;
            _marketRules = marketRules;
            _newsTemplates = newsTemplates;
        }

        /// <summary>
        /// 运行模拟
        /// </summary>
        public MarketStateSaveData RunSimulation()
        {
            if (_config.simulation.randomSeed.HasValue)
            {
                Console.WriteLine($"使用随机种子: {_config.simulation.randomSeed.Value}");
            }

            // 解析季节
            Season season = ParseSeason(_config.simulation.season);
            int year = _config.simulation.year;

            Console.WriteLine($"\n========== 开始模拟 ==========");
            Console.WriteLine($"商品: {_config.simulation.commodity}");
            Console.WriteLine($"季节: {season} (Year {year})");
            Console.WriteLine($"市场时间: {_config.marketTiming.openingTime} - {_config.marketTiming.closingTime}");

            // 1. 获取商品配置
            var commodityConfig = _commodityConfigs.FirstOrDefault(c => c.Name == _config.simulation.commodity);
            if (commodityConfig == null)
            {
                throw new Exception($"未找到商品配置: {_config.simulation.commodity}");
            }

            // 2. 计算初始价格
            double initialPrice = CalculateInitialPrice(commodityConfig, season);
            Console.WriteLine($"初始价格: {initialPrice:F2}g");

            // 3. 构建计算输入
            var input = new PriceCalculationInput
            {
                CommodityName = _config.simulation.commodity,
                CommodityConfig = commodityConfig,
                StartPrice = initialPrice,
                Season = season,
                TotalDays = 28,
                StepsPerDay = TimeUtils.CalculateStepsPerDay(
                    _config.marketTiming.openingTime,
                    _config.marketTiming.closingTime,
                    10  // 每10分钟一个数据点
                ),
                OpeningTime = _config.marketTiming.openingTime,
                ClosingTime = _config.marketTiming.closingTime,
                NewsTemplates = _newsTemplates,
                MarketRules = _marketRules,
                BaseVolatility = 0.02,
                IntraVolatility = 0.005,
                RandomSeed = _config.simulation.randomSeed
            };

            // 4. 创建独立计算器并运行
            Console.WriteLine($"\n正在生成季度数据...");
            var calculator = new StandalonePriceCalculator(
                log: (msg, level) => 
                {
                    if (_config.advanced.verboseOutput)
                        Console.WriteLine($"[{level}] {msg}");
                },
                seed: _config.simulation.randomSeed
            );

            var output = calculator.Calculate(input);

            // 5. 输出统计信息
            PrintStatistics(output);

            // 6. 转换为游戏存档格式
            var marketState = new FuturesMarketState
            {
                Symbol = $"{_config.simulation.commodity}_F",
                Season = season,
                Year = year,
                CommodityName = _config.simulation.commodity,
                DeliveryDay = 28,
                ShadowPrices = output.ShadowPrices,
                FundamentalValues = output.FundamentalValues,
                ScheduledNews = output.ScheduledNews,
                StepsPerDay = output.StepsPerDay
            };

            var saveData = new MarketStateSaveData
            {
                CurrentSeason = season,
                CurrentYear = year,
                CurrentDay = 1,
                SavedOpeningTime = _config.marketTiming.openingTime,
                SavedClosingTime = _config.marketTiming.closingTime,
                FuturesStates = new List<FuturesMarketState> { marketState }
            };

            Console.WriteLine($"\n✓ 模拟完成");
            return saveData;
        }

        /// <summary>
        /// 计算初始价格
        /// </summary>
        private double CalculateInitialPrice(CommodityConfig commodity, Season season)
        {
            double basePrice = commodity.BasePrice;
            
            // 应用季节性乘数
            if (commodity.GrowingSeason != season && !commodity.IsGreenhouseCrop)
            {
                basePrice *= commodity.OffSeasonMultiplier;
            }

            return basePrice;
        }

        /// <summary>
        /// 打印统计信息
        /// </summary>
        private void PrintStatistics(PriceCalculationOutput output)
        {
            if (!_config.advanced.verboseOutput) return;

            Console.WriteLine($"\n========== 模拟统计 ==========");
            Console.WriteLine($"总数据点数: {output.TotalDataPoints}");
            Console.WriteLine($"每日数据点数: {output.StepsPerDay}");
            Console.WriteLine($"总天数: {output.TotalDays}");

            // 价格统计
            Console.WriteLine($"\n价格统计:");
            Console.WriteLine($"  开始价格: {output.OpeningPrice:F2}g");
            Console.WriteLine($"  结束价格: {output.ClosingPrice:F2}g");
            Console.WriteLine($"  最低价格: {output.MinPrice:F2}g");
            Console.WriteLine($"  最高价格: {output.MaxPrice:F2}g");
            Console.WriteLine($"  平均价格: {output.AvgPrice:F2}g");
            Console.WriteLine($"  总涨跌幅: {output.TotalChange:F2}g ({output.TotalChangePercent:F2}%)");

            // 新闻统计
            Console.WriteLine($"\n新闻事件:");
            Console.WriteLine($"  总事件数: {output.ScheduledNews.Count}");
            
            var dailyNews = output.ScheduledNews.Where(n => n.TriggerTimeRatio == null).Count();
            var intradayNews = output.ScheduledNews.Where(n => n.TriggerTimeRatio != null).Count();
            
            Console.WriteLine($"  盘后新闻: {dailyNews}");
            Console.WriteLine($"  盘中新闻: {intradayNews}");

            // 基本面统计
            if (output.FundamentalValues != null && output.FundamentalValues.Length > 0)
            {
                double startFundamental = output.FundamentalValues.First();
                double endFundamental = output.FundamentalValues.Last();
                
                Console.WriteLine($"\n基本面价值:");
                Console.WriteLine($"  初始值: {startFundamental:F2}g");
                Console.WriteLine($"  最终值: {endFundamental:F2}g");
                Console.WriteLine($"  变化: {(endFundamental - startFundamental):F2}g ({(endFundamental / startFundamental - 1) * 100:F2}%)");
            }
        }

        private Season ParseSeason(string seasonStr)
        {
            return seasonStr.ToLower() switch
            {
                "spring" => Season.Spring,
                "summer" => Season.Summer,
                "fall" => Season.Fall,
                "winter" => Season.Winter,
                _ => throw new ArgumentException($"未知的季节: {seasonStr}")
            };
        }
    }
}
