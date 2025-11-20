using HedgeHarvest.Services;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Menus;
using StardewValley.Objects;

namespace HedgeHarvest.UI
{
    public class ExchangeMenuController
    {
        private readonly IMonitor _monitor;
        private readonly ExchangeService _exchangeService;
        private readonly IModHelper _helper;

        private ClickableComponent? _exchangeButton;
        private Chest? _currentChest;

        public ExchangeMenuController(IModHelper helper, IMonitor monitor, ExchangeService exchangeService)
        {
            _helper = helper;
            _monitor = monitor;
            _exchangeService = exchangeService;

            helper.Events.Display.MenuChanged += OnMenuChanged;
            helper.Events.Display.RenderedActiveMenu += OnRenderedActiveMenu;
            helper.Events.Input.ButtonPressed += OnButtonPressed;
        }

        private void OnMenuChanged(object? sender, MenuChangedEventArgs e)
        {
            _exchangeButton = null;
            _currentChest = null;

            if (e.NewMenu is ItemGrabMenu menu && menu.context is Chest chest)
            {
                _currentChest = chest;
                // Position the button to the right of the menu
                _exchangeButton = new ClickableComponent(
                    new Rectangle(menu.xPositionOnScreen + menu.width + 16, menu.yPositionOnScreen + 64, 64, 64), 
                    "Exchange"
                );
            }
        }

        private void OnRenderedActiveMenu(object? sender, RenderedActiveMenuEventArgs e)
        {
            if (_exchangeButton != null && _currentChest != null)
            {
                bool isExchange = _exchangeService.IsExchangeBox(_currentChest);
                
                // Draw Button Background
                IClickableMenu.drawTextureBox(e.SpriteBatch, Game1.mouseCursors, new Rectangle(403, 373, 9, 9), 
                    _exchangeButton.bounds.X, _exchangeButton.bounds.Y, _exchangeButton.bounds.Width, _exchangeButton.bounds.Height, 
                    Color.White, 4f, false);

                // Draw Icon (Coin or Star)
                // Using a coin icon from mouseCursors (approximate coordinates)
                // Coin is usually around 211, 373 in mouseCursors 1.5? 
                // Let's use a safe icon, maybe the Star (Gold Star)
                // Rectangle(173, 373, 9, 9) is often a box.
                // Let's try to draw a simple "E" or "$" text if icon is hard to find without checking sheet.
                // Actually, let's use the "Star" from the title menu or similar.
                // Or just draw text "$".
                
                Utility.drawTextWithShadow(e.SpriteBatch, "$", Game1.dialogueFont, 
                    new Vector2(_exchangeButton.bounds.X + 18, _exchangeButton.bounds.Y + 8), 
                    isExchange ? Color.Gold : Color.Gray);
                
                if (_exchangeButton.containsPoint(Game1.getMouseX(), Game1.getMouseY()))
                {
                    IClickableMenu.drawHoverText(e.SpriteBatch, isExchange ? "Exchange Box (Active)" : "Set as Exchange Box", Game1.smallFont);
                }
            }
        }

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
