// =====================================================================
// 文件：TradingSession.cs
// 用途：定义交易时段参数（开盘/收盘时间、tick间隔等）。
// =====================================================================

namespace StardewCapital.Core.Time;

/// <summary>
/// 定义交易时段的时间参数。
/// </summary>
public class TradingSession
{
    /// <summary>
    /// 开盘时间（游戏格式，例如 600 = 上午6:00）。
    /// </summary>
    public int MarketOpen { get; init; } = 600;
    
    /// <summary>
    /// 收盘时间（游戏格式，例如 1400 = 下午2:00）。
    /// </summary>
    public int MarketClose { get; init; } = 1400;
    
    /// <summary>
    /// 单个 tick 的实际毫秒时长。
    /// </summary>
    public int TickIntervalMs { get; init; } = 700;
    
    /// <summary>
    /// 返回一个交易日的总交易分钟数。
    /// </summary>
    public int TotalTradingMinutes => (MarketClose - MarketOpen) / 100 * 60 
                                     + (MarketClose - MarketOpen) % 100;
    
    /// <summary>
    /// 默认交易时段（上午6:00 - 下午2:00）。
    /// </summary>
    public static TradingSession Default => new();
}
