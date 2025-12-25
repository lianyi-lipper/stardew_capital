// =====================================================================
// 文件：MarketConfigLoader.cs
// 用途：从 JSON 文件加载全局市场配置。
// =====================================================================

using System.Text.Json;
using StardewCapital.Core.Common;

namespace StardewCapital.Core.Futures.Config;

/// <summary>
/// 全局市场配置加载器。
/// </summary>
public class MarketConfigLoader
{
    private MarketConfig? _config;
    private readonly IRandomProvider _random;
    
    public MarketConfigLoader(IRandomProvider? random = null)
    {
        _random = random ?? new DefaultRandomProvider();
    }
    
    /// <summary>
    /// 从 JSON 文件加载配置。
    /// </summary>
    public void LoadFromFile(string filePath)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"市场配置文件未找到: {filePath}");
        }
        
        string json = File.ReadAllText(filePath);
        LoadFromJson(json);
    }
    
    /// <summary>
    /// 从 JSON 字符串加载配置。
    /// </summary>
    public void LoadFromJson(string json)
    {
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip
        };
        
        _config = JsonSerializer.Deserialize<MarketConfig>(json, options)
            ?? throw new InvalidOperationException("无法解析市场配置");
    }
    
    /// <summary>
    /// 获取当前时间段名称。
    /// </summary>
    public string GetCurrentTimePeriod(int gameTime)
    {
        if (_config == null) return "morning";
        
        foreach (var (name, period) in _config.NewsTiming.TimePeriods)
        {
            // 处理跨夜的情况
            if (period.StartTime > period.EndTime)
            {
                if (gameTime >= period.StartTime || gameTime < period.EndTime)
                    return name;
            }
            else
            {
                if (gameTime >= period.StartTime && gameTime < period.EndTime)
                    return name;
            }
        }
        
        return "morning";
    }
    
    /// <summary>
    /// 获取新闻触发概率修正系数。
    /// 根据当前时间和新闻严重度返回概率乘数。
    /// </summary>
    public double GetNewsProbabilityModifier(int gameTime, string severity)
    {
        if (_config == null) return 1.0;
        
        // 获取基础时间段权重
        string period = GetCurrentTimePeriod(gameTime);
        double baseWeight = 1.0;
        if (_config.NewsTiming.TimePeriods.TryGetValue(period, out var periodConfig))
        {
            baseWeight = periodConfig.NewsWeight;
        }
        
        // 获取严重度修正
        double severityWeight = 1.0;
        if (_config.NewsTiming.SeverityTimeModifiers.TryGetValue(severity.ToLower(), out var severityMod))
        {
            severityWeight = period.ToLower() switch
            {
                "morning" => severityMod.MorningWeight,
                "afternoon" => severityMod.AfternoonWeight,
                "evening" => severityMod.EveningWeight,
                "night" => severityMod.NightWeight,
                _ => 1.0
            };
        }
        
        return baseWeight * severityWeight;
    }
    
    /// <summary>
    /// 检查当前时间是否是新闻检查时间点。
    /// </summary>
    public bool IsNewsCheckTime(int gameTime)
    {
        if (_config == null) return false;
        
        return _config.NewsTiming.CheckIntervals.RegularCheckTimes.Contains(gameTime);
    }
    
    /// <summary>
    /// 检查是否允许突发新闻（任意时间）。
    /// </summary>
    public bool AllowBreakingNewsAnyTime => _config?.NewsTiming.CheckIntervals.BreakingNewsAnyTime ?? true;
    
    /// <summary>
    /// 获取每天 tick 数。
    /// </summary>
    public int TicksPerDay => _config?.MarketSettings.TimeConfig.TicksPerDay ?? 100;
    
    /// <summary>
    /// 获取开盘时间。
    /// </summary>
    public int OpenTime => _config?.MarketSettings.TradingHours.OpenTime ?? 600;
    
    /// <summary>
    /// 获取收盘时间。
    /// </summary>
    public int CloseTime => _config?.MarketSettings.TradingHours.CloseTime ?? 2200;
    
    /// <summary>
    /// 获取涨跌停限制（百分比）。
    /// </summary>
    public double DailyLimitPercent => _config?.MarketSettings.PriceLimits.DailyLimitPercent ?? 10;
    
    /// <summary>
    /// 是否启用涨跌停。
    /// </summary>
    public bool PriceLimitsEnabled => _config?.MarketSettings.PriceLimits.Enabled ?? true;
    
    /// <summary>
    /// 获取市场冲击衰减率。
    /// </summary>
    public double ImpactDecayRate => _config?.MarketSettings.MarketImpact.ImpactDecayRate ?? 0.95;
    
    /// <summary>
    /// 获取每季天数。
    /// </summary>
    public int DaysPerSeason => _config?.Seasons.DaysPerSeason ?? 28;
    
    /// <summary>
    /// 获取默认地区。
    /// </summary>
    public string DefaultRegion => _config?.Regions.DefaultRegion ?? "pelican_town";
    
    /// <summary>
    /// 获取原始配置（供高级用法）。
    /// </summary>
    public MarketConfig? Config => _config;
    
    /// <summary>
    /// 获取配置元数据。
    /// </summary>
    public MarketConfigMetadata? Metadata => _config?.Metadata;
}
