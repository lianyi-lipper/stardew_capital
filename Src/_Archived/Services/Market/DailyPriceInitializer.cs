using System;
using System.Collections.Generic;
using System.Linq;
using StardewCapital.Core.Time;
using StardewCapital.Domain.Instruments;
using StardewCapital.Domain.Market;
using StardewCapital.Services.Pricing;
using StardewCapital.Services.News;
using StardewModdingAPI;
using StardewValley;
using StardewCapital.Config;

namespace StardewCapital.Services.Market
{
    /// <summary>
    /// 新一天价格初始化服务
    /// 负责处理新一天开始的逻辑：新闻生成、价格收敛、基本面计算
    /// </summary>
    public class DailyPriceInitializer
    {
        private readonly IMonitor _monitor;
        private readonly PriceEngine _priceEngine;
        private readonly FundamentalEngine _fundamentalEngine;
        private readonly ConvenienceYieldService _convenienceYieldService;
        private readonly NewsGenerator _newsGenerator;
        private readonly ScenarioManager _scenarioManager;
        private readonly MarketManager _marketManager;
        private readonly OrderBookManager _orderBookManager;
        private readonly MarketRules _rules;
        private readonly MarketTimeCalculator _timeCalculator;

        public DailyPriceInitializer(
            IMonitor monitor,
            PriceEngine priceEngine,
            FundamentalEngine fundamentalEngine,
            ConvenienceYieldService convenienceYieldService,
            NewsGenerator newsGenerator,
            ScenarioManager scenarioManager,
            MarketManager marketManager,
            OrderBookManager orderBookManager,
            MarketRules rules,
            MarketTimeCalculator timeCalculator)
        {
            _monitor = monitor;
            _priceEngine = priceEngine;
            _fundamentalEngine = fundamentalEngine;
            _convenienceYieldService = convenienceYieldService;
            _newsGenerator = newsGenerator;
            _scenarioManager = scenarioManager;
            _marketManager = marketManager;
            _orderBookManager = orderBookManager;
            _rules = rules;
            _timeCalculator = timeCalculator;
        }

