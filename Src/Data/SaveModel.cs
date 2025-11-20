using System.Collections.Generic;
using HedgeHarvest.Domain.Account;

namespace HedgeHarvest.Data
{
    public class SaveModel
    {
        public decimal Cash { get; set; }
        public List<Position> Positions { get; set; } = new List<Position>();
        
        // Future: Save market history/prices here
        // public Dictionary<string, double> LastPrices { get; set; }
    }
}
