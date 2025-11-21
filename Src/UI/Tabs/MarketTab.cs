// ============================================================================
// 星露资本 (Stardew Capital)
// 模块：交易终端界面
// 作者：Stardew Capital Team
// 用途：实现市场标签页，提供行情查看和交易功能
// ============================================================================

using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewCapital.Services;
using StardewValley;
using StardewValley.Menus;
using StardewModdingAPI;

namespace StardewCapital.UI.Tabs
{
    /// <summary>
    /// 市场标签页
    /// 
    /// 功能：
    /// 1. 显示当前合约价格
    /// 2. 提供买入/卖出操作
    /// 3. 提供杠杆倍数选择
    /// 4. 提供Web终端入口
    /// </summary>
    public class MarketTab : BaseTradingTab
    {
        private readonly MarketManager _marketManager;
        private readonly BrokerageService _brokerageService;
        
        // UI组件
        private ClickableComponent _buyButton = null!;
        private ClickableComponent _sellButton = null!;
        private ClickableComponent _webButton = null!;
        private List<ClickableComponent> _leverageButtons = new();
        private int _selectedLeverage = 1;

        // 状态反馈
        public string StatusMessage { get; private set; } = "";

        /// <summary>
        /// 构造函数
        /// </summary>
        public MarketTab(
            IMonitor monitor, 
            int x, int y, int width, int height,
            MarketManager marketManager, 
            BrokerageService brokerageService) 
            : base(monitor, x, y, width, height)
        {
            _marketManager = marketManager;
            _brokerageService = brokerageService;

            InitializeComponents();
        }

        /// <summary>
        /// 初始化UI组件
        /// </summary>
        private void InitializeComponents()
        {
            int centerX = XPositionOnScreen + Width / 2;
            int centerY = YPositionOnScreen + Height / 2;

            _buyButton = new ClickableComponent(new Rectangle(centerX - 160, centerY + 50, 150, 64), "Buy");
            _sellButton = new ClickableComponent(new Rectangle(centerX + 10, centerY + 50, 150, 64), "Sell");
            
            _leverageButtons.Clear();
            _leverageButtons.Add(new ClickableComponent(new Rectangle(centerX - 120, centerY - 20, 60, 40), "1x") { myID = 1 });
            _leverageButtons.Add(new ClickableComponent(new Rectangle(centerX - 40, centerY - 20, 60, 40), "5x") { myID = 5 });
            _leverageButtons.Add(new ClickableComponent(new Rectangle(centerX + 40, centerY - 20, 60, 40), "10x") { myID = 10 });

            _webButton = new ClickableComponent(new Rectangle(centerX - 100, centerY + 150, 200, 48), "Web Terminal");
        }

        /// <summary>
        /// 绘制市场标签页
        /// </summary>
        public override void Draw(SpriteBatch b)
        {
            int centerX = XPositionOnScreen + Width / 2;
            int centerY = YPositionOnScreen + Height / 2;

            // 1. 价格显示
            var instruments = _marketManager.GetInstruments();
            if (instruments.Count > 0)
            {
                var inst = instruments[0];
                string symbolText = inst.Symbol;
                string priceText = $"{inst.CurrentPrice:F2} g";

                // 合约代码
                Utility.drawTextWithShadow(b, symbolText, Game1.dialogueFont,
                    new Vector2(centerX - Game1.dialogueFont.MeasureString(symbolText).X / 2, centerY - 120),
                    Game1.textColor);
                
                // 价格（大字体）
                Vector2 priceSize = Game1.dialogueFont.MeasureString(priceText);
                Utility.drawTextWithShadow(b, priceText, Game1.dialogueFont,
                    new Vector2(centerX - priceSize.X / 2, centerY - 80),
                    Color.DarkGreen);
            }

            // 2. 杠杆选择器
            Utility.drawTextWithShadow(b, "Leverage:", Game1.smallFont, new Vector2(centerX - 200, centerY - 15), Game1.textColor);
            foreach (var btn in _leverageButtons)
            {
                bool isSelected = _selectedLeverage == btn.myID;
                DrawButton(b, btn, isSelected ? Color.Gold : Color.LightGray);
            }

            // 3. 操作按钮
            DrawButton(b, _buyButton, Color.LightGreen);
            DrawButton(b, _sellButton, Color.LightCoral);
            DrawButton(b, _webButton, Color.LightBlue);
        }

        /// <summary>
        /// 处理点击事件
        /// </summary>
        public override bool ReceiveLeftClick(int x, int y)
        {
            bool handled = false;

            if (_buyButton.containsPoint(x, y))
            {
                ExecuteTrade(1);
                handled = true;
            }
            else if (_sellButton.containsPoint(x, y))
            {
                ExecuteTrade(-1);
                handled = true;
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
                catch { StatusMessage = "Error opening browser"; }
                handled = true;
            }

            foreach (var btn in _leverageButtons)
            {
                if (btn.containsPoint(x, y))
                {
                    _selectedLeverage = btn.myID;
                    Game1.playSound("smallSelect");
                    handled = true;
                }
            }

            return handled;
        }

        /// <summary>
        /// 执行交易
        /// </summary>
        /// <param name="direction">交易方向：1=买入，-1=卖出</param>
        private void ExecuteTrade(int direction)
        {
            Game1.playSound("coin");
            var instruments = _marketManager.GetInstruments();
            if (instruments.Count > 0)
            {
                string symbol = instruments[0].Symbol;
                _brokerageService.ExecuteOrder(symbol, direction, _selectedLeverage);
                StatusMessage = $"{(direction > 0 ? "Buy" : "Sell")} Order Sent ({symbol} x{_selectedLeverage})";
            }
        }
    }
}
