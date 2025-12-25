// =====================================================================
// 文件：FundamentalEngine.cs
// 用途：基本面价值引擎（模型一）。
//       基于供需基本面计算"真实"价值 S_T。
//       新闻事件现在从 JSON 配置文件加载。
// =====================================================================

using StardewCapital.Core.Futures.Config;
using StardewCapital.Core.Futures.Models;

namespace StardewCapital.Core.Futures.Pricing;

/// <summary>
/// 简化的新闻事件（供内部使用，兼容旧代码）。
/// 新系统请使用 RuntimeNewsEvent。
/// </summary>
public record NewsEvent
{
    public string Id { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public int TriggerDay { get; init; }
    
    /// <summary>
    /// 新闻严重度：critical | high | medium | low
    /// </summary>
    public string Severity { get; init; } = "medium";
    
    /// <summary>
    /// 对需求的影响（正值 = 需求增加 = 价格上涨）。
    /// </summary>
    public double DemandImpact { get; init; }
    
    /// <summary>
    /// 对供给的影响（负值 = 供给减少 = 价格上涨）。
    /// </summary>
    public double SupplyImpact { get; init; }
    
    /// <summary>
    /// 该新闻影响价格的持续天数（消化期）。
    /// </summary>
    public int DurationDays { get; init; } = 1;
    
    /// <summary>
    /// 是否为永久性影响。
    /// true = 消化完后影响持续存在（如开新店、害虫毁庄稼）
    /// false = 临时性，过期后消失（如节日促销）
    /// </summary>
    public bool IsPermanent { get; init; } = false;
    
    /// <summary>
    /// 波动率影响（可选）。
    /// </summary>
    public double VolatilityDelta { get; init; }
    
    /// <summary>
    /// 价格乘数（用于 critical 新闻的即时冲击）。
    /// 1.0 = 无影响，1.1 = 涨10%，0.9 = 跌10%。
    /// </summary>
    public double PriceMultiplier { get; init; } = 1.0;
    
    /// <summary>
    /// 从 RuntimeNewsEvent 创建 NewsEvent。
    /// </summary>
    public static NewsEvent FromRuntime(RuntimeNewsEvent runtime, int currentDay)
    {
        return new NewsEvent
        {
            Id = runtime.Id,
            Title = runtime.Title,
            TriggerDay = runtime.TriggerDay,
            Severity = runtime.Severity,
            DemandImpact = runtime.DemandDelta,
            SupplyImpact = runtime.SupplyDelta,
            DurationDays = runtime.DurationDays,
            IsPermanent = runtime.IsPermanent,
            VolatilityDelta = runtime.VolatilityDelta,
            PriceMultiplier = runtime.PriceMultiplier
        };
    }
}

/// <summary>
/// 基本面价值引擎（模型一）。
/// 基于供需基本面计算"真实"价值 S_T。
/// 
/// 公式：S_T = P_base * λ_s * (D_base + ΣD_news) / (S_base + ΣS_news)
/// </summary>
public class FundamentalEngine
{
    private readonly List<NewsEvent> _activeNews = new();
    private NewsConfigLoader? _newsConfigLoader;
    
    /// <summary>
    /// 当前的波动率修正值（来自新闻影响）。
    /// </summary>
    public double CurrentVolatilityModifier { get; private set; }
    
    /// <summary>
    /// 加载新闻配置。
    /// </summary>
    public void LoadNewsConfig(string filePath)
    {
        _newsConfigLoader = new NewsConfigLoader();
        _newsConfigLoader.LoadFromFile(filePath);
    }
    
    /// <summary>
    /// 加载新闻配置（从 JSON 字符串）。
    /// </summary>
    public void LoadNewsConfigFromJson(string json)
    {
        _newsConfigLoader = new NewsConfigLoader();
        _newsConfigLoader.LoadFromJson(json);
    }
    
