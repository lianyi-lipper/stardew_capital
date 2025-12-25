// =====================================================================
// 文件：FuturesPriceGenerator.cs
// 用途：期货价格生成器，整合所有定价模型。
//       同时生成日间价格和日内价格。
//       现在支持从 RuntimeCommodity 读取价格行为参数。
// =====================================================================

using StardewCapital.Core.Common;
using StardewCapital.Core.Futures.Config;
using StardewCapital.Core.Futures.Math;
using StardewCapital.Core.Futures.Models;
using StardewCapital.Core.Time;

namespace StardewCapital.Core.Futures.Pricing;

/// <summary>
/// 单个时间周期的 tick 数据。
/// </summary>
public record TickData
{
    public double Open { get; init; }
    public double High { get; init; }
    public double Low { get; init; }
    public double Close { get; init; }
    public int Volume { get; init; }
    public DateTime Timestamp { get; init; }
    
    public static TickData FromPrice(double price) => new()
    {
        Open = price,
        High = price,
        Low = price,
        Close = price,
        Timestamp = DateTime.UtcNow
    };
}

/// <summary>
/// 完整的期货价格生成器，整合所有定价模型。
/// 同时生成日间价格和日内价格。
/// </summary>
public class FuturesPriceGenerator
{
    private readonly GBM _gbm;
    private readonly BrownianBridge _bridge;
    private readonly FundamentalEngine _fundamentalEngine;
    private readonly IRandomProvider _random;
    
    // 当前状态
    private double _currentDailyTarget;
    private double _currentIntradayPrice;
    
    // 价格历史
    private readonly List<double> _dailyPrices = new();
    private readonly List<TickData> _intradayTicks = new();
    
    // RuntimeCommodity 参数（可选）
    private RuntimeCommodity? _runtimeCommodity;
    private PriceBehaviorParams _behaviorParams = PriceBehaviorParams.Default;
    
    public FuturesPriceGenerator(
        IRandomProvider? random = null,
        FundamentalEngine? fundamentalEngine = null)
    {
        _random = random ?? new DefaultRandomProvider();
        _gbm = new GBM(_random);
        _bridge = new BrownianBridge(_random);
        _fundamentalEngine = fundamentalEngine ?? new FundamentalEngine();
    }
    
    /// <summary>
    /// 设置 RuntimeCommodity，使用其价格行为参数。
    /// </summary>
    public void SetRuntimeCommodity(RuntimeCommodity commodity)
    {
        _runtimeCommodity = commodity;
        _behaviorParams = new PriceBehaviorParams
        {
            BaseVolatility = commodity.BaseVolatility,
            MomentumFactor = commodity.MomentumFactor,
            MeanReversionSpeed = commodity.MeanReversionSpeed,
            VolatilityClustering = commodity.VolatilityClustering,
            JumpProbability = commodity.JumpProbability,
            JumpMagnitude = commodity.JumpMagnitude
        };
    }
    
    /// <summary>
    /// 获取当前使用的价格行为参数。
    /// </summary>
    public PriceBehaviorParams CurrentBehaviorParams => _behaviorParams;
    
    /// <summary>
    /// 初始化合约的价格生成器。
    /// 应在季节开始时调用。
    /// </summary>
    public void Initialize(FuturesContract contract, Season currentSeason)
    {
        _gbm.Reset(); // 重置 GBM 状态
        
        double fundamental = _fundamentalEngine.CalculateFundamentalValue(
            contract.Underlying, 
            currentSeason);
        
        _currentDailyTarget = fundamental;
        _currentIntradayPrice = contract.CurrentPrice;
        
        _dailyPrices.Clear();
        _dailyPrices.Add(contract.CurrentPrice);
    }
    
