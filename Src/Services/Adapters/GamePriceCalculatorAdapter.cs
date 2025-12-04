// ============================================================================
// 星露谷资本 (Stardew Capital)
// 模块:游戏价格计算器适配器
// 作者：Stardew Capital Team
// 用途：将游戏服务适配为独立价格计算器的输入
// ============================================================================

using System;
using StardewCapital.Services.News;
using System.Collections.Generic;
using StardewCapital.Core.Futures.Config;
using StardewCapital.Core.Futures.Calculation;
using StardewCapital.Core.Futures.Models;
using StardewCapital.Core.Common.Utils;
using StardewCapital.Core.Futures.Domain.Instruments;
using StardewCapital.Core.Futures.Domain.Market;
using StardewCapital.Core.Futures.Domain.Market.MarketState;
using StardewCapital.Core.Futures.Data;
using StardewCapital.Services.Pricing;

namespace StardewCapital.Services.Adapters
{
    /// <summary>
    /// 游戏价格计算器适配器
    /// 将游戏中的服务（NewsGenerator、FundamentalEngine等）适配为独立计算器所需的纯数据输入
    /// </summary>
    public class GamePriceCalculatorAdapter
    {
        private readonly FundamentalEngine _fundamentalEngine;
        private readonly NewsGenerator _newsGenerator;
        private readonly ModConfig _config;
        private readonly MarketRules _rules;
        private readonly LogCallback? _log;
        
        public GamePriceCalculatorAdapter(
            FundamentalEngine fundamentalEngine,
            NewsGenerator newsGenerator,
            ModConfig config,
            MarketRules rules,
            LogCallback? log = null)
        {
            _fundamentalEngine = fundamentalEngine;
            _newsGenerator = newsGenerator;
            _config = config;
            _rules = rules;
            _log = log;
        }

        /// <summary>
        /// 生成期货市场状态
        /// </summary>
        public IMarketState GenerateMarketState(CommodityFutures futures, Season season, int year)
        {
            // 1. 获取商品配置 (from FundamentalEngine)
            var commodityConfig = _fundamentalEngine.GetCommodityConfig(futures.CommodityName);
            if (commodityConfig == null)
                throw new ArgumentException($"Commodity config not found for {futures.CommodityName}");

            // 2. 计算初始价格
            double initialPrice = CalculateInitialPrice(commodityConfig, season);

            // 3. 计算每日步数
            int stepsPerDay = TimeUtils.CalculateStepsPerDay(
                _config.OpeningTime,
                _config.ClosingTime,
                10  // 每10分钟一个数据点
            );

            // 4. 构建输入（从配置文件读取参数）
            var shadowConfig = _rules.ShadowPricing;
            var input = new PriceCalculationInput
            {
                CommodityName = futures.CommodityName,
                CommodityConfig = commodityConfig,
                StartPrice = initialPrice,
                Season = season,
                TotalDays = shadowConfig.TimeSettings.TotalDays,
                StepsPerDay = stepsPerDay,
                OpeningTime = shadowConfig.TimeSettings.OpeningTime,
                ClosingTime = shadowConfig.TimeSettings.ClosingTime,
                NewsTemplates = LoadNewsTemplates(),
                MarketRules = _rules,
                BaseVolatility = shadowConfig.Volatility.BaseVolatility,
                IntraVolatility = shadowConfig.Volatility.IntraVolatility,
                RandomSeed = null
            };

            // 5. 创建独立计算器并运行
            var calculator = new StandalonePriceCalculator(_log, seed: null);
            var output = calculator.Calculate(input);

            // 6. 转换为市场状态
            var marketState = new FuturesMarketState
            {
                Symbol = futures.Symbol,
                Season = season,
                Year = year,
                CommodityName = futures.CommodityName,
                DeliveryDay = 28,
                ShadowPrices = output.ShadowPrices,
                FundamentalValues = output.FundamentalValues,
                ScheduledNews = output.ScheduledNews,
                StepsPerDay = output.StepsPerDay
            };

            return marketState;
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
        /// 加载新闻模板
        /// </summary>
        private List<StardewCapital.Core.Futures.Data.NewsTemplate> LoadNewsTemplates()
        {
            // 直接从 NewsGenerator 获取已加载的模板
            return _newsGenerator.GetNewsTemplates();
        }
    }
}




