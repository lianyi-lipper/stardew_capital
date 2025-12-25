// =====================================================================
// 文件：PriceTimePriorityEngine.cs
// 用途：价格-时间优先撮合引擎。
//       实现标准的撮合算法：价格优先，同价格时间优先。
// =====================================================================

using StardewCapital.Core.Common.Market.Interfaces;
using StardewCapital.Core.Common.Market.Models;

namespace StardewCapital.Core.Common.Market.Engines;

/// <summary>
/// 价格-时间优先撮合引擎。
/// 实现标准的撮合算法：价格优先，同价格时间优先。
/// </summary>
public class PriceTimePriorityEngine : IMatchingEngine
{
    private static long _nextTradeId = 1;
    
    /// <summary>
    /// 尝试将传入订单与现有订单进行匹配。
    /// </summary>
    /// <param name="incomingOrder">待匹配的新订单</param>
    /// <param name="bids">当前买单列表（应按价格降序排列）</param>
    /// <param name="asks">当前卖单列表（应按价格升序排列）</param>
    /// <returns>成交记录列表</returns>
    public IList<Trade> Match(Order incomingOrder, IList<Order> bids, IList<Order> asks)
    {
        var trades = new List<Trade>();
        
        if (incomingOrder.RemainingQuantity <= 0)
            return trades;
        
        // 根据订单方向选择对手盘
        var counterOrders = incomingOrder.Side == OrderSide.Buy ? asks : bids;
        
        // 遍历对手盘进行撮合
        foreach (var counterOrder in counterOrders.ToList())
        {
            if (incomingOrder.RemainingQuantity <= 0)
                break;
            
            // 检查价格是否可以成交
            if (!CanMatch(incomingOrder, counterOrder))
                break; // 价格已排序，后续订单也不会成交
            
            // 计算成交数量
            int matchQuantity = System.Math.Min(
                incomingOrder.RemainingQuantity, 
                counterOrder.RemainingQuantity);
            
            // 成交价格使用被动方（挂单方）价格
            double tradePrice = counterOrder.Price;
            
            // 如果是市价单，使用对手方价格
            if (incomingOrder.Type == OrderType.Market)
                tradePrice = counterOrder.Price;
            
            // 创建成交记录
            var trade = new Trade
            {
                TradeId = Interlocked.Increment(ref _nextTradeId),
                Symbol = incomingOrder.Symbol,
                Price = tradePrice,
                Quantity = matchQuantity,
                BuyOrderId = incomingOrder.Side == OrderSide.Buy 
                    ? incomingOrder.OrderId 
                    : counterOrder.OrderId,
                SellOrderId = incomingOrder.Side == OrderSide.Sell 
                    ? incomingOrder.OrderId 
                    : counterOrder.OrderId,
                Timestamp = DateTime.UtcNow
            };
            
            trades.Add(trade);
            
            // 更新订单成交数量
            incomingOrder.Fill(matchQuantity);
            counterOrder.Fill(matchQuantity);
            
            // 如果对手单完全成交，从列表中移除
            if (counterOrder.Status == OrderStatus.Filled)
            {
                counterOrders.Remove(counterOrder);
            }
        }
        
        return trades;
    }
    
    /// <summary>
    /// 检查两个订单是否可以成交。
    /// </summary>
    private bool CanMatch(Order incoming, Order counter)
    {
        // 市价单总是可以成交
        if (incoming.Type == OrderType.Market)
            return true;
        
        // 限价单需要检查价格
        if (incoming.Side == OrderSide.Buy)
        {
            // 买单价格 >= 卖单价格才能成交
            return incoming.Price >= counter.Price;
        }
        else
        {
            // 卖单价格 <= 买单价格才能成交
            return incoming.Price <= counter.Price;
        }
    }
}
