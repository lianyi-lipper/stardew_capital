using System.Collections.Generic;

namespace StardewCapital.Domain.Market.MarketState
{
    /// <summary>
    /// 市场状态接口
    /// 支持多种金融产品（期货、股票、期权等）
    /// 包含完整的预计算价格轨迹和事件时间表
    /// </summary>
    public interface IMarketState
    {
        /// <summary>产品类型（Futures/Stock/Option）</summary>
        string InstrumentType { get; }
        
        /// <summary>合约标识</summary>
        string Symbol { get; }
        
        /// <summary>季节</summary>
        Season Season { get; }
        
        /// <summary>年份</summary>
        int Year { get; }
        
        /// <summary>
        /// 获取指定时刻的影子价格
        /// </summary>
        /// <param name="day">日期（1-28）</param>
        /// <param name="timeRatio">时间进度（0.0-1.0）</param>
        /// <returns>影子价格</returns>
        double GetPrice(int day, double timeRatio);
        
        /// <summary>
        /// 获取待触发的事件
        /// </summary>
        /// <param name="day">当前日期</param>
        /// <param name="timeRatio">当前时间进度</param>
        /// <returns>应该被触发的事件列表</returns>
        List<IMarketEvent> GetPendingEvents(int day, double timeRatio);
    }
}
