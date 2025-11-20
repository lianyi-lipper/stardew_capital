using System;
using System.Collections.Generic;
using HedgeHarvest.Core.Time;
using HedgeHarvest.Domain.Instruments;
using StardewModdingAPI;

namespace HedgeHarvest.Services
{
    public class MarketManager
    {
        private readonly IMonitor _monitor;
        private readonly MixedTimeClock _clock;
        private readonly PriceEngine _priceEngine;
        
        private List<IInstrument> _instruments;
        private Dictionary<string, double> _dailyTargets; // Symbol -> Target Price

        private int _lastUpdateTick = 0;
        private const int UPDATE_INTERVAL_TICKS = 60; // Update every 60 ticks (approx 1 sec)

        public MarketManager(IMonitor monitor, MixedTimeClock clock, PriceEngine priceEngine)
        {
            _monitor = monitor;
            _clock = clock;
            _priceEngine = priceEngine;
            _instruments = new List<IInstrument>();
            _dailyTargets = new Dictionary<string, double>();
        }

        public void InitializeMarket()
        {
            // For Phase 2, we hardcode a test instrument
            var parsnipFutures = new CommodityFutures("24", "Parsnip", "Spring", 28, 35.0);
            _instruments.Add(parsnipFutures);
            
            // Initial target (just for testing, assume it goes to 40)
            _dailyTargets[parsnipFutures.Symbol] = 40.0;
            
            _monitor.Log($"[Market] Initialized with {parsnipFutures.Symbol} @ {parsnipFutures.CurrentPrice}g", LogLevel.Info);
        }

        public void OnNewDay()
        {
            foreach (var instrument in _instruments)
            {
                // 1. Snap to yesterday's target (or close to it) to simulate overnight movement
                if (_dailyTargets.TryGetValue(instrument.Symbol, out double prevTarget))
                {
                    instrument.CurrentPrice = prevTarget;
                }

                // 2. Calculate NEW target for today
                // For Phase 5, we assume fundamental value is constant (e.g. 35.0)
                // In future, fundamental value should change based on news/season
                double fundamentalValue = 35.0; 
                double newTarget = _priceEngine.CalculateDailyTarget(instrument.CurrentPrice, fundamentalValue, 28); // 28 days to maturity
                
                _dailyTargets[instrument.Symbol] = newTarget;
                
                _monitor.Log($"[Market] New Day: {instrument.Symbol} Open: {instrument.CurrentPrice:F2}g, Target: {newTarget:F2}g", LogLevel.Info);
            }
        }

        public void Update(int currentTick)
        {
            // Throttle updates
            if (currentTick - _lastUpdateTick < UPDATE_INTERVAL_TICKS) return;
            _lastUpdateTick = currentTick;

            // Stop updates if game is paused or market is closed
            if (_clock.IsPaused() || !_clock.IsMarketOpen()) return;

            foreach (var instrument in _instruments)
            {
                if (_dailyTargets.TryGetValue(instrument.Symbol, out double target))
                {
                    // ... existing update logic ...
                    double oldPrice = instrument.CurrentPrice;
                    _priceEngine.UpdatePrice(instrument, target);
                }
            }
        }
        public List<IInstrument> GetInstruments()
        {
            return _instruments;
        }
    }
}
