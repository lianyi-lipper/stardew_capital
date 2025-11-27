using System;

namespace StardewCapital.Domain.Market
{
    /// <summary>
    /// 预定的新闻事件
    /// 包含触发时间信息的新闻事件
    /// </summary>
    [Serializable]
    public class ScheduledNewsEvent
    {
        /// <summary>新闻事件内容</summary>
        public NewsEvent Event { get; set; } = null!;
        
        /// <summary>触发日期（1-28）</summary>
        public int TriggerDay { get; set; }
        
        /// <summary>
        /// 触发时间比例（0.0-1.0）
        /// </summary>
        /// <remarks>
        /// null = 开盘时生效（盘后新闻）
        /// 0.0 = 开盘时触发
        /// 0.5 = 午盘触发（盘中新闻）
        /// </remarks>
        public double? TriggerTimeRatio { get; set; }
        
        /// <summary>是否已触发</summary>
        public bool HasTriggered { get; set; }
        
        /// <summary>
        /// 判断是否应该在指定时刻触发
        /// </summary>
        public bool ShouldTrigger(int currentDay, double currentTimeRatio)
        {
            if (HasTriggered || TriggerDay != currentDay)
                return false;
            
            // 盘后新闻：在开盘时触发
            if (TriggerTimeRatio == null)
                return currentTimeRatio < 0.01;
            
            // 盘中新闻：在指定时刻触发（容差0.01）
            return Math.Abs(TriggerTimeRatio.Value - currentTimeRatio) < 0.01;
        }
    }
}
