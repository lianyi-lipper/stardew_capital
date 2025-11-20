using System;

namespace StardewCapital.Core.Math
{
    public static class StatisticsUtils
    {
        private static readonly Random _random = new Random();

        /// <summary>
        /// Generates a random number from a standard normal distribution N(0, 1)
        /// using the Box-Muller transform.
        /// </summary>
        public static double NextGaussian()
        {
            double u1 = 1.0 - _random.NextDouble(); // uniform(0,1] random doubles
            double u2 = 1.0 - _random.NextDouble();
            
            double randStdNormal = System.Math.Sqrt(-2.0 * System.Math.Log(u1)) *
                                   System.Math.Sin(2.0 * System.Math.PI * u2);
            
            return randStdNormal;
        }

        /// <summary>
        /// Generates a random number from a normal distribution N(mean, stdDev).
        /// </summary>
        public static double NextGaussian(double mean, double stdDev)
        {
            return mean + stdDev * NextGaussian();
        }
    }
}
