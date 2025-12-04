using System;
using StardewCapital.Core.Futures.Services;
using System.Collections.Generic;
using StardewCapital.Core.Futures.Services;
using System.Linq;
using StardewCapital.Core.Futures.Services;
using StardewCapital.Core.Futures.Domain.Account;
using StardewCapital.Core.Futures.Domain.Instruments;
using StardewCapital.Core.Futures.Domain.Market;
using StardewCapital.Services.Market;
using StardewCapital.Services.Infrastructure;
using StardewModdingAPI;
using StardewValley;

namespace StardewCapital.Services.Trading
{
    /// <summary>
    /// 经纪服务
    /// 处理玩家与市场之间的交易操作，管理交易账户。
    /// 
    /// 核心功能：
    /// - 资金存取（从游戏钱包到交易账户的转移）
    /// - 订单执行（开仓、平仓、加仓）
    /// - 账户状态查询（余额、权益、保证金）
    /// 
    /// 设计模式：
    /// 作为Facade模式，封装了TradingAccount和MarketManager的复杂交互。
    /// </summary>
    public class BrokerageService
    {
        private readonly TradingAccount _account;
        private readonly MarketManager _marketManager;
        private readonly ImpactService _impactService;
        private readonly IMonitor _monitor;

        /// <summary>获取交易账户实例（只读访问）</summary>
        public TradingAccount Account => _account;

        public BrokerageService(MarketManager marketManager, ImpactService impactService, IMonitor monitor)
        {
            _marketManager = marketManager;
            _impactService = impactService;
            _monitor = monitor;
            _account = new TradingAccount();
        }

        /// <summary>
        /// 从游戏钱包存入资金到交易账户
        /// </summary>
        /// <param name="amount">存入金额（金币）</param>
        public void Deposit(int amount)
        {
            if (Game1.player.Money >= amount)
            {
                Game1.player.Money -= amount;
                _account.Deposit(amount);
                _monitor.Log($"Deposited {amount}g. New Balance: {_account.Cash}g", LogLevel.Info);
            }
            else
            {
                _monitor.Log("Insufficient funds in wallet.", LogLevel.Warn);
            }
        }

        /// <summary>
        /// 从交易账户提取资金到游戏钱包
        /// 需要满足保证金要求（可用保证金充足）
        /// </summary>
        /// <param name="amount">提取金额（金币）</param>
        public void Withdraw(int amount)
        {
            try
            {
                var prices = GetCurrentPrices();
                if (_account.GetFreeMargin(prices) >= amount)
                {
                    _account.Withdraw(amount);
                    Game1.player.Money += amount;
                    _monitor.Log($"Withdrawn {amount}g. New Balance: {_account.Cash}g", LogLevel.Info);
                }
                else
                {
                    _monitor.Log("Insufficient free margin to withdraw.", LogLevel.Warn);
                }
            }
            catch (Exception ex)
            {
                _monitor.Log($"Withdraw failed: {ex.Message}", LogLevel.Error);
            }
        }

