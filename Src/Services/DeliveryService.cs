using System;
using System.Collections.Generic;
using System.Linq;
using StardewCapital.Domain.Account;
using StardewCapital.Domain.Instruments;
using StardewModdingAPI;
using StardewValley;

using Microsoft.Xna.Framework;

namespace StardewCapital.Services
{
    public class DeliveryService
    {
        private readonly IMonitor _monitor;
        private readonly BrokerageService _brokerageService;
        private readonly MarketManager _marketManager;
        private readonly ExchangeService _exchangeService;

        public DeliveryService(IMonitor monitor, BrokerageService brokerageService, MarketManager marketManager, ExchangeService exchangeService)
        {
            _monitor = monitor;
            _brokerageService = brokerageService;
            _marketManager = marketManager;
            _exchangeService = exchangeService;
        }

        public void ProcessDailyDelivery()
        {
            // ... (existing check logic) ...
            string currentSeason = Game1.currentSeason;
            int currentDay = Game1.dayOfMonth;

            var instruments = _marketManager.GetInstruments();
            var positions = _brokerageService.Account.Positions.ToList();

            foreach (var pos in positions)
            {
                var instrument = instruments.FirstOrDefault(i => i.Symbol == pos.Symbol);
                if (instrument is CommodityFutures future)
                {
                    if (future.DeliverySeason.Equals(currentSeason, StringComparison.OrdinalIgnoreCase) && 
                        future.DeliveryDay == currentDay)
                    {
                        ProcessPositionSettlement(pos, future);
                    }
                }
            }
        }

        private void ProcessPositionSettlement(Position pos, CommodityFutures future)
        {
            _monitor.Log($"[Delivery] Settling position {pos.Symbol} x{pos.Quantity}", LogLevel.Info);

            int quantity = Math.Abs(pos.Quantity);
            string itemId = future.UnderlyingItemId;
            var exchangeBoxes = _exchangeService.FindAllExchangeBoxes();

            if (pos.Quantity > 0) // LONG: Receive Item
            {
                int remaining = quantity;
                
                // 1. Try Exchange Boxes
                foreach (var box in exchangeBoxes)
                {
                    Item item = ItemRegistry.Create(itemId, remaining);
                    Item? leftOver = box.addItem(item);
                    
                    if (leftOver == null)
                    {
                        remaining = 0;
                    }
                    else
                    {
                        remaining = leftOver.Stack;
                    }
                    
                    if (remaining <= 0) break;
                }

                // 2. Drop at Bed if remaining > 0
                if (remaining > 0)
                {
                    // Drop at player's bed position
                    Vector2 dropPos = new Vector2(Game1.player.mostRecentBed.X, Game1.player.mostRecentBed.Y) * 64f;
                    // If bed spot is invalid (0,0), use player position
                    if (dropPos == Vector2.Zero) dropPos = Game1.player.Position;

                    Item dropItem = ItemRegistry.Create(itemId, remaining);
                    Game1.createItemDebris(dropItem, dropPos, 2, Game1.player.currentLocation); // Drop in current location (usually FarmHouse if sleeping)
                    
                    Game1.addHUDMessage(new HUDMessage($"Delivery: {quantity} {future.Name} (Overflow at Bed)", 2));
                }
                else
                {
                    Game1.addHUDMessage(new HUDMessage($"Delivery: {quantity} {future.Name} to Exchange Box", 2));
                }
            }
            else // SHORT: Deliver Item
            {
                int remaining = quantity;

                // 1. Scan Exchange Boxes
                foreach (var box in exchangeBoxes)
                {
                    // Remove from box
                    // iterate items in box
                    for (int i = box.Items.Count - 1; i >= 0; i--)
                    {
                        var item = box.Items[i];
                        if (item != null && item.ItemId == itemId)
                        {
                            int toRemove = Math.Min(remaining, item.Stack);
                            item.Stack -= toRemove;
                            remaining -= toRemove;

                            if (item.Stack <= 0) box.Items[i] = null;
                            if (remaining <= 0) break;
                        }
                    }
                    box.clearNulls();
                    if (remaining <= 0) break;
                }

                // 2. Scan Inventory
                if (remaining > 0)
                {
                    for (int i = Game1.player.Items.Count - 1; i >= 0; i--)
                    {
                        var item = Game1.player.Items[i];
                        if (item != null && item.ItemId == itemId)
                        {
                            int toRemove = Math.Min(remaining, item.Stack);
                            item.Stack -= toRemove;
                            remaining -= toRemove;

                            if (item.Stack <= 0) Game1.player.Items[i] = null;
                            if (remaining <= 0) break;
                        }
                    }
                }

                // 3. Penalty
                if (remaining > 0)
                {
                    int missing = remaining;
                    decimal penaltyPrice = (decimal)future.CurrentPrice;
                    decimal cost = missing * penaltyPrice;
                    
                    _brokerageService.Account.Cash -= cost;
                    Game1.addHUDMessage(new HUDMessage($"Delivery Shortfall: Bought {missing} {future.Name} for {cost:F0}g", 3));
                }
                else
                {
                    Game1.addHUDMessage(new HUDMessage($"Delivery: {quantity} {future.Name} collected.", 2));
                }
            }

            _brokerageService.Account.Positions.Remove(pos);
        }
    }
}
