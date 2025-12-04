using System;

namespace StardewCapital.Core.Math
{
    /// <summary>
    /// 几何布朗运动（GBM）价格模型
    /// 用于模拟期货价格的日间波动，具有均值回归特性。
    /// 
    /// 公式: ln(S_{t+1}) = ln(S_t) + alpha * (ln(Target) - ln(S_t)) + sigma_t * epsilon
    /// - alpha: 回归系数，控制价格向目标价的收敛速度
    /// - sigma_t: 动态波动率，随到期日临近而减小
    /// - epsilon: 标准正态分布随机变量
    /// 
    /// 该模型确保价格在到期日收敛到目标价格（期货标的物的预期价值）。
    /// </summary>
    public class GBM
    {
        /// <summary>
        /// 计算下一个交易日的价格（基于均值回归的GBM）
        /// </summary>
        /// <param name="currentPrice">S_t：当前现货价格</param>
        /// <param name="targetPrice">E[S_T]：到期日的目标价格（期望值）</param>
        /// <param name="daysRemaining">T - t：距离到期日的剩余天数</param>
        /// <param name="baseVolatility">sigma_base：基础波动率参数（通常取0.01-0.05）</param>
        /// <returns>S_{t+1}：下一个交易日的现货价格</returns>
        public static double CalculateNextPrice(double currentPrice, double targetPrice, int daysRemaining, double baseVolatility, Random? random = null)
        {
            // 边界条件：到期日当天强制收敛到目标价格
            if (daysRemaining <= 0) return targetPrice;

            // 1. 计算回归系数 (alpha)
            // alpha = 1 / (T - t)
            // 含义：剩余天数越少，回归力度越强，确保到期日价格收敛
            double alpha = 1.0 / daysRemaining;

            // 2. 计算动态波动率 (sigma_t)
            // sigma_t = sigma_base * sqrt(T - t)
            // 含义：波动率随时间衰减，接近到期日时波动变小
            double sigma_t = baseVolatility * System.Math.Sqrt(daysRemaining);
            if (daysRemaining > 25) {
                sigma_t *= 0.5;  // 前3天减半
            }

            // 3. 生成随机扰动项 (epsilon ~ N(0,1))
            double epsilon = StatisticsUtils.NextGaussian(random);

            // 4. 在对数空间应用 GBM 公式
            // ln(S_{t+1}) = ln(S_t) + alpha * (ln(Target) - ln(S_t)) + sigma_t * epsilon
            // 对数空间可以保证价格始终为正
            double lnS_t = System.Math.Log(currentPrice);
            double lnTarget = System.Math.Log(targetPrice);
            
            double lnS_next = lnS_t + alpha * (lnTarget - lnS_t) + sigma_t * epsilon;

            // 5. 转换回价格空间
            return System.Math.Exp(lnS_next);
        }
    }
}
