// =====================================================================
// 文件：DefaultRandomProvider.cs
// 用途：IRandomProvider 的默认实现，使用 Box-Muller 变换生成高斯分布。
// =====================================================================

namespace StardewCapital.Core.Common;

/// <summary>
/// IRandomProvider 的默认实现，基于 System.Random。
/// 使用 Box-Muller 变换生成高斯分布随机数。
/// </summary>
public class DefaultRandomProvider : IRandomProvider
{
    private readonly Random _random;
    private double? _spareGaussian;
    
    public DefaultRandomProvider()
    {
        _random = new Random();
    }
    
    public DefaultRandomProvider(int seed)
    {
        _random = new Random(seed);
    }
    
    public double NextDouble() => _random.NextDouble();
    
    public int Next(int minValue, int maxValue) => _random.Next(minValue, maxValue);
    
    /// <summary>
    /// 使用 Box-Muller 变换生成标准正态分布随机数。
    /// 每次调用生成两个值，多余的一个缓存供下次调用使用。
    /// </summary>
    public double NextGaussian()
    {
        if (_spareGaussian.HasValue)
        {
            var spare = _spareGaussian.Value;
            _spareGaussian = null;
            return spare;
        }
        
        double u, v, s;
        do
        {
            u = 2.0 * _random.NextDouble() - 1.0;
            v = 2.0 * _random.NextDouble() - 1.0;
            s = u * u + v * v;
        } while (s >= 1.0 || s == 0.0);
        
        var factor = Math.Sqrt(-2.0 * Math.Log(s) / s);
        _spareGaussian = v * factor;
        return u * factor;
    }
}
