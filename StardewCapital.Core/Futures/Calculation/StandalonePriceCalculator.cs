// ============================================================================
// 星露谷资本 (Stardew Capital)
// 模块：独立价格计算器
// 作者：Stardew Capital Team
// 用途：完全独立于游戏API的价格轨迹生成器
// ============================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using StardewCapital.Core.Futures.Math;
using StardewCapital.Core.Futures.Models;
using StardewCapital.Core.Futures.Domain.Market;
using StardewCapital.Core.Futures.Domain.Market.MarketState;

namespace StardewCapital.Core.Futures.Calculation
{
    /// <summary>
    /// 独立价格计算器（无游戏依赖版本）
    /// 完全基于纯函数，可在任何环境中运行
    /// </summary>
    public class StandalonePriceCalculator
    {
        private readonly Random _random;
        private readonly LogCallback? _log;
        
        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="log">可选的日志回调</param>
        /// <param name="seed">可选的随机种子（用于可重现测试）</param>
        public StandalonePriceCalculator(LogCallback? log = null, int? seed = null)
        {
            _log = log;
            _random = seed.HasValue ? new Random(seed.Value) : new Random();
        }
        
        /// <summary>
        /// 计算整个季度的价格轨迹
        /// </summary>
        /// <param name="input">输入参数</param>
        /// <returns>计算结果</returns>
        public PriceCalculationOutput Calculate(PriceCalculationInput input)
        {
            Log($"[StandalonePriceCalculator] Starting calculation for {input.CommodityName}", SimpleLogLevel.Info);
            Log($"  Season: {input.Season}, Days: {input.TotalDays}, Steps/Day: {input.StepsPerDay}", SimpleLogLevel.Info);
            
            // 1. 初始化输出数组
            int totalPoints = input.TotalDays * input.StepsPerDay;
            float[] shadowPrices = new float[totalPoints];
            float[] fundamentalValues = new float[input.TotalDays];
            List<ScheduledNewsEvent> allNews = new();
            
            // 2. 创建辅助计算器
            var newsSimulator = new StandaloneNewsSimulator(input.NewsTemplates, _random, _log);
            var fundamentalCalc = new StandaloneFundamentalCalculator(input.CommodityConfig, _log);
            
            // 3. 模拟每一天
            List<NewsEvent> activeNews = new();
            double currentPrice = input.StartPrice;
            double currentOpenPrice = input.StartPrice;
            
            for (int day = 0; day < input.TotalDays; day++)
            {
                int absoluteDay = day + 1; // 1-based
                
                // 3.1 生成盘后新闻
                var dailyNews = newsSimulator.GenerateDailyNews(absoluteDay, input.CommodityName);
                
                foreach (var news in dailyNews)
                {
                    allNews.Add(new ScheduledNewsEvent
                    {
                        Event = news,
                        TriggerDay = absoluteDay,
                        TriggerTimeRatio = null, // 盘后新闻，开盘时生效
                        HasTriggered = false
                    });
                    activeNews.Add(news);
                }
                
                // 3.2 生成盘中新闻（可选）
                if (input.MarketRules?.IntradayNews?.IntradayNewsProbability > 0)
                {
                    double randomTriggerTime = _random.NextDouble(); // 0.0-1.0
                    var intradayNews = newsSimulator.GenerateIntradayNews(
                        absoluteDay,
                        randomTriggerTime,
                        input.CommodityName,
                        input.MarketRules.IntradayNews.IntradayNewsProbability
                    );
                    
                    if (intradayNews != null)
                    {
                        allNews.Add(new ScheduledNewsEvent
                        {
                            Event = intradayNews,
                            TriggerDay = absoluteDay,
                            TriggerTimeRatio = randomTriggerTime,
                            HasTriggered = false
                        });
                    }
                }
                
                // 3.3 清理过期新闻
                activeNews.RemoveAll(n => !n.Timing.IsEffectiveOn(absoluteDay));
                
                // 3.4 计算基本面价值
                double fundamental = fundamentalCalc.Calculate(input.Season, activeNews);
                fundamentalValues[day] = (float)fundamental;
                
                // 3.5 计算当日收盘价（GBM）
                int daysRemaining = input.TotalDays - day;
                double nextClose = GBM.CalculateNextPrice(
                    currentPrice,
                    fundamental,
                    daysRemaining,
                    input.BaseVolatility,
                    _random
                );
                
                // 3.6 生成日内轨迹（布朗桥）
                float[] dailyTrajectory = GenerateBrownianBridge(
                    currentOpenPrice,
                    nextClose,
                    input.StepsPerDay,
                    input.IntraVolatility,
                    input
                );
                
                // 3.7 填充数组
                int startIdx = day * input.StepsPerDay;
                int lengthToCopy = System.Math.Min(dailyTrajectory.Length, input.StepsPerDay);
                Array.Copy(dailyTrajectory, 0, shadowPrices, startIdx, lengthToCopy);
                
                // 准备下一天
                currentPrice = nextClose;
                currentOpenPrice = nextClose;
            }
            
            Log($"[StandalonePriceCalculator] Calculation complete. Generated {totalPoints} data points, {allNews.Count} news events", SimpleLogLevel.Info);
            
            return new PriceCalculationOutput
            {
                ShadowPrices = shadowPrices,
                FundamentalValues = fundamentalValues,
                ScheduledNews = allNews,
                StepsPerDay = input.StepsPerDay,
                GeneratedAt = DateTime.UtcNow,
                RandomSeed = input.RandomSeed
            };
        }
        
        /// <summary>
        /// 生成单日影子价格轨迹（日内布朗桥）
        /// </summary>
        private float[] GenerateBrownianBridge(
            double startPrice,
            double targetPrice,
            int steps,
            double intraVolatility,
            PriceCalculationInput input)
        {
            if (steps < 2) steps = 2;
            
            float[] prices = new float[steps];
            double currentPrice = startPrice;
            prices[0] = (float)currentPrice;
            
            // 从配置读取布朗桥参数
            var bbConfig = input.MarketRules.ShadowPricing.BrownianBridge;
            
            for (int i = 1; i < steps; i++)
            {
                double timeRatio = (double)(i - 1) / (steps - 1);
                double timeStep = 1.0 / (steps - 1);
                
                double nextPrice = BrownianBridge.CalculateNextTickPrice(
                    currentPrice,
                    targetPrice,
                    timeRatio,
                    timeStep,
                    intraVolatility,
                    _random,
                    bbConfig.OpeningShockAlpha,
                    bbConfig.ShockDecayLambda,
                    bbConfig.NoiseScaleFactor
                );
                
                nextPrice = System.Math.Max(0.01, nextPrice);
                prices[i] = (float)nextPrice;
                currentPrice = nextPrice;
            }
            
            return prices;
        }
        
        /// <summary>
        /// 输出日志（如果有回调）
        /// </summary>
        private void Log(string message, SimpleLogLevel level = SimpleLogLevel.Info)
        {
            _log?.Invoke(message, level);
        }
    }
}

