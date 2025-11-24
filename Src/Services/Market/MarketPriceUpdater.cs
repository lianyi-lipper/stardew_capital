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

namespace StardewCapital.Services.Market
{
    /// <summary>
    /// 市场价格更新器
    /// 负责所有价格计算和更新逻辑。
    /// 
    /// 核心职责：
    /// - 每帧更新市场价格（布朗桥 + 市场冲击）
    /// - 处理新一天的价格初始化
    /// - 虚拟流量处理（订单簿碰撞检测）
    /// - 新闻系统和季节管理
    /// </summary>
    public class MarketPriceUpdater
    {
        private readonly IMonitor _monitor;
        private readonly MixedTimeClock _clock;
        private readonly PriceEngine _priceEngine;
        private readonly FundamentalEngine _fundamentalEngine;
        private readonly ConvenienceYieldService _convenienceYieldService;
        private readonly NewsGenerator _newsGenerator;
        private readonly ImpactService _impactService;
        private readonly ScenarioManager _scenarioManager;
        private readonly ModConfig _config;
        private readonly MarketManager _marketManager;
        private readonly OrderBookManager _orderBookManager;

        private Dictionary<string, double> _dailyTargets; // Symbol -> 目标价格
        
        /// <summary>
        /// 新闻事件完整历史列表（永久保存，供UI查看）
        /// </summary>
        private List<NewsEvent> _newsHistory;
        
        /// <summary>
        /// 生效新闻列表（每季重置，用于价格计算）
        /// </summary>
        private List<NewsEvent> _activeNewsEffects;

        private int _lastUpdateTick = 0;
        private const int UPDATE_INTERVAL_TICKS = 60; // 每60个tick更新一次价格（约1秒）

        public MarketPriceUpdater(
            IMonitor monitor,
            MixedTimeClock clock,
            PriceEngine priceEngine,
            FundamentalEngine fundamentalEngine,
            ConvenienceYieldService convenienceYieldService,
            NewsGenerator newsGenerator,
            ImpactService impactService,
            ScenarioManager scenarioManager,
            ModConfig config,
            MarketManager marketManager,
            OrderBookManager orderBookManager)
        {
            _monitor = monitor;
            _clock = clock;
            _priceEngine = priceEngine;
            _fundamentalEngine = fundamentalEngine;
            _convenienceYieldService = convenienceYieldService;
            _newsGenerator = newsGenerator;
            _impactService = impactService;
            _scenarioManager = scenarioManager;
            _config = config;
            _marketManager = marketManager;
            _orderBookManager = orderBookManager;
            
            _dailyTargets = new Dictionary<string, double>();
            _newsHistory = new List<NewsEvent>();
            _activeNewsEffects = new List<NewsEvent>();
        }

