using System.Collections.Generic;
using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.Objects;

namespace StardewCapital.Services.Infrastructure
{
    /// <summary>
    /// 交易所服务
    /// 管理"交易所箱子"的标记和查找，用于期货实物交割。
    /// 
    /// 核心概念：
    /// 玩家可以将普通箱子标记为"交易所箱子"，期货到期时：
    /// - 多头仓位：物品优先放入交易所箱子
    /// - 空头仓位：物品优先从交易所箱子扣除
    /// 
    /// 实现方式：
    /// 使用SMAPI的modData字典存储标记，用颜色（金色）作为视觉提示。
    /// </summary>
    public class ExchangeService
    {
        /// <summary>交易所箱子的标记键（存储在Chest.modData中）</summary>
        public const string EXCHANGE_KEY = "StardewCapital.Exchange";

        /// <summary>
        /// 判断箱子是否为交易所箱子
        /// </summary>
        /// <param name="chest">要检查的箱子对象</param>
        /// <returns>true表示是交易所箱子</returns>
        public bool IsExchangeBox(Chest chest)
        {
            return chest.modData.ContainsKey(EXCHANGE_KEY);
        }

        /// <summary>
        /// 切换箱子的交易所状态（标记/取消标记）
        /// </summary>
        /// <param name="chest">目标箱子</param>
        public void ToggleExchangeStatus(Chest chest)
        {
            if (IsExchangeBox(chest))
            {
                // 取消标记
                chest.modData.Remove(EXCHANGE_KEY);
                // 重置为默认颜色（黑色通常表示无颜色）
                chest.playerChoiceColor.Value = Color.Black; 
            }
            else
            {
                // 标记为交易所箱子
                chest.modData[EXCHANGE_KEY] = "true";
                // 设置为金色作为视觉提示
                chest.playerChoiceColor.Value = Color.Gold;
            }
        }

        /// <summary>
        /// 查找所有被标记为交易所的箱子
        /// 遍历所有游戏位置（农场、建筑物等）
        /// </summary>
        /// <returns>所有交易所箱子的列表</returns>
        public List<Chest> FindAllExchangeBoxes()
        {
            var boxes = new List<Chest>();
            
            // 遍历所有游戏位置
            foreach (var location in Game1.locations)
            {
                foreach (var obj in location.Objects.Values)
                {
                    if (obj is Chest chest && IsExchangeBox(chest))
                    {
                        boxes.Add(chest);
                    }
                }
                
                // Game1.locations 已经包含了所有活跃位置，包括建筑物内部
                // 例如：小屋（Cabins）、棚屋（Sheds）等
            }
            
            return boxes;
        }
    }
}
