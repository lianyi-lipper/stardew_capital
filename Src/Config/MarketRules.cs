using System.Collections.Generic;

namespace StardewCapital.Config
{
    /// <summary>
    /// 市场规则配置类
    /// 对应 Assets/market_rules.json
    /// 包含所有核心经济参数和数学常数
    /// </summary>
    public class MarketRules
    {
        /// <summary>
        /// 宏观经济参数
        /// </summary>
        public MacroConfig Macro { get; set; } = new();

        /// <summary>
        /// 市场微观结构参数（冲击系统）
        /// </summary>
        public MarketMicrostructureConfig MarketMicrostructure { get; set; } = new();

        /// <summary>
        /// 盘中新闻配置
        /// </summary>
        public IntradayNewsConfig IntradayNews { get; set; } = new();

        /// <summary>
        /// 熔断机制配置
        /// </summary>
        public CircuitBreakerConfig CircuitBreaker { get; set; } = new();

        /// <summary>
        /// 具体金融工具配置
        /// </summary>
        public InstrumentConfig Instruments { get; set; } = new();
    }

    public class MacroConfig
    {
        /// <summary>
        /// 无风险利率（每日）
        /// </summary>
        public double RiskFreeRate { get; set; } = 0.002;
    }

    public class MarketMicrostructureConfig
    {
        /// <summary>
        /// 冲击衰减率（每帧）
        /// </summary>
        public double DecayRate { get; set; } = 0.95;

        /// <summary>
        /// 移动平均线周期
        /// </summary>
        public int MovingAveragePeriod { get; set; } = 20;

        /// <summary>
        /// 剧本切换概率（每日）
        /// </summary>
        public double ScenarioSwitchProbability { get; set; } = 0.3;

        /// <summary>
        /// 冲击值上限
        /// </summary>
        public double MaxImpactClamp { get; set; } = 30.0;

        /// <summary>
        /// 市场剧本配置
        /// </summary>
        public Dictionary<string, ScenarioData> Scenarios { get; set; } = new();
    }

    /// <summary>
    /// 盘中突发新闻配置
    /// </summary>
    public class IntradayNewsConfig
    {
        /// <summary>
        /// 是否启用盘中突发新闻
        /// </summary>
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// 盘中新闻触发概率（每次检查时）
        /// 建议值: 0.001 表示每次检查有 0.1% 的概率触发
        /// </summary>
        public double TriggerProbability { get; set; } = 0.001;

        /// <summary>
        /// 盘中新闻检查间隔（ticks）
        /// 默认值: 300 ticks ≈ 5秒（以 UPDATE_INTERVAL_TICKS=60 为基准）
        /// </summary>
        public int CheckIntervalTicks { get; set; } = 300;

        /// <summary>
        /// 最小新闻触发间隔（ticks）- 防止频繁触发
        /// 默认值: 1800 ticks ≈ 30秒
        /// </summary>
        public int MinNewsIntervalTicks { get; set; } = 1800;
    }

    /// <summary>
    /// 熔断机制配置
    /// </summary>
    public class CircuitBreakerConfig
    {
        /// <summary>
        /// 是否启用熔断机制
        /// </summary>
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// 触发熔断的时间阈值（timeRatio）
        /// 默认: 0.95 表示剩余时间 < 5%
        /// </summary>
        public double TimeThreshold { get; set; } = 0.95;

        /// <summary>
        /// 单日最大涨跌幅（金币）
        /// </summary>
        public double MaxMove { get; set; } = 15.0;
    }

    /// <summary>
    /// 剧本数据结构
    /// </summary>
    public class ScenarioData
    {
        public double SmartMoneyStrength { get; set; }
        public double TrendFollowerStrength { get; set; }
        public double FomoStrength { get; set; }
        public double AsymmetricDown { get; set; } = 1.0;
        public string Description { get; set; } = "";
    }

    public class InstrumentConfig
    {
        public FuturesConfig Futures { get; set; } = new();
        
        // Future extensions
        // public OptionsConfig Options { get; set; } = new();
    }

    public class FuturesConfig
    {
        /// <summary>
        /// 仓储成本（每日）
        /// </summary>
        public double StorageCost { get; set; } = 0.005;

        /// <summary>
        /// 基础便利收益率（每日）
        /// </summary>
        public double BaseConvenienceYield { get; set; } = 0.001;
    }
}
