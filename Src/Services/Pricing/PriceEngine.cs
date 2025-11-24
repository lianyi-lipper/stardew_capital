using System;
using System.Collections.Generic;
using StardewCapital.Core.Math;
using StardewCapital.Core.Time;
using StardewCapital.Domain.Instruments;
using StardewCapital.Config;

namespace StardewCapital.Services.Pricing
{
    /// <summary>
    /// 价格引擎
    /// 负责驱动金融产品的价格变化，调用Core层的数学模型。
    /// 
    /// 价格模型：
    /// - 模型2（GBM）：计算每日目标价格，具有均值回归特性
    /// - 模型3（持有成本）：计算期货价格，基于现货价格和持有成本
    /// - 模型4（布朗桥）：计算日内tick价格，确保收敛到目标价
    /// </summary>
    public class PriceEngine
    {
        private readonly MixedTimeClock _clock;
        private readonly MarketRules _rules;
        private readonly Random _random = new Random();

        // 配置参数（未来可移至配置文件）
        /// <summary>基础日波动率：控制每日价格波动幅度（2%左右）</summary>
        private const double BASE_VOLATILITY = 0.02;
        
        /// <summary>日内波动率：控制tick间价格噪声大小（0.5%左右）</summary>
        private const double INTRA_VOLATILITY = 0.005;

        public PriceEngine(MixedTimeClock clock, MarketRules rules)
        {
            _clock = clock;
            _rules = rules;
        }

        /// <summary>
        /// Updates the price of an instrument for the current tick.
        /// Uses Model 4 (Brownian Bridge) for intraday movement.
        /// </summary>
        /// <param name="instrument">要更新的金融产品</param>
        /// <param name="targetPrice">目标价格</param>
        /// <param name="timeStep">这一帧的时间步长（当前帧 timeRatio - 上一帧 timeRatio）</param>
        public void UpdatePrice(IInstrument instrument, double targetPrice, double timeStep)
        {
            double currentPrice = instrument.CurrentPrice;
            double timeRatio = _clock.GetDayProgress();

            // Calculate next tick price using Brownian Bridge
            double nextPrice = BrownianBridge.CalculateNextTickPrice(
                currentPrice, 
                targetPrice, 
                timeRatio,
                timeStep,  // 动态时间步长
                INTRA_VOLATILITY
            );

            // Ensure price doesn't go negative
            instrument.CurrentPrice = System.Math.Max(0.01, nextPrice);
        }
        public double CalculateDailyTarget(double currentPrice, double fundamentalValue, int daysToMaturity)
        {
            return GBM.CalculateNextPrice(
                currentPrice, 
                fundamentalValue, 
                daysToMaturity, 
                BASE_VOLATILITY
            );
        }

        /// <summary>
        /// 计算期货价格（模型三：持有成本模型）
        /// </summary>
        /// <param name="spotPrice">现货价格（S_t），即基本面价值</param>
        /// <param name="daysToMaturity">距离交割日的天数（τ）</param>
        /// <param name="convenienceYield">便利收益率（q），动态值，取决于具体物品</param>
        /// <returns>期货价格（F_t）</returns>
        /// <remarks>
        /// 公式：F_t = S_t × e^((r + φ - q) × τ)
        /// 
        /// 参数说明：
        /// - r (RiskFreeRate): 无风险利率，资金的时间价值
        /// - φ (StorageCost): 仓储成本，持有现货的腐败/损耗
        /// - q (ConvenienceYield): 便利收益率，持有现货的好处（送礼/烹饪/任务）
        /// - τ (DaysToMaturity): 距离交割日的天数
        /// 
        /// 经济含义：
        /// - 如果 r + φ > q: 期货价格 > 现货价格（Contango 升水）
        /// - 如果 r + φ < q: 期货价格 < 现货价格（Backwardation 贴水）
        /// 
        /// 示例：
        /// - 无NPC生日: q = 0.001, F_t > S_t (期货升水)
        /// - NPC生日加成: q = 0.101, F_t < S_t (期货贴水，持有现货价值高)
        /// </remarks>
        public double CalculateFuturesPrice(
            double spotPrice,
            int daysToMaturity,
            double convenienceYield)
        {
            // 从配置文件读取市场参数
            double r = _rules.Macro.RiskFreeRate;      // 无风险利率
            double phi = _rules.Instruments.Futures.StorageCost;     // 仓储成本
            double q = convenienceYield;          // 便利收益率（动态）

            // 计算指数部分：(r + φ - q) × τ
            double exponent = (r + phi - q) * daysToMaturity;

            // 应用公式：F_t = S_t × e^exponent
            double futuresPrice = spotPrice * Math.Exp(exponent);

            return futuresPrice;
        }
    }
}
