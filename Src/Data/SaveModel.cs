using System.Collections.Generic;
using StardewCapital.Domain.Account;

namespace StardewCapital.Data
{
    public class SaveModel
    {
        public decimal Cash { get; set; }
        public List<Position> Positions { get; set; } = new List<Position>();
        
        // Future: Save market history/prices here
        // public Dictionary<string, double> LastPrices { get; set; }
    }
}
