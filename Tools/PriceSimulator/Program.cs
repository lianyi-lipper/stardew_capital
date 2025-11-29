using System;
using System.IO;
using StardewCapital.Domain.Market;
using StardewCapital.Config;
using StardewCapital.Services.News;

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

                // 3. 创建模拟运行器
                var runner = new SimulationRunner(
                    simulatorConfig,
                    commodityConfigs,
                    marketRules,
                    newsTemplates
                );

                // 4. 运行模拟
                var result = runner.RunSimulation();

                // 5. 输出结果
                string outputPath = Path.Combine(baseDirectory, simulatorConfig.simulation.outputPath);
                OutputWriter.WriteJson(result, outputPath);
                OutputWriter.PrintSummary(result);

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
