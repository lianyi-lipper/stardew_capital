namespace StardewCapital.Domain.Instruments
{
    /// <summary>
    /// 商品期货合约
    /// 代表基于星露谷农产品的期货合约，具有交割日和保证金机制。
    /// 
    /// 期货特性：
    /// - 标准化合约：明确的交割日期和标的物
    /// - 保证金交易：默认10%保证金，支持10倍杠杆
    /// - 实物交割：到期日需要交割真实的游戏物品
    /// </summary>
    public class CommodityFutures : IInstrument
    {
        /// <summary>合约标识符，格式：商品名-季节-天数 (例：PARSNIP-SPR-28)</summary>
        public string Symbol { get; private set; }
        
        /// <summary>显示名称，例："防风草期货 (春季28号)"</summary>
        public string Name { get; private set; }
        
        /// <summary>商品名称（用于 FundamentalEngine 查询），例："Parsnip"</summary>
        public string CommodityName { get; private set; }
        
        /// <summary>标的物品ID（Stardew Valley的物品ID，例："24"代表防风草）</summary>
        public string UnderlyingItemId { get; private set; }
        
        /// <summary>
        /// 现货价格（S_t），即基本面价值
        /// 由 FundamentalEngine 计算，代表"如果今天现货市场交易"的价格
        /// </summary>
        public double CurrentPrice { get; set; }

        /// <summary>
        /// 期货价格（F_t），基于持有成本模型计算
        /// 由 PriceEngine.CalculateFuturesPrice() 计算
        /// 这是玩家在期货市场实际交易的价格
        /// </summary>
        /// <remarks>
        /// 期货价格与现货价格的关系（基差）：
        /// - Contango (升水): F_t > S_t，持有成本高于便利收益
        /// - Backwardation (贴水): F_t < S_t，便利收益高于持有成本
        /// </remarks>
        public double FuturesPrice { get; set; }

        /// <summary>
        /// 开盘价（当日开盘时的价格）
        /// 用于计算涨跌额和涨跌幅，在每日 OnNewDay 时记录
        /// </summary>
        public double OpenPrice { get; set; }
        
        /// <summary>保证金比例（默认0.1，即10%保证金）</summary>
        public double MarginRatio { get; private set; }

        /// <summary>交割日期（月份中的几号，1-28）</summary>
        public int DeliveryDay { get; private set; }
        
        /// <summary>交割季节（Spring, Summer, Fall, Winter）</summary>
        public string DeliverySeason { get; private set; }

        /// <summary>
        /// 创建商品期货合约
        /// </summary>
        /// <param name="underlyingItemId">标的物品的Stardew Valley ID（例："24"代表防风草）</param>
        /// <param name="name">商品显示名称（例："Parsnip"）</param>
        /// <param name="season">交割季节（Spring/Summer/Fall/Winter）</param>
        /// <param name="deliveryDay">交割日期（月份中的几号，1-28）</param>
        /// <param name="initialPrice">合约初始价格（金币）</param>
        public CommodityFutures(string underlyingItemId, string name, string season, int deliveryDay, double initialPrice)
        {
            UnderlyingItemId = underlyingItemId;
            Name = name;
            CommodityName = name; // 添加：用于FundamentalEngine查询
            DeliverySeason = season;
            DeliveryDay = deliveryDay;
            CurrentPrice = initialPrice;
            FuturesPrice = initialPrice; // 初始值与现货价格相同，后续由PriceEngine计算
            OpenPrice = initialPrice;    // 初始开盘价
            
            // 生成合约代码：商品名-季节(前3字母)-交割日
            // 例：PARSNIP-SPR-28 代表 防风草-春季-28号交割
            Symbol = $"{name.ToUpper().Replace(" ", "")}-{season.Substring(0, 3).ToUpper()}-{deliveryDay}";
            
            // 期货默认10%保证金，即支持10倍杠杆
            MarginRatio = 0.1;
        }
    }
}
