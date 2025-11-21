// ============================================================================
// 星露谷资本 (Stardew Capital)
// 模块：便利收益率服务
// 作者：Stardew Capital Team
// 用途：计算商品的动态便利收益率（模型三：持有成本模型的 q 参数）
// ============================================================================

using System;
using System.Linq;
using StardewModdingAPI;
using StardewValley;

namespace StardewCapital.Services
{
    /// <summary>
    /// 便利收益率服务（Convenience Yield Service）
    /// 
    /// 负责计算动态便利收益率 (q)，该值影响期货价格公式：
    /// F_t = S_t × e^((r + φ - q) × τ)
    /// 
    /// 便利收益率的经济含义：
    /// - 持有现货（而非期货）带来的额外好处
    /// - 例如：可以送礼给NPC、烹饪料理、完成社区中心任务
    /// 
    /// 动态因素：
    /// 1. NPC生日：如果今天是某NPC生日，且该NPC喜欢此物品，q 大幅提升
    /// 2. 社区中心任务：如果某Bundle需要此物品，q 小幅提升
    /// </summary>
    public class ConvenienceYieldService
    {
        private readonly IMonitor _monitor;

        public ConvenienceYieldService(IMonitor monitor)
        {
            _monitor = monitor;
        }

        /// <summary>
        /// 计算指定物品的综合便利收益率
        /// </summary>
        /// <param name="itemId">物品ID（例如："24"代表防风草）</param>
        /// <param name="baseYield">基础便利收益率（从配置文件读取）</param>
        /// <returns>综合便利收益率（基础 + NPC生日加成 + 社区中心加成）</returns>
        /// <remarks>
        /// 计算公式：q_total = q_base + q_birthday + q_bundle
        /// 
        /// 典型值：
        /// - 基础：0.001 (0.1%)
        /// - NPC生日加成：0.10 (10%)
        /// - 社区中心加成：0.005 (0.5%)
        /// </remarks>
        public double GetConvenienceYield(string itemId, double baseYield)
        {
            double totalYield = baseYield;

            // 检查NPC生日加成
            double birthdayBonus = CheckNPCBirthdayBonus(itemId);
            totalYield += birthdayBonus;

            // 检查社区中心加成
            double bundleBonus = CheckCommunityBundleBonus(itemId);
            totalYield += bundleBonus;

            if (birthdayBonus > 0 || bundleBonus > 0)
            {
                _monitor.Log(
                    $"[ConvenienceYield] ItemID={itemId}: Base={baseYield:F4}, " +
                    $"Birthday={birthdayBonus:F4}, Bundle={bundleBonus:F4}, Total={totalYield:F4}",
                    LogLevel.Debug
                );
            }

            return totalYield;
        }

        /// <summary>
        /// 检查NPC生日对便利收益率的影响
        /// </summary>
        /// <param name="itemId">物品ID</param>
        /// <returns>便利收益率加成值（0 表示无加成，0.10 表示 10% 加成）</returns>
        /// <remarks>
        /// 逻辑：
        /// 1. 遍历所有NPC
        /// 2. 检查今天是否是该NPC的生日
        /// 3. 检查该NPC是否喜欢（Love或Like）此物品
        /// 4. 如果满足条件，返回高额加成（10%）
        /// 
        /// 经济含义：
        /// 如果今天是海莉生日且她喜欢向日葵，则持有向日葵现货的价值极高
        /// （可以送礼获得好感度），因此期货价格会低于现货价格（贴水）
        /// </remarks>
        private double CheckNPCBirthdayBonus(string itemId)
        {
            try
            {
                // 遍历所有NPC
                foreach (var npc in Utility.getAllCharacters())
                {
                    // 检查今天是否是该NPC的生日
                    if (npc.isBirthday())
                    {
                        // 创建物品实例用于检查喜好度
                        // 使用 ItemRegistry.Create 是 Stardew Valley 1.6+ 的推荐方式
                        var item = ItemRegistry.Create(itemId);
                        
                        if (item != null)
                        {
                            // 检查该NPC对此物品的喜好度
                            // getGiftTasteForThisItem() 返回值：
                            // 0 = Love (最喜欢)
                            // 2 = Like (喜欢)
                            // 4 = Dislike (不喜欢)
                            // 6 = Hate (讨厌)
                            // 8 = Neutral (中立)
                            int giftTaste = npc.getGiftTasteForThisItem(item);

                            if (giftTaste == NPC.gift_taste_love || giftTaste == NPC.gift_taste_like)
                            {
                                _monitor.Log(
                                    $"[ConvenienceYield] 🎂 {npc.Name}'s Birthday! Loves/Likes ItemID={itemId}. " +
                                    $"Convenience Yield +10%",
                                    LogLevel.Info
                                );
                                return 0.10; // 10% 加成
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _monitor.Log($"[ConvenienceYield] Error checking NPC birthdays: {ex.Message}", LogLevel.Warn);
            }

            return 0.0; // 无加成
        }

        /// <summary>
        /// 检查社区中心Bundle对便利收益率的影响
        /// </summary>
        /// <param name="itemId">物品ID</param>
        /// <returns>便利收益率加成值（0 表示无加成，0.005 表示 0.5% 加成）</returns>
        /// <remarks>
        /// WHY (为什么这样实现):
        /// 精确检查社区中心Bundle需求需要深入解析 Bundles.json 或使用
        /// CommunityCenter.bundleData API，较为复杂且容易因游戏版本更新而失效。
        /// 
        /// 当前简化实现：
        /// - 如果社区中心存在（即未完成修复），返回固定加成 0.5%
        /// - 表示"社区中心需求期"，所有农作物都有一定便利价值
        /// 
        /// 未来优化（见 task.md 未来优化项）：
        /// - 读取 Bundles.json 或使用 bundleData API
        /// - 精确匹配当前未完成的Bundle需求物品
        /// - 根据Bundle紧急程度动态调整加成（例如：秋季收获Bundle需要南瓜 → +5-10%）
        /// </remarks>
        private double CheckCommunityBundleBonus(string itemId)
        {
            try
            {
                // 检查社区中心是否存在且未完全修复
                var communityCenter = Game1.getLocationFromName("CommunityCenter") as StardewValley.Locations.CommunityCenter;
                
                if (communityCenter != null && !communityCenter.areAllAreasComplete())
                {
                    // 简化实现：返回固定加成
                    // 表示"社区中心需求期"，所有农作物都有一定额外价值
                    return 0.005; // 0.5% 加成
                }
            }
            catch (Exception ex)
            {
                _monitor.Log($"[ConvenienceYield] Error checking Community Center: {ex.Message}", LogLevel.Warn);
            }

            return 0.0; // 无加成或社区中心已修复
        }
    }
}