        /// <summary>
        /// 处理新一天开始的逻辑
        /// - 将昨天的价格收敛到目标价
        /// - 计算今天的新目标价（使用 FundamentalEngine）
        /// - 计算期货价格（使用 PriceEngine + ConvenienceYieldService）
        /// </summary>
        public void OnNewDay()
        {
            // ========== 市场剧本切换 ==========
            _scenarioManager.OnNewDay();
            
            // ========== 新闻系统逻辑 ==========
            
            // 1. 检测新季节 - 清空生效新闻列表
            if (Game1.dayOfMonth == 1)
            {
                _activeNewsEffects.Clear();
                _monitor.Log("[News] New season started, cleared active news effects", LogLevel.Info);
            }
            
            // 2. 生成今日新闻
            var availableCommodities = _marketManager.GetInstruments()
                .OfType<CommodityFutures>()
                .Select(f => f.CommodityName)
                .Distinct()
                .ToList();
            
            int currentDay = GetAbsoluteDay(); // 绝对日期（春1=1，春28=28，夏1=29...）
            var todayNews = _newsGenerator.GenerateDailyNews(currentDay, availableCommodities);
            
            // 3. 添加到历史列表和生效列表
            foreach (var news in todayNews)
            {
                _newsHistory.Add(news);
                _activeNewsEffects.Add(news);
                
                _monitor.Log(
                    $"[News] {news.Title} ({news.Scope.AffectedItems.FirstOrDefault() ?? "N/A"}) | " +
                    $"D:{news.Impact.DemandImpact:+0;-0;0} S:{news.Impact.SupplyImpact:+0;-0;0}",
                    LogLevel.Info
                );
            }
            
            // 4. 过滤过期新闻（不再生效的）
            int beforeCount = _activeNewsEffects.Count;
            _activeNewsEffects.RemoveAll(n => !n.Timing.IsEffectiveOn(currentDay));
            int removedCount = beforeCount - _activeNewsEffects.Count;
            
            if (removedCount > 0)
            {
                _monitor.Log($"[News] Removed {removedCount} expired news from active effects", LogLevel.Info);
            }
            
            // ========== 价格计算逻辑 ==========
            
            // 获取当前季节（从 Stardew Valley 游戏状态）
            var currentSeason = GetCurrentSeason();
            
            foreach (var instrument in _marketManager.GetInstruments())
            {
                // 1. 收敛到昨天的目标价（模拟隔夜波动）
                if (_dailyTargets.TryGetValue(instrument.Symbol, out double prevTarget))
                {
                    instrument.CurrentPrice = prevTarget;
                }

                // 2. 计算今天的新目标价（使用基本面引擎）
                double fundamentalValue = 35.0; // 默认值，用于非CommodityFutures类型
                
                if (instrument is CommodityFutures futures)
                {
                    // 2.1 使用 FundamentalEngine 计算现货基本面价值（S_t）
                    fundamentalValue = _fundamentalEngine.CalculateFundamentalValue(
                        commodityName: futures.CommodityName,
                        currentSeason: currentSeason,
                        newsHistory: _activeNewsEffects
                    );

                    // 2.2 计算距离交割日的天数
                    int daysToMaturity = CalculateDaysToMaturity(futures);

                    // 2.3 计算便利收益率（q）
                    double convenienceYield = _convenienceYieldService.GetConvenienceYield(
                        itemId: futures.UnderlyingItemId,
                        baseYield: _config.BaseConvenienceYield
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
                
                _dailyTargets[instrument.Symbol] = newTarget;
                
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

        /// <summary>
        /// 每帧更新市场价格
        /// 使用节流机制，避免过于频繁的更新
        /// </summary>
        /// <param name="currentTick">当前tick计数</param>
        public void Update(int currentTick)
        {
            // 节流：只在指定间隔后更新
            if (currentTick - _lastUpdateTick < UPDATE_INTERVAL_TICKS) return;
            _lastUpdateTick = currentTick;

            // 如果游戏暂停或市场关闭，停止更新
            if (_clock.IsPaused() || !_clock.IsMarketOpen()) return;

            // 获取当前市场剧本参数和季节
            var scenarioParams = _scenarioManager.GetCurrentParameters();
            var currentSeason = GetCurrentSeason();

            // 更新所有产品的价格
            foreach (var instrument in _marketManager.GetInstruments())
            {
                if (_dailyTargets.TryGetValue(instrument.Symbol, out double target))
                {
                    // 1. 更新日内价格（模型四：布朗桥）
                    _priceEngine.UpdatePrice(instrument, target);
                    
                    // 2. 叠加市场冲击（模型五）
                    if (instrument is CommodityFutures futures)
                    {
                        // 获取基本面价值（用于聪明钱回归计算）
                        double fundamentalValue = _fundamentalEngine.CalculateFundamentalValue(
                            commodityName: futures.CommodityName,
                            currentSeason: currentSeason,
                            newsHistory: _activeNewsEffects
                        );
                        
                        // 更新冲击值
                        _impactService.UpdateImpact(
                            commodityId: futures.UnderlyingItemId,
                            currentPrice: instrument.CurrentPrice,
                            fundamentalPrice: fundamentalValue,
                            scenario: scenarioParams
                        );
                        
                        // 叠加冲击值到最终价格 P_Final = P_Model + I(t)
                        double impact = _impactService.GetCurrentImpact(futures.UnderlyingItemId);
                        instrument.CurrentPrice += impact;
                    }
                }
            }
            
            // ========== 虚拟流量处理（订单簿碰撞检测） ==========
            var currentScenarioType = _scenarioManager.GetCurrentScenario();
            ProcessVirtualFlow(currentScenarioType.ToString());
        }

        /// <summary>
        /// 获取新闻历史列表
        /// </summary>
        public List<NewsEvent> GetNewsHistory() => _newsHistory;

        /// <summary>
        /// 获取生效新闻列表
        /// </summary>
        public List<NewsEvent> GetActiveNews() => _activeNewsEffects;

        /// <summary>
        /// 获取当前游戏季节（转换为 CommodityConfig 的 Season 枚举）
        /// </summary>
        private Domain.Market.Season GetCurrentSeason()
        {
            string currentSeason = Game1.currentSeason;
            
            return currentSeason.ToLower() switch
            {
                "spring" => Domain.Market.Season.Spring,
                "summer" => Domain.Market.Season.Summer,
                "fall" => Domain.Market.Season.Fall,
                "winter" => Domain.Market.Season.Winter,
                _ => Domain.Market.Season.Spring // 默认春季
            };
        }

        /// <summary>
        /// 计算距离交割日的剩余天数
        /// </summary>
        private int CalculateDaysToMaturity(CommodityFutures futures)
        {
            int currentDay = Game1.dayOfMonth;
            int deliveryDay = futures.DeliveryDay;
            
            // 简化计算：假设都在同一季节
            int daysRemaining = deliveryDay - currentDay;
            
            // 如果已经过了交割日或到达交割日，返回1天（最小值）
            return Math.Max(1, daysRemaining);
        }

        /// <summary>
        /// 计算绝对日期（从春季第1天开始计数）
        /// </summary>
        private int GetAbsoluteDay()
        {
            string season = Game1.currentSeason;
            int dayOfMonth = Game1.dayOfMonth;
            
            int seasonIndex = season.ToLower() switch
            {
                "spring" => 0,
                "summer" => 1,
                "fall" => 2,
                "winter" => 3,
                _ => 0
            };
            
            return (seasonIndex * 28) + dayOfMonth;
        }

        /// <summary>
        /// 处理虚拟流量（订单簿碰撞检测）
        /// </summary>
        private void ProcessVirtualFlow(string scenarioType)
        {
            foreach (var instrument in _marketManager.GetInstruments())
            {
                if (instrument is not CommodityFutures futures) continue;
                
                // 获取订单簿
                var orderBook = _orderBookManager.GetOrderBook(futures.Symbol);
                if (orderBook == null)
                    continue;
                
                // 1. 获取理论目标价（来自价格引擎 + 冲击层）
                decimal targetPrice = (decimal)futures.CurrentPrice;
                
                // 2. 获取当前盘口中间价
                decimal midPrice = orderBook.GetMidPrice();
                
                // 如果订单簿为空（无深度），先生成NPC深度
                if (midPrice == 0)
                {
                    var config = _marketManager.GetCommodityConfig(futures.CommodityName);
                    if (config != null)
                    {
                        _orderBookManager.RegenerateDepth(
                            futures.Symbol,
                            targetPrice,
                            scenarioType,
                            config.LiquiditySensitivity
                        );
                    }
                    continue;
                }
                
                // 3. 计算价差
                decimal priceDiff = targetPrice - midPrice;
                
                // 如果价差小于阈值，无需虚拟流量（避免过度撮合）
                if (Math.Abs(priceDiff) < 0.1m)
                    continue;
                
                // 4. 计算虚拟流量数量（价差越大，流量越大）
                bool isBuyPressure = priceDiff > 0; // 目标价 > 中间价，需要买压推高价格
                int flowQuantity = CalculateFlowQuantity(priceDiff);
                
                // 5. 虚拟流量撞击订单簿
                var (vwap, slippage) = orderBook.ExecuteMarketOrder(isBuyPressure, flowQuantity);
                
                // 6. 日志输出（调试用）
                if (flowQuantity > 0 && vwap > 0)
                {
                    _monitor.Log(
                        $"[OrderBook] {futures.Symbol}: VirtualFlow {(isBuyPressure ? "BUY" : "SELL")} {flowQuantity} @ VWAP={vwap:F2}g, Slippage={slippage:F2}g",
                        LogLevel.Debug
                    );
                }
            }
        }

        /// <summary>
        /// 计算虚拟流量数量
        /// </summary>
        private int CalculateFlowQuantity(decimal priceDiff)
        {
            // 价差越大，流量越大（非线性关系）
            decimal absDiff = Math.Abs(priceDiff);
            
            if (absDiff < 0.5m)
                return 10;
            if (absDiff < 1.0m)
                return 25;
            if (absDiff < 2.0m)
                return 50;
            
            return 100; // 极端价差，强力流量
        }

        /// <summary>
        /// 获取商品配置
        /// </summary>
        public CommodityConfig? GetCommodityConfig(string commodityName)
        {
            return _fundamentalEngine.GetCommodityConfig(commodityName);
        }

        /// <summary>
        /// 获取所有商品配置列表
        /// </summary>
        public List<CommodityConfig> GetAllCommodityConfigs()
        {
            return _fundamentalEngine.GetAllCommodityConfigs();
        }

        /// <summary>
        /// 设置初始目标价格（用于市场初始化）
        /// </summary>
        public void SetInitialTarget(string symbol, double target)
        {
            _dailyTargets[symbol] = target;
        }

        /// <summary>
        /// 获取历史冲击值
        /// </summary>
        public List<double> GetImpactHistory(string commodityId)
        {
            return _impactService.GetImpactHistory(commodityId);
        }
    }
}
