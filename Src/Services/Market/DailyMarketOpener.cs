using System.Collections.Generic;
using StardewCapital.Domain.Instruments;
using StardewCapital.Services.Pricing;
using StardewCapital.Services.News;
using StardewCapital.Config;
using StardewModdingAPI;
using StardewValley;

namespace StardewCapital.Services.Market
{
    /// <summary>
    /// 每日开盘服务（重构版）
    /// 负责每天开盘时的价格初始化和订单簿设置
    /// 从MarketStateManager读取预计算的价格数据，不再动态生成新闻
    /// </summary>
    public class DailyMarketOpener
    {
        private readonly IMonitor _monitor;
        private readonly ScenarioManager _scenarioManager;
        private readonly MarketManager _marketManager;
        private readonly OrderBookManager _orderBookManager;
        private readonly MarketStateManager _marketStateManager;
        private readonly MarketRules _rules;
        private readonly PriceEngine _priceEngine;
        private readonly ConvenienceYieldService _convenienceYieldService;
        private readonly MarketTimeCalculator _timeCalculator;
        private readonly NPCAgentManager _npcAgentManager;

        public DailyMarketOpener(
            IMonitor monitor,
            ScenarioManager scenarioManager,
            MarketManager marketManager,
            OrderBookManager orderBookManager,
            MarketStateManager marketStateManager,
            MarketRules rules,
            PriceEngine priceEngine,
            ConvenienceYieldService convenienceYieldService,
            NPCAgentManager npcAgentManager)
        {
            _monitor = monitor;
            _scenarioManager = scenarioManager;
            _marketManager = marketManager;
            _orderBookManager = orderBookManager;
            _marketStateManager = marketStateManager;
            _rules = rules;
            _priceEngine = priceEngine;
            _convenienceYieldService = convenienceYieldService;
            _npcAgentManager = npcAgentManager;
            _timeCalculator = new MarketTimeCalculator();
        }
        
        /// <summary>
        /// 获取NPC代理力量数据（供MarketManager公开给WebServer）
        /// </summary>
        public Dictionary<string, AgentForces> GetNPCForces()
        {
            return _npcAgentManager.LastForces;
        }

        /// <summary>
        /// 处理新一天开始的逻辑
        /// </summary>
        public void OnNewDay(
            int currentDay,
            Dictionary<string, double> dailyTargets)
        {
            // 调试日志：确认方法被调用
            _monitor?.Log($"[DailyOpener] OnNewDay called for day {currentDay}", LogLevel.Debug);
            
            // 防御性检查
            if (_marketManager == null)
            {
                _monitor?.Log("[DailyOpener] MarketManager is null, skipping", LogLevel.Warn);
                return;
            }

            // 切换市场剧本
            _scenarioManager.OnNewDay();
            
            var instruments = _marketManager.GetInstruments();
            if (instruments == null)
            {
                _monitor?.Log("[DailyOpener] No instruments available, skipping", LogLevel.Warn);
                return;
            }

            var scenarioType = _scenarioManager.GetCurrentScenario();
            var scenarioTypeName = scenarioType.ToString();

            foreach (var instrument in instruments)
            {
                _monitor?.Log($"[DailyOpener] Processing instrument: {instrument.Symbol}", LogLevel.Debug);
                
                if (instrument is not CommodityFutures futures)
                    continue;

                // 1. 从MarketStateManager获取今日目标价（预计算的收盘价）
                double targetPrice = _marketStateManager.GetCurrentPrice(
                    futures.Symbol,
                    currentDay,
                    1.0 // 收盘价
                );

                if (targetPrice <= 0)
                {
                    // 如果没有预计算数据，使用当前价格
                    _monitor?.Log($"[DailyOpener] No precalculated price for {futures.Symbol}, using current price", LogLevel.Warn);
                    targetPrice = futures.CurrentPrice;
                }

                // 2. 处理隔夜跳空开盘（熔断机制产生的Gap）
                if (futures.Gap != 0.0)
                {
                    futures.CurrentPrice += futures.Gap;
                    
                    _monitor?.Log(
                        $"[Gap Opening] {futures.Symbol}: Gap={futures.Gap:+0.00;-0.00}g applied, " +
                        $"Final Open={futures.CurrentPrice:F2}g",
                        LogLevel.Warn
                    );
                    
                    // 清零 Gap 和熔断标志
                    futures.Gap = 0.0;
                    futures.CircuitBreakerActive = false;
                }

                // 3. 计算期货价格（基差系统）
                int daysToMaturity = _timeCalculator.CalculateDaysToMaturity(futures);
                
                double convenienceYield = _convenienceYieldService.GetConvenienceYield(
                    itemId: futures.UnderlyingItemId,
                    baseYield: _rules.Instruments.Futures.BaseConvenienceYield
                );
                
                futures.FuturesPrice = _priceEngine.CalculateFuturesPrice(
                    spotPrice: futures.CurrentPrice,  // CurrentPrice 是现货价
                    daysToMaturity: daysToMaturity,
                    convenienceYield: convenienceYield
                );

                // 4. 设置今日目标价
                dailyTargets[futures.Symbol] = targetPrice;
                
                // 5. 记录开盘价
                futures.OpenPrice = futures.CurrentPrice;

                // 6. 日志输出：基差分析
                double basis = futures.FuturesPrice - futures.CurrentPrice;
                string basisType = basis > 0 ? "Contango(升水)" : "Backwardation(贴水)";
                
                _monitor?.Log(
                    $"[Market] New Day: {futures.Symbol} | " +
                    $"Spot={futures.CurrentPrice:F2}g, Futures={futures.FuturesPrice:F2}g, " +
                    $"Basis={basis:+0.00;-0.00}g ({basisType}), " +
                    $"DTM={daysToMaturity}d, ConvYield={convenienceYield:F4}, Target={targetPrice:F2}g",
                    LogLevel.Info
                );

                // 7. 初始化订单簿NPC深度
                var config = _marketManager.GetCommodityConfig(futures.CommodityName);
                if (config != null)
                {
                    _orderBookManager.RegenerateDepth(
                        futures.Symbol,
                        (decimal)targetPrice,
                        scenarioTypeName,
                        config.LiquiditySensitivity
                    );
                }
            }
        }
    }
}
