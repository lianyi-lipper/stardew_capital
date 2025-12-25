// =====================================================================
// 文件：GBM.cs
// 用途：带均值回归的几何布朗运动（模型二）。
//       模拟日间价格变动，并向基本面价值收敛。
//       现在支持配置化的波动率、趋势惯性和波动率聚集。
//       注意：这里计算的是现货价格，期货价格由 CostOfCarry 模型叠加持有成本
// =====================================================================

using StardewCapital.Core.Common;

namespace StardewCapital.Core.Futures.Math;

/// <summary>
/// 价格行为参数，用于控制 GBM 的行为。
/// </summary>
public record PriceBehaviorParams
{
    /// <summary>
    /// 基础波动率（默认 2%）。
    /// </summary>
    public double BaseVolatility { get; init; } = 0.02;
    
    /// <summary>
    /// 趋势惯性因子 0-1（默认 0.3）。
    /// 值越大，价格越倾向于延续之前的趋势。
    /// </summary>
    public double MomentumFactor { get; init; } = 0.3;
    
    /// <summary>
    /// 均值回归速度 0-1（默认 0.15）。
    /// 值越大，价格越快回归目标。
    /// </summary>
    public double MeanReversionSpeed { get; init; } = 0.15;
    
    /// <summary>
    /// 波动率聚集效应 0-1（默认 0.6）。
    /// 值越大，高波动后越可能继续高波动。
    /// </summary>
    public double VolatilityClustering { get; init; } = 0.6;
    
    /// <summary>
    /// 跳空概率（默认 1%）。
    /// </summary>
    public double JumpProbability { get; init; } = 0.01;
    
    /// <summary>
    /// 跳空幅度（默认 3%）。
    /// </summary>
    public double JumpMagnitude { get; init; } = 0.03;
    
    /// <summary>
    /// 默认参数。
    /// </summary>
    public static PriceBehaviorParams Default => new();
}

/// <summary>
/// 带均值回归的几何布朗运动（模型二）。
/// 模拟日间价格变动，并向基本面价值收敛。
/// 
/// 公式：ln(S_{t+1}) = ln(S_t) + α_t(ln(Target) - ln(S_t)) + μ * lastReturn + σ_t * ε
/// 其中：
///   α_t = meanReversionSpeed / (T - t)  -- 回归强度随到期临近而增加
///   μ = momentumFactor  -- 趋势惯性
///   σ_t = σ_base * volatilityMultiplier * √(T - t)  -- 动态波动率
/// </summary>
public class GBM
{
    private readonly IRandomProvider _random;
    
    // 状态变量
    private double _lastReturn;          // 上一次的收益率（用于趋势惯性）
    private double _volatilityState = 1.0; // 波动率状态（用于聚集效应）
    
    public GBM(IRandomProvider? random = null)
    {
        _random = random ?? new DefaultRandomProvider();
    }
    
    /// <summary>
    /// 重置状态（新合约或新季节时调用）。
    /// </summary>
    public void Reset()
    {
        _lastReturn = 0;
        _volatilityState = 1.0;
    }
    
    /// <summary>
    /// 使用均值回归 GBM 计算次日价格（使用默认参数）。
    /// </summary>
    public double CalculateNextPrice(
        double currentPrice, 
        double targetPrice, 
        int daysRemaining, 
        double baseVolatility)
    {
        return CalculateNextPriceAdvanced(
            currentPrice, 
            targetPrice, 
            daysRemaining, 
            new PriceBehaviorParams { BaseVolatility = baseVolatility });
    }
    
