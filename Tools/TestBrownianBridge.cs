// 测试程序：验证布朗桥噪声生成
using System;
using StardewCapital.Core.Math;

class TestBrownianBridge
{
    static void Main()
    {
        var random = new Random();
        
        Console.WriteLine("测试布朗桥噪声生成：");
        Console.WriteLine("intraVolatility = 0.2");
        Console.WriteLine("alpha = 2.0, lambda = 10.0");
        Console.WriteLine();
        
        double startPrice = 43.0;
        double targetPrice = 41.2;
        int steps = 120;
        
        double currentPrice = startPrice;
        
        for (int i = 1; i <= 10; i++)  // 只测试前10步
        {
            double timeRatio = (double)(i - 1) / (steps - 1);
            double timeStep = 1.0 / (steps - 1);
            
            // 手动计算 psi
            double alpha = 2.0;
            double lambda = 10.0;
            double t_remain = 1.0 - timeRatio;
            double openingShock = 1.0 + alpha * Math.Exp(-lambda * timeRatio);
            double closingConverge = Math.Sqrt(t_remain);
            double psi = openingShock * closingConverge;
            
            // 手动计算噪声
            double epsilon = StatisticsUtils.NextGaussian(random);
            double noise = 0.2 * psi * epsilon * Math.Sqrt(timeStep);
            
            double gravity = (targetPrice - currentPrice) * (timeStep / t_remain);
            double nextPrice = currentPrice + gravity + noise;
            
            Console.WriteLine($"Step {i}:");
            Console.WriteLine($"  timeRatio={timeRatio:F4}, psi={psi:F3}, epsilon={epsilon:F3}");
            Console.WriteLine($"  gravity={gravity:F4}g, noise={noise:F4}g");
            Console.WriteLine($"  price: {currentPrice:F2}g -> {nextPrice:F2}g");
            Console.WriteLine();
            
            currentPrice = nextPrice;
        }
    }
}
