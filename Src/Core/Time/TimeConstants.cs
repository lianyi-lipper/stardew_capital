namespace StardewCapital.Core.Time
{
    public static class TimeConstants
    {
        public const int OpeningTime = 600;  // 6:00 AM
        public const int ClosingTime = 2600; // 2:00 AM (next day)
        
        // Total minutes in a standard Stardew day (6am to 2am = 20 hours)
        public const int MinutesPerDay = 20 * 60; 

        // Real-time tick interval for market updates (in seconds)
        public const double RealTimeTickInterval = 0.7;
    }
}
