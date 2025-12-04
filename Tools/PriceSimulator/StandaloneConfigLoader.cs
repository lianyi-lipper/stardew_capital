using System;
using System.IO;
using System.Text.Json;
using System.Collections.Generic;
using StardewCapital.Core.Futures.Config;
using StardewCapital.Core.Futures.Domain.Market;  // CommodityConfig在这里
using StardewCapital.Core.Futures.Data;  // NewsTemplate在这里
using Newtonsoft.Json;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace StardewCapital.Simulator
{
    /// <summary>
    /// 模拟器配置类
    /// </summary>
    public class SimulatorConfig
    {
        public SimulationSettings simulation { get; set; } = new();
        public MarketTimingSettings marketTiming { get; set; } = new();
        public AdvancedSettings advanced { get; set; } = new();
        
        public class SimulationSettings
        {
            public string commodity { get; set; } = "Parsnip";
            public string season { get; set; } = "Spring";
            public int year { get; set; } = 1;
            public string outputPath { get; set; } = "output/simulation_result.json";
            public int? randomSeed { get; set; }
        }
        
        public class MarketTimingSettings
        {
            public int openingTime { get; set; } = 600;
            public int closingTime { get; set; } = 2600;
        }
        
        public class AdvancedSettings
        {
            public bool batchMode { get; set; }
            public bool verboseOutput { get; set; } = true;
        }
    }

    /// <summary>
    /// 独立配置加载器
    /// 替代SMAPI的IModHelper，从文件系统直接加载JSON配置
    /// </summary>
    public class StandaloneConfigLoader
    {
        private readonly string _baseDirectory;

        public StandaloneConfigLoader(string baseDirectory)
        {
            _baseDirectory = baseDirectory;
        }

        /// <summary>
        /// 加载模拟器配置
        /// </summary>
        public SimulatorConfig LoadSimulatorConfig()
        {
            string configPath = Path.Combine(_baseDirectory, "SimulatorConfig.json");
            if (!File.Exists(configPath))
            {
                Console.WriteLine($"警告: 配置文件不存在，使用默认配置: {configPath}");
                return new SimulatorConfig();
            }

            try
            {
                string json = File.ReadAllText(configPath);
                var config = JsonSerializer.Deserialize<SimulatorConfig>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    ReadCommentHandling = JsonCommentHandling.Skip,
                    AllowTrailingCommas = true
                });
                
                Console.WriteLine($"✓ 成功加载模拟器配置: {configPath}");
                return config ?? new SimulatorConfig();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"错误: 加载配置文件失败: {ex.Message}");
                Console.WriteLine("使用默认配置");
                return new SimulatorConfig();
            }
        }

        /// <summary>
        /// 加载商品配置
        /// </summary>
        public List<CommodityConfig> LoadCommoditiesConfig()
        {
            string path = Path.Combine(_baseDirectory, "Assets", "commodities.json");
            var wrapper = LoadJsonConfig<CommoditiesWrapper>(path);
            return wrapper?.commodities ?? new List<CommodityConfig>();
        }

        /// <summary>
        /// 加载市场规则配置
        /// </summary>
        public MarketRules LoadMarketRules()
        {
            string path = Path.Combine(_baseDirectory, "Assets", "data", "market_rules.json");
            return LoadJsonConfig<MarketRules>(path) ?? new MarketRules();
        }

        /// <summary>
        /// 加载新闻模板配置
        /// </summary>
        public List<NewsTemplate> LoadNewsTemplates()
        {
            string path = Path.Combine(_baseDirectory, "Assets", "data", "news_config.json");
            var wrapper = LoadJsonConfig<NewsConfigWrapper>(path);
            return wrapper?.news ?? new List<NewsTemplate>();
        }

        private T? LoadJsonConfig<T>(string filePath) where T : class
        {
            if (!File.Exists(filePath))
            {
                Console.WriteLine($"错误: 配置文件不存在: {filePath}");
                return null;
            }

            try
            {
                string json = File.ReadAllText(filePath);
                
                // 使用Newtonsoft.Json来保证与主项目的兼容性
                var config = Newtonsoft.Json.JsonConvert.DeserializeObject<T>(json);
                
                Console.WriteLine($"✓ 成功加载配置: {Path.GetFileName(filePath)}");
                return config;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"错误: 加载配置文件失败 {Path.GetFileName(filePath)}: {ex.Message}");
                return null;
            }
        }
    }

    // 简单的包装类用于加载commodities.json
    internal class CommoditiesWrapper
    {
        public List<CommodityConfig> commodities { get; set; } = new();
    }

    // 简单的包装类用于加载news_config.json
    internal class NewsConfigWrapper
    {
        public List<NewsTemplate> news { get; set; } = new();
    }
}


