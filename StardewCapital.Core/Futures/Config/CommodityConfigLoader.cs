// =====================================================================
// 文件：CommodityConfigLoader.cs
// 用途：从 JSON 文件加载商品配置，并转换为运行时使用的 Commodity。
// =====================================================================

using System.Text.Json;
using StardewCapital.Core.Futures.Models;

namespace StardewCapital.Core.Futures.Config;

/// <summary>
/// 运行时商品（从配置转换而来）。
/// 扩展了原有的 Commodity，包含更多参数。
/// </summary>
public class RuntimeCommodity
{
    // ═══ 基础标识 ═══
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string ItemId { get; set; } = "";
    public string Description { get; set; } = "";
    public string Category { get; set; } = "";
    
    // ═══ 当前地区的价格（运行时决定）═══
    public Region CurrentRegion { get; set; } = Region.PelicanTown;
    public double BasePrice { get; set; }
    public Availability Availability { get; set; } = Config.Availability.Common;
    
    // ═══ 地区价格表 ═══
    public Dictionary<Region, (double Price, Availability Availability)> RegionPrices { get; } = new();
    
    // ═══ 交易设置 ═══
    public ContractType ContractType { get; set; } = ContractType.Monthly;
    public int ContractSize { get; set; } = 100;
    public double TickSize { get; set; } = 0.01;
    public double InitialMarginRatio { get; set; } = 0.10;
    public double MaintenanceMarginRatio { get; set; } = 0.08;
    public double MinPrice { get; set; } = 1;
    public double MaxPrice { get; set; } = 10000;
    
    // ═══ 价格行为（核心）═══
    public double BaseVolatility { get; set; } = 0.02;
    public double IntradayVolatility { get; set; } = 0.01;
    public double VolatilityClustering { get; set; } = 0.6;
    public double MeanReversionSpeed { get; set; } = 0.15;
    public double MomentumFactor { get; set; } = 0.3;
    public double JumpProbability { get; set; } = 0.01;
    public double JumpMagnitude { get; set; } = 0.03;
    
    // ═══ 季节性 ═══
    public List<Season> GrowSeasons { get; set; } = new();
    public double OffSeasonMultiplier { get; set; } = 2.5;
    public Dictionary<Season, double> SeasonVolatility { get; } = new();
    
    // ═══ 供需 ═══
    public double BaseDemand { get; set; } = 100000;
    public double BaseSupply { get; set; } = 100000;
    public double StorageCostPerDay { get; set; } = 0.005;
    public double DemandElasticity { get; set; } = 1.0;
    
    // ═══ 市场结构 ═══
    public double BaseLiquidity { get; set; } = 100000;
    public double ImpactCoefficient { get; set; } = 0.005;
    public double BaseSpreadRatio { get; set; } = 0.01;
    public double TurnoverRate { get; set; } = 0.05;
    
    // ═══ 事件敏感度 ═══
    public double WeatherSensitivity { get; set; } = 0.5;
    public double PestSensitivity { get; set; } = 0.5;
    public double FestivalSensitivity { get; set; } = 0.3;
    public double NpcDemandSensitivity { get; set; } = 0.4;
    
    // ═══ 市场关联 ═══
    public List<(string CommodityId, CorrelationType Type, double Strength)> Correlations { get; } = new();
    
    /// <summary>
    /// 切换到指定地区，更新当前价格。
    /// </summary>
    public void SwitchRegion(Region region)
    {
        CurrentRegion = region;
        if (RegionPrices.TryGetValue(region, out var priceInfo))
        {
            BasePrice = priceInfo.Price;
            Availability = priceInfo.Availability;
        }
    }
    
    /// <summary>
    /// 获取指定地区的基础价格。
    /// </summary>
    public double GetRegionPrice(Region region)
    {
        return RegionPrices.TryGetValue(region, out var info) ? info.Price : BasePrice;
    }
    
    /// <summary>
    /// 检查是否在当前季节可种植。
    /// </summary>
    public bool IsInSeason(Season season)
    {
        return GrowSeasons.Contains(season);
    }
    
    /// <summary>
    /// 获取季节乘数。
    /// </summary>
    public double GetSeasonalMultiplier(Season season)
    {
        return IsInSeason(season) ? 1.0 : OffSeasonMultiplier;
    }
    
    /// <summary>
    /// 获取季节波动率修正。
    /// </summary>
    public double GetSeasonVolatility(Season season)
    {
        return SeasonVolatility.TryGetValue(season, out var vol) ? vol : 1.0;
    }
    
