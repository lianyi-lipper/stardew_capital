using System;
using System.Collections.Generic;
using System.Linq;
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
        private readonly ImpactService _impactService;
        private readonly ScenarioManager _scenarioManager;
        private readonly ModConfig _config;
        private BrokerageService? _brokerageService; // ✅ 用于订单结算回调
        
        private List<IInstrument> _instruments;
        private Dictionary<string, double> _dailyTargets; // Symbol -> 目标价格
        
        /// <summary>
        /// 订单簿集合（每个期货商品维护独立的订单簿）
        /// Key = Symbol (例如 "PARSNIP-SPR-28"), Value = 该商品的订单簿实例
        /// </summary>
        private Dictionary<string, OrderBook> _orderBooks;
        
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
            ImpactService impactService,
            ScenarioManager scenarioManager,
            ModConfig config)
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
            
            _instruments = new List<IInstrument>();
            _dailyTargets = new Dictionary<string, double>();
            _orderBooks = new Dictionary<string, OrderBook>();
            _newsHistory = new List<NewsEvent>();
            _activeNewsEffects = new List<NewsEvent>();
        }

        /// <summary>
        /// 设置 BrokerageService 引用（用于订单结算回调）
        /// </summary>
        /// <param name="brokerageService">经纪服务实例</param>
        /// <remarks>
        /// WHY（为什么不在构造函数注入）：
        /// MarketManager 和 BrokerageService 存在循环依赖：
        /// - MarketManager 需要通知 BrokerageService 订单成交
        /// - BrokerageService 需要访问 MarketManager 的订单簿
        /// 使用 Setter 注入打破循环依赖。
        /// </remarks>
        public void SetBrokerageService(BrokerageService brokerageService)
        {
            _brokerageService = brokerageService;
            
            // 为所有现有订单簿订阅事件
            foreach (var orderBook in _orderBooks.Values)
            {
                SubscribeToOrderBook(orderBook);
            }
        }

        /// <summary>
        /// 订阅订单簿的玩家成交事件
        /// </summary>
        private void SubscribeToOrderBook(OrderBook orderBook)
        {
            orderBook.OnPlayerOrderFilled += (fillInfo) =>
            {
                // 转发到 BrokerageService 进行资金结算
                _brokerageService?.HandlePlayerOrderFilled(fillInfo);
                
                // ========== 🔥 问题1修复：记录被动成交的市场冲击 ==========
                // 当玩家限价单（Maker）被虚拟流量吃掉时，视为真实成交量
                // 需要计入市场冲击系统，影响后续价格
                
                // 获取期货合约信息
                var instrument = _instruments.FirstOrDefault(i => i.Symbol == fillInfo.Symbol);
                if (instrument is CommodityFutures futures)
                {
                    // 获取商品配置（流动性敏感度）
                    var config = GetCommodityConfig(futures.CommodityName);
                    if (config != null)
                    {
                        // ⚠️ 注意方向：
                        // - 玩家买单被吃 → 市场有卖压 → 负冲击（压价）
                        // - 玩家卖单被吃 → 市场有买压 → 正冲击（推价）
                        // 因此需要**反转方向**
                        int impactQuantity = fillInfo.IsBuy 
                            ? -fillInfo.FillQuantity  // 买单被吃 = 市场卖出
                            : +fillInfo.FillQuantity; // 卖单被吃 = 市场买入
                        
                        _impactService.RecordPlayerTrade(
                            commodityId: futures.UnderlyingItemId,
                            quantity: impactQuantity,
                            liquiditySensitivity: config.LiquiditySensitivity
                        );
                        
                        _monitor.Log(
                            $"[Impact] Passive fill: {fillInfo.Symbol} {(fillInfo.IsBuy ? "BUY" : "SELL")} " +
                            $"{fillInfo.FillQuantity} → Impact qty={impactQuantity}",
                            LogLevel.Debug
                        );
                    }
                }
            };
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
            
            // ========== Phase 10: 为每个期货创建订单簿 ==========
            _orderBooks[parsnipFutures.Symbol] = new OrderBook(parsnipFutures.Symbol);
            
            // ✅ 订阅订单簿事件（用于结算回调）
            if (_brokerageService != null)
            {
                SubscribeToOrderBook(_orderBooks[parsnipFutures.Symbol]);
            }
            
            // 添加以下3行:
            var scenarioType = _scenarioManager.GetCurrentScenario();
            _orderBooks[parsnipFutures.Symbol].GenerateNPCDepth(
                (decimal)parsnipFutures.CurrentPrice, scenarioType.ToString());

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
                
                // ========== Phase 10: 初始化订单簿NPC深度 ==========
                if (_orderBooks.TryGetValue(instrument.Symbol, out var orderBook))
                {
                    var scenarioType = _scenarioManager.GetCurrentScenario();
                    var scenarioTypeName = scenarioType.ToString();
                    orderBook.GenerateNPCDepth((decimal)newTarget, scenarioTypeName);
                }
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

            // 获取当前市场剧本参数和季节
            var scenarioParams = _scenarioManager.GetCurrentParameters();
            var currentSeason = GetCurrentSeason();

            // 更新所有产品的价格
            foreach (var instrument in _instruments)
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
            
            // ========== Phase 10: 虚拟流量处理（订单簿碰撞检测） ==========
            var currentScenarioType = _scenarioManager.GetCurrentScenario();
            ProcessVirtualFlow(currentScenarioType.ToString());
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

        /// <summary>
        /// 获取商品配置（用于获取流动性参数等）
        /// </summary>
        /// <param name="commodityName">商品名称或ItemId</param>
        /// <returns>商品配置，如果不存在返回null</returns>
        public CommodityConfig? GetCommodityConfig(string commodityName)
        {
            return _fundamentalEngine.GetCommodityConfig(commodityName);
        }

        /// <summary>
        /// 获取指定期货的订单簿
        /// </summary>
        /// <param name="symbol">合约代码（例如："PARSNIP-SPR-28"）</param>
        /// <returns>订单簿实例，如果不存在返回null</returns>
        public OrderBook? GetOrderBook(string symbol)
        {
            return _orderBooks.TryGetValue(symbol, out var orderBook) ? orderBook : null;
        }

        /// <summary>
        /// 获取所有订单簿（用于Web UI显示）
        /// </summary>
        /// <returns>订单簿列表</returns>
        public List<OrderBook> GetAllOrderBooks()
        {
            return _orderBooks.Values.ToList();
        }

        /// <summary>
        /// 处理虚拟流量（订单簿碰撞检测）
        /// </summary>
        /// <param name="scenarioType">当前市场剧本</param>
        /// <remarks>
        /// WHY（为什么需要虚拟流量）：
        /// 连接宏观价格模型与微观订单簿的桥梁。模型四计算的目标价需要通过
        /// "虚拟市价单"来推动订单簿价格移动，实现价格发现机制。
        /// 
        /// 碰撞机制：
        /// 1. 虚拟流量撞击NPC订单：瞬间穿透，价格移动
        /// 2. 虚拟流量撞击玩家挂单：消耗玩家订单，价格被"钉住"
        /// 3. 玩家挂单被吃光：价格继续向目标移动
        /// </remarks>
        private void ProcessVirtualFlow(string scenarioType)
        {
            foreach (var instrument in _instruments)
            {
                if (instrument is not CommodityFutures futures) continue;
                
                // 获取订单簿
                if (!_orderBooks.TryGetValue(futures.Symbol, out var orderBook))
                    continue;
                
                // 1. 获取理论目标价（来自价格引擎 + 冲击层）
                decimal targetPrice = (decimal)futures.CurrentPrice;
                
                // 2. 获取当前盘口中间价
                decimal midPrice = orderBook.GetMidPrice();
                
                // 如果订单簿为空（无深度），先生成NPC深度
                if (midPrice == 0)
                {
                    orderBook.GenerateNPCDepth(targetPrice, scenarioType);
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
                
                // 6. 更新盘口中间价（反馈到价格引擎）
                // 注意：这里不直接修改instrument.CurrentPrice，避免与价格引擎冲突
                // 订单簿的价格将在下次玩家交易时体现
                
                // 7. 日志输出（调试用）
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
        /// <param name="priceDiff">价格差距（目标价 - 中间价）</param>
        /// <returns>虚拟流量数量</returns>
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
    }
}