    /// <summary>
    /// 使用 GBM（模型二）生成次日结算价。
    /// 应在每个交易日结束时调用。
    /// </summary>
    public double GenerateDailyClose(
        FuturesContract contract,
        int currentDay,
        Season currentSeason)
    {
        // 计算包含当前新闻的基本面价值（应用消化因子）
        double fundamentalValue = _fundamentalEngine.CalculateFundamentalValue(
            contract.Underlying,
            currentSeason,
            _fundamentalEngine.ActiveNews,
            currentDay);
        
        // 计算距到期天数
        int daysRemaining = contract.DaysToMaturity(currentDay, currentSeason);
        
        // 使用 GBM 获取下一价格
        double currentPrice = _dailyPrices.Count > 0 
            ? _dailyPrices[^1] 
            : contract.CurrentPrice;
        
        // 获取季节波动率修正
        double seasonVolatilityMultiplier = 1.0;
        if (_runtimeCommodity != null)
        {
            seasonVolatilityMultiplier = _runtimeCommodity.GetSeasonVolatility(currentSeason);
        }
        
        // 应用新闻对波动率的影响
        double newsVolatilityDelta = _fundamentalEngine.CurrentVolatilityModifier;
        
        // 创建调整后的行为参数
        var adjustedBehavior = _behaviorParams with
        {
            BaseVolatility = (_behaviorParams.BaseVolatility + newsVolatilityDelta) * seasonVolatilityMultiplier
        };
        
        // 使用高级 GBM 计算
        double nextPrice = _gbm.CalculateNextPriceAdvanced(
            currentPrice,
            fundamentalValue,
            daysRemaining,
            adjustedBehavior);
        
        // 应用持有成本调整
        double spotEquivalent = CostOfCarry.CalculateImpliedSpot(
            nextPrice,
            daysRemaining,
            contract.Parameters.RiskFreeRate,
            contract.Parameters.StorageCost,
            contract.Parameters.BaseConvenienceYield);
        
        double futuresPrice = CostOfCarry.CalculateFuturesPrice(
            spotEquivalent,
            daysRemaining,
            contract.Parameters.RiskFreeRate,
            contract.Parameters.StorageCost,
            contract.Parameters.BaseConvenienceYield);
        
        _dailyPrices.Add(futuresPrice);
        _currentDailyTarget = futuresPrice;
        
        return futuresPrice;
    }
    
    /// <summary>
    /// 使用布朗桥（模型四）生成下一日内 tick 价格。
    /// 应在交易时段内每个游戏 tick 调用。
    /// </summary>
    public double GenerateIntradayTick(
        FuturesContract contract,
        int ticksRemaining,
        int totalTicksPerDay)
    {
        double targetClose = _currentDailyTarget;
        
        // 使用 RuntimeCommodity 的日内波动率（如果有的话）
        double intradayVol = _runtimeCommodity?.IntradayVolatility 
            ?? contract.Parameters.IntradayVolatility;
        
        double nextPrice = _bridge.GetNextPrice(
            _currentIntradayPrice,
            targetClose,
            ticksRemaining,
            totalTicksPerDay,
            intradayVol);
        
        // 应用市场冲击（如有）
        nextPrice += contract.MarketImpact;
        
        // 衰减市场冲击
        contract.MarketImpact *= 0.95;
        
        // 更新状态
        _currentIntradayPrice = nextPrice;
        
        // 记录 tick
        _intradayTicks.Add(TickData.FromPrice(nextPrice));
        
        return nextPrice;
    }
    
    /// <summary>
    /// 当重大新闻事件改变目标价格时调用。
    /// </summary>
    public void OnNewsEvent(
        FuturesContract contract,
        NewsEvent news,
        Season currentSeason,
        int currentDay,
        int ticksRemaining,
        int totalTicks)
    {
        // 默认使用 gradual 模式
        OnNewsEventAdvanced(contract, news, currentSeason, currentDay, 
            ticksRemaining, totalTicks, "gradual", 0.8, true);
    }
    
