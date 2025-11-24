using StardewCapital.Services.Infrastructure;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Menus;
using StardewValley.Objects;

namespace StardewCapital.UI
{
    /// <summary>
    /// 交易所箱子菜单控制器
    /// 在箱子菜单中注入一个按钮，用于标记/取消标记"交易所箱子"。
    /// 
    /// 工作机制：
    /// 1. 监听箱子菜单打开事件（MenuChanged）
    /// 2. 在箱子菜单右侧绘制按钮（RenderedActiveMenu）
    /// 3. 处理按钮点击（ButtonPressed）
    /// 
    /// UI反馈：
    /// - 未标记：灰色 $ 符号
    /// - 已标记：金色 $ 符号 + 箱子变金色
    /// - 鼠标悬停：显示提示文字
    /// </summary>
    public class ExchangeMenuController
    {
        private readonly IMonitor _monitor;
        private readonly ExchangeService _exchangeService;
        private readonly IModHelper _helper;

        private ClickableComponent? _exchangeButton;
        private Chest? _currentChest;

        /// <summary>
        /// 初始化交易所菜单控制器
        /// 注册事件处理器（菜单变化、菜单绘制、按键按下）
        /// </summary>
        public ExchangeMenuController(IModHelper helper, IMonitor monitor, ExchangeService exchangeService)
        {
            _helper = helper;
            _monitor = monitor;
            _exchangeService = exchangeService;

            helper.Events.Display.MenuChanged += OnMenuChanged;
            helper.Events.Display.RenderedActiveMenu += OnRenderedActiveMenu;
            helper.Events.Input.ButtonPressed += OnButtonPressed;
        }

        /// <summary>
        /// 处理菜单变化事件
        /// 检测箱子菜单打开，初始化交易所按钮位置
        /// </summary>
        private void OnMenuChanged(object? sender, MenuChangedEventArgs e)
        {
            _exchangeButton = null;
            _currentChest = null;

            if (e.NewMenu is ItemGrabMenu menu && menu.context is Chest chest)
            {
                _currentChest = chest;
                // 将按钮定位在菜单右侧
                _exchangeButton = new ClickableComponent(
                    new Rectangle(menu.xPositionOnScreen + menu.width + 16, menu.yPositionOnScreen + 64, 64, 64), 
                    "Exchange"
                );
            }
        }

        /// <summary>
        /// 处理菜单绘制事件
        /// 在箱子菜单右侧绘制交易所按钮
        /// </summary>
        private void OnRenderedActiveMenu(object? sender, RenderedActiveMenuEventArgs e)
        {
            if (_exchangeButton != null && _currentChest != null)
            {
                bool isExchange = _exchangeService.IsExchangeBox(_currentChest);
                
                // 绘制按钮背景
                IClickableMenu.drawTextureBox(e.SpriteBatch, Game1.mouseCursors, new Rectangle(403, 373, 9, 9), 
                    _exchangeButton.bounds.X, _exchangeButton.bounds.Y, _exchangeButton.bounds.Width, _exchangeButton.bounds.Height, 
                    Color.White, 4f, false);

                // 绘制 $ 符号（代表交易所）
                // 金色 = 已标记，灰色 = 未标记
                Utility.drawTextWithShadow(e.SpriteBatch, "$", Game1.dialogueFont, 
                    new Vector2(_exchangeButton.bounds.X + 18, _exchangeButton.bounds.Y + 8), 
                    isExchange ? Color.Gold : Color.Gray);
                
                // 鼠标悬停时显示提示文字
                if (_exchangeButton.containsPoint(Game1.getMouseX(), Game1.getMouseY()))
                {
                    IClickableMenu.drawHoverText(e.SpriteBatch, isExchange ? "Exchange Box (Active)" : "Set as Exchange Box", Game1.smallFont);
                }
            }
        }

        /// <summary>
        /// 处理按键按下事件
        /// 检测点击交易所按钮，切换箱子的交易所状态
        /// </summary>
        private void OnButtonPressed(object? sender, ButtonPressedEventArgs e)
        {
            if (e.Button == SButton.MouseLeft && _exchangeButton != null && _currentChest != null)
            {
                if (_exchangeButton.containsPoint((int)e.Cursor.ScreenPixels.X, (int)e.Cursor.ScreenPixels.Y))
                {
                    Game1.playSound("coin");
                    _exchangeService.ToggleExchangeStatus(_currentChest);
                }
            }
        }
    }
}
