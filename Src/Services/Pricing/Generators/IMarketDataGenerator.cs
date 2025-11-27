using StardewCapital.Domain.Instruments;
using StardewCapital.Domain.Market;
using StardewCapital.Domain.Market.MarketState;

namespace StardewCapital.Services.Pricing.Generators
{
    /// <summary>
    /// 市场数据生成器接口
    /// 负责为特定金融产品生成完整的市场状态（价格轨迹+事件）
    /// </summary>
    public interface IMarketDataGenerator
    {
        /// <summary>
        /// 生成市场状态
        /// </summary>
        /// <param name="instrument">金融产品</param>
        /// <param name="season">季节</param>
        /// <param name="year">年份</param>
        /// <returns>完整的市场状态（包含价格和事件）</returns>
        IMarketState Generate(IInstrument instrument, Season season, int year);
    }
}
