using System;
using StardewCapital.Domain.Instruments;
using StardewCapital.Domain.Market;
using StardewValley;

namespace StardewCapital.Services.Market
{
    /// <summary>
    /// 市场时间计算工具
    /// 负责各种时间相关的计算
    /// </summary>
    public class MarketTimeCalculator
    {
        /// <summary>
        /// 获取当前游戏季节（转换为 CommodityConfig 的 Season 枚举）
        /// </summary>
        public Domain.Market.Season GetCurrentSeason()
        {
            string currentSeason = Game1.currentSeason;
            
            return currentSeason.ToLower() switch
            {
                "spring" => Domain.Market.Season.Spring,
                "summer" => Domain.Market.Season.Summer,
                "fall" => Domain.Market.Season.Fall,
                "winter" => Domain.Market.Season.Winter,
                _ => Domain.Market.Season.Spring // 默认春季
            };
        }

        /// <summary>
        /// 计算距离交割日的剩余天数
        /// </summary>
        public int CalculateDaysToMaturity(CommodityFutures futures)
        {
            int currentDay = Game1.dayOfMonth;
            int deliveryDay = futures.DeliveryDay;
            
            // 简化计算：假设都在同一季节
            int daysRemaining = deliveryDay - currentDay;
            
            // 如果已经过了交割日或到达交割日，返回1天（最小值）
            return Math.Max(1, daysRemaining);
        }

        /// <summary>
        /// 计算绝对日期（从春季第1天开始计数）
        /// </summary>
        public int GetAbsoluteDay()
        {
            string season = Game1.currentSeason;
            int dayOfMonth = Game1.dayOfMonth;
            
            int seasonIndex = season.ToLower() switch
            {
                "spring" => 0,
                "summer" => 1,
                "fall" => 2,
                "winter" => 3,
                _ => 0
            };
            
            return (seasonIndex * 28) + dayOfMonth;
        }
    }
}
