using System;
using System.Collections.Generic;
using StardewCapital.Core.Futures.Domain.Market;
using StardewCapital.Core.Futures.Domain.Market.MarketState;

namespace StardewCapital.Data.SaveData
{
    /// <summary>
    /// 市场状态存档数据
    /// 包含所有金融产品的预计算状态，可序列化为JSON
    /// </summary>
    [Serializable]
    public class MarketStateSaveData
    {
        /// <summary>当前季节</summary>
        public Season CurrentSeason { get; set; }
        
        /// <summary>当前年份</summary>
        public int CurrentYear { get; set; }
        
        /// <summary>当前日期（1-28）</summary>
        public int CurrentDay { get; set; }
        
        /// <summary>期货市场状态列表</summary>
        public List<FuturesMarketState> FuturesStates { get; set; } = new();
        
        /// <summary>存档时间戳</summary>
        public DateTime SaveTimestamp { get; set; }
        
        /// <summary>版本号（用于兼容性检查）</summary>
        public string Version { get; set; } = "1.0.0";
        
        /// <summary>存档时生效的开盘时间（用于检测游戏外配置修改）</summary>
        public int SavedOpeningTime { get; set; }
        
        /// <summary>存档时生效的收盘时间（用于检测游戏外配置修改）</summary>
        public int SavedClosingTime { get; set; }
        
        /// <summary>
        /// 构造函数
        /// </summary>
        public MarketStateSaveData()
        {
            SaveTimestamp = DateTime.Now;
        }
    }
}