    /// <summary>
    /// 当新闻事件改变目标价格时调用（高级版本，支持不同影响模式）。
    /// </summary>
    /// <param name="contract">期货合约</param>
    /// <param name="news">新闻事件</param>
    /// <param name="currentSeason">当前季节</param>
    /// <param name="currentDay">当前天数</param>
    /// <param name="ticksRemaining">剩余 tick 数</param>
    /// <param name="totalTicks">每天总 tick 数</param>
    /// <param name="impactMode">影响模式：immediate（即时）、gradual（渐进）、next_open（次日跳空）</param>
    /// <param name="impactSpeed">影响速度 0-1，仅对 immediate 有效</param>
    /// <param name="isWithinTradingHours">是否在交易时段内</param>
    public void OnNewsEventAdvanced(
        FuturesContract contract,
        NewsEvent news,
        Season currentSeason,
        int currentDay,
        int ticksRemaining,
        int totalTicks,
        string impactMode,
        double impactSpeed,
        bool isWithinTradingHours)
    {
        // 将新闻添加到基本面引擎
        _fundamentalEngine.AddNews(news);
        
        // 重新计算目标价格（应用消化因子）
        double newFundamental = _fundamentalEngine.CalculateFundamentalValue(
            contract.Underlying,
            currentSeason,
            _fundamentalEngine.ActiveNews,
            currentDay);
        
        int daysRemaining = contract.DaysToMaturity(currentDay, currentSeason);
        
        // 应用持有成本计算新目标
        double newTarget = CostOfCarry.CalculateFuturesPrice(
            newFundamental,
            daysRemaining,
            contract.Parameters.RiskFreeRate,
            contract.Parameters.StorageCost,
            contract.Parameters.BaseConvenienceYield);
        
        // 计算价格冲击幅度
        double priceImpact = newTarget - _currentIntradayPrice;
        
        switch (impactMode.ToLower())
        {
            case "immediate":
                // 即时冲击模式（用于 critical 新闻）
                // price_multiplier → 瞬间跳动
                // demand/supply_delta → 影响收盘价（已通过基本面引擎处理）
                
                if (isWithinTradingHours && news.PriceMultiplier != 1.0)
                {
                    // 盘中：使用 PriceMultiplier 瞬间调整价格
                    double newPrice = _currentIntradayPrice * news.PriceMultiplier;
                    _currentIntradayPrice = newPrice;
                    
                    // 目标价由供需变化决定（收盘时体现）
                    _currentDailyTarget = newTarget;
                }
                else if (!isWithinTradingHours && news.PriceMultiplier != 1.0)
                {
                    // 盘前/盘后：累积到次日跳空
                    contract.ShadowPrice = contract.CurrentPrice * news.PriceMultiplier;
                    _currentDailyTarget = newTarget;
                }
                else
                {
                    // PriceMultiplier == 1.0，只更新目标价
                    _currentDailyTarget = newTarget;
                }
                break;
                
            case "next_open":
                // 次日跳空：影响全部累积到次日开盘
                contract.ShadowPrice = newTarget;
                // 不改变当前目标价，让今日价格正常运行
                break;
                
            case "gradual":
            default:
                // 渐进模式：通过目标价调整，让布朗桥自然过渡
                // 检查熔断机制（尾盘熔断）
                double priceChange = System.Math.Abs(newTarget - _currentIntradayPrice) / _currentIntradayPrice;
                double timeProgress = 1.0 - (double)ticksRemaining / totalTicks;
                
                if (timeProgress > 0.9 && priceChange > 0.05)
                {
                    // 熔断：限制当日涨跌幅，溢出部分延至次日跳空
                    double maxMove = _currentIntradayPrice * 0.05;
                    double direction = newTarget > _currentIntradayPrice ? 1 : -1;
                    
                    _currentDailyTarget = _currentIntradayPrice + direction * maxMove;
                    contract.ShadowPrice = newTarget; // 存储真实目标以供跳空开盘
                }
                else
                {
                    _currentDailyTarget = newTarget;
                }
                break;
        }
    }
    
    /// <summary>
    /// 使用 RuntimeNewsEvent 处理新闻（推荐方式）。
    /// 根据 Severity 自动决定影响模式：
    /// - critical → immediate（即时冲击）
    /// - high/medium/low → gradual（渐进）
    /// </summary>
    public void OnRuntimeNewsEvent(
        FuturesContract contract,
        RuntimeNewsEvent news,
        Season currentSeason,
        int currentDay,
        int ticksRemaining,
        int totalTicks,
        bool isWithinTradingHours)
    {
        // 转换为 NewsEvent
        var newsEvent = NewsEvent.FromRuntime(news, currentDay);
        
        // 根据 severity 决定影响模式
        string impactMode = news.Severity.ToLower() switch
        {
            "critical" => "immediate",
            _ => "gradual"
        };
        
        // critical 的冲击速度为 0.8，其他不适用
        double impactSpeed = news.Severity.ToLower() == "critical" ? 0.8 : 0.5;
        
        // 使用高级方法
        OnNewsEventAdvanced(
            contract, 
            newsEvent, 
            currentSeason, 
            currentDay,
            ticksRemaining, 
            totalTicks, 
            impactMode, 
            impactSpeed,
            isWithinTradingHours);
    }
    
    /// <summary>
    /// 获取完整的日间价格历史。
    /// </summary>
    public IReadOnlyList<double> DailyPrices => _dailyPrices;
    
    /// <summary>
    /// 获取当前日内 tick 记录。
    /// </summary>
    public IReadOnlyList<TickData> IntradayTicks => _intradayTicks;
    
    /// <summary>
    /// 清理日内 tick（在新交易日开始时调用）。
    /// </summary>
    public void ClearIntradayTicks()
    {
        _intradayTicks.Clear();
    }
    
    /// <summary>
    /// 获取当前目标价格（用于调试/显示）。
    /// </summary>
    public double CurrentTarget => _currentDailyTarget;
    
    /// <summary>
    /// 获取 GBM 的当前波动率状态。
    /// </summary>
    public double CurrentVolatilityState => _gbm.CurrentVolatilityState;
}
