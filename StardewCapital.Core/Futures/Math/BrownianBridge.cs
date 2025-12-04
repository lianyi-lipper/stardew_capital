using System;
using StardewCapital.Core.Common.Time;

namespace StardewCapital.Core.Futures.Math
{
    /// <summary>
    /// 离散布朗桥价格模型
    /// 用于模拟日内价格波动，确保价格在一天结束时收敛到目标值。
    /// 
    /// 核心特性：
    /// 1. 引力（Gravity）：将价格拉向目标，时间越晚引力越强
    /// 2. 波动率微笑（Volatility Smile）：开盘时波动大，收盘时波动小
    /// 3. 动态噪声：根据时间进度调整随机扰动幅度
    /// </summary>
    public class BrownianBridge
    {
        /// <summary>
        /// 计算下一帧的价格（连续时间版本）
        /// 公式：P_{tau+1} = P_tau + Gravity + Noise
        /// </summary>
        /// <param name="currentPrice">P_tau：当前价格</param>
        /// <param name="targetPrice">P_target：当天的目标价格（收盘价）</param>
        /// <param name="timeRatio">tau：当前时间进度（0.0 = 开盘，1.0 = 收盘）</param>
        /// <param name="timeStep">dt：这一帧的时间步长（当前帧 timeRatio - 上一帧 timeRatio）</param>
        /// <param name="intraVolatility">sigma_intra：日内波动率参数</param>
        /// <param name="alpha">开盘冲击系数（默认2.0）</param>
        /// <param name="lambda">冲击衰减速度（默认10.0）</param>
        /// <returns>P_{tau+1}：下一帧的价格</returns>
        public static double CalculateNextTickPrice(
            double currentPrice, 
            double targetPrice, 
            double timeRatio, 
            double timeStep, 
            double intraVolatility, 
            Random? random = null,
            double alpha = 2.0,
            double lambda = 10.0,
            double noiseScaleFactor = 5.0)
        {
            // 1. 计算剩余时间比例 (T_remain = 1.0 - tau)
            double t_remain = 1.0 - timeRatio;

            // 边界检查：接近收盘时直接返回目标价格
            if (t_remain <= 0.001) return targetPrice;

            // 2. 计算引力（均值回归）
            // Gravity = (Target - Current) * (timeStep / T_remain)
            // 引力将价格拉向目标，时间越晚引力越强
            // 使用动态 timeStep 而非硬编码，确保在时间倍速变化时引力计算正确
            double gravity = (targetPrice - currentPrice) * (timeStep / t_remain);

            // 3. 计算波动率微笑因子 (Psi)
            // Psi(tau) = (1 + alpha * e^(-lambda * tau)) * sqrt(T_remain)
            // - 开盘冲击：alpha 导致开盘时波动大
            // - 收盘收敛：sqrt(T_remain) 导致收盘时波动小
            double openingShock = 1.0 + alpha * System.Math.Exp(-lambda * timeRatio);
            double closingConverge = System.Math.Sqrt(t_remain);
            
            double psi = openingShock * closingConverge;

            // 4. 计算动态噪声
            double epsilon = StatisticsUtils.NextGaussian(random);
            double noise = intraVolatility * psi * epsilon * noiseScaleFactor;

            // 5. 计算下一价格 = 当前价格 + 引力 + 噪声
            return currentPrice + gravity + noise;
        }

    }
}

