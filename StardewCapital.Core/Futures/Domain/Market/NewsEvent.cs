// ============================================================================
// 星露谷资本 (Stardew Capital)
// 模块：新闻事件数据模型
// 作者：Stardew Capital Team
// 用途：定义影响市场供需关系的新闻事件
// ============================================================================

using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace StardewCapital.Core.Futures.Domain.Market
{
    /// <summary>
    /// 新闻事件类型枚举
    /// 定义所有可能影响商品价格的随机事件类型
    /// </summary>
    public enum NewsType
    {
        /// <summary>害虫危机 - 导致作物减产（供给下降）</summary>
        PestCrisis,
        
        /// <summary>大丰收 - 作物产量激增（供给上升）</summary>
        BumperHarvest,
        
        /// <summary>Zuzu市大订单 - 外部需求激增（需求上升）</summary>
        ZuzuCityOrder,
        
        /// <summary>干旱灾害 - 作物减产（供给下降）</summary>
        Drought,
        
        /// <summary>洪水灾害 - 作物减产（供给下降）</summary>
        Flood,
        
        /// <summary>节日庆典 - 消费增加（需求上升）</summary>
        Festival,
        
        /// <summary>市长推广 - 政策支持增加需求（需求上升）</summary>
        MayorPromotion,
        
        /// <summary>储存腐烂 - 已有库存损失（供给下降）</summary>
        StorageSpoilage
    }

    /// <summary>
    /// 数值影响参数
    /// </summary>
    public class NewsImpact
    {
        /// <summary>对需求的直接影响值</summary>
        [JsonPropertyName("demand_impact")]
        public double DemandImpact { get; set; }
        
        /// <summary>对供给的直接影响值</summary>
        [JsonPropertyName("supply_impact")]
        public double SupplyImpact { get; set; }
        
        /// <summary>价格乘数（直接乘以基础价格）</summary>
        [JsonPropertyName("price_multiplier")]
        public double PriceMultiplier { get; set; } = 1.0;
        
        /// <summary>市场信心影响（影响价格波动率和交易活跃度）</summary>
        [JsonPropertyName("confidence_impact")]
        public double ConfidenceImpact { get; set; } = 0.0;
        
        /// <summary>波动率影响（改变价格的日间波动幅度）</summary>
        [JsonPropertyName("volatility_impact")]
        public double VolatilityImpact { get; set; } = 0.0;
    }

    /// <summary>
    /// 作用范围参数
    /// </summary>
    public class NewsScope
    {
        /// <summary>受影响的特定物品列表</summary>
        [JsonPropertyName("affected_items")]
        public List<string> AffectedItems { get; set; } = new();
        
        /// <summary>受影响的物品类别</summary>
        [JsonPropertyName("affected_categories")]
        public List<string> AffectedCategories { get; set; } = new();
        
        /// <summary>是否为全局影响</summary>
        [JsonPropertyName("is_global")]
        public bool IsGlobal { get; set; } = false;
        
        /// <summary>受影响的地域范围</summary>
        [JsonPropertyName("regions")]
        public List<string> Regions { get; set; } = new();
    }

    /// <summary>
    /// 时间参数
    /// </summary>
    public class NewsTiming
    {
        /// <summary>新闻公告日</summary>
        [JsonPropertyName("announcement_day")]
        public int AnnouncementDay { get; set; }
        
        /// <summary>新闻有效期间 [开始日, 结束日]</summary>
        [JsonPropertyName("effective_days")]
        public int[] EffectiveDays { get; set; } = new int[2];
        
        /// <summary>检查新闻在指定日期是否生效</summary>
        public bool IsEffectiveOn(int day)
        {
            return day >= EffectiveDays[0] && day <= EffectiveDays[1];
        }
    }

    /// <summary>
    /// 条件参数
    /// </summary>
    public class NewsConditions
    {
        /// <summary>新闻发生概率 (0.0-1.0)</summary>
        [JsonPropertyName("probability")]
        public double Probability { get; set; } = 1.0;
        
        /// <summary>前置条件列表</summary>
        [JsonPropertyName("prerequisites")]
        public List<string> Prerequisites { get; set; } = new();
        
        /// <summary>影响值的随机范围 [最小值, 最大值]</summary>
        [JsonPropertyName("random_range")]
        public double[] RandomRange { get; set; } = new double[] { 0, 0 };
    }

    /// <summary>
    /// 新闻事件数据模型（完整版）
    /// 支持JSON配置驱动的新闻系统
    /// </summary>
    public class NewsEvent
    {
        // ========== A. 标识参数 ==========
        
        /// <summary>唯一标识符</summary>
        public string Id { get; set; }
        
        /// <summary>新闻数据格式版本</summary>
        public string Version { get; set; } = "1.0";
        
        /// <summary>事件发生的游戏日期（绝对日期）</summary>
        public int Day { get; set; }
        
        /// <summary>新闻标题</summary>
        public string Title { get; set; } = string.Empty;
        
        /// <summary>新闻描述文本</summary>
        public string Description { get; set; } = string.Empty;
        
        /// <summary>新闻严重程度：low | medium | high | critical</summary>
        public string Severity { get; set; } = "medium";
        
        /// <summary>新闻类型</summary>
        public NewsType Type { get; set; }

        // ========== B. 数值影响参数 ==========
        
        public NewsImpact Impact { get; set; } = new();

        // ========== C. 作用范围参数 ==========
        
        public NewsScope Scope { get; set; } = new();

        // ========== D. 时间参数 ==========
        
        public NewsTiming Timing { get; set; } = new();

        // ========== E. 条件参数 ==========
        
        public NewsConditions Conditions { get; set; } = new();

        /// <summary>
        /// 默认构造函数
        /// </summary>
        public NewsEvent()
        {
            Id = System.Guid.NewGuid().ToString();
        }

        /// <summary>
        /// 获取事件的总影响方向
        /// </summary>
        /// <returns>
        /// "利好"：综合影响使价格上涨（需求增加或供给减少）
        /// "利空"：综合影响使价格下跌（需求减少或供给增加）
        /// "中性"：影响相互抵消
        /// </returns>
        public string GetImpactDirection()
        {
            // 需求增加或供给减少 => 价格上涨 => 利好
            // 需求减少或供给增加 => 价格下跌 => 利空
            double netImpact = Impact.DemandImpact - Impact.SupplyImpact;
            
            if (netImpact > 100) return "利好";
            if (netImpact < -100) return "利空";
            return "中性";
        }

        /// <summary>
        /// 获取严重程度对应的数值 (1-5)
        /// </summary>
        public int GetSeverityLevel()
        {
            return Severity?.ToLower() switch
            {
                "low" => 2,
                "medium" => 3,
                "high" => 4,
                "critical" => 5,
                _ => 3
            };
        }
    }
}

