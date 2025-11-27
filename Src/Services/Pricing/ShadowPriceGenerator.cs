using System;
using StardewCapital.Core.Math;
using StardewCapital.Config;
using StardewCapital.Services.Infrastructure;

namespace StardewCapital.Services.Pricing
{
    /// <summary>
    /// 影子价格生成器
    /// 负责在季度/天开始时预生成完整价格轨迹
    /// </summary>
    public class ShadowPriceGenerator
    {
        private readonly ModConfig _config;
        private readonly StardewTimeProvider _timeProvider;
        private readonly News.NewsGenerator _newsGenerator;
        private readonly FundamentalEngine _fundamentalEngine;

        public ShadowPriceGenerator(
            ModConfig config, 
            StardewTimeProvider timeProvider,
            News.NewsGenerator newsGenerator,
            FundamentalEngine fundamentalEngine)
        {
            _config = config;
            _timeProvider = timeProvider;
            _newsGenerator = newsGenerator;
            _fundamentalEngine = fundamentalEngine;
        }

        /// <summary>
        /// 生成单日影子价格轨迹（日内10分钟级别）
        /// 使用布朗桥模型确保价格从 startPrice 平滑过渡到 targetPrice
        /// </summary>
        /// <param name="startPrice">开盘价</param>
        /// <param name="targetPrice">目标收盘价</param>
        /// <param name="intraVolatility">日内波动率 (默认 0.005)</param>
        /// <returns>包含全天每10分钟价格的数组</returns>
        public float[] GenerateDailyShadowPrices(
            double startPrice,
            double targetPrice,
            double intraVolatility = 0.005)
        {
            // 1. 计算总时间步数
            int startMinutes = _timeProvider.ToMinutes(_config.OpeningTime);
            int endMinutes = _timeProvider.ToMinutes(_config.ClosingTime);
            int totalMinutes = endMinutes - startMinutes;
            
            // 每10分钟一个数据点
            // 例如：6:00 到 26:00 = 20小时 = 1200分钟 = 120个点
            int steps = totalMinutes / 10; 

            // 确保至少有2个点（起点和终点）
            if (steps < 2) steps = 2;

            float[] prices = new float[steps];
            
            // 2. 初始化起点
            double currentPrice = startPrice;
            prices[0] = (float)currentPrice;

            // 3. 使用布朗桥生成后续轨迹
            for (int i = 1; i < steps; i++)
            {
                // 上一步的时间进度 (tau)
                double timeRatio = (double)(i - 1) / (steps - 1);
                
                // 时间步长 (dt)
                double timeStep = 1.0 / (steps - 1);

                // 调用核心数学模型计算下一步价格
                double nextPrice = BrownianBridge.CalculateNextTickPrice(
                    currentPrice,
                    targetPrice,
                    timeRatio,
                    timeStep,
                    intraVolatility
                );

                // 确保价格非负
                nextPrice = System.Math.Max(0.01, nextPrice);
                
                prices[i] = (float)nextPrice;
                currentPrice = nextPrice;
            }

            return prices;
        }
        /// <summary>
        /// 生成整个季度的影子价格轨迹
        /// 包含28天的所有日内数据点，并模拟每日新闻
        /// </summary>
        /// <param name="commodityName">商品名称</param>
        /// <param name="startPrice">季度初价格</param>
        /// <param name="initialFundamentalValue">初始基本面价值</param>
        /// <param name="baseVolatility">基础日波动率 (默认 0.02)</param>
        /// <param name="intraVolatility">日内波动率 (默认 0.005)</param>
        /// <returns>包含全季度所有价格点的数组 和 模拟出的新闻列表</returns>
        public (float[] Prices, List<Domain.Market.NewsEvent> ScheduledNews) GenerateSeasonalShadowPrices(
            string commodityName,
            double startPrice,
            double initialFundamentalValue,
            double baseVolatility = 0.02,
            double intraVolatility = 0.005)
        {
            // 1. 计算总数据点数
            int startMinutes = _timeProvider.ToMinutes(_config.OpeningTime);
            int endMinutes = _timeProvider.ToMinutes(_config.ClosingTime);
            int stepsPerDay = (endMinutes - startMinutes) / 10;
            if (stepsPerDay < 2) stepsPerDay = 2;

            int totalDays = 28;
            float[] seasonalPrices = new float[totalDays * stepsPerDay];
            List<Domain.Market.NewsEvent> allScheduledNews = new List<Domain.Market.NewsEvent>();
            
            // 临时维护的新闻状态（用于计算基本面）
            List<Domain.Market.NewsEvent> simulatedActiveNews = new List<Domain.Market.NewsEvent>();

            // 2. 逐日模拟
            double currentDailyPrice = startPrice;
            double currentOpenPrice = startPrice;
            
            // 获取当前季节（假设在生成时游戏已经处于该季节，或者需要传入Season参数）
            // 这里简化处理，假设生成的是当前游戏季节的轨迹
            var currentSeason = StardewValley.Game1.currentSeason.ToLower() switch
            {
                "spring" => Domain.Market.Season.Spring,
                "summer" => Domain.Market.Season.Summer,
                "fall" => Domain.Market.Season.Fall,
                "winter" => Domain.Market.Season.Winter,
                _ => Domain.Market.Season.Spring
            };

            for (int day = 0; day < totalDays; day++)
            {
                int absoluteDay = day + 1; // 1-based day index for news generation
                
                // 2.1 模拟今日新闻
                // 注意：这里我们只关心影响当前商品的新闻，或者全局新闻
                // 为了效率，我们可以只生成一次全局新闻，然后过滤。
                // 但由于 ShadowPriceGenerator 是针对单个商品调用的，我们需要确保新闻生成的一致性。
                // 这是一个潜在问题：如果对每个商品都调用 NewsGenerator，可能会生成不一致的全局新闻。
                // 解决方案：NewsGenerator 应该在外部调用，生成全季度的所有新闻，然后传给这里？
                // 或者，我们在这里只生成针对该商品的特定新闻？
                // 鉴于目前架构，我们在 MarketManager 中统一生成可能更好。
                // 但为了遵循当前任务，我们先在这里生成，后续可能需要重构为“先生成所有新闻，再生成所有价格”。
                
                // 修正方案：
                // 为了避免不同商品生成不一致的全局新闻，我们假设 NewsGenerator 在这里生成的只是“针对该商品”的独立事件。
                // 或者，我们接受这个限制，假设每个商品的世界线是独立的（不太好）。
                // 最好的办法是：GenerateSeasonalShadowPrices 接受一个预先生成的“全季度新闻列表”。
                
                // 但用户要求“一次性模拟完整个季度的价格和新闻”。
                // 让我们先实现“边走边生成”，并假设这是针对单一商品的模拟。
                // 实际在 MarketManager 中，我们需要确保全局新闻的一致性。
                // 暂时，我们在这里调用 GenerateDailyNews，只传入当前商品作为 availableCommodities。
                
                var dailyNews = _newsGenerator.GenerateDailyNews(absoluteDay, new List<string> { commodityName });
                allScheduledNews.AddRange(dailyNews);
                simulatedActiveNews.AddRange(dailyNews);
                
                // 清理过期新闻
                simulatedActiveNews.RemoveAll(n => !n.Timing.IsEffectiveOn(absoluteDay));

                // 2.2 重新计算基本面价值 (E_t[S_T^*])
                double currentFundamentalValue = _fundamentalEngine.CalculateFundamentalValue(
                    commodityName,
                    currentSeason,
                    simulatedActiveNews
                );

                // 2.3 计算当日收盘价 (GBM)
                int daysRemaining = totalDays - day;
                double nextClosingPrice = GBM.CalculateNextPrice(
                    currentDailyPrice,
                    currentFundamentalValue, // 使用更新后的基本面
                    daysRemaining,
                    baseVolatility
                );

                // 2.4 生成日内轨迹 (Brownian Bridge)
                float[] dailyTrajectory = GenerateDailyShadowPrices(
                    currentOpenPrice, 
                    nextClosingPrice, 
                    intraVolatility
                );

                // 2.5 填充数据
                int startIndex = day * stepsPerDay;
                int lengthToCopy = System.Math.Min(dailyTrajectory.Length, stepsPerDay);
                Array.Copy(dailyTrajectory, 0, seasonalPrices, startIndex, lengthToCopy);

                // 准备下一天
                currentDailyPrice = nextClosingPrice;
                currentOpenPrice = nextClosingPrice;
            }

            return (seasonalPrices, allScheduledNews);
        }
    }
}
