// ============================================================================
// 星露资本 (Stardew Capital)
// 模块：时间补丁（Harmony Patch）
// 作者：Stardew Capital Team
// 用途：强制游戏时间在交易UI打开时继续流逝
// ============================================================================

using System;
using HarmonyLib;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;
using StardewCapital.UI;

namespace StardewCapital.Patches
{
    /// <summary>
    /// 时间补丁类
    /// 
    /// 核心功能：
    /// - 拦截 Game1.shouldTimePass() 方法，让交易UI打开时时间继续流逝
    /// - 拦截 Game1._update() 方法，手动更新游戏时钟
    /// 
    /// 设计意图：
    /// 1. 防止作弊：如果UI暂停时间，玩家可以打开菜单查看价格后再关闭等待时机
    /// 2. 实时交易：价格必须在UI打开状态下继续波动，模拟真实金融市场
    /// 3. 高频交易：只有时间不暂停，才能实现基于真实时间的Tick更新
    /// </summary>
    public static class TimePatch
    {
        private static IMonitor? _monitor;

        /// <summary>
        /// 初始化补丁（由 ModEntry 调用）
        /// </summary>
        /// <param name="monitor">日志监视器</param>
        public static void Initialize(IMonitor monitor)
        {
            _monitor = monitor;
        }

        /// <summary>
        /// 补丁 1: 拦截 Game1.shouldTimePass() 方法
        /// 
        /// 原理：
        /// - 游戏默认在打开菜单时返回 false，暂停时间流逝
        /// - 当检测到 TradingMenu 打开时，强制返回 true
        /// - 这会影响UI显示（时间继续走）和补丁2的检查
        /// </summary>
        /// <param name="__result">原始方法的返回值（通过 ref 可以修改）</param>
        public static void ShouldTimePass_Postfix(ref bool __result)
        {
            try
            {
                // 只在以下情况下强制时间流逝：
                // 1. 原始方法返回 false（表示游戏想暂停时间）
                // 2. 当前打开的菜单是 TradingMenu
                if (!__result && Game1.activeClickableMenu is TradingMenu)
                {
                    // 强制让时间继续流逝
                    __result = true;
                }
            }
            catch (Exception ex)
            {
                _monitor?.Log($"[TimePatch] Error in ShouldTimePass_Postfix: {ex}", LogLevel.Error);
            }
        }

        /// <summary>
        /// 补丁 2: 拦截 Game1._update() 方法
        /// 
        /// 原理：
        /// - 游戏的主循环在检测到菜单打开时会跳过时钟更新
        /// - 我们在 _update() 后手动调用 UpdateGameClock()
        /// - 只在单人模式且补丁1生效时执行
        /// </summary>
        /// <param name="gameTime">游戏时间对象</param>
        public static void Update_Postfix(GameTime gameTime)
        {
            try
            {
                // 只在以下情况下更新时钟：
                // 1. 不是多人游戏（多人游戏时间由服务器控制）
                // 2. 有菜单打开
                // 3. shouldTimePass() 返回 true（补丁1生效）
                if (Game1.IsMultiplayer || Game1.activeClickableMenu == null)
                {
                    return;
                }

                // 检查 shouldTimePass()（会触发我们的补丁1）
                // 如果返回 true（意味着 TradingMenu 打开），则手动更新时钟
                if (Game1.shouldTimePass())
                {
                    Game1.UpdateGameClock(gameTime);
                }
            }
            catch (Exception ex)
            {
                _monitor?.Log($"[TimePatch] Error in Update_Postfix: {ex}", LogLevel.Error);
            }
        }
    }
}
