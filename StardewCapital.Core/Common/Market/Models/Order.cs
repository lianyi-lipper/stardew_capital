// =====================================================================
// 文件：Order.cs
// 用途：订单模型，表示订单簿中的一个订单实体。
// =====================================================================

namespace StardewCapital.Core.Common.Market.Models;

/// <summary>
/// 表示订单簿中的一个订单。
/// </summary>
public class Order
{
    private static long _nextId = 1;
    
    public long OrderId { get; }
    public string Symbol { get; init; } = string.Empty;
    public OrderSide Side { get; init; }
    public OrderType Type { get; init; }
    public double Price { get; init; }
    public int Quantity { get; private set; }
    public int FilledQuantity { get; private set; }
    public OrderStatus Status { get; private set; }
    public DateTime Timestamp { get; init; }
    public string TraderId { get; init; } = "Player";
    public bool IsPlayerOrder { get; init; } = true;
    
    public int RemainingQuantity => Quantity - FilledQuantity;
    
    public Order()
    {
        OrderId = Interlocked.Increment(ref _nextId);
        Timestamp = DateTime.UtcNow;
        Status = OrderStatus.Pending;
    }
    
    /// <summary>
    /// 成交部分订单数量。
    /// </summary>
    public void Fill(int quantity)
    {
        FilledQuantity += quantity;
        Status = FilledQuantity >= Quantity 
            ? OrderStatus.Filled 
            : OrderStatus.PartiallyFilled;
    }
    
    /// <summary>
    /// 撤销订单。
    /// </summary>
    public void Cancel()
    {
        Status = OrderStatus.Cancelled;
    }
    
    /// <summary>
    /// 创建市价买单。
    /// </summary>
    public static Order MarketBuy(string symbol, int quantity, bool isPlayer = true)
        => new()
        {
            Symbol = symbol,
            Side = OrderSide.Buy,
            Type = OrderType.Market,
            Quantity = quantity,
            IsPlayerOrder = isPlayer
        };
    
    /// <summary>
    /// 创建市价卖单。
    /// </summary>
    public static Order MarketSell(string symbol, int quantity, bool isPlayer = true)
        => new()
        {
            Symbol = symbol,
            Side = OrderSide.Sell,
            Type = OrderType.Market,
            Quantity = quantity,
            IsPlayerOrder = isPlayer
        };
    
    /// <summary>
    /// 创建限价买单。
    /// </summary>
    public static Order LimitBuy(string symbol, double price, int quantity, bool isPlayer = true)
        => new()
        {
            Symbol = symbol,
            Side = OrderSide.Buy,
            Type = OrderType.Limit,
            Price = price,
            Quantity = quantity,
            IsPlayerOrder = isPlayer
        };
    
    /// <summary>
    /// 创建限价卖单。
    /// </summary>
    public static Order LimitSell(string symbol, double price, int quantity, bool isPlayer = true)
        => new()
        {
            Symbol = symbol,
            Side = OrderSide.Sell,
            Type = OrderType.Limit,
            Price = price,
            Quantity = quantity,
            IsPlayerOrder = isPlayer
        };
}
