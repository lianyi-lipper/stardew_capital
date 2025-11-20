using System;
using StardewCapital.Core.Time;
using StardewCapital.Services;
using StardewCapital.UI;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;

namespace StardewCapital
{
    /// <summary>
    /// HedgeHarvest Mod 主入口
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
                    Game1.activeClickableMenu = new UI.TradingMenu(_marketManager, _brokerageService, Monitor);
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
