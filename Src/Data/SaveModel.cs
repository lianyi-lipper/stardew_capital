using System.Collections.Generic;
using StardewCapital.Core.Futures.Domain.Account;

namespace StardewCapital.Data
{
    /// <summary>
    /// 存档数据模型
    /// 定义需要持久化的交易账户数据结构。
    /// 
    /// 当前版本保存内容：
    /// - Cash：账户现金余额
    /// - Positions：所有开仓的交易仓位
    /// 
    /// 未来扩展：
    /// - LastPrices：最后已知的市场价格（避免读档后价格跳空）
    /// - HistoricalData：K线历史数据（避免读档后图表重画）
    /// </summary>
    public class SaveModel
    {
        /// <summary>账户现金余额（金币）</summary>
        public decimal Cash { get; set; }
        
        /// <summary>所有开仓的交易仓位</summary>
        public List<Position> Positions { get; set; } = new List<Position>();
        
        // 未来扩展：保存市场历史数据
        // public Dictionary<string, double> LastPrices { get; set; }
    }
}

