// =====================================================================
// 文件：NewsConfigLoader.cs
// 用途：从 JSON 文件加载新闻配置，并转换为运行时使用的 NewsEvent。
// =====================================================================

using System.Text.Json;
using StardewCapital.Core.Common;
using StardewCapital.Core.Futures.Models;

namespace StardewCapital.Core.Futures.Config;

/// <summary>
/// 运行时新闻事件（从配置转换而来）。
/// </summary>
public class RuntimeNewsEvent
{
    // 基础信息
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public string Content { get; set; } = "";
    public string Severity { get; set; } = "medium";
    public string NewsType { get; set; } = "demand";
    
    // 影响参数
    public double DemandDelta { get; set; }
    public double SupplyDelta { get; set; }
    public double PriceMultiplier { get; set; } = 1.0;
    public double VolatilityDelta { get; set; }
    public double ConfidenceDelta { get; set; }
    /// <summary>
    /// 是否为永久性影响。true = 消化完后持续存在，false = 临时性，过期后消失。
    /// </summary>
    public bool IsPermanent { get; set; } = false;
    
    // 范围
    public List<string> AffectedItems { get; set; } = new();
    public bool IsGlobal { get; set; }
    
    // 时间
    public Season? TriggerSeason { get; set; }
    public int TriggerDayMin { get; set; } = 1;
    public int TriggerDayMax { get; set; } = 28;
    /// <summary>
    /// 触发时间（星露谷格式：600-2600）。
    /// 如果为 null，则在当天开盘时触发。
    /// </summary>
    public int? TriggerTime { get; set; }
    public int DurationDays { get; set; } = 7;
    
    // 条件
    public double Probability { get; set; } = 1.0;
    public List<string> Prerequisites { get; set; } = new();
    public int RandomImpactMin { get; set; }
    public int RandomImpactMax { get; set; }
    
    // 后续
    public string? FollowUpNewsId { get; set; }
    public int FollowUpDelayDays { get; set; }
    public double FollowUpProbability { get; set; }
    
    // 运行时状态
    public int TriggerDay { get; set; }  // 实际触发的日期
    public int ActualTriggerTime { get; set; } = 600;  // 实际触发时间（星露谷格式）
    public bool IsTriggered { get; set; }
    public bool IsActive { get; set; }
}

/// <summary>
/// 新闻配置加载器。
/// </summary>
public class NewsConfigLoader
{
    private readonly IRandomProvider _random;
    private NewsConfig? _config;
    private Dictionary<string, RuntimeNewsEvent> _newsEvents = new();
    
    public NewsConfigLoader(IRandomProvider? random = null)
    {
        _random = random ?? new DefaultRandomProvider();
    }
    
