using System;
using StardewCapital.Core.Futures.Services;
using System.Collections.Generic;
using StardewCapital.Core.Futures.Services;
using System.Linq;
using StardewCapital.Core.Futures.Services;
using StardewCapital.Core.Futures.Domain.Instruments;
using StardewCapital.Core.Futures.Domain.Market;
using StardewCapital.Services.Trading;
using StardewCapital.Core.Common.Logging;

namespace StardewCapital.Services.Market
{
    /// <summary>
    /// 订单簿管理器
    /// 负责创建、维护和查询所有金融产品的订单簿。
    /// 
    /// 核心职责：
    /// - 维护订单簿集合（每个期货商品一个订单簿）
    /// - 订阅订单成交事件并转发给 BrokerageService
    /// - 提供订单簿查询 API
    /// - 记录被动成交的市场冲击
    /// </summary>
    public class OrderBookManager
    {
        private readonly ILogger _logger;
        private readonly ImpactService _impactService;
        private readonly MarketManager _marketManager;
        private BrokerageService? _brokerageService;
        
        /// <summary>
        /// 订单簿集合（每个期货商品维护独立的订单簿）
        /// Key = Symbol (例如 "PARSNIP-SPR-28"), Value = 该商品的订单簿实例
        /// </summary>
        private Dictionary<string, OrderBook> _orderBooks;

        public OrderBookManager(
            ILogger logger,
            ImpactService impactService,
            MarketManager marketManager)
        {
            _logger = logger;
            _impactService = impactService;
            _marketManager = marketManager;
            _orderBooks = new Dictionary<string, OrderBook>();
        }

        /// <summary>
        /// 设置 BrokerageService 引用（用于订单结算回调）
        /// </summary>
        /// <param name="brokerageService">经纪服务实例</param>
        /// <remarks>
        /// WHY（为什么不在构造函数注入）：
        /// MarketManager 和 BrokerageService 存在循环依赖。
        /// 使用 Setter 注入打破循环依赖。
        /// </remarks>
        public void SetBrokerageService(BrokerageService brokerageService)
        {
            _brokerageService = brokerageService;
            
            // 为所有现有订单簿订阅事件
            foreach (var orderBook in _orderBooks.Values)
            {
                SubscribeToOrderBook(orderBook);
            }
        }

        /// <summary>
        /// 创建新订单簿
        /// </summary>
        /// <param name="symbol">合约代码</param>
        /// <param name="initialPrice">初始价格</param>
        /// <param name="scenarioType">市场剧本</param>
        /// <param name="liquiditySensitivity">流动性敏感度</param>
        /// <returns>新创建的订单簿</returns>
        public OrderBook CreateOrderBook(
            string symbol, 
            decimal initialPrice, 
            string scenarioType, 
            double liquiditySensitivity)
        {
            var orderBook = new OrderBook(symbol);
            _orderBooks[symbol] = orderBook;
            
            // 生成初始深度
            orderBook.GenerateNPCDepth(initialPrice, scenarioType, liquiditySensitivity);
            
            // 订阅事件
            if (_brokerageService != null)
            {
                SubscribeToOrderBook(orderBook);
            }
            
            return orderBook;
        }

        /// <summary>
        /// 订阅订单簿的玩家成交事件
        /// </summary>
        private void SubscribeToOrderBook(OrderBook orderBook)
        {
            orderBook.OnPlayerOrderFilled += (fillInfo) =>
            {
                // 转发到 BrokerageService 进行资金结算
                _brokerageService?.HandlePlayerOrderFilled(fillInfo);
                
                // ========== 🔥 问题1修复：记录被动成交的市场冲击 ==========
                // 当玩家限价单（Maker）被虚拟流量吃掉时，视为真实成交量
                // 需要计入市场冲击系统，影响后续价格
                
                // 获取期货合约信息
                var instrument = _marketManager.GetInstruments()
                    .FirstOrDefault(i => i.Symbol == fillInfo.Symbol);
                
                if (instrument is CommodityFutures futures)
                {
                    // 获取商品配置（流动性敏感度）
                    var config = _marketManager.GetCommodityConfig(futures.CommodityName);
                    if (config != null)
                    {
                        // ⚠️ 注意方向：
                        // - 玩家买单被吃 → 市场有卖压 → 负冲击（压价）
                        // - 玩家卖单被吃 → 市场有买压 → 正冲击（推价）
                        // 因此需要**反转方向**
                        int impactQuantity = fillInfo.IsBuy 
                            ? -fillInfo.FillQuantity  // 买单被吃 = 市场卖出
                            : +fillInfo.FillQuantity; // 卖单被吃 = 市场买入
                        
                        _impactService.RecordPlayerTrade(
                            commodityId: futures.UnderlyingItemId,
                            quantity: impactQuantity,
                            liquiditySensitivity: config.LiquiditySensitivity
                        );
                        
                        _logger?.Log(
                            $"[Impact] Passive fill: {fillInfo.Symbol} {(fillInfo.IsBuy ? "BUY" : "SELL")} " +
                            $"{fillInfo.FillQuantity} → Impact qty={impactQuantity}",
                            LogLevel.Debug
                        );
                    }
                }
            };
        }

        /// <summary>
        /// 获取指定期货的订单簿
        /// </summary>
        /// <param name="symbol">合约代码（例如："PARSNIP-SPR-28"）</param>
        /// <returns>订单簿实例，如果不存在返回null</returns>
        public OrderBook? GetOrderBook(string symbol)
        {
            return _orderBooks.TryGetValue(symbol, out var orderBook) ? orderBook : null;
        }

        /// <summary>
        /// 获取所有订单簿（用于Web UI显示）
        /// </summary>
        /// <returns>订单簿列表</returns>
        public List<OrderBook> GetAllOrderBooks()
        {
            return _orderBooks.Values.ToList();
        }

        /// <summary>
        /// 重新生成订单簿深度（新一天或市场剧本切换时调用）
        /// </summary>
        /// <param name="symbol">合约代码</param>
        /// <param name="targetPrice">目标价格</param>
        /// <param name="scenarioType">市场剧本</param>
        /// <param name="liquiditySensitivity">流动性敏感度</param>
        public void RegenerateDepth(
            string symbol, 
            decimal targetPrice, 
            string scenarioType, 
            double liquiditySensitivity)
        {
            if (_orderBooks.TryGetValue(symbol, out var orderBook))
            {
                orderBook.GenerateNPCDepth(targetPrice, scenarioType, liquiditySensitivity);
            }
        }
    }
}


