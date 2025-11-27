// ============================================================================
// 星露资本 (Stardew Capital)
// 模块：基本面价值计算引擎
// 作者：Stardew Capital Team
// 用途：实现模型一 - 基于供需关系计算商品的基本面价值（S_T）
// ============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using StardewCapital.Domain.Market;
using StardewCapital.Services.Config;
using StardewModdingAPI;

namespace StardewCapital.Services.Pricing
{
    /// <summary>
    /// 基本面价值计算引擎（模型一）
    /// 
    /// 实现期货.md中的公式：
    /// S_T = P_base × λ_s × (D_base + ΣD_news(t)) / (S_base + ΣS_news(t))
    /// 
    /// 核心职责：
    /// 1. 根据供需关系计算商品的"真实价值"（基本面价值）
    /// 2. 整合新闻事件对供需的累积影响
    /// 3. 考虑季节性因素（温室作物的稀缺性溢价）
    /// 
    /// 这个值是市场的"锚点"，其他模型（GBM、市场冲击）都围绕这个值波动
    /// </summary>
    public class FundamentalEngine
    {
        private readonly IMonitor _monitor;
        private readonly IModHelper _helper;
        
        /// <summary>
        /// 商品配置字典（商品名 -> 配置）
        /// </summary>
        private readonly Dictionary<string, CommodityConfig> _commodityConfigs;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="monitor">SMAPI 日志监视器</param>
        /// <param name="helper">SMAPI Mod 辅助工具（用于文件路径解析）</param>
        public FundamentalEngine(IMonitor monitor, IModHelper helper)
        {
            _monitor = monitor;
            _helper = helper;
            _commodityConfigs = new Dictionary<string, CommodityConfig>();
            
            // 初始化商品配置（从 JSON 加载或使用默认值）
            InitializeDefaultCommodities();
        }

        /// <summary>
        /// 初始化默认商品配置
        /// 优先从 JSON 文件加载，如果失败则使用硬编码配置
        /// </summary>
        /// <remarks>
        /// 加载顺序：
        /// 1. 尝试从 Assets/commodities.json 加载
        /// 2. 如果失败，回退到硬编码的默认配置
        /// </remarks>
        private void InitializeDefaultCommodities()
        {
            // 尝试从 JSON 文件加载（使用相对路径）
            var loader = new CommodityConfigLoader(_monitor, _helper);
            var configs = loader.LoadFromJson("Assets/commodities.json");

            if (configs.Count > 0)
            {
                // 成功加载 JSON 配置
                foreach (var config in configs)
                {
                    AddCommodity(config);
                }
                _monitor.Log($"[FundamentalEngine] 从 JSON 加载了 {_commodityConfigs.Count} 种商品配置", LogLevel.Info);
            }
            else
            {
                // 回退到硬编码配置
                _monitor.Log("[FundamentalEngine] JSON 加载失败，使用硬编码配置", LogLevel.Warn);
                AddCommodity(CommodityConfig.CreateParsnipConfig());
                AddCommodity(CommodityConfig.CreateStrawberryConfig());
                AddCommodity(CommodityConfig.CreateBlueberryConfig());
                _monitor.Log($"[FundamentalEngine] 已加载 {_commodityConfigs.Count} 种硬编码商品配置", LogLevel.Info);
            }
        }

        /// <summary>
        /// 添加商品配置
        /// </summary>
        /// <param name="config">商品配置对象</param>
        public void AddCommodity(CommodityConfig config)
        {
            _commodityConfigs[config.Name] = config;
        }

        /// <summary>
        /// 获取商品配置
        /// </summary>
        /// <param name="commodityName">商品名称</param>
        /// <returns>商品配置，如果不存在则返回null</returns>
        public CommodityConfig? GetCommodityConfig(string commodityName)
        {
            return _commodityConfigs.TryGetValue(commodityName, out var config) ? config : null;
        }

