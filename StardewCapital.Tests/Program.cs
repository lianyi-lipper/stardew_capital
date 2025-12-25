// =====================================================================
// 文件：Program.cs
// 用途：StardewCapital.Core 的交互式测试控制台。
//       用于验证所有核心模块的功能正确性。
// =====================================================================

using StardewCapital.Core.Common;
using StardewCapital.Core.Common.Market.Engines;
using StardewCapital.Core.Common.Market.Models;
using StardewCapital.Core.Futures;
using StardewCapital.Core.Futures.Market;
using StardewCapital.Core.Futures.Math;
using StardewCapital.Core.Futures.Models;
using StardewCapital.Core.Futures.Pricing;
using StardewCapital.Core.Futures.Config;
using StardewCapital.Core.Time;

namespace StardewCapital.Tests;

class Program
{
    static void Main(string[] args)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        Console.WriteLine("╔════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║         StardewCapital.Core 测试控制台                     ║");
        Console.WriteLine("╚════════════════════════════════════════════════════════════╝\n");
        
        // 选择测试项
        while (true)
        {
            Console.WriteLine("\n请选择测试项目：");
            Console.WriteLine("  1. 测试 GBM（几何布朗运动）日间价格生成");
            Console.WriteLine("  2. 测试布朗桥日内价格生成");
            Console.WriteLine("  3. 测试持有成本定价模型");
            Console.WriteLine("  4. 测试订单簿与撮合引擎");
            Console.WriteLine("  5. 测试完整期货市场模拟（综合测试）");
            Console.WriteLine("  6. 导出完整季节模拟 JSON 文件");
            Console.WriteLine("  7. 测试商品配置加载（地区价格）");
            Console.WriteLine("  0. 退出");
            Console.Write("\n输入选项 > ");
            
            var input = Console.ReadLine();
            Console.WriteLine();
            
            switch (input)
            {
                case "1": TestGBM(); break;
                case "2": TestBrownianBridge(); break;
                case "3": TestCostOfCarry(); break;
                case "4": TestOrderBook(); break;
                case "5": TestFullMarketSimulation(); break;
                case "6": ExportSeasonSimulation(); break;
                case "7": TestCommodityConfig(); break;
                case "0": return;
                default: Console.WriteLine("无效选项，请重新选择。"); break;
            }
        }
    }
    
    /// <summary>
    /// 测试 GBM 日间价格生成。
    /// 验证价格是否向目标收敛。
    /// </summary>
    static void TestGBM()
    {
        Console.WriteLine("═══ GBM 日间价格生成测试 ═══\n");
        
        var random = new DefaultRandomProvider(42); // 固定种子以保证可重复
        var gbm = new GBM(random);
        
        double startPrice = 100.0;
        double targetPrice = 150.0;
        int totalDays = 28;
        double volatility = 0.02;
        
        Console.WriteLine($"起始价格: {startPrice:F2}");
        Console.WriteLine($"目标价格: {targetPrice:F2}");
        Console.WriteLine($"总天数: {totalDays}");
        Console.WriteLine($"波动率: {volatility:P1}\n");
        
        var prices = gbm.GeneratePricePath(startPrice, targetPrice, totalDays, volatility);
        
        Console.WriteLine("价格路径:");
        Console.WriteLine("Day  | Price      | Δ%");
        Console.WriteLine("-----|------------|--------");
        
        for (int day = 0; day <= totalDays; day++)
        {
            double change = day > 0 
                ? (prices[day] - prices[day - 1]) / prices[day - 1] * 100 
                : 0;
            string bar = new string('█', (int)(prices[day] / 3));
            Console.WriteLine($"{day,4} | {prices[day],10:F2} | {change,6:+0.0;-0.0}% {bar}");
        }
        
        Console.WriteLine($"\n✓ 最终价格 ({prices[totalDays]:F2}) 等于目标价格 ({targetPrice:F2}): {prices[totalDays] == targetPrice}");
    }
    
    /// <summary>
    /// 测试布朗桥日内价格生成。
    /// 验证价格是否在收盘时收敛到目标。
    /// </summary>
    static void TestBrownianBridge()
    {
        Console.WriteLine("═══ 布朗桥日内价格生成测试 ═══\n");
        
        var random = new DefaultRandomProvider(42);
        var bridge = new BrownianBridge(random);
        
        double openPrice = 100.0;
        double closeTarget = 105.0;
        int totalTicks = 50;
        double intradayVol = 0.01;
        
        Console.WriteLine($"开盘价: {openPrice:F2}");
        Console.WriteLine($"收盘目标: {closeTarget:F2}");
        Console.WriteLine($"总 Tick 数: {totalTicks}");
        Console.WriteLine($"日内波动率: {intradayVol:P1}\n");
        
        var prices = bridge.GenerateIntradayPath(openPrice, closeTarget, totalTicks, intradayVol);
        
        // 找出最高和最低价
        double high = prices.Max();
        double low = prices.Min();
        
        Console.WriteLine("日内价格曲线:");
        Console.WriteLine("Tick | Price      | 可视化");
        Console.WriteLine("-----|------------|" + new string('-', 40));
        
        for (int tick = 0; tick <= totalTicks; tick += 5)
        {
            double normalized = (prices[tick] - low) / (high - low);
            int barLength = (int)(normalized * 35);
            string bar = new string('░', barLength) + "█";
            Console.WriteLine($"{tick,4} | {prices[tick],10:F2} | {bar}");
        }
        
        Console.WriteLine($"\n✓ 收盘价 ({prices[totalTicks]:F2}) 等于目标 ({closeTarget:F2}): {prices[totalTicks] == closeTarget}");
        Console.WriteLine($"  日高: {high:F2}, 日低: {low:F2}, 振幅: {(high - low) / openPrice:P1}");
    }
    
    /// <summary>
    /// 测试持有成本定价模型。
    /// </summary>
    static void TestCostOfCarry()
    {
        Console.WriteLine("═══ 持有成本定价模型测试 ═══\n");
        
        double spotPrice = 100.0;
        double r = 0.002;    // 日无风险利率
        double phi = 0.005;  // 日存储成本
        double q = 0.001;    // 日便利收益
        
        Console.WriteLine($"现货价格: {spotPrice:F2}");
        Console.WriteLine($"无风险利率 r: {r:P2}/天");
        Console.WriteLine($"存储成本 φ: {phi:P2}/天");
        Console.WriteLine($"便利收益 q: {q:P2}/天");
        Console.WriteLine($"持有成本率 (r+φ-q): {(r + phi - q):P2}/天\n");
        
        Console.WriteLine("期货价格随期限变化:");
        Console.WriteLine("Days | Futures   | Basis    | 年化基差");
        Console.WriteLine("-----|-----------|----------|--------");
        
        int[] days = { 1, 7, 14, 21, 28, 56, 112 };
        foreach (int d in days)
        {
            double futuresPrice = CostOfCarry.CalculateFuturesPrice(spotPrice, d, r, phi, q);
            double basis = CostOfCarry.CalculateBasis(futuresPrice, spotPrice);
            double annualizedBasis = CostOfCarry.CalculateAnnualizedBasis(futuresPrice, spotPrice, d);
            
            Console.WriteLine($"{d,4} | {futuresPrice,9:F2} | {basis,8:+0.00;-0.00} | {annualizedBasis,6:P1}");
        }
        
        Console.WriteLine("\n✓ 正向市场（contango）: 期货 > 现货（持有成本为正）");
    }
    
    /// <summary>
    /// 测试订单簿与撮合引擎。
    /// </summary>
    static void TestOrderBook()
    {
        Console.WriteLine("═══ 订单簿与撮合引擎测试 ═══\n");
        
        // 创建合约和订单簿
        var contract = new FuturesContract(Commodity.Parsnip);
        var engine = new PriceTimePriorityEngine();
        var orderBook = new FuturesOrderBook(contract, engine);
        
        // 生成 NPC 深度
        orderBook.GenerateNPCDepth(100.0, spreadPercent: 0.02, depthLevels: 5, quantityPerLevel: 10);
        
        Console.WriteLine("初始订单簿深度:");
        PrintOrderBook(orderBook);
        
        // 玩家下限价买单
        Console.WriteLine("\n▶ 玩家下限价买单: 价格 99, 数量 5");
        var limitBuy = Order.LimitBuy(contract.Symbol, 99, 5, isPlayer: true);
        var result1 = orderBook.PlaceOrder(limitBuy);
        Console.WriteLine($"  成交数量: {result1.FilledQuantity}, 均价: {result1.AveragePrice:F2}");
        
        // 玩家下市价买单
        Console.WriteLine("\n▶ 玩家下市价买单: 数量 15");
        var result2 = orderBook.ExecuteMarketOrder(OrderSide.Buy, 15);
        Console.WriteLine($"  成交数量: {result2.FilledQuantity}, 均价: {result2.AveragePrice:F2}");
        Console.WriteLine($"  滑点: {result2.Slippage:F2}");
        
        foreach (var trade in result2.Trades)
        {
            Console.WriteLine($"    成交: {trade.Quantity}手 @ {trade.Price:F2}");
        }
        
        Console.WriteLine("\n成交后订单簿深度:");
        PrintOrderBook(orderBook);
        
        Console.WriteLine("\n✓ 撮合引擎正常工作");
    }
    
    /// <summary>
    /// 打印订单簿深度。
    /// </summary>
    static void PrintOrderBook(FuturesOrderBook book)
    {
        var asks = book.GetAsks(5).Reverse().ToList();
        var bids = book.GetBids(5);
        
        Console.WriteLine("       卖单       |       买单");
        Console.WriteLine("价格      数量    |  价格      数量");
        Console.WriteLine("------------------|------------------");
        
        for (int i = 0; i < 5; i++)
        {
            string askStr = i < asks.Count 
                ? $"{asks[i].Price,7:F2}  {asks[i].TotalQuantity,4}" 
                : "                ";
            string bidStr = i < bids.Count 
                ? $"{bids[i].Price,7:F2}  {bids[i].TotalQuantity,4}" 
                : "                ";
            Console.WriteLine($"{askStr} | {bidStr}");
        }
        
        Console.WriteLine($"\n中间价: {book.MidPrice:F2}, 价差: {book.Spread:F2}");
    }
    
    /// <summary>
    /// 综合测试：模拟一个完整交易日。
    /// </summary>
    static void TestFullMarketSimulation()
    {
        Console.WriteLine("═══ 完整期货市场模拟测试 ═══\n");
        
        // 创建时钟
        var clock = new SimulationClock();
        clock.SetTime(day: 1, timeOfDay: 600, season: 0); // 春季第1天开盘
        
        // 创建市场
        var random = new DefaultRandomProvider(42);
        var market = new FuturesMarket(clock, random);
        market.TicksPerDay = 10; // 简化测试
        
        // 创建合约
        var contract = market.CreateContract(Commodity.Parsnip);
        
        Console.WriteLine($"合约: {contract.Symbol}");
        Console.WriteLine($"开盘价: {contract.OpenPrice:F2}");
        Console.WriteLine($"所需保证金/手: {contract.RequiredMargin:F2}g\n");
        
        Console.WriteLine("模拟交易日价格变动:");
        Console.WriteLine("Tick | Time  | Price      | Δ");
        Console.WriteLine("-----|-------|------------|--------");
        
        double prevPrice = contract.CurrentPrice;
        
        for (int tick = 0; tick < market.TicksPerDay; tick++)
        {
            int remaining = market.TicksPerDay - tick;
            market.Tick(remaining);
            
            double delta = contract.CurrentPrice - prevPrice;
            Console.WriteLine($"{tick,4} | {clock.CurrentTimeOfDay,5} | {contract.CurrentPrice,10:F2} | {delta,+7:+0.00;-0.00}");
            
            prevPrice = contract.CurrentPrice;
            clock.Tick(10);
        }
        
        // 收盘结算
        market.EndOfDay();
        
        Console.WriteLine($"\n日终结算价: {contract.SettlementPrice:F2}");
        Console.WriteLine($"日高: {contract.HighPrice:F2}");
        Console.WriteLine($"日低: {contract.LowPrice:F2}");
        Console.WriteLine($"振幅: {(contract.HighPrice - contract.LowPrice) / contract.OpenPrice:P1}");
        
        // 模拟新闻事件（手动创建，实际使用时从 JSON 加载）
        Console.WriteLine("\n▶ 发布新闻：害虫危机！");
        var pestNews = new NewsEvent
        {
            Id = "pest_crisis_test",
            Title = "害虫危机席卷鹈鹕镇！",
            TriggerDay = 1,
            DemandImpact = 0,
            SupplyImpact = -2000,
            DurationDays = 7
        };
        market.AddNews(pestNews);
        
        Console.WriteLine($"  新闻影响后目标价: {market.GetPriceGenerator(contract.Symbol)?.CurrentTarget:F2}");
        
        Console.WriteLine("\n✓ 期货市场模拟完成");
    }
    
    /// <summary>
    /// 导出完整季节模拟为 JSON 文件。
    /// </summary>
    static void ExportSeasonSimulation()
    {
        Console.WriteLine("═══ 导出完整季节模拟 ═══\n");
        
        Console.Write("选择季节 (1=春季, 2=夏季, 3=秋季, 4=冬季) > ");
        var seasonInput = Console.ReadLine();
        var season = seasonInput switch
        {
            "1" => Season.Spring,
            "2" => Season.Summer,
            "3" => Season.Fall,
            "4" => Season.Winter,
            _ => Season.Spring
        };
        
        Console.Write("每天模拟 Tick 数 (默认 50) > ");
        var tickInput = Console.ReadLine();
        int ticksPerDay = int.TryParse(tickInput, out var t) && t > 0 ? t : 50;
        
        Console.Write("随机种子 (留空随机) > ");
        var seedInput = Console.ReadLine();
        int? seed = int.TryParse(seedInput, out var s) ? s : null;
        
        Console.WriteLine($"\n正在模拟 {season} 季节，每天 {ticksPerDay} 个 Tick...");
        
        var exporter = new SimulationExporter(ticksPerDay);
        
        // 定义要模拟的商品
        var commodities = new[]
        {
            Commodity.Parsnip,
            Commodity.Cauliflower,
            Commodity.Strawberry,
            Commodity.Melon,
            Commodity.Pumpkin
        };
        
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        
        // 获取新闻配置路径
        string newsConfigPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "news_config.json");
        
        var output = exporter.RunFullSeasonSimulation(commodities, season, seed, newsConfigPath);
        stopwatch.Stop();
        
        Console.WriteLine($"模拟完成！耗时: {stopwatch.ElapsedMilliseconds}ms");
        
        // 保存文件
        string outputDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "output");
        Directory.CreateDirectory(outputDir);
        
        string fileName = $"season_simulation_{season.ToString().ToLower()}_{DateTime.Now:yyyyMMdd_HHmmss}.json";
        string filePath = Path.Combine(outputDir, fileName);
        
        exporter.SaveToFile(output, filePath);
        
        Console.WriteLine($"\n✓ JSON 文件已保存: {filePath}");
        Console.WriteLine($"  文件大小: {new FileInfo(filePath).Length / 1024.0:F1} KB");
        Console.WriteLine($"  商品数量: {output.CommodityResults.Count}");
        Console.WriteLine($"  总天数: {output.TotalDays}");
        Console.WriteLine($"  每天 Tick: {ticksPerDay}");
        
        // 输出每个商品的汇总
        Console.WriteLine("\n各商品价格汇总:");
        Console.WriteLine("商品         | 开盘价    | 收盘价    | 季度涨跌");
        Console.WriteLine("-------------|-----------|-----------|----------");
        foreach (var (symbol, result) in output.CommodityResults)
        {
            double openPrice = result.DailyData.First().OpenPrice;
            double closePrice = result.DailyData.Last().ClosePrice;
            double change = (closePrice - openPrice) / openPrice * 100;
            Console.WriteLine($"{result.CommodityName,-12} | {openPrice,9:F2} | {closePrice,9:F2} | {change,+8:+0.0;-0.0}%");
        }
    }
    
    /// <summary>
    /// 测试商品配置加载（地区价格差异）。
    /// </summary>
    static void TestCommodityConfig()
    {
        Console.WriteLine("═══ 商品配置加载测试 ═══\n");
        
        // 加载配置
        string configPath = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory, 
            "..", "..", "..", "..", 
            "StardewCapital.Core", "Assets", "commodities_config.json");
        
        if (!File.Exists(configPath))
        {
            // 尝试相对于 Debug 输出目录
            configPath = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "Assets", "commodities_config.json");
        }
        
        if (!File.Exists(configPath))
        {
            Console.WriteLine($"找不到配置文件: {configPath}");
            Console.WriteLine("请确保 commodities_config.json 已放置在正确位置。");
            return;
        }
        
        var loader = new CommodityConfigLoader();
        loader.LoadFromFile(configPath);
        
        Console.WriteLine($"✓ 已加载 {loader.AllCommodities.Count} 个商品");
        Console.WriteLine($"✓ 配置版本: {loader.Metadata?.SchemaVersion}");
        Console.WriteLine($"✓ 最后更新: {loader.Metadata?.LastUpdated}\n");
        
        // 显示所有商品
        Console.WriteLine("═══ 商品列表 ═══\n");
        Console.WriteLine("ID           | 名称       | 类别       | 合约类型");
        Console.WriteLine("-------------|------------|------------|----------");
        foreach (var commodity in loader.AllCommodities.Values)
        {
            Console.WriteLine($"{commodity.Id,-12} | {commodity.Name,-10} | {commodity.Category,-10} | {commodity.ContractType}");
        }
        
        // 显示地区价格差异
        Console.WriteLine("\n═══ 地区价格对比 ═══\n");
        Console.WriteLine("商品         | 鹈鹕镇    | 沙漠      | 姜岛      | 价差");
        Console.WriteLine("-------------|-----------|-----------|-----------|------");
        
        foreach (var commodity in loader.AllCommodities.Values)
        {
            double pelicanPrice = commodity.GetRegionPrice(Region.PelicanTown);
            double desertPrice = commodity.GetRegionPrice(Region.CalicoDesert);
            double islandPrice = commodity.GetRegionPrice(Region.GingerIsland);
            double maxSpread = (System.Math.Max(System.Math.Max(pelicanPrice, desertPrice), islandPrice) / 
                               System.Math.Min(System.Math.Min(pelicanPrice, desertPrice), islandPrice) - 1) * 100;
            
            Console.WriteLine($"{commodity.Name,-12} | {pelicanPrice,9:F0}g | {desertPrice,9:F0}g | {islandPrice,9:F0}g | {maxSpread,5:F0}%");
        }
        
        // 演示地区切换
        Console.WriteLine("\n═══ 地区切换演示 ═══");
        var blueberry = loader.GetCommodity("BLUEBERRY");
        if (blueberry != null)
        {
            Console.WriteLine($"\n蓝莓价格在不同地区：");
            
            loader.CurrentRegion = Region.PelicanTown;
            Console.WriteLine($"  鹈鹕镇: {blueberry.BasePrice}g ({blueberry.Availability})");
            
            loader.CurrentRegion = Region.CalicoDesert;
            Console.WriteLine($"  沙漠:   {blueberry.BasePrice}g ({blueberry.Availability})");
            
            loader.CurrentRegion = Region.GingerIsland;
            Console.WriteLine($"  姜岛:   {blueberry.BasePrice}g ({blueberry.Availability})");
        }
        
        // 显示市场关联
        Console.WriteLine("\n═══ 市场关联 ═══");
        foreach (var commodity in loader.AllCommodities.Values)
        {
            if (commodity.Correlations.Count == 0) continue;
            
            Console.WriteLine($"\n{commodity.Name}:");
            foreach (var (corrId, type, strength) in commodity.Correlations)
            {
                var correlated = loader.GetCommodity(corrId);
                string corrName = correlated?.Name ?? corrId;
                string arrow = type == CorrelationType.Input ? "→" : 
                              type == CorrelationType.Output ? "←" : "↔";
                Console.WriteLine($"  {arrow} {corrName} ({type}, 强度: {strength:P0})");
            }
        }
        
        // 显示价格行为参数
        Console.WriteLine("\n═══ 价格行为参数 ═══\n");
        Console.WriteLine("商品         | 波动率 | 趋势惯性 | 均值回归 | 跳空概率");
        Console.WriteLine("-------------|--------|----------|----------|--------");
        foreach (var commodity in loader.AllCommodities.Values)
        {
            Console.WriteLine($"{commodity.Name,-12} | {commodity.BaseVolatility,5:P1} | {commodity.MomentumFactor,8:P0} | {commodity.MeanReversionSpeed,8:P0} | {commodity.JumpProbability,6:P1}");
        }
        
        Console.WriteLine("\n✓ 商品配置加载测试完成");
    }
}
