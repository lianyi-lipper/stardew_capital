namespace HedgeHarvest.Domain.Instruments
{
    /// <summary>
    /// Represents any tradable asset in the market (Futures, Stocks, Options).
    /// </summary>
    public interface IInstrument
    {
        /// <summary>
        /// Unique symbol, e.g., "PARSNIP-SPR-28".
        /// </summary>
        string Symbol { get; }

        /// <summary>
        /// Display name, e.g., "Parsnip Futures (Spring 28)".
        /// </summary>
        string Name { get; }

        /// <summary>
        /// The underlying item ID in Stardew Valley (e.g., "24" for Parsnip).
        /// </summary>
        string UnderlyingItemId { get; }

        /// <summary>
        /// Current market price per unit.
        /// </summary>
        double CurrentPrice { get; set; }

        /// <summary>
        /// Margin requirement ratio (0.0 to 1.0).
        /// Futures might be 0.1 (10%), Stocks 1.0 (100%).
        /// </summary>
        double MarginRatio { get; }
    }
}
