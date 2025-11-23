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
    /// <summary>
    /// 交割服务
    /// 负责处理期货合约到期时的实物交割流程。
    /// 
    /// 核心职责：
    /// - 检测到期合约
    /// - 多头仓位：发放实物物品（优先放入交易所箱子，其次放在床边）
    /// - 空头仓位：收取实物物品（优先从交易所箱子扣除，其次从背包扣除，不足则罚金）
    /// - 显示交割通知
    /// 
    /// 设计理念：
    /// 通过实物交割机制，将虚拟期货与游戏实体物品系统结合，增强游戏沉浸感。
    /// </summary>
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

        /// <summary>
        /// 处理每日交割检查
        /// 在每日结束时调用，检查所有到期的期货合约并执行交割。
        /// </summary>
        public void ProcessDailyDelivery()
        {
            // 获取当前游戏日期
            string currentSeason = Game1.currentSeason;
            int currentDay = Game1.dayOfMonth;

            var instruments = _marketManager.GetInstruments();
            var positions = _brokerageService.Account.Positions.ToList();

            // ========== 1. 处理仓位交割 ==========
            // 遍历所有持仓，查找到期合约
            foreach (var pos in positions)
            {
                var instrument = instruments.FirstOrDefault(i => i.Symbol == pos.Symbol);
                if (instrument is CommodityFutures future)
                {
                    // 检查是否到期：季节和日期都匹配
                    if (future.DeliverySeason.Equals(currentSeason, StringComparison.OrdinalIgnoreCase) && 
                        future.DeliveryDay == currentDay)
                    {
                        ProcessPositionSettlement(pos, future);
                    }
                }
            }

            // ========== 2. 处理到期的限价单（问题3修复） ==========
            ProcessExpiredOrders(currentSeason, currentDay);
        }

        /// <summary>
        /// 处理到期合约的玩家限价单
        /// </summary>
        /// <param name="currentSeason">当前季节</param>
        /// <param name="currentDay">当前日期</param>
        /// <remarks>
        /// WHY（为什么需要这个方法）：
        /// 玩家可能在合约到期日前挂了限价单，但未成交。
        /// 到期时必须强制撤单，否则订单会泄漏到下个合约周期，引发数据污染。
        /// </remarks>
        private void ProcessExpiredOrders(string currentSeason, int currentDay)
        {
            var instruments = _marketManager.GetInstruments();

            foreach (var instrument in instruments)
            {
                if (instrument is CommodityFutures future &&
                    future.DeliverySeason.Equals(currentSeason, StringComparison.OrdinalIgnoreCase) &&
                    future.DeliveryDay == currentDay)
                {
                    var orderBook = _marketManager.GetOrderBook(future.Symbol);
                    if (orderBook == null) continue;

                    var playerOrders = orderBook.GetPlayerOrders().ToList(); // ToList 避免修改集合时的迭代器问题

                    foreach (var order in playerOrders)
                    {
                        _monitor.Log(
                            $"[Delivery] Cancelling expired order: {order.OrderId} " +
                            $"({order.RemainingQuantity} @ {order.Price:F2}g)",
                            LogLevel.Warn
                        );

                        // 强制撤单（会释放冻结的保证金）
                        bool cancelled = _brokerageService.CancelOrder(future.Symbol, order.OrderId);

                        if (cancelled)
                        {
                            // 发送 HUD 警告
                            Game1.addHUDMessage(new HUDMessage(
                                $"合约到期：限价单已撤销 ({order.RemainingQuantity} {future.Name})",
                                3 // type=3 表示警告（黄色）
                            ));
                        }
                    }

                    int cancelledCount = playerOrders.Count;
                    if (cancelledCount > 0)
                    {
                        _monitor.Log(
                            $"[Delivery] Cancelled {cancelledCount} expired orders for {future.Symbol}",
                            LogLevel.Info
                        );
                    }
                }
            }
        }

        /// <summary>
        /// 处理单个仓位的交割结算
        /// </summary>
        /// <param name="pos">要结算的仓位</param>
        /// <param name="future">对应的期货合约</param>
        private void ProcessPositionSettlement(Position pos, CommodityFutures future)
        {
            _monitor.Log($"[Delivery] Settling position {pos.Symbol} x{pos.Quantity}", LogLevel.Info);

            int quantity = Math.Abs(pos.Quantity);
            string itemId = future.UnderlyingItemId;
            var exchangeBoxes = _exchangeService.FindAllExchangeBoxes();

            if (pos.Quantity > 0) // 多头：接收物品
            {
                ProcessLongPosition(quantity, itemId, future.Name, exchangeBoxes);
            }
            else // 空头：交付物品
            {
                ProcessShortPosition(quantity, itemId, future, exchangeBoxes);
            }

            // 交割完成后移除仓位
            _brokerageService.Account.Positions.Remove(pos);
        }

        /// <summary>
        /// 处理多头仓位交割（接收物品）
        /// 优先级：交易所箱子 → 床边掉落
        /// </summary>
        /// <param name="quantity">物品数量</param>
        /// <param name="itemId">物品ID</param>
        /// <param name="itemName">物品名称</param>
        /// <param name="exchangeBoxes">交易所箱子列表</param>
        private void ProcessLongPosition(int quantity, string itemId, string itemName, List<StardewValley.Objects.Chest> exchangeBoxes)
        {
            int remaining = quantity;
            
            // 1. 尝试放入交易所箱子
            foreach (var box in exchangeBoxes)
            {
                Item item = ItemRegistry.Create(itemId, remaining);
                Item? leftOver = box.addItem(item);
                
                if (leftOver == null)
                {
                    // 完全放入成功
                    remaining = 0;
                }
                else
                {
                    // 部分放入，还有剩余
                    remaining = leftOver.Stack;
                }
                
                if (remaining <= 0) break;
            }

            // 2. 如果还有剩余，掉落在床边
            if (remaining > 0)
            {
                Vector2 dropPos = new Vector2(Game1.player.mostRecentBed.X, Game1.player.mostRecentBed.Y) * 64f;
                
                // 如果床位置无效（0,0），使用玩家当前位置
                if (dropPos == Vector2.Zero) 
                    dropPos = Game1.player.Position;

                Item dropItem = ItemRegistry.Create(itemId, remaining);
                Game1.createItemDebris(dropItem, dropPos, 2, Game1.player.currentLocation);
                
                Game1.addHUDMessage(new HUDMessage($"交割: {quantity} {itemName} (溢出物品放在床边)", 2));
            }
            else
            {
                Game1.addHUDMessage(new HUDMessage($"交割: {quantity} {itemName} 已放入交易所箱子", 2));
            }
        }

        /// <summary>
        /// 处理空头仓位交割（交付物品）
        /// 优先级：交易所箱子 → 玩家背包 → 扣除金币（罚金）
        /// </summary>
        /// <param name="quantity">需要交付的数量</param>
        /// <param name="itemId">物品ID</param>
        /// <param name="future">期货合约</param>
        /// <param name="exchangeBoxes">交易所箱子列表</param>
        private void ProcessShortPosition(int quantity, string itemId, CommodityFutures future, List<StardewValley.Objects.Chest> exchangeBoxes)
        {
            int remaining = quantity;

            // 1. 从交易所箱子扣除
            foreach (var box in exchangeBoxes)
            {
                // 从后往前遍历，避免索引问题
                for (int i = box.Items.Count - 1; i >= 0; i--)
                {
                    var item = box.Items[i];
                    if (item != null && item.ItemId == itemId)
                    {
                        int toRemove = Math.Min(remaining, item.Stack);
                        item.Stack -= toRemove;
                        remaining -= toRemove;

                        if (item.Stack <= 0) 
                            box.Items[i] = null;
                        
                        if (remaining <= 0) break;
                    }
                }
                box.clearNulls();
                if (remaining <= 0) break;
            }

            // 2. 从玩家背包扣除
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

                        if (item.Stack <= 0) 
                            Game1.player.Items[i] = null;
                        
                        if (remaining <= 0) break;
                    }
                }
            }

            // 3. 如果仍然不足，扣除金币作为罚金
            if (remaining > 0)
            {
                int missing = remaining;
                decimal penaltyPrice = (decimal)future.CurrentPrice;
                decimal cost = missing * penaltyPrice;
                
                _brokerageService.Account.Cash -= cost;
                Game1.addHUDMessage(new HUDMessage($"交割不足: 以市价 {cost:F0}g 购买了 {missing} {future.Name}", 3));
            }
            else
            {
                Game1.addHUDMessage(new HUDMessage($"交割: 已收取 {quantity} {future.Name}", 2));
            }
        }
    }
}
