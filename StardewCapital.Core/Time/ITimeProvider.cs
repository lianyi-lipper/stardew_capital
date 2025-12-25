// =====================================================================
// 文件：ITimeProvider.cs
// 用途：时间提供者抽象接口，用于解耦游戏时间。
//       生产环境：读取 Game1.timeOfDay
//       测试环境：可模拟确定性行为
// =====================================================================

namespace StardewCapital.Core.Time;

/// <summary>
/// 时间提供者的抽象接口——实现与游戏时间的解耦。
/// 生产环境：读取 Game1.timeOfDay。
/// 测试环境：可模拟以实现确定性行为。
/// </summary>
public interface ITimeProvider
{
    /// <summary>
    /// 当前季节的第几天（1-28）。
    /// </summary>
    int CurrentDay { get; }
    
    /// <summary>
    /// 当前游戏时间（600 = 上午6:00，1400 = 下午2:00，以此类推）。
    /// </summary>
    int CurrentTimeOfDay { get; }
    
    /// <summary>
    /// 每个季节的天数（通常为28天）。
    /// </summary>
    int DaysInSeason { get; }
    
    /// <summary>
    /// 当前季节（0=春季，1=夏季，2=秋季，3=冬季）。
    /// </summary>
    int CurrentSeason { get; }
}
