using System;
using System.Collections.Generic;
using System.Linq;
using StardewCapital.Domain.Account;
using StardewCapital.Domain.Instruments;
using StardewModdingAPI;
using StardewValley;

namespace StardewCapital.Services
{
    public class BrokerageService
    {
        private readonly TradingAccount _account;
        private readonly MarketManager _marketManager;
        private readonly IMonitor _monitor;

        public TradingAccount Account => _account;

        public BrokerageService(MarketManager marketManager, IMonitor monitor)
        {
            _marketManager = marketManager;
            _monitor = monitor;
            _account = new TradingAccount();
        }

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

        public void Withdraw(int amount)
        {
            try
            {
                // Need current prices to check free margin
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

        public void ExecuteOrder(string symbol, int quantity, int leverage)
        {
            var instrument = _marketManager.GetInstruments().FirstOrDefault(i => i.Symbol == symbol);
            if (instrument == null)
            {
                _monitor.Log($"Instrument {symbol} not found.", LogLevel.Error);
                return;
            }

            decimal price = (decimal)instrument.CurrentPrice;
            decimal requiredMargin = (price * Math.Abs(quantity)) / leverage;

            var prices = GetCurrentPrices();
            if (_account.GetFreeMargin(prices) >= requiredMargin)
            {
                // Create or update position
                var existingPos = _account.Positions.FirstOrDefault(p => p.Symbol == symbol);
                if (existingPos != null)
                {
                    // Averaging down/up logic (simplified for now: just add quantity, re-calc avg cost)
                    decimal totalCost = (existingPos.AverageCost * existingPos.Quantity) + (price * quantity);
                    int newQuantity = existingPos.Quantity + quantity;
                    
                    if (newQuantity == 0)
                    {
                        _account.Positions.Remove(existingPos);
                        _monitor.Log($"Position closed for {symbol}.", LogLevel.Info);
                    }
                    else
                    {
                        existingPos.Quantity = newQuantity;
                        existingPos.AverageCost = totalCost / newQuantity;
                        _monitor.Log($"Position updated for {symbol}. New Qty: {newQuantity}", LogLevel.Info);
                    }
                }
                else
                {
                    _account.Positions.Add(new Position(symbol, quantity, price, leverage));
                    _monitor.Log($"New Position opened: {quantity}x {symbol} @ {price:F2} (Lev: {leverage}x)", LogLevel.Info);
                }
            }
            else
            {
                _monitor.Log($"Insufficient margin! Required: {requiredMargin:F2}g", LogLevel.Warn);
            }
        }

        public void LoadAccount(decimal cash, List<Position> positions)
        {
            _account.Load(cash, positions);
        }

        private Dictionary<string, decimal> GetCurrentPrices()
        {
            var dict = new Dictionary<string, decimal>();
            foreach (var inst in _marketManager.GetInstruments())
            {
                dict[inst.Symbol] = (decimal)inst.CurrentPrice;
            }
            return dict;
        }
    }
}
