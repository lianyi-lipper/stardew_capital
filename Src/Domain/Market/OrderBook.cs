using System;
using System.Collections.Generic;
using System.Linq;

namespace StardewCapital.Domain.Market
{
    /// <summary>
    /// 订单成交信息（用于事件通知）
    /// </summary>
    public class OrderFillInfo
    {
        public string OrderId { get; set; } = string.Empty;
        public string Symbol { get; set; } = string.Empty;
        public bool IsBuy { get; set; }
        public decimal FillPrice { get; set; }
        public int FillQuantity { get; set; }
        public int Leverage { get; set; }
        public DateTime FillTime { get; set; }
    }

    /// <summary>
    /// L2订单簿
    /// 维护单个期货商品的买卖深度，支持订单插入、撮合、取消。
    /// 
    /// 核心功能：
    /// - 订单管理：插入、撤销、查询玩家订单
    /// - 市价单撮合：计算VWAP和滑点
    /// - NPC深度生成：根据市场剧本提供背景流动性
    /// - 事件通知：玩家订单成交时触发结算流程
    /// 
    /// 数据结构：
    /// - Bids：买盘队列，降序排列（最高买价在索引0）
    /// - Asks：卖盘队列，升序排列（最低卖价在索引0）
    /// </summary>
    public class OrderBook
    {
        /// <summary>合约代码（例如："PARSNIP-SPR-28"）</summary>
        public string Symbol { get; private set; }

        /// <summary>买盘队列（降序：价格从高到低）</summary>
        public List<LimitOrder> Bids { get; private set; }

        /// <summary>卖盘队列（升序：价格从低到高）</summary>
        public List<LimitOrder> Asks { get; private set; }
        
        /// <summary>
        /// 玩家订单成交事件
        /// 当玩家限价单（部分或全部）成交时触发，用于资金结算和仓位建立
        /// </summary>
        public event Action<OrderFillInfo>? OnPlayerOrderFilled;

        /// <summary>
        /// 创建订单簿
        /// </summary>
        /// <param name="symbol">合约代码</param>
        public OrderBook(string symbol)
        {
            Symbol = symbol;
            Bids = new List<LimitOrder>();
            Asks = new List<LimitOrder>();
        }

        /// <summary>
        /// 获取最优买价（买一价）
        /// </summary>
        /// <returns>最高买价，如果买盘为空返回0</returns>
        public decimal GetBestBid()
        {
            return Bids.Count > 0 ? Bids[0].Price : 0;
        }

        /// <summary>
        /// 获取最优卖价（卖一价）
        /// </summary>
        /// <returns>最低卖价，如果卖盘为空返回decimal.MaxValue</returns>
        public decimal GetBestAsk()
        {
            return Asks.Count > 0 ? Asks[0].Price : decimal.MaxValue;
        }

        /// <summary>
        /// 获取盘口中间价（Mid Price）
        /// </summary>
        /// <returns>买一卖一的平均值</returns>
        public decimal GetMidPrice()
        {
            var bestBid = GetBestBid();
            var bestAsk = GetBestAsk();
            
            if (bestBid == 0 && bestAsk == decimal.MaxValue)
                return 0;
            if (bestBid == 0)
                return bestAsk;
            if (bestAsk == decimal.MaxValue)
                return bestBid;
                
            return (bestBid + bestAsk) / 2;
        }

        /// <summary>
        /// 下限价单（Maker模式）
        /// </summary>
        /// <param name="order">待插入的订单</param>
        /// <remarks>
        /// WHY（为什么这样实现）：
        /// 1. 先尝试立即撮合：如果订单价格穿过对手盘最优价，立即作为Taker成交
        /// 2. 未成交部分入册：插入到对应队列并排序
        /// 3. 撮合逻辑在TryImmediateMatch中处理，避免代码重复
        /// </remarks>
        public void PlaceOrder(LimitOrder order)
        {
            // 1. 尝试立即撮合（如果订单价格穿价）
            TryImmediateMatch(order);

            // 2. 剩余部分插入订单簿
            if (order.RemainingQuantity > 0)
            {
                if (order.IsBuy)
                    Bids.Add(order);
                else
                    Asks.Add(order);

                SortOrderBook();
            }
        }

