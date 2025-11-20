using NUnit.Framework;
using HedgeHarvest.Core.Math;
using System;
using System.Collections.Generic;

namespace HedgeHarvest.Tests
{
    public class MathTests
    {
        [Test]
        public void Test_StatisticsUtils_NormalDistribution()
        {
            // Verify that the mean is roughly 0 and stdDev is roughly 1
            int samples = 10000;
            double sum = 0;
            double sumSq = 0;

            for (int i = 0; i < samples; i++)
            {
                double val = StatisticsUtils.NextGaussian();
                sum += val;
                sumSq += val * val;
            }

            double mean = sum / samples;
            double variance = (sumSq / samples) - (mean * mean);
            double stdDev = System.Math.Sqrt(variance);

            Assert.That(mean, Is.EqualTo(0).Within(0.05));
            Assert.That(stdDev, Is.EqualTo(1).Within(0.05));
        }

        [Test]
        public void Test_GBM_Convergence()
        {
            // Test that GBM converges to target as daysRemaining -> 0
            double current = 100;
            double target = 120;
            double vol = 0.1;

            // Day 5 remaining
            double next = GBM.CalculateNextPrice(current, target, 5, vol);
            Assert.That(next, Is.Not.NaN);

            // Day 0 remaining (should be exactly target)
            double final = GBM.CalculateNextPrice(current, target, 0, vol);
            Assert.That(final, Is.EqualTo(target));
        }

        [Test]
        public void Test_BrownianBridge_Convergence()
        {
            double current = 100;
            double target = 105;
            double vol = 0.5;

            // 模拟一整天，用120个步长从0.0到1.0
            List<double> prices = new List<double>();
            prices.Add(current);

            int totalSteps = 120;
            for (int i = 0; i < totalSteps; i++)
            {
                double prev = prices[prices.Count - 1];
                double timeRatio = (double)i / totalSteps; // 0.0 到接近 1.0
                double next = BrownianBridge.CalculateNextTickPrice(prev, target, timeRatio, vol);
                prices.Add(next);
            }

            // 最后的价格应该非常接近目标值
            double finalPrice = prices[prices.Count - 1];
            Assert.That(finalPrice, Is.EqualTo(target).Within(0.5)); // 放宽容差，因为随机性
        }
    }
}