    /// <summary>
    /// 转换为基础 Commodity 对象（兼容旧代码）。
    /// </summary>
    public Commodity ToCommodity()
    {
        return new Commodity
        {
            Symbol = Id,
            Name = Name,
            BasePrice = BasePrice,
            BaseDemand = BaseDemand,
            BaseSupply = BaseSupply,
            GrowSeasons = GrowSeasons.ToArray(),
            OffSeasonMultiplier = OffSeasonMultiplier
        };
    }
}

/// <summary>
/// 商品配置加载器。
/// </summary>
public class CommodityConfigLoader
{
    private CommoditiesConfig? _config;
    private readonly Dictionary<string, RuntimeCommodity> _commodities = new();
    private readonly Dictionary<string, RegionInfo> _regions = new();
    private Region _currentRegion = Region.PelicanTown;
    
    /// <summary>
    /// 当前地区。
    /// </summary>
    public Region CurrentRegion
    {
        get => _currentRegion;
        set
        {
            _currentRegion = value;
            foreach (var commodity in _commodities.Values)
            {
                commodity.SwitchRegion(value);
            }
        }
    }
    
    /// <summary>
    /// 从 JSON 文件加载商品配置。
    /// </summary>
    public void LoadFromFile(string filePath)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"商品配置文件未找到: {filePath}");
        }
        
        string json = File.ReadAllText(filePath);
        LoadFromJson(json);
    }
    
    /// <summary>
    /// 从 JSON 字符串加载商品配置。
    /// </summary>
    public void LoadFromJson(string json)
    {
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip
        };
        
        _config = JsonSerializer.Deserialize<CommoditiesConfig>(json, options)
            ?? throw new InvalidOperationException("无法解析商品配置");
        
        // 加载地区
        _regions.Clear();
        foreach (var (key, region) in _config.Regions)
        {
            _regions[key] = region;
            if (region.IsDefault)
            {
                _currentRegion = ParseRegion(key);
            }
        }
        
        // 转换为运行时商品
        _commodities.Clear();
        foreach (var item in _config.Commodities)
        {
            var runtime = ConvertToRuntime(item);
            _commodities[runtime.Id] = runtime;
        }
    }
    
    /// <summary>
    /// 将配置项转换为运行时商品。
    /// </summary>
    private RuntimeCommodity ConvertToRuntime(CommodityItemConfig config)
    {
        var commodity = new RuntimeCommodity
        {
            Id = config.Id,
            Name = config.Name,
            ItemId = config.ItemId,
            Description = config.Description,
            Category = config.Category,
            
            // 交易设置
            ContractType = ParseContractType(config.TradeSettings.ContractType),
            ContractSize = config.TradeSettings.ContractSize,
            TickSize = config.TradeSettings.TickSize,
            InitialMarginRatio = config.TradeSettings.InitialMarginRatio,
            MaintenanceMarginRatio = config.TradeSettings.MaintenanceMarginRatio,
            MinPrice = config.TradeSettings.PriceLimits.Min,
            MaxPrice = config.TradeSettings.PriceLimits.Max,
            
            // 价格行为
            BaseVolatility = config.PriceBehavior.BaseVolatility,
            IntradayVolatility = config.PriceBehavior.IntradayVolatility,
            VolatilityClustering = config.PriceBehavior.VolatilityClustering,
            MeanReversionSpeed = config.PriceBehavior.MeanReversionSpeed,
            MomentumFactor = config.PriceBehavior.MomentumFactor,
            JumpProbability = config.PriceBehavior.JumpProbability,
            JumpMagnitude = config.PriceBehavior.JumpMagnitude,
            
            // 季节性
            OffSeasonMultiplier = config.Seasonality.OffSeasonMultiplier,
            
            // 供需
            BaseDemand = config.SupplyDemand.BaseDemand,
            BaseSupply = config.SupplyDemand.BaseSupply,
            StorageCostPerDay = config.SupplyDemand.StorageCostPerDay,
            DemandElasticity = config.SupplyDemand.DemandElasticity,
            
            // 市场结构
            BaseLiquidity = config.MarketStructure.BaseLiquidity,
            ImpactCoefficient = config.MarketStructure.ImpactCoefficient,
            BaseSpreadRatio = config.MarketStructure.BaseSpreadRatio,
            TurnoverRate = config.MarketStructure.TurnoverRate,
            
            // 事件敏感度
            WeatherSensitivity = config.EventSensitivity.Weather,
            PestSensitivity = config.EventSensitivity.Pest,
            FestivalSensitivity = config.EventSensitivity.Festival,
            NpcDemandSensitivity = config.EventSensitivity.NpcDemand
        };
        
        // 解析地区价格
        foreach (var (regionKey, priceConfig) in config.RegionPrices)
        {
            var region = ParseRegion(regionKey);
            var availability = ParseAvailability(priceConfig.Availability);
            commodity.RegionPrices[region] = (priceConfig.BasePrice, availability);
        }
        
        // 设置当前地区价格
        commodity.SwitchRegion(_currentRegion);
        
        // 解析生长季节
        foreach (var seasonStr in config.Seasonality.GrowSeasons)
        {
            if (Enum.TryParse<Season>(seasonStr, true, out var season))
            {
                commodity.GrowSeasons.Add(season);
            }
        }
        
        // 解析季节波动率
        foreach (var (seasonStr, vol) in config.Seasonality.SeasonVolatility)
        {
            if (Enum.TryParse<Season>(seasonStr, true, out var season))
            {
                commodity.SeasonVolatility[season] = vol;
            }
        }
        
        // 解析市场关联
        foreach (var corr in config.Correlations)
        {
            var corrType = ParseCorrelationType(corr.Type);
            commodity.Correlations.Add((corr.CommodityId, corrType, corr.Strength));
        }
        
        return commodity;
    }
    
    /// <summary>
    /// 解析合约类型。
    /// </summary>
    private ContractType ParseContractType(string type)
    {
        return type.ToLower() switch
        {
            "monthly" => ContractType.Monthly,
            "yearly" => ContractType.Yearly,
            "perpetual" => ContractType.Perpetual,
            _ => ContractType.Monthly
        };
    }
    
    /// <summary>
    /// 解析地区。
    /// </summary>
    private Region ParseRegion(string region)
    {
        return region.ToLower().Replace("_", "") switch
        {
            "pelicantown" => Region.PelicanTown,
            "calicodesert" => Region.CalicoDesert,
            "gingerisland" => Region.GingerIsland,
            _ => Region.PelicanTown
        };
    }
    
    /// <summary>
    /// 解析可用性。
    /// </summary>
    private Availability ParseAvailability(string availability)
    {
        return availability.ToLower() switch
        {
            "none" => Availability.None,
            "rare" => Availability.Rare,
            "uncommon" => Availability.Uncommon,
            "common" => Availability.Common,
            "abundant" => Availability.Abundant,
            _ => Availability.Common
        };
    }

    /// <summary>
    /// 解析关联类型。
    /// </summary>
    private CorrelationType ParseCorrelationType(string type)
    {
        return type.ToLower() switch
        {
            "input" => CorrelationType.Input,
            "output" => CorrelationType.Output,
            "substitute" => CorrelationType.Substitute,
            _ => CorrelationType.Substitute
        };
    }
    
    /// <summary>
    /// 获取指定商品。
    /// </summary>
    public RuntimeCommodity? GetCommodity(string id)
    {
        return _commodities.TryGetValue(id, out var commodity) ? commodity : null;
    }
    
    /// <summary>
    /// 获取所有商品。
    /// </summary>
    public IReadOnlyDictionary<string, RuntimeCommodity> AllCommodities => _commodities;
    
    /// <summary>
    /// 获取指定类别的商品。
    /// </summary>
    public IEnumerable<RuntimeCommodity> GetByCategory(string category)
    {
        return _commodities.Values.Where(c => c.Category == category);
    }
    
    /// <summary>
    /// 获取指定季节可种植的商品。
    /// </summary>
    public IEnumerable<RuntimeCommodity> GetBySeason(Season season)
    {
        return _commodities.Values.Where(c => c.IsInSeason(season));
    }
    
    /// <summary>
    /// 获取商品的关联商品列表。
    /// </summary>
    public IEnumerable<(RuntimeCommodity Commodity, CorrelationType Type, double Strength)> 
        GetCorrelatedCommodities(string commodityId)
    {
        if (!_commodities.TryGetValue(commodityId, out var source))
            yield break;
        
        foreach (var (corrId, type, strength) in source.Correlations)
        {
            if (_commodities.TryGetValue(corrId, out var target))
            {
                yield return (target, type, strength);
            }
        }
    }
    
    /// <summary>
    /// 获取所有地区信息。
    /// </summary>
    public IReadOnlyDictionary<string, RegionInfo> Regions => _regions;
    
    /// <summary>
    /// 获取配置元数据。
    /// </summary>
    public CommodityMetadata? Metadata => _config?.Metadata;
}
