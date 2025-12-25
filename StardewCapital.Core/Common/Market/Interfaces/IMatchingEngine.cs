// =====================================================================
// 文件：IMatchingEngine.cs
// 用途：订单撮合引擎接口，负责将新订单与订单簿中的现有订单进行匹配。
// =====================================================================

using StardewCapital.Core.Common.Market.Models;

namespace StardewCapital.Core.Common.Market.Interfaces;

/// <summary>
/// 订单撮合引擎接口。
/// 负责将传入的订单与订单簿中的现有订单进行匹配。
/// </summary>
public interface IMatchingEngine
{
    /// <summary>
    /// 尝试将传入订单与现有订单进行匹配。
    /// </summary>
    /// <param name="incomingOrder">待匹配的新订单</param>
    /// <param name="bids">当前买单列表（买方）</param>
    /// <param name="asks">当前卖单列表（卖方）</param>
    /// <returns>成交记录列表</returns>
    IList<Trade> Match(Order incomingOrder, IList<Order> bids, IList<Order> asks);
}
