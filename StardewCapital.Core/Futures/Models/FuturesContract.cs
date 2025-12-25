// =====================================================================
// 文件：FuturesContract.cs
// 用途：期货合约模型，表示某商品的期货合约。
//       实现 ITradable 接口以兼容订单簿系统。
// =====================================================================

using StardewCapital.Core.Common.Market.Interfaces;

namespace StardewCapital.Core.Futures.Models;

/// <summary>
/// 商品期货合约。
/// 实现 ITradable 接口以兼容订单簿系统。
/// </summary>
public class FuturesContract : ITradable
{
    /// <summary>
    /// 标的商品。
    /// </summary>
    public Commodity Underlying { get; }
    
    /// <summary>
    /// 合约到期日（1-28）。
    /// </summary>
    public int ExpirationDay { get; }
    
    /// <summary>
    /// 合约到期季节。
    /// </summary>
    public Season ExpirationSeason { get; }
    
    /// <summary>
    /// 合约规模（每手单位数量）。
    /// </summary>
    public int ContractSize { get; }
    
    /// <summary>
    /// 该合约的市场参数。
    /// </summary>
    public FuturesParameters Parameters { get; }
    
    // ITradable 接口实现
    public string Symbol => $"{Underlying.Symbol}-{ExpirationSeason.ToString().Substring(0, 3).ToUpper()}-{ExpirationDay}";
    public double CurrentPrice { get; set; }
    public double TickSize { get; init; } = 0.01;
    
    // 价格跟踪
    public double OpenPrice { get; set; }
    public double HighPrice { get; set; }
    public double LowPrice { get; set; }
    public double SettlementPrice { get; set; }
    public double PreviousClose { get; set; }
    
    // 影子价格（理论公允价值）
    public double ShadowPrice { get; set; }
    
    // 市场冲击累计量
    public double MarketImpact { get; set; }
    
    public FuturesContract(
        Commodity underlying, 
        int expirationDay = 28, 
        Season expirationSeason = Season.Spring,
        FuturesParameters? parameters = null)
    {
        Underlying = underlying;
        ExpirationDay = expirationDay;
        ExpirationSeason = expirationSeason;
        Parameters = parameters ?? FuturesParameters.Default;
        ContractSize = Parameters.ContractSize;
        
        // 初始化价格为基础价格
        CurrentPrice = underlying.BasePrice;
        OpenPrice = underlying.BasePrice;
        HighPrice = underlying.BasePrice;
        LowPrice = underlying.BasePrice;
        SettlementPrice = underlying.BasePrice;
        ShadowPrice = underlying.BasePrice;
    }
    
    /// <summary>
    /// 根据新价格更新 OHLC 跟踪数据。
    /// </summary>
    public void UpdatePrice(double newPrice)
    {
        CurrentPrice = newPrice;
        HighPrice = System.Math.Max(HighPrice, newPrice);
        LowPrice = System.Math.Min(LowPrice, newPrice);
    }
    
    /// <summary>
    /// 重置为新交易日状态。
    /// 如果存在未消化的隔夜情绪（ShadowPrice != SettlementPrice），
    /// 则应用跳空开盘，模拟真实市场的盘后消息消化机制。
    /// </summary>
    public void NewDay()
    {
        PreviousClose = SettlementPrice;
        
        // 计算隔夜跳空：ShadowPrice 存储了尾盘熔断时未能到达的真实目标价
        double gap = ShadowPrice - SettlementPrice;
        
        // 如果有显著的未消化价差（超过 1%），应用跳空开盘
        const double gapThreshold = 0.01; // 1% 阈值
        if (System.Math.Abs(gap / SettlementPrice) > gapThreshold)
        {
            // 跳空开盘：开盘价 = 昨日收盘 + 隔夜情绪
            OpenPrice = ShadowPrice;
            CurrentPrice = ShadowPrice;
            
            // 重置 ShadowPrice 为当前价格（已消化）
            ShadowPrice = CurrentPrice;
        }
        else
        {
            // 正常开盘：延续昨日收盘
            OpenPrice = SettlementPrice;
            CurrentPrice = SettlementPrice;
        }
        
        // 重置日内 OHLC
        HighPrice = CurrentPrice;
        LowPrice = CurrentPrice;
        
        // 重置市场冲击
        MarketImpact = 0;
    }
    
    /// <summary>
    /// 根据当前日期和季节计算距到期天数。
    /// </summary>
    public int DaysToMaturity(int currentDay, Season currentSeason, int daysPerSeason = 28)
    {
        if (currentSeason == ExpirationSeason)
        {
            return System.Math.Max(0, ExpirationDay - currentDay);
        }
        
        // 跨季节计算
        int seasonDiff = ((int)ExpirationSeason - (int)currentSeason + 4) % 4;
        if (seasonDiff == 0) seasonDiff = 4; // 整年
        
        int daysRemaining = (daysPerSeason - currentDay) // 当前季节剩余天数
                          + (seasonDiff - 1) * daysPerSeason // 中间完整季节天数
                          + ExpirationDay; // 到期季节已过天数
        
        return daysRemaining;
    }
    
    /// <summary>
    /// 每手所需保证金。
    /// </summary>
    public double RequiredMargin => CurrentPrice * ContractSize * Parameters.InitialMarginRatio;
    
    /// <summary>
    /// 每手名义价值。
    /// </summary>
    public double NotionalValue => CurrentPrice * ContractSize;
}
