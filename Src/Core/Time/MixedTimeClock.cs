using System;

namespace StardewCapital.Core.Time
{
    /// <summary>
    /// The "Heart" of the market simulation.
    /// Handles the conversion between Real Time (for frequency) and Game Time (for trends).
    /// </summary>
    public class MixedTimeClock
    {
        private readonly IGameTimeProvider _timeProvider;
        private readonly ModConfig _config;

        public MixedTimeClock(IGameTimeProvider timeProvider, ModConfig config)
        {
            _timeProvider = timeProvider;
            _config = config;
        }

        /// <summary>
        /// Gets the current normalized time progress (0.0 to 1.0) for the trading day.
        /// </summary>
        public double GetDayProgress()
        {
            return _timeProvider.TimeRatio;
        }

        /// <summary>
        /// Calculates the remaining time (T - t) for the day, normalized.
        /// </summary>
        public double GetTimeRemaining()
        {
            return 1.0 - _timeProvider.TimeRatio;
        }

        /// <summary>
        /// Checks if the market is currently open.
        /// </summary>
        public bool IsMarketOpen()
        {
            int time = _timeProvider.CurrentTimeOfDay;
            return time >= _config.OpeningTime && time < _config.ClosingTime;
        }

        /// <summary>
        /// Checks if the game time is paused.
        /// </summary>
        public bool IsPaused()
        {
            return _timeProvider.IsPaused;
        }
    }
}
