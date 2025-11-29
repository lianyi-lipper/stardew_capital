// TimePatch.cs
using System;
using StardewModdingAPI;
using StardewValley;
using Microsoft.Xna.Framework; // <-- [新增] 为 GameTime 添加
using StardewValley.Menus;     // <-- [新增] 良好的 practice

namespace StardewCapital
{
    public static class TimePatch
    {
        private static IMonitor _Monitor;

        // 由 ModEntry 调用，用于传递 Monitor 实例
        public static void Initialize(IMonitor monitor)
        {
            _Monitor = monitor;
        }

        /// <summary>补丁 1: 在 Game1.shouldTimePass() 方法后运行。</summary>
        /// <param name="__result">原始方法的返回值。</param>
        public static void ShouldTimePass_Postfix(ref bool __result)
        {
            try
            {
                // [重要] 我们只在自己的菜单打开时强制时间流逝
                if (!__result && Game1.activeClickableMenu is StardewCapitalMenu)
                {
                    // 强制让时间继续流逝 (这会影响UI)
                    __result = true;
                }
            }
            catch (Exception ex)
            {
                _Monitor.Log($"Failed in {nameof(ShouldTimePass_Postfix)}:\n{ex}", LogLevel.Error);
            }
        }

        /// <summary>补丁 2: 在 Game1._update() 方法后运行。</summary>
        /// <remarks>这是确保时间真正流逝的核心。</remarks>
        public static void Update_Postfix(GameTime gameTime)
        {
            try
            {
                // 这个逻辑只应在单人游戏且有菜单打开时运行
                if (Game1.IsMultiplayer || Game1.activeClickableMenu == null)
                {
                    return;
                }

                // 检查 Game1.shouldTimePass() (它会运行我们的补丁 1)
                // 如果它返回 true (意味着我们的菜单是打开的),
                // 我们就必须手动调用时钟更新, 因为主循环会跳过它。
                if (Game1.shouldTimePass())
                {
                    Game1.UpdateGameClock(gameTime);
                }
            }
            catch (Exception ex)
            {
                _Monitor.Log($"Failed in {nameof(Update_Postfix)}:\n{ex}", LogLevel.Error);
            }
        }
    }
}