using System;
using StardewCapital.Core.Time;
using StardewCapital.Services;
using StardewCapital.UI;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;

namespace StardewCapital
{
    public class ModEntry : Mod
    {
        private MarketManager _marketManager = null!;
        private MixedTimeClock _clock = null!;
        private StardewTimeProvider _timeProvider = null!;
        private PriceEngine _priceEngine = null!;

        private WebServer _webServer = null!;
        private ModConfig _config = null!;
        private BrokerageService _brokerageService = null!;
        private PersistenceService _persistenceService = null!;
        private DeliveryService _deliveryService = null!;

        public override void Entry(IModHelper helper)
        {
            // 0. Load Config
            _config = helper.ReadConfig<ModConfig>();

            // 1. Initialize Core Services
            _timeProvider = new StardewTimeProvider(_config);
            _clock = new MixedTimeClock(_timeProvider, _config);
            _priceEngine = new PriceEngine(_clock);
            
            // 2. Initialize Market Manager
            _marketManager = new MarketManager(Monitor, _clock, _priceEngine);

            // 3. Initialize Brokerage Service
            _brokerageService = new BrokerageService(_marketManager, Monitor);

            // 4. Initialize Persistence & Delivery
            _persistenceService = new PersistenceService(helper, Monitor, _brokerageService);
            var exchangeService = new ExchangeService();
            var exchangeMenuController = new ExchangeMenuController(helper, Monitor, exchangeService);
            _deliveryService = new DeliveryService(Monitor, _brokerageService, _marketManager, exchangeService);

            // 5. Initialize Web Server
            _webServer = new WebServer(Monitor, _marketManager, helper.DirectoryPath);
            _webServer.Start();

            // Events
            helper.Events.GameLoop.UpdateTicked += OnUpdateTicked;
            helper.Events.Input.ButtonPressed += OnButtonPressed;
            helper.Events.GameLoop.SaveLoaded += OnSaveLoaded;
            helper.Events.GameLoop.Saving += OnSaving;
            helper.Events.GameLoop.DayStarted += OnDayStarted;
            helper.Events.GameLoop.DayEnding += OnDayEnding;
            
            Monitor.Log("HedgeHarvest Market Initialized!", LogLevel.Info);
        }

        private void OnDayEnding(object? sender, DayEndingEventArgs e)
        {
            _deliveryService.ProcessDailyDelivery();
        }

        private void OnDayStarted(object? sender, DayStartedEventArgs e)
        {
            _marketManager.OnNewDay();
        }

        private void OnSaveLoaded(object? sender, SaveLoadedEventArgs e)
        {
            _persistenceService.LoadData();
            _marketManager.InitializeMarket();
        }

        private void OnSaving(object? sender, SavingEventArgs e)
        {
            _persistenceService.SaveData();
        }

        private void OnButtonPressed(object? sender, ButtonPressedEventArgs e)
        {
            // Toggle menu with F10
            if (e.Button == SButton.F10)
            {
                if (Game1.activeClickableMenu is UI.TradingMenu)
                {
                    Game1.activeClickableMenu = null;
                }
                else if (Context.IsPlayerFree)
                {
                    Game1.activeClickableMenu = new UI.TradingMenu(_marketManager, _brokerageService, Monitor);
                }
            }
        }

        private void OnUpdateTicked(object? sender, UpdateTickedEventArgs e)
        {
            // Only run if the world is loaded
            if (!Context.IsWorldReady) return;

            // Pass the tick count to the manager
            _marketManager.Update((int)e.Ticks);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _webServer?.Stop();
            }
            base.Dispose(disposing);
        }
    }
}
