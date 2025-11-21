// ============================================================================
// 星露资本 (Stardew Capital)
// 模块：交易终端界面
// 作者：Stardew Capital Team
// 用途：定义交易菜单标签页的通用接口
// ============================================================================

using Microsoft.Xna.Framework.Graphics;

namespace StardewCapital.UI.Tabs
{
    /// <summary>
    /// 交易菜单标签页接口
    /// </summary>
    public interface ITradingTab
    {
        /// <summary>
        /// 绘制标签页内容
        /// </summary>
        /// <param name="b">SpriteBatch 用于绘图</param>
        void Draw(SpriteBatch b);

        /// <summary>
        /// 处理鼠标左键点击事件
        /// </summary>
        /// <param name="x">鼠标X坐标</param>
        /// <param name="y">鼠标Y坐标</param>
        /// <returns>如果事件被处理则返回true</returns>
        bool ReceiveLeftClick(int x, int y);

        /// <summary>
        /// 处理鼠标滚轮事件
        /// </summary>
        /// <param name="direction">滚动方向（正数向上，负数向下）</param>
        void ReceiveScrollWheelAction(int direction);
    }
}
