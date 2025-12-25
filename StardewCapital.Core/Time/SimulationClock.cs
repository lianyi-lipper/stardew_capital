// =====================================================================
// 文件：SimulationClock.cs
// 用途：模拟时钟，可独立于游戏时间运行，用于测试和独立模拟。
// =====================================================================

namespace StardewCapital.Core.Time;

/// <summary>
/// 可独立于游戏时间运行的模拟时钟。
/// 适用于测试和独立模拟场景。
/// </summary>
public class SimulationClock : ITimeProvider
{
    private readonly TradingSession _session;
    
    public int CurrentDay { get; private set; }
    public int CurrentTimeOfDay { get; private set; }
    public int CurrentSeason { get; private set; }
    public int DaysInSeason { get; init; } = 28;
    
    public SimulationClock(TradingSession? session = null)
    {
        _session = session ?? TradingSession.Default;
        CurrentDay = 1;
        CurrentSeason = 0;
        CurrentTimeOfDay = _session.MarketOpen;
    }
    
    /// <summary>
    /// 返回交易时段内的标准化日间进度 [0.0, 1.0]。
    /// 0.0 = 开盘时刻，1.0 = 即将收盘。
    /// </summary>
    public double GetDayProgress()
    {
        if (CurrentTimeOfDay <= _session.MarketOpen) return 0.0;
        if (CurrentTimeOfDay >= _session.MarketClose) return 1.0;
        
        double elapsed = CurrentTimeOfDay - _session.MarketOpen;
        double total = _session.MarketClose - _session.MarketOpen;
        return elapsed / total;
    }
    
    /// <summary>
    /// 根据给定的 tick 间隔，返回距离收盘还剩多少 tick。
    /// </summary>
    public int GetRemainingTicks(int totalTicksPerDay)
    {
        double progress = GetDayProgress();
        return (int)((1.0 - progress) * totalTicksPerDay);
    }
    
    /// <summary>
    /// 推进一个 tick（默认10个游戏分钟）。
    /// </summary>
    public void Tick(int gameMinutes = 10)
    {
        CurrentTimeOfDay += gameMinutes;
        
        // 游戏时间格式归一化：60分钟 -> 100单位
        int hours = CurrentTimeOfDay / 100;
        int minutes = CurrentTimeOfDay % 100;
        
        if (minutes >= 60)
        {
            hours += minutes / 60;
            minutes = minutes % 60;
            CurrentTimeOfDay = hours * 100 + minutes;
        }
    }
    
    /// <summary>
    /// 推进到下一天，时间重置为开盘时刻。
    /// </summary>
    public void NextDay()
    {
        CurrentDay++;
        CurrentTimeOfDay = _session.MarketOpen;
        
        if (CurrentDay > DaysInSeason)
        {
            CurrentDay = 1;
            CurrentSeason = (CurrentSeason + 1) % 4;
        }
    }
    
    /// <summary>
    /// 检查市场是否正在开放。
    /// </summary>
    public bool IsMarketOpen()
    {
        return CurrentTimeOfDay >= _session.MarketOpen 
            && CurrentTimeOfDay < _session.MarketClose;
    }
    
    /// <summary>
    /// 设置时钟到特定状态（用于测试）。
    /// </summary>
    public void SetTime(int day, int timeOfDay, int season = 0)
    {
        CurrentDay = day;
        CurrentTimeOfDay = timeOfDay;
        CurrentSeason = season;
    }
}
