// =====================================================================
// 文件：MarketConfig.cs
// 用途：全局市场配置的数据模型，对应 market_config.json 的结构。
// =====================================================================

using System.Text.Json.Serialization;

namespace StardewCapital.Core.Futures.Config;

/// <summary>
/// 全局市场配置根对象。
/// </summary>
public class MarketConfig
{
    [JsonPropertyName("market_settings")]
    public MarketSettingsConfig MarketSettings { get; set; } = new();
    
    [JsonPropertyName("news_timing")]
    public GlobalNewsTimingConfig NewsTiming { get; set; } = new();
    
    [JsonPropertyName("seasons")]
    public SeasonsConfig Seasons { get; set; } = new();
    
    [JsonPropertyName("contract_settings")]
    public ContractSettingsConfig ContractSettings { get; set; } = new();
    
    [JsonPropertyName("regions")]
    public RegionsConfig Regions { get; set; } = new();
    
    [JsonPropertyName("simulation_settings")]
    public SimulationSettingsConfig SimulationSettings { get; set; } = new();
    
    [JsonPropertyName("metadata")]
    public MarketConfigMetadata Metadata { get; set; } = new();
}

/// <summary>
/// 市场设置。
/// </summary>
public class MarketSettingsConfig
{
    [JsonPropertyName("version")]
    public string Version { get; set; } = "1.0";
    
    [JsonPropertyName("trading_hours")]
    public TradingHoursConfig TradingHours { get; set; } = new();
    
    [JsonPropertyName("time_config")]
    public TimeConfig TimeConfig { get; set; } = new();
    
    [JsonPropertyName("price_limits")]
    public PriceLimitsGlobalConfig PriceLimits { get; set; } = new();
    
    [JsonPropertyName("market_impact")]
    public MarketImpactConfig MarketImpact { get; set; } = new();
}

/// <summary>
/// 交易时段配置。
/// </summary>
public class TradingHoursConfig
{
    [JsonPropertyName("open_time")]
    public int OpenTime { get; set; } = 600;
    
    [JsonPropertyName("close_time")]
    public int CloseTime { get; set; } = 2200;
}

/// <summary>
/// 时间配置。
/// </summary>
public class TimeConfig
{
    [JsonPropertyName("ticks_per_day")]
    public int TicksPerDay { get; set; } = 100;
    
    [JsonPropertyName("minutes_per_tick")]
    public int MinutesPerTick { get; set; } = 10;
}

/// <summary>
/// 价格限制配置。
/// </summary>
public class PriceLimitsGlobalConfig
{
    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; } = true;
    
    [JsonPropertyName("daily_limit_percent")]
    public double DailyLimitPercent { get; set; } = 10;
}

/// <summary>
/// 市场冲击配置。
/// </summary>
public class MarketImpactConfig
{
    [JsonPropertyName("impact_decay_rate")]
    public double ImpactDecayRate { get; set; } = 0.95;
    
    [JsonPropertyName("max_impact_percent")]
    public double MaxImpactPercent { get; set; } = 5;
}

/// <summary>
/// 全局新闻时机配置（与 NewsConfig 中的 NewsTimingConfig 区分）。
/// </summary>
public class GlobalNewsTimingConfig
{
    [JsonPropertyName("time_periods")]
    public Dictionary<string, TimePeriodConfig> TimePeriods { get; set; } = new();
    
    [JsonPropertyName("severity_time_modifiers")]
    public Dictionary<string, SeverityTimeModifier> SeverityTimeModifiers { get; set; } = new();
    
    [JsonPropertyName("check_intervals")]
    public NewsCheckIntervals CheckIntervals { get; set; } = new();
}

/// <summary>
/// 时间段配置。
/// </summary>
public class TimePeriodConfig
{
    [JsonPropertyName("start_time")]
    public int StartTime { get; set; }
    
    [JsonPropertyName("end_time")]
    public int EndTime { get; set; }
    
    [JsonPropertyName("news_weight")]
    public double NewsWeight { get; set; } = 1.0;
}

/// <summary>
/// 严重度时间修正系数。
/// </summary>
public class SeverityTimeModifier
{
    [JsonPropertyName("morning_weight")]
    public double MorningWeight { get; set; } = 1.0;
    
    [JsonPropertyName("afternoon_weight")]
    public double AfternoonWeight { get; set; } = 1.0;
    
    [JsonPropertyName("evening_weight")]
    public double EveningWeight { get; set; } = 1.0;
    
    [JsonPropertyName("night_weight")]
    public double NightWeight { get; set; } = 1.0;
}

/// <summary>
/// 新闻检查间隔。
/// </summary>
public class NewsCheckIntervals
{
    [JsonPropertyName("regular_check_times")]
    public int[] RegularCheckTimes { get; set; } = new[] { 700, 900, 1200, 1500, 1800 };
    
    [JsonPropertyName("breaking_news_any_time")]
    public bool BreakingNewsAnyTime { get; set; } = true;
}

/// <summary>
/// 季节配置。
/// </summary>
public class SeasonsConfig
{
    [JsonPropertyName("days_per_season")]
    public int DaysPerSeason { get; set; } = 28;
    
    [JsonPropertyName("seasons_per_year")]
    public int SeasonsPerYear { get; set; } = 4;
    
    [JsonPropertyName("season_names")]
    public string[] SeasonNames { get; set; } = new[] { "Spring", "Summer", "Fall", "Winter" };
    
    [JsonPropertyName("season_names_cn")]
    public string[] SeasonNamesCn { get; set; } = new[] { "春季", "夏季", "秋季", "冬季" };
}

/// <summary>
/// 合约设置配置。
/// </summary>
public class ContractSettingsConfig
{
    [JsonPropertyName("default_expiration_day")]
    public int DefaultExpirationDay { get; set; } = 28;
    
    [JsonPropertyName("contract_types")]
    public Dictionary<string, ContractTypeConfig> ContractTypes { get; set; } = new();
}

/// <summary>
/// 合约类型配置。
/// </summary>
public class ContractTypeConfig
{
    [JsonPropertyName("duration_days")]
    public int DurationDays { get; set; }
    
    [JsonPropertyName("description")]
    public string Description { get; set; } = "";
}

/// <summary>
/// 地区配置。
/// </summary>
public class RegionsConfig
{
    [JsonPropertyName("default_region")]
    public string DefaultRegion { get; set; } = "pelican_town";
    
    [JsonPropertyName("available_regions")]
    public List<RegionConfigItem> AvailableRegions { get; set; } = new();
}

/// <summary>
/// 地区配置项。
/// </summary>
public class RegionConfigItem
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";
    
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";
    
    [JsonPropertyName("unlock_condition")]
    public string UnlockCondition { get; set; } = "none";
    
    [JsonPropertyName("description")]
    public string Description { get; set; } = "";
}

/// <summary>
/// 模拟设置配置。
/// </summary>
public class SimulationSettingsConfig
{
    [JsonPropertyName("random_seed")]
    public int? RandomSeed { get; set; }
    
    [JsonPropertyName("enable_logging")]
    public bool EnableLogging { get; set; }
    
    [JsonPropertyName("log_level")]
    public string LogLevel { get; set; } = "info";
}

/// <summary>
/// 市场配置元数据。
/// </summary>
public class MarketConfigMetadata
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