        /// <summary>
        /// 尝试立即撮合订单
        /// </summary>
        /// <param name="order">待撮合的订单</param>
        /// <remarks>
        /// WHY（为什么需要这个方法）：
        /// 限价单如果价格优于对手盘，应立即作为Taker成交。
        /// 例如：当前卖一=42g，玩家下买单43g，应立即以42g成交。
        /// </remarks>
        private void TryImmediateMatch(LimitOrder order)
        {
            var targetQueue = order.IsBuy ? Asks : Bids;

            while (order.RemainingQuantity > 0 && targetQueue.Count > 0)
            {
                var bestOpposite = targetQueue[0];

                // 检查是否能成交（买单价>=卖单价，或卖单价<=买单价）
                bool canMatch = order.IsBuy 
                    ? order.Price >= bestOpposite.Price 
                    : order.Price <= bestOpposite.Price;

                if (!canMatch)
                    break;

                // 计算成交数量
                int fillQty = Math.Min(order.RemainingQuantity, bestOpposite.RemainingQuantity);

                // 更新剩余数量
                order.RemainingQuantity -= fillQty;
                bestOpposite.RemainingQuantity -= fillQty;

                // 如果对手单完全成交，从队列移除
                if (bestOpposite.RemainingQuantity == 0)
                    targetQueue.RemoveAt(0);
            }
        }

        /// <summary>
        /// 执行市价单（Taker模式）
        /// </summary>
        /// <param name="isBuy">买卖方向（true=买入，false=卖出）</param>
        /// <param name="quantity">数量</param>
        /// <returns>成交均价(VWAP)和滑点</returns>
        /// <remarks>
        /// WHY（为什么返回VWAP和滑点）：
        /// - VWAP：玩家实际支付/收到的平均价格
        /// - 滑点：相对于初始最优价的恶化程度，用于UI提示和日志
        /// 
        /// 滑点计算：
        /// - 买入滑点 = VWAP - 初始卖一价（正数=价格恶化）
        /// - 卖出滑点 = 初始买一价 - VWAP（正数=价格恶化）
        /// </remarks>
        public (decimal vwap, decimal slippage) ExecuteMarketOrder(bool isBuy, int quantity)
        {
            var targetQueue = isBuy ? Asks : Bids;

            if (targetQueue.Count == 0)
                return (0, 0); // 无深度，无法成交

            decimal initialBestPrice = targetQueue[0].Price;
            decimal totalCost = 0;
            int remainingQty = quantity;

            // 逐层消耗对手盘
            foreach (var order in targetQueue.ToList())
            {
                int fillQty = Math.Min(remainingQty, order.RemainingQuantity);
                totalCost += fillQty * order.Price;
                remainingQty -= fillQty;

                // ========== 🔥 触发玩家订单成交事件（用于资金结算） ==========
                if (order.IsPlayerOrder && fillQty > 0)
                {
                    OnPlayerOrderFilled?.Invoke(new OrderFillInfo
                    {
                        OrderId = order.OrderId,
                        Symbol = order.Symbol,
                        IsBuy = order.IsBuy,
                        FillPrice = order.Price,
                        FillQuantity = fillQty,
                        Leverage = order.Leverage,
                        FillTime = DateTime.Now
                    });
                }

                order.RemainingQuantity -= fillQty;
                if (order.RemainingQuantity == 0)
                    targetQueue.Remove(order);

                if (remainingQty == 0)
                    break;
            }

            // 计算VWAP
            int filledQty = quantity - remainingQty;
            decimal vwap = filledQty > 0 ? totalCost / filledQty : 0;

            // 计算滑点（买入时VWAP>初始价为正滑点，卖出时初始价>VWAP为正滑点）
            decimal slippage = isBuy 
                ? (vwap - initialBestPrice) 
                : (initialBestPrice - vwap);

            return (vwap, slippage);
        }

        /// <summary>
        /// 撤销订单
        /// </summary>
        /// <param name="orderId">订单ID</param>
        /// <returns>是否成功撤销</returns>
        public bool CancelOrder(string orderId)
        {
            // 在买盘中查找
            var bidOrder = Bids.FirstOrDefault(o => o.OrderId == orderId);
            if (bidOrder != null)
            {
                Bids.Remove(bidOrder);
                return true;
            }

            // 在卖盘中查找
            var askOrder = Asks.FirstOrDefault(o => o.OrderId == orderId);
            if (askOrder != null)
            {
                Asks.Remove(askOrder);
                return true;
            }

            return false;
        }

