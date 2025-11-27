using System;
using System.Collections.Generic;
using System.Linq;

namespace StardewCapital.Domain.Market.MarketState
{
    /// <summary>
    /// 期货市场状态
    /// 存储整个季度的预计算数据，可序列化为JSON存档
    /// </summary>
    [Serializable]
    public class FuturesMarketState : IMarketState
    {
        public string InstrumentType => "Futures";
        
        public string Symbol { get; set; } = string.Empty;
        
        public Season Season { get; set; }
        
        public int Year { get; set; }
        
        /// <summary>商品名称</summary>
        public string CommodityName { get; set; } = string.Empty;
        
        /// <summary>交割日</summary>
        public int DeliveryDay { get; set; }
        
        /// <summary>
        /// 影子价格数组（预计算）
        /// 长度 = 28天 × StepsPerDay
        /// </summary>
        public float[] ShadowPrices { get; set; } = Array.Empty<float>();
        
        /// <summary>每天的时间步数（时间分辨率）</summary>
        public int StepsPerDay { get; set; }
        
        /// <summary>新闻事件时间表（预计算）</summary>
        public List<ScheduledNewsEvent> ScheduledNews { get; set; } = new();
        
        /// <summary>基本面价值轨迹（用于分析和调试）</summary>
        public float[] FundamentalValues { get; set; } = Array.Empty<float>();
        
        /// <summary>
        /// 获取指定时刻的影子价格
        /// </summary>
        public double GetPrice(int day, double timeRatio)
        {
            if (ShadowPrices.Length == 0)
                return 0.0;
            
            int dayIndex = day - 1; // 1-based to 0-based
            int stepIndex = (int)(timeRatio * StepsPerDay);
            int globalIndex = dayIndex * StepsPerDay + stepIndex;
            
            // 边界检查
            if (globalIndex < 0)
                globalIndex = 0;
            if (globalIndex >= ShadowPrices.Length)
                globalIndex = ShadowPrices.Length - 1;
            
            return ShadowPrices[globalIndex];
        }
        
        /// <summary>
        /// 获取待触发的事件
        /// </summary>
        public List<IMarketEvent> GetPendingEvents(int day, double timeRatio)
        {
            return ScheduledNews
                .Where(n => n.ShouldTrigger(day, timeRatio))
                .Select(n => new NewsMarketEvent(n))
                .Cast<IMarketEvent>()
                .ToList();
        }
        
        /// <summary>
        /// 获取当日开盘价
        /// </summary>
        public double GetDailyOpen(int day)
        {
            return GetPrice(day, 0.0);
        }
        
        /// <summary>
        /// 获取当日收盘价
        /// </summary>
        public double GetDailyClose(int day)
        {
            return GetPrice(day, 1.0);
        }
    }
    
    /// <summary>
    /// 新闻市场事件包装器
    /// 将 ScheduledNewsEvent 转换为 IMarketEvent
    /// </summary>
    internal class NewsMarketEvent : IMarketEvent
    {
        private readonly ScheduledNewsEvent _scheduledNews;
        
        public NewsMarketEvent(ScheduledNewsEvent scheduledNews)
        {
            _scheduledNews = scheduledNews;
        }
        
        public string EventId => _scheduledNews.Event.Id;
        public string EventType => "News";
        public int Priority => _scheduledNews.Event.GetSeverityLevel();
        
        public void Apply()
        {
            _scheduledNews.HasTriggered = true;
        }
    }
}
