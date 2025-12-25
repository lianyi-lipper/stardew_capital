// =====================================================================
// 文件：CostOfCarry.cs
// 用途：持有成本模型（模型三），用于期货定价。
//       基于现货价格和持有成本计算理论期货价格。
// =====================================================================

namespace StardewCapital.Core.Futures.Math;

/// <summary>
/// 持有成本模型：用于期货定价（模型三）。
/// 基于现货价格和持有成本计算理论期货价格。
/// 
/// 公式：F_t = S_t * e^((r + φ - q) * (T - t))
/// 其中：
///   r = 无风险利率（黄金的时间价值）
///   φ = 存储/损耗成本
///   q = 便利收益率（持有实物的收益）
///   T-t = 距到期日天数
/// </summary>
public static class CostOfCarry
{
/// <summary>
/// 计算理论期货价格。
/// 注意：利率参数应为年化值，此方法会自动转换为游戏日利率。
/// </summary>
/// <param name="spotPrice">当前现货价格 S_t</param>
/// <param name="daysToMaturity">距到期天数 (T - t)</param>
/// <param name="riskFreeRate">年化无风险利率 r</param>
/// <param name="storageCost">年化存储成本 φ</param>
/// <param name="convenienceYield">年化便利收益率 q</param>
/// <returns>理论期货价格 F_t</returns>
public static double CalculateFuturesPrice(
    double spotPrice,
    int daysToMaturity,
    double riskFreeRate,
    double storageCost,
    double convenienceYield)
{
    if (daysToMaturity <= 0)
    {
        // 到期时，期货价格等于现货价格
        return spotPrice;
    }
    
    // 游戏年天数（4季节 × 28天）
    const int daysPerYear = 112;
    
    // 年化持有成本率：r + φ - q
    double annualCarryRate = riskFreeRate + storageCost - convenienceYield;
    
    // 转换为到期时间的比例 (T - t) / 年
    double timeToMaturityInYears = (double)daysToMaturity / daysPerYear;
    
    // F_t = S_t * e^(annualCarryRate * timeToMaturityInYears)
    double futuresPrice = spotPrice * System.Math.Exp(annualCarryRate * timeToMaturityInYears);
    
    return futuresPrice;
}
    
    /// <summary>
    /// 根据期货价格反推隐含现货价格。
    /// 注意：利率参数应为年化值。
    /// </summary>
    public static double CalculateImpliedSpot(
        double futuresPrice,
        int daysToMaturity,
        double riskFreeRate,
        double storageCost,
        double convenienceYield)
    {
        if (daysToMaturity <= 0)
        {
            return futuresPrice;
        }
        
        const int daysPerYear = 112;
        double annualCarryRate = riskFreeRate + storageCost - convenienceYield;
        double timeToMaturityInYears = (double)daysToMaturity / daysPerYear;
        
        return futuresPrice * System.Math.Exp(-annualCarryRate * timeToMaturityInYears);
    }
    
    /// <summary>
    /// 计算基差（期货与现货价差）。
    /// 正基差 = 正向市场（contango，期货 > 现货）
    /// 负基差 = 反向市场（backwardation，期货 < 现货）
    /// </summary>
    public static double CalculateBasis(double futuresPrice, double spotPrice)
    {
        return futuresPrice - spotPrice;
    }
    
    /// <summary>
    /// 计算年化基差（便于跨期限比较）。
    /// </summary>
    public static double CalculateAnnualizedBasis(
        double futuresPrice, 
        double spotPrice, 
        int daysToMaturity,
        int daysPerYear = 112) // 4个季节 * 28天
    {
        if (daysToMaturity <= 0 || spotPrice <= 0)
        {
            return 0;
        }
        
        double basis = (futuresPrice - spotPrice) / spotPrice;
        return basis * daysPerYear / daysToMaturity;
    }
    
    /// <summary>
    /// 计算特殊事件带来的便利收益率加成。
    /// 例如：NPC生日会创造礼物需求。
    /// </summary>
    public static double CalculateEventConvenienceBoost(
        double baseYield,
        bool isNpcBirthday,
        bool isCommunityBundleNeeded,
        bool isFestivalItem)
    {
        double yield = baseYield;
        
        if (isNpcBirthday)
        {
            yield += 0.10; // 生日礼物需求 +10%
        }
        
        if (isCommunityBundleNeeded)
        {
            yield += 0.05; // 社区中心收集需求 +5%
        }
        
        if (isFestivalItem)
        {
            yield += 0.03; // 节日需求 +3%
        }
        
        return yield;
    }
}
