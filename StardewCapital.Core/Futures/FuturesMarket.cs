// =====================================================================
// 文件：FuturesMarket.cs
// 用途：期货模块的门面类，提供对所有期货功能的简化访问接口。
//       现在支持从 CommodityConfigLoader 加载商品配置。
// =====================================================================

using StardewCapital.Core.Common;
using StardewCapital.Core.Futures.Config;
using StardewCapital.Core.Futures.Math;
using StardewCapital.Core.Futures.Models;
using StardewCapital.Core.Futures.Pricing;
using StardewCapital.Core.Time;

namespace StardewCapital.Core.Futures;

/// <summary>
/// 期货模块的门面类。
/// 提供对所有期货功能的简化访问接口。
/// </summary>
public class FuturesMarket
{
    private readonly ITimeProvider _timeProvider;
    private readonly IRandomProvider _random;
    private readonly FundamentalEngine _fundamentalEngine;
    private readonly Dictionary<string, FuturesContract> _contracts = new();
    private readonly Dictionary<string, FuturesPriceGenerator> _priceGenerators = new();
    private readonly Dictionary<string, RuntimeCommodity> _runtimeCommodities = new();
    
    // 配置加载器
    private CommodityConfigLoader? _commodityLoader;
    private NewsConfigLoader? _newsLoader;
    
    // 配置参数
    public FuturesParameters DefaultParameters { get; set; } = FuturesParameters.Default;
    public int TicksPerDay { get; set; } = 100; // 每交易日的可配置 tick 数量
    public Region CurrentRegion { get; private set; } = Region.PelicanTown;
    
    public FuturesMarket(ITimeProvider timeProvider, IRandomProvider? random = null)
    {
        _timeProvider = timeProvider;
        _random = random ?? new DefaultRandomProvider();
        _fundamentalEngine = new FundamentalEngine();
    }
    
    /// <summary>
    /// 加载商品配置。
    /// </summary>
    public void LoadCommodityConfig(string filePath)
    {
        _commodityLoader = new CommodityConfigLoader();
        _commodityLoader.LoadFromFile(filePath);
        _commodityLoader.CurrentRegion = CurrentRegion;
    }
    
    /// <summary>
    /// 加载新闻配置。
    /// </summary>
    public void LoadNewsConfig(string filePath)
    {
        _newsLoader = new NewsConfigLoader(_random);
        _newsLoader.LoadFromFile(filePath);
        _fundamentalEngine.SetNewsLoader(_newsLoader);
    }
    
    /// <summary>
    /// 切换当前地区。
    /// </summary>
    public void SwitchRegion(Region region)
    {
        CurrentRegion = region;
        if (_commodityLoader != null)
        {
            _commodityLoader.CurrentRegion = region;
        }
    }
    
    /// <summary>
    /// 使用 RuntimeCommodity 创建新的期货合约（推荐方式）。
    /// </summary>
    public FuturesContract CreateContractFromConfig(
        string commodityId,
        int expirationDay = 28,
        Season? expirationSeason = null)
    {
        if (_commodityLoader == null)
        {
            throw new InvalidOperationException("请先调用 LoadCommodityConfig 加载商品配置");
        }
        
        var runtimeCommodity = _commodityLoader.GetCommodity(commodityId);
        if (runtimeCommodity == null)
        {
            throw new ArgumentException($"未找到商品配置: {commodityId}");
        }
        
        // 转换为基础 Commodity（兼容旧代码）
        var commodity = runtimeCommodity.ToCommodity();
        
        // 根据配置创建参数
        var parameters = new FuturesParameters
        {
            InitialMarginRatio = runtimeCommodity.InitialMarginRatio,
            MaintenanceMarginRatio = runtimeCommodity.MaintenanceMarginRatio,
            ContractSize = runtimeCommodity.ContractSize,
            BaseVolatility = runtimeCommodity.BaseVolatility,
            IntradayVolatility = runtimeCommodity.IntradayVolatility,
            StorageCost = runtimeCommodity.StorageCostPerDay
        };
        
        var season = expirationSeason ?? (Season)_timeProvider.CurrentSeason;
        var contract = new FuturesContract(commodity, expirationDay, season, parameters);
        
        // 初始化价格（应用消化因子）
        var currentSeason = (Season)_timeProvider.CurrentSeason;
        int currentDay = _timeProvider.CurrentDay;
        double fundamental = _fundamentalEngine.CalculateFundamentalValue(commodity, currentSeason, null, currentDay);
        
        int daysRemaining = contract.DaysToMaturity(currentDay, currentSeason);
        
        double futuresPrice = CostOfCarry.CalculateFuturesPrice(
            fundamental,
            daysRemaining,
            contract.Parameters.RiskFreeRate,
            contract.Parameters.StorageCost,
            contract.Parameters.BaseConvenienceYield);
        
        contract.CurrentPrice = futuresPrice;
        contract.OpenPrice = futuresPrice;
        contract.SettlementPrice = futuresPrice;
        contract.ShadowPrice = fundamental;
        
        // 创建价格生成器并设置 RuntimeCommodity
        var generator = new FuturesPriceGenerator(_random, _fundamentalEngine);
        generator.SetRuntimeCommodity(runtimeCommodity);
        generator.Initialize(contract, currentSeason);
        
        _contracts[contract.Symbol] = contract;
        _priceGenerators[contract.Symbol] = generator;
        _runtimeCommodities[contract.Symbol] = runtimeCommodity;
        
        return contract;
    }
    
