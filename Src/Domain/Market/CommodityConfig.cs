// ============================================================================
// 星露谷资本 (Stardew Capital)
// 模块：商品配置数据模型
// 作者：Stardew Capital Team
// 用途：定义每个商品的基础经济参数（用于基本面价值计算）
// ============================================================================

namespace StardewCapital.Domain.Market
{
    /// <summary>
    /// 季节枚举
    /// </summary>
    public enum Season
    {
        /// <summary>春季</summary>
        Spring,
        /// <summary>夏季</summary>
        Summer,
        /// <summary>秋季</summary>
        Fall,
        /// <summary>冬季</summary>
        Winter,
        /// <summary>全季节（温室作物）</summary>
        AllSeasons
    }

    /// <summary>
    /// 商品配置数据模型
    /// 定义单个商品的基础经济参数，用于模型一的基本面价值计算
    /// 
    /// 基于期货.md中的模型一公式：
    /// S_T = P_base × λ_s × (D_base + ΣD_news) / (S_base + ΣS_news)
    /// 
    /// 本类存储每个商品的 P_base、D_base、S_base 和 λ_s 相关参数
    /// </summary>
    public class CommodityConfig
    {
        /// <summary>
        /// 商品名称（英文）
        /// 例如："Parsnip", "Strawberry", "Pumpkin"
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Stardew Valley 物品ID
        /// 例如："24" 代表防风草
        /// </summary>
        public string ItemId { get; set; } = string.Empty;

        /// <summary>
        /// 基础价格（P_base）
        /// 在供需平衡（D = S）且无新闻影响时的理论价格
        /// 单位：金币/个
        /// 
        /// 示例：防风草基础价格 = 35g
        /// </summary>
        public double BasePrice { get; set; }

        /// <summary>
        /// 基础需求（D_base）
        /// 在一个"正常"的28天周期内，鹈鹕镇对该商品的标准总需求量
        /// 单位：数量（个）
        /// 
        /// 示例：防风草基础需求 = 10,000 单位
        /// 
        /// 备注：这是一个抽象的宏观经济参数，不需要与实际游戏物品数量严格对应
        /// </summary>
        public double BaseDemand { get; set; }

        /// <summary>
        /// 基础供给（S_base）
        /// 在一个"正常"的28天周期内，农民们的标准总产量
        /// 单位：数量（个）
        /// 
        /// 示例：防风草基础供给 = 10,000 单位
        /// 
        /// 备注：为简化计算，初始设定 S_base = D_base，表示市场平衡状态
        /// </summary>
        public double BaseSupply { get; set; }

        /// <summary>
        /// 生长季节
        /// 该作物的主要生长季节（决定季节性乘数 λ_s）
        /// </summary>
        public Season GrowingSeason { get; set; }

        /// <summary>
        /// 非生长季乘数（λ_s）
        /// 当处于非生长季节时的价格乘数
        /// 
        /// 示例：
        /// - 春季作物在夏季交割：λ_s = 2.0（较稀缺）
        /// - 春季作物在冬季交割：λ_s = 5.0（极度稀缺，需温室）
        /// - 温室作物全年：λ_s = 1.0（无季节影响）
        /// 
        /// 默认值：1.0（表示无季节影响）
        /// </summary>
        public double OffSeasonMultiplier { get; set; }

        /// <summary>
        /// 是否为温室作物
        /// 温室作物不受季节影响，λ_s 始终为 1.0
        /// </summary>
        public bool IsGreenhouseCrop { get; set; }

        /// <summary>
        /// 默认构造函数
        /// </summary>
        public CommodityConfig()
        {
            // 默认值：供需平衡，无季节影响
            BaseDemand = 10000;
            BaseSupply = 10000;
            OffSeasonMultiplier = 1.0;
            IsGreenhouseCrop = false;
            GrowingSeason = Season.Spring;
        }

        /// <summary>
        /// 完整构造函数
        /// </summary>
        /// <param name="name">商品名称</param>
        /// <param name="itemId">Stardew Valley物品ID</param>
        /// <param name="basePrice">基础价格（P_base）</param>
        /// <param name="baseDemand">基础需求（D_base）</param>
        /// <param name="baseSupply">基础供给（S_base）</param>
        /// <param name="growingSeason">生长季节</param>
        /// <param name="offSeasonMultiplier">非生长季乘数（默认1.0）</param>
        public CommodityConfig(
            string name,
            string itemId,
            double basePrice,
            double baseDemand,
            double baseSupply,
            Season growingSeason,
            double offSeasonMultiplier = 1.0)
        {
            Name = name;
            ItemId = itemId;
            BasePrice = basePrice;
            BaseDemand = baseDemand;
            BaseSupply = baseSupply;
            GrowingSeason = growingSeason;
            OffSeasonMultiplier = offSeasonMultiplier;
            IsGreenhouseCrop = (growingSeason == Season.AllSeasons);
        }

        /// <summary>
        /// 获取在指定季节的季节性乘数（λ_s）
        /// </summary>
        /// <param name="currentSeason">当前季节</param>
        /// <returns>季节性价格乘数</returns>
        /// <remarks>
        /// 计算逻辑：
        /// - 如果是温室作物：返回 1.0（无季节影响）
        /// - 如果在生长季：返回 1.0（正常价格）
        /// - 如果非生长季：返回 OffSeasonMultiplier（稀缺溢价）
        /// </remarks>
        public double GetSeasonalMultiplier(Season currentSeason)
        {
            // 温室作物不受季节影响
            if (IsGreenhouseCrop)
                return 1.0;

            // 在生长季节，价格正常
            if (GrowingSeason == currentSeason)
                return 1.0;

            // 非生长季，应用稀缺性乘数
            return OffSeasonMultiplier;
        }

        /// <summary>
        /// 创建预设商品配置：防风草
        /// </summary>
        public static CommodityConfig CreateParsnipConfig()
        {
            return new CommodityConfig(
                name: "Parsnip",
                itemId: "24",
                basePrice: 35.0,
                baseDemand: 10000,
                baseSupply: 10000,
                growingSeason: Season.Spring,
                offSeasonMultiplier: 2.5 // 非春季时价格为2.5倍
            );
        }

        /// <summary>
        /// 创建预设商品配置：草莓
        /// </summary>
        public static CommodityConfig CreateStrawberryConfig()
        {
            return new CommodityConfig(
                name: "Strawberry",
                itemId: "400",
                basePrice: 120.0,
                baseDemand: 8000,
                baseSupply: 8000,
                growingSeason: Season.Spring,
                offSeasonMultiplier: 3.0 // 高价值作物，非季节稀缺性更高
            );
        }

        /// <summary>
        /// 创建预设商品配置：蓝莓
        /// </summary>
        public static CommodityConfig CreateBlueberryConfig()
        {
            return new CommodityConfig(
                name: "Blueberry",
                itemId: "258",
                basePrice: 50.0,
                baseDemand: 12000,
                baseSupply: 12000,
                growingSeason: Season.Summer,
                offSeasonMultiplier: 2.5
            );
        }
    }
}
