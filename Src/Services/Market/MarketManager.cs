using System;
using System.Collections.Generic;
using System.Linq;
using StardewCapital.Core.Time;
using StardewCapital.Domain.Instruments;
using StardewCapital.Domain.Market;
using StardewCapital.Services.Pricing;
using StardewCapital.Services.News;
using StardewCapital.Services.Trading;
using StardewModdingAPI;
using StardewValley;

namespace StardewCapital.Services.Market
{
    /// <summary>
    /// 市场管理器
    /// 协调所有市场相关服务，管理金融产品列表，驱动价格更新。
    /// 
    /// 核心职责：
    /// - 管理所有可交易的金融产品（期货、股票等）
    /// - 协调 MarketPriceUpdater 和 OrderBookManager
    /// - 提供统一的 API 供 UI 和其他服务调用
    /// </summary>
    public class MarketManager
    {
        private readonly IMonitor _monitor;
        private readonly OrderBookManager _orderBookManager;
        private readonly MarketPriceUpdater _priceUpdater;
        private readonly ModConfig _config;
        private ClearingService? _clearingService;
        
        private List<IInstrument> _instruments;
        
        public MarketManager(
            IMonitor monitor, 
            OrderBookManager orderBookManager,
            MarketPriceUpdater priceUpdater,
            ModConfig config)
        {
            _monitor = monitor;
            _orderBookManager = orderBookManager;
            _priceUpdater = priceUpdater;
            _config = config;
            
            _instruments = new List<IInstrument>();
        }

        /// <summary>
        /// 设置 BrokerageService 引用（用于订单结算回调）
        /// 同时初始化 ClearingService
        /// </summary>
        public void SetBrokerageService(BrokerageService brokerageService)
        {
            _orderBookManager.SetBrokerageService(brokerageService);
            
            // 初始化结算服务
            _clearingService = new ClearingService(_monitor, brokerageService, this);
        }

        /// <summary>
        /// 初始化市场，创建所有配置的商品期货合约
        /// </summary>
        public void InitializeMarket()
        {
            // 获取所有已加载的商品配置
            var commodityConfigs = _priceUpdater.GetAllCommodityConfigs();
            
            if (commodityConfigs.Count == 0)
            {
                _monitor.Log("[Market] 警告：没有加载任何商品配置！", LogLevel.Warn);
                return;
            }

            foreach (var config in commodityConfigs)
            {
                // 为每个商品创建期货合约
                // 使用当前季节作为默认交割季节（后续可改为可配置）
                string deliverySeason = Game1.currentSeason ?? "Spring";
                int daysToMaturity = 28; // 默认28天到期
                
                var futures = new CommodityFutures(
                    config.ItemId, 
                    config.Name, 
                    deliverySeason, 
                    daysToMaturity, 
                    config.BasePrice
                );
                
                _instruments.Add(futures);
                
                // 设置初始目标价（使用基础价格的1.1倍作为初始目标）
                double initialTarget = config.BasePrice * 1.1;
                _priceUpdater.SetInitialTarget(futures.Symbol, initialTarget);
                
                // 初始化订单簿
                double sensitivity = config.LiquiditySensitivity;
                _orderBookManager.CreateOrderBook(
                    futures.Symbol, 
                    (decimal)futures.CurrentPrice, 
                    "Normal", // 初始剧本
                    sensitivity
                );

                _monitor.Log(
                    $"[Market] 已创建期货合约: {futures.Symbol} " +
                    $"(ItemId={config.ItemId}, BasePrice={config.BasePrice}g, " +
                    $"Liquidity={sensitivity:F4})", 
                    LogLevel.Info
                );
            }

            _monitor.Log($"[Market] 市场初始化完成，共创建 {_instruments.Count} 个期货合约", LogLevel.Info);
        }

        /// <summary>
        /// 处理新一天开始的逻辑
        /// </summary>
        public void OnNewDay()
        {
            _priceUpdater.OnNewDay();
            
            // 执行每日结算
            _clearingService?.DailySettlement();
        }

        /// <summary>
        /// 每帧更新市场价格
        /// </summary>
        public void Update(int currentTick)
        {
            _priceUpdater.Update(currentTick);
        }
        
        /// <summary>
        /// 获取所有可交易的金融产品列表
        /// </summary>
        public List<IInstrument> GetInstruments()
        {
            return _instruments;
        }

        /// <summary>
        /// 获取完整新闻历史列表
        /// </summary>
        public List<NewsEvent> GetNewsHistory()
        {
            return _priceUpdater.GetNewsHistory();
        }

        /// <summary>
        /// 获取当前生效的新闻列表
        /// </summary>
        public List<NewsEvent> GetActiveNews()
        {
            return _priceUpdater.GetActiveNews();
        }

        /// <summary>
        /// 获取商品配置
        /// </summary>
        public CommodityConfig? GetCommodityConfig(string commodityName)
        {
            return _priceUpdater.GetCommodityConfig(commodityName);
        }

        /// <summary>
        /// 获取指定期货的订单簿
        /// </summary>
        public OrderBook? GetOrderBook(string symbol)
        {
            return _orderBookManager.GetOrderBook(symbol);
        }

        /// <summary>
        /// 获取所有订单簿
        /// </summary>
        public List<OrderBook> GetAllOrderBooks()
        {
            return _orderBookManager.GetAllOrderBooks();
        }

        /// <summary>
        /// 获取历史冲击值
        /// </summary>
        public List<double> GetImpactHistory(string commodityId)
        {
            return _priceUpdater.GetImpactHistory(commodityId);
        }
    }
}
