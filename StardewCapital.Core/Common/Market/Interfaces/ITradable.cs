// =====================================================================
// 文件：ITradable.cs
// 用途：可交易资产的基础接口（期货、股票、期权等）。
// =====================================================================

namespace StardewCapital.Core.Common.Market.Interfaces;

/// <summary>
/// 所有可交易资产的基础接口（期货、股票、期权等）。
/// </summary>
public interface ITradable
{
    /// <summary>
    /// 唯一标识符（例如："PARSNIP-SPR-28"）。
    /// </summary>
    string Symbol { get; }
    
    /// <summary>
    /// 当前市场价格。
    /// </summary>
    double CurrentPrice { get; }
    
    /// <summary>
    /// 最小价格变动单位（跳动点）。
    /// </summary>
    double TickSize { get; }
}
