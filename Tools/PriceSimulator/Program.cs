using System;
using System.IO;
using StardewCapital.Core.Futures.Domain.Market;
using StardewCapital.Core.Futures.Config;
using StardewCapital.Core.Futures.Data;

namespace StardewCapital.Simulator
{
    /// <summary>
    /// 价格模拟器主程序
    /// </summary>
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("=========================================");
            Console.WriteLine("  Stardew Capital - 价格模拟器");
            Console.WriteLine("  独立运行模式 (无需启动游戏)");
            Console.WriteLine("=========================================\n");

            try
            {
                // 1. 获取程序目录
                string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
                Console.WriteLine($"工作目录: {baseDirectory}\n");

                // 2. 加载所有配置
                Console.WriteLine("正在加载配置文件...");
                var configLoader = new StandaloneConfigLoader(baseDirectory);
                
                var simulatorConfig = configLoader.LoadSimulatorConfig();
                var commodityConfigs = configLoader.LoadCommoditiesConfig();
                var marketRules = configLoader.LoadMarketRules();
                var newsTemplates = configLoader.LoadNewsTemplates();

                if (commodityConfigs.Count == 0)
                {
                    Console.WriteLine("✗ 错误: 未能加载商品配置");
                    return;
                }

                // 3. 选择模拟模式
                Console.WriteLine("\n请选择模拟模式:");
                Console.WriteLine("1. 影子价格模拟 (Shadow Price) - 仅布朗桥 + 新闻");
                Console.WriteLine("2. 实时价格模拟 (Realtime) - 完整系统（NPC + 订单簿 + 冲击）");
                Console.Write("\n输入选择 [1/2]: ");
                
                string choice = Console.ReadLine() ?? "1";
                
                if (choice == "2")
                {
                    RunRealtimeSimulation(simulatorConfig, commodityConfigs, marketRules, baseDirectory);
                }
                else
                {
                    RunShadowPriceSimulation(simulatorConfig, commodityConfigs, marketRules, newsTemplates, baseDirectory);
                }

                Console.WriteLine("\n========== 模拟完成 ==========");
                Console.WriteLine("提示: 可以修改 SimulatorConfig.json 来调整模拟参数");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n✗ 模拟失败: {ex.Message}");
                Console.WriteLine($"\n堆栈跟踪:\n{ex.StackTrace}");
                Environment.Exit(1);
            }

            // 等待用户按键退出
            Console.WriteLine("\n按任意键退出...");
            Console.ReadKey();
        }

        static void RunShadowPriceSimulation(
            SimulatorConfig config,
            System.Collections.Generic.List<CommodityConfig> commodityConfigs,
            MarketRules marketRules,
            System.Collections.Generic.List<NewsTemplate> newsTemplates,
            string baseDirectory)
        {
            Console.WriteLine("\n========== 影子价格模拟模式 ==========\n");
            
            var runner = new SimulationRunner(
                config,
                commodityConfigs,
                marketRules,
                newsTemplates
            );

            var result = runner.RunSimulation();
            
            string outputPath = Path.Combine(baseDirectory, config.simulation.outputPath);
            OutputWriter.WriteJson(result, outputPath);
            OutputWriter.PrintSummary(result);
        }

        static void RunRealtimeSimulation(
            SimulatorConfig config,
            System.Collections.Generic.List<CommodityConfig> commodityConfigs,
            MarketRules marketRules,
            string baseDirectory)
        {
            Console.WriteLine("\n========== 实时价格模拟模式 ==========\n");
            
            var runner = new RealtimeSimulationRunner(
                config,
                commodityConfigs,
                marketRules
            );

            var result = runner.Run();
            
            string outputPath = Path.Combine(baseDirectory, "realtime_simulation_output.json");
            OutputWriter.WriteRealtimeJson(result, outputPath);
            OutputWriter.PrintRealtimeSummary(result);
        }

        private static Season ParseSeason(string seasonStr)
        {
            return seasonStr.ToLower() switch
            {
                "spring" => Season.Spring,
                "summer" => Season.Summer,
                "fall" => Season.Fall,
                "winter" => Season.Winter,
                _ => throw new ArgumentException($"未知的季节: {seasonStr}")
            };
        }
    }
}

