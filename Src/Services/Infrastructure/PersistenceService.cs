using StardewCapital.Data;
using StardewCapital.Data.SaveData;
using StardewCapital.Services.Trading;
using StardewCapital.Services.Market;
using StardewModdingAPI;

namespace StardewCapital.Services.Infrastructure
{
    /// <summary>
    /// 持久化服务
    /// 负责交易数据和市场状态的存档和读档，使用SMAPI的存档系统。
    /// 
    /// 存档内容：
    /// - 玩家的现金余额
    /// - 所有开仓的交易仓位
    /// - 市场状态（预计算的价格和新闻）
    /// </summary>
    public class PersistenceService
    {
        private readonly IModHelper _helper;
        private readonly IMonitor _monitor;
        private readonly BrokerageService _brokerageService;
        private readonly MarketStateManager _marketStateManager;

        /// <summary>SMAPI存档数据的唯一键</summary>
        private const string SAVE_KEY = "StardewCapital-SaveData";
        private const string MARKET_STATE_KEY = "StardewCapital-MarketState";

        public PersistenceService(
            IModHelper helper, 
            IMonitor monitor, 
            BrokerageService brokerageService,
            MarketStateManager marketStateManager)
        {
            _helper = helper;
            _monitor = monitor;
            _brokerageService = brokerageService;
            _marketStateManager = marketStateManager;
        }

        /// <summary>
        /// 保存交易数据和市场状态到存档
        /// 在游戏存档时自动调用
        /// </summary>
        public void SaveData()
        {
            // 1. 保存账户数据
            var model = new SaveModel
            {
                Cash = _brokerageService.Account.Cash,
                Positions = _brokerageService.Account.Positions
            };

            _helper.Data.WriteSaveData(SAVE_KEY, model);
            _monitor.Log("Saved trading data.", LogLevel.Trace);
            
            // 2. 保存市场状态数据
            if (_marketStateManager.IsInitialized())
            {
                var marketStateData = _marketStateManager.ExportSaveData();
                _helper.Data.WriteSaveData(MARKET_STATE_KEY, marketStateData);
                _monitor.Log($"Saved market state for season {marketStateData.CurrentSeason}.", LogLevel.Trace);
            }
        }

        /// <summary>
        /// 从存档加载交易数据和市场状态
        /// 在游戏读档时自动调用
        /// </summary>
        public void LoadData()
        {
            // 1. 加载账户数据
            var model = _helper.Data.ReadSaveData<SaveModel>(SAVE_KEY);
            if (model != null)
            {
                _brokerageService.LoadAccount(model.Cash, model.Positions);
                _monitor.Log($"Loaded trading data. Cash: {model.Cash}g, Positions: {model.Positions.Count}", LogLevel.Info);
            }
            else
            {
                _monitor.Log("No save data found. Starting with new account.", LogLevel.Info);
            }
            
            // 2. 加载市场状态数据
            var marketStateData = _helper.Data.ReadSaveData<MarketStateSaveData>(MARKET_STATE_KEY);
            if (marketStateData != null)
            {
                _marketStateManager.ImportSaveData(marketStateData);
                _monitor.Log($"Loaded market state for season {marketStateData.CurrentSeason} day {marketStateData.CurrentDay}.", LogLevel.Info);
            }
            else
            {
                _monitor.Log("No market state save data found. Will initialize on season start.", LogLevel.Info);
            }
        }
    }
}
