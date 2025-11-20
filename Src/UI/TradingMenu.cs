using System;
using System.Collections.Generic;
using System.Linq;
using HedgeHarvest.Services;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;
using StardewValley.Menus;
using StardewModdingAPI;

namespace HedgeHarvest.UI
{
    public class TradingMenu : IClickableMenu
    {
        private enum Tab { Market, Account, Positions }
        
        private readonly MarketManager _marketManager;
        private readonly BrokerageService _brokerageService;
        private readonly IMonitor _monitor;

        private Tab _currentTab = Tab.Market;
        private List<ClickableComponent> _tabButtons = new();

        // Market Tab Components
        private ClickableComponent _buyButton = null!;
        private ClickableComponent _sellButton = null!;
        private ClickableComponent _webButton = null!;
        private List<ClickableComponent> _leverageButtons = new();
        private int _selectedLeverage = 1;

        // Account Tab Components
        private ClickableComponent _depositButton = null!;
        private ClickableComponent _withdrawButton = null!;

        private string _statusMessage = "";

        public TradingMenu(MarketManager marketManager, BrokerageService brokerageService, IMonitor monitor)
            : base(Game1.viewport.Width / 2 - 400, Game1.viewport.Height / 2 - 300, 800, 600, true)
        {
            _marketManager = marketManager;
            _brokerageService = brokerageService;
            _monitor = monitor;

            InitializeComponents();
        }

        private void InitializeComponents()
        {
            int x = xPositionOnScreen;
            int y = yPositionOnScreen;
            int centerX = x + width / 2;
            int centerY = y + height / 2;

            // --- Tabs ---
            _tabButtons.Clear();
            int tabWidth = 150;
            _tabButtons.Add(new ClickableComponent(new Rectangle(x + 50, y + 100, tabWidth, 48), "Market") { myID = 0 });
            _tabButtons.Add(new ClickableComponent(new Rectangle(x + 50 + tabWidth + 10, y + 100, tabWidth, 48), "Account") { myID = 1 });
            _tabButtons.Add(new ClickableComponent(new Rectangle(x + 50 + (tabWidth + 10) * 2, y + 100, tabWidth, 48), "Positions") { myID = 2 });

            // --- Market Tab ---
            _buyButton = new ClickableComponent(new Rectangle(centerX - 160, centerY + 50, 150, 64), "Buy");
            _sellButton = new ClickableComponent(new Rectangle(centerX + 10, centerY + 50, 150, 64), "Sell");
            
            _leverageButtons.Clear();
            _leverageButtons.Add(new ClickableComponent(new Rectangle(centerX - 120, centerY - 20, 60, 40), "1x") { myID = 1 });
            _leverageButtons.Add(new ClickableComponent(new Rectangle(centerX - 40, centerY - 20, 60, 40), "5x") { myID = 5 });
            _leverageButtons.Add(new ClickableComponent(new Rectangle(centerX + 40, centerY - 20, 60, 40), "10x") { myID = 10 });

            _webButton = new ClickableComponent(new Rectangle(centerX - 100, centerY + 150, 200, 48), "Web Terminal");

            // --- Account Tab ---
            _depositButton = new ClickableComponent(new Rectangle(centerX - 160, centerY + 80, 150, 64), "Deposit");
            _withdrawButton = new ClickableComponent(new Rectangle(centerX + 10, centerY + 80, 150, 64), "Withdraw");
        }

        public override void draw(SpriteBatch b)
        {
            // 1. Draw Background
            b.Draw(Game1.fadeToBlackRect, Game1.graphics.GraphicsDevice.Viewport.Bounds, Color.Black * 0.75f);
            Game1.drawDialogueBox(xPositionOnScreen, yPositionOnScreen, width, height, false, true);

            // 2. Draw Title
            string title = "HedgeHarvest Terminal";
            Utility.drawTextWithShadow(b, title, Game1.dialogueFont, 
                new Vector2(xPositionOnScreen + width / 2 - Game1.dialogueFont.MeasureString(title).X / 2, yPositionOnScreen + 35), 
                Game1.textColor);

            // 3. Draw Tabs
            foreach (var tab in _tabButtons)
            {
                bool isSelected = (int)_currentTab == tab.myID;
                IClickableMenu.drawTextureBox(b, Game1.mouseCursors, new Rectangle(403, 373, 9, 9), 
                    tab.bounds.X, tab.bounds.Y, tab.bounds.Width, tab.bounds.Height, 
                    isSelected ? Color.Gold : Color.White, 4f, false);
                
                Vector2 textSize = Game1.smallFont.MeasureString(tab.name);
                Utility.drawTextWithShadow(b, tab.name, Game1.smallFont, 
                    new Vector2(tab.bounds.X + (tab.bounds.Width - textSize.X) / 2, tab.bounds.Y + 12), 
                    Game1.textColor);
            }

            // 4. Draw Content based on Tab
            switch (_currentTab)
            {
                case Tab.Market:
                    DrawMarketTab(b);
                    break;
                case Tab.Account:
                    DrawAccountTab(b);
                    break;
                case Tab.Positions:
                    DrawPositionsTab(b);
                    break;
            }

            // 5. Draw Status Bar
            if (!string.IsNullOrEmpty(_statusMessage))
            {
                Utility.drawTextWithShadow(b, _statusMessage, Game1.smallFont,
                    new Vector2(xPositionOnScreen + width / 2 - Game1.smallFont.MeasureString(_statusMessage).X / 2, yPositionOnScreen + height - 60),
                    Color.DarkSlateGray);
            }

            drawMouse(b);
        }