    /// <summary>
    /// 设置新闻配置加载器。
    /// </summary>
    public void SetNewsLoader(NewsConfigLoader loader)
    {
        _newsConfigLoader = loader;
    }
    
    /// <summary>
    /// 处理每日新闻触发。
    /// </summary>
    /// <param name="day">当前天数</param>
    /// <param name="season">当前季节</param>
    /// <returns>今日触发的新闻列表</returns>
    public List<RuntimeNewsEvent> ProcessDailyNews(int day, Season season)
    {
        if (_newsConfigLoader == null)
            return new List<RuntimeNewsEvent>();
        
        var alreadyTriggered = new HashSet<string>(
            _activeNews.Select(n => n.Id));
        
        // 获取今日触发的新闻
        var newlyTriggered = _newsConfigLoader.GetTriggeredNews(day, season, alreadyTriggered);
        
        // 检查后续新闻
        alreadyTriggered.UnionWith(newlyTriggered.Select(n => n.Id));
        var followUps = _newsConfigLoader.CheckFollowUps(day, alreadyTriggered);
        newlyTriggered.AddRange(followUps);
        
        // 转换为 NewsEvent 并添加
        foreach (var runtime in newlyTriggered)
        {
            var newsEvent = NewsEvent.FromRuntime(runtime, day);
            _activeNews.Add(newsEvent);
        }
        
        // 更新波动率修正
        UpdateVolatilityModifier();
        
        return newlyTriggered;
    }
    
    /// <summary>
    /// 更新波动率修正值（累计所有活跃新闻的影响）。
    /// </summary>
    private void UpdateVolatilityModifier()
    {
        CurrentVolatilityModifier = _activeNews.Sum(n => n.VolatilityDelta);
    }
    
    /// <summary>
    /// 计算商品的基本面价值。
    /// 新闻影响使用消化函数 γ(t,i) 逐步生效（避免红线跳崖）。
    /// </summary>
    /// <param name="commodity">待定价的商品</param>
    /// <param name="currentSeason">当前游戏季节</param>
    /// <param name="activeNews">影响该商品的活跃新闻事件</param>
    /// <param name="currentDay">当前游戏天数（用于计算消化程度）</param>
    public double CalculateFundamentalValue(
        Commodity commodity,
        Season currentSeason,
        IEnumerable<NewsEvent>? activeNews = null,
        int currentDay = 28)  // 默认28 = 完全消化（兼容旧调用）
    {
        // 基础价格
        double basePrice = commodity.BasePrice;
        
        // 季节乘数 λ_s
        double seasonalMultiplier = commodity.GetSeasonalMultiplier(currentSeason);
        
        // 计算包含新闻影响的总需求和总供给
        double totalDemand = commodity.BaseDemand;
        double totalSupply = commodity.BaseSupply;
        
        var newsToApply = activeNews ?? _activeNews;
        
        foreach (var news in newsToApply)
        {
            // 计算消化因子 γ(t, i)
            // 永久性新闻：消化完后 γ=1 保持
            // 临时性新闻：消化完后 γ 逐渐衰减到 0
            double gamma = CalculateDigestionFactor(currentDay, news.TriggerDay, news.DurationDays, news.IsPermanent);
            
            // 应用消化因子到新闻影响
            totalDemand += news.DemandImpact * gamma;
            totalSupply += news.SupplyImpact * gamma;
        }
        
        // 确保供给不会归零或为负
        totalSupply = System.Math.Max(100, totalSupply);
        totalDemand = System.Math.Max(100, totalDemand);
        
        // S_T = P_base * λ_s * (D / S)
        double supplyDemandRatio = totalDemand / totalSupply;
        double fundamentalValue = basePrice * seasonalMultiplier * supplyDemandRatio;
        
        return fundamentalValue;
    }
    
