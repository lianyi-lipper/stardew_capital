using System;
using StardewCapital.Config;
using StardewCapital.Core.Models;
using StardewCapital.Domain.Instruments;
using StardewCapital.Domain.Market;
using StardewCapital.Domain.Market.MarketState;
using StardewCapital.Services.Adapters;
using StardewCapital.Services.News;
using StardewCapital.Services.Pricing;
using StardewModdingAPI;

namespace StardewCapital.Services.Pricing.Generators
{
    /// <summary>
    /// 期货影子价格生成器（重构版）
    /// 现在委托给独立计算器，通过适配器桥接
    /// </summary>
    public class FuturesShadowGenerator : IMarketDataGenerator
    {
        private readonly GamePriceCalculatorAdapter _adapter;
        private readonly IMonitor _monitor;
        
        public FuturesShadowGenerator(
            ModConfig config,
            NewsGenerator newsGenerator,
            FundamentalEngine fundamentalEngine,
            MarketRules rules,
            IMonitor monitor)
        {
            _monitor = monitor;
            
            // 创建日志回调
            var log = SmapiLoggerAdapter.CreateLogCallback(monitor);
            
            // 创建适配器
            _adapter = new GamePriceCalculatorAdapter(
                fundamentalEngine,
                newsGenerator,
                config,
                rules,
                log
            );
        }

        /// <summary>
        /// 生成期货市场状态
        /// </summary>
        public IMarketState Generate(IInstrument instrument, Season season, int year)
        {
            if (instrument is not CommodityFutures futures)
                throw new ArgumentException("FuturesShadowGenerator only supports CommodityFutures");

            _monitor.Log($"[FuturesShadowGenerator] Generating market state for {futures.CommodityName} (Season: {season}, Year: {year})", LogLevel.Info);
            
            // 委托给适配器
            return _adapter.GenerateMarketState(futures, season, year);
        }
    }
}
