using System;
using System.Collections.Generic;
using StardewCapital.Core.Math;
using StardewCapital.Core.Time;
using StardewCapital.Domain.Instruments;

namespace StardewCapital.Services
{
    /// <summary>
    /// 价格引擎
    /// 负责驱动金融产品的价格变化，调用Core层的数学模型。
    /// 
    /// 价格模型：
    /// - 模型2（GBM）：计算每日目标价格，具有均值回归特性
    /// - 模型4（布朗桥）：计算日内tick价格，确保收敛到目标价
    /// </summary>
    public class PriceEngine
    {
        private readonly MixedTimeClock _clock;
        private readonly Random _random = new Random();

        // 配置参数（未来可移至配置文件）
        /// <summary>基础日波动率：控制每日价格波动幅度（2%左右）</summary>
        private const double BASE_VOLATILITY = 0.02;
        
        /// <summary>日内波动率：控制tick间价格噪声大小（0.5%左右）</summary>
        private const double INTRA_VOLATILITY = 0.005;

        public PriceEngine(MixedTimeClock clock)
        {
            _clock = clock;
        }

        /// <summary>
        /// Updates the price of an instrument for the current tick.
        /// Uses Model 4 (Brownian Bridge) for intraday movement.
        /// </summary>
        public void UpdatePrice(IInstrument instrument, double targetPrice)
        {
            double currentPrice = instrument.CurrentPrice;
            double timeRatio = _clock.GetDayProgress();

            // Calculate next tick price using Brownian Bridge
            double nextPrice = BrownianBridge.CalculateNextTickPrice(
                currentPrice, 
                targetPrice, 
                timeRatio, 
                INTRA_VOLATILITY
            );

            // Ensure price doesn't go negative
            instrument.CurrentPrice = System.Math.Max(0.01, nextPrice);
        }

        /// <summary>
        /// Calculates the target price for the end of the day (Model 2: GBM).
        /// This should be called once at the start of the day or when news happens.
        /// </summary>
        public double CalculateDailyTarget(double currentPrice, double fundamentalValue, int daysToMaturity)
        {
            return GBM.CalculateNextPrice(
                currentPrice, 
                fundamentalValue, 
                daysToMaturity, 
                BASE_VOLATILITY
            );
        }
    }
}
