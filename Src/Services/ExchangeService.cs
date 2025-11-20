using System.Collections.Generic;
using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.Objects;

namespace StardewCapital.Services
{
    public class ExchangeService
    {
        public const string EXCHANGE_KEY = "HedgeHarvest.Exchange";

        public bool IsExchangeBox(Chest chest)
        {
            return chest.modData.ContainsKey(EXCHANGE_KEY);
        }

        public void ToggleExchangeStatus(Chest chest)
        {
            if (IsExchangeBox(chest))
            {
                chest.modData.Remove(EXCHANGE_KEY);
                // Reset to default color (Black usually means no color in SDV logic for chests)
                chest.playerChoiceColor.Value = Color.Black; 
            }
            else
            {
                chest.modData[EXCHANGE_KEY] = "true";
                chest.playerChoiceColor.Value = Color.Gold;
            }
        }

        public List<Chest> FindAllExchangeBoxes()
        {
            var boxes = new List<Chest>();
            
            // Iterate all locations
            foreach (var location in Game1.locations)
            {
                foreach (var obj in location.Objects.Values)
                {
                    if (obj is Chest chest && IsExchangeBox(chest))
                    {
                        boxes.Add(chest);
                    }
                }
                
                // Also check buildings (Cabins, Sheds, etc. are locations in Game1.locations, 
                // but let's ensure we cover everything if needed. 
                // Game1.locations usually covers all active locations including buildings.)
            }
            
            return boxes;
        }
    }
}
