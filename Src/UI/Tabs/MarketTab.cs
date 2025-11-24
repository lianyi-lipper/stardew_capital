// ============================================================================
// 星露谷资本 (Stardew Capital)
// 模块：交易终端界面
// 作者：Stardew Capital Team
// 用途：实现市场标签页，提供行情查看和交易功能
// ============================================================================

using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewCapital.Domain.Instruments;
using StardewCapital.Services.Market;
using StardewCapital.Services.Trading;
using StardewValley;
using StardewValley.Menus;
using StardewModdingAPI;

namespace StardewCapital.UI.Tabs
{
    /// <summary>
    /// 市场标签页
    /// 
    /// 功能：
    /// 1. 显示期货行情列表（名称、价格、涨跌幅）
    /// 2. 点击进入交易详情页
    /// 3. 提供买入/卖出操作
    /// 4. 提供杠杆倍数选择
    /// </summary>
    public class MarketTab : BaseTradingTab
    {
        /// <summary>
        /// 视图模式枚举
        /// </summary>
        private enum ViewMode
        {
            /// <summary>期货列表视图</summary>
            ListView,
            /// <summary>交易详情视图</summary>
            DetailView
        }

        private readonly MarketManager _marketManager;
        private readonly BrokerageService _brokerageService;
        
        // 视图状态
        private ViewMode _currentView = ViewMode.ListView;
        private int _selectedInstrumentIndex = 0;
        
        // UI组件（详情视图）
        private ClickableComponent _buyButton = null!;
        private ClickableComponent _sellButton = null!;
        private ClickableComponent _backButton = null!;
        private ClickableComponent _webButton = null!;
        private List<ClickableComponent> _leverageButtons = new();
        private int _selectedLeverage = 1;

        // UI组件（列表视图）
        private List<ClickableComponent> _listItemButtons = new();

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

            // 详情视图组件
            _buyButton = new ClickableComponent(new Rectangle(centerX - 160, centerY + 50, 150, 64), "Buy");
            _sellButton = new ClickableComponent(new Rectangle(centerX + 10, centerY + 50, 150, 64), "Sell");
            _backButton = new ClickableComponent(new Rectangle(XPositionOnScreen + 20, YPositionOnScreen + 20, 120, 48), "Back");
            
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
            if (_currentView == ViewMode.ListView)
                DrawListView(b);
            else
                DrawDetailView(b);
        }

        /// <summary>
        /// 绘制期货列表视图
        /// </summary>
        private void DrawListView(SpriteBatch b)
        {
            int centerX = XPositionOnScreen + Width / 2;
            int startY = YPositionOnScreen + 200;  // 从80改为120，往下移40像素

            // 标题
            string title = "期货行情";
            Utility.drawTextWithShadow(b, title, Game1.dialogueFont,
                new Vector2(centerX - Game1.dialogueFont.MeasureString(title).X / 2, YPositionOnScreen + 160),  // 从30改为70
                Color.Gold);

            // 重新构建列表项按钮（以防商品数量变化）
            _listItemButtons.Clear();
            var instruments = _marketManager.GetInstruments();

            // 表头
            int headerY = startY;
            Utility.drawTextWithShadow(b, "商品", Game1.smallFont, new Vector2(XPositionOnScreen + 50, headerY), Color.Gray);
            Utility.drawTextWithShadow(b, "价格", Game1.smallFont, new Vector2(XPositionOnScreen + 250, headerY), Color.Gray);
            Utility.drawTextWithShadow(b, "涨跌", Game1.smallFont, new Vector2(XPositionOnScreen + 400, headerY), Color.Gray);
            Utility.drawTextWithShadow(b, "涨跌幅", Game1.smallFont, new Vector2(XPositionOnScreen + 550, headerY), Color.Gray);

            startY += 40;

            // 遍历所有期货
            for (int i = 0; i < instruments.Count; i++)
            {
                var inst = instruments[i];
                if (inst is not CommodityFutures futures) continue;

                int rowY = startY + i * 60;
                
                // 创建可点击区域
                var itemButton = new ClickableComponent(
                    new Rectangle(XPositionOnScreen + 30, rowY - 5, Width - 60, 50),
                    $"Item_{i}"
                ) { myID = i };
                _listItemButtons.Add(itemButton);

                // 背景高亮（悬停效果）
                if (itemButton.containsPoint(Game1.getMouseX(), Game1.getMouseY()))
                {
                    b.Draw(Game1.staminaRect, itemButton.bounds, Color.White * 0.1f);
                }

                // 计算涨跌数据
                double price = futures.FuturesPrice;
                double openPrice = futures.OpenPrice;
                double change = price - openPrice;
                double changePercent = openPrice > 0 ? (change / openPrice) * 100 : 0;

                Color changeColor = change >= 0 ? Color.LightGreen : Color.LightCoral;

                // 商品名称
                Utility.drawTextWithShadow(b, futures.CommodityName, Game1.dialogueFont,
                    new Vector2(XPositionOnScreen + 50, rowY), Game1.textColor);

                // 价格
                string priceText = $"{price:F2}g";
                Utility.drawTextWithShadow(b, priceText, Game1.dialogueFont,
                    new Vector2(XPositionOnScreen + 250, rowY), Game1.textColor);

                // 涨跌额
                string changeText = change >= 0 ? $"+{change:F2}" : $"{change:F2}";
                Utility.drawTextWithShadow(b, changeText, Game1.dialogueFont,
                    new Vector2(XPositionOnScreen + 400, rowY), changeColor);

                // 涨跌幅
                string percentText = changePercent >= 0 ? $"+{changePercent:F2}%" : $"{changePercent:F2}%";
                Utility.drawTextWithShadow(b, percentText, Game1.dialogueFont,
                    new Vector2(XPositionOnScreen + 550, rowY), changeColor);

                // 箭头提示
                Utility.drawTextWithShadow(b, "→", Game1.smallFont,
                    new Vector2(XPositionOnScreen + Width - 80, rowY + 5), Color.Gray);
            }

            // Web终端按钮
            DrawButton(b, _webButton, Color.LightBlue);
        }

