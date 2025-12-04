using System.Collections.Generic;

namespace StardewCapital.Core.Futures.Config
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
        /// 虚拟流量配置
        /// </summary>
        public VirtualFlowConfig VirtualFlow { get; set; } = new();

        /// <summary>
        /// NPC代理配置
        /// </summary>
        public NpcAgentsConfig NpcAgents { get; set; } = new();

        /// <summary>
        /// 影子价格生成配置
        /// </summary>
        public ShadowPricingConfig ShadowPricing { get; set; } = new();

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
        /// 盘中新闻每日发生概率（0.0-1.0）
        /// 每天开盘时检查，决定当天是否可能生成盘中新闻
        /// 建议值: 0.1 表示每天有10%概率发生盘中新闻
        /// </summary>
        public double IntradayNewsProbability { get; set; } = 0.1;

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

    public class VirtualFlowConfig
    {
        public double LiquidityCoefficient { get; set; } = 10.0;
        public int MaxFlowPerTick { get; set; } = 500;
    }

    public class NpcAgentsConfig
    {
        public AgentConfig SmartMoney { get; set; } = new();
        public AgentConfig TrendFollowers { get; set; } = new();
        public AgentConfig FomoTraders { get; set; } = new();
    }

    public class AgentConfig
    {
        public double BaseStrength { get; set; } = 0.05;
        public double Threshold { get; set; } = 0.5;
        public int MovingAveragePeriod { get; set; } = 20;
    }

    /// <summary>
    /// 影子价格生成配置
    /// </summary>
    public class ShadowPricingConfig
    {
        /// <summary>
        /// 波动率参数
        /// </summary>
        public VolatilityConfig Volatility { get; set; } = new();

        /// <summary>
        /// 布朗桥参数
        /// </summary>
        public BrownianBridgeConfig BrownianBridge { get; set; } = new();

        /// <summary>
        /// 时间设置
        /// </summary>
        public TimeSettingsConfig TimeSettings { get; set; } = new();
    }

    /// <summary>
    /// 波动率配置
    /// </summary>
    public class VolatilityConfig
    {
        /// <summary>
        /// 日间波动率（用于GBM）
        /// </summary>
        public double BaseVolatility { get; set; } = 0.02;

        /// <summary>
        /// 日内波动率（用于布朗桥）
        /// </summary>
        public double IntraVolatility { get; set; } = 0.2;

        /// <summary>
        /// 前3天波动率衰减系数
        /// </summary>
        public double EarlyDayDampening { get; set; } = 0.5;
    }

    /// <summary>
    /// 布朗桥配置
    /// </summary>
    public class BrownianBridgeConfig
    {
        /// <summary>
        /// 开盘冲击系数（alpha）
        /// </summary>
        public double OpeningShockAlpha { get; set; } = 2.0;

        /// <summary>
        /// 冲击衰减速度（lambda）
        /// </summary>
        public double ShockDecayLambda { get; set; } = 10.0;

        /// <summary>
        /// 噪声缩放因子（替代 sqrt(timeStep) 以避免步数影响）
        /// </summary>
        public double NoiseScaleFactor { get; set; } = 5.0;
    }

    /// <summary>
    /// 时间设置配置
    /// </summary>
    public class TimeSettingsConfig
    {
        /// <summary>
        /// 季度总天数
        /// </summary>
        public int TotalDays { get; set; } = 28;

        /// <summary>
        /// 市场开盘时间（HHMM格式）
        /// </summary>
        public int OpeningTime { get; set; } = 630;

        /// <summary>
        /// 市场收盘时间（HHMM格式）
        /// </summary>
        public int ClosingTime { get; set; } = 2600;
    }
}

