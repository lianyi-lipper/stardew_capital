using System.Collections.Generic;
using System.Linq;
using StardewCapital.Domain.Market;
using StardewCapital.Domain.Market.MarketState;
using StardewModdingAPI;

namespace StardewCapital.Services.Market
{
    /// <summary>
    /// 新闻时间表播放器（重构版）
    /// 负责按照预定时间表触发新闻事件
    /// 不再随机生成新闻，而是播放MarketStateManager中的预定新闻
    /// </summary>
    public class NewsSchedulePlayer
    {
        private readonly IMonitor _monitor;
        private readonly MarketStateManager _marketStateManager;
        private readonly MarketTimeCalculator _timeCalculator;

        // 新闻历史和活跃列表（由外部管理）
        private List<NewsEvent> _newsHistory;
        private List<NewsEvent> _activeNewsEffects;

        public NewsSchedulePlayer(
            IMonitor monitor,
            MarketStateManager marketStateManager,
            MarketTimeCalculator timeCalculator)
        {
            _monitor = monitor;
            _marketStateManager = marketStateManager;
            _timeCalculator = timeCalculator;
            _newsHistory = new List<NewsEvent>();
            _activeNewsEffects = new List<NewsEvent>();
        }

        /// <summary>
        /// 设置新闻列表（用于外部注入）
        /// </summary>
        public void SetNewsLists(List<NewsEvent> newsHistory, List<NewsEvent> activeNewsEffects)
        {
            _newsHistory = newsHistory;
            _activeNewsEffects = activeNewsEffects;
        }

        /// <summary>
        /// 检查并触发预定的新闻事件
        /// </summary>
        public void CheckAndTriggerScheduledNews(int currentDay, double currentTimeRatio)
        {
            // 从MarketStateManager获取待触发的事件
            var pendingEvents = _marketStateManager.GetPendingEvents(currentDay, currentTimeRatio);

            if (pendingEvents == null || pendingEvents.Count == 0)
                return;

            foreach (var marketEvent in pendingEvents)
            {
                if (marketEvent.EventType != "News")
                    continue;

                // 应用事件（标记为已触发）
                marketEvent.Apply();

                // 获取对应的ScheduledNewsEvent
                var scheduledNews = FindScheduledNewsEvent(marketEvent.EventId, currentDay);
                if (scheduledNews == null)
                    continue;

                var newsEvent = scheduledNews.Event;

                // 添加到历史和活跃列表
                _newsHistory.Add(newsEvent);
                _activeNewsEffects.Add(newsEvent);

                _monitor.Log(
                    $"[News] Triggered: {newsEvent.Title} ({newsEvent.Scope.AffectedItems.FirstOrDefault() ?? "N/A"}) | " +
                    $"D:{newsEvent.Impact.DemandImpact:+0;-0;0} S:{newsEvent.Impact.SupplyImpact:+0;-0;0}",
                    LogLevel.Info
                );
            }
        }

        /// <summary>
        /// 清理过期新闻
        /// </summary>
        public void CleanupExpiredNews(int currentDay)
        {
            int beforeCount = _activeNewsEffects.Count;
            _activeNewsEffects.RemoveAll(n => !n.Timing.IsEffectiveOn(currentDay));
            int removedCount = beforeCount - _activeNewsEffects.Count;

            if (removedCount > 0)
            {
                _monitor.Log($"[News] Removed {removedCount} expired news from active effects", LogLevel.Info);
            }
        }

        /// <summary>
        /// 新季节开始时清空活跃新闻
        /// </summary>
        public void OnNewSeason()
        {
            _activeNewsEffects.Clear();
            _monitor.Log("[News] New season started, cleared active news effects", LogLevel.Info);
        }

        /// <summary>
        /// 查找对应的ScheduledNewsEvent
        /// </summary>
        private ScheduledNewsEvent? FindScheduledNewsEvent(string eventId, int currentDay)
        {
            // 遍历所有市场状态查找事件
            var allSymbols = GetAllInstrumentSymbols();

            foreach (var symbol in allSymbols)
            {
                var marketState = _marketStateManager.GetMarketState(symbol);
                if (marketState is not Domain.Market.MarketState.FuturesMarketState futuresState)
                    continue;

                var scheduledNews = futuresState.ScheduledNews
                    .FirstOrDefault(n => n.Event.Id == eventId && n.TriggerDay == currentDay);

                if (scheduledNews != null)
                    return scheduledNews;
            }

            return null;
        }

        /// <summary>
        /// 获取所有合约Symbol（临时方法，应该由外部提供）
        /// </summary>
        private List<string> GetAllInstrumentSymbols()
        {
            // TODO: 这应该由外部注入或通过MarketManager获取
            // 暂时返回空列表，实际使用时需要完善
            return new List<string>();
        }
    }
}
