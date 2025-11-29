// FuturesMarket.cs

using System;
using StardewModdingAPI;
using StardewValley;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace StardewCapital
{
    public class TradeCommand
    {
        public string Action { get; set; }
        public int Amount { get; set; }
        public double Value { get; set; } // For deposit/withdraw
        public bool IsLong { get; set; }
        public Guid? PositionId { get; set; }
    }

    public class FuturesMarket
    {
        private readonly IModHelper _helper;
        private readonly IMonitor _monitor;
        private Random _random = new Random();
        private double _hiddenFinalSpotPrice;
        private double _initialSpotPrice; //季初基准价
        private double _marketImbalance = 0.0; // 市场失衡值

        private const double CARRY_COST_PER_DAY = 0.1;
        private const double NOISE_FACTOR = 0.5;
        private const double MOMENTUM_STRENGTH = 0.03; // 跟风盘强度
        private const double CONTRARIAN_STRENGTH = 0.05; // 对手盘强度 (必须大于MOMENTUM)

        // --- Trading Constants ---
        public const double LEVERAGE = 20.0;
        public const double COMMISSION_RATE = 0.0005; // 0.05%

        public List<double> DailyKLineData { get; private set; } = new List<double>();
        public double CurrentPrice { get; private set; }
        public string ContractName { get; private set; }
        public string DailyNews { get; private set; }
        public MarketStatus CurrentStatus { get; private set; } = MarketStatus.Closed;

        // --- Trading Account Data ---
        public double TradingAccountBalance { get; set; } = 10000.0; // Example starting balance
        public double UsedMargin { get; private set; } = 0.0;
        public double FreeMargin => TradingAccountBalance - UsedMargin;
        public double AccountEquity { get; private set; } = 10000.0;


        // [新增] 用于存储最后一个计算的价格（即收盘价）
        private double _lastCalculatedPrice = 0.0;

        // --- Position Management ---
        public List<PlayerPosition> OpenPositions { get; set; } = new List<PlayerPosition>();

        public enum MarketStatus
        {
            Waiting,
            Open,
            Closed
        }

        private InstitutionalShockEvent _todaysShockEvent = null;

        private class InstitutionalShockEvent
        {
            public int StartTime { get; set; }
            public int DurationMinutes { get; set; }
            public double ForcePerTick { get; set; }
        }

        public FuturesMarket(IModHelper helper, IMonitor monitor)
        {
            this._helper = helper;
            this._monitor = monitor;
        }

        public void InitializeNewSeason()
        {
            if (Game1.currentSeason.ToLower() == "spring")
            {
                this.ContractName = "春季防风草-28日";
                _initialSpotPrice = 35.0; // 保存季初的基准价
                _hiddenFinalSpotPrice = 35.0 + (_random.NextDouble() * 20.0 - 10.0);

                // Immediately calculate the price for the current time
                // UpdateMarketPrice(); // 在这里调用还为时过早，因为K线数据还没算
                _monitor.Log($"New season initialized. Hidden Final Price for Parsnip: {_hiddenFinalSpotPrice:F2}g", LogLevel.Info);
            }
            else
            {
                this.ContractName = "暂无期货";
                this.CurrentPrice = 0;
            }
        }

        public void ApplyDailyNews()
        {
            this.DailyNews = ""; // Reset news at the start of the day
            DailyKLineData.Clear(); // 清空前一天的K线数据
            this.CurrentStatus = MarketStatus.Waiting; // 新的一天，等待开盘
            _todaysShockEvent = null; // 重置主力事件
            _marketImbalance = 0.0; // [重要] 重置市场失衡值

            // --- 概率触发主力冲击事件 ---
            if (_random.NextDouble() < 0.5) // 50%的概率触发
            {
                int startTime = _random.Next(900, 1500); // 在 9:00 AM 到 3:00 PM 之间随机开始
                int duration = _random.Next(60, 181);   // 持续 1 到 3 小时
                double force = (_random.NextDouble() * 0.4 - 0.2); // 冲击力在 [-0.2, 0.2] 之间

                _todaysShockEvent = new InstitutionalShockEvent
                {
                    StartTime = startTime,
                    DurationMinutes = duration,
                    ForcePerTick = force
                };
                _monitor.Log($"Institutional Shock Event triggered! Start: {startTime}, Duration: {duration}min, Force: {force:F3}", LogLevel.Info);
            }

            if (string.IsNullOrEmpty(this.ContractName) || this.ContractName == "暂无期货")
            {
                return; // Don't apply news if there's no active contract
            }

            // --- 新闻影响最终价格 (逻辑保持不变) ---
            if (_random.NextDouble() < 1.0) // 100% chance to have news
            {
                try
                {
                    string filePath = Path.Combine(_helper.DirectoryPath, "news.csv");
                    if (File.Exists(filePath))
                    {
                        var lines = File.ReadAllLines(filePath).Skip(1);
                        var newsEvents = new List<Tuple<string, double>>();
                        string currentContractBaseName = this.ContractName.Split('-')[0];

                        foreach (var line in lines)
                        {
                            var parts = line.Split(',');
                            if (parts.Length == 3 && parts[0] == currentContractBaseName)
                            {
                                newsEvents.Add(new Tuple<string, double>(parts[1], double.Parse(parts[2])));
                            }
                        }

                        if (newsEvents.Count > 0)
                        {
                            var selectedNews = newsEvents[_random.Next(newsEvents.Count)];
                            _hiddenFinalSpotPrice += selectedNews.Item2;
                            this.DailyNews = selectedNews.Item1;
                            _monitor.Log($"News Event: {selectedNews.Item1}. Price changed by {selectedNews.Item2:F2}g. New Hidden Final Price: {_hiddenFinalSpotPrice:F2}g", LogLevel.Info);
                        }
                    }
                    else
                    {
                        _monitor.Log("news.csv not found in mod directory.", LogLevel.Warn);
                    }
                }
                catch (Exception ex)
                {
                    _monitor.Log($"Failed to apply daily news: {ex.Message}", LogLevel.Error);
                }
            }

            // --- [已修改] 预计算当天的K线路径 (9:00 AM - 5:00 PM) ---
            int daysPassed = Game1.dayOfMonth - 1;
            // 交易时段 9:00 AM (180分钟 from 6AM) to 5:00 PM (660分钟 from 6AM)
            for (int minute = 180; minute <= 660; minute += 10)
            {
                // 注意: totalSeasonProgress 仍需使用相对于6AM的完整时间来计算，以保证价格路径的正确性
                float timeRatio = (float)minute / 1200f;
                double totalSeasonProgress = (daysPassed + timeRatio) / 28.0;
                totalSeasonProgress = Math.Clamp(totalSeasonProgress, 0.0, 1.0);

                double pPath = _initialSpotPrice + (_hiddenFinalSpotPrice - _initialSpotPrice) * totalSeasonProgress;

                DailyKLineData.Add(pPath);
            }
            _monitor.Log($"Generated daily K-line data with {DailyKLineData.Count} points (9:00-17:00).", LogLevel.Info);

            // 在K线数据准备好后，立即更新一次价格（主要为了设置Waiting状态的开盘价）
            UpdateMarketPrice();
        }

        public void ApplyPlayerTrade(double tradeAmount)
        {
            // tradeAmount > 0 for buy, < 0 for sell
            _marketImbalance += tradeAmount;
            // 可以加一个失衡值上限
            _marketImbalance = Math.Clamp(_marketImbalance, -10.0, 10.0);
        }

        public void UpdateUsedMargin()
        {
            UsedMargin = OpenPositions.Sum(p => p.MarginUsed);
        }

        public void UpdateMarketPrice()
        {
            if (Game1.currentSeason.ToLower() != "spring" || DailyKLineData.Count == 0) return;

            int currentGameTime = Game1.timeOfDay;
            // [修复] 变量 'totalMinutesInDay' 只声明一次
            int totalMinutesInDay = (currentGameTime / 100) * 60 + (currentGameTime % 100);

            // --- 更新市场状态 ---
            if (currentGameTime >= 900 && currentGameTime < 1700)
            {
                CurrentStatus = MarketStatus.Open;
            }
            else if (currentGameTime >= 1700)
            {
                CurrentStatus = MarketStatus.Closed;
            }
            else
            {
                CurrentStatus = MarketStatus.Waiting;
            }

            // --- [重大逻辑修复] ---
            // 只有在市场开放时才进行动态计算
            if (CurrentStatus == MarketStatus.Open)
            {
                // --- 1. 市场活跃时，更新失衡值 ---
                if (_todaysShockEvent != null)
                {
                    int eventStartMinutes = (_todaysShockEvent.StartTime / 100) * 60 + (_todaysShockEvent.StartTime % 100);
                    int eventEndMinutes = eventStartMinutes + _todaysShockEvent.DurationMinutes;

                    if (totalMinutesInDay >= eventStartMinutes && totalMinutesInDay < eventEndMinutes)
                    {
                        _marketImbalance += _todaysShockEvent.ForcePerTick;
                    }
                }

                // --- 2. "拔河" 模型 ---
                double contrarianForce = -_marketImbalance * CONTRARIAN_STRENGTH;
                double momentumForce = _marketImbalance * MOMENTUM_STRENGTH;
                _marketImbalance += contrarianForce + momentumForce;

                // --- 3. 价格计算 ---
                // 'minutesPassed' 是距 6:00 AM 的分钟数
                int minutesPassed = totalMinutesInDay - 360; 
                // 'minutesPassedSinceOpen' 是距 9:00 AM 的分钟数
                int minutesPassedSinceOpen = minutesPassed - 180; // 180 = 9:00 AM

                // K线索引基于 9:00 AM (0) 开始
                int kLineIndex = (int)Math.Round(minutesPassedSinceOpen / 10.0);
                kLineIndex = Math.Clamp(kLineIndex, 0, DailyKLineData.Count - 1);

                double basePrice = DailyKLineData[kLineIndex];
                double noise = (_random.NextDouble() * 2.0 - 1.0) * NOISE_FACTOR;
                double finalPrice = basePrice + _marketImbalance + noise;

                this.CurrentPrice = Math.Round(Math.Max(1.0, finalPrice), 2);
                _lastCalculatedPrice = this.CurrentPrice; // 存储最后一个计算的价格
            }
            else if (CurrentStatus == MarketStatus.Waiting)
            {
                // 市场等待开盘，价格应固定为 9:00 AM 的开盘价
                this.CurrentPrice = Math.Round(DailyKLineData[0], 2);
                _lastCalculatedPrice = this.CurrentPrice; // 确保收盘价也被重置
            }
            else // (CurrentStatus == MarketStatus.Closed)
            {
                // 市场已收盘，价格应冻结在最后一个计算值 (收盘价)
                this.CurrentPrice = _lastCalculatedPrice;
            }

            // --- Risk Management Calculations ---
            UpdateUsedMargin();
            double unrealizedPl = OpenPositions.Sum(p => (p.IsLong ? 1 : -1) * (CurrentPrice - p.EntryPrice) * p.Contracts);
            AccountEquity = TradingAccountBalance + unrealizedPl;

            // --- Liquidation Logic ---
            if (UsedMargin > 0 && AccountEquity < UsedMargin)
            {
                // Liquidation
                OpenPositions.Clear();
                TradingAccountBalance = 0; // Or some other penalty
                _monitor.Log("!!! LIQUIDATION EVENT !!!", LogLevel.Alert);
            }
        }

        public void ExecuteTradeCommand(string commandJson)
        {
            try
            {
                var command = JsonConvert.DeserializeObject<TradeCommand>(commandJson);
                if (command == null) return;

                switch (command.Action.ToUpper())
                {
                    case "OPEN":
                        if (CurrentStatus != MarketStatus.Open) return;
                        OpenPosition(command.Amount, command.IsLong);
                        break;
                    case "CLOSE":
                        if (CurrentStatus != MarketStatus.Open) return;
                        if (command.PositionId.HasValue)
                        {
                            ClosePosition(command.PositionId.Value);
                        }
                        break;
                    case "DEPOSIT":
                        Deposit(command.Value);
                        break;
                    case "WITHDRAW":
                        Withdraw(command.Value);
                        break;
                }
            }
            catch (Exception ex)
            {
                _monitor.Log($"Failed to execute trade command: {ex.Message}", LogLevel.Error);
            }
        }

        private void OpenPosition(int contracts, bool isLong)
        {
            if (contracts <= 0) return;

            double marginRequired = (CurrentPrice * contracts) / LEVERAGE;
            double commission = CurrentPrice * contracts * COMMISSION_RATE;

            if (FreeMargin >= marginRequired + commission)
            {
                TradingAccountBalance -= commission;

                var newPosition = new PlayerPosition
                {
                    PositionId = Guid.NewGuid(),
                    IsLong = isLong,
                    EntryPrice = CurrentPrice,
                    Contracts = contracts,
                    MarginUsed = marginRequired
                };
                OpenPositions.Add(newPosition);
                UpdateUsedMargin();
                ApplyPlayerTrade(isLong ? contracts : -contracts); // Market impact
                _monitor.Log($"Opened position: {(isLong ? "LONG" : "SHORT")} {contracts} @ {CurrentPrice:F2}", LogLevel.Info);
            }
            else
            {
                _monitor.Log("Not enough free margin to open position.", LogLevel.Warn);
            }
        }

        private void ClosePosition(Guid positionId)
        {
            var positionToClose = OpenPositions.FirstOrDefault(p => p.PositionId == positionId);
            if (positionToClose != null)
            {
                double pnl = (positionToClose.IsLong ? 1 : -1) * (CurrentPrice - positionToClose.EntryPrice) * positionToClose.Contracts;
                double commission = CurrentPrice * positionToClose.Contracts * COMMISSION_RATE;

                TradingAccountBalance += positionToClose.MarginUsed + pnl - commission;
                OpenPositions.Remove(positionToClose);
                UpdateUsedMargin();
                ApplyPlayerTrade(positionToClose.IsLong ? -positionToClose.Contracts : positionToClose.Contracts); // Market impact
                _monitor.Log($"Closed position {positionId} for a P/L of {pnl:F2}", LogLevel.Info);
            }
        }

        private void Deposit(double amount)
        {
            if (amount <= 0)
            {
                _monitor.Log($"Invalid deposit amount: {amount}. Must be positive.", LogLevel.Warn);
                return;
            }
            if (Game1.player.Money < amount)
            {
                _monitor.Log($"Not enough money to deposit. Player has {Game1.player.Money}, tried to deposit {amount}.", LogLevel.Warn);
                return;
            }

            Game1.player.Money -= (int)amount;
            TradingAccountBalance += amount;
            _monitor.Log($"Player deposited {amount:F2}g. New balance: {TradingAccountBalance:F2}", LogLevel.Info);
        }

        private void Withdraw(double amount)
        {
            if (amount <= 0)
            {
                _monitor.Log($"Invalid withdraw amount: {amount}. Must be positive.", LogLevel.Warn);
                return;
            }
            if (FreeMargin < amount) // Use FreeMargin to prevent withdrawing funds that are holding positions
            {
                _monitor.Log($"Not enough free margin to withdraw. Free margin is {FreeMargin}, tried to withdraw {amount}.", LogLevel.Warn);
                return;
            }

            TradingAccountBalance -= amount;
            Game1.player.Money += (int)amount;
            _monitor.Log($"Player withdrew {amount:F2}g. New balance: {TradingAccountBalance:F2}", LogLevel.Info);
        }
    }
}
