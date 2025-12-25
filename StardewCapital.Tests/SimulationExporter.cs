// =====================================================================
// 文件：SimulationExporter.cs
// 用途：将模拟结果导出为 JSON 文件。
//       格式参考废弃项目的 full_season_output.json。
// =====================================================================

using System.Text.Json;
using System.Text.Json.Serialization;
using StardewCapital.Core.Common;
using StardewCapital.Core.Futures;
using StardewCapital.Core.Futures.Math;
using StardewCapital.Core.Futures.Models;
using StardewCapital.Core.Futures.Pricing;
using StardewCapital.Core.Time;

namespace StardewCapital.Tests;

/// <summary>
/// 日内价格数据点。
/// </summary>
public class IntradayPricePoint
{
    [JsonPropertyName("step")]
    public int Step { get; set; }
    
    [JsonPropertyName("time")]
    public string Time { get; set; } = "";
    
    /// <summary>
    /// 目标价格（基本面价值）。
    /// </summary>
    [JsonPropertyName("targetPrice")]
    public double TargetPrice { get; set; }
    
    /// <summary>
    /// 影子价格（模拟市场价格，未含交易冲击）。
    /// 未来 marketPrice = shadowPrice + 玩家/NPC交易冲击
    /// </summary>
    [JsonPropertyName("shadowPrice")]
    public double ShadowPrice { get; set; }
    
    [JsonPropertyName("marketImpact")]
    public double MarketImpact { get; set; }
}

/// <summary>
/// 日交易数据。
/// </summary>
public class DailyData
{
    [JsonPropertyName("day")]
    public int Day { get; set; }
    
    [JsonPropertyName("openPrice")]
    public double OpenPrice { get; set; }
    
    [JsonPropertyName("closePrice")]
    public double ClosePrice { get; set; }
    
    [JsonPropertyName("highPrice")]
    public double HighPrice { get; set; }
    
    [JsonPropertyName("lowPrice")]
    public double LowPrice { get; set; }
    
    [JsonPropertyName("fundamentalValue")]
    public double FundamentalValue { get; set; }
    
    [JsonPropertyName("intradayPrices")]
    public List<IntradayPricePoint> IntradayPrices { get; set; } = new();
}

/// <summary>
/// 商品模拟结果。
/// </summary>
public class CommodityResult
{
    [JsonPropertyName("commodityName")]
    public string CommodityName { get; set; } = "";
    
    [JsonPropertyName("basePrice")]
    public double BasePrice { get; set; }
    
    [JsonPropertyName("stepsPerDay")]
    public int StepsPerDay { get; set; }
    
    [JsonPropertyName("dailyData")]
    public List<DailyData> DailyData { get; set; } = new();
}

/// <summary>
/// 完整季节模拟输出。
/// </summary>
public class SeasonSimulationOutput
{
    [JsonPropertyName("season")]
    public string Season { get; set; } = "";
    
    [JsonPropertyName("totalDays")]
    public int TotalDays { get; set; }
    
    [JsonPropertyName("generatedAt")]
    public string GeneratedAt { get; set; } = "";
    
    [JsonPropertyName("parameters")]
    public ParametersInfo Parameters { get; set; } = new();
    
    [JsonPropertyName("commodityResults")]
    public Dictionary<string, CommodityResult> CommodityResults { get; set; } = new();
    
    [JsonPropertyName("newsEvents")]
    public List<NewsEventOutput> NewsEvents { get; set; } = new();
}

/// <summary>
/// 新闻事件输出。
/// </summary>
public class NewsEventOutput
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";
    
    [JsonPropertyName("title")]
    public string Title { get; set; } = "";
    
    [JsonPropertyName("description")]
    public string Description { get; set; } = "";
    
    [JsonPropertyName("severity")]
    public string Severity { get; set; } = "medium";
    
    [JsonPropertyName("newsType")]
    public string NewsType { get; set; } = "demand";
    
    [JsonPropertyName("triggerDay")]
    public int TriggerDay { get; set; }
    
    /// <summary>
    /// 触发时间（HH:MM 格式，如 "09:00"）。
    /// </summary>
    [JsonPropertyName("triggerTime")]
    public string TriggerTime { get; set; } = "06:00";
    
    [JsonPropertyName("durationDays")]
    public int DurationDays { get; set; }
    
    [JsonPropertyName("impact")]
    public NewsImpactOutput Impact { get; set; } = new();
    
    [JsonPropertyName("affectedItems")]
    public List<string> AffectedItems { get; set; } = new();
    
    [JsonPropertyName("isGlobal")]
    public bool IsGlobal { get; set; }
}

/// <summary>
/// 新闻影响输出。
/// </summary>
public class NewsImpactOutput
{
    [JsonPropertyName("demandDelta")]
    public double DemandDelta { get; set; }
    
    [JsonPropertyName("supplyDelta")]
    public double SupplyDelta { get; set; }
    
    [JsonPropertyName("priceMultiplier")]
    public double PriceMultiplier { get; set; } = 1.0;
    