        /// <summary>
        /// 处理新一天开始的逻辑
        /// - 将昨天的价格收敛到目标价
        /// - 计算今天的新目标价（使用 FundamentalEngine）
        /// - 计算期货价格（使用 PriceEngine + ConvenienceYieldService）
        /// </summary>
        public void OnNewDay(
            Dictionary<string, double> dailyTargets,
            List<NewsEvent> newsHistory,
            List<NewsEvent> activeNewsEffects)
        {
            // ========== 市场剧本切换 ==========
            _scenarioManager.OnNewDay();
            
            // ========== 新闻系统逻辑 ==========
            
            // 1. 检测新季节 - 清空生效新闻列表
            if (Game1.dayOfMonth == 1)
            {
                activeNewsEffects.Clear();
                _monitor.Log("[News] New season started, cleared active news effects", LogLevel.Info);
            }
            
            // 2. 生成今日新闻
            // 防御性检查：确保MarketManager已初始化
            if (_marketManager == null)
            {
                _monitor?.Log("[DailyInit] MarketManager is null, skipping", LogLevel.Warn);
                return;
            }
            
            var instruments = _marketManager.GetInstruments();
            if (instruments == null)
            {
                _monitor?.Log("[DailyInit] No instruments available, skipping", LogLevel.Warn);
                return;
            }
                
            var availableCommodities = instruments
                .OfType<CommodityFutures>()
                .Select(f => f.CommodityName)
                .Distinct()
                .ToList();
            
            int currentDay = _timeCalculator.GetAbsoluteDay();
            var todayNews = _newsGenerator.GenerateDailyNews(currentDay, availableCommodities);
            
            // 3. 添加到历史列表和生效列表
            foreach (var news in todayNews)
            {
                newsHistory.Add(news);
                activeNewsEffects.Add(news);
                
                _monitor.Log(
                    $"[News] {news.Title} ({news.Scope.AffectedItems.FirstOrDefault() ?? "N/A"}) | " +
                    $"D:{news.Impact.DemandImpact:+0;-0;0} S:{news.Impact.SupplyImpact:+0;-0;0}",
                    LogLevel.Info
                );
            }
            
            // 4. 过滤过期新闻（不再生效的）
            int beforeCount = activeNewsEffects.Count;
            activeNewsEffects.RemoveAll(n => !n.Timing.IsEffectiveOn(currentDay));
            int removedCount = beforeCount - activeNewsEffects.Count;
            
            if (removedCount > 0)
            {
                _monitor.Log($"[News] Removed {removedCount} expired news from active effects", LogLevel.Info);
            }
            
            // ========== 价格计算逻辑 ==========
            
            // 获取当前季节（从 Stardew Valley 游戏状态）
            var currentSeason = _timeCalculator.GetCurrentSeason();
            
            foreach (var instrument in instruments)
            {
                // 1. 收敛到昨天的目标价（模拟隔夜波动）
                if (dailyTargets.TryGetValue(instrument.Symbol, out double prevTarget))
                {
                    instrument.CurrentPrice = prevTarget;
                }

                // 1.5 处理隔夜跳空开盘（熔断机制）
                if (instrument is CommodityFutures futuresGap && futuresGap.Gap != 0.0)
                {
                    instrument.CurrentPrice += futuresGap.Gap;
                    
                    _monitor.Log(
                        $"[Gap Opening] {instrument.Symbol}: Gap={futuresGap.Gap:+0.00;-0.00}g applied, " +
                        $"Final Open={instrument.CurrentPrice:F2}g",
                        LogLevel.Warn
                    );
                    
                    // 清零 Gap 和熔断标志
                    futuresGap.Gap = 0.0;
                    futuresGap.CircuitBreakerActive = false;
                }

                // 2. 计算今天的新目标价（使用基本面引擎）
                double fundamentalValue = 35.0; // 默认值，用于非CommodityFutures类型
                
                if (instrument is CommodityFutures futures)
                {
                    // 2.1 使用 FundamentalEngine 计算现货基本面价值（S_t）
                    fundamentalValue = _fundamentalEngine.CalculateFundamentalValue(
                        commodityName: futures.CommodityName,
                        currentSeason: currentSeason,
                        newsHistory: activeNewsEffects
                    );

                    // 2.2 计算距离交割日的天数
                    int daysToMaturity = _timeCalculator.CalculateDaysToMaturity(futures);

                    // 2.3 计算便利收益率（q）
                    double convenienceYield = _convenienceYieldService.GetConvenienceYield(
                        itemId: futures.UnderlyingItemId,
                        baseYield: _rules.Instruments.Futures.BaseConvenienceYield
                    );

                    // 2.4 计算期货价格（F_t），使用持有成本模型
                    futures.FuturesPrice = _priceEngine.CalculateFuturesPrice(
                        spotPrice: fundamentalValue,
                        daysToMaturity: daysToMaturity,
                        convenienceYield: convenienceYield
                    );

                    // 2.5 更新现货价格（S_t）
                    futures.CurrentPrice = fundamentalValue;

                    // 2.6 记录开盘价（用于涨跌幅计算）
                    futures.OpenPrice = fundamentalValue;

                    // 日志输出：基差分析
                    double basis = futures.FuturesPrice - futures.CurrentPrice;
                    string basisType = basis > 0 ? "Contango(升水)" : "Backwardation(贴水)";
                    _monitor.Log(
                        $"[Market] {futures.Symbol}: " +
                        $"Spot={futures.CurrentPrice:F2}g, Futures={futures.FuturesPrice:F2}g, " +
                        $"Basis={basis:F2}g ({basisType}), DaysToMaturity={daysToMaturity}, " +
                        $"ConvYield={convenienceYield:F4}",
                        LogLevel.Debug
                    );
                }
                
                double newTarget = _priceEngine.CalculateDailyTarget(instrument.CurrentPrice, fundamentalValue, 28);
                
                dailyTargets[instrument.Symbol] = newTarget;
                
                _monitor.Log($"[Market] New Day: {instrument.Symbol} Open: {instrument.CurrentPrice:F2}g, Target: {newTarget:F2}g (Fundamental: {fundamentalValue:F2}g)", LogLevel.Info);
                
                // ========== 初始化订单簿NPC深度 ==========
                var scenarioType = _scenarioManager.GetCurrentScenario();
                var scenarioTypeName = scenarioType.ToString();
                
                if (instrument is CommodityFutures futuresInst)
                {
                    var config = _marketManager.GetCommodityConfig(futuresInst.CommodityName);
                    if (config != null)
                    {
                        _orderBookManager.RegenerateDepth(
                            futuresInst.Symbol,
                            (decimal)newTarget,
                            scenarioTypeName,
                            config.LiquiditySensitivity
                        );
                    }
                }
            }
        }
    }
}
