// =====================================================================
// 文件：FuturesParameters.cs
// 用途：期货定价的市场参数配置（模型三的参数）。
// =====================================================================

namespace StardewCapital.Core.Futures.Models;

/// <summary>
/// 期货定价的市场参数（模型三的参数）。
/// 注意：利率和成本参数均为年化值，会在计算时根据游戏年天数转换。
/// </summary>
public record FuturesParameters
{
    /// <summary>
    /// 游戏一年的天数（4个季节 × 28天）。
    /// </summary>
    public const int DaysPerYear = 112;
    
    /// <summary>
    /// 年化无风险利率 r。
    /// 默认：0.03（年利率 3%）
    /// </summary>
    public double RiskFreeRate { get; init; } = 0.03;
    
    /// <summary>
    /// 年化存储/损耗成本 φ。
    /// 默认：0.05（年成本 5%）
    /// </summary>
    public double StorageCost { get; init; } = 0.05;
    
    /// <summary>
    /// 年化便利收益率 q（持有实物的收益）。
    /// 默认：0.02（年收益 2%）
    /// </summary>
    public double BaseConvenienceYield { get; init; } = 0.02;
    
    /// <summary>
    /// 价格变动的基础日波动率 σ_base。
    /// 默认：0.02（日波动率 2%）
    /// </summary>
    public double BaseVolatility { get; init; } = 0.02;
    
    /// <summary>
    /// 布朗桥模型的日内波动率。
    /// 默认：0.01（日内波动 1%）
    /// </summary>
    public double IntradayVolatility { get; init; } = 0.01;
    
    /// <summary>
    /// 初始保证金率（10% = 10倍杠杆）。
    /// </summary>
    public double InitialMarginRatio { get; init; } = 0.10;
    
    /// <summary>
    /// 维持保证金率（低于此值触发追保）。
    /// </summary>
    public double MaintenanceMarginRatio { get; init; } = 0.08;
    
    /// <summary>
    /// 强制平仓阈值。
    /// </summary>
    public double LiquidationMarginRatio { get; init; } = 0.05;
    
    /// <summary>
    /// 合约规模（每手单位数量）。
    /// </summary>
    public int ContractSize { get; init; } = 100;
    
    /// <summary>
    /// 年化持有成本率：r + φ - q
    /// </summary>
    public double AnnualCostOfCarryRate => RiskFreeRate + StorageCost - BaseConvenienceYield;
    
    /// <summary>
    /// 日持有成本率（年化除以游戏年天数）
    /// </summary>
    public double DailyCostOfCarryRate => AnnualCostOfCarryRate / DaysPerYear;
    
    /// <summary>
    /// 默认市场参数。
    /// </summary>
    public static FuturesParameters Default => new();
}
