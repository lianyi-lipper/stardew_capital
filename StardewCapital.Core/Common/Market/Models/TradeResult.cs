// =====================================================================
// 文件：TradeResult.cs
// 用途：交易执行结果及相关模型（价格档位、成交记录）。
// =====================================================================

namespace StardewCapital.Core.Common.Market.Models;

/// <summary>
/// 订单簿中的价格档位（按价格聚合）。
/// </summary>
public class PriceLevel
{
    public double Price { get; init; }
    public int TotalQuantity { get; set; }
    public int OrderCount { get; set; }
}

/// <summary>
/// 交易执行结果。
/// </summary>
public class TradeResult
{
    public bool Success { get; init; }
    public string Symbol { get; init; } = string.Empty;
    public OrderSide Side { get; init; }
    public int RequestedQuantity { get; init; }
    public int FilledQuantity { get; init; }
    public double AveragePrice { get; init; }
    public double Slippage { get; init; }
    public string? ErrorMessage { get; init; }
    public List<Trade> Trades { get; init; } = new();
    
    public int UnfilledQuantity => RequestedQuantity - FilledQuantity;
    public double TotalValue => FilledQuantity * AveragePrice;
    
    public static TradeResult Failed(string error, string symbol = "", OrderSide side = OrderSide.Buy)
        => new()
        {
            Success = false,
            Symbol = symbol,
            Side = side,
            ErrorMessage = error
        };
}

/// <summary>
/// 表示单笔成交记录。
/// </summary>
public class Trade
{
    public long TradeId { get; init; }
    public string Symbol { get; init; } = string.Empty;
    public double Price { get; init; }
    public int Quantity { get; init; }
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
    public long BuyOrderId { get; init; }
    public long SellOrderId { get; init; }
}
