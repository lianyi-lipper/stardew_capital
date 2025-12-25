// =====================================================================
// 文件：CommodityConfig.cs
// 用途：商品配置的数据模型，对应 commodities_config.json 的结构。
// =====================================================================

using System.Text.Json.Serialization;

namespace StardewCapital.Core.Futures.Config;

/// <summary>
/// 合约类型枚举。
/// </summary>
public enum ContractType
{
    Monthly,    // 每月（季度）交割
    Yearly,     // 每年交割
    Perpetual   // 永续合约
}

/// <summary>
/// 地区枚举。
/// </summary>
public enum Region
{
    PelicanTown,    // 鹈鹕镇（默认）
    CalicoDesert,   // 卡利科沙漠
    GingerIsland    // 姜岛
}

/// <summary>
/// 可用性枚举。
/// </summary>
public enum Availability
{
    None,       // 不可用
    Rare,       // 稀有
    Uncommon,   // 不常见
    Common,     // 常见
    Abundant    // 丰富
}

/// <summary>
/// 关联类型枚举。
/// </summary>
public enum CorrelationType
{
    Input,      // 原料 → 产品（蓝莓 → 蓝莓酒）
    Output,     // 产品 ← 原料（反向）
    Substitute  // 替代品（土豆 ↔ 防风草）
}

/// <summary>
/// 商品配置根对象。
/// </summary>
public class CommoditiesConfig
{
    [JsonPropertyName("commodities")]
    public List<CommodityItemConfig> Commodities { get; set; } = new();
    
    [JsonPropertyName("regions")]
    public Dictionary<string, RegionInfo> Regions { get; set; } = new();
    
    [JsonPropertyName("categories")]
    public Dictionary<string, List<string>> Categories { get; set; } = new();
    
    [JsonPropertyName("correlation_types")]
    public Dictionary<string, CorrelationTypeInfo> CorrelationTypes { get; set; } = new();
    
    [JsonPropertyName("metadata")]
    public CommodityMetadata Metadata { get; set; } = new();
}

/// <summary>
/// 单个商品的配置。
/// </summary>
public class CommodityItemConfig
{
    // ═══ 基础标识 ═══
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";
    
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";
    
    [JsonPropertyName("item_id")]
    public string ItemId { get; set; } = "";
    
    [JsonPropertyName("description")]
    public string Description { get; set; } = "";
    
    [JsonPropertyName("category")]
    public string Category { get; set; } = "";
    
    [JsonPropertyName("created_at")]
    public string CreatedAt { get; set; } = "";
    
    // ═══ 地区价格 ═══
    [JsonPropertyName("region_prices")]
    public Dictionary<string, RegionPriceConfig> RegionPrices { get; set; } = new();
    
    // ═══ 交易属性 ═══
    [JsonPropertyName("trade_settings")]
    public TradeSettingsConfig TradeSettings { get; set; } = new();
    
    // ═══ 价格行为 ═══
    [JsonPropertyName("price_behavior")]
    public PriceBehaviorConfig PriceBehavior { get; set; } = new();
    
    // ═══ 季节性 ═══
    [JsonPropertyName("seasonality")]
    public SeasonalityConfig Seasonality { get; set; } = new();
    
    // ═══ 供需 ═══
    [JsonPropertyName("supply_demand")]
    public SupplyDemandConfig SupplyDemand { get; set; } = new();
    
    // ═══ 市场结构 ═══
    [JsonPropertyName("market_structure")]
    public MarketStructureConfig MarketStructure { get; set; } = new();
    
    // ═══ 事件敏感度 ═══
    [JsonPropertyName("event_sensitivity")]
    public EventSensitivityConfig EventSensitivity { get; set; } = new();
    
    // ═══ 市场关联 ═══
    [JsonPropertyName("correlations")]
    public List<CorrelationConfig> Correlations { get; set; } = new();
}

/// <summary>
/// 地区价格配置。
/// </summary>
public class RegionPriceConfig
{
    [JsonPropertyName("base_price")]
    public double BasePrice { get; set; }
    
    [JsonPropertyName("availability")]
    public string Availability { get; set; } = "common";
    
    [JsonPropertyName("description")]
    public string Description { get; set; } = "";
}

/// <summary>
/// 交易设置配置。
/// </summary>
public class TradeSettingsConfig
{
    [JsonPropertyName("contract_type")]
    public string ContractType { get; set; } = "monthly";
    
    [JsonPropertyName("contract_size")]
    public int ContractSize { get; set; } = 100;
    
    [JsonPropertyName("tick_size")]
    public double TickSize { get; set; } = 0.01;
    
    [JsonPropertyName("initial_margin_ratio")]
    public double InitialMarginRatio { get; set; } = 0.10;
    
    [JsonPropertyName("maintenance_margin_ratio")]
    public double MaintenanceMarginRatio { get; set; } = 0.08;
    
    [JsonPropertyName("price_limits")]
    public PriceLimitsConfig PriceLimits { get; set; } = new();
}