    /// <summary>
    /// 为某商品创建新的期货合约（兼容旧 API）。
    /// </summary>
    public FuturesContract CreateContract(
        Commodity commodity, 
        int expirationDay = 28,
        Season? expirationSeason = null,
        FuturesParameters? parameters = null)
    {
        var season = expirationSeason ?? (Season)_timeProvider.CurrentSeason;
        var contract = new FuturesContract(
            commodity, 
            expirationDay, 
            season, 
            parameters ?? DefaultParameters);
        
        // 初始化价格（应用消化因子）
        var currentSeason = (Season)_timeProvider.CurrentSeason;
        int currentDay = _timeProvider.CurrentDay;
        double fundamental = _fundamentalEngine.CalculateFundamentalValue(commodity, currentSeason, null, currentDay);
        
        int daysRemaining = contract.DaysToMaturity(
            currentDay, 
            currentSeason);
        
        double futuresPrice = CostOfCarry.CalculateFuturesPrice(
            fundamental,
            daysRemaining,
            contract.Parameters.RiskFreeRate,
            contract.Parameters.StorageCost,
            contract.Parameters.BaseConvenienceYield);
        
        contract.CurrentPrice = futuresPrice;
        contract.OpenPrice = futuresPrice;
        contract.SettlementPrice = futuresPrice;
        contract.ShadowPrice = fundamental;
        
        // 创建价格生成器
        var generator = new FuturesPriceGenerator(_random, _fundamentalEngine);
        generator.Initialize(contract, currentSeason);
        
        _contracts[contract.Symbol] = contract;
        _priceGenerators[contract.Symbol] = generator;
        
        return contract;
    }
    
    /// <summary>
    /// 根据合约代码获取现有合约。
    /// </summary>
    public FuturesContract? GetContract(string symbol)
    {
        return _contracts.TryGetValue(symbol, out var contract) ? contract : null;
    }
    
    /// <summary>
    /// 获取所有活跃合约。
    /// </summary>
    public IReadOnlyCollection<FuturesContract> AllContracts => _contracts.Values;
    
    /// <summary>
    /// 获取商品的 RuntimeCommodity（如果有）。
    /// </summary>
    public RuntimeCommodity? GetRuntimeCommodity(string symbol)
    {
        return _runtimeCommodities.TryGetValue(symbol, out var rc) ? rc : null;
    }
    
    /// <summary>
    /// 更新一个 tick 的价格。
    /// 应在交易时段内每个游戏 tick 调用。
    /// </summary>
    public void Tick(int ticksRemaining)
    {
        var currentSeason = (Season)_timeProvider.CurrentSeason;
        
        foreach (var (symbol, contract) in _contracts)
        {
            if (_priceGenerators.TryGetValue(symbol, out var generator))
            {
                double newPrice = generator.GenerateIntradayTick(
                    contract, 
                    ticksRemaining, 
                    TicksPerDay);
                
                contract.UpdatePrice(newPrice);
            }
        }
        
        // 处理市场关联传导
        ProcessCorrelations();
    }
    
    /// <summary>
    /// 处理市场关联价格传导。
    /// </summary>
    private void ProcessCorrelations()
    {
        if (_commodityLoader == null) return;
        
        foreach (var (symbol, contract) in _contracts)
        {
            if (!_runtimeCommodities.TryGetValue(symbol, out var sourceCommodity))
                continue;
            
            // 计算价格变化率
            double priceChange = (contract.CurrentPrice - contract.OpenPrice) / contract.OpenPrice;
            
            // 传导到关联商品
            foreach (var (corrId, corrType, strength) in sourceCommodity.Correlations)
            {
                if (!_contracts.TryGetValue(corrId, out var targetContract))
                    continue;
                
                // 计算传导冲击
                double impact = 0;
                switch (corrType)
                {
                    case CorrelationType.Input:
                        // 原料涨 → 产品成本涨 → 产品涨
                        impact = priceChange * strength * targetContract.CurrentPrice * 0.1;
                        break;
                    case CorrelationType.Output:
                        // 产品涨 ← 原料涨（反向传导较弱）
                        impact = priceChange * strength * 0.5 * targetContract.CurrentPrice * 0.1;
                        break;
                    case CorrelationType.Substitute:
                        // 一方涨 → 需求转移 → 另一方涨（较弱）
                        impact = priceChange * strength * 0.3 * targetContract.CurrentPrice * 0.1;
                        break;
                }
                
                // 应用冲击（累积到 MarketImpact）
                targetContract.MarketImpact += impact;
            }
        }
    }
    
