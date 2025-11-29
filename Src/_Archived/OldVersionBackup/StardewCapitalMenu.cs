// StardewCapitalMenu.cs

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;
using StardewValley.Menus;
using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace StardewCapital
{
    public class StardewCapitalMenu : IClickableMenu
    {
        private readonly FuturesMarket _market;
        private string _currentTab = "行情";
        private List<ClickableComponent> _tabs = new List<ClickableComponent>();


        // --- Positions Tab Components ---
        private List<ClickableTextureComponent> _closeButtons = new List<ClickableTextureComponent>();


        public StardewCapitalMenu(FuturesMarket market)
        {
            this._market = market;
            int menuWidth = 800;
            int menuHeight = 600;
            int x = (Game1.uiViewport.Width - menuWidth) / 2;
            int y = (Game1.uiViewport.Height - menuHeight) / 2;

            initialize(x, y, menuWidth, menuHeight, true);

            // --- Initialize Tabs ---
            _tabs.Add(new ClickableComponent(new Rectangle(xPositionOnScreen + 64, yPositionOnScreen + 16, 120, 64), "行情"));
            _tabs.Add(new ClickableComponent(new Rectangle(xPositionOnScreen + 184, yPositionOnScreen + 16, 120, 64), "交易"));
            _tabs.Add(new ClickableComponent(new Rectangle(xPositionOnScreen + 304, yPositionOnScreen + 16, 120, 64), "持仓"));
            _tabs.Add(new ClickableComponent(new Rectangle(xPositionOnScreen + 424, yPositionOnScreen + 16, 120, 64), "账户"));

        }

        public override void receiveLeftClick(int x, int y, bool playSound = true)
        {
            base.receiveLeftClick(x, y, playSound);

            foreach (var tab in _tabs)
            {
                if (tab.containsPoint(x, y))
                {
                    _currentTab = tab.name;
                    Game1.playSound("smallSelect");
                    return;
                }
            }

            if (_currentTab == "持仓")
            {
                for (int i = 0; i < _closeButtons.Count; i++)
                {
                    if (_closeButtons[i].containsPoint(x, y) && i < _market.OpenPositions.Count)
                    {
                        var positionToClose = _market.OpenPositions[i];
                        var command = new TradeCommand { Action = "CLOSE", PositionId = positionToClose.PositionId };
                        string commandJson = JsonConvert.SerializeObject(command);
                        _market.ExecuteTradeCommand(commandJson);
                        Game1.playSound("smallSelect");
                        return;
                    }
                }
            }
        }

        public override void draw(SpriteBatch b)
        {
            // Draw the menu box
            Game1.drawDialogueBox(this.xPositionOnScreen, this.yPositionOnScreen, this.width, this.height, false, true);

            // Draw web interface info
            string webInfo = "高级图表和交易API: http://localhost:8080";
            Utility.drawTextWithShadow(b, webInfo, Game1.smallFont, new Vector2(this.xPositionOnScreen + this.width - Game1.smallFont.MeasureString(webInfo).X - 20, this.yPositionOnScreen + this.height - 50), Game1.textColor);

            // Draw Tabs
            foreach (var tab in _tabs)
            {
                IClickableMenu.drawTextureBox(b, Game1.mouseCursors, new Rectangle(432, 439, 9, 9), tab.bounds.X, tab.bounds.Y, tab.bounds.Width, tab.bounds.Height, (tab.name == _currentTab) ? Color.White : Color.Gray, 4f, false);
                Utility.drawTextWithShadow(b, tab.name, Game1.smallFont, new Vector2(tab.bounds.Center.X, tab.bounds.Center.Y) - Game1.smallFont.MeasureString(tab.name) / 2f, Game1.textColor);
            }

            // Draw content based on the current tab
            switch (_currentTab)
            {
                case "账户":
                    DrawAccountTab(b);
                    break;
                case "交易":
                    DrawTradeTab(b);
                    break;
                case "持仓":
                    DrawPositionsTab(b);
                    break;
                // ... other cases for other tabs ...
                default:
                    DrawDefaultTab(b);
                    break;
            }

            // Draw the base and mouse
            base.draw(b);
            drawMouse(b);
        }

        private void DrawTradeTab(SpriteBatch b)
        {
            // Title
            string title = "交易";
            Vector2 titleSize = Game1.dialogueFont.MeasureString(title);
            float titleX = this.xPositionOnScreen + (this.width - titleSize.X) / 2;
            float titleY = this.yPositionOnScreen + IClickableMenu.spaceToClearTopBorder + IClickableMenu.borderWidth;
            b.DrawString(Game1.dialogueFont, title, new Vector2(titleX, titleY), Game1.textColor);

            // Info
            float contentX = xPositionOnScreen + 50;
            float contentY = titleY + titleSize.Y + 50;
            b.DrawString(Game1.smallFont, $"当前价格: {_market.CurrentPrice:F2}g", new Vector2(contentX, contentY), Game1.textColor);
            b.DrawString(Game1.smallFont, $"可用保证金: {_market.FreeMargin:F2}g", new Vector2(contentX, contentY + 40), Game1.textColor);

        }

        private void DrawPositionsTab(SpriteBatch b)
        {
            // Title
            string title = "持仓";
            Vector2 titleSize = Game1.dialogueFont.MeasureString(title);
            float titleX = this.xPositionOnScreen + (this.width - titleSize.X) / 2;
            float titleY = this.yPositionOnScreen + IClickableMenu.spaceToClearTopBorder + IClickableMenu.borderWidth;
            b.DrawString(Game1.dialogueFont, title, new Vector2(titleX, titleY), Game1.textColor);

            // Header
            float contentX = xPositionOnScreen + 50;
            float contentY = titleY + titleSize.Y + 20;
            b.DrawString(Game1.smallFont, "多/空", new Vector2(contentX, contentY), Game1.textColor);
            b.DrawString(Game1.smallFont, "数量", new Vector2(contentX + 100, contentY), Game1.textColor);
            b.DrawString(Game1.smallFont, "开仓价", new Vector2(contentX + 200, contentY), Game1.textColor);
            b.DrawString(Game1.smallFont, "当前价", new Vector2(contentX + 300, contentY), Game1.textColor);
            b.DrawString(Game1.smallFont, "浮动盈亏", new Vector2(contentX + 400, contentY), Game1.textColor);

            _closeButtons.Clear();
            // Positions
            for (int i = 0; i < _market.OpenPositions.Count; i++)
            {
                var position = _market.OpenPositions[i];
                float positionY = contentY + 40 + i * 40;
                double pnl = (position.IsLong ? 1 : -1) * (_market.CurrentPrice - position.EntryPrice) * position.Contracts;

                b.DrawString(Game1.smallFont, position.IsLong ? "多" : "空", new Vector2(contentX, positionY), position.IsLong ? Color.Green : Color.Red);
                b.DrawString(Game1.smallFont, position.Contracts.ToString(), new Vector2(contentX + 100, positionY), Game1.textColor);
                b.DrawString(Game1.smallFont, position.EntryPrice.ToString("F2"), new Vector2(contentX + 200, positionY), Game1.textColor);
                b.DrawString(Game1.smallFont, _market.CurrentPrice.ToString("F2"), new Vector2(contentX + 300, positionY), Game1.textColor);
                b.DrawString(Game1.smallFont, pnl.ToString("F2"), new Vector2(contentX + 400, positionY), pnl >= 0 ? Color.Green : Color.Red);

                var closeButton = new ClickableTextureComponent(new Rectangle(xPositionOnScreen + 600, (int)positionY - 10, 120, 40), Game1.mouseCursors, new Rectangle(46, 440, 21, 21), 2f);
                _closeButtons.Add(closeButton);

                IClickableMenu.drawTextureBox(b, Game1.mouseCursors, new Rectangle(432, 439, 9, 9), closeButton.bounds.X, closeButton.bounds.Y, closeButton.bounds.Width, closeButton.bounds.Height, Color.White, 4f, false);
                Utility.drawTextWithShadow(b, "平仓", Game1.smallFont, new Vector2(closeButton.bounds.Center.X, closeButton.bounds.Center.Y + 8) - Game1.smallFont.MeasureString("平仓") / 2f, Game1.textColor);
            }
        }

        private void DrawAccountTab(SpriteBatch b)
        {
            // Title
            string title = "账户";
            Vector2 titleSize = Game1.dialogueFont.MeasureString(title);
            float titleX = this.xPositionOnScreen + (this.width - titleSize.X) / 2;
            float titleY = this.yPositionOnScreen + IClickableMenu.spaceToClearTopBorder + IClickableMenu.borderWidth;
            b.DrawString(Game1.dialogueFont, title, new Vector2(titleX, titleY), Game1.textColor);

            // Balances
            float contentX = xPositionOnScreen + 50;
            float contentY = titleY + titleSize.Y + 50;
            b.DrawString(Game1.smallFont, $"玩家钱包: {Game1.player.Money}g", new Vector2(contentX, contentY), Game1.textColor);
            b.DrawString(Game1.smallFont, $"期货账户: {_market.TradingAccountBalance:F2}g", new Vector2(contentX, contentY + 40), Game1.textColor);
            b.DrawString(Game1.smallFont, $"账户净值: {_market.AccountEquity:F2}g", new Vector2(contentX, contentY + 80), Game1.textColor);
            b.DrawString(Game1.smallFont, $"已用保证金: {_market.UsedMargin:F2}g", new Vector2(contentX, contentY + 120), Game1.textColor);
            b.DrawString(Game1.smallFont, $"可用保证金: {_market.FreeMargin:F2}g", new Vector2(contentX, contentY + 160), Game1.textColor);
            double marginLevel = _market.UsedMargin > 0 ? (_market.AccountEquity / _market.UsedMargin) * 100 : 0;
            b.DrawString(Game1.smallFont, $"保证金水平: {marginLevel:F2}%", new Vector2(contentX, contentY + 200), Game1.textColor);

        }

        private void DrawDefaultTab(SpriteBatch b)
        {
             // Prepare the text to display
            string title = "星露资本";
            string contract = $"合约: {_market.ContractName}";
            string price = $"当前价格: {_market.CurrentPrice:F2}g";
            string statusText = "";
            Color statusColor = Game1.textColor;

            switch (_market.CurrentStatus)
            {
                case FuturesMarket.MarketStatus.Waiting:
                    statusText = "状态: 等待开盘 (9:00 AM)";
                    statusColor = Color.Orange;
                    break;
                case FuturesMarket.MarketStatus.Open:
                    statusText = "状态: 交易中 (9:00 AM - 5:00 PM)";
                    statusColor = Color.Green;
                    break;
                case FuturesMarket.MarketStatus.Closed:
                    statusText = "状态: 今日已收盘";
                    statusColor = Color.Red;
                    break;
            }

            // Measure text for centering
            Vector2 titleSize = Game1.dialogueFont.MeasureString(title);
            Vector2 contractSize = Game1.smallFont.MeasureString(contract);
            Vector2 priceSize = Game1.smallFont.MeasureString(price);

            // Calculate positions
            float titleX = this.xPositionOnScreen + (this.width - titleSize.X) / 2;
            float titleY = this.yPositionOnScreen + IClickableMenu.spaceToClearTopBorder + IClickableMenu.borderWidth;

            float contractX = this.xPositionOnScreen + 50;
            float contractY = titleY + titleSize.Y + 50;

            float priceX = contractX;
            float priceY = contractY + contractSize.Y + 20;

            // Draw the text
            b.DrawString(Game1.dialogueFont, title, new Vector2(titleX, titleY), Game1.textColor);
            b.DrawString(Game1.smallFont, contract, new Vector2(contractX, contractY), Game1.textColor);
            b.DrawString(Game1.smallFont, price, new Vector2(priceX, priceY), Game1.textColor);

            // --- 绘制市场状态文本 ---
            Vector2 statusSize = Game1.smallFont.MeasureString(statusText);
            float statusX = contractX;
            float statusY = priceY + priceSize.Y + 20;
            b.DrawString(Game1.smallFont, statusText, new Vector2(statusX, statusY), statusColor);

            // Display daily news if available
            if (!string.IsNullOrEmpty(_market.DailyNews))
            {
                string newsText = $"今日新闻: {_market.DailyNews}";
                Vector2 newsSize = Game1.smallFont.MeasureString(newsText);
                float newsX = contractX;
                float newsY = statusY + statusSize.Y + 20; // 在状态文本下方显示
                b.DrawString(Game1.smallFont, newsText, new Vector2(newsX, newsY), Color.Red);
            }
        }
    }
}