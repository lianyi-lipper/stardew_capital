using System;
using System.Collections.Generic;
using System.Linq;
using StardewCapital.Domain.Account;
using StardewCapital.Domain.Instruments;
using StardewModdingAPI;
using StardewValley;

namespace StardewCapital.Services
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
        private readonly IMonitor _monitor;

        /// <summary>获取交易账户实例（只读访问）</summary>
        public TradingAccount Account => _account;

        public BrokerageService(MarketManager marketManager, IMonitor monitor)
        {
            _marketManager = marketManager;
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
        private Dictionary<string, decimal> GetCurrentPrices()
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
    }
}
