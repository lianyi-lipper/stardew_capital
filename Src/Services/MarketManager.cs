using System;
using System.Collections.Generic;
using StardewCapital.Core.Time;
using StardewCapital.Domain.Instruments;
using StardewCapital.Domain.Market;
using StardewModdingAPI;
using StardewValley;

namespace StardewCapital.Services
{
    /// <summary>
    /// 市场管理器
    /// 协调所有市场相关服务，管理金融产品列表，驱动价格更新。
    /// 
    /// 核心职责：
    /// - 管理所有可交易的金融产品（期货、股票等）
    /// - 协调价格引擎和基本面引擎进行价格更新
    /// - 维护每日目标价格和新闻历史
    /// - 处理新一天的市场初始化
    /// </summary>
    public class MarketManager
    {
        private readonly IMonitor _monitor;
        private readonly MixedTimeClock _clock;
        private readonly PriceEngine _priceEngine;
        private readonly FundamentalEngine _fundamentalEngine;
        private readonly ConvenienceYieldService _convenienceYieldService;
        private readonly NewsGenerator _newsGenerator;
        private readonly ModConfig _config;
        
        private List<IInstrument> _instruments;
        private Dictionary<string, double> _dailyTargets; // Symbol -> 目标价格
        
        /// <summary>
        /// 新闻事件完整历史列表（永久保存，供UI查看）
        /// 存储所有新闻事件，不会在新季节清空
        /// </summary>
        private List<NewsEvent> _newsHistory;
        
        /// <summary>
        /// 生效新闻列表（每季重置，用于价格计算）
        /// 只包含当前季节生效的新闻，用于FundamentalEngine计算
        /// </summary>
        private List<NewsEvent> _activeNewsEffects;

        private int _lastUpdateTick = 0;
        private const int UPDATE_INTERVAL_TICKS = 60; // 每60个tick更新一次价格（约1秒）

        public MarketManager(
            IMonitor monitor, 
            MixedTimeClock clock, 
            PriceEngine priceEngine, 
            FundamentalEngine fundamentalEngine,
            ConvenienceYieldService convenienceYieldService,
            NewsGenerator newsGenerator,
            ModConfig config)
        {
            _monitor = monitor;
            _clock = clock;
            _priceEngine = priceEngine;
            _fundamentalEngine = fundamentalEngine;
            _convenienceYieldService = convenienceYieldService;
            _newsGenerator = newsGenerator;
            _config = config;
            
            _instruments = new List<IInstrument>();
            _dailyTargets = new Dictionary<string, double>();
            _newsHistory = new List<NewsEvent>();
            _activeNewsEffects = new List<NewsEvent>();
        }

        /// <summary>
        /// 初始化市场，创建默认的金融产品
        /// 当前阶段：硬编码创建测试用的防风草期货
        /// </summary>
        public void InitializeMarket()
        {
            // 阶段2：硬编码创建测试产品
            var parsnipFutures = new CommodityFutures("24", "Parsnip", "Spring", 28, 35.0);
            _instruments.Add(parsnipFutures);
            
            // 设置初始目标价（测试用，假设收盘价为40）
            _dailyTargets[parsnipFutures.Symbol] = 40.0;
            
            _monitor.Log($"[Market] Initialized with {parsnipFutures.Symbol} @ {parsnipFutures.CurrentPrice}g", LogLevel.Info);
        }