    /// <summary>
    /// 使用均值回归 GBM 计算次日价格（使用完整参数）。
    /// </summary>
    /// <param name="currentPrice">当前价格 S_t</param>
    /// <param name="targetPrice">目标价格（基本面价值）S_T</param>
    /// <param name="daysRemaining">距到期天数 (T - t)</param>
    /// <param name="behavior">价格行为参数</param>
    /// <returns>次日价格 S_{t+1}</returns>
    public double CalculateNextPriceAdvanced(
        double currentPrice, 
        double targetPrice, 
        int daysRemaining, 
        PriceBehaviorParams behavior)
    {
        if (daysRemaining <= 0)
        {
            // 到期时，价格等于目标价格
            return targetPrice;
        }
        
        if (daysRemaining == 1)
        {
            // 最后一天：强力收敛，噪声极小
            double alpha = 0.9;
            double logCurrent = System.Math.Log(currentPrice);
            double logTarget = System.Math.Log(targetPrice);
            double logNext = logCurrent + alpha * (logTarget - logCurrent);
            return System.Math.Exp(logNext);
        }
        
        // 计算回归系数 α_t = max(k, 1/(T-t))
        // k = 最小回归速度，确保每天至少修复 k% 的价差（套利者纠偏）
        // 1/(T-t) = 交割日强制收敛逻辑
        const double minReversionRate = 0.15;  // 每天至少修复 15% 的价差
        double convergenceRate = 1.0 / daysRemaining;
        double alpha_t = System.Math.Max(minReversionRate, convergenceRate);
        
        // 更新波动率状态（GARCH-like 效应）
        // σ_state = clustering * σ_state + (1 - clustering) * |lastReturn| / baseVol
        if (_lastReturn != 0)
        {
            double normalizedReturn = System.Math.Abs(_lastReturn) / behavior.BaseVolatility;
            _volatilityState = behavior.VolatilityClustering * _volatilityState 
                             + (1 - behavior.VolatilityClustering) * normalizedReturn;
            _volatilityState = System.Math.Max(0.5, System.Math.Min(2.5, _volatilityState)); // 限制范围
        }
        
        // 计算时变波动率（平方根衰减公式）
        // σ_t = σ_max * √((T - t) / T)
        // - 前期波动大，后期逐渐收敛
        // - 归一化避免数值爆炸
        const int totalDays = 28; // 季节总天数
        double volatilityDecay = System.Math.Sqrt((double)daysRemaining / totalDays);
        double sigma_t = behavior.BaseVolatility * _volatilityState * volatilityDecay;
        
        // 随机冲击 ε ~ N(0, 1)
        double epsilon = _random.NextGaussian();
        
        // 对数价格演变
        double logCurrent_ = System.Math.Log(currentPrice);
        double logTarget_ = System.Math.Log(targetPrice);
        
        // 趋势惯性项
        double momentumTerm = behavior.MomentumFactor * _lastReturn;
        
        // 跳空检测
        double jumpTerm = 0;
        if (_random.NextDouble() < behavior.JumpProbability)
        {
            // 随机跳空方向
            double jumpDirection = _random.NextDouble() > 0.5 ? 1 : -1;
            jumpTerm = jumpDirection * behavior.JumpMagnitude;
        }
        
        // ln(S_{t+1}) = ln(S_t) + α_t(ln(Target) - ln(S_t)) + momentum + σ_t * ε + jump
        double logNext_ = logCurrent_ 
                        + alpha_t * (logTarget_ - logCurrent_) 
                        + momentumTerm
                        + sigma_t * epsilon
                        + jumpTerm;
        
        double nextPrice = System.Math.Exp(logNext_);
        
        // 更新上一次收益率（用于下次的趋势惯性）
        _lastReturn = logNext_ - logCurrent_;
        
        // 确保价格为正且合理
        return System.Math.Max(0.01, nextPrice);
    }
    
    /// <summary>
    /// 生成整个季节的价格路径（使用默认参数）。
    /// </summary>
    public double[] GeneratePricePath(
        double startPrice,
        double targetPrice,
        int totalDays,
        double baseVolatility)
    {
        return GeneratePricePathAdvanced(
            startPrice, 
            targetPrice, 
            totalDays, 
            new PriceBehaviorParams { BaseVolatility = baseVolatility });
    }
    
    /// <summary>
    /// 生成整个季节的价格路径（使用完整参数）。
    /// </summary>
    public double[] GeneratePricePathAdvanced(
        double startPrice,
        double targetPrice,
        int totalDays,
        PriceBehaviorParams behavior)
    {
        Reset(); // 重置状态
        
        var prices = new double[totalDays + 1];
        prices[0] = startPrice;
        
        for (int day = 0; day < totalDays; day++)
        {
            int daysRemaining = totalDays - day;
            prices[day + 1] = CalculateNextPriceAdvanced(
                prices[day], 
                targetPrice, 
                daysRemaining, 
                behavior);
        }
        
        // 强制最终价格等于目标
        prices[totalDays] = targetPrice;
        
        return prices;
    }
    
    /// <summary>
    /// 获取当前波动率状态（用于调试）。
    /// </summary>
    public double CurrentVolatilityState => _volatilityState;
}
