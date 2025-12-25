// =====================================================================
// 文件：FuturesOrderBook.cs
// 用途：期货专用订单簿，继承 BaseOrderBook。
//       添加期货特有的验证逻辑（保证金检查、爆仓检查等）。
// =====================================================================

using StardewCapital.Core.Common.Market.Base;
using StardewCapital.Core.Common.Market.Interfaces;
using StardewCapital.Core.Common.Market.Models;
using StardewCapital.Core.Futures.Models;

namespace StardewCapital.Core.Futures.Market;

/// <summary>
/// 期货专用订单簿。
/// 继承 BaseOrderBook，添加期货特有的验证逻辑。
/// </summary>
public class FuturesOrderBook : BaseOrderBook<FuturesContract>
{
    // 玩家账户余额（简化实现，实际应从账户服务获取）
    private readonly Func<string, double>? _getAccountBalance;
    
    public FuturesOrderBook(
        FuturesContract contract, 
        IMatchingEngine engine,
        Func<string, double>? getAccountBalance = null) 
        : base(contract, engine)
    {
        _getAccountBalance = getAccountBalance;
    }
    
    /// <summary>
    /// 期货特有的订单验证逻辑。
    /// </summary>
    /// <param name="order">待验证的订单</param>
    /// <exception cref="InvalidOperationException">验证失败时抛出</exception>
    protected override void ValidateOrder(Order order)
    {
        // 基础验证
        if (order.Quantity <= 0)
        {
            throw new InvalidOperationException("订单数量必须大于0");
        }
        
        if (order.Type == OrderType.Limit && order.Price <= 0)
        {
            throw new InvalidOperationException("限价单价格必须大于0");
        }
        
        // 期货特有验证：检查保证金是否足够
        if (_getAccountBalance != null && order.IsPlayerOrder)
        {
            double requiredMargin = CalculateRequiredMargin(order);
            double availableBalance = _getAccountBalance(order.TraderId);
            
            if (availableBalance < requiredMargin)
            {
                throw new InvalidOperationException(
                    $"保证金不足。需要 {requiredMargin:F2}g，可用 {availableBalance:F2}g");
            }
        }
    }
    
    /// <summary>
    /// 计算订单所需保证金。
    /// </summary>
    private double CalculateRequiredMargin(Order order)
    {
        // 使用当前价格或订单价格
        double price = order.Type == OrderType.Market 
            ? Asset.CurrentPrice 
            : order.Price;
        
        // 保证金 = 价格 × 数量 × 合约规模 × 保证金率
        return price * order.Quantity * Asset.ContractSize * Asset.Parameters.InitialMarginRatio;
    }
    
    /// <summary>
    /// 检查订单簿中的玩家订单是否需要爆仓。
    /// </summary>
    /// <param name="currentMarketPrice">当前市场价格</param>
    /// <param name="getPositionPnL">获取持仓盈亏的函数</param>
    /// <returns>需要强平的订单ID列表</returns>
    public IEnumerable<long> CheckLiquidations(
        double currentMarketPrice, 
        Func<string, double> getPositionPnL)
    {
        var ordersToLiquidate = new List<long>();
        
        // 这里是占位逻辑，实际应该检查玩家持仓的保证金覆盖率
        // 如果保证金覆盖率低于强平线，则触发强制平仓
        
        return ordersToLiquidate;
    }
    
    /// <summary>
    /// 生成 NPC 报价深度（虚拟流动性）。
    /// </summary>
    /// <param name="midPrice">中间价</param>
    /// <param name="spreadPercent">价差百分比</param>
    /// <param name="depthLevels">深度档位数</param>
    /// <param name="quantityPerLevel">每档数量</param>
    public void GenerateNPCDepth(
        double midPrice, 
        double spreadPercent = 0.01,
        int depthLevels = 5,
        int quantityPerLevel = 10)
    {
        // 清除旧的 NPC 订单
        _bids.RemoveAll(o => !o.IsPlayerOrder);
        _asks.RemoveAll(o => !o.IsPlayerOrder);
        
        double halfSpread = midPrice * spreadPercent / 2;
        
        for (int i = 0; i < depthLevels; i++)
        {
            // 生成买单（价格递减）
            double bidPrice = midPrice - halfSpread - (i * midPrice * 0.005);
            var bidOrder = Order.LimitBuy(Asset.Symbol, bidPrice, quantityPerLevel, isPlayer: false);
            _bids.Add(bidOrder);
            
            // 生成卖单（价格递增）
            double askPrice = midPrice + halfSpread + (i * midPrice * 0.005);
            var askOrder = Order.LimitSell(Asset.Symbol, askPrice, quantityPerLevel, isPlayer: false);
            _asks.Add(askOrder);
        }
        
        // 重新排序
        _bids.Sort((a, b) => b.Price.CompareTo(a.Price)); // 降序
        _asks.Sort((a, b) => a.Price.CompareTo(b.Price)); // 升序
    }
}