        /// <summary>
        /// 处理新一天开始的逻辑
        /// - 将昨天的价格收敛到目标价
        /// - 计算今天的新目标价（使用 FundamentalEngine）
        /// - 计算期货价格（使用 PriceEngine + ConvenienceYieldService）
        /// </summary>
        public void OnNewDay()
        {
            // ========== 新闻系统逻辑 ==========
            
            // 1. 检测新季节 - 清空生效新闻列表
            if (Game1.dayOfMonth == 1)
            {
                _activeNewsEffects.Clear();
                _monitor.Log("[News] New season started, cleared active news effects", LogLevel.Info);
            }
            
            // 2. 生成今日新闻
            var availableCommodities = _instruments
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
            
            foreach (var instrument in _instruments)
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
                    // 传入 _activeNewsEffects 以计算当前生效新闻对供需的影响
                    fundamentalValue = _fundamentalEngine.CalculateFundamentalValue(
                        commodityName: futures.CommodityName,
                        currentSeason: currentSeason,
                        newsHistory: _activeNewsEffects // 使用生效新闻列表（每季重置）
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
                
                double newTarget = _priceEngine.CalculateDailyTarget(instrument.CurrentPrice, fundamentalValue, 28); // 假设28天到期
                
                _dailyTargets[instrument.Symbol] = newTarget;
                
                _monitor.Log($"[Market] New Day: {instrument.Symbol} Open: {instrument.CurrentPrice:F2}g, Target: {newTarget:F2}g (Fundamental: {fundamentalValue:F2}g)", LogLevel.Info);
            }
        }

        /// <summary>
        /// 获取当前游戏季节（转换为 CommodityConfig 的 Season 枚举）
        /// </summary>
        /// <returns>当前季节枚举值</returns>
        /// <remarks>
        /// 将 Stardew Valley 的季节字符串（"spring", "summer", "fall", "winter"）
        /// 转换为 Domain.Market.Season 枚举
        /// </remarks>
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
        /// <param name="futures">期货合约</param>
        /// <returns>剩余天数（最少为1天）</returns>
        /// <remarks>
        /// WHY (为什么这样实现):
        /// 当前简化版本仅支持同季节内的合约（例如：春季开仓，春28交割）。
        /// 跨季节合约（例如：春季开仓，夏28交割）需要复杂的日历计算。
        /// 
        /// 简化逻辑：
        /// - 如果当前日期 \u003c 交割日期：正常计算剩余天数
        /// - 如果当前日期 = 交割日期：返回1天（即将交割）
        /// - 如果当前日期 \u003e 交割日期：返回1天（合约已到期，fallback）
        /// 
        /// 未来优化（见 task.md 未来优化项）：
        /// - 创建 DateUtils.cs 日历计算工具
        /// - 支持跨季节合约（例如：春1 -\u003e 秋28 = 3×28 天）
        /// </remarks>
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
        /// <returns>绝对日期：春1=1, 春28=28, 夏1=29, 夏28=56, 秋1=57...</returns>
        /// <remarks>
        /// WHY (为什么需要这个方法):
        /// 新闻事件需要跨季节的绝对日期来判断生效期和过期时间。
        /// 例如：春28天发布的新闻，生效期为28天，会延续到夏季。
        /// 
        /// 计算公式：
        /// 绝对日期 = (季节索引 × 28) + 当前日期
        /// </remarks>
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

            // 更新所有产品的价格
            foreach (var instrument in _instruments)
            {
                if (_dailyTargets.TryGetValue(instrument.Symbol, out double target))
                {
                    _priceEngine.UpdatePrice(instrument, target);
                }
            }
        }
        
        /// <summary>
        /// 获取所有可交易的金融产品列表
        /// </summary>
        /// <returns>金融产品列表</returns>
        public List<IInstrument> GetInstruments()
        {
            return _instruments;
        }

        /// <summary>
        /// 获取完整新闻历史列表（用于UI显示）
        /// </summary>
        /// <returns>所有新闻事件列表</returns>
        public List<Domain.Market.NewsEvent> GetNewsHistory()
        {
            return _newsHistory;
        }

        /// <summary>
        /// 获取当前生效的新闻列表（用于UI显示）
        /// </summary>
        /// <returns>生效中的新闻事件列表</returns>
        public List<Domain.Market.NewsEvent> GetActiveNews()
        {
            return _activeNewsEffects;
        }
    }
}