    /// <summary>
    /// 从 JSON 文件加载新闻配置。
    /// </summary>
    public void LoadFromFile(string filePath)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"新闻配置文件未找到: {filePath}");
        }
        
        string json = File.ReadAllText(filePath);
        LoadFromJson(json);
    }
    
    /// <summary>
    /// 从 JSON 字符串加载新闻配置。
    /// </summary>
    public void LoadFromJson(string json)
    {
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip
        };
        
        _config = JsonSerializer.Deserialize<NewsConfig>(json, options)
            ?? throw new InvalidOperationException("无法解析新闻配置");
        
        // 转换为运行时事件
        _newsEvents.Clear();
        foreach (var item in _config.NewsItems)
        {
            var runtimeEvent = ConvertToRuntime(item);
            _newsEvents[runtimeEvent.Id] = runtimeEvent;
        }
    }
    
    /// <summary>
    /// 将配置项转换为运行时事件。
    /// </summary>
    private RuntimeNewsEvent ConvertToRuntime(NewsItemConfig config)
    {
        var evt = new RuntimeNewsEvent
        {
            Id = config.Id,
            Title = config.Title,
            Description = config.Description,
            Content = config.Content,
            Severity = config.Severity,
            NewsType = config.NewsType,
            
            DemandDelta = config.Impact.DemandDelta,
            SupplyDelta = config.Impact.SupplyDelta,
            PriceMultiplier = config.Impact.PriceMultiplier,
            VolatilityDelta = config.Impact.VolatilityDelta,
            ConfidenceDelta = config.Impact.ConfidenceDelta,
            IsPermanent = config.Impact.IsPermanent,
            
            IsGlobal = config.Scope.IsGlobal,
            
            TriggerDayMin = config.Timing.TriggerDayRange.Length > 0 ? config.Timing.TriggerDayRange[0] : 1,
            TriggerDayMax = config.Timing.TriggerDayRange.Length > 1 ? config.Timing.TriggerDayRange[1] : 28,
            DurationDays = config.Timing.DurationDays,
            TriggerTime = config.Timing.TriggerTime,
            
            Probability = config.Conditions.Probability,
            Prerequisites = config.Conditions.Prerequisites,
            RandomImpactMin = config.Conditions.RandomImpactRange.Length > 0 ? config.Conditions.RandomImpactRange[0] : 0,
            RandomImpactMax = config.Conditions.RandomImpactRange.Length > 1 ? config.Conditions.RandomImpactRange[1] : 0
        };
        
        // 解析季节
        evt.TriggerSeason = config.Timing.TriggerSeason?.ToLower() switch
        {
            "spring" => Season.Spring,
            "summer" => Season.Summer,
            "fall" => Season.Fall,
            "winter" => Season.Winter,
            _ => null  // Any
        };
        
        // 展开受影响的物品（包括类别）
        evt.AffectedItems = new List<string>(config.Scope.AffectedItems);
        if (_config != null)
        {
            foreach (var category in config.Scope.AffectedCategories)
            {
                if (_config.Categories.TryGetValue(category, out var items))
                {
                    evt.AffectedItems.AddRange(items);
                }
            }
        }
        
        // 后续事件
        if (config.FollowUp != null)
        {
            evt.FollowUpNewsId = config.FollowUp.NewsId;
            evt.FollowUpDelayDays = config.FollowUp.DelayDays;
            evt.FollowUpProbability = config.FollowUp.Probability;
        }
        
        return evt;
    }
    
    /// <summary>
    /// 获取某一天应该触发的新闻列表。
    /// </summary>
    public List<RuntimeNewsEvent> GetTriggeredNews(int day, Season season, HashSet<string> alreadyTriggered)
    {
        var triggered = new List<RuntimeNewsEvent>();
        
        foreach (var evt in _newsEvents.Values)
        {
            // 已经触发过的跳过
            if (alreadyTriggered.Contains(evt.Id))
                continue;
            
            // 检查季节
            if (evt.TriggerSeason.HasValue && evt.TriggerSeason.Value != season)
                continue;
            
            // 检查日期范围
            if (day < evt.TriggerDayMin || day > evt.TriggerDayMax)
                continue;
            
            // 检查前置条件
            if (evt.Prerequisites.Any(p => !alreadyTriggered.Contains(p)))
                continue;
            
            // 检查概率
            if (_random.NextDouble() > evt.Probability)
                continue;
            
            // 应用随机影响
            if (evt.RandomImpactMax > evt.RandomImpactMin)
            {
                int randomDelta = _random.Next(evt.RandomImpactMin, evt.RandomImpactMax + 1);
                evt.DemandDelta += randomDelta;
                evt.SupplyDelta += randomDelta;
            }
            
            evt.TriggerDay = day;
            
            // 生成随机触发时间（基于时段权重）
            // 如果配置指定了时间则使用配置值，否则随机生成
            if (evt.TriggerTime.HasValue && evt.TriggerTime.Value > 0)
            {
                evt.ActualTriggerTime = evt.TriggerTime.Value;
            }
            else
            {
                evt.ActualTriggerTime = GenerateWeightedTriggerTime(evt.Severity);
            }
            
            evt.IsTriggered = true;
            evt.IsActive = true;
            triggered.Add(evt);
        }
        
        return triggered;
    }
    
    /// <summary>
    /// 检查后续新闻是否应该触发。
    /// </summary>
    public List<RuntimeNewsEvent> CheckFollowUps(int day, HashSet<string> alreadyTriggered)
    {
        var followUps = new List<RuntimeNewsEvent>();
        
        foreach (var evt in _newsEvents.Values)
        {
            if (!evt.IsTriggered || string.IsNullOrEmpty(evt.FollowUpNewsId))
                continue;
            
            // 检查延迟天数
            if (day - evt.TriggerDay < evt.FollowUpDelayDays)
                continue;
            
            // 检查后续新闻是否已触发
            if (alreadyTriggered.Contains(evt.FollowUpNewsId))
                continue;
            
            // 检查概率
            if (_random.NextDouble() > evt.FollowUpProbability)
                continue;
            
            if (_newsEvents.TryGetValue(evt.FollowUpNewsId, out var followUp))
            {
                followUp.TriggerDay = day;
                followUp.IsTriggered = true;
                followUp.IsActive = true;
                followUps.Add(followUp);
            }
        }
        
        return followUps;
    }
    
    /// <summary>
    /// 更新新闻的活跃状态（根据持续时间）。
    /// </summary>
    public void UpdateActiveStatus(int currentDay)
    {
        foreach (var evt in _newsEvents.Values)
        {
            if (!evt.IsTriggered)
                continue;
            
            evt.IsActive = currentDay <= evt.TriggerDay + evt.DurationDays;
        }
    }
    
    /// <summary>
    /// 获取当前活跃的新闻列表。
    /// </summary>
    public List<RuntimeNewsEvent> GetActiveNews()
    {
        return _newsEvents.Values.Where(e => e.IsActive).ToList();
    }
    
    /// <summary>
    /// 获取所有新闻事件。
    /// </summary>
    public IReadOnlyDictionary<string, RuntimeNewsEvent> AllNews => _newsEvents;
    
    /// <summary>
    /// 获取配置元数据。
    /// </summary>
    public NewsMetadata? Metadata => _config?.Metadata;
    
    /// <summary>
    /// 重置所有新闻状态（新游戏/新季节）。
    /// </summary>
    public void Reset()
    {
        foreach (var evt in _newsEvents.Values)
        {
            evt.IsTriggered = false;
            evt.IsActive = false;
            evt.TriggerDay = 0;
            evt.ActualTriggerTime = 600;
        }
    }
    
    /// <summary>
    /// 根据时段权重生成随机触发时间。
    /// 时段权重：
    /// - 早间 (6:00-12:00): +50% 基础权重，critical新闻 x2.0
    /// - 午间 (12:00-18:00): +20% 基础权重，critical新闻 x1.5
    /// - 晚间 (18:00-22:00): -50% 基础权重，critical新闻 x0.5
    /// - 夜间 (22:00-6:00): -80% 基础权重，critical新闻 x0.1
    /// </summary>
    private int GenerateWeightedTriggerTime(string severity)
    {
        // 时段定义（星露谷时间格式）
        // 早间: 600-1200, 午间: 1200-1800, 晚间: 1800-2200, 夜间: 2200-2600 或 0-600
        
        // 时段基础权重
        double morningWeight = 1.5;    // 6:00-12:00, +50%
        double afternoonWeight = 1.2;  // 12:00-18:00, +20%
        double eveningWeight = 0.5;    // 18:00-22:00, -50%
        double nightWeight = 0.2;      // 22:00-6:00, -80%
        
        // 严重度修正（高严重度新闻更可能在早间发布）
        double severityMultiplier = severity?.ToLower() switch
        {
            "critical" => 2.0,
            "high" => 1.5,
            "medium" => 1.0,
            "low" => 0.8,
            _ => 1.0
        };
        
        // 严格的严重度修正（只影响早间权重）
        morningWeight *= severityMultiplier;
        
        // critical新闻的晚间/夜间权重降低
        if (severity?.ToLower() == "critical")
        {
            eveningWeight *= 0.5;  // x0.5
            nightWeight *= 0.1;    // x0.1
        }
        
        // 计算总权重
        double totalWeight = morningWeight + afternoonWeight + eveningWeight + nightWeight;
        
        // 按权重随机选择时段
        double roll = _random.NextDouble() * totalWeight;
        double cumulative = 0;
        
        // 早间: 6:00-12:00 (600-1200)
        cumulative += morningWeight;
        if (roll < cumulative)
        {
            int hour = 6 + _random.Next(0, 6);  // 6-11
            int minute = _random.Next(0, 6) * 10;  // 0, 10, 20, 30, 40, 50
            return hour * 100 + minute;
        }
        
        // 午间: 12:00-18:00 (1200-1800)
        cumulative += afternoonWeight;
        if (roll < cumulative)
        {
            int hour = 12 + _random.Next(0, 6);  // 12-17
            int minute = _random.Next(0, 6) * 10;
            return hour * 100 + minute;
        }
        
        // 晚间: 18:00-22:00 (1800-2200)
        cumulative += eveningWeight;
        if (roll < cumulative)
        {
            int hour = 18 + _random.Next(0, 4);  // 18-21
            int minute = _random.Next(0, 6) * 10;
            return hour * 100 + minute;
        }
        
        // 夜间: 22:00-6:00 (2200-2600 或 0-600，但统一用22-26表示)
        int nightHour = 22 + _random.Next(0, 8);  // 22-29 (29 = 5:00AM)
        if (nightHour >= 24) nightHour = nightHour - 24;  // 转换为 0-5
        int nightMinute = _random.Next(0, 6) * 10;
        return nightHour * 100 + nightMinute;
    }
}
