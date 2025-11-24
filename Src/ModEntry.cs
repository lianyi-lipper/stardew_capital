using System;
using StardewCapital.Core.Time;
using StardewCapital.Services.Market;
using StardewCapital.Services.Pricing;
using StardewCapital.Services.Trading;
using StardewCapital.Services.News;
using StardewCapital.Services.Infrastructure;
using StardewCapital.UI;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;

namespace StardewCapital
{
    /// <summary>
    /// StardewCapital Mod 主入口
    /// 负责初始化所有服务、注册事件处理器、管理Mod生命周期。
    /// 
    /// 架构分层：
    /// - Core层：纯数学引擎（GBM、布朗桥、时间系统）
    /// - Domain层：金融实体（期货、账户、仓位）
    /// - Services层：业务逻辑（市场管理、交易执行、交割处理）
    /// - UI层：用户界面（交易菜单、图表显示）
    /// </summary>
    public class ModEntry : Mod
    {
        private MarketManager _marketManager = null!;
        private MixedTimeClock _clock = null!;
        private StardewTimeProvider _timeProvider = null!;
        private PriceEngine _priceEngine = null!;
        private FundamentalEngine _fundamentalEngine = null!;
        private ConvenienceYieldService _convenienceYieldService = null!;
        private NewsGenerator _newsGenerator = null!;
        private ScenarioManager _scenarioManager = null!;
        private ImpactService _impactService = null!;

        private WebServer _webServer = null!;
        private ModConfig _config = null!;
        private BrokerageService _brokerageService = null!;
        private PersistenceService _persistenceService = null!;
        private DeliveryService _deliveryService = null!;

