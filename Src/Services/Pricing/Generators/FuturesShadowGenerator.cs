using System;
using System.Collections.Generic;
using System.Linq;
using StardewCapital.Core.Math;
using StardewCapital.Config;
using StardewCapital.Domain.Instruments;
using StardewCapital.Domain.Market;
using StardewCapital.Domain.Market.MarketState;
using StardewCapital.Services.Infrastructure;
using StardewCapital.Services.News;

namespace StardewCapital.Services.Pricing.Generators
{
    /// <summary>
    /// 期货影子价格生成器
    /// 负责预计算整个季度的期货价格轨迹和新闻事件
    /// </summary>
    public class FuturesShadowGenerator : IMarketDataGenerator
    {
        private readonly ModConfig _config;
        private readonly StardewTimeProvider _timeProvider;
        private readonly NewsGenerator _newsGenerator;
        private readonly FundamentalEngine _fundamentalEngine;
        private readonly MarketRules _rules;
        private readonly Random _random = new();

        public FuturesShadowGenerator(
            ModConfig config,
            StardewTimeProvider timeProvider,
            NewsGenerator newsGenerator,
            FundamentalEngine fundamentalEngine,
            MarketRules rules)
        {
            _config = config;
            _timeProvider = timeProvider;
            _newsGenerator = newsGenerator;
            _fundamentalEngine = fundamentalEngine;
            _rules = rules;
        }

        /// <summary>
        /// 生成期货市场状态
        /// </summary>
        public IMarketState Generate(IInstrument instrument, Season season, int year)
        {
            if (instrument is not CommodityFutures futures)
                throw new ArgumentException("FuturesShadowGenerator only supports CommodityFutures");

            // 获取初始价格和基本面价值
            double startPrice = futures.CurrentPrice;
            double initialFundamentalValue = _fundamentalEngine.CalculateFundamentalValue(
                futures.CommodityName,
                season,
                new List<NewsEvent>()
            );

            // 生成完整季度数据
            var (shadowPrices, fundamentalValues, scheduledNews) = GenerateSeasonalData(
                futures.CommodityName,
                season,
                startPrice,
                initialFundamentalValue
            );

            // 构建市场状态
            return new FuturesMarketState
            {
                Symbol = futures.Symbol,
                Season = season,
                Year = year,
                CommodityName = futures.CommodityName,
                DeliveryDay = futures.DeliveryDay,
                ShadowPrices = shadowPrices,
                StepsPerDay = CalculateStepsPerDay(),
                ScheduledNews = scheduledNews,
                FundamentalValues = fundamentalValues
            };
        }

