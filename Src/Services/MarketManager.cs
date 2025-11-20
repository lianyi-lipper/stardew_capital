using System;
using System.Collections.Generic;
using StardewCapital.Core.Time;
using StardewCapital.Domain.Instruments;
using StardewModdingAPI;

namespace StardewCapital.Services
{
    /// <summary>
    /// 市场管理器
    /// 协调所有市场相关服务，管理金融产品列表，驱动价格更新。
    /// 
    /// 核心职责：
    /// - 管理所有可交易的金融产品（期货、股票等）
    /// - 协调价格引擎进行价格更新
    /// - 维护每日目标价格
    /// - 处理新一天的市场初始化
    /// </summary>
    public class MarketManager
    {
        private readonly IMonitor _monitor;
        private readonly MixedTimeClock _clock;
        private readonly PriceEngine _priceEngine;
        
        private List<IInstrument> _instruments;
        private Dictionary<string, double> _dailyTargets; // Symbol -> 目标价格

        private int _lastUpdateTick = 0;
        private const int UPDATE_INTERVAL_TICKS = 60; // 每60个tick更新一次价格（约1秒）

        public MarketManager(IMonitor monitor, MixedTimeClock clock, PriceEngine priceEngine)
        {
            _monitor = monitor;
            _clock = clock;
            _priceEngine = priceEngine;
            _instruments = new List<IInstrument>();
            _dailyTargets = new Dictionary<string, double>();
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
        /// - 计算今天的新目标价
        /// </summary>
        public void OnNewDay()
        {
            foreach (var instrument in _instruments)
            {
                // 1. 收敛到昨天的目标价（模拟隔夜波动）
                if (_dailyTargets.TryGetValue(instrument.Symbol, out double prevTarget))
                {
                    instrument.CurrentPrice = prevTarget;
                }

                // 2. 计算今天的新目标价
                // TODO: 未来应该基于新闻、季节等因素动态调整基本面价值
                double fundamentalValue = 35.0; // 目前假设基本面价值恒定
                double newTarget = _priceEngine.CalculateDailyTarget(instrument.CurrentPrice, fundamentalValue, 28); // 假设28天到期
                
                _dailyTargets[instrument.Symbol] = newTarget;
                
                _monitor.Log($"[Market] New Day: {instrument.Symbol} Open: {instrument.CurrentPrice:F2}g, Target: {newTarget:F2}g", LogLevel.Info);
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
    }
}
