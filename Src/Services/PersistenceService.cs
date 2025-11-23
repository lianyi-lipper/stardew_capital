using StardewCapital.Data;
using StardewCapital.Services;
using StardewModdingAPI;

namespace StardewCapital.Services
{
    /// <summary>
    /// 持久化服务
    /// 负责交易数据的存档和读档，使用SMAPI的存档系统。
    /// 
    /// 存档内容：
    /// - 玩家的现金余额
    /// - 所有开仓的交易仓位
    /// 
    /// 未来扩展：
    /// - 历史K线数据
    /// - 最后已知价格
    /// </summary>
    public class PersistenceService
    {
        private readonly IModHelper _helper;
        private readonly IMonitor _monitor;
        private readonly BrokerageService _brokerageService;

        /// <summary>SMAPI存档数据的唯一键</summary>
        private const string SAVE_KEY = "StardewCapital-SaveData";

        public PersistenceService(IModHelper helper, IMonitor monitor, BrokerageService brokerageService)
        {
            _helper = helper;
            _monitor = monitor;
            _brokerageService = brokerageService;
        }

        /// <summary>
        /// 保存交易数据到存档
        /// 在游戏存档时自动调用
        /// </summary>
        public void SaveData()
        {
            var model = new SaveModel
            {
                Cash = _brokerageService.Account.Cash,
                Positions = _brokerageService.Account.Positions
            };

            _helper.Data.WriteSaveData(SAVE_KEY, model);
            _monitor.Log("Saved trading data.", LogLevel.Trace);
        }

        /// <summary>
        /// 从存档加载交易数据
        /// 在游戏读档时自动调用
        /// </summary>
        public void LoadData()
        {
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
        }
    }
}
