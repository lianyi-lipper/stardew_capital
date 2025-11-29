// MarketStateDto.cs

using System.Collections.Generic;

namespace StardewCapital
{
    public class MarketStateDto
    {
        public string ContractName { get; set; }
        public string DailyNews { get; set; }
        public FuturesMarket.MarketStatus MarketStatus { get; set; }
        public double CurrentPrice { get; set; }
        public double AccountEquity { get; set; }
        public double FreeMargin { get; set; }
        public List<PlayerPosition> OpenPositions { get; set; }
    }
}
