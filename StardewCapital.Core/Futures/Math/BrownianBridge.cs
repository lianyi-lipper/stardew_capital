// =====================================================================
// 文件：BrownianBridge.cs
// 用途：布朗桥模型（模型四），用于日内价格模拟。
//       生成平滑的价格路径，并在收盘时收敛至目标价格。
// =====================================================================

using StardewCapital.Core.Common;

namespace StardewCapital.Core.Futures.Math;

/// <summary>
/// 布朗桥：用于日内价格模拟（模型四）。
/// 生成平滑的价格路径，并在收盘时收敛至目标价格。
/// 
/// 公式：P_{τ+1} = P_τ + (P_target - P_τ)/T_remain + σ_intra * Ψ(τ) * ε
/// 
/// 波动率微笑因子 Ψ(τ) = (1 + α * e^(-λ * τ_elapsed)) * √(T_remain / T_total)
///   - 开盘时波动率高（消化隔夜情绪）
///   - 收盘时波动率降低（强制收敛）
/// </summary>
public class BrownianBridge
{
    private readonly IRandomProvider _random;
    
    // 波动率微笑参数
    private readonly double _alpha;   // 开盘活跃度提升系数
    private readonly double _lambda;  // 衰减速率
    
    public BrownianBridge(IRandomProvider? random = null, double alpha = 2.0, double lambda = 10.0)
    {
        _random = random ?? new DefaultRandomProvider();
        _alpha = alpha;
        _lambda = lambda;
    }
    
    /// <summary>
    /// 使用布朗桥计算下一个 tick 的价格。
    /// </summary>
    /// <param name="currentPrice">当前价格 P_τ</param>
    /// <param name="targetPrice">日终目标价格</param>
    /// <param name="ticksRemaining">距离收盘剩余的 tick 数 (T_remain)</param>
    /// <param name="totalTicks">交易日内的总 tick 数 (T_total)</param>
    /// <param name="intradayVolatility">日内波动率 σ_intra</param>
    /// <returns>下一个 tick 的价格 P_{τ+1}</returns>
    public double GetNextPrice(
        double currentPrice,
        double targetPrice,
        int ticksRemaining,
        int totalTicks,
        double intradayVolatility)
    {
        if (ticksRemaining <= 0)
        {
            return targetPrice;
        }
        
        if (ticksRemaining == 1)
        {
            // 最后一个 tick：直接返回目标价格
            return targetPrice;
        }
        
        // 引力项：将价格拉向目标
        double gravity = (targetPrice - currentPrice) / ticksRemaining;
        
        // 计算已进行的时间比例 τ_elapsed ∈ [0, 1]
        double elapsedRatio = 1.0 - (double)ticksRemaining / totalTicks;
        
        // 波动率微笑因子 Ψ(τ)
        double psi = CalculateVolatilitySmile(elapsedRatio, ticksRemaining, totalTicks);
        
        // 随机噪声
        double epsilon = _random.NextGaussian();
        
        // 动态噪声项
        double noise = intradayVolatility * psi * epsilon;
        
        // 计算下一价格
        double nextPrice = currentPrice + gravity + noise;
        
        // 确保价格为正
        return System.Math.Max(0.01, nextPrice);
    }
    
    /// <summary>
    /// 计算波动率微笑因子 Ψ(τ)。
    /// 开盘时高，收盘时趋于零。
    /// </summary>
    private double CalculateVolatilitySmile(double elapsedRatio, int ticksRemaining, int totalTicks)
    {
        // 开盘活跃度项：(1 + α * e^(-λ * τ_elapsed))
        // τ=0 时最高，随时间快速衰减
        double openingBoost = 1.0 + _alpha * System.Math.Exp(-_lambda * elapsedRatio);
        
        // 收盘包络线：√(T_remain / T_total)
        // 临近收盘时强制波动率趋于零
        double closingEnvelope = System.Math.Sqrt((double)ticksRemaining / totalTicks);
        
        return openingBoost * closingEnvelope;
    }
    
    /// <summary>
    /// 生成完整的日内价格路径。
    /// </summary>
    public double[] GenerateIntradayPath(
        double openPrice,
        double closeTarget,
        int totalTicks,
        double intradayVolatility)
    {
        var prices = new double[totalTicks + 1];
        prices[0] = openPrice;
        
        for (int tick = 0; tick < totalTicks; tick++)
        {
            int remaining = totalTicks - tick;
            prices[tick + 1] = GetNextPrice(
                prices[tick],
                closeTarget,
                remaining,
                totalTicks,
                intradayVolatility);
        }
        
        // 强制最终价格等于目标
        prices[totalTicks] = closeTarget;
        
        return prices;
    }
    
    /// <summary>
    /// 处理日中目标价格变化（针对新闻事件）。
    /// 返回考虑目标突变后的新价格。
    /// </summary>
    public double HandleTargetChange(
        double currentPrice,
        double oldTarget,
        double newTarget,
        int ticksRemaining,
        int totalTicks)
    {
        // 新的引力项会自动调整方向
        // 无需重置位置，只需更新目标
        // 下一个 tick 会在 GetNextPrice() 中使用新目标
        return currentPrice; // 价格不会立即跳变，桥梁会逐渐调整
    }
}
