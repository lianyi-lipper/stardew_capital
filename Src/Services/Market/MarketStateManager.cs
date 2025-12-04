using System;
using StardewCapital.Config;
using System.Collections.Generic;
using System.Linq;
using StardewCapital.Core.Futures.Domain.Instruments;
using StardewCapital.Core.Futures.Domain.Market;
using StardewCapital.Core.Futures.Domain.Market.MarketState;
using StardewCapital.Core.Futures.Data;
using StardewCapital.Services.Pricing.Generators;
using StardewCapital.Core.Futures.Config;
using StardewModdingAPI;

namespace StardewCapital.Services.Market
{
    /// <summary>
    /// 市场状态管理器
    /// 负责：
    /// 1. 季节开始时生成并缓存市场状态
    /// 2. 运行时提供价格和事件查询
    /// 3. 配合PersistenceService进行存档/读档
    /// </summary>
    public class MarketStateManager
    {
        private readonly IMonitor _monitor;
        private readonly Dictionary<string, IMarketDataGenerator> _generators;
        private Dictionary<string, IMarketState> _marketStates;
        
        private Season _currentSeason;
        private int _currentYear;
        
        // 配置跟踪：用于检测配置变更和保护数据一致性
        private int _currentOpeningTime;
        private int _currentClosingTime;
        private PendingConfigChange _pendingConfig = new();

        public MarketStateManager(IMonitor monitor, ModConfig config)
        {
            _monitor = monitor;
            _generators = new Dictionary<string, IMarketDataGenerator>();
            _marketStates = new Dictionary<string, IMarketState>();
            
            // 初始化当前配置
            _currentOpeningTime = config.OpeningTime;
            _currentClosingTime = config.ClosingTime;
        }

        /// <summary>
        /// 注册生成器
        /// </summary>
        public void RegisterGenerator(string instrumentType, IMarketDataGenerator generator)
        {
            _generators[instrumentType] = generator;
        }

        /// <summary>
        /// 初始化新季节（预计算所有数据）
        /// </summary>
        public void InitializeSeason(
            Season season,
            int year,
            List<IInstrument> instruments)
        {
            // ✅ 检查并应用待生效的配置
            if (_pendingConfig.HasPendingChanges)
            {
                _currentOpeningTime = _pendingConfig.OpeningTime;
                _currentClosingTime = _pendingConfig.ClosingTime;
                _pendingConfig.Clear();
                
                _monitor.Log(
                    $"[MarketState] Applying pending config changes: " +
                    $"OpeningTime={_currentOpeningTime}, ClosingTime={_currentClosingTime}",
                    LogLevel.Info
                );
                
                StardewValley.Game1.addHUDMessage(new StardewValley.HUDMessage(
                    "Market hours updated! New schedule is now active.",
                    StardewValley.HUDMessage.achievement_type
                ));
            }
            
            _currentSeason = season;
            _currentYear = year;
            _marketStates.Clear();

            _monitor.Log($"[MarketState] Initializing season {season} {year}...", LogLevel.Info);

            foreach (var instrument in instruments)
            {
                try
                {
                    // 根据产品类型选择生成器
                    string instrumentType = instrument.GetType().Name;
                    
                    if (!_generators.ContainsKey(instrumentType))
                    {
                        _monitor.Log($"[MarketState] No generator for {instrumentType}, skipping {instrument.Symbol}", LogLevel.Warn);
                        continue;
                    }

                    var generator = _generators[instrumentType];
                    
                    // 生成市场状态
                    var marketState = generator.Generate(instrument, season, year);
                    
                    // 缓存
                    _marketStates[instrument.Symbol] = marketState;
                    
                    _monitor.Log($"[MarketState] Generated state for {instrument.Symbol}", LogLevel.Debug);
                }
                catch (Exception ex)
                {
                    _monitor.Log($"[MarketState] Failed to generate state for {instrument.Symbol}: {ex.Message}", LogLevel.Error);
                }
            }

            _monitor.Log($"[MarketState] Season initialization complete. {_marketStates.Count} states generated.", LogLevel.Info);
        }

        /// <summary>
        /// 获取当前价格
        /// </summary>
        public double GetCurrentPrice(string symbol, int day, double timeRatio)
        {
            if (!_marketStates.ContainsKey(symbol))
            {
                _monitor.Log($"[MarketState] No state found for {symbol}", LogLevel.Warn);
                return 0.0;
            }

            return _marketStates[symbol].GetPrice(day, timeRatio);
        }