        /// <summary>
        /// 获取所有商品配置列表
        /// </summary>
        /// <returns>所有已加载的商品配置</returns>
        public List<CommodityConfig> GetAllCommodityConfigs()
        {
            return _commodityConfigs.Values.ToList();
        }

        /// <summary>
        /// 计算基本面价值（S_T）- 核心方法
        /// </summary>
        /// <param name="commodityName">商品名称（例如："Parsnip"）</param>
        /// <param name="currentSeason">当前季节</param>
        /// <param name="newsHistory">历史新闻事件列表（从Day 1到当前日期的所有新闻）</param>
        /// <returns>计算得出的基本面价值（金币）</returns>
        /// <remarks>
        /// 公式实现：S_T = P_base × λ_s × (D_base + ΣD_news) / (S_base + ΣS_news)
        /// 
        /// 步骤：
        /// 1. 获取商品的基础参数（P_base, D_base, S_base）
        /// 2. 计算季节性乘数（λ_s）
        /// 3. 累加所有相关新闻对供需的影响
        /// 4. 应用供需比例公式
        /// 5. 返回最终价值
        /// 
        /// 注意事项：
        /// - 如果供给降为0或负数，视为极端稀缺，返回高价（10倍基础价格）
        /// - 如果需求降为0或负数，视为无人问津，返回低价（0.1倍基础价格）
        /// </remarks>
        public double CalculateFundamentalValue(
            string commodityName, 
            Season currentSeason, 
            List<NewsEvent> newsHistory)
        {
            // 1. 获取商品配置
            var config = GetCommodityConfig(commodityName);
            if (config == null)
            {
                _monitor.Log($"[FundamentalEngine] 警告：商品 {commodityName} 配置不存在，使用默认值", LogLevel.Warn);
                return 100.0; // 默认基本面价值
            }

            // 2. 计算季节性乘数（λ_s）
            double seasonalMultiplier = config.GetSeasonalMultiplier(currentSeason);

            // 3. 计算总需求（D_base + ΣD_news）
            double totalDemand = CalculateTotalDemand(config.BaseDemand, commodityName, newsHistory);

            // 4. 计算总供给（S_base + ΣS_news）
            double totalSupply = CalculateTotalSupply(config.BaseSupply, commodityName, newsHistory);

            // 5. 异常处理：防止除以零或负数
            if (totalSupply <= 0)
            {
                // 供给耗尽，极端稀缺，价格暴涨
                _monitor.Log($"[FundamentalEngine] {commodityName} 供给耗尽！价格暴涨", LogLevel.Warn);
                return config.BasePrice * seasonalMultiplier * 10.0;
            }

            if (totalDemand <= 0)
            {
                // 需求消失，无人问津，价格崩盘
                _monitor.Log($"[FundamentalEngine] {commodityName} 需求消失！价格崩盘", LogLevel.Warn);
                return config.BasePrice * seasonalMultiplier * 0.1;
            }

            // 6. 应用公式：S_T = P_base × λ_s × (D / S)
            double fundamentalValue = config.BasePrice * seasonalMultiplier * (totalDemand / totalSupply);

            // 7. 调试日志（已注释，避免刷屏）
            // _monitor.Log(
            //     $"[FundamentalEngine] {commodityName}: " +
            //     $"P_base={config.BasePrice:F2}g, λ_s={seasonalMultiplier:F2}, " +
            //     $"D={totalDemand:F0}, S={totalSupply:F0}, " +
            //     $"S_T={fundamentalValue:F2}g",
            //     LogLevel.Debug
            // );

            return fundamentalValue;
        }