    [JsonPropertyName("volatilityDelta")]
    public double VolatilityDelta { get; set; }
}

/// <summary>
/// 参数信息。
/// </summary>
public class ParametersInfo
{
    [JsonPropertyName("riskFreeRate")]
    public double RiskFreeRate { get; set; }
    
    [JsonPropertyName("storageCost")]
    public double StorageCost { get; set; }
    
    [JsonPropertyName("convenienceYield")]
    public double ConvenienceYield { get; set; }
    
    [JsonPropertyName("baseVolatility")]
    public double BaseVolatility { get; set; }
    
    [JsonPropertyName("intradayVolatility")]
    public double IntradayVolatility { get; set; }
}

/// <summary>
/// 模拟结果导出器。
/// </summary>
public class SimulationExporter
{
    private readonly int _ticksPerDay;
    private readonly int _daysPerSeason;
    
    public SimulationExporter(int ticksPerDay = 50, int daysPerSeason = 28)
    {
        _ticksPerDay = ticksPerDay;
        _daysPerSeason = daysPerSeason;
    }
    
    /// <summary>
    /// 运行完整季节模拟并导出 JSON。
    /// </summary>
    public SeasonSimulationOutput RunFullSeasonSimulation(
        Commodity[] commodities,
        Season season,
        int? seed = null,
        string? newsConfigPath = null)
    {
        var random = seed.HasValue 
            ? new DefaultRandomProvider(seed.Value) 
            : new DefaultRandomProvider();
        
        var clock = new SimulationClock();
        clock.SetTime(day: 1, timeOfDay: 600, season: (int)season);
        
        var output = new SeasonSimulationOutput
        {
            Season = season.ToString(),
            TotalDays = _daysPerSeason,
            GeneratedAt = DateTime.Now.ToString("O"),
            Parameters = new ParametersInfo
            {
                RiskFreeRate = FuturesParameters.Default.RiskFreeRate,
                StorageCost = FuturesParameters.Default.StorageCost,
                ConvenienceYield = FuturesParameters.Default.BaseConvenienceYield,
                BaseVolatility = FuturesParameters.Default.BaseVolatility,
                IntradayVolatility = FuturesParameters.Default.IntradayVolatility
            }
        };
        
        // 创建共享市场实例（用于收集新闻）
        var sharedMarket = new FuturesMarket(clock, random);
        sharedMarket.TicksPerDay = _ticksPerDay;
        
        // 加载新闻配置
        if (!string.IsNullOrEmpty(newsConfigPath) && File.Exists(newsConfigPath))
        {
            sharedMarket.LoadNewsConfig(newsConfigPath);
        }
        
        // 收集每天触发的新闻
        var triggeredNewsIds = new HashSet<string>();
        for (int day = 1; day <= _daysPerSeason; day++)
        {
            clock.SetTime(day, 600, (int)season);
            
            // 调用 NewDay 来触发新闻处理
            sharedMarket.NewDay();
            
            // 从 NewsLoader 获取活跃的 RuntimeNewsEvent（包含 AffectedItems）
            if (sharedMarket.NewsLoader != null)
            {
                var activeEvents = sharedMarket.NewsLoader.GetActiveNews();
                foreach (var runtimeEvent in activeEvents)
                {
                    if (!triggeredNewsIds.Contains(runtimeEvent.Id))
                    {
                        triggeredNewsIds.Add(runtimeEvent.Id);
                        
                        // 转换星露谷时间格式（如 900）为 HH:MM 格式（如 "09:00"）
                        int triggerTimeValue = runtimeEvent.ActualTriggerTime > 0 
                            ? runtimeEvent.ActualTriggerTime 
                            : runtimeEvent.TriggerTime ?? 600;
                        int hours = triggerTimeValue / 100;
                        int minutes = triggerTimeValue % 100;
                        string triggerTimeStr = $"{hours:D2}:{minutes:D2}";
                        
                        output.NewsEvents.Add(new NewsEventOutput
                        {
                            Id = runtimeEvent.Id,
                            Title = runtimeEvent.Title,
                            Description = runtimeEvent.Description,
                            Severity = runtimeEvent.Severity,
                            NewsType = runtimeEvent.NewsType,
                            TriggerDay = day,
                            TriggerTime = triggerTimeStr,
                            DurationDays = runtimeEvent.DurationDays,
                            AffectedItems = new List<string>(runtimeEvent.AffectedItems),
                            IsGlobal = runtimeEvent.IsGlobal,
                            Impact = new NewsImpactOutput
                            {
                                DemandDelta = runtimeEvent.DemandDelta,
                                SupplyDelta = runtimeEvent.SupplyDelta,
                                PriceMultiplier = runtimeEvent.PriceMultiplier,
                                VolatilityDelta = runtimeEvent.VolatilityDelta
                            }
                        });
                    }
                }
            }
        }
        
        // 为每个商品运行模拟
        foreach (var commodity in commodities)
        {
            var result = SimulateCommodity(commodity, season, random.Next(0, int.MaxValue), newsConfigPath);
            output.CommodityResults[commodity.Symbol] = result;
        }
        
        return output;
    }
    