    /// <summary>
    /// 处理日终结算。
    /// 应在收盘时调用。
    /// </summary>
    public void EndOfDay()
    {
        var currentSeason = (Season)_timeProvider.CurrentSeason;
        var currentDay = _timeProvider.CurrentDay;
        
        foreach (var (symbol, contract) in _contracts)
        {
            int daysRemaining = contract.DaysToMaturity(currentDay, currentSeason);
            
            if (daysRemaining <= 0)
            {
                // 到期日：强制结算价等于基本面价值（完全消化）
                double fundamentalValue = _fundamentalEngine.CalculateFundamentalValue(
                    contract.Underlying, 
                    currentSeason,
                    null,
                    currentDay);
                contract.SettlementPrice = fundamentalValue;
                contract.CurrentPrice = fundamentalValue;
            }
            else
            {
                // 非到期日：使用当前价格结算
                contract.SettlementPrice = contract.CurrentPrice;
            }
            
            // 生成明日目标价
            if (_priceGenerators.TryGetValue(symbol, out var generator))
            {
                generator.GenerateDailyClose(contract, currentDay, currentSeason);
                generator.ClearIntradayTicks();
            }
        }
    }
    
    /// <summary>
    /// 处理新交易日开始。
    /// </summary>
    public void NewDay()
    {
        var currentSeason = (Season)_timeProvider.CurrentSeason;
        var currentDay = _timeProvider.CurrentDay;
        
        foreach (var contract in _contracts.Values)
        {
            contract.NewDay();
        }
        
        // 清理过期新闻
        _fundamentalEngine.ClearExpiredNews(currentDay);
        
        // 处理今日新闻触发（如果有配置加载器）
        if (_newsLoader != null)
        {
            var triggeredNews = _fundamentalEngine.ProcessDailyNews(currentDay, currentSeason);
            
            // 通知相关合约
            foreach (var news in triggeredNews)
            {
                // 将 RuntimeNewsEvent 转换为 NewsEvent（已在 ProcessDailyNews 内部处理）
            }
        }
    }
    
    /// <summary>
    /// 添加影响商品价格的新闻事件。
    /// </summary>
    public void AddNews(NewsEvent news, string? commoditySymbol = null)
    {
        _fundamentalEngine.AddNews(news);
        
        var currentSeason = (Season)_timeProvider.CurrentSeason;
        
        // 通知所有相关合约
        foreach (var (symbol, contract) in _contracts)
        {
            if (commoditySymbol == null || contract.Underlying.Symbol == commoditySymbol)
            {
                if (_priceGenerators.TryGetValue(symbol, out var generator))
                {
                    // 计算剩余 tick 数（简化处理，假设在日中）
                    int ticksRemaining = TicksPerDay / 2;
                    
                    generator.OnNewsEvent(
                        contract,
                        news,
                        currentSeason,
                        _timeProvider.CurrentDay,
                        ticksRemaining,
                        TicksPerDay);
                }
            }
        }
    }
    
    /// <summary>
    /// 应用交易对市场价格的冲击。
    /// </summary>
    public void ApplyTradeImpact(string symbol, double impactAmount)
    {
        if (_contracts.TryGetValue(symbol, out var contract))
        {
            contract.MarketImpact += impactAmount;
        }
    }
    
    /// <summary>
    /// 获取基本面引擎（供高级用法）。
    /// </summary>
    public FundamentalEngine FundamentalEngine => _fundamentalEngine;
    
    /// <summary>
    /// 获取合约的价格生成器（供高级用法）。
    /// </summary>
    public FuturesPriceGenerator? GetPriceGenerator(string symbol)
    {
        return _priceGenerators.TryGetValue(symbol, out var gen) ? gen : null;
    }
    
    /// <summary>
    /// 获取商品配置加载器。
    /// </summary>
    public CommodityConfigLoader? CommodityLoader => _commodityLoader;
    
    /// <summary>
    /// 获取新闻配置加载器。
    /// </summary>
    public NewsConfigLoader? NewsLoader => _newsLoader;
}