        private void DrawMarketTab(SpriteBatch b)
        {
            int centerX = xPositionOnScreen + width / 2;
            int centerY = yPositionOnScreen + height / 2;

            // Price Display
            var instruments = _marketManager.GetInstruments();
            if (instruments.Count > 0)
            {
                var inst = instruments[0];
                string symbolText = inst.Symbol;
                string priceText = $"{inst.CurrentPrice:F2} g";

                // Symbol
                Utility.drawTextWithShadow(b, symbolText, Game1.dialogueFont,
                    new Vector2(centerX - Game1.dialogueFont.MeasureString(symbolText).X / 2, centerY - 120),
                    Game1.textColor);
                
                // Price (Big)
                Vector2 priceSize = Game1.dialogueFont.MeasureString(priceText);
                Utility.drawTextWithShadow(b, priceText, Game1.dialogueFont,
                    new Vector2(centerX - priceSize.X / 2, centerY - 80),
                    Color.DarkGreen);
            }

            // Leverage Selector
            Utility.drawTextWithShadow(b, "Leverage:", Game1.smallFont, new Vector2(centerX - 200, centerY - 15), Game1.textColor);
            foreach (var btn in _leverageButtons)
            {
                bool isSelected = _selectedLeverage == btn.myID;
                DrawButton(b, btn, isSelected ? Color.Gold : Color.LightGray);
            }

            // Action Buttons
            DrawButton(b, _buyButton, Color.LightGreen);
            DrawButton(b, _sellButton, Color.LightCoral);
            DrawButton(b, _webButton, Color.LightBlue);
        }

        private void DrawAccountTab(SpriteBatch b)
        {
            int leftX = xPositionOnScreen + 100;
            int topY = yPositionOnScreen + 200;

            var prices = GetCurrentPrices();
            var account = _brokerageService.Account;

            DrawStatRow(b, "Cash Balance:", $"{account.Cash:N0} g", leftX, topY);
            DrawStatRow(b, "Equity:", $"{account.GetTotalEquity(prices):N0} g", leftX, topY + 40);
            DrawStatRow(b, "Used Margin:", $"{account.GetUsedMargin(prices):N0} g", leftX, topY + 80);
            
            decimal freeMargin = account.GetFreeMargin(prices);
            DrawStatRow(b, "Free Margin:", $"{freeMargin:N0} g", leftX, topY + 120, freeMargin >= 0 ? Game1.textColor : Color.DarkRed);

            // Buttons
            DrawButton(b, _depositButton, Color.Orange);
            DrawButton(b, _withdrawButton, Color.Orange);
        }

        private void DrawPositionsTab(SpriteBatch b)
        {
            int leftX = xPositionOnScreen + 60;
            int topY = yPositionOnScreen + 180;

            // Header
            b.DrawString(Game1.smallFont, "Symbol", new Vector2(leftX, topY), Game1.textColor);
            b.DrawString(Game1.smallFont, "Qty", new Vector2(leftX + 200, topY), Game1.textColor);
            b.DrawString(Game1.smallFont, "Entry", new Vector2(leftX + 300, topY), Game1.textColor);
            b.DrawString(Game1.smallFont, "PnL", new Vector2(leftX + 450, topY), Game1.textColor);
            
            // Divider
            b.Draw(Game1.staminaRect, new Rectangle(leftX, topY + 25, 600, 2), Color.DarkGray);

            // Rows
            var prices = GetCurrentPrices();
            int row = 0;
            foreach (var pos in _brokerageService.Account.Positions)
            {
                int y = topY + 35 + (row * 30);
                if (prices.TryGetValue(pos.Symbol, out decimal currentPrice))
                {
                    decimal pnl = pos.GetUnrealizedPnL(currentPrice);
                    Color pnlColor = pnl >= 0 ? Color.DarkGreen : Color.DarkRed;

                    b.DrawString(Game1.smallFont, pos.Symbol, new Vector2(leftX, y), Game1.textColor);
                    b.DrawString(Game1.smallFont, $"{pos.Quantity} (x{pos.Leverage})", new Vector2(leftX + 200, y), Game1.textColor);
                    b.DrawString(Game1.smallFont, $"{pos.AverageCost:F1}", new Vector2(leftX + 300, y), Game1.textColor);
                    b.DrawString(Game1.smallFont, $"{pnl:F1} g", new Vector2(leftX + 450, y), pnlColor);
                }
                row++;
            }

            if (row == 0)
            {
                b.DrawString(Game1.smallFont, "No open positions.", new Vector2(leftX, topY + 50), Color.Gray);
            }
        }

