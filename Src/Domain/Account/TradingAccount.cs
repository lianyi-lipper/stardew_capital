using System;
using System.Collections.Generic;
using System.Linq;

namespace StardewCapital.Domain.Account
{
    public class TradingAccount
    {
        public decimal Cash { get; set; }
        public List<Position> Positions { get; private set; }

        public TradingAccount()
        {
            Cash = 0;
            Positions = new List<Position>();
        }

        public void Deposit(decimal amount)
        {
            if (amount <= 0) throw new ArgumentException("Deposit amount must be positive.");
            Cash += amount;
        }

        public void Withdraw(decimal amount)
        {
            if (amount <= 0) throw new ArgumentException("Withdraw amount must be positive.");
            if (amount > GetFreeMargin()) throw new InvalidOperationException("Insufficient free margin.");
            Cash -= amount;
        }

        public decimal GetTotalEquity(Dictionary<string, decimal> currentPrices)
        {
            decimal unrealizedPnL = 0;
            foreach (var pos in Positions)
            {
                if (currentPrices.TryGetValue(pos.Symbol, out decimal price))
                {
                    unrealizedPnL += pos.GetUnrealizedPnL(price);
                }
            }
            return Cash + unrealizedPnL;
        }

        public decimal GetUsedMargin(Dictionary<string, decimal> currentPrices)
        {
            decimal usedMargin = 0;
            foreach (var pos in Positions)
            {
                if (currentPrices.TryGetValue(pos.Symbol, out decimal price))
                {
                    // Margin is based on current market value? Or entry value?
                    // Usually initial margin is based on entry, maintenance on current.
                    // For simplicity, let's base it on current value for now (Mark-to-Market).
                    usedMargin += pos.GetMarginUsed(price);
                }
            }
            return usedMargin;
        }

        public decimal GetFreeMargin(Dictionary<string, decimal>? currentPrices = null)
        {
            if (currentPrices == null) return Cash; // Approximate if no prices
            return GetTotalEquity(currentPrices) - GetUsedMargin(currentPrices);
        }

        public void Load(decimal cash, List<Position> positions)
        {
            Cash = cash;
            Positions = positions ?? new List<Position>();
        }
    }
}