        /// <summary>
        /// 生成NPC虚拟深度（基于流动性系数）
        /// </summary>
        /// <param name="midPrice">盘口中间价（公式计算的上帝价格）</param>
        /// <param name="scenarioType">市场剧本类型</param>
        /// <param name="liquiditySensitivity">流动性敏感度 λ（来自 CommodityConfig.LiquiditySensitivity）</param>
        /// <remarks>
        /// WHY（为什么需要虚拟深度）：
        /// 真实市场有做市商提供流动性，我们用NPC虚拟订单模拟这一机制。
        /// 不同剧本下，NPC深度分布不同，影响玩家的交易体验。
        /// 
        /// WHY（为什么订单密度要与流动性系数一致）：
        /// Impact公式：ΔI = λ × Q（买入Q个商品，价格上涨 λQ）
        /// 订单簿滑点：如果1g价格区间内有N个订单，买入N个价格上涨1g
        /// 一致性要求：N ≈ 1/λ（流动性越差，λ越大，订单越稀疏）
        /// 
        /// 剧本特征：
        /// - DeadMarket（死水一潭）：买卖盘密集且对称，价格难以波动
        /// - IrrationalExuberance（非理性繁荣）：买盘密集，卖盘稀疏，价格易涨
        /// - PanicSelling（恐慌踩踏）：卖盘密集，买盘稀疏，价格易跌
        /// - ShortSqueeze（轧空风暴）：买盘极度密集，卖盘几乎消失
        /// </remarks>
        public void GenerateNPCDepth(decimal midPrice, string scenarioType, double liquiditySensitivity)
        {
            // 清除现有NPC订单（保留玩家订单）
            ClearNPCOrders();

            // 生成5档深度
            for (int level = 1; level <= 5; level++)
            {
                decimal bidPrice = midPrice * (1 - level * 0.01m);  // -1%, -2%, -3%, -4%, -5%
                decimal askPrice = midPrice * (1 + level * 0.01m);  // +1%, +2%, +3%, +4%, +5%

                // 🔥 核心：根据流动性系数计算订单数量
                // 价格间隔 = midPrice × 1% (例如 100g × 0.01 = 1g)
                decimal priceGap = midPrice * 0.01m;
                
                // 单位价格区间内应放置的订单数量 = priceGap / λ
                // 例如：λ = 0.5（流动性差），1g区间内放 2 个订单
                //      λ = 0.01（流动性好），1g区间内放 100 个订单
                int baseQty = Math.Max(1, (int)((double)priceGap / liquiditySensitivity));

                // 应用剧本调整（微调买卖盘分布）
                int bidQty = ApplyScenarioMultiplier(baseQty, scenarioType, isBuy: true, level);
                int askQty = ApplyScenarioMultiplier(baseQty, scenarioType, isBuy: false, level);

                if (bidQty > 0)
                    Bids.Add(new LimitOrder(Symbol, true, bidPrice, bidQty, isPlayerOrder: false));
                if (askQty > 0)
                    Asks.Add(new LimitOrder(Symbol, false, askPrice, askQty, isPlayerOrder: false));
            }

            SortOrderBook();
        }

        /// <summary>
        /// 应用剧本乘数（微调深度分布）
        /// </summary>
        /// <param name="baseQty">基础订单数量（由流动性系数计算）</param>
        /// <param name="scenarioType">剧本类型</param>
        /// <param name="isBuy">买卖方向</param>
        /// <param name="level">档位（1-5，1为最优价）</param>
        /// <returns>调整后的订单数量</returns>
        private int ApplyScenarioMultiplier(int baseQty, string scenarioType, bool isBuy, int level)
        {
            // 基础衰减（档位越远，数量越少）
            // Level 1: 100%, Level 2: 85%, Level 3: 70%, Level 4: 55%, Level 5: 40%
            double decayFactor = 1.0 - (level - 1) * 0.15;

            // 剧本乘数（调整买卖盘比例）
            double multiplier = scenarioType switch
            {
                "DeadMarket" => 1.0,  // 死水一潭：买卖对称
                "IrrationalExuberance" => isBuy ? 2.0 : 0.5,  // 非理性繁荣：买盘密集，卖盘稀疏
                "PanicSelling" => isBuy ? 0.5 : 2.0,  // 恐慌踩踏：卖盘密集，买盘稀疏
                "ShortSqueeze" => isBuy ? 3.0 : 0.2,  // 轧空风暴：买盘极密集，卖盘几乎消失
                _ => 1.0
            };

            return Math.Max(1, (int)(baseQty * multiplier * decayFactor));
        }

