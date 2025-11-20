using System;

namespace HedgeHarvest.Domain.Account
{
    public class Position
    {
        public string Symbol { get; set; }
        public int Quantity { get; set; } // Positive for Long, Negative for Short
        public decimal AverageCost { get; set; }
        public int Leverage { get; set; } // 1x, 5x, 10x, etc.

        public Position(string symbol, int quantity, decimal averageCost, int leverage)
        {
            Symbol = symbol;
            Quantity = quantity;
            AverageCost = averageCost;
            Leverage = leverage;
        }

        public decimal GetMarketValue(decimal currentPrice)
        {
            return Quantity * currentPrice;
        }

        public decimal GetUnrealizedPnL(decimal currentPrice)
        {
            return (currentPrice - AverageCost) * Quantity;
        }

        public decimal GetMarginUsed(decimal currentPrice)
        {
            // Margin = |Value| / Leverage
            return Math.Abs(GetMarketValue(currentPrice)) / Leverage;
        }
    }
}
