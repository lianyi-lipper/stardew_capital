// =====================================================================
// 文件：NewsConfig.cs
// 用途：新闻配置的数据模型，对应 news_config.json 的结构。
// =====================================================================

using System.Text.Json.Serialization;

namespace StardewCapital.Core.Futures.Config;

/// <summary>
/// 新闻配置根对象。
/// </summary>
public class NewsConfig
{
    [JsonPropertyName("news_items")]
    public List<NewsItemConfig> NewsItems { get; set; } = new();
    
    [JsonPropertyName("categories")]
    public Dictionary<string, List<string>> Categories { get; set; } = new();
    
    [JsonPropertyName("metadata")]
    public NewsMetadata Metadata { get; set; } = new();
}

/// <summary>
/// 单条新闻的配置。
/// </summary>
public class NewsItemConfig
{
    // ═══ A. 标识参数 ═══
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";
    
    [JsonPropertyName("created_at")]
    public string CreatedAt { get; set; } = "";
    
    [JsonPropertyName("title")]
    public string Title { get; set; } = "";
    
    [JsonPropertyName("description")]
    public string Description { get; set; } = "";
    
    [JsonPropertyName("content")]
    public string Content { get; set; } = "";
    
    [JsonPropertyName("severity")]
    public string Severity { get; set; } = "medium";  // low | medium | high | critical
    
    [JsonPropertyName("news_type")]
    public string NewsType { get; set; } = "demand";  // demand | supply | mixed | sentiment
    
    // ═══ B. 数值影响 ═══
    [JsonPropertyName("impact")]
    public NewsImpactConfig Impact { get; set; } = new();
    
    // ═══ C. 作用范围 ═══
    [JsonPropertyName("scope")]
    public NewsScopeConfig Scope { get; set; } = new();
    
    // ═══ D. 时间控制 ═══
    [JsonPropertyName("timing")]
    public NewsTimingConfig Timing { get; set; } = new();
    
    // ═══ E. 触发条件 ═══
    [JsonPropertyName("conditions")]
    public NewsConditionsConfig Conditions { get; set; } = new();
    
    // ═══ F. 后续事件 ═══
    [JsonPropertyName("follow_up")]
    public NewsFollowUpConfig? FollowUp { get; set; }
}

/// <summary>
/// 新闻影响参数。
/// </summary>
public class NewsImpactConfig
{
    [JsonPropertyName("demand_delta")]
    public double DemandDelta { get; set; }
    
    [JsonPropertyName("supply_delta")]
    public double SupplyDelta { get; set; }
    
    [JsonPropertyName("price_multiplier")]
    public double PriceMultiplier { get; set; } = 1.0;
    
    [JsonPropertyName("volatility_delta")]
    public double VolatilityDelta { get; set; }
    
    [JsonPropertyName("confidence_delta")]
    public double ConfidenceDelta { get; set; }
    
    /// <summary>
    /// 是否为永久性影响（默认 false = 临时性）。
    /// 永久性：消化完后影响持续存在（如开新店、害虫毁庄稼）
    /// 临时性：消化完后影响逐渐消失（如节日促销）
    /// </summary>
    [JsonPropertyName("is_permanent")]
    public bool IsPermanent { get; set; } = false;
}

/// <summary>
/// 新闻作用范围。
/// </summary>
public class NewsScopeConfig
{
    [JsonPropertyName("affected_items")]
    public List<string> AffectedItems { get; set; } = new();
    
    [JsonPropertyName("affected_categories")]
    public List<string> AffectedCategories { get; set; } = new();
    
    [JsonPropertyName("is_global")]
    public bool IsGlobal { get; set; }
}

/// <summary>
/// 新闻时间控制。
/// </summary>
public class NewsTimingConfig
{
    [JsonPropertyName("trigger_season")]
    public string TriggerSeason { get; set; } = "Any";  // Spring | Summer | Fall | Winter | Any
    
    [JsonPropertyName("trigger_day_range")]
    public int[] TriggerDayRange { get; set; } = new[] { 1, 28 };
    
    /// <summary>
    /// 触发时间（星露谷时间格式：600-2600，如 900 = 9:00AM）。
    /// 如果为 null 或 0，则表示当天开盘时触发。
    /// </summary>
    [JsonPropertyName("trigger_time")]
    public int? TriggerTime { get; set; }
    
    [JsonPropertyName("duration_days")]
    public int DurationDays { get; set; } = 7;
}

/// <summary>
/// 新闻触发条件。
/// </summary>
public class NewsConditionsConfig
{
    [JsonPropertyName("probability")]
    public double Probability { get; set; } = 1.0;
    
    [JsonPropertyName("prerequisites")]
    public List<string> Prerequisites { get; set; } = new();
    
    [JsonPropertyName("random_impact_range")]
    public int[] RandomImpactRange { get; set; } = new[] { 0, 0 };
}

/// <summary>
/// 后续新闻配置。
/// </summary>
public class NewsFollowUpConfig
{
    [JsonPropertyName("news_id")]
    public string NewsId { get; set; } = "";
    
    [JsonPropertyName("delay_days")]
    public int DelayDays { get; set; }
    
    [JsonPropertyName("probability")]
    public double Probability { get; set; } = 1.0;
}

/// <summary>
/// 新闻配置元数据。
/// </summary>
public class NewsMetadata
{
    [JsonPropertyName("schema_version")]
    public string SchemaVersion { get; set; } = "1.0";
    
    [JsonPropertyName("game_version")]
    public string GameVersion { get; set; } = "1.5";
    
    [JsonPropertyName("last_updated")]
    public string LastUpdated { get; set; } = "";
    
    [JsonPropertyName("author")]
    public string Author { get; set; } = "";
}
