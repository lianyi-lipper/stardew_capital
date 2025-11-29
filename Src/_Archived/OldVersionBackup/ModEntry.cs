// ModEntry.cs

using System;
using Microsoft.Xna.Framework; // <-- 确保这个 using 存在
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using HarmonyLib;
using System.Reflection;
using Newtonsoft.Json;
using System.Collections.Generic;

namespace StardewCapital
{
    public class ModEntry : Mod
    {
        private FuturesMarket _futuresMarket;
        private MarketDataServer _server;
        private int _tickCounter = 0;
        private const int REAL_TIME_TICK_INTERVAL = 42; // Approx. 0.7 seconds at 60fps
        private string _currentSeason = "";

        public override void Entry(IModHelper helper)
        {
            _futuresMarket = new FuturesMarket(helper, this.Monitor);
            _server = new MarketDataServer(this.Monitor);

            helper.Events.GameLoop.GameLaunched += (sender, args) => _server.Start();
            helper.Events.Input.ButtonPressed += this.OnButtonPressed;
            helper.Events.GameLoop.UpdateTicked += this.OnUpdateTicked;
            helper.Events.GameLoop.SaveLoaded += this.OnSaveLoaded;
            helper.Events.GameLoop.Saving += this.OnSaving;
            helper.Events.GameLoop.DayStarted += this.OnDayStarted;

            // --- 在这里应用 Harmony 补丁 ---
            this.ApplyHarmonyPatch();
        }

        protected override void Dispose(bool disposing)
        {
            _server.Stop();
            base.Dispose(disposing);
        }

        // --- [修改] 这个方法现在会应用两个补丁 ---
        private void ApplyHarmonyPatch()
        {
            try
            {
                var harmony = new Harmony(this.ModManifest.UniqueID);
                TimePatch.Initialize(this.Monitor);

                // --- 补丁 1: Game1.shouldTimePass() ---
                // (影响UI，以及让补丁2的检查生效)
                this.Monitor.Log("Applying patch 1: Game1.shouldTimePass...", LogLevel.Info);
                harmony.Patch(
                    original: AccessTools.Method(typeof(Game1), nameof(Game1.shouldTimePass)),
                    postfix: new HarmonyMethod(AccessTools.Method(typeof(TimePatch), nameof(TimePatch.ShouldTimePass_Postfix)))
                );

                // --- 补丁 2: Game1._update() ---
                // (真正让时钟走动)
                this.Monitor.Log("Applying patch 2: Game1._update...", LogLevel.Info);
                harmony.Patch(
                    original: AccessTools.Method(typeof(Game1), "_update"), // _update 是私有方法
                    postfix: new HarmonyMethod(AccessTools.Method(typeof(TimePatch), nameof(TimePatch.Update_Postfix)))
                );
                
                this.Monitor.Log("Harmony patches applied successfully.", LogLevel.Info);
            }
            catch (Exception ex)
            {
                this.Monitor.Log($"Failed to apply Harmony patch:\n{ex}", LogLevel.Error);
            }
        }

        private void OnSaving(object sender, SavingEventArgs e)
        {
            // Save Trading Account Balance
            string balanceKey = $"{this.ModManifest.UniqueID}_AccountBalance";
            Game1.player.modData[balanceKey] = _futuresMarket.TradingAccountBalance.ToString();
            this.Monitor.Log($"Saving futures account balance: {_futuresMarket.TradingAccountBalance}", LogLevel.Info);

            // Save Open Positions
            string positionsKey = $"{this.ModManifest.UniqueID}_OpenPositions";
            string positionsJson = JsonConvert.SerializeObject(_futuresMarket.OpenPositions);
            Game1.player.modData[positionsKey] = positionsJson;
            this.Monitor.Log($"Saving open positions: {positionsJson}", LogLevel.Info);
        }

        private void OnSaveLoaded(object sender, SaveLoadedEventArgs e)
        {
            // Load Trading Account Balance
            string balanceKey = $"{this.ModManifest.UniqueID}_AccountBalance";
            if (Game1.player.modData.ContainsKey(balanceKey))
            {
                if (double.TryParse(Game1.player.modData[balanceKey], out double savedBalance))
                {
                    _futuresMarket.TradingAccountBalance = savedBalance;
                    this.Monitor.Log($"Loaded futures account balance: {savedBalance}", LogLevel.Info);
                }
            }

            // Load Open Positions
            string positionsKey = $"{this.ModManifest.UniqueID}_OpenPositions";
            if (Game1.player.modData.ContainsKey(positionsKey))
            {
                string positionsJson = Game1.player.modData[positionsKey];
                var positions = JsonConvert.DeserializeObject<List<PlayerPosition>>(positionsJson);
                if (positions != null)
                {
                    _futuresMarket.OpenPositions = positions;
                    this.Monitor.Log($"Loaded open positions: {positionsJson}", LogLevel.Info);
                }
            }

            // Initialize the market when the game is first loaded
            _currentSeason = Game1.currentSeason;
            _futuresMarket.InitializeNewSeason();
        }

        private void OnDayStarted(object sender, DayStartedEventArgs e)
        {
            // Re-initialize the market only if the season has changed
            if (_currentSeason != Game1.currentSeason)
            {
                _currentSeason = Game1.currentSeason;
                _futuresMarket.InitializeNewSeason();
            }
            _futuresMarket.ApplyDailyNews();
        }

        private void OnUpdateTicked(object sender, UpdateTickedEventArgs e)
        {
            // --- [修复] 1. 优先处理所有待处理的交易指令 ---
            // 这会立刻更新你的持仓、保证金和余额
            // 如果是 CLOSE 指令, 会在这里被执行, 仓位被移除
            while (_server.CommandQueue.TryDequeue(out string commandJson))
            {
                _futuresMarket.ExecuteTradeCommand(commandJson);
            }

            _tickCounter++;
            if (_tickCounter >= REAL_TIME_TICK_INTERVAL)
            {
                _tickCounter = 0;

                // --- 2. 在处理完交易后，再更新市场价格和执行风险检查 ---
                // 此时如果仓位已平, UpdateUsedMargin() 会得到 0, 强平检查就不会触发
                _futuresMarket.UpdateMarketPrice();

                // --- 3. 广播最新的状态 ---
                var state = new MarketStateDto
                {
                    ContractName = _futuresMarket.ContractName,
                    DailyNews = _futuresMarket.DailyNews,
                    MarketStatus = _futuresMarket.CurrentStatus,
                    CurrentPrice = _futuresMarket.CurrentPrice,
                    AccountEquity = _futuresMarket.AccountEquity,
                    FreeMargin = _futuresMarket.FreeMargin,
                    OpenPositions = _futuresMarket.OpenPositions
                };
                string jsonState = JsonConvert.SerializeObject(state);
                _server.Broadcast(jsonState);
            }
        }

        private void OnButtonPressed(object sender, ButtonPressedEventArgs e)
        {
            if (!Context.IsWorldReady)
                return;

            if (e.Button == SButton.O)
            {
                this.Monitor.Log("O key was pressed!", LogLevel.Info);

                if (Game1.player.hasOrWillReceiveMail("JojaMember") && Game1.player.Money > 100)
                {
                    if (Game1.activeClickableMenu == null)
                    {
                        this.Monitor.Log("Conditions met. Opening custom menu.", LogLevel.Info);
                        Game1.activeClickableMenu = new StardewCapitalMenu(_futuresMarket); // Pass the market instance
                    }
                }
                else
                {
                    this.Monitor.Log("Conditions not met. Player is not a Joja member or doesn't have enough money.", LogLevel.Info);
                }
            }
        }
    }
}