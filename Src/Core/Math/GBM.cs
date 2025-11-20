using System;

namespace HedgeHarvest.Core.Math
{
    /// <summary>
    /// Model 2: Geometric Brownian Motion (GBM)
    /// Used for simulating the daily spot price movement towards a target.
    /// Formula: ln(S_{t+1}) = ln(S_t) + alpha * (ln(Target) - ln(S_t)) + sigma * W_t
    /// </summary>
    public class GBM
    {
        /// <summary>
        /// Calculates the next day's price based on GBM with mean reversion.
        /// </summary>
        /// <param name="currentPrice">S_t: Current spot price</param>
        /// <param name="targetPrice">E[S_T]: Expected target price at maturity</param>
        /// <param name="daysRemaining">T - t: Days remaining until maturity</param>
        /// <param name="baseVolatility">sigma_base: Base volatility parameter</param>
        /// <returns>S_{t+1}: Next day's spot price</returns>
        public static double CalculateNextPrice(double currentPrice, double targetPrice, int daysRemaining, double baseVolatility)
        {
            if (daysRemaining <= 0) return targetPrice; // Force convergence at maturity

            // 1. Calculate Pull Factor (alpha)
            // alpha = 1 / (T - t). As t approaches T, alpha approaches 1 (strong pull).
            // We use daysRemaining as (T - t).
            // Avoid division by zero if daysRemaining is 0 (handled above).
            double alpha = 1.0 / daysRemaining;

            // 2. Calculate Dynamic Volatility (sigma_t)
            // sigma_t = sigma_base * sqrt(T - t)
            double sigma_t = baseVolatility * System.Math.Sqrt(daysRemaining);

            // 3. Random Walk (W_t)
            double epsilon = StatisticsUtils.NextGaussian();

            // 4. Apply GBM Formula in Log Space
            // ln(S_{t+1}) = ln(S_t) + alpha * (ln(Target) - ln(S_t)) + sigma_t * epsilon
            double lnS_t = System.Math.Log(currentPrice);
            double lnTarget = System.Math.Log(targetPrice);
            
            double lnS_next = lnS_t + alpha * (lnTarget - lnS_t) + sigma_t * epsilon;

            // 5. Convert back to Price
            return System.Math.Exp(lnS_next);
        }
    }
}
