// ============================================================================
// 星露谷资本 (Stardew Capital)
// 模块：市场剧本系统
// 作者：Stardew Capital Team
// 用途：定义市场情绪/剧本类型和参数配置（用于模型五：市场冲击系统）
// ============================================================================

namespace StardewCapital.Core.Futures.Domain.Market
{
    /// <summary>
    /// 市场剧本类型枚举
    /// 
    /// 含义：
    /// 每个剧本代表一种市场参与者行为模式，通过调整 k_smart、k_trend、k_fomo 系数
    /// 来模拟不同的市场心理状态和价格演化特征。
    /// 
    /// 基于期货.md中的模型五设计
    /// </summary>
    public enum ScenarioType
    {
        /// <summary>
        /// 死水一潭 (Liquidity Trap)
        /// 特征：k_smart极高，k_fomo=0
        /// 表现：价格被强力锚定在基本面，任何偏离都会快速回归
        /// 玩家体验：试图操纵价格极其困难，感觉"市场像焊死了一样"
        /// </summary>
        DeadMarket,

        /// <summary>
        /// 非理性繁荣 (Irrational Exuberance)
        /// 特征：k_smart=0，k_fomo极高
        /// 表现：理性消失，情绪主导，微小波动被极度放大
        /// 玩家体验：少量买入即可引发壮观泡沫，价格脱离地心引力
        /// </summary>
        IrrationalExuberance,

        /// <summary>
        /// 恐慌踩踏 (Panic Selling)
        /// 特征：下跌敏感度不对称（k_down > k_up），情绪放大
        /// 表现：任何卖压都会引发连锁崩盘，买盘难以托住价格
        /// 玩家体验：卖出导致暴跌，试图护盘却瞬间被抛压淹没
        /// </summary>
        PanicSelling,

        /// <summary>
        /// 轧空风暴 (Short Squeeze)
        /// 特征：k_smart翻转为负（聪明钱被迫反向操作）
        /// 表现：价格明显高估，但空头爆仓导致二次加速拉升
        /// 玩家体验：违反直觉的价格暴涨，"空头不死，多头不止"
        /// </summary>
        ShortSqueeze
    }

    /// <summary>
    /// 市场剧本参数配置
    /// 
    /// 用途：
    /// 定义每个剧本下的三类NPC agent的行为强度系数，
    /// 这些系数直接作用于模型五的冲击演化方程：
    /// I(t+1) = I(t)×0.95 + ΔI_Player + ΔI_Smart + ΔI_Trend + ΔI_FOMO
    /// </summary>
    public class ScenarioParameters
    {
        /// <summary>
        /// 聪明钱回归系数 (k_smart)
        /// 
        /// 公式：ΔI_Smart = k_smart × (S_T - P_Final)
        /// 
        /// 含义：
        /// - 正值：聪明钱通过套利使价格回归基本面（稳定器）
        /// - 负值：聪明钱被迫反向操作，加剧偏离（轧空）
        /// - 0值：聪明钱失效，理性消失
        /// 
        /// 建议范围：-0.5 ~ 1.0
        /// </summary>
        public double SmartMoneyStrength { get; set; }

        /// <summary>
        /// 趋势跟随系数 (k_trend)
        /// 
        /// 公式：ΔI_Trend = k_trend × sign(P_Final - MA)
        /// 
        /// 含义：
        /// 技术派根据均线判断趋势，价格在均线上方则追涨，下方则杀跌
        /// 这是市场产生"惯性"的主要来源
        /// 
        /// 建议范围：0 ~ 0.5
        /// </summary>
        public double TrendFollowerStrength { get; set; }

        /// <summary>
        /// FOMO情绪系数 (k_fomo)
        /// 
        /// 公式：ΔI_FOMO = k_fomo × (I(t) - I(t-1))
        /// 
        /// 含义：
        /// 韭菜对"价格变化的加速度"最敏感，形成正反馈循环
        /// - 刚才涨了 → 跟风买入 → 继续推高
        /// - 刚才跌了 → 恐慌卖出 → 继续砸盘
        /// 
        /// 建议范围：0 ~ 1.0
        /// </summary>
        public double FOMOStrength { get; set; }

        /// <summary>
        /// 下跌不对称系数（仅用于恐慌踩踏剧本）
        /// 
        /// 含义：
        /// 当价格下跌时，FOMOStrength 乘以此系数
        /// 模拟"涨时慢，跌时快"的市场心理
        /// 
        /// 示例：asymmetricDown = 1.5 表示下跌时情绪放大1.5倍
        /// 默认值：1.0（无不对称）
        /// </summary>
        public double AsymmetricDown { get; set; } = 1.0;

        /// <summary>
        /// 剧本中文描述（用于UI显示）
        /// 
        /// 示例："市场交投清淡，价格被稳稳锁死"
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// 默认构造函数
        /// </summary>
        public ScenarioParameters()
        {
        }

        /// <summary>
        /// 完整构造函数
        /// </summary>
        public ScenarioParameters(
            double smartMoneyStrength,
            double trendFollowerStrength,
            double fomoStrength,
            string description,
            double asymmetricDown = 1.0)
        {
            SmartMoneyStrength = smartMoneyStrength;
            TrendFollowerStrength = trendFollowerStrength;
            FOMOStrength = fomoStrength;
            Description = description;
            AsymmetricDown = asymmetricDown;
        }
    }
}

