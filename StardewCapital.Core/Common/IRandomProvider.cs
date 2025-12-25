// =====================================================================
// 文件：IRandomProvider.cs
// 用途：定义随机数生成器的抽象接口，便于在测试中替换为确定性序列。
// =====================================================================

namespace StardewCapital.Core.Common;

/// <summary>
/// 随机数生成器接口。
/// 通过抽象化实现可测试性——测试时可替换为确定性序列。
/// </summary>
public interface IRandomProvider
{
    /// <summary>
    /// 返回符合标准正态分布 N(0,1) 的随机数。
    /// </summary>
    double NextGaussian();
    
    /// <summary>
    /// 返回范围 [0, 1) 内的随机浮点数。
    /// </summary>
    double NextDouble();
    
    /// <summary>
    /// 返回范围 [minValue, maxValue) 内的随机整数。
    /// </summary>
    int Next(int minValue, int maxValue);
}