        /// <summary>
        /// 清除所有NPC订单（保留玩家订单）
        /// </summary>
        private void ClearNPCOrders()
        {
            Bids.RemoveAll(o => !o.IsPlayerOrder);
            Asks.RemoveAll(o => !o.IsPlayerOrder);
        }

        /// <summary>
        /// 对订单簿进行排序
        /// </summary>
        /// <remarks>
        /// WHY（为什么用List+Sort而非SortedList）：
        /// 订单簿规模小（<100项），List.Sort性能足够。
        /// SortedList的插入成本（O(n)）在小数据量下并无优势。
        /// 
        /// 排序规则：
        /// - Bids：价格降序 + 时间优先（价格高者在前，同价时间早者在前）
        /// - Asks：价格升序 + 时间优先（价格低者在前，同价时间早者在前）
        /// </remarks>
        private void SortOrderBook()
        {
            // 买盘降序排列（价格从高到低，时间优先）
            Bids.Sort((a, b) =>
            {
                int priceCompare = b.Price.CompareTo(a.Price); // 降序
                if (priceCompare != 0)
                    return priceCompare;
                return a.Timestamp.CompareTo(b.Timestamp); // 时间优先
            });

            // 卖盘升序排列（价格从低到高，时间优先）
            Asks.Sort((a, b) =>
            {
                int priceCompare = a.Price.CompareTo(b.Price); // 升序
                if (priceCompare != 0)
                    return priceCompare;
                return a.Timestamp.CompareTo(b.Timestamp); // 时间优先
            });
        }

        /// <summary>
        /// 获取玩家在此订单簿中的所有订单
        /// </summary>
        /// <returns>玩家订单列表</returns>
        public List<LimitOrder> GetPlayerOrders()
        {
            var playerOrders = new List<LimitOrder>();
            playerOrders.AddRange(Bids.Where(o => o.IsPlayerOrder));
            playerOrders.AddRange(Asks.Where(o => o.IsPlayerOrder));
            return playerOrders;
        }

        /// <summary>
        /// 计算价格区间内的订单总量（用于虚拟流量反推）
        /// </summary>
        /// <param name="fromPrice">起始价格</param>
        /// <param name="toPrice">目标价格</param>
        /// <param name="side">订单方向（Ask=卖盘，Bid=买盘）</param>
        /// <returns>区间内的订单总量</returns>
        /// <remarks>
        /// WHY（为什么需要这个方法）：
        /// 实现"价格位移 → 成交量反推"机制。
        /// 当公式计算出目标价时，需要知道从当前价到目标价需要吃掉多少订单。
        /// 
        /// 例如：
        /// - 当前盘口中间价 100g，目标价 105g（需要买压推高）
        /// - 扫描卖盘 [100, 105] 区间，发现有 50 个订单
        /// - 生成 50 个虚拟买单，正好推到 105g
        /// </remarks>
        public int CalculateVolumeInRange(decimal fromPrice, decimal toPrice, OrderSide side)
        {
            var targetQueue = side == OrderSide.Ask ? Asks : Bids;
            
            int totalVolume = 0;
            foreach (var order in targetQueue)
            {
                // 检查订单是否在区间内
                bool inRange = side == OrderSide.Ask
                    ? (order.Price >= fromPrice && order.Price <= toPrice)
                    : (order.Price >= toPrice && order.Price <= fromPrice);
                
                if (inRange)
                {
                    totalVolume += order.RemainingQuantity;
                }
            }
            
            return totalVolume;
        }
    }

    /// <summary>
    /// 订单方向枚举（用于 CalculateVolumeInRange）
    /// </summary>
    public enum OrderSide
    {
        /// <summary>买盘（Bids）</summary>
        Bid,
        /// <summary>卖盘（Asks）</summary>
        Ask
    }
}
