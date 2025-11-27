namespace StardewCapital.Config
{
    /// <summary>
    /// 待生效的配置变更
    /// 用于存储玩家修改的配置，在下一个季度开始时生效
    /// </summary>
    public class PendingConfigChange
    {
        /// <summary>待生效的开盘时间</summary>
        public int OpeningTime { get; set; }
        
        /// <summary>待生效的收盘时间</summary>
        public int ClosingTime { get; set; }
        
        /// <summary>是否有待生效的配置</summary>
        public bool HasPendingChanges { get; set; }
        
        /// <summary>
        /// 从 ModConfig 应用配置
        /// </summary>
        public void ApplyFrom(ModConfig config)
        {
            OpeningTime = config.OpeningTime;
            ClosingTime = config.ClosingTime;
            HasPendingChanges = true;
        }
        
        /// <summary>
        /// 清除待生效配置（已应用后）
        /// </summary>
        public void Clear()
        {
            HasPendingChanges = false;
        }
    }
}