        /// <summary>
        /// 生成整个季度的数据
        /// </summary>
        private (float[] prices, float[] fundamentals, List<ScheduledNewsEvent> news) GenerateSeasonalData(
            string commodityName,
            Season season,
            double startPrice,
            double initialFundamentalValue,
            double baseVolatility = 0.02,
            double intraVolatility = 0.005)
        {
            int stepsPerDay = CalculateStepsPerDay();
            int totalDays = 28;
            
            float[] seasonalPrices = new float[totalDays * stepsPerDay];
            float[] fundamentalValues = new float[totalDays];
            List<ScheduledNewsEvent> allScheduledNews = new();
            
            // 临时维护的新闻状态（用于计算基本面）
            List<NewsEvent> simulatedActiveNews = new();

            double currentDailyPrice = startPrice;
            double currentOpenPrice = startPrice;

            for (int day = 0; day < totalDays; day++)
            {
                int absoluteDay = day + 1; // 1-based
                
                // 1. 生成今日新闻
                var dailyNews = _newsGenerator.GenerateDailyNews(
                    absoluteDay,
                    new List<string> { commodityName }
                );
                
                // 转换为ScheduledNewsEvent
                foreach (var news in dailyNews)
                {
                    allScheduledNews.Add(new ScheduledNewsEvent
                    {
                        Event = news,
                        TriggerDay = absoluteDay,
                        TriggerTimeRatio = null, // 盘后新闻，开盘时生效
                        HasTriggered = false
                    });
                    
                    simulatedActiveNews.Add(news);
                }
                
                // 清理过期新闻
                simulatedActiveNews.RemoveAll(n => !n.Timing.IsEffectiveOn(absoluteDay));

                // 2. 可选：生成盘中新闻（随机时间触发）
                double randomTriggerTime = _random.NextDouble(); // 0.0-1.0 完全随机
                var intradayNews = _newsGenerator.GenerateIntradayNews(
                    absoluteDay,
                    randomTriggerTime, // 随机时间：可能在开盘到收盘之间任意时刻
                    new List<string> { commodityName },
                    _rules.IntradayNews.IntradayNewsProbability
                );
                
                if (intradayNews != null)
                {
                    allScheduledNews.Add(new ScheduledNewsEvent
                    {
                        Event = intradayNews,
                        TriggerDay = absoluteDay,
                        TriggerTimeRatio = randomTriggerTime, // 使用随机时间
                        HasTriggered = false
                    });
                    
                    // 注意：盘中新闻不立即添加到simulatedActiveNews
                    // 因为它在价格计算时才生效
                }

                // 3. 重新计算基本面价值
                double currentFundamentalValue = _fundamentalEngine.CalculateFundamentalValue(
                    commodityName,
                    season,
                    simulatedActiveNews
                );
                
                fundamentalValues[day] = (float)currentFundamentalValue;

                // 4. 计算当日收盘价 (GBM)
                int daysRemaining = totalDays - day;
                double nextClosingPrice = GBM.CalculateNextPrice(
                    currentDailyPrice,
                    currentFundamentalValue,
                    daysRemaining,
                    baseVolatility
                );

                // 5. 生成日内轨迹 (Brownian Bridge)
                float[] dailyTrajectory = GenerateDailyShadowPrices(
                    currentOpenPrice,
                    nextClosingPrice,
                    intraVolatility
                );

                // 6. 填充数据
                int startIndex = day * stepsPerDay;
                int lengthToCopy = System.Math.Min(dailyTrajectory.Length, stepsPerDay);
                Array.Copy(dailyTrajectory, 0, seasonalPrices, startIndex, lengthToCopy);

                // 准备下一天
                currentDailyPrice = nextClosingPrice;
                currentOpenPrice = nextClosingPrice;
            }

            return (seasonalPrices, fundamentalValues, allScheduledNews);
        }

        /// <summary>
        /// 生成单日影子价格轨迹（日内10分钟级别）
        /// </summary>
        private float[] GenerateDailyShadowPrices(
            double startPrice,
            double targetPrice,
            double intraVolatility = 0.005)
        {
            int steps = CalculateStepsPerDay();
            if (steps < 2) steps = 2;

            float[] prices = new float[steps];
            double currentPrice = startPrice;
            prices[0] = (float)currentPrice;

            for (int i = 1; i < steps; i++)
            {
                double timeRatio = (double)(i - 1) / (steps - 1);
                double timeStep = 1.0 / (steps - 1);

                double nextPrice = BrownianBridge.CalculateNextTickPrice(
                    currentPrice,
                    targetPrice,
                    timeRatio,
                    timeStep,
                    intraVolatility
                );

                nextPrice = System.Math.Max(0.01, nextPrice);
                prices[i] = (float)nextPrice;
                currentPrice = nextPrice;
            }

            return prices;
        }

        /// <summary>
        /// 计算每天的时间步数
        /// </summary>
        private int CalculateStepsPerDay()
        {
            int startMinutes = _timeProvider.ToMinutes(_config.OpeningTime);
            int endMinutes = _timeProvider.ToMinutes(_config.ClosingTime);
            int totalMinutes = endMinutes - startMinutes;
            
            // 每10分钟一个数据点
            int steps = totalMinutes / 10;
            return steps < 2 ? 2 : steps;
        }
    }
}
