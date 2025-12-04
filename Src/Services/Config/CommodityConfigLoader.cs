// ============================================================================
// 星露谷资本 (Stardew Capital)
// 模块：商品配置加载器
// 作者：Stardew Capital Team
// 用途：从 JSON 文件加载商品配置
// ============================================================================

using System;
using System.Collections.Generic;
using StardewCapital.Core.Futures.Domain.Market;
using StardewModdingAPI;

namespace StardewCapital.Services.Config
{
    /// <summary>
    /// JSON 配置文件数据模型
    /// 用于反序列化 commodities.json
    /// </summary>
    public class CommoditiesJsonData
    {
        /// <summary>
        /// 商品配置列表
        /// </summary>
        public List<CommodityJsonEntry> commodities { get; set; } = new List<CommodityJsonEntry>();
    }

    /// <summary>
    /// 单个商品的 JSON 条目
    /// </summary>
    public class CommodityJsonEntry
    {
        public string name { get; set; } = string.Empty;
        public string itemId { get; set; } = string.Empty;
        public double basePrice { get; set; }
        public double baseDemand { get; set; }
        public double baseSupply { get; set; }
        public string growingSeason { get; set; } = "Spring";
        public double offSeasonMultiplier { get; set; } = 1.0;
        public bool isGreenhouseCrop { get; set; } = false;
        public double liquiditySensitivity { get; set; } = 0.01;
    }

    /// <summary>
    /// 商品配置加载器
    /// 使用 SMAPI 的内置 JSON 读取功能从 Assets/commodities.json 加载商品配置数据
    /// </summary>
    public class CommodityConfigLoader
    {
        private readonly IMonitor _monitor;
        private readonly IModHelper _helper;

        public CommodityConfigLoader(IMonitor monitor, IModHelper helper)
        {
            _monitor = monitor;
            _helper = helper;
        }

        /// <summary>
        /// 从 JSON 文件加载商品配置列表
        /// </summary>
        /// <param name="relativePath">相对于 Mod 目录的路径（例如："Assets/commodities.json"）</param>
        /// <returns>商品配置列表，加载失败返回空列表</returns>
        public List<CommodityConfig> LoadFromJson(string relativePath)
        {
            var configs = new List<CommodityConfig>();

            try
            {
                // 使用 SMAPI 的 Data API 读取 JSON 文件
                var jsonData = _helper.Data.ReadJsonFile<CommoditiesJsonData>(relativePath);

                if (jsonData == null)
                {
                    _monitor.Log($"[CommodityConfigLoader] 无法读取配置文件: {relativePath}", LogLevel.Warn);
                    return configs;
                }

                if (jsonData.commodities == null || jsonData.commodities.Count == 0)
                {
                    _monitor.Log("[CommodityConfigLoader] 配置文件中没有商品数据", LogLevel.Warn);
                    return configs;
                }

                // 转换 JSON 数据为 CommodityConfig 对象
                foreach (var entry in jsonData.commodities)
                {
                    try
                    {
                        var config = ConvertToCommodityConfig(entry);
                        if (config != null)
                        {
                            configs.Add(config);
                            _monitor.Log($"[CommodityConfigLoader] 已加载商品: {config.Name}", LogLevel.Debug);
                        }
                    }
                    catch (Exception ex)
                    {
                        _monitor.Log($"[CommodityConfigLoader] 解析商品配置时出错: {ex.Message}", LogLevel.Error);
                    }
                }

                _monitor.Log($"[CommodityConfigLoader] 成功加载 {configs.Count} 个商品配置", LogLevel.Info);
            }
            catch (Exception ex)
            {
                _monitor.Log($"[CommodityConfigLoader] 加载配置文件失败: {ex.Message}", LogLevel.Error);
                _monitor.Log($"[CommodityConfigLoader] 详细信息: {ex.StackTrace}", LogLevel.Trace);
            }

            return configs;
        }

        /// <summary>
        /// 将 JSON 条目转换为 CommodityConfig 对象
        /// </summary>
        private CommodityConfig? ConvertToCommodityConfig(CommodityJsonEntry entry)
        {
            // 验证必填字段
            if (string.IsNullOrEmpty(entry.name) || string.IsNullOrEmpty(entry.itemId))
            {
                _monitor.Log("[CommodityConfigLoader] 商品缺少必填字段 'name' 或 'itemId'", LogLevel.Warn);
                return null;
            }

            // 解析季节
            Season season = ParseSeason(entry.growingSeason);

            // 创建配置对象
            var config = new CommodityConfig
            {
                Name = entry.name,
                ItemId = entry.itemId,
                BasePrice = entry.basePrice,
                BaseDemand = entry.baseDemand,
                BaseSupply = entry.baseSupply,
                GrowingSeason = season,
                OffSeasonMultiplier = entry.offSeasonMultiplier,
                IsGreenhouseCrop = entry.isGreenhouseCrop,
                LiquiditySensitivity = entry.liquiditySensitivity
            };

            return config;
        }

        /// <summary>
        /// 解析季节字符串为枚举
        /// </summary>
        private Season ParseSeason(string seasonStr)
        {
            if (string.IsNullOrEmpty(seasonStr))
                return Season.Spring;

            return seasonStr.ToLower() switch
            {
                "spring" => Season.Spring,
                "summer" => Season.Summer,
                "fall" => Season.Fall,
                "winter" => Season.Winter,
                "allseasons" => Season.AllSeasons,
                _ => Season.Spring // 默认春季
            };
        }
    }
}

