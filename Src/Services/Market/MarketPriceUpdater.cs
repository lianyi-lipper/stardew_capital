using System;
using System.Collections.Generic;
using System.Linq;
using StardewCapital.Core.Time;
using StardewCapital.Domain.Instruments;
using StardewCapital.Domain.Market;
using StardewCapital.Services.Pricing;
using StardewCapital.Services.News;
using StardewModdingAPI;
using StardewCapital.Config;
using StardewValley;

namespace StardewCapital.Services.Market
{
    /// <summary>
    /// 市场价格更新器（重构版）
    /// 负责协调各个服务模块完成价格更新逻辑
    /// 
    /// 核心职责：
    /// - 每帧更新市场价格（布朗桥 + 市场冲击）
    /// - 协调其他服务完成复杂功能
    /// </summary>
    public class MarketPriceUpdater
    {
        private readonly IMonitor _monitor;
        private readonly MixedTimeClock _clock;
        private readonly PriceEngine _priceEngine;
        private readonly FundamentalEngine _fundamentalEngine;
        private readonly ConvenienceYieldService _convenienceYieldService;
        private readonly ImpactService _impactService;
        private readonly ScenarioManager _scenarioManager;
        private readonly MarketManager _marketManager;
        private readonly MarketRules _rules;
        
        // 新的服务组件
        private readonly CircuitBreakerService _circuitBreakerService;
        private readonly VirtualFlowProcessor _virtualFlowProcessor;
        private readonly MarketTimeCalculator _timeCalculator;

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
        
        /// <summary>上一帧的时间进度，用于计算 timeStep（这一帧过了多久）</summary>
        private double _lastTimeRatio = 0.0;

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
            OrderBookManager orderBookManager,
            MarketRules rules)
        {
            _monitor = monitor;
            _clock = clock;
            _priceEngine = priceEngine;
            _fundamentalEngine = fundamentalEngine;
            _convenienceYieldService = convenienceYieldService;
            _impactService = impactService;
            _scenarioManager = scenarioManager;
            _marketManager = marketManager;
            _rules = rules;
            
            _dailyTargets = new Dictionary<string, double>();
            CurrentShadowPrice = new Dictionary<string, double>();
            _newsHistory = new List<NewsEvent>();
            _activeNewsEffects = new List<NewsEvent>();
            
            // 初始化新的服务组件
            _timeCalculator = new MarketTimeCalculator();
            
            _circuitBreakerService = new CircuitBreakerService(monitor, rules);
            
            _virtualFlowProcessor = new VirtualFlowProcessor(
                monitor,
                marketManager,
                orderBookManager
            );
        }

        /// <summary>
        /// 当前各合约的影子价格 (用于UI显示)
        /// </summary>
        public Dictionary<string, double> CurrentShadowPrice { get; }