        /// <summary>
        /// 绘制交易详情视图
        /// </summary>
        private void DrawDetailView(SpriteBatch b)
        {
            int centerX = XPositionOnScreen + Width / 2;
            int centerY = YPositionOnScreen + Height / 2;

            var instruments = _marketManager.GetInstruments();
            if (_selectedInstrumentIndex >= instruments.Count) return;

            var inst = instruments[_selectedInstrumentIndex];
            if (inst is not CommodityFutures futures) return;

            // 返回按钮
            DrawButton(b, _backButton, Color.LightGray);

            // 标题
            string title = $"{futures.CommodityName} 期货";
            Utility.drawTextWithShadow(b, title, Game1.dialogueFont,
                new Vector2(centerX - Game1.dialogueFont.MeasureString(title).X / 2, YPositionOnScreen + 30),
                Color.Gold);

            // 价格信息
            double price = futures.FuturesPrice;
            double openPrice = futures.OpenPrice;
            double change = price - openPrice;
            double changePercent = openPrice > 0 ? (change / openPrice) * 100 : 0;
            Color changeColor = change >= 0 ? Color.LightGreen : Color.LightCoral;

            string symbolText = inst.Symbol;
            string priceText = $"{price:F2} g";
            string changeText = change >= 0 ? $"+{change:F2}" : $"{change:F2}";
            string percentText = changePercent >= 0 ? $"+{changePercent:F2}%" : $"{changePercent:F2}%";

            // 合约代码
            Utility.drawTextWithShadow(b, symbolText, Game1.smallFont,
                new Vector2(centerX - Game1.smallFont.MeasureString(symbolText).X / 2, centerY - 140),
                Color.Gray);
            
            // 价格（大字体）
            Vector2 priceSize = Game1.dialogueFont.MeasureString(priceText);
            Utility.drawTextWithShadow(b, priceText, Game1.dialogueFont,
                new Vector2(centerX - priceSize.X / 2, centerY - 110),
                Game1.textColor);

            // 涨跌信息
            string changeInfo = $"{changeText} ({percentText})";
            Vector2 changeSize = Game1.smallFont.MeasureString(changeInfo);
            Utility.drawTextWithShadow(b, changeInfo, Game1.smallFont,
                new Vector2(centerX - changeSize.X / 2, centerY - 70),
                changeColor);

            // 杠杆选择器
            Utility.drawTextWithShadow(b, "Leverage:", Game1.smallFont, new Vector2(centerX - 200, centerY - 15), Game1.textColor);
            foreach (var btn in _leverageButtons)
            {
                bool isSelected = _selectedLeverage == btn.myID;
                DrawButton(b, btn, isSelected ? Color.Gold : Color.LightGray);
            }

            // 操作按钮
            DrawButton(b, _buyButton, Color.LightGreen);
            DrawButton(b, _sellButton, Color.LightCoral);
        }

        /// <summary>
        /// 处理点击事件
        /// </summary>
        public override bool ReceiveLeftClick(int x, int y)
        {
            bool handled = false;

            if (_currentView == ViewMode.ListView)
            {
                // 列表视图点击
                foreach (var btn in _listItemButtons)
                {
                    if (btn.containsPoint(x, y))
                    {
                        _selectedInstrumentIndex = btn.myID;
                        _currentView = ViewMode.DetailView;
                        Game1.playSound("shwip");
                        handled = true;
                        break;
                    }
                }

                // Web终端按钮
                if (_webButton.containsPoint(x, y))
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
            }
            else
            {
                // 详情视图点击
                if (_backButton.containsPoint(x, y))
                {
                    _currentView = ViewMode.ListView;
                    Game1.playSound("shwip");
                    handled = true;
                }
                else if (_buyButton.containsPoint(x, y))
                {
                    ExecuteTrade(1);
                    handled = true;
                }
                else if (_sellButton.containsPoint(x, y))
                {
                    ExecuteTrade(-1);
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
            if (_selectedInstrumentIndex < instruments.Count)
            {
                string symbol = instruments[_selectedInstrumentIndex].Symbol;
                _brokerageService.ExecuteOrder(symbol, direction, _selectedLeverage);
                StatusMessage = $"{(direction > 0 ? "Buy" : "Sell")} Order Sent ({symbol} x{_selectedLeverage})";
            }
        }
    }
}
