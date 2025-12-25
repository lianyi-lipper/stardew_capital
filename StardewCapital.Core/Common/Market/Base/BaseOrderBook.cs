// =====================================================================
// 文件：BaseOrderBook.cs
// 用途：通用订单簿基类，处理所有"枯燥"的列表管理工作。
//       子类只需实现特定的验证逻辑（如保证金检查）。
// =====================================================================

using StardewCapital.Core.Common.Market.Interfaces;
using StardewCapital.Core.Common.Market.Models;

namespace StardewCapital.Core.Common.Market.Base;

/// <summary>
/// 通用订单簿基类。
/// 处理订单存储、排序、撮合调用等通用逻辑。
/// 子类通过重写 ValidateOrder 方法添加特定验证。
/// </summary>
/// <typeparam name="TAsset">可交易资产的类型</typeparam>
public abstract class BaseOrderBook<TAsset> : IOrderBook<TAsset> 
    where TAsset : ITradable
{
    // 核心存储：所有市场都需要
    protected readonly List<Order> _bids = new();  // 买单（按价格降序）
    protected readonly List<Order> _asks = new();  // 卖单（按价格升序）
    protected readonly IMatchingEngine _engine;
    
    /// <summary>
    /// 订单簿对应的资产。
    /// </summary>
    public TAsset Asset { get; }
    
    /// <summary>
    /// 最优买价（最高买单价格）。
    /// </summary>
    public double? BestBid => _bids.Count > 0 ? _bids[0].Price : null;
    
    /// <summary>
    /// 最优卖价（最低卖单价格）。
    /// </summary>
    public double? BestAsk => _asks.Count > 0 ? _asks[0].Price : null;
    
    /// <summary>
    /// 中间价 = (最优买价 + 最优卖价) / 2。
    /// </summary>
    public double? MidPrice => BestBid.HasValue && BestAsk.HasValue 
        ? (BestBid.Value + BestAsk.Value) / 2.0 
        : null;
    
    /// <summary>
    /// 买卖价差 = 最优卖价 - 最优买价。
    /// </summary>
    public double? Spread => BestBid.HasValue && BestAsk.HasValue 
        ? BestAsk.Value - BestBid.Value 
        : null;
    
    protected BaseOrderBook(TAsset asset, IMatchingEngine engine)
    {
        Asset = asset;
        _engine = engine;
    }
    
    /// <summary>
    /// 在订单簿中挂单。
    /// </summary>
    public virtual TradeResult PlaceOrder(Order order)
    {
        try
        {
            // 1. 预检查 - 留给子类扩展
            ValidateOrder(order);
            
            // 2. 尝试撮合
            var trades = _engine.Match(order, _bids, _asks);
            
            // 3. 如果订单未完全成交且是限价单，则入账簿
            if (order.RemainingQuantity > 0 && order.Type == OrderType.Limit)
            {
                InsertOrder(order);
            }
            
            // 4. 构建结果
            return BuildTradeResult(order, trades);
        }
        catch (Exception ex)
        {
            return TradeResult.Failed(ex.Message, order.Symbol, order.Side);
        }
    }
    
    /// <summary>
    /// 立即执行市价单。
    /// </summary>
    public TradeResult ExecuteMarketOrder(OrderSide side, int quantity)
    {
        var order = side == OrderSide.Buy
            ? Order.MarketBuy(Asset.Symbol, quantity)
            : Order.MarketSell(Asset.Symbol, quantity);
        
        return PlaceOrder(order);
    }
    
    /// <summary>
    /// 根据订单ID撤单。
    /// </summary>
    public bool CancelOrder(long orderId)
    {
        // 在买单中查找
        var bidOrder = _bids.FirstOrDefault(o => o.OrderId == orderId);
        if (bidOrder != null)
        {
            bidOrder.Cancel();
            _bids.Remove(bidOrder);
            return true;
        }
        
        // 在卖单中查找
        var askOrder = _asks.FirstOrDefault(o => o.OrderId == orderId);
        if (askOrder != null)
        {
            askOrder.Cancel();
            _asks.Remove(askOrder);
            return true;
        }
        
        return false;
    }
    
    /// <summary>
    /// 获取前N档买单价格档位。
    /// </summary>
    public IReadOnlyList<PriceLevel> GetBids(int depth)
    {
        return AggregatePriceLevels(_bids, depth);
    }
    
    /// <summary>
    /// 获取前N档卖单价格档位。
    /// </summary>
    public IReadOnlyList<PriceLevel> GetAsks(int depth)
    {
        return AggregatePriceLevels(_asks, depth);
    }
    
    /// <summary>
    /// 获取某交易者的所有活跃订单。
    /// </summary>
    public IReadOnlyList<Order> GetOrdersByTrader(string traderId)
    {
        return _bids.Concat(_asks)
            .Where(o => o.TraderId == traderId && o.Status == OrderStatus.Pending)
            .ToList();
    }
    
    /// <summary>
    /// 钩子方法：强制子类实现特定的检查逻辑。
    /// </summary>
    /// <param name="order">待验证的订单</param>
    /// <exception cref="InvalidOperationException">验证失败时抛出</exception>
    protected abstract void ValidateOrder(Order order);
    
    /// <summary>
    /// 将订单插入到正确的位置（保持排序）。
    /// </summary>
    private void InsertOrder(Order order)
    {
        if (order.Side == OrderSide.Buy)
        {
            // 买单按价格降序（高价优先）
            int index = _bids.FindIndex(o => o.Price < order.Price);
            if (index < 0) index = _bids.Count;
            _bids.Insert(index, order);
        }
        else
        {
            // 卖单按价格升序（低价优先）
            int index = _asks.FindIndex(o => o.Price > order.Price);
            if (index < 0) index = _asks.Count;
            _asks.Insert(index, order);
        }
    }
    
    /// <summary>
    /// 聚合订单为价格档位。
    /// </summary>
    private List<PriceLevel> AggregatePriceLevels(List<Order> orders, int depth)
    {
        return orders
            .GroupBy(o => o.Price)
            .Take(depth)
            .Select(g => new PriceLevel
            {
                Price = g.Key,
                TotalQuantity = g.Sum(o => o.RemainingQuantity),
                OrderCount = g.Count()
            })
            .ToList();
    }
    
    /// <summary>
    /// 构建交易结果。
    /// </summary>
    private TradeResult BuildTradeResult(Order order, IList<Trade> trades)
    {
        int filledQty = trades.Sum(t => t.Quantity);
        double avgPrice = filledQty > 0 
            ? trades.Sum(t => t.Price * t.Quantity) / filledQty 
            : 0;
        
        // 计算滑点（市价单相对于下单时的最优价）
        double slippage = 0;
        if (order.Type == OrderType.Market && filledQty > 0)
        {
            double expectedPrice = order.Side == OrderSide.Buy 
                ? (BestAsk ?? avgPrice) 
                : (BestBid ?? avgPrice);
            slippage = System.Math.Abs(avgPrice - expectedPrice);
        }
        
        return new TradeResult
        {
            Success = filledQty > 0 || order.Type == OrderType.Limit,
            Symbol = order.Symbol,
            Side = order.Side,
            RequestedQuantity = order.Quantity,
            FilledQuantity = filledQty,
            AveragePrice = avgPrice,
            Slippage = slippage,
            Trades = trades.ToList()
        };
    }
}
