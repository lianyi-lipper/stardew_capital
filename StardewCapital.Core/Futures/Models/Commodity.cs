// =====================================================================
// 文件：Commodity.cs
// 用途：商品模型（期货合约的标的资产），及季节枚举定义。
// =====================================================================

namespace StardewCapital.Core.Futures.Models;

/// <summary>
/// 季节枚举，对应星露谷物语的季节。
/// </summary>
public enum Season
{
    Spring = 0,
    Summer = 1,
    Fall = 2,
    Winter = 3
}

/// <summary>
/// 商品模型（期货合约的标的资产）。
/// </summary>
public record Commodity
{
    /// <summary>
    /// 唯一标识符（例如："PARSNIP"、"CAULIFLOWER"）。
    /// </summary>
    public string Symbol { get; init; } = string.Empty;
    
    /// <summary>
    /// 显示名称（例如："防风草"）。
    /// </summary>
    public string Name { get; init; } = string.Empty;
    
    /// <summary>
    /// 基础价格（金币，皮埃尔商店售价）。
    /// </summary>
    public double BasePrice { get; init; }
    
    /// <summary>
    /// 基础需求量——标准化需求水平（默认10000）。
    /// </summary>
    public double BaseDemand { get; init; } = 10000;
    
    /// <summary>
    /// 基础供给量——标准化供给水平（默认10000）。
    /// </summary>
    public double BaseSupply { get; init; } = 10000;
    
    /// <summary>
    /// 可种植的季节列表。
    /// </summary>
    public Season[] GrowSeasons { get; init; } = Array.Empty<Season>();
    
    /// <summary>
    /// 非当季乘数（不可种植时的稀缺溢价）。
    /// </summary>
    public double OffSeasonMultiplier { get; init; } = 5.0;
    
    /// <summary>
    /// 检查该商品是否在当前季节可种植。
    /// </summary>
    public bool IsInSeason(Season currentSeason)
        => GrowSeasons.Contains(currentSeason);
    
    /// <summary>
    /// 获取季节乘数 λ_s。
    /// 当季返回 1.0，非当季返回 OffSeasonMultiplier。
    /// </summary>
    public double GetSeasonalMultiplier(Season currentSeason)
        => IsInSeason(currentSeason) ? 1.0 : OffSeasonMultiplier;
    
    // 常见商品预设
    public static Commodity Parsnip => new()
    {
        Symbol = "PARSNIP",
        Name = "Parsnip",
        BasePrice = 35,
        GrowSeasons = new[] { Season.Spring }
    };
    
    public static Commodity Cauliflower => new()
    {
        Symbol = "CAULIFLOWER",
        Name = "Cauliflower",
        BasePrice = 175,
        GrowSeasons = new[] { Season.Spring }
    };
    
    public static Commodity Strawberry => new()
    {
        Symbol = "STRAWBERRY",
        Name = "Strawberry",
        BasePrice = 120,
        GrowSeasons = new[] { Season.Spring }
    };
    
    public static Commodity Melon => new()
    {
        Symbol = "MELON",
        Name = "Melon",
        BasePrice = 250,
        GrowSeasons = new[] { Season.Summer }
    };
    
    public static Commodity Pumpkin => new()
    {
        Symbol = "PUMPKIN",
        Name = "Pumpkin",
        BasePrice = 320,
        GrowSeasons = new[] { Season.Fall }
    };
}
