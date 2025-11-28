using System;
using System.IO;
using System.Text.Json;
using StardewCapital.Data;
using StardewCapital.Data.SaveData;
using StardewCapital.Services.Trading;
using StardewCapital.Services.Market;
using StardewModdingAPI;

namespace StardewCapital.Services.Infrastructure
{
    /// <summary>
    /// 持久化服务
    /// 负责交易数据和市场状态的存档和读档，使用独立的JSON文件。
    /// 
    /// 存档内容：
    /// - 玩家的现金余额
    /// - 所有开仓的交易仓位
    /// - 市场状态（预计算的价格和新闻）
    /// 
    /// 存档位置：ModDirectory/Archives/
    /// </summary>
    public class PersistenceService
    {
        private readonly IModHelper _helper;
        private readonly IMonitor _monitor;
        private readonly BrokerageService _brokerageService;
        private readonly MarketStateManager _marketStateManager;

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
        /// 获取存档目录路径（延迟初始化）
        /// </summary>
        private string GetArchiveDirectory()
        {
            // 获取游戏存档文件夹路径
            // 路径例如: C:\Users\XXX\AppData\Roaming\StardewValley\Saves\FarmName_UniqueID\
            string saveFolderName = $"{StardewValley.Game1.GetSaveGameName()}_{StardewValley.Game1.uniqueIDForThisGame}";
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), 
                "StardewValley", "Saves", saveFolderName);
        }

        /// <summary>
        /// 保存交易数据和市场状态到独立的JSON文件
        /// 在游戏存档时自动调用
        /// </summary>
        public void SaveData()
        {
            try
            {
                string archiveDir = GetArchiveDirectory();
                
                // 1. 保存账户数据
                var accountModel = new SaveModel
                {
                    Cash = _brokerageService.Account.Cash,
                    Positions = _brokerageService.Account.Positions
                };

                string accountFileName = Path.Combine(archiveDir, "StardewCapital-SaveData.json");
                string accountJson = JsonSerializer.Serialize(accountModel, new JsonSerializerOptions 
                { 
                    WriteIndented = true 
                });
                File.WriteAllText(accountFileName, accountJson);
                _monitor.Log($"[PersistenceService] Saved account data to: {accountFileName}", LogLevel.Debug);
                
                // 2. 保存市场状态数据
                if (_marketStateManager.IsInitialized())
                {
                    var marketStateData = _marketStateManager.ExportSaveData();
                    string marketFileName = Path.Combine(archiveDir, "StardewCapital-MarketState.json");
                    
                    string marketJson = JsonSerializer.Serialize(marketStateData, new JsonSerializerOptions 
                    { 
                        WriteIndented = true 
                    });
                    File.WriteAllText(marketFileName, marketJson);
                    _monitor.Log($"[PersistenceService] Saved market state to: {marketFileName}", LogLevel.Info);
                }
            }
            catch (Exception ex)
            {
                _monitor.Log($"[PersistenceService] Error saving data: {ex.Message}", LogLevel.Error);
            }
        }

        /// <summary>
        /// 从独立的JSON文件加载交易数据和市场状态
        /// 在游戏读档时自动调用
        /// </summary>
        public void LoadData()
        {
            try
            {
                string archiveDir = GetArchiveDirectory();
                
                // 1. 加载账户数据
                string accountFileName = Path.Combine(archiveDir, "StardewCapital-SaveData.json");
                if (File.Exists(accountFileName))
                {
                    string accountJson = File.ReadAllText(accountFileName);
                    var model = JsonSerializer.Deserialize<SaveModel>(accountJson);
                    if (model != null)
                    {
                        _brokerageService.LoadAccount(model.Cash, model.Positions);
                        _monitor.Log($"[PersistenceService] Loaded account data. Cash: {model.Cash}g, Positions: {model.Positions.Count}", LogLevel.Info);
                    }
                }
                else
                {
                    _monitor.Log($"[PersistenceService] No account save data found. Starting with new account.", LogLevel.Info);
                }
                
                // 2. 加载市场状态数据
                string marketFileName = Path.Combine(archiveDir, "StardewCapital-MarketState.json");
                if (File.Exists(marketFileName))
                {
                    string marketJson = File.ReadAllText(marketFileName);
                    var marketStateData = JsonSerializer.Deserialize<MarketStateSaveData>(marketJson);
                    if (marketStateData != null)
                    {
                        _marketStateManager.ImportSaveData(marketStateData);
                        _monitor.Log($"[PersistenceService] Loaded market state for season {marketStateData.CurrentSeason} day {marketStateData.CurrentDay}", LogLevel.Info);
                    }
                }
                else
                {
                    _monitor.Log($"[PersistenceService] No market state save data found. Will initialize on season start.", LogLevel.Info);
                }
            }
            catch (Exception ex)
            {
                _monitor.Log($"[PersistenceService] Error loading data: {ex.Message}", LogLevel.Error);
            }
        }
    }
}