/// <summary>
/// 价格限制配置。
/// </summary>
public class PriceLimitsConfig
{
    [JsonPropertyName("min")]
    public double Min { get; set; } = 1;
    
    [JsonPropertyName("max")]
    public double Max { get; set; } = 10000;
}

/// <summary>
/// 价格行为配置（核心！）。
/// </summary>
public class PriceBehaviorConfig
{
    [JsonPropertyName("base_volatility")]
    public double BaseVolatility { get; set; } = 0.02;
    
    [JsonPropertyName("intraday_volatility")]
    public double IntradayVolatility { get; set; } = 0.01;
    
    [JsonPropertyName("volatility_clustering")]
    public double VolatilityClustering { get; set; } = 0.6;
    
    [JsonPropertyName("mean_reversion_speed")]
    public double MeanReversionSpeed { get; set; } = 0.15;
    
    [JsonPropertyName("momentum_factor")]
    public double MomentumFactor { get; set; } = 0.3;
    
    [JsonPropertyName("jump_probability")]
    public double JumpProbability { get; set; } = 0.01;
    
    [JsonPropertyName("jump_magnitude")]
    public double JumpMagnitude { get; set; } = 0.03;
}

/// <summary>
/// 季节性配置。
/// </summary>
public class SeasonalityConfig
{
    [JsonPropertyName("grow_seasons")]
    public List<string> GrowSeasons { get; set; } = new();
    
    [JsonPropertyName("off_season_multiplier")]
    public double OffSeasonMultiplier { get; set; } = 2.5;
    
    [JsonPropertyName("season_volatility")]
    public Dictionary<string, double> SeasonVolatility { get; set; } = new();
}

/// <summary>
/// 供需配置。
/// </summary>
public class SupplyDemandConfig
{
    [JsonPropertyName("base_demand")]
    public double BaseDemand { get; set; } = 100000;
    
    [JsonPropertyName("base_supply")]
    public double BaseSupply { get; set; } = 100000;
    
    [JsonPropertyName("storage_cost_per_day")]
    public double StorageCostPerDay { get; set; } = 0.005;
    
    [JsonPropertyName("demand_elasticity")]
    public double DemandElasticity { get; set; } = 1.0;
}

/// <summary>
/// 市场结构配置。
/// </summary>
public class MarketStructureConfig
{
    [JsonPropertyName("base_liquidity")]
    public double BaseLiquidity { get; set; } = 100000;
    
    [JsonPropertyName("impact_coefficient")]
    public double ImpactCoefficient { get; set; } = 0.005;
    
    [JsonPropertyName("base_spread_ratio")]
    public double BaseSpreadRatio { get; set; } = 0.01;
    
    [JsonPropertyName("turnover_rate")]
    public double TurnoverRate { get; set; } = 0.05;
}

/// <summary>
/// 事件敏感度配置。
/// </summary>
public class EventSensitivityConfig
{
    [JsonPropertyName("weather")]
    public double Weather { get; set; } = 0.5;
    
    [JsonPropertyName("pest")]
    public double Pest { get; set; } = 0.5;
    
    [JsonPropertyName("festival")]
    public double Festival { get; set; } = 0.3;
    
    [JsonPropertyName("npc_demand")]
    public double NpcDemand { get; set; } = 0.4;
}

/// <summary>
/// 市场关联配置。
/// </summary>
public class CorrelationConfig
{
    [JsonPropertyName("commodity_id")]
    public string CommodityId { get; set; } = "";
    
    [JsonPropertyName("type")]
    public string Type { get; set; } = "input";
    
    [JsonPropertyName("strength")]
    public double Strength { get; set; } = 0.5;
}

/// <summary>
/// 地区信息。
/// </summary>
public class RegionInfo
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";
    
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";
    
    [JsonPropertyName("is_default")]
    public bool IsDefault { get; set; }
    
    [JsonPropertyName("description")]
    public string Description { get; set; } = "";
}

/// <summary>
/// 关联类型信息。
/// </summary>
public class CorrelationTypeInfo
{
    [JsonPropertyName("description")]
    public string Description { get; set; } = "";
    
    [JsonPropertyName("propagation_delay_ticks")]
    public int PropagationDelayTicks { get; set; } = 5;
    
    [JsonPropertyName("decay_factor")]
    public double DecayFactor { get; set; } = 0.9;
}

/// <summary>
/// 商品配置元数据。
/// </summary>
public class CommodityMetadata
{
    [JsonPropertyName("schema_version")]
    public string SchemaVersion { get; set; } = "1.0";
    
    [JsonPropertyName("game_version")]
    public string GameVersion { get; set; } = "1.5";
    
    [JsonPropertyName("last_updated")]
    public string LastUpdated { get; set; } = "";
    
    [JsonPropertyName("author")]
    public string Author { get; set; } = "";
    
    [JsonPropertyName("notes")]
    public string Notes { get; set; } = "";
}