        /// <summary>
        /// 执行交易订单（开仓或加仓/减仓）
        /// </summary>
        /// <param name="symbol">合约代码</param>
        /// <param name="quantity">交易数量（正数=买入/做多，负数=卖出/做空）</param>
        /// <param name="leverage">杠杆倍数（1x, 5x, 10x等）</param>
        public void ExecuteOrder(string symbol, int quantity, int leverage)
        {
            var instrument = _marketManager.GetInstruments().FirstOrDefault(i => i.Symbol == symbol);
            if (instrument == null)
            {
                _monitor.Log($"Instrument {symbol} not found.", LogLevel.Error);
                return;
            }

            // 期货合约使用期货价格交易，其他产品使用现货价格
            decimal price = instrument is CommodityFutures futures
                ? (decimal)futures.FuturesPrice
                : (decimal)instrument.CurrentPrice;
            
            decimal requiredMargin = (price * Math.Abs(quantity)) / leverage;

            var prices = GetCurrentPrices();
            if (_account.GetFreeMargin(prices) >= requiredMargin)
            {
                // 查找现有仓位
                var existingPos = _account.Positions.FirstOrDefault(p => p.Symbol == symbol);
                if (existingPos != null)
                {
                    // 加仓/减仓逻辑：重新计算平均成本
                    decimal totalCost = (existingPos.AverageCost * existingPos.Quantity) + (price * quantity);
                    int newQuantity = existingPos.Quantity + quantity;
                    
                    if (newQuantity == 0)
                    {
                        // 完全平仓
                        _account.Positions.Remove(existingPos);
                        _monitor.Log($"Position closed for {symbol}.", LogLevel.Info);
                    }
                    else
                    {
                        // 更新仓位
                        existingPos.Quantity = newQuantity;
                        existingPos.AverageCost = totalCost / newQuantity;
                        _monitor.Log($"Position updated for {symbol}. New Qty: {newQuantity}", LogLevel.Info);
                    }
                }
                else
                {
                    // 开新仓
                    _account.Positions.Add(new Position(symbol, quantity, price, leverage));
                    _monitor.Log($"New Position opened: {quantity}x {symbol} @ {price:F2} (Lev: {leverage}x)", LogLevel.Info);
                }
                
                // ========== 记录玩家交易对市场的冲击 (Phase 9) ==========
                if (instrument is CommodityFutures commodityFutures)
                {
                    // 获取商品配置（包含流动性敏感度）
                    var config = _marketManager.GetCommodityConfig(commodityFutures.CommodityName);
                    if (config != null)
                    {
                        // 记录交易冲击：ΔI_Player = Q × η
                        _impactService.RecordPlayerTrade(
                            commodityId: commodityFutures.UnderlyingItemId,
                            quantity: quantity,
                            liquiditySensitivity: config.LiquiditySensitivity
                        );
                    }
                }
            }
            else
            {
                _monitor.Log($"Insufficient margin! Required: {requiredMargin:F2}g", LogLevel.Warn);
            }
        }

        /// <summary>
        /// 加载存档数据到账户
        /// </summary>
        /// <param name="cash">现金余额</param>
        /// <param name="positions">仓位列表</param>
        public void LoadAccount(decimal cash, List<Position> positions)
        {
            _account.Load(cash, positions);
        }

        /// <summary>
        /// 获取当前所有产品的市场价格
        /// </summary>
        /// <returns>价格字典（Symbol -> Price）</returns>
        public Dictionary<string, decimal> GetCurrentPrices()
        {
            var dict = new Dictionary<string, decimal>();
            foreach (var inst in _marketManager.GetInstruments())
            {
                // 期货合约使用期货价格，其他产品使用现货价格
                decimal price = inst is CommodityFutures futures
                    ? (decimal)futures.FuturesPrice
                    : (decimal)inst.CurrentPrice;
                dict[inst.Symbol] = price;
            }
            return dict;
        }

        /// <summary>
        /// 获取账户总未实现盈亏
        /// </summary>
        public decimal GetTotalUnrealizedPnL(Dictionary<string, decimal> currentPrices)
        {
            return _account.GetTotalUnrealizedPnL(currentPrices);
        }

        /// <summary>
        /// 获取账户权益 (Equity = Cash + Unrealized PnL)
        /// </summary>
        public decimal GetEquity(Dictionary<string, decimal> currentPrices)
        {
            return _account.GetEquity(currentPrices);
        }

