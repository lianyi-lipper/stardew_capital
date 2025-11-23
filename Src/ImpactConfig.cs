namespace StardewCapital
{
    /// <summary>
    /// 市场冲击系统配置
    /// 对应 Assets/impact_config.json
    /// </summary>
    public class ImpactConfig
    {
        /// <summary>冲击衰减率（每帧）</summary>
        public double DecayRate { get; set; } = 0.95;
        
        /// <summary>移动平均线周期</summary>
        public int MovingAveragePeriod { get; set; } = 20;
        
        /// <summary>剧本切换概率（每日）</summary>
        public double ScenarioSwitchProbability { get; set; } = 0.3;
        
        /// <summary>冲击值上限（金币），null表示不限制</summary>
        public double? MaxImpactClamp { get; set; } = 30.0;
    }
}
