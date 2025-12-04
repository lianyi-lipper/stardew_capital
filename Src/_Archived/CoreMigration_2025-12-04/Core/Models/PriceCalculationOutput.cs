// ============================================================================
// 星露谷资本 (Stardew Capital)
// 模块：价格计算输出模型
// 作者：Stardew Capital Team
// 用途：定义独立价格计算器的输出结果
// ============================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using StardewCapital.Core.Futures.Domain.Market;
using StardewCapital.Core.Futures.Domain.Market.MarketState;

namespace StardewCapital.Core.Models
{
    /// <summary>
    /// 价格计算器的输出结果
    /// 包含完整季度的价格轨迹和新闻事件
    /// </summary>
    public class PriceCalculationOutput
    {
        // ========== 价格数据 ==========
        
        /// <summary>
        /// 影子价格数组（整个季度的日内价格）
        /// 长度 = TotalDays * StepsPerDay
        /// 例如：28天 * 72步/天 = 2016 个数据点
        /// </summary>
        public float[] ShadowPrices { get; set; } = Array.Empty<float>();
        
        /// <summary>
        /// 每日基本面价值数组
        /// 长度 = TotalDays
        /// </summary>
        public float[] FundamentalValues { get; set; } = Array.Empty<float>();
        
        // ========== 新闻事件 ==========
        
        /// <summary>
        /// 整个季度的预定新闻事件列表
        /// 包含盘后新闻和盘中新闻
        /// </summary>
        public List<ScheduledNewsEvent> ScheduledNews { get; set; } = new();
        
        // ========== 元数据 ==========
        
        /// <summary>每天的时间步数</summary>
        public int StepsPerDay { get; set; }
        
        /// <summary>数据生成时间（UTC）</summary>
        public DateTime GeneratedAt { get; set; }
        
        /// <summary>使用的随机种子（如果有）</summary>
        public int? RandomSeed { get; set; }
        
        // ========== 统计信息（计算属性） ==========
        
        /// <summary>最低价格</summary>
        public double MinPrice => ShadowPrices.Length > 0 ? ShadowPrices.Min() : 0;
        
        /// <summary>最高价格</summary>
        public double MaxPrice => ShadowPrices.Length > 0 ? ShadowPrices.Max() : 0;
        
        /// <summary>平均价格</summary>
        public double AvgPrice => ShadowPrices.Length > 0 ? ShadowPrices.Average() : 0;
        
        /// <summary>总数据点数</summary>
        public int TotalDataPoints => ShadowPrices?.Length ?? 0;
        
        /// <summary>总天数</summary>
        public int TotalDays => StepsPerDay > 0 ? TotalDataPoints / StepsPerDay : 0;
        
        /// <summary>开盘价（第一个数据点）</summary>
        public double OpeningPrice => ShadowPrices.Length > 0 ? ShadowPrices[0] : 0;
        
        /// <summary>收盘价（最后一个数据点）</summary>
        public double ClosingPrice => ShadowPrices.Length > 0 ? ShadowPrices[^1] : 0;
        
        /// <summary>总涨跌幅（金币）</summary>
        public double TotalChange => ClosingPrice - OpeningPrice;
        
        /// <summary>总涨跌幅（百分比）</summary>
        public double TotalChangePercent => OpeningPrice > 0 ? (ClosingPrice / OpeningPrice - 1) * 100 : 0;
    }
}

