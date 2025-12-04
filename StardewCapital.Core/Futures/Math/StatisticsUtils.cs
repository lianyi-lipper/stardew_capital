using System;

namespace StardewCapital.Core.Futures.Math
{
    /// <summary>
    /// 统计工具类
    /// 提供正态分布随机数生成功能，用于金融模型中的随机波动模拟。
    /// </summary>
    public static class StatisticsUtils
    {
        private static readonly Random _defaultRandom = new Random();

        /// <summary>
        /// 生成标准正态分布 N(0, 1) 的随机数
        /// 使用 Box-Muller 变换算法将均匀分布转换为正态分布。
        /// </summary>
        /// <param name="random">可选的随机数生成器。如果未提供，使用默认的静态生成器。</param>
        /// <returns>符合标准正态分布的随机数（均值=0，标准差=1）</returns>
        public static double NextGaussian(Random? random = null)
        {
            Random rng = random ?? _defaultRandom;

            // 生成两个独立的 (0,1] 均匀分布随机数
            double u1 = 1.0 - rng.NextDouble();
            // 防御性编程：防止 u1 太接近 0 导致 Log(u1) 变为负无穷
            // 虽然理论上 NextDouble < 1.0，但为了安全起见
            if (u1 <= double.Epsilon) u1 = double.Epsilon;

            double u2 = 1.0 - rng.NextDouble();
            
            // Box-Muller 变换公式：sqrt(-2*ln(U1)) * sin(2*π*U2)
            double randStdNormal = System.Math.Sqrt(-2.0 * System.Math.Log(u1)) *
                                   System.Math.Sin(2.0 * System.Math.PI * u2);
            
            return randStdNormal;
        }

        /// <summary>
        /// 生成指定均值和标准差的正态分布随机数 N(mean, stdDev)
        /// </summary>
        /// <param name="mean">均值（期望值）</param>
        /// <param name="stdDev">标准差（波动幅度）</param>
        /// <param name="random">可选的随机数生成器</param>
        /// <returns>符合 N(mean, stdDev) 分布的随机数</returns>
        public static double NextGaussian(double mean, double stdDev, Random? random = null)
        {
            return mean + stdDev * NextGaussian(random);
        }
    }
}

