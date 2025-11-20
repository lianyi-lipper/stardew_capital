using System;

namespace StardewCapital.Domain.Market
{
    /// <summary>
    /// Represents a single candle or data point in the price history.
    /// </summary>
    public class TickData
    {
        public DateTime Timestamp { get; set; } // Real world time or Game Date+Time
        public int GameDay { get; set; }
        public int GameTime { get; set; }
        
        public double Open { get; set; }
        public double High { get; set; }
        public double Low { get; set; }
        public double Close { get; set; }
        public long Volume { get; set; }

        public TickData(int gameDay, int gameTime, double price)
        {
            GameDay = gameDay;
            GameTime = gameTime;
            Open = price;
            High = price;
            Low = price;
            Close = price;
            Volume = 0;
        }

        public void Update(double newPrice)
        {
            Close = newPrice;
            if (newPrice > High) High = newPrice;
            if (newPrice < Low) Low = newPrice;
        }
    }
}