        /// <summary>
        /// 获取待触发事件
        /// </summary>
        public List<IMarketEvent> GetPendingEvents(int day, double timeRatio)
        {
            var allEvents = new List<IMarketEvent>();

            foreach (var state in _marketStates.Values)
            {
                try
                {
                    var events = state.GetPendingEvents(day, timeRatio);
                    allEvents.AddRange(events);
                }
                catch (Exception ex)
                {
                    _monitor.Log($"[MarketState] Error getting events from {state.Symbol}: {ex.Message}", LogLevel.Error);
                }
            }

            // 按优先级排序
            return allEvents.OrderBy(e => e.Priority).ToList();
        }

        /// <summary>
        /// 获取市场状态
        /// </summary>
        public IMarketState? GetMarketState(string symbol)
        {
            return _marketStates.GetValueOrDefault(symbol);
        }

        /// <summary>
        /// 导出存档数据
        /// </summary>
        public MarketStateSaveData ExportSaveData()
        {
            return new MarketStateSaveData
            {
                CurrentSeason = _currentSeason,
                CurrentYear = _currentYear,
                CurrentDay = StardewValley.Game1.dayOfMonth,
                FuturesStates = _marketStates.Values
                    .OfType<FuturesMarketState>()
                    .ToList(),
                SaveTimestamp = DateTime.Now,
                Version = "1.0.0",
                
                // 保存当前生效的配置（用于读档时检测配置变更）
                SavedOpeningTime = _currentOpeningTime,
                SavedClosingTime = _currentClosingTime
            };
        }

        /// <summary>
        /// 导入存档数据
        /// </summary>
        public void ImportSaveData(MarketStateSaveData saveData)
        {
            _currentSeason = saveData.CurrentSeason;
            _currentYear = saveData.CurrentYear;
            _marketStates.Clear();
            
            // 恢复配置快照
            _currentOpeningTime = saveData.SavedOpeningTime;
            _currentClosingTime = saveData.SavedClosingTime;

            foreach (var futuresState in saveData.FuturesStates)
            {
                _marketStates[futuresState.Symbol] = futuresState;
            }

            _monitor.Log($"[MarketState] Loaded {_marketStates.Count} market states from save data", LogLevel.Info);
        }

        /// <summary>
        /// 尝试调度配置变更（下季度生效）
        /// </summary>
        /// <param name="newConfig">新配置</param>
        /// <param name="currentDay">当前日期</param>
        /// <returns>是否成功调度</returns>
        public bool TryScheduleConfigChange(ModConfig newConfig, int currentDay)
        {
            // 检查是否是交割日（第28天）
            if (currentDay == 28)
            {
                StardewValley.Game1.addHUDMessage(new StardewValley.HUDMessage(
                    "Cannot modify market hours on delivery day (Day 28)!",
                    StardewValley.HUDMessage.error_type
                ));
                
                _monitor.Log("[MarketState] Config change blocked: Day 28 is delivery day", LogLevel.Warn);
                return false;
            }
            
            // 检查配置是否真的改变了
            if (newConfig.OpeningTime == _currentOpeningTime && 
                newConfig.ClosingTime == _currentClosingTime)
            {
                return false; // 没有变化，无需调度
            }
            
            // 暂存配置，下季度生效
            _pendingConfig.ApplyFrom(newConfig);
            
            StardewValley.Game1.addHUDMessage(new StardewValley.HUDMessage(
                $"Market hours scheduled: {newConfig.OpeningTime}-{newConfig.ClosingTime}. " +
                "Will take effect next season.",
                StardewValley.HUDMessage.newQuest_type
            ));
            
            _monitor.Log(
                $"[MarketState] Config change scheduled: " +
                $"{_currentOpeningTime}-{_currentClosingTime} → " +
                $"{newConfig.OpeningTime}-{newConfig.ClosingTime} (next season)",
                LogLevel.Info
            );
            
            return true;
        }

        /// <summary>
        /// 获取存档数据（用于ModEntry读档时检测配置变更）
        /// </summary>
        public MarketStateSaveData? GetSaveData()
        {
            if (_marketStates.Count == 0)
                return null;
                
            return ExportSaveData();
        }

        /// <summary>
        /// 检查是否已初始化
        /// </summary>
        public bool IsInitialized()
        {
            return _marketStates.Count > 0;
        }
    }
}