    /// <summary>
    /// 计算新闻的消化因子 γ(t, i)。
    /// 
    /// 永久性新闻 (isPermanent = true):
    ///   γ = 0: 新闻还未发生
    ///   γ = (t - tStart + 1) / L: 正在消化中
    ///   γ = 1: 完全消化，影响永久保持
    /// 
    /// 临时性新闻 (isPermanent = false):
    ///   γ = 0: 新闻还未发生
    ///   γ = (t - tStart + 1) / L: 正在消化中
    ///   γ = 1: 完全消化
    ///   γ = 0: 过期后影响消失
    /// </summary>
    private double CalculateDigestionFactor(int currentDay, int triggerDay, int durationDays, bool isPermanent)
    {
        if (currentDay < triggerDay)
        {
            // 新闻还未发生
            return 0;
        }
        
        int daysSinceTrigger = currentDay - triggerDay;
        
        if (isPermanent)
        {
            // 永久性新闻：消化完后保持 γ=1
            if (durationDays <= 0 || daysSinceTrigger >= durationDays)
            {
                return 1.0;
            }
            // 正在消化中
            double gamma = (double)(daysSinceTrigger + 1) / durationDays;
            return System.Math.Min(1.0, gamma);
        }
        else
        {
            // 临时性新闻：消化完后过期消失
            // 持续期 = 2 * durationDays（前半消化，后半衰减）
            int totalDuration = durationDays * 2;
            
            if (daysSinceTrigger >= totalDuration)
            {
                // 已过期，影响消失
                return 0;
            }
            
            if (daysSinceTrigger < durationDays)
            {
                // 前半：消化期（0% → 100%）
                double gamma = (double)(daysSinceTrigger + 1) / durationDays;
                return System.Math.Min(1.0, gamma);
            }
            else
            {
                // 后半：衰减期（100% → 0%）
                int daysSinceFullDigestion = daysSinceTrigger - durationDays;
                double gamma = 1.0 - (double)(daysSinceFullDigestion + 1) / durationDays;
                return System.Math.Max(0, gamma);
            }
        }
    }
    
    /// <summary>
    /// 计算时刻 t 的期望基本面价值 E_t[S_T*]，
    /// 仅纳入截至 t 日已知的新闻信息。
    /// </summary>
    public double CalculateExpectedValue(
        Commodity commodity,
        Season currentSeason,
        int currentDay,
        IEnumerable<NewsEvent> allNews)
    {
        // 筛选截至当前日期已发生的新闻
        var knownNews = allNews.Where(n => n.TriggerDay <= currentDay);
        
        return CalculateFundamentalValue(commodity, currentSeason, knownNews);
    }
    
    /// <summary>
    /// 添加新闻事件（手动添加）。
    /// </summary>
    public void AddNews(NewsEvent news)
    {
        _activeNews.Add(news);
        UpdateVolatilityModifier();
    }
    
    /// <summary>
    /// 清理已过期的新闻事件。
    /// 永久性新闻不会被清理，临时性新闻在持续期结束后清理。
    /// </summary>
    public void ClearExpiredNews(int currentDay)
    {
        // 只清理临时性新闻（IsPermanent = false）
        // 临时性新闻的有效期 = TriggerDay + DurationDays * 2（消化期 + 衰减期）
        _activeNews.RemoveAll(n => 
            !n.IsPermanent && 
            currentDay > n.TriggerDay + n.DurationDays * 2);
        
        _newsConfigLoader?.UpdateActiveStatus(currentDay);
        UpdateVolatilityModifier();
    }
    
    /// <summary>
    /// 获取当前活跃的新闻列表。
    /// </summary>
    public IReadOnlyList<NewsEvent> ActiveNews => _activeNews;
    
    /// <summary>
    /// 获取新闻配置加载器。
    /// </summary>
    public NewsConfigLoader? NewsLoader => _newsConfigLoader;
    
    /// <summary>
    /// 重置所有新闻状态。
    /// </summary>
    public void Reset()
    {
        _activeNews.Clear();
        _newsConfigLoader?.Reset();
        CurrentVolatilityModifier = 0;
    }
}
