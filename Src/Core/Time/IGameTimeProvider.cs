namespace HedgeHarvest.Core.Time
{
    /// <summary>
    /// Provides time information to the Core layer without depending on StardewValley.
    /// This allows the Core layer to be unit tested in isolation.
    /// </summary>
    public interface IGameTimeProvider
    {
        /// <summary>
        /// Gets the current time of day in the game (e.g., 600 for 6:00 AM, 1350 for 1:50 PM).
        /// </summary>
        int CurrentTimeOfDay { get; }

        /// <summary>
        /// Gets the normalized time ratio from 0.0 (Start of Day) to 1.0 (End of Day).
        /// Useful for interpolation and progress calculations.
        /// </summary>
        double TimeRatio { get; }

        /// <summary>
        /// Gets a value indicating whether the game is currently paused.
        /// </summary>
        bool IsPaused { get; }

        /// <summary>
        /// Gets the total number of minutes played in the current day.
        /// Useful for continuous time calculations.
        /// </summary>
        int TotalMinutesToday { get; }
    }
}