        /// <summary>
        /// 处理新一天开始的逻辑
        /// </summary>
        public void OnNewDay()
        {
            // OnNewDay logic now handled by DailyMarketOpener in MarketManager
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
            var currentSeason = _timeCalculator.GetCurrentSeason();

            // ========== 计算动态时间步长 ==========
            // timeStep = 当前帧的 timeRatio - 上一帧的 timeRatio
            double currentTimeRatio = _clock.GetDayProgress();
            double timeStep = currentTimeRatio - _lastTimeRatio;
            
            // 防御性检查：首次调用、时间回溯或异常跳跃时，使用合理的默认值
            if (timeStep <= 0.0 || timeStep > 0.1)
            {
                timeStep = 0.001; // 默认步长
            }

            // 获取当前日期（用于查询预计算价格）
            int currentDay = Game1.dayOfMonth;

            // 更新所有产品的价格
            foreach (var instrument in _marketManager.GetInstruments())
            {
                if (_dailyTargets.TryGetValue(instrument.Symbol, out double target))
                {
                    // 1. 从MarketStateManager获取预计算的影子价格（如果已初始化）
                    if (_marketManager.GetMarketStateManager().IsInitialized())
                    {
                        double shadowPrice = _marketManager.GetMarketStateManager().GetCurrentPrice(
                            instrument.Symbol,
                            currentDay,
                            currentTimeRatio
                        );
                        
                        // 直接设置为影子价格
                        instrument.CurrentPrice = shadowPrice;
                        
                        // 更新影子价格记录
                        CurrentShadowPrice[instrument.Symbol] = shadowPrice;
                    }
                    else
                    {
                        // 降级：如果MarketStateManager未初始化，使用旧的布朗桥逻辑
                        _priceEngine.UpdatePrice(instrument, target, timeStep);
                    }
                    
                    // 1.5 熔断检查（在叠加冲击之前）
                    if (instrument is CommodityFutures futuresInst)
                    {
                        _circuitBreakerService.CheckAndApplyCircuitBreaker(
                            futuresInst, 
                            target, 
                            currentTimeRatio,
                            _dailyTargets
                        );
                    }
                    
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
                            
                            
                            // 2.3 计算NPC代理力量（供监控面板显示和订单簿流量计算）
                            double shadowPrice = CurrentShadowPrice.GetValueOrDefault(futures.Symbol, futures.CurrentPrice);
                            
                            _npcAgentManager.CalculateNetVirtualFlow(
                                symbol: futures.Symbol,
                                currentPrice: instrument.CurrentPrice,
                                shadowPrice: shadowPrice,
                                fundamentalValue: fundamentalValue,
                                scenario: scenarioParams
                            );
                            
                            // 2.5 计算期货价格（基差系统）
                            int daysToMaturity = _timeCalculator.CalculateDaysToMaturity(futures);
                            
                            double convenienceYield = _convenienceYieldService.GetConvenienceYield(
                                itemId: futures.UnderlyingItemId,
                                baseYield: _rules.Instruments.Futures.BaseConvenienceYield
                            );
                            
                            futures.FuturesPrice = _priceEngine.CalculateFuturesPrice(
                                spotPrice: instrument.CurrentPrice,  // CurrentPrice 已经叠加了冲击 I(t)
                                daysToMaturity: daysToMaturity,
                                convenienceYield: convenienceYield
                            );
                            
                            // 实时价格更新日志（只在价格有明显变化时输出）
                            double basis = futures.FuturesPrice - instrument.CurrentPrice;
                            if (Math.Abs(impact) > 0.01 || currentTick % 300 == 0) // 冲击>0.01g 或每5秒输出一次
                            {
                                _monitor.Log(
                                    $"[Price] {futures.Symbol} | " +
                                    $"Futures={futures.FuturesPrice:F2}g, Spot={instrument.CurrentPrice:F2}g, " +
                                    $"Basis={basis:+0.00;-0.00}g, Impact={impact:+0.00;-0.00}g",
                                    LogLevel.Info
                                );
                            }
                        }
                    }
                }
            
            // ========== 更新时间进度记录 ==========
            // 在所有 instrument 更新完成后才更新 _lastTimeRatio
            _lastTimeRatio = currentTimeRatio;
            
            // ========== 盘中新闻检查（事件驱动价格更新） ==========
            // Intraday news now handled by NewsSchedulePlayer in MarketManager
            
            // ========== 虚拟流量处理（订单簿碰撞检测） ==========
            var currentScenarioType = _scenarioManager.GetCurrentScenario();
            _virtualFlowProcessor.ProcessVirtualFlow(currentScenarioType.ToString());
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

        /// <summary>
        /// 获取每日目标价字典（供DailyMarketOpener使用）
        /// </summary>
        public Dictionary<string, double> GetDailyTargets()
        {
            return _dailyTargets;
        }

        /// <summary>
        /// 获取当前时间进度（供NewsSchedulePlayer使用）
        /// </summary>
        public double GetDayProgress()
        {
            return _clock.GetDayProgress();
        }
    }
}
