using System;
using StardewCapital.Core.Time;

namespace StardewCapital.Core.Math
{
    /// <summary>
    /// Model 4: Discrete Brownian Bridge
    /// Used for simulating intraday price movements that converge to a daily target.
    /// </summary>
    public class BrownianBridge
    {
        /// <summary>
        /// Calculates the next tick's price.
        /// P_{tau+1} = P_tau + Gravity + Noise
        /// </summary>
        /// <param name="currentPrice">P_tau: Current price</param>
        /// <param name="targetPrice">P_target: The target price to converge to by end of day</param>
        /// <param name="timeRatio">tau: Current normalized time (0.0 to 1.0)</param>
        /// <param name="intraVolatility">sigma_intra: Intraday volatility parameter</param>
        /// <returns>P_{tau+1}: Next tick price</returns>
        public static double CalculateNextTickPrice(double currentPrice, double targetPrice, double timeRatio, double intraVolatility)
        {
            // 1. Calculate Time Remaining (T_remain)
            // We assume T_total = 1.0 (normalized day)
            double t_remain = 1.0 - timeRatio;

            // Safety check to prevent division by zero or negative time
            if (t_remain <= 0.001) return targetPrice;

            // 2. Gravity (Mean Reversion)
            // Pulls the price towards the target. Stronger as time runs out.
            // Gravity = (Target - Current) / T_remain
            // Note: Since we are doing this per-tick, we need to scale the step size.
            // In a discrete simulation, if we just add (Target - Current)/T_remain, we might overshoot if the step is large.
            // However, following the user's formula: P_{t+1} = P_t + (P_target - P_t)/T_remain + Noise
            // This formula implies T_remain is in "ticks".
            // Let's adapt it to normalized time.
            // If T_remain is 0.5 (half day), and we update every 0.01 (1% of day),
            // The gravity should be proportional to dt / T_remain.
            
            // Let's assume this function is called every "tick".
            // We need to know the "dt" (delta time) of this step to scale correctly.
            // For now, let's use a simplified approach where we assume a fixed number of ticks per day.
            // But the user's formula is explicit: (P_target - P_tau) / T_remain.
            // If T_remain is "number of ticks left", this works perfectly.
            // So we need to know "Ticks Remaining".
            
            // Let's change the signature to accept ticks if possible, or estimate it.
            // Stardew day: 6am to 2am = 20 hours = 1200 minutes.
            // If we update every 10 game minutes, there are 120 steps.
            // Let's assume a standard resolution for T_remain.
            
            // REVISION: Let's stick to the user's formula concept but adapt for continuous time ratio.
            // Gravity = (Target - Current) * (dt / T_remain)
            // Let's assume dt is small (e.g. 0.001).
            double dt = 0.001; // Small step
            double gravity = (targetPrice - currentPrice) * (dt / t_remain);

            // 3. Volatility Smile (Psi)
            // Psi(tau) = (1 + alpha * e^(-lambda * tau)) * sqrt(t_remain)
            // alpha = 2.0 (high opening volatility), lambda = 10.0 (fast decay)
            double alpha = 2.0;
            double lambda = 10.0;
            double openingShock = 1.0 + alpha * System.Math.Exp(-lambda * timeRatio);
            double closingConverge = System.Math.Sqrt(t_remain);
            
            double psi = openingShock * closingConverge;

            // 4. Dynamic Noise
            double epsilon = StatisticsUtils.NextGaussian();
            double noise = intraVolatility * psi * epsilon;

            // 5. Next Price
            return currentPrice + gravity + noise;
        }
        
        /// <summary>
        /// Alternative implementation using explicit tick counts as per user's formula.
        /// </summary>
        public static double CalculateNextTickPriceDiscrete(double currentPrice, double targetPrice, int ticksRemaining, double intraVolatility)
        {
            if (ticksRemaining <= 1) return targetPrice;

            // Gravity: (Target - Current) / TicksRemaining
            double gravity = (targetPrice - currentPrice) / ticksRemaining;

            // Volatility Smile
            // We need total ticks to calculate progress. Let's assume standard day is 120 ticks (10 min intervals).
            int totalTicks = 120; 
            int ticksElapsed = totalTicks - ticksRemaining;
            double timeRatio = (double)ticksElapsed / totalTicks;
            double t_remain_norm = (double)ticksRemaining / totalTicks;

            double alpha = 2.0;
            double lambda = 10.0;
            double openingShock = 1.0 + alpha * System.Math.Exp(-lambda * timeRatio);
            double closingConverge = System.Math.Sqrt(t_remain_norm);
            double psi = openingShock * closingConverge;

            double epsilon = StatisticsUtils.NextGaussian();
            double noise = intraVolatility * psi * epsilon;

            return currentPrice + gravity + noise;
        }
    }
}
