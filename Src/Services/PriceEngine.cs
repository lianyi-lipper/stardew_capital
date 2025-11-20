using System;
using System.Collections.Generic;
using StardewCapital.Core.Math;
using StardewCapital.Core.Time;
using StardewCapital.Domain.Instruments;

namespace StardewCapital.Services
{
    public class PriceEngine
    {
        private readonly MixedTimeClock _clock;
        private readonly Random _random = new Random();

        // Configuration parameters (could be moved to a config file)
        private const double BASE_VOLATILITY = 0.02; // 2% daily volatility
        private const double INTRA_VOLATILITY = 0.005; // 0.5% intraday noise

        public PriceEngine(MixedTimeClock clock)
        {
            _clock = clock;
        }

        /// <summary>
        /// Updates the price of an instrument for the current tick.
        /// Uses Model 4 (Brownian Bridge) for intraday movement.
        /// </summary>
        public void UpdatePrice(IInstrument instrument, double targetPrice)
        {
            double currentPrice = instrument.CurrentPrice;
            double timeRatio = _clock.GetDayProgress();

            // Calculate next tick price using Brownian Bridge
            double nextPrice = BrownianBridge.CalculateNextTickPrice(
                currentPrice, 
                targetPrice, 
                timeRatio, 
                INTRA_VOLATILITY
            );

            // Ensure price doesn't go negative
            instrument.CurrentPrice = System.Math.Max(0.01, nextPrice);
        }

        /// <summary>
        /// Calculates the target price for the end of the day (Model 2: GBM).
        /// This should be called once at the start of the day or when news happens.
        /// </summary>
        public double CalculateDailyTarget(double currentPrice, double fundamentalValue, int daysToMaturity)
        {
            return GBM.CalculateNextPrice(
                currentPrice, 
                fundamentalValue, 
                daysToMaturity, 
                BASE_VOLATILITY
            );
        }
    }
}
