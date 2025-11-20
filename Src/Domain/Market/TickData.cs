using System;

namespace StardewCapital.Domain.Market
{
    /// <summary>
    /// Tick数据（K线数据点）
    /// 代表价格历史记录中的单个数据点，用于绘制K线图。
    /// 
    /// 标准K线要素：
    /// - Open（开盘价）：该时间段的第一个价格
    /// - High（最高价）：该时间段的最高价格
    /// - Low（最低价）：该时间段的最低价格
    /// - Close（收盘价）：该时间段的最后一个价格
    /// - Volume（成交量）：该时间段的交易量
    /// </summary>
    public class TickData
    {
        /// <summary>真实时间戳（用于记录）</summary>
        public DateTime Timestamp { get; set; }
        
        /// <summary>游戏内日期（第几天）</summary>
        public int GameDay { get; set; }
        
        /// <summary>游戏内时间（例：600表示早上6点）</summary>
        public int GameTime { get; set; }
        
        /// <summary>开盘价</summary>
        public double Open { get; set; }
        
        /// <summary>最高价</summary>
        public double High { get; set; }
        
        /// <summary>最低价</summary>
        public double Low { get; set; }
        
        /// <summary>收盘价</summary>
        public double Close { get; set; }
        
        /// <summary>成交量</summary>
        public long Volume { get; set; }

        /// <summary>
        /// 创建新的Tick数据点
        /// 初始化时，开盘价=最高价=最低价=收盘价
        /// </summary>
        /// <param name="gameDay">游戏日期</param>
        /// <param name="gameTime">游戏时间</param>
        /// <param name="price">初始价格</param>
        public TickData(int gameDay, int gameTime, double price)
        {
            GameDay = gameDay;
            GameTime = gameTime;
            Open = price;
            High = price;
            Low = price;
            Close = price;
            Volume = 0;
        }

        /// <summary>
        /// 更新当前Tick的价格数据
        /// 自动更新最高价、最低价和收盘价
        /// </summary>
        /// <param name="newPrice">新的价格数据</param>
        public void Update(double newPrice)
        {
            Close = newPrice;
            if (newPrice > High) High = newPrice;
            if (newPrice < Low) Low = newPrice;
        }
    }
}
