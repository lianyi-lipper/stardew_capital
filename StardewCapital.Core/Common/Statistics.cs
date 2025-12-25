// =====================================================================
// 文件：Statistics.cs
// 用途：金融计算相关的统计工具函数集合。
// =====================================================================

namespace StardewCapital.Core.Common;

/// <summary>
/// 金融计算相关的统计工具函数。
/// </summary>
public static class Statistics
{
    /// <summary>
    /// 标准正态分布的累积分布函数（CDF）。
    /// 使用 Abramowitz-Stegun 近似公式（误差 &lt; 7.5e-8）。
    /// </summary>
    public static double NormalCdf(double x)
    {
        const double a1 = 0.254829592;
        const double a2 = -0.284496736;
        const double a3 = 1.421413741;
        const double a4 = -1.453152027;
        const double a5 = 1.061405429;
        const double p = 0.3275911;
        
        int sign = x < 0 ? -1 : 1;
        x = Math.Abs(x) / Math.Sqrt(2.0);
        
        double t = 1.0 / (1.0 + p * x);
        double y = 1.0 - (((((a5 * t + a4) * t) + a3) * t + a2) * t + a1) * t * Math.Exp(-x * x);
        
        return 0.5 * (1.0 + sign * y);
    }
    
    /// <summary>
    /// 标准正态分布的概率密度函数（PDF）。
    /// </summary>
    public static double NormalPdf(double x)
    {
        return Math.Exp(-0.5 * x * x) / Math.Sqrt(2.0 * Math.PI);
    }
    
    /// <summary>
    /// 计算价格序列的简单移动平均（SMA）。
    /// </summary>
    public static double MovingAverage(IReadOnlyList<double> prices, int period)
    {
        if (prices.Count == 0) return 0;
        
        int count = Math.Min(period, prices.Count);
        double sum = 0;
        
        for (int i = prices.Count - count; i < prices.Count; i++)
        {
            sum += prices[i];
        }
        
        return sum / count;
    }
    
    /// <summary>
    /// 计算数值序列的标准差。
    /// </summary>
    public static double StandardDeviation(IReadOnlyList<double> values)
    {
        if (values.Count < 2) return 0;
        
        double mean = values.Average();
        double sumSquares = values.Sum(v => (v - mean) * (v - mean));
        
        return Math.Sqrt(sumSquares / (values.Count - 1));
    }
    
    /// <summary>
    /// 将数值限制在最小值和最大值之间。
    /// </summary>
    public static double Clamp(double value, double min, double max)
    {
        return Math.Max(min, Math.Min(max, value));
    }
}
