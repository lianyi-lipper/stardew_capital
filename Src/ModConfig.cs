namespace StardewCapital
{
    public class ModConfig
    {
        /// <summary>
        /// The time when the market opens (e.g., 600 for 6:00 AM).
        /// </summary>
        public int OpeningTime { get; set; } = 600;

        /// <summary>
        /// The time when the market closes (e.g., 2600 for 2:00 AM next day).
        /// </summary>
        public int ClosingTime { get; set; } = 2600;
    }
}
