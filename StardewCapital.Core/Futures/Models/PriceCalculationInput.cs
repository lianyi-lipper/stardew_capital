// ============================================================================
// 星露谷资本 (Stardew Capital)
// 模块：价格计算输入模型
// 作者：Stardew Capital Team
// 用途：定义独立价格计算器的输入参数
// ============================================================================

using System.Collections.Generic;
using StardewCapital.Core.Futures.Config;
using StardewCapital.Core.Futures.Domain.Market;
using StardewCapital.Core.Futures.Data;

namespace StardewCapital.Core.Futures.Models
{
    /// <summary>
    /// 价格计算器的输入参数
    /// 完全独立于游戏API，只包含纯数据
    /// </summary>
    public class PriceCalculationInput
    {
        // ========== 商品基础参数 ==========
        
        /// <summary>商品名称（例如：Strawberry）</summary>
        public string CommodityName { get; set; } = string.Empty;
        
        /// <summary>商品配置（从 commodities.json 加载）</summary>
        public CommodityConfig CommodityConfig { get; set; } = null!;
        
        /// <summary>起始价格（金币）</summary>
        public double StartPrice { get; set; }
        
        /// <summary>当前季节</summary>
        public Season Season { get; set; }
        
        // ========== 时间参数 ==========
        
        /// <summary>总天数（默认28天，一个季度）</summary>
        public int TotalDays { get; set; } = 28;
        
        /// <summary>每天的时间步数（即日内数据点数量）</summary>
        public int StepsPerDay { get; set; }
        
        /// <summary>市场开盘时间（HHMM格式，如 600 代表 6:00）</summary>
        public int OpeningTime { get; set; } = 600;
        
        /// <summary>市场收盘时间（HHMM格式，如 2600 代表 26:00）</summary>
        public int ClosingTime { get; set; } = 2600;
        
        // ========== 新闻配置 ==========
        
        /// <summary>新闻模板列表（从 news_config.json 加载）</summary>
        public List<NewsTemplate> NewsTemplates { get; set; } = new();
        
        // ========== 市场规则 ==========
        
        /// <summary>市场规则配置（从 market_rules.json 加载）</summary>
        public MarketRules MarketRules { get; set; } = null!;
        
        // ========== 波动率参数 ==========
        
        /// <summary>基础波动率（用于日间GBM，默认2%）</summary>
        public double BaseVolatility { get; set; } = 0.02;
        
        /// <summary>日内波动率（用于布朗桥，默认0.5%）</summary>
        public double IntraVolatility { get; set; } = 0.005;
        
        // ========== 可重现性参数 ==========
        
        /// <summary>随机数种子（可选，用于测试可重现性）</summary>
        public int? RandomSeed { get; set; } = null;
    }
}