    /// <summary>
    /// 模拟单个商品的完整季节数据。
    /// </summary>
    private CommodityResult SimulateCommodity(Commodity commodity, Season season, int seed, string? newsConfigPath)
    {
        var random = new DefaultRandomProvider(seed);
        var clock = new SimulationClock();
        clock.SetTime(day: 1, timeOfDay: 600, season: (int)season);
        
        var market = new FuturesMarket(clock, random);
        market.TicksPerDay = _ticksPerDay;
        
        // 加载新闻配置，使基本面价值能够随新闻更新
        if (!string.IsNullOrEmpty(newsConfigPath) && File.Exists(newsConfigPath))
        {
            market.LoadNewsConfig(newsConfigPath);
        }
        
        var contract = market.CreateContract(commodity, expirationDay: 28, expirationSeason: season);
        var generator = market.GetPriceGenerator(contract.Symbol)!;
        var fundamentalEngine = market.FundamentalEngine;
        
        var result = new CommodityResult
        {
            CommodityName = commodity.Name,
            BasePrice = commodity.BasePrice,
            StepsPerDay = _ticksPerDay
        };
        
        // 模拟每一天
        for (int day = 1; day <= _daysPerSeason; day++)
        {
            clock.SetTime(day, 600, (int)season);
            market.NewDay();
            
            double openPrice = System.Math.Round(contract.CurrentPrice, 2);
            // 计算基本面价值（应用消化因子，基于当前天数）
            double fundamentalValue = System.Math.Round(fundamentalEngine.CalculateFundamentalValue(commodity, season, null, day), 2);
            
            var dailyData = new DailyData
            {
                Day = day,
                OpenPrice = openPrice,
                FundamentalValue = fundamentalValue
            };
            
            double highPrice = openPrice;
            double lowPrice = openPrice;
            
            // 模拟每个 tick
            for (int tick = 0; tick < _ticksPerDay; tick++)
            {
                int remaining = _ticksPerDay - tick;
                
                double prevPrice = contract.CurrentPrice;
                market.Tick(remaining);
                
                double currentPrice = contract.CurrentPrice;
                highPrice = System.Math.Max(highPrice, currentPrice);
                lowPrice = System.Math.Min(lowPrice, currentPrice);
                
                // ===== 星露谷时间系统 =====
                // 交易时间: 6:00 (600) 到 18:00 (1800)
                // 格式: 0600, 0610, 0620... (每10分钟进位)
                // 计算每tick对应的游戏分钟数
                const int tradingStartTime = 600;   // 6:00
                const int tradingEndTime = 1800;    // 18:00
                const int tradingHours = 12;        // 12小时交易
                const int gameMinutesPerHour = 60;  // 每小时60分钟（星露谷格式）
                
                // 计算总交易分钟数和每tick的分钟间隔
                int totalTradingMinutes = tradingHours * gameMinutesPerHour;  // 720分钟
                int gameMinutesPerTick = totalTradingMinutes / _ticksPerDay;  // 每tick的分钟数
                
                // 计算当前tick对应的游戏分钟偏移
                int minuteOffset = tick * gameMinutesPerTick;
                
                // 转换为星露谷时间格式 (HHMM)
                int hoursOffset = minuteOffset / 60;
                int minutesInHour = minuteOffset % 60;
                int timeOfDay = tradingStartTime + (hoursOffset * 100) + minutesInHour;
                
                // 处理分钟进位 (如 0670 → 0710)
                if (timeOfDay % 100 >= 60)
                {
                    timeOfDay = ((timeOfDay / 100) + 1) * 100 + (timeOfDay % 100 - 60);
                }
                
                // 格式化为 "HH:MM" 显示
                int displayHours = timeOfDay / 100;
                int displayMinutes = timeOfDay % 100;
                
                dailyData.IntradayPrices.Add(new IntradayPricePoint
                {
                    Step = tick,
                    Time = $"{displayHours:D2}:{displayMinutes:D2}",
                    TargetPrice = System.Math.Round(generator.CurrentTarget, 2),
                    ShadowPrice = System.Math.Round(currentPrice, 2),
                    MarketImpact = System.Math.Round(contract.MarketImpact, 4)
                });
            }
            
            // 收盘结算
            market.EndOfDay();
            
            dailyData.ClosePrice = System.Math.Round(contract.SettlementPrice, 2);
            dailyData.HighPrice = System.Math.Round(highPrice, 2);
            dailyData.LowPrice = System.Math.Round(lowPrice, 2);
            
            result.DailyData.Add(dailyData);
        }
        
        return result;
    }
    
    /// <summary>
    /// 将结果保存为 JSON 文件。
    /// </summary>
    public void SaveToFile(SeasonSimulationOutput output, string filePath)
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };
        
        string json = JsonSerializer.Serialize(output, options);
        File.WriteAllText(filePath, json);
    }
}