        /// <summary>
        /// 计算总需求（D_base + ΣD_news）
        /// </summary>
        /// <param name="baseDemand">基础需求（D_base）</param>
        /// <param name="commodityName">商品名称</param>
        /// <param name="newsHistory">新闻历史</param>
        /// <returns>总需求量</returns>
        /// <remarks>
        /// 逻辑：
        /// 1. 从基础需求开始
        /// 2. 遍历所有新闻事件
        /// 3. 如果新闻影响该商品（或影响所有商品），累加其 DemandImpact
        /// 4. 返回累积后的总需求
        /// </remarks>
        public double CalculateTotalDemand(
            double baseDemand, 
            string commodityName, 
            List<NewsEvent> newsHistory)
        {
            if (newsHistory == null || newsHistory.Count == 0)
                return baseDemand;

            // 累加所有相关新闻的需求影响
            double totalDemandImpact = newsHistory
                .Where(news => IsNewsRelevant(news, commodityName))
                .Sum(news => news.Impact.DemandImpact);

            return baseDemand + totalDemandImpact;
        }

        /// <summary>
        /// 计算总供给（S_base + ΣS_news）
        /// </summary>
        /// <param name="baseSupply">基础供给（S_base）</param>
        /// <param name="commodityName">商品名称</param>
        /// <param name="newsHistory">新闻历史</param>
        /// <returns>总供给量</returns>
        /// <remarks>
        /// 逻辑：同 CalculateTotalDemand，但累加的是 SupplyImpact
        /// </remarks>
        public double CalculateTotalSupply(
            double baseSupply, 
            string commodityName, 
            List<NewsEvent> newsHistory)
        {
            if (newsHistory == null || newsHistory.Count == 0)
                return baseSupply;

            // 累加所有相关新闻的供给影响
            double totalSupplyImpact = newsHistory
                .Where(news => IsNewsRelevant(news, commodityName))
                .Sum(news => news.Impact.SupplyImpact);

            return baseSupply + totalSupplyImpact;
        }

        /// <summary>
        /// 判断新闻事件是否影响指定商品
        /// </summary>
        /// <param name="news">新闻事件</param>
        /// <param name="commodityName">商品名称</param>
        /// <returns>true表示影响该商品，false表示不影响</returns>
        /// <remarks>
        /// 判断逻辑：
        /// - 如果新闻的 Scope.IsGlobal 为 true，则影响所有商品
        /// - 如果新闻的 Scope.AffectedItems 包含 commodityName，则影响该商品
        /// - 否则不影响
        /// </remarks>
        private bool IsNewsRelevant(NewsEvent news, string commodityName)
        {
            if (news.Scope == null)
                return false;

            // 全局新闻影响所有商品
            if (news.Scope.IsGlobal)
                return true;

            // 检查是否在受影响商品列表中
            if (news.Scope.AffectedItems != null && news.Scope.AffectedItems.Count > 0)
            {
                return news.Scope.AffectedItems.Any(item => 
                    item.Equals(commodityName, StringComparison.OrdinalIgnoreCase) ||
                    item.Equals("ALL", StringComparison.OrdinalIgnoreCase));
            }

            return false;
        }

        /// <summary>
        /// 获取季节性乘数（暴露给外部使用）
        /// </summary>
        /// <param name="commodityName">商品名称</param>
        /// <param name="currentSeason">当前季节</param>
        /// <returns>季节性乘数，如果商品不存在返回1.0</returns>
        public double GetSeasonalMultiplier(string commodityName, Season currentSeason)
        {
            var config = GetCommodityConfig(commodityName);
            return config?.GetSeasonalMultiplier(currentSeason) ?? 1.0;
        }

        /// <summary>
        /// 批量计算多个商品的基本面价值
        /// </summary>
        /// <param name="commodityNames">商品名称列表</param>
        /// <param name="currentSeason">当前季节</param>
        /// <param name="newsHistory">新闻历史</param>
        /// <returns>商品名称 -> 基本面价值的字典</returns>
        public Dictionary<string, double> CalculateBatchFundamentalValues(
            List<string> commodityNames,
            Season currentSeason,
            List<NewsEvent> newsHistory)
        {
            var results = new Dictionary<string, double>();
            
            foreach (var name in commodityNames)
            {
                results[name] = CalculateFundamentalValue(name, currentSeason, newsHistory);
            }

            return results;
        }
    }
}