        /// <summary>
        /// 强制平仓指定合约（忽略保证金检查）
        /// </summary>
        /// <param name="symbol">合约代码</param>
        public void LiquidatePosition(string symbol)
        {
            var pos = _account.Positions.FirstOrDefault(p => p.Symbol == symbol);
            if (pos == null) return;

            // 强制平仓 = 反向开单，数量等于持仓量
            // 使用 ExecuteOrder，但在内部需要绕过保证金检查吗？
            // ExecuteOrder 会检查保证金，这在强平且保证金不足时会失败。
            // 因此需要一个专门的 InternalExecuteOrder 或者在此处直接操作。
            
            // 为了安全，我们直接模拟一次成交，不走 ExecuteOrder 的保证金检查流程
            
            var prices = GetCurrentPrices();
            if (!prices.TryGetValue(symbol, out decimal currentPrice)) return;

            // 1. 计算盈亏
            decimal pnl = pos.GetUnrealizedPnL(currentPrice);
            
            // 2. 结算到现金
            _account.Cash += pnl;
            
            // 3. 释放保证金
            _account.ReleaseMargin(pos.UsedMargin);
            
            // 4. 移除仓位
            _account.Positions.Remove(pos);
            
            _monitor.Log($"[Brokerage] Force liquidated {symbol}. PnL: {pnl:F2}g", LogLevel.Warn);
            
            // 5. 记录冲击（强平也是市场交易）
            var instrument = _marketManager.GetInstruments().FirstOrDefault(i => i.Symbol == symbol);
            if (instrument is CommodityFutures futures)
            {
                var config = _marketManager.GetCommodityConfig(futures.CommodityName);
                if (config != null)
                {
                    // 强平方向与持仓相反：多头强平=卖出，空头强平=买入
                    int quantity = -pos.Quantity;
                    _impactService.RecordPlayerTrade(
                        commodityId: futures.UnderlyingItemId,
                        quantity: quantity,
                        liquiditySensitivity: config.LiquiditySensitivity
                    );
                }
            }
        }

        /// <summary>
        /// 下限价单（玩家作为Maker）
        /// </summary>
        /// <param name="symbol">合约代码</param>
        /// <param name="isBuy">买卖方向（true=买入/做多，false=卖出/做空）</param>
        /// <param name="price">限价</param>
        /// <param name="quantity">数量</param>
        /// <param name="leverage">杠杆倍数</param>
        /// <returns>订单ID，成功返回GUID，失败返回null</returns>
        /// <remarks>
        /// WHY（为什么需要限价单）：
        /// 限价单允许玩家在指定价格成交，而非市价。这让玩家可以：
        /// 1. 避免滑点：在理想价格成交，不接受价格恶化
        /// 2. 挂单阻挡：大额挂单可以阻挡虚拟流量，影响市场价格
        /// 3. 战略交易：可以在支撑/阻力位挂单，等待成交
        /// </remarks>
        public string? PlaceLimitOrder(string symbol, bool isBuy, decimal price, int quantity, int leverage)
        {
            var instrument = _marketManager.GetInstruments().FirstOrDefault(i => i.Symbol == symbol);
            if (instrument == null)
            {
                _monitor.Log($"Instrument {symbol} not found.", LogLevel.Error);
                return null;
            }

            // 1. 计算需要冻结的保证金
            decimal requiredMargin = (price * Math.Abs(quantity)) / leverage;

            // 2. 检查可用保证金
            var prices = GetCurrentPrices();
            if (_account.GetFreeMargin(prices) < requiredMargin)
            {
                _monitor.Log($"Insufficient margin! Required: {requiredMargin:F2}g", LogLevel.Warn);
                return null;
            }

            // 3. 冻结保证金
            try
            {
                _account.FreezeMargin(requiredMargin);
            }
            catch (Exception ex)
            {
                _monitor.Log($"Failed to freeze margin: {ex.Message}", LogLevel.Error);
                return null;
            }

            // 4. 创建限价订单
            var order = new LimitOrder(
                symbol: symbol,
                isBuy: isBuy,
                price: price,
                quantity: quantity,
                isPlayerOrder: true,
                leverage: leverage  // ✅ 传递杠杆参数
            );

            // 5. 提交到订单簿
            var orderBook = _marketManager.GetOrderBook(symbol);
            if (orderBook == null)
            {
                // 如果订单簿不存在，释放保证金
                _account.ReleaseMargin(requiredMargin);
                _monitor.Log($"Order book not found for {symbol}.", LogLevel.Error);
                return null;
            }

            orderBook.PlaceOrder(order);

            // 6. 日志输出
            _monitor.Log(
                $"[LimitOrder] Placed: {(isBuy ? "BUY" : "SELL")} {quantity}x {symbol} @ {price:F2}g (OrderID: {order.OrderId})",
                LogLevel.Info
            );

            return order.OrderId;
        }

