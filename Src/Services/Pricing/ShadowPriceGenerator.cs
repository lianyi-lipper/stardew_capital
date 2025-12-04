using System;
using StardewCapital.Core.Futures.Math;
using StardewCapital.Core.Futures.Config;
using StardewCapital.Services.Infrastructure;

namespace StardewCapital.Services.Pricing
{
    /// <summary>
    /// 褰卞瓙浠锋牸鐢熸垚鍣?
    /// 璐熻矗鍦ㄥ搴?澶╁紑濮嬫椂棰勭敓鎴愬畬鏁翠环鏍艰建杩?
    /// </summary>
    public class ShadowPriceGenerator
    {
        private readonly ModConfig _config;
        private readonly StardewTimeProvider _timeProvider;
        private readonly News.NewsGenerator _newsGenerator;
        private readonly FundamentalEngine _fundamentalEngine;
        private readonly Random _random = new Random();

        public ShadowPriceGenerator(
            ModConfig config, 
            StardewTimeProvider timeProvider,
            News.NewsGenerator newsGenerator,
            FundamentalEngine fundamentalEngine)
        {
            _config = config;
            _timeProvider = timeProvider;
            _newsGenerator = newsGenerator;
            _fundamentalEngine = fundamentalEngine;
        }

        /// <summary>
        /// 鐢熸垚鍗曟棩褰卞瓙浠锋牸杞ㄨ抗锛堟棩鍐?0鍒嗛挓绾у埆锛?
        /// 浣跨敤甯冩湕妗ユā鍨嬬‘淇濅环鏍间粠 startPrice 骞虫粦杩囨浮鍒?targetPrice
        /// </summary>
        /// <param name="startPrice">寮€鐩樹环</param>
        /// <param name="targetPrice">鐩爣鏀剁洏浠?/param>
        /// <param name="intraVolatility">鏃ュ唴娉㈠姩鐜?(榛樿 0.02)</param>
        /// <returns>鍖呭惈鍏ㄥぉ姣?0鍒嗛挓浠锋牸鐨勬暟缁?/returns>
        public float[] GenerateDailyShadowPrices(
            double startPrice,
            double targetPrice,
            double intraVolatility = 0.02)
        {
            // 1. 璁＄畻鎬绘椂闂存鏁?
            int startMinutes = _timeProvider.ToMinutes(_config.OpeningTime);
            int endMinutes = _timeProvider.ToMinutes(_config.ClosingTime);
            int totalMinutes = endMinutes - startMinutes;
            
            // 姣?0鍒嗛挓涓€涓暟鎹偣
            // 渚嬪锛?:00 鍒?26:00 = 20灏忔椂 = 1200鍒嗛挓 = 120涓偣
            int steps = totalMinutes / 10; 

            // 纭繚鑷冲皯鏈?涓偣锛堣捣鐐瑰拰缁堢偣锛?
            if (steps < 2) steps = 2;

            float[] prices = new float[steps];
            
            // 2. 鍒濆鍖栬捣鐐?
            double currentPrice = startPrice;
            prices[0] = (float)currentPrice;

            // 3. 浣跨敤甯冩湕妗ョ敓鎴愬悗缁建杩?
            for (int i = 1; i < steps; i++)
            {
                // 涓婁竴姝ョ殑鏃堕棿杩涘害 (tau)
                double timeRatio = (double)(i - 1) / (steps - 1);
                
                // 鏃堕棿姝ラ暱 (dt)
                double timeStep = 1.0 / (steps - 1);

                // 璋冪敤鏍稿績鏁板妯″瀷璁＄畻涓嬩竴姝ヤ环鏍?
                double nextPrice = BrownianBridge.CalculateNextTickPrice(
                    currentPrice,
                    targetPrice,
                    timeRatio,
                    timeStep,
                    intraVolatility,
                    _random
                );

                // 纭繚浠锋牸闈炶礋
                nextPrice = System.Math.Max(0.01, nextPrice);
                
                prices[i] = (float)nextPrice;
                currentPrice = nextPrice;
            }

            return prices;
        }
        /// <summary>
        /// 鐢熸垚鏁翠釜瀛ｅ害鐨勫奖瀛愪环鏍艰建杩?
        /// 鍖呭惈28澶╃殑鎵€鏈夋棩鍐呮暟鎹偣锛屽苟妯℃嫙姣忔棩鏂伴椈
        /// </summary>
        /// <param name="commodityName">鍟嗗搧鍚嶇О</param>
        /// <param name="startPrice">瀛ｅ害鍒濅环鏍?/param>
        /// <param name="initialFundamentalValue">鍒濆鍩烘湰闈环鍊?/param>
        /// <param name="baseVolatility">鍩虹鏃ユ尝鍔ㄧ巼 (榛樿 0.02)</param>
        /// <param name="intraVolatility">鏃ュ唴娉㈠姩鐜?(榛樿 0.005)</param>
        /// <returns>鍖呭惈鍏ㄥ搴︽墍鏈変环鏍肩偣鐨勬暟缁?鍜?妯℃嫙鍑虹殑鏂伴椈鍒楄〃</returns>
        public (float[] Prices, List<StardewCapital.Core.Futures.Domain.Market.NewsEvent> ScheduledNews) GenerateSeasonalShadowPrices(
            string commodityName,
            double startPrice,
            double initialFundamentalValue,
            double baseVolatility = 0.02,
            double intraVolatility = 0.02)
        {
            // 1. 璁＄畻鎬绘暟鎹偣鏁?
            int startMinutes = _timeProvider.ToMinutes(_config.OpeningTime);
            int endMinutes = _timeProvider.ToMinutes(_config.ClosingTime);
            int stepsPerDay = (endMinutes - startMinutes) / 10;
            if (stepsPerDay < 2) stepsPerDay = 2;

            int totalDays = 28;
            float[] seasonalPrices = new float[totalDays * stepsPerDay];
            List<StardewCapital.Core.Futures.Domain.Market.NewsEvent> allScheduledNews = new List<StardewCapital.Core.Futures.Domain.Market.NewsEvent>();
            
            // 涓存椂缁存姢鐨勬柊闂荤姸鎬侊紙鐢ㄤ簬璁＄畻鍩烘湰闈級
            List<StardewCapital.Core.Futures.Domain.Market.NewsEvent> simulatedActiveNews = new List<StardewCapital.Core.Futures.Domain.Market.NewsEvent>();

            // 2. 閫愭棩妯℃嫙
            double currentDailyPrice = startPrice;
            double currentOpenPrice = startPrice;
            
            // 鑾峰彇褰撳墠瀛ｈ妭锛堝亣璁惧湪鐢熸垚鏃舵父鎴忓凡缁忓浜庤瀛ｈ妭锛屾垨鑰呴渶瑕佷紶鍏eason鍙傛暟锛?
            // 杩欓噷绠€鍖栧鐞嗭紝鍋囪鐢熸垚鐨勬槸褰撳墠娓告垙瀛ｈ妭鐨勮建杩?
            var currentSeason = StardewValley.Game1.currentSeason.ToLower() switch
            {
                "spring" => StardewCapital.Core.Futures.Domain.Market.Season.Spring,
                "summer" => StardewCapital.Core.Futures.Domain.Market.Season.Summer,
                "fall" => StardewCapital.Core.Futures.Domain.Market.Season.Fall,
                "winter" => StardewCapital.Core.Futures.Domain.Market.Season.Winter,
                _ => StardewCapital.Core.Futures.Domain.Market.Season.Spring
            };

            for (int day = 0; day < totalDays; day++)
            {
                int absoluteDay = day + 1; // 1-based day index for news generation
                
                // 2.1 妯℃嫙浠婃棩鏂伴椈
                // 娉ㄦ剰锛氳繖閲屾垜浠彧鍏冲績褰卞搷褰撳墠鍟嗗搧鐨勬柊闂伙紝鎴栬€呭叏灞€鏂伴椈
                // 涓轰簡鏁堢巼锛屾垜浠彲浠ュ彧鐢熸垚涓€娆″叏灞€鏂伴椈锛岀劧鍚庤繃婊ゃ€?
                // 浣嗙敱浜?ShadowPriceGenerator 鏄拡瀵瑰崟涓晢鍝佽皟鐢ㄧ殑锛屾垜浠渶瑕佺‘淇濇柊闂荤敓鎴愮殑涓€鑷存€с€?
                // 杩欐槸涓€涓綔鍦ㄩ棶棰橈細濡傛灉瀵规瘡涓晢鍝侀兘璋冪敤 NewsGenerator锛屽彲鑳戒細鐢熸垚涓嶄竴鑷寸殑鍏ㄥ眬鏂伴椈銆?
                // 瑙ｅ喅鏂规锛歂ewsGenerator 搴旇鍦ㄥ閮ㄨ皟鐢紝鐢熸垚鍏ㄥ搴︾殑鎵€鏈夋柊闂伙紝鐒跺悗浼犵粰杩欓噷锛?
                // 鎴栬€咃紝鎴戜滑鍦ㄨ繖閲屽彧鐢熸垚閽堝璇ュ晢鍝佺殑鐗瑰畾鏂伴椈锛?
                // 閴翠簬鐩墠鏋舵瀯锛屾垜浠湪 MarketManager 涓粺涓€鐢熸垚鍙兘鏇村ソ銆?
                // 浣嗕负浜嗛伒寰綋鍓嶄换鍔★紝鎴戜滑鍏堝湪杩欓噷鐢熸垚锛屽悗缁彲鑳介渶瑕侀噸鏋勪负鈥滃厛鐢熸垚鎵€鏈夋柊闂伙紝鍐嶇敓鎴愭墍鏈変环鏍尖€濄€?
                
                // 淇鏂规锛?
                // 涓轰簡閬垮厤涓嶅悓鍟嗗搧鐢熸垚涓嶄竴鑷寸殑鍏ㄥ眬鏂伴椈锛屾垜浠亣璁?NewsGenerator 鍦ㄨ繖閲岀敓鎴愮殑鍙槸鈥滈拡瀵硅鍟嗗搧鈥濈殑鐙珛浜嬩欢銆?
                // 鎴栬€咃紝鎴戜滑鎺ュ彈杩欎釜闄愬埗锛屽亣璁炬瘡涓晢鍝佺殑涓栫晫绾挎槸鐙珛鐨勶紙涓嶅お濂斤級銆?
                // 鏈€濂界殑鍔炴硶鏄細GenerateSeasonalShadowPrices 鎺ュ彈涓€涓鍏堢敓鎴愮殑鈥滃叏瀛ｅ害鏂伴椈鍒楄〃鈥濄€?
                
                // 浣嗙敤鎴疯姹傗€滀竴娆℃€фā鎷熷畬鏁翠釜瀛ｅ害鐨勪环鏍煎拰鏂伴椈鈥濄€?
                // 璁╂垜浠厛瀹炵幇鈥滆竟璧拌竟鐢熸垚鈥濓紝骞跺亣璁捐繖鏄拡瀵瑰崟涓€鍟嗗搧鐨勬ā鎷熴€?
                // 瀹為檯鍦?MarketManager 涓紝鎴戜滑闇€瑕佺‘淇濆叏灞€鏂伴椈鐨勪竴鑷存€с€?
                // 鏆傛椂锛屾垜浠湪杩欓噷璋冪敤 GenerateDailyNews锛屽彧浼犲叆褰撳墠鍟嗗搧浣滀负 availableCommodities銆?
                
                var dailyNews = _newsGenerator.GenerateDailyNews(absoluteDay, new List<string> { commodityName });
                allScheduledNews.AddRange(dailyNews);
                simulatedActiveNews.AddRange(dailyNews);
                
                // 娓呯悊杩囨湡鏂伴椈
                simulatedActiveNews.RemoveAll(n => !n.Timing.IsEffectiveOn(absoluteDay));

                // 2.2 閲嶆柊璁＄畻鍩烘湰闈环鍊?(E_t[S_T^*])
                double currentFundamentalValue = _fundamentalEngine.CalculateFundamentalValue(
                    commodityName,
                    currentSeason,
                    simulatedActiveNews
                );

                // 2.3 璁＄畻褰撴棩鏀剁洏浠?(GBM)
                int daysRemaining = totalDays - day;
                double nextClosingPrice = GBM.CalculateNextPrice(
                    currentDailyPrice,
                    currentFundamentalValue, // 浣跨敤鏇存柊鍚庣殑鍩烘湰闈?
                    daysRemaining,
                    baseVolatility,
                    _random
                );

                // 2.4 鐢熸垚鏃ュ唴杞ㄨ抗 (Brownian Bridge)
                float[] dailyTrajectory = GenerateDailyShadowPrices(
                    currentOpenPrice, 
                    nextClosingPrice, 
                    intraVolatility
                );

                // 2.5 濉厖鏁版嵁
                int startIndex = day * stepsPerDay;
                int lengthToCopy = System.Math.Min(dailyTrajectory.Length, stepsPerDay);
                Array.Copy(dailyTrajectory, 0, seasonalPrices, startIndex, lengthToCopy);

                // 鍑嗗涓嬩竴澶?
                currentDailyPrice = nextClosingPrice;
                currentOpenPrice = nextClosingPrice;
            }

            return (seasonalPrices, allScheduledNews);
        }
    }
}
