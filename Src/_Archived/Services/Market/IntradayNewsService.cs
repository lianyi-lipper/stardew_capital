using System;
using System.Collections.Generic;
using System.Linq;
using StardewCapital.Core.Time;
using StardewCapital.Domain.Instruments;
using StardewCapital.Domain.Market;
using StardewCapital.Services.News;
using StardewCapital.Services.Pricing;
using StardewCapital.Config;
using StardewModdingAPI;

namespace StardewCapital.Services.Market
{
    /// <summary>
    /// 盘中新闻服务
    /// 负责检查并触发盘中突发新闻，动态更新目标价格
    /// </summary>
    public class IntradayNewsService
    {
        private readonly IMonitor _monitor;
        private readonly MixedTimeClock _clock;
        private readonly NewsGenerator _newsGenerator;
        private readonly FundamentalEngine _fundamentalEngine;
        private readonly PriceEngine _priceEngine;
        private readonly ScenarioManager _scenarioManager;
        private readonly MarketManager _marketManager;
        private readonly OrderBookManager _orderBookManager;
        private readonly MarketRules _rules;
        private readonly MarketTimeCalculator _timeCalculator;

        /// <summary>上次检查盘中新闻的tick</summary>
        private int _lastIntradayNewsCheckTick = 0;
        
        /// <summary>上次触发盘中新闻的tick（用于最小间隔限制）</summary>
        private int _lastIntradayNewsTriggeredTick = 0;

        public IntradayNewsService(
            IMonitor monitor,
            MixedTimeClock clock,
            NewsGenerator newsGenerator,
            FundamentalEngine fundamentalEngine,
            PriceEngine priceEngine,
            ScenarioManager scenarioManager,
            MarketManager marketManager,
            OrderBookManager orderBookManager,
            MarketRules rules,
            MarketTimeCalculator timeCalculator)
        {
            _monitor = monitor;
            _clock = clock;
            _newsGenerator = newsGenerator;
            _fundamentalEngine = fundamentalEngine;
            _priceEngine = priceEngine;
            _scenarioManager = scenarioManager;
            _marketManager = marketManager;
            _orderBookManager = orderBookManager;
            _rules = rules;
            _timeCalculator = timeCalculator;
        }

        /// <summary>
        /// 检查并触发盘中突发新闻
        /// 事件驱动：随机检测是否生成突发新闻并更新目标价格
        /// </summary>
        public void CheckAndTriggerIntradayNews(
            int currentTick,
            Dictionary<string, double> dailyTargets,
            List<NewsEvent> newsHistory,
            List<NewsEvent> activeNewsEffects)
        {
            // 1. 检查是否启用
            if (!_rules.IntradayNews.Enabled)
                return;
            
            // 2. 检查是否到达检查间隔
            if (currentTick - _lastIntradayNewsCheckTick < _rules.IntradayNews.CheckIntervalTicks)
                return;
            
            _lastIntradayNewsCheckTick = currentTick;
            
            // 3. 检查最小新闻间隔（防止频繁触发）
            if (currentTick - _lastIntradayNewsTriggeredTick < _rules.IntradayNews.MinNewsIntervalTicks)
                return;
            
            // 4. 概率检查
            var random = new Random();
            if (random.NextDouble() > _rules.IntradayNews.TriggerProbability)
                return;
            
            // 5. 生成盘中新闻
            // 防御性检查：确保MarketManager已初始化
            if (_marketManager == null)
            {
                _monitor?.Log("[IntradayNews] MarketManager is null, skipping", LogLevel.Trace);
                return;
            }
            
            var instruments = _marketManager.GetInstruments();
            if (instruments == null)
            {
                _monitor?.Log("[IntradayNews] No instruments available, skipping", LogLevel.Trace);
                return;
            }
                
            var availableCommodities = instruments
                .OfType<CommodityFutures>()
                .Select(f => f.CommodityName)
                .Distinct()
                .ToList();
            
            int currentDay = _timeCalculator.GetAbsoluteDay();
            double currentTimeRatio = _clock.GetDayProgress();
            
            var breakingNews = _newsGenerator.GenerateIntradayNews(currentDay, currentTimeRatio, availableCommodities);
            
            if (breakingNews == null)
                return;
            
            // 6. 添加到历史和生效列表
            newsHistory.Add(breakingNews);
            activeNewsEffects.Add(breakingNews);
            
            _lastIntradayNewsTriggeredTick = currentTick;
            
            _monitor.Log(
                $"[BREAKING NEWS] {breakingNews.Title} ({breakingNews.Scope.AffectedItems.FirstOrDefault() ?? "N/A"}) | " +
                $"D:{breakingNews.Impact.DemandImpact:+0;-0;0} S:{breakingNews.Impact.SupplyImpact:+0;-0;0}",
                LogLevel.Warn
            );
            
            // 7. 动态更新目标价格（核心逻辑）
            UpdateTargetPricesForBreakingNews(breakingNews, dailyTargets, activeNewsEffects);
        }
        
