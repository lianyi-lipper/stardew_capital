// =====================================================================
// 文件：IOrderBook.cs
// 用途：订单簿通用接口，支持任何可交易资产类型。
// =====================================================================

using StardewCapital.Core.Common.Market.Models;

namespace StardewCapital.Core.Common.Market.Interfaces;

/// <summary>
/// 通用订单簿接口，适用于任何可交易资产。
/// </summary>
/// <typeparam name="TAsset">可交易资产的类型</typeparam>
public interface IOrderBook<TAsset> where TAsset : ITradable
{
    /// <summary>
    /// 订单簿对应的资产。
    /// </summary>
    TAsset Asset { get; }
    
    /// <summary>
    /// 最优买价（最高买单价格）。
    /// </summary>
    double? BestBid { get; }
    
    /// <summary>
    /// 最优卖价（最低卖单价格）。
    /// </summary>
    double? BestAsk { get; }
    
    /// <summary>
    /// 中间价 = (最优买价 + 最优卖价) / 2。
    /// </summary>
    double? MidPrice { get; }
    
    /// <summary>
    /// 买卖价差 = 最优卖价 - 最优买价。
    /// </summary>
    double? Spread { get; }
    
    /// <summary>
    /// 在订单簿中挂单。
    /// </summary>
    TradeResult PlaceOrder(Order order);
    
    /// <summary>
    /// 立即执行市价单。
    /// </summary>
    TradeResult ExecuteMarketOrder(OrderSide side, int quantity);
    
    /// <summary>
    /// 根据订单ID撤单。
    /// </summary>
    bool CancelOrder(long orderId);
    
    /// <summary>
    /// 获取前N档买单（买方）价格档位。
    /// </summary>
    IReadOnlyList<PriceLevel> GetBids(int depth);
    
    /// <summary>
    /// 获取前N档卖单（卖方）价格档位。
    /// </summary>
    IReadOnlyList<PriceLevel> GetAsks(int depth);
    
    /// <summary>
    /// 获取某交易者的所有活跃订单。
    /// </summary>
    IReadOnlyList<Order> GetOrdersByTrader(string traderId);
}