        /// <summary>
        /// 撤销限价单
        /// </summary>
        /// <param name="symbol">合约代码</param>
        /// <param name="orderId">订单ID</param>
        /// <returns>是否成功</returns>
        /// <remarks>
        /// WHY（为什么需要撤单功能）：
        /// 市场情况变化时，玩家可能需要撤销原有挂单，重新调整战略。
        /// 撤单后需要释放冻结的保证金，使其可以用于其他交易。
        /// </remarks>
        public bool CancelOrder(string symbol, string orderId)
        {
            // 1. 获取订单簿
            var orderBook = _marketManager.GetOrderBook(symbol);
            if (orderBook == null)
            {
                _monitor.Log($"Order book not found for {symbol}.", LogLevel.Error);
                return false;
            }

            // 2. 获取订单详情（用于计算释放的保证金）
            var playerOrders = orderBook.GetPlayerOrders();
            var order = playerOrders.FirstOrDefault(o => o.OrderId == orderId);

            if (order == null)
            {
                _monitor.Log($"Order {orderId} not found or not owned by player.", LogLevel.Warn);
                return false;
            }

            // 3. 撤销订单
            bool cancelled = orderBook.CancelOrder(orderId);

            if (!cancelled)
            {
                _monitor.Log($"Failed to cancel order {orderId}.", LogLevel.Error);
                return false;
            }

            // 4. 计算需要释放的保证金（仅释放未成交部分）
            // 注意：如果订单部分成交，已成交部分的保证金已转为仓位保证金，不需要释放
            var instrument = _marketManager.GetInstruments().FirstOrDefault(i => i.Symbol == symbol);
            if (instrument == null)
            {
                _monitor.Log($"Instrument {symbol} not found.", LogLevel.Error);
                return false;
            }

            // TODO: 需要记录订单的杠杆，暂时使用10x
            int leverage = 10;
            decimal frozenMargin = (order.Price * order.RemainingQuantity) / leverage;

            // 5. 释放冻结的保证金
            try
            {
                _account.ReleaseMargin(frozenMargin);

                _monitor.Log(
                    $"[LimitOrder] Cancelled: OrderID {orderId}, Released margin: {frozenMargin:F2}g",
                    LogLevel.Info
                );

                return true;
            }
            catch (Exception ex)
            {
                _monitor.Log($"Failed to release margin: {ex.Message}", LogLevel.Error);
                return false;
            }
        }