        private void DrawStatRow(SpriteBatch b, string label, string value, int x, int y, Color? valueColor = null)
        {
            b.DrawString(Game1.smallFont, label, new Vector2(x, y), Game1.textColor);
            b.DrawString(Game1.smallFont, value, new Vector2(x + 250, y), valueColor ?? Game1.textColor);
        }

        private void DrawButton(SpriteBatch b, ClickableComponent btn, Color color)
        {
            IClickableMenu.drawTextureBox(b, Game1.mouseCursors, new Rectangle(403, 373, 9, 9), 
                btn.bounds.X, btn.bounds.Y, btn.bounds.Width, btn.bounds.Height, color, 4f, false);
            
            Vector2 textSize = Game1.smallFont.MeasureString(btn.name);
            Vector2 textPos = new Vector2(
                btn.bounds.X + (btn.bounds.Width - textSize.X) / 2, 
                btn.bounds.Y + (btn.bounds.Height - textSize.Y) / 2);
            
            Utility.drawTextWithShadow(b, btn.name, Game1.smallFont, textPos, Game1.textColor);
        }

        public override void receiveLeftClick(int x, int y, bool playSound = true)
        {
            base.receiveLeftClick(x, y, playSound);

            // Tab Switching
            foreach (var tab in _tabButtons)
            {
                if (tab.containsPoint(x, y))
                {
                    _currentTab = (Tab)tab.myID;
                    Game1.playSound("smallSelect");
                    return;
                }
            }

            // Tab Specific Actions
            switch (_currentTab)
            {
                case Tab.Market:
                    HandleMarketClick(x, y);
                    break;
                case Tab.Account:
                    HandleAccountClick(x, y);
                    break;
            }
        }

        private void HandleMarketClick(int x, int y)
        {
            if (_buyButton.containsPoint(x, y))
            {
                ExecuteTrade(1);
            }
            else if (_sellButton.containsPoint(x, y))
            {
                ExecuteTrade(-1);
            }
            else if (_webButton.containsPoint(x, y))
            {
                Game1.playSound("bigSelect");
                try
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = "http://localhost:5000",
                        UseShellExecute = true
                    });
                }
                catch { _statusMessage = "Error opening browser"; }
            }

            foreach (var btn in _leverageButtons)
            {
                if (btn.containsPoint(x, y))
                {
                    _selectedLeverage = btn.myID;
                    Game1.playSound("smallSelect");
                }
            }
        }

        private void HandleAccountClick(int x, int y)
        {
            if (_depositButton.containsPoint(x, y))
            {
                Game1.playSound("coin");
                _brokerageService.Deposit(1000);
                _statusMessage = "Deposited 1000g";
            }
            else if (_withdrawButton.containsPoint(x, y))
            {
                Game1.playSound("coin");
                _brokerageService.Withdraw(1000);
                _statusMessage = "Withdrawn 1000g";
            }
        }

        private void ExecuteTrade(int direction)
        {
            Game1.playSound("coin");
            var instruments = _marketManager.GetInstruments();
            if (instruments.Count > 0)
            {
                string symbol = instruments[0].Symbol;
                _brokerageService.ExecuteOrder(symbol, direction, _selectedLeverage);
                _statusMessage = $"{(direction > 0 ? "Buy" : "Sell")} Order Sent ({symbol} x{_selectedLeverage})";
            }
        }

        private Dictionary<string, decimal> GetCurrentPrices()
        {
            var dict = new Dictionary<string, decimal>();
            foreach (var inst in _marketManager.GetInstruments())
            {
                dict[inst.Symbol] = (decimal)inst.CurrentPrice;
            }
            return dict;
        }
    }
}
