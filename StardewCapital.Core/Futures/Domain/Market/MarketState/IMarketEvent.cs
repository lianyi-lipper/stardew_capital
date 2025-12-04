namespace StardewCapital.Core.Futures.Domain.Market.MarketState
{
    /// <summary>
    /// 市场事件接口
    /// 所有市场事件（新闻、公告、财报等）都实现此接口
    /// </summary>
    public interface IMarketEvent
    {
        /// <summary>事件唯一标识</summary>
        string EventId { get; }
        
        /// <summary>事件类型（News/Earnings/Dividend等）</summary>
        string EventType { get; }
        
        /// <summary>触发优先级（数字越小优先级越高）</summary>
        int Priority { get; }
        
        /// <summary>
        /// 应用事件效果到市场
        /// </summary>
        void Apply();
    }
}

