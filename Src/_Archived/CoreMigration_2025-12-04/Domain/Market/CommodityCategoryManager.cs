// ============================================================================
// 星露资本 (Stardew Capital)
// 模块：商品分类系统
// 作者：Stardew Capital Team
// 用途：定义商品分类和映射关系，用于新闻系统和高级功能
// ============================================================================

using System.Collections.Generic;
using System.Linq;

namespace StardewCapital.Domain.Market
{
    /// <summary>
    /// 商品分类枚举
    /// </summary>
    public enum CommodityCategory
    {
        Vegetables,  // 蔬菜类
        Fruits,      // 水果类
        Grains,      // 谷物类
        Flowers,     // 花卉类
        Artisan,     // 手工艺品
        All          // 全部类别
    }

    /// <summary>
    /// 商品分类系统
    /// 提供商品到分类的映射关系
    /// </summary>
    public static class CommodityCategoryManager
    {
        /// <summary>
        /// 商品到分类的映射表
        /// </summary>
        private static readonly Dictionary<string, List<CommodityCategory>> _categoryMap = new()
        {
            // 蔬菜类
            ["Parsnip"] = new() { CommodityCategory.Vegetables },
            ["Potato"] = new() { CommodityCategory.Vegetables },
            ["Cauliflower"] = new() { CommodityCategory.Vegetables },
            ["Kale"] = new() { CommodityCategory.Vegetables },
            ["Tomato"] = new() { CommodityCategory.Vegetables },
            ["Corn"] = new() { CommodityCategory.Vegetables, CommodityCategory.Grains },
            ["Pumpkin"] = new() { CommodityCategory.Vegetables },
            
            // 水果类
            ["Strawberry"] = new() { CommodityCategory.Fruits },
            ["Melon"] = new() { CommodityCategory.Fruits },
            ["Blueberry"] = new() { CommodityCategory.Fruits },
            ["Cranberries"] = new() { CommodityCategory.Fruits },
            
            // 花卉类
            ["Sunflower"] = new() { CommodityCategory.Flowers },
            ["Tulip"] = new() { CommodityCategory.Flowers },
            
            // 谷物类
            ["Wheat"] = new() { CommodityCategory.Grains },
        };

        /// <summary>
        /// 获取商品所属的所有分类
        /// </summary>
        /// <param name="commodityName">商品名称</param>
        /// <returns>分类列表</returns>
        public static List<CommodityCategory> GetCategories(string commodityName)
        {
            if (_categoryMap.TryGetValue(commodityName, out var categories))
            {
                return categories;
            }
            return new List<CommodityCategory>();
        }

        /// <summary>
        /// 检查商品是否属于指定分类
        /// </summary>
        /// <param name="commodityName">商品名称</param>
        /// <param name="category">分类</param>
        /// <returns>是否属于该分类</returns>
        public static bool IsInCategory(string commodityName, CommodityCategory category)
        {
            if (category == CommodityCategory.All)
                return true;
            
            return GetCategories(commodityName).Contains(category);
        }

        /// <summary>
        /// 根据分类名称字符串获取枚举值
        /// </summary>
        /// <param name="categoryName">分类名称字符串（例如："vegetables"）</param>
        /// <returns>分类枚举值，如果无法解析则返回null</returns>
        public static CommodityCategory? ParseCategory(string categoryName)
        {
            return categoryName?.ToLower() switch
            {
                "vegetables" => CommodityCategory.Vegetables,
                "fruits" => CommodityCategory.Fruits,
                "grains" => CommodityCategory.Grains,
                "flowers" => CommodityCategory.Flowers,
                "artisan" => CommodityCategory.Artisan,
                "all" => CommodityCategory.All,
                _ => null
            };
        }

        /// <summary>
        /// 获取指定分类中的所有商品
        /// </summary>
        /// <param name="category">分类</param>
        /// <returns>商品名称列表</returns>
        public static List<string> GetCommoditiesInCategory(CommodityCategory category)
        {
            if (category == CommodityCategory.All)
                return _categoryMap.Keys.ToList();
            
            return _categoryMap
                .Where(kvp => kvp.Value.Contains(category))
                .Select(kvp => kvp.Key)
                .ToList();
        }
    }
}
