using System;

namespace StardewCapital.Core.Futures.Domain.Market
{
    /// <summary>
    /// 限价订单
    /// 表示订单簿中的单个挂单，可以是玩家订单或NPC虚拟订单。
    /// 
    /// 订单类型：
    /// - 玩家订单：通过 BrokerageService.PlaceLimitOrder() 创建，持久化到存档
    /// - NPC订单：由 OrderBook.GenerateNPCDepth() 生成，提供背景流动性
    /// 
    /// 撮合规则：
    /// - 价格优先：买单价格高者优先成交，卖单价格低者优先成交
    /// - 时间优先：同价格时，先下单者优先成交
    /// </summary>
    public class LimitOrder
    {
        /// <summary>订单唯一标识符（GUID格式）</summary>
        public string OrderId { get; set; }

        /// <summary>合约代码（例如："PARSNIP-SPR-28"）</summary>
        public string Symbol { get; set; }

        /// <summary>是否为玩家订单（true=玩家，false=NPC虚拟深度）</summary>
        public bool IsPlayerOrder { get; set; }

        /// <summary>买卖方向（true=买单/做多，false=卖单/做空）</summary>
        public bool IsBuy { get; set; }

        /// <summary>限价（期望成交的价格）</summary>
        public decimal Price { get; set; }

        /// <summary>原始订单数量</summary>
        public int Quantity { get; set; }

        /// <summary>剩余未成交数量（初始值=Quantity，随撮合递减）</summary>
        public int RemainingQuantity { get; set; }

        /// <summary>下单时间戳（用于实现时间优先规则）</summary>
        public DateTime Timestamp { get; set; }
        
        /// <summary>杠杆倍数（用于计算保证金，默认10x）</summary>
        public int Leverage { get; set; }

        /// <summary>
        /// 创建限价订单
        /// </summary>
        /// <param name="symbol">合约代码</param>
        /// <param name="isBuy">买卖方向</param>
        /// <param name="price">限价</param>
        /// <param name="quantity">数量</param>
        /// <param name="isPlayerOrder">是否玩家订单</param>
        public LimitOrder(string symbol, bool isBuy, decimal price, int quantity, bool isPlayerOrder = false, int leverage = 10)
        {
            OrderId = Guid.NewGuid().ToString();
            Symbol = symbol;
            IsBuy = isBuy;
            Price = price;
            Quantity = quantity;
            RemainingQuantity = quantity;
            IsPlayerOrder = isPlayerOrder;
            Leverage = leverage;
            Timestamp = DateTime.Now;
        }

        /// <summary>
        /// 默认构造函数（用于反序列化）
        /// </summary>
        public LimitOrder()
        {
            OrderId = string.Empty;
            Symbol = string.Empty;
            Timestamp = DateTime.Now;
        }

        /// <summary>
        /// 检查订单是否已完全成交
        /// </summary>
        public bool IsFullyFilled => RemainingQuantity == 0;

        /// <summary>
        /// 计算已成交数量
        /// </summary>
        public int FilledQuantity => Quantity - RemainingQuantity;
    }
}

