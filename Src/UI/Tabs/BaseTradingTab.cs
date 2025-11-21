// ============================================================================
// 星露资本 (Stardew Capital)
// 模块：交易终端界面
// 作者：Stardew Capital Team
// 用途：交易菜单标签页的基类，提供通用功能
// ============================================================================

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;
using StardewValley.Menus;
using StardewModdingAPI;

namespace StardewCapital.UI.Tabs
{
    /// <summary>
    /// 交易菜单标签页基类
    /// 
    /// 提供通用的绘图辅助方法和基础属性。
    /// </summary>
    public abstract class BaseTradingTab : ITradingTab
    {
        protected readonly IMonitor Monitor;
        protected int XPositionOnScreen;
        protected int YPositionOnScreen;
        protected int Width;
        protected int Height;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="monitor">日志监视器</param>
        /// <param name="x">屏幕X坐标</param>
        /// <param name="y">屏幕Y坐标</param>
        /// <param name="width">宽度</param>
        /// <param name="height">高度</param>
        protected BaseTradingTab(IMonitor monitor, int x, int y, int width, int height)
        {
            Monitor = monitor;
            XPositionOnScreen = x;
            YPositionOnScreen = y;
            Width = width;
            Height = height;
        }

        /// <summary>
        /// 绘制标签页内容（由子类实现）
        /// </summary>
        public abstract void Draw(SpriteBatch b);

        /// <summary>
        /// 处理鼠标左键点击事件（由子类实现）
        /// </summary>
        public abstract bool ReceiveLeftClick(int x, int y);

        /// <summary>
        /// 处理鼠标滚轮事件（默认不处理，由子类覆盖）
        /// </summary>
        public virtual void ReceiveScrollWheelAction(int direction)
        {
        }

        /// <summary>
        /// 绘制按钮
        /// </summary>
        /// <param name="b">SpriteBatch</param>
        /// <param name="btn">按钮组件</param>
        /// <param name="color">按钮颜色</param>
        /// <remarks>
        /// 封装了通用的按钮绘制逻辑：
        /// 1. 绘制纹理背景
        /// 2. 居中绘制文字
        /// </remarks>
        protected void DrawButton(SpriteBatch b, ClickableComponent btn, Color color)
        {
            IClickableMenu.drawTextureBox(b, Game1.mouseCursors, new Rectangle(403, 373, 9, 9), 
                btn.bounds.X, btn.bounds.Y, btn.bounds.Width, btn.bounds.Height, color, 4f, false);
            
            Vector2 textSize = Game1.smallFont.MeasureString(btn.name);
            Vector2 textPos = new Vector2(
                btn.bounds.X + (btn.bounds.Width - textSize.X) / 2, 
                btn.bounds.Y + (btn.bounds.Height - textSize.Y) / 2);
            
            Utility.drawTextWithShadow(b, btn.name, Game1.smallFont, textPos, Game1.textColor);
        }

        /// <summary>
        /// 绘制统计数据行（标签 + 数值）
        /// </summary>
        /// <param name="b">SpriteBatch</param>
        /// <param name="label">标签文本</param>
        /// <param name="value">数值文本</param>
        /// <param name="x">X坐标</param>
        /// <param name="y">Y坐标</param>
        /// <param name="valueColor">数值颜色（可选，默认为文本色）</param>
        protected void DrawStatRow(SpriteBatch b, string label, string value, int x, int y, Color? valueColor = null)
        {
            b.DrawString(Game1.smallFont, label, new Vector2(x, y), Game1.textColor);
            b.DrawString(Game1.smallFont, value, new Vector2(x + 250, y), valueColor ?? Game1.textColor);
        }
    }
}
