using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using StardewCapital.Config;
using StardewCapital.Domain.Instruments;
using StardewCapital.Domain.Market;
using StardewCapital.Domain.Market.MarketState;
using StardewCapital.Data.SaveData;
using StardewCapital.Simulator.Services;  // 使用模拟器版本的服务
using StardewCapital.Services.Pricing.Generators;

namespace StardewCapital.Simulator
{
    /// <summary>
    /// 核心模拟运行器
    /// 负责初始化服务并运行价格模拟
    /// </summary>
    public class SimulationRunner
    {
        private readonly SimulatorConfig _config;
        private readonly List<CommodityConfig> _commodityConfigs;
        private readonly MarketRules _marketRules;
        private readonly string _newsConfigPath;  
        private readonly MockTimeProvider _timeProvider;

        public SimulationRunner(
            SimulatorConfig config,
            List<CommodityConfig> commodityConfigs,
            MarketRules marketRules,
            string newsConfigPath,
            MockTimeProvider timeProvider)
        {
            _config = config;
            _commodityConfigs = commodityConfigs;
            _marketRules = marketRules;
            _newsConfigPath = newsConfigPath;
            _timeProvider = timeProvider;
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

            // 1. 初始化服务
            var newsGenerator = new NewsGenerator(_newsConfigPath);
            var fundamentalEngine = new FundamentalEngine(_commodityConfigs);
            
            var shadowGenerator = new FuturesShadowGenerator(
                CreateModConfig(),
                _timeProvider,
                newsGenerator,
                fundamentalEngine,
                _marketRules
            );

            // 2. 获取商品配置
            var commodityConfig = _commodityConfigs.FirstOrDefault(c => c.name == _config.simulation.commodity);
            if (commodityConfig == null)
            {
                throw new Exception($"未找到商品配置: {_config.simulation.commodity}");
            }

            // 3. 创建期货对象
            double initialPrice = CalculateInitialPrice(commodityConfig, season);
            var futures = new CommodityFutures(commodityConfig, season)
            {
                CurrentPrice = initialPrice,
                FuturesPrice = initialPrice
            };

            Console.WriteLine($"初始价格: {initialPrice:F2}g");

            // 4. 生成市场状态
            Console.WriteLine($"\n正在生成季度数据...");
            var marketState = shadowGenerator.Generate(futures, season, year) as FuturesMarketState;

            if (marketState == null)
            {
                throw new Exception("生成市场状态失败");
            }

            // 5. 输出统计信息
            PrintStatistics(marketState);

            // 6. 构建存档数据（与游戏格式一致）
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
            double basePrice = commodity.basePrice;
            
            // 应用季节性乘数
            if (commodity.growingSeason != season.ToString() && !commodity.isGreenhouseCrop)
            {
                basePrice *= commodity.offSeasonMultiplier;
            }

            return basePrice;
        }

        /// <summary>
        /// 打印统计信息
        /// </summary>
        private void PrintStatistics(FuturesMarketState marketState)
        {
            if (!_config.advanced.verboseOutput) return;

            Console.WriteLine($"\n========== 模拟统计 ==========");
            Console.WriteLine($"总数据点数: {marketState.ShadowPrices.Length}");
            Console.WriteLine($"每日数据点数: {marketState.StepsPerDay}");
            Console.WriteLine($"总天数: {marketState.ShadowPrices.Length / marketState.StepsPerDay}");

            // 价格统计
            var prices = marketState.ShadowPrices;
            double minPrice = prices.Min();
            double maxPrice = prices.Max();
            double avgPrice = prices.Average();
            double startPrice = prices.First();
            double endPrice = prices.Last();

            Console.WriteLine($"\n价格统计:");
            Console.WriteLine($"  开始价格: {startPrice:F2}g");
            Console.WriteLine($"  结束价格: {endPrice:F2}g");
            Console.WriteLine($"  最低价格: {minPrice:F2}g");
            Console.WriteLine($"  最高价格: {maxPrice:F2}g");
            Console.WriteLine($"  平均价格: {avgPrice:F2}g");
            Console.WriteLine($"  总涨跌幅: {(endPrice - startPrice):F2}g ({(endPrice / startPrice - 1) * 100:F2}%)");

            // 新闻统计
            Console.WriteLine($"\n新闻事件:");
            Console.WriteLine($"  总事件数: {marketState.ScheduledNews.Count}");
            
            var dailyNews = marketState.ScheduledNews.Where(n => n.TriggerTimeRatio == null).Count();
            var intradayNews = marketState.ScheduledNews.Where(n => n.TriggerTimeRatio != null).Count();
            
            Console.WriteLine($"  盘后新闻: {dailyNews}");
            Console.WriteLine($"  盘中新闻: {intradayNews}");

            // 基本面统计
            if (marketState.FundamentalValues != null && marketState.FundamentalValues.Length > 0)
            {
                double startFundamental = marketState.FundamentalValues.First();
                double endFundamental = marketState.FundamentalValues.Last();
                
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

        private ModConfig CreateModConfig()
        {
            return new ModConfig
            {
                OpeningTime = _config.marketTiming.openingTime,
                ClosingTime = _config.marketTiming.closingTime
            };
        }
    }
}
