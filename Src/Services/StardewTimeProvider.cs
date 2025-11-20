using HedgeHarvest.Core.Time;
using StardewValley;

namespace HedgeHarvest.Services
{
    /// <summary>
    /// The bridge between Stardew Valley's game loop and our Core Time system.
    /// </summary>
    public class StardewTimeProvider : IGameTimeProvider
    {
        private readonly ModConfig _config;

        public StardewTimeProvider(ModConfig config)
        {
            _config = config;
        }

        public int CurrentTimeOfDay => Game1.timeOfDay;

        public double TimeRatio
        {
            get
            {
                // Convert Stardew time (600 to 2600) to 0.0 - 1.0 range
                // Note: Stardew time is not linear (650 -> 700). We need to convert to minutes first.
                int minutes = ToMinutes(Game1.timeOfDay);
                int startMinutes = ToMinutes(_config.OpeningTime);
                int endMinutes = ToMinutes(_config.ClosingTime);
                
                double totalDuration = endMinutes - startMinutes;
                double elapsed = minutes - startMinutes;

                return System.Math.Clamp(elapsed / totalDuration, 0.0, 1.0);
            }
        }

        public bool IsPaused => Game1.paused || Game1.activeClickableMenu != null;

        public int TotalMinutesToday => ToMinutes(Game1.timeOfDay);

        /// <summary>
        /// Helper to convert Stardew time (e.g. 630) to total minutes.
        /// </summary>
        private int ToMinutes(int time)
        {
            return (time / 100) * 60 + (time % 100);
        }
    }
}