        /// <summary>
        /// 处理玩家限价单成交回调（由 OrderBook 事件触发）
        /// </summary>
        /// <param name="fillInfo">成交信息</param>
        /// <remarks>
        /// WHY（为什么需要这个方法）：
        /// 限价单成交是分散发生的（每次撮合只成交一部分），需要实时处理：
        /// 1. 解冻对应数量的保证金
        /// 2. 结算资金流动（买入支付，卖出收款）
        /// 3. 建立或更新仓位（计算加权平均成本）
        /// 
        /// 流程：
        /// A. 解冻保证金：frozenMargin = (fillPrice × fillQty) / leverage
        /// B. 资金流动：买入扣款，卖出收款
        /// C. 仓位管理：开仓 or 加仓 or 对冲平仓
        /// </remarks>
        public void HandlePlayerOrderFilled(OrderFillInfo fillInfo)
        {
            try
            {
                // ========== A. 解冻保证金 ==========
                // 限价单成交时，将冻结的保证金释放，随后创建的仓位会占用保证金
                decimal frozenMargin = (fillInfo.FillPrice * fillInfo.FillQuantity) / fillInfo.Leverage;
                _account.ReleaseMargin(frozenMargin);

                // ========== B. 资金流动（期货交易特性） ==========
                // ⚠️ 重要：期货交易开仓时**不扣除全额资金**，只占用保证金
                // 资金流动仅在以下情况发生：
                // - 平仓时结算盈亏
                // - 每日 Mark-to-Market 结算（未实现）
                // 
                // 因此这里 **不改变 Cash**，保证金从冻结状态转为仓位占用状态
                // 
                // 错误示例（已删除）：
                // decimal cashFlow = fillInfo.IsBuy
                //     ? -(fillInfo.FillPrice * fillInfo.FillQuantity)  // ❌ 错误！期货不需要全额支付
                //     : +(fillInfo.FillPrice * fillInfo.FillQuantity); // ❌ 错误！
                // _account.Cash += cashFlow;

                // ========== C. 仓位管理 ==========
                var existingPos = _account.Positions.FirstOrDefault(p => p.Symbol == fillInfo.Symbol);
                int signedQty = fillInfo.IsBuy ? fillInfo.FillQuantity : -fillInfo.FillQuantity;

                if (existingPos != null)
                {
                    // 检查是否为对冲（反向开仓）
                    bool isOpposite = (existingPos.Quantity > 0 && signedQty < 0) || 
                                     (existingPos.Quantity < 0 && signedQty > 0);

                    if (isOpposite)
                    {
                        // 对冲逻辑：减少现有仓位
                        int newQuantity = existingPos.Quantity + signedQty;

                        if (newQuantity == 0)
                        {
                            // 完全对冲平仓 → 这里应该结算盈亏
                            // TODO: 计算并结算盈亏到 Cash
                            // decimal pnl = (fillInfo.FillPrice - existingPos.AverageCost) * Math.Abs(existingPos.Quantity);
                            // _account.Cash += pnl;
                            
                            _account.Positions.Remove(existingPos);
                            _monitor.Log(
                                $"[Settlement] Position closed: {fillInfo.Symbol} (hedge)",
                                LogLevel.Info
                            );
                        }
                        else
                        {
                            // 部分对冲（保持原平均成本）
                            // TODO: 结算部分平仓的盈亏
                            existingPos.Quantity = newQuantity;
                            _monitor.Log(
                                $"[Settlement] Position hedged: {fillInfo.Symbol}, NewQty={newQuantity}",
                                LogLevel.Info
                            );
                        }
                    }
                    else
                    {
                        // 加仓：重新计算加权平均成本
                        decimal totalCost = (existingPos.AverageCost * Math.Abs(existingPos.Quantity)) +
                                          (fillInfo.FillPrice * fillInfo.FillQuantity);
                        int totalQty = Math.Abs(existingPos.Quantity) + fillInfo.FillQuantity;

                        existingPos.Quantity += signedQty;
                        existingPos.AverageCost = totalCost / totalQty;

                        _monitor.Log(
                            $"[Settlement] Position increased: {fillInfo.Symbol}, " +
                            $"NewQty={existingPos.Quantity}, AvgCost={existingPos.AverageCost:F2}g",
                            LogLevel.Info
                        );
                    }
                }
                else
                {
                    // 开新仓（只占用保证金，不扣除现金）
                    _account.Positions.Add(new Position(
                        symbol: fillInfo.Symbol,
                        quantity: signedQty,
                        averageCost: fillInfo.FillPrice,
                        leverage: fillInfo.Leverage
                    ));

                    _monitor.Log(
                        $"[Settlement] New position opened: {signedQty}x {fillInfo.Symbol} @ {fillInfo.FillPrice:F2}g (Margin-only)",
                        LogLevel.Info
                    );
                }

                // ========== D. 日志输出（调试用） ==========
                _monitor.Log(
                    $"[Settlement] Order {fillInfo.OrderId} filled: " +
                    $"{fillInfo.FillQuantity} @ {fillInfo.FillPrice:F2}g, " +
                    $"ReleasedMargin={frozenMargin:F2}g, " +
                    $"Cash={_account.Cash:F2}g (unchanged)",
                    LogLevel.Debug
                );
            }
            catch (Exception ex)
            {
                _monitor.Log(
                    $"[Settlement] ERROR handling order fill {fillInfo.OrderId}: {ex.Message}",
                    LogLevel.Error
                );
            }
        }
    }
}