        /// <summary>
        /// 根据突发新闻动态更新目标价格
        /// 核心机制：仅更新 P_target，无需重置 τ 或 P_start
        /// 布朗桥的引力项 (P_target - P_τ) 将自动调整，实现自然收敛
        /// </summary>
        private void UpdateTargetPricesForBreakingNews(
            NewsEvent breakingNews,
            Dictionary<string, double> dailyTargets,
            List<NewsEvent> activeNewsEffects)
        {
            var currentSeason = _timeCalculator.GetCurrentSeason();
            
            // 防御性检查：确保MarketManager已初始化
            if (_marketManager == null)
            {
                _monitor?.Log("[IntradayNews] MarketManager is null in UpdateTargetPrices, skipping", LogLevel.Trace);
                return;
            }
            
            var instruments2 = _marketManager.GetInstruments();
            if (instruments2 == null)
            {
                _monitor?.Log("[IntradayNews] No instruments in UpdateTargetPrices, skipping", LogLevel.Trace);
                return;
            }
                
            foreach (var instrument in instruments2)
            {
                if (instrument is not CommodityFutures futures)
                    continue;
                
                // 检查新闻是否影响此商品
                bool isAffected = breakingNews.Scope.IsGlobal ||
                                 breakingNews.Scope.AffectedItems.Contains(futures.CommodityName);
                
                if (!isAffected)
                    continue;
                
                // 1. 重新计算基本面价值（包含新闻影响）
                double newFundamentalValue = _fundamentalEngine.CalculateFundamentalValue(
                    commodityName: futures.CommodityName,
                    currentSeason: currentSeason,
                    newsHistory: activeNewsEffects  // 已包含新触发的新闻
                );
                
                // 2. 计算新的日内目标价格
                double newTarget = _priceEngine.CalculateDailyTarget(
                    futures.CurrentPrice, 
                    newFundamentalValue, 
                    28
                );
                
                // 3. 更新目标价格（事件驱动的核心操作）
                double oldTarget = dailyTargets[futures.Symbol];
                dailyTargets[futures.Symbol] = newTarget;
                
                _monitor.Log(
                    $"[Target Updated] {futures.Symbol}: {oldTarget:F2}g → {newTarget:F2}g " +
                    $"(Δ={newTarget - oldTarget:+0.00;-0.00}g, Current={futures.CurrentPrice:F2}g)",
                    LogLevel.Info
                );
                
                // 4. 可选：更新订单簿深度
                var scenarioType = _scenarioManager.GetCurrentScenario();
                var config = _marketManager.GetCommodityConfig(futures.CommodityName);
                if (config != null)
                {
                    _orderBookManager.RegenerateDepth(
                        futures.Symbol,
                        (decimal)newTarget,
                        scenarioType.ToString(),
                        config.LiquiditySensitivity
                    );
                }
            }
        }
    }
}