        /// <summary>
        /// Mod入口方法
        /// SMAPI在加载Mod时自动调用，负责初始化所有组件并注册事件。
        /// 
        /// 初始化顺序：
        /// 1. 加载配置文件
        /// 2. 初始化Core服务（时间系统、价格引擎）
        /// 3. 初始化业务服务（市场、交易、存档、交割）
        /// 4. 启动Web服务器（可选）
        /// 5. 注册游戏事件处理器
        /// </summary>
        /// <param name="helper">SMAPI提供的Helper接口</param>
        public override void Entry(IModHelper helper)
        {
            // 0. Load Config
            _config = helper.ReadConfig<ModConfig>();
            
            // Load Market Rules (New Architecture) - Moved up for dependency injection
            var marketRules = helper.Data.ReadJsonFile<Config.MarketRules>("Assets/market_rules.json") ?? new Config.MarketRules();
            Monitor.Log($"[MarketRules] Loaded. RiskFreeRate={marketRules.Macro.RiskFreeRate}, Decay={marketRules.MarketMicrostructure.DecayRate}", LogLevel.Info);

            // 1. Initialize Core Services
            _timeProvider = new StardewTimeProvider(_config);
            _clock = new MixedTimeClock(_timeProvider, _config);
            _priceEngine = new PriceEngine(_clock, marketRules);
            _fundamentalEngine = new FundamentalEngine(Monitor, Helper);
            _convenienceYieldService = new ConvenienceYieldService(Monitor);
            _newsGenerator = new NewsGenerator(helper, Monitor);
            
            // 1.5 Initialize Impact System (Phase 9)
            _scenarioManager = new ScenarioManager(Monitor);
            _impactService = new ImpactService(Monitor);
            
            // Configure Impact Service using Market Rules
            _impactService.Configure(marketRules.MarketMicrostructure);
            _scenarioManager.SetSwitchProbability(marketRules.MarketMicrostructure.ScenarioSwitchProbability);
            
            // 2. Initialize Market Manager (with new architecture)
            var orderBookManager = new OrderBookManager(Monitor, _impactService, null!); // MarketManager set later
            var priceUpdater = new MarketPriceUpdater(
                Monitor, _clock, _priceEngine, _fundamentalEngine,
                _convenienceYieldService, _newsGenerator,
                _impactService, _scenarioManager, _config, 
                null!, // MarketManager (arg 10)
                orderBookManager, // OrderBookManager (arg 11)
                marketRules); // MarketRules (arg 12)
            
            _marketManager = new MarketManager(Monitor, orderBookManager, priceUpdater, _config);
            
            // Fix circular references using reflection
            var orderBookManagerField = typeof(OrderBookManager).GetField("_marketManager", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            orderBookManagerField?.SetValue(orderBookManager, _marketManager);
            
            var priceUpdaterField = typeof(MarketPriceUpdater).GetField("_marketManager",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            priceUpdaterField?.SetValue(priceUpdater, _marketManager);

            // 3. Initialize Brokerage Service
            _brokerageService = new BrokerageService(_marketManager, _impactService, Monitor);
            
            // ✅ 重要：设置 BrokerageService 引用，用于订单结算回调
            // 必须在 _marketManager.InitializeMarket() 之前调用，确保订单簿事件正确订阅
            _marketManager.SetBrokerageService(_brokerageService);

            // 4. Initialize Persistence & Delivery
            _persistenceService = new PersistenceService(helper, Monitor, _brokerageService);
            var exchangeService = new ExchangeService();
            var exchangeMenuController = new ExchangeMenuController(helper, Monitor, exchangeService);
            _deliveryService = new DeliveryService(Monitor, _brokerageService, _marketManager, exchangeService);

            // 5. Initialize Web Server
            _webServer = new WebServer(Monitor, _marketManager, _brokerageService, helper.DirectoryPath);
            _webServer.Start();

            // Events
            helper.Events.GameLoop.UpdateTicked += OnUpdateTicked;
            helper.Events.Input.ButtonPressed += OnButtonPressed;
            helper.Events.GameLoop.SaveLoaded += OnSaveLoaded;
            helper.Events.GameLoop.Saving += OnSaving;
            helper.Events.GameLoop.DayStarted += OnDayStarted;
            helper.Events.GameLoop.DayEnding += OnDayEnding;
            
            Monitor.Log("StardewCapital Market Initialized!", LogLevel.Info);
        }

        /// <summary>
        /// 处理每日结束事件
        /// 触发期货合约的实物交割检查
        /// </summary>
        private void OnDayEnding(object? sender, DayEndingEventArgs e)
        {
            _deliveryService.ProcessDailyDelivery();
        }

        /// <summary>
        /// 处理新一天开始事件
        /// 触发市场的日更新（计算新的目标价格）
        /// </summary>
        private void OnDayStarted(object? sender, DayStartedEventArgs e)
        {
            _marketManager.OnNewDay();
        }

        /// <summary>
        /// 处理存档加载事件
        /// 恢复玩家的交易账户和市场数据
        /// </summary>
        private void OnSaveLoaded(object? sender, SaveLoadedEventArgs e)
        {
            _persistenceService.LoadData();
            _marketManager.InitializeMarket();
        }

        /// <summary>
        /// 处理游戏存档事件
        /// 保存玩家的交易账户数据
        /// </summary>
        private void OnSaving(object? sender, SavingEventArgs e)
        {
            _persistenceService.SaveData();
        }

        /// <summary>
        /// 处理按键事件
        /// F10键：打开/关闭交易菜单
        /// </summary>
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
                    Game1.activeClickableMenu = new UI.TradingMenu(_marketManager, _brokerageService, _scenarioManager, _impactService, Monitor);
                }
            }
        }

        /// <summary>
        /// 处理游戏每帧更新事件
        /// 驱动市场价格的实时更新
        /// </summary>
        private void OnUpdateTicked(object? sender, UpdateTickedEventArgs e)
        {
            // Only run if the world is loaded
            if (!Context.IsWorldReady) return;

            // Pass the tick count to the manager
            _marketManager.Update((int)e.Ticks);
        }

        /// <summary>
        /// 资源清理方法
        /// 确保Web服务器在Mod卸载时正确关闭
        /// </summary>
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
