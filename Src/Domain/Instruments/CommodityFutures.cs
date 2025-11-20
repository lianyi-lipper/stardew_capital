namespace StardewCapital.Domain.Instruments
{
    public class CommodityFutures : IInstrument
    {
        public string Symbol { get; private set; }
        public string Name { get; private set; }
        public string UnderlyingItemId { get; private set; }
        public double CurrentPrice { get; set; }
        public double MarginRatio { get; private set; }

        public int DeliveryDay { get; private set; }
        public string DeliverySeason { get; private set; }

        public CommodityFutures(string underlyingItemId, string name, string season, int deliveryDay, double initialPrice)
        {
            UnderlyingItemId = underlyingItemId;
            Name = name;
            DeliverySeason = season;
            DeliveryDay = deliveryDay;
            CurrentPrice = initialPrice;
            
            // Symbol format: ITEM-SEASON-DAY (e.g., PARSNIP-SPR-28)
            Symbol = $"{name.ToUpper().Replace(" ", "")}-{season.Substring(0, 3).ToUpper()}-{deliveryDay}";
            
            // Default 10% margin for futures
            MarginRatio = 0.1;
        }
    }
}
