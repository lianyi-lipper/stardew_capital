using HedgeHarvest.Data;
using HedgeHarvest.Services;
using StardewModdingAPI;

namespace HedgeHarvest.Services
{
    public class PersistenceService
    {
        private readonly IModHelper _helper;
        private readonly IMonitor _monitor;
        private readonly BrokerageService _brokerageService;

        private const string SAVE_KEY = "HedgeHarvest-SaveData";

        public PersistenceService(IModHelper helper, IMonitor monitor, BrokerageService brokerageService)
        {
            _helper = helper;
            _monitor = monitor;
            _brokerageService = brokerageService;
        }

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
