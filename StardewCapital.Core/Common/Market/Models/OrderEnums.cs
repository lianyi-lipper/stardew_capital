// =====================================================================
// 文件：OrderEnums.cs
// 用途：订单相关的枚举类型定义。
// =====================================================================

namespace StardewCapital.Core.Common.Market.Models;

/// <summary>
/// 订单方向：买入或卖出。
/// </summary>
public enum OrderSide
{
    Buy,
    Sell
}

/// <summary>
/// 订单类型：市价单或限价单。
/// </summary>
public enum OrderType
{
    Market,
    Limit
}

/// <summary>
/// 订单状态。
/// </summary>
public enum OrderStatus
{
    Pending,
    PartiallyFilled,
    Filled,
    Cancelled
}
