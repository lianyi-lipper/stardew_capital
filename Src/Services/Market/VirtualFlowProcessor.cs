using System;
using System.Linq;
using StardewCapital.Domain.Instruments;
using StardewCapital.Config;
using StardewModdingAPI;

namespace StardewCapital.Services.Market
{
    /// <summary>
    /// 虚拟流量处理器
    /// 负责订单簿碰撞检测和虚拟流量处理
    /// </summary>
    public class VirtualFlowProcessor
    {
        private readonly IMonitor _monitor;
        private readonly MarketManager _marketManager;
        private readonly OrderBookManager _orderBookManager;
        private readonly NPCAgentManager _npcAgentManager;

        public VirtualFlowProcessor(
            IMonitor monitor,
            MarketManager marketManager,
            OrderBookManager orderBookManager,
            NPCAgentManager npcAgentManager)
        {
            _monitor = monitor;
            _marketManager = marketManager;
            _orderBookManager = orderBookManager;
            _npcAgentManager = npcAgentManager;
        }

        /// <summary>
        /// 处理虚拟流量（订单簿碰撞检测）
        /// </summary>
        public void ProcessVirtualFlow(string scenarioType)
        {
            // 防御性检查：确保依赖服务已初始化
            if (_marketManager == null || _orderBookManager == null)
            {
                _monitor?.Log("[VirtualFlow] MarketManager or OrderBookManager is null, skipping", LogLevel.Trace);
                return;
            }

            var instruments = _marketManager.GetInstruments();
            if (instruments == null)
            {
                _monitor?.Log("[VirtualFlow] No instruments available, skipping", LogLevel.Trace);
                return;
            }
                
            foreach (var instrument in instruments)
            {
                if (instrument is not CommodityFutures futures) continue;
                
                // 获取订单簿
                var orderBook = _orderBookManager.GetOrderBook(futures.Symbol);
                if (orderBook == null)
                    continue;
                
                // 1. 获取理论目标价（来自价格引擎 + 冲击层）
                decimal targetPrice = (decimal)futures.CurrentPrice;
                
                // 2. 获取当前盘口中间价
                decimal midPrice = orderBook.GetMidPrice();
                
                // 如果订单簿为空（无深度），先生成NPC深度
                if (midPrice == 0)
                {
                    var config = _marketManager.GetCommodityConfig(futures.CommodityName);
                    if (config != null)
                    {
                        _orderBookManager.RegenerateDepth(
                            futures.Symbol,
                            targetPrice,
                            scenarioType,
                            config.LiquiditySensitivity
                        );
                    }
                    continue;
                }
                
                // 3. 计算价差
                decimal priceDiff = targetPrice - midPrice;
                
                // 如果价差小于阈值，无需虚拟流量（避免过度撮合）
                if (Math.Abs(priceDiff) < 0.1m)
                    continue;
                
                // 4. 从NPCAgentManager获取虚拟流量（使用真实NPC计算）
                int flowQuantity = 0;
                bool isBuyPressure = priceDiff > 0; // 默认值：价差方向
                
                var npcForces = _npcAgentManager.LastForces;
                if (npcForces.TryGetValue(futures.Symbol, out var forces))
                {
                    // 使用NPC计算的总流量
                    flowQuantity = Math.Abs((int)forces.TotalFlow);
                    isBuyPressure = forces.TotalFlow > 0; // 正流量=买压，负流量=卖压
                    
                    _monitor?.Log(
                        $"[VirtualFlow] {futures.Symbol}: Using NPC forces - " +
                        $"Total={forces.TotalFlow:F1}, Smart={forces.SmartMoneyFlow:F1}, " +
                        $"Trend={forces.TrendFlow:F1}, FOMO={forces.FomoFlow:F1}",
                        LogLevel.Trace
                    );
                }
                else
                {
                    // 降级：如果没有NPC数据，使用简化计算
                    flowQuantity = CalculateFlowQuantity(priceDiff);
                    _monitor?.Log(
                        $"[VirtualFlow] {futures.Symbol}: No NPC data, using fallback calculation",
                        LogLevel.Debug
                    );
                }
                
                // 跳过极小的流量
                if (flowQuantity < 5)
                    continue;
                
                // 5. 虚拟流量撞击订单簿
                var (vwap, slippage) = orderBook.ExecuteMarketOrder(isBuyPressure, flowQuantity);
                
                // 6. 日志输出（调试用）
                if (flowQuantity > 0 && vwap > 0)
                {
                    _monitor?.Log(
                        $"[OrderBook] {futures.Symbol}: VirtualFlow {(isBuyPressure ? "BUY" : "SELL")} {flowQuantity} @ VWAP={vwap:F2}g, Slippage={slippage:F2}g",
                        LogLevel.Debug
                    );
                }
            }
        }

        /// <summary>
        /// 计算虚拟流量数量 (降级方法，当NPC数据不可用时使用)
        /// </summary>
        private int CalculateFlowQuantity(decimal priceDiff)
        {
            // 价差越大，流量越大（非线性关系）
            decimal absDiff = Math.Abs(priceDiff);
            
            if (absDiff < 0.5m)
                return 10;
            if (absDiff < 1.0m)
                return 25;
            if (absDiff < 2.0m)
                return 50;
            
            return 100; // 极端价差，强力流量
        }
    }
}
