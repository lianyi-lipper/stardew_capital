// ============================================================================
// 星露资本 (Stardew Capital)
// 模块：交易终端界面
// 作者：Stardew Capital Team
// 用途：交易终端主菜单，管理标签页切换
// ============================================================================

using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewCapital.Services.Market;
using StardewCapital.Services.Trading;
using StardewCapital.Services.News;
using StardewCapital.UI.Tabs;
using StardewValley;
using StardewValley.Menus;
using StardewModdingAPI;

namespace StardewCapital.UI
{
    /// <summary>
    /// 交易终端主菜单
    /// 提供四个标签页的交易界面：市场、账户、持仓、新闻。
    /// 
    /// 功能特性：
    /// - Market（市场）：查看实时价格、执行买卖、选择杠杆
    /// - Account（账户）：查看余额、存取资金
    /// - Positions（持仓）：查看所有仓位和盈亏
    /// - News（新闻）：查看今日新闻和当前生效的新闻事件
    /// 
    /// 快捷键：F10 打开/关闭菜单
    /// 
    /// 设计模式：
    /// - 标签页切换：通过Tab枚举管理当前显示的标签
    /// - 实时刷新：每次绘制时获取最新数据
    /// - 响应式UI：基于视口大小计算位置
    /// </summary>
    public class TradingMenu : IClickableMenu
    {
        /// <summary>标签页枚举：市场、账户、持仓、新闻</summary>
        private enum Tab { Market, Account, Positions, News }
        
        private readonly MarketManager _marketManager;
        private readonly BrokerageService _brokerageService;
        private readonly ScenarioManager _scenarioManager;
        private readonly ImpactService _impactService;
        private readonly IMonitor _monitor;

        private Tab _currentTab = Tab.Market;
        private List<ClickableComponent> _tabButtons = new();

        // 标签页实例
        private MarketTab _marketTab = null!;
        private AccountTab _accountTab = null!;
        private PositionsTab _positionsTab = null!;
        private NewsTab _newsTab = null!;

        private string _statusMessage = "";

        /// <summary>
        /// 创建交易终端菜单
        /// </summary>
        /// <param name="marketManager">市场管理器（提供价格数据）</param>
        /// <param name="brokerageService">经纪服务（处理交易和账户）</param>
        /// <param name="scenarioManager">剧本管理器（提供市场剧本信息）</param>
        /// <param name="impactService">冲击服务（提供价格冲击信息）</param>
        /// <param name="monitor">日志监视器</param>
        public TradingMenu(
            MarketManager marketManager, 
            BrokerageService brokerageService, 
            ScenarioManager scenarioManager,
            ImpactService impactService,
            IMonitor monitor)
            : base(Game1.viewport.Width / 2 - 400, Game1.viewport.Height / 2 - 300, 800, 600, false)
        {
            _marketManager = marketManager;
            _brokerageService = brokerageService;
            _scenarioManager = scenarioManager;
            _impactService = impactService;
            _monitor = monitor;

            InitializeComponents();
        }

        /// <summary>
        /// 初始化所有UI组件（标签按钮、各标签页实例）
        /// 计算所有组件的位置和大小
        /// </summary>
        private void InitializeComponents()
        {
            int x = xPositionOnScreen;
            int y = yPositionOnScreen;

            // --- 标签页按钮 ---
            _tabButtons.Clear();
            int tabWidth = 120; // 缩小以容纳4个标签
            _tabButtons.Add(new ClickableComponent(new Rectangle(x + 50, y + 100, tabWidth, 48), "Market") { myID = 0 });
            _tabButtons.Add(new ClickableComponent(new Rectangle(x + 50 + tabWidth + 10, y + 100, tabWidth, 48), "Account") { myID = 1 });
            _tabButtons.Add(new ClickableComponent(new Rectangle(x + 50 + (tabWidth + 10) * 2, y + 100, tabWidth, 48), "Positions") { myID = 2 });
            _tabButtons.Add(new ClickableComponent(new Rectangle(x + 50 + (tabWidth + 10) * 3, y + 100, tabWidth, 48), "News") { myID = 3 });

            // --- 初始化各标签页 ---
            _marketTab = new MarketTab(_monitor, x, y, width, height, _marketManager, _brokerageService);
            _accountTab = new AccountTab(_monitor, x, y, width, height, _marketManager, _brokerageService);
            _positionsTab = new PositionsTab(_monitor, x, y, width, height, _marketManager, _brokerageService);
            _newsTab = new NewsTab(_monitor, x, y, width, height, _marketManager, _scenarioManager, _impactService);
        }

        /// <summary>
        /// 绘制菜单的主方法
        /// 每帧调用，绘制背景、标题、标签页和当前标签的内容
        /// </summary>
        /// <param name="b">SpriteBatch用于绘图</param>
        public override void draw(SpriteBatch b)
        {
            // 1. 绘制背景
            b.Draw(Game1.fadeToBlackRect, Game1.graphics.GraphicsDevice.Viewport.Bounds, Color.Black * 0.75f);
            Game1.drawDialogueBox(xPositionOnScreen, yPositionOnScreen, width, height, false, true);

            // 2. 绘制标题
            string title = "StardewCapital Terminal";
            Utility.drawTextWithShadow(b, title, Game1.dialogueFont, 
                new Vector2(xPositionOnScreen + width / 2 - Game1.dialogueFont.MeasureString(title).X / 2, yPositionOnScreen + 35), 
                Game1.textColor);

            // 3. 绘制标签页按钮
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

            // 4. 根据当前标签绘制内容
            switch (_currentTab)
            {
                case Tab.Market:
                    _marketTab.Draw(b);
                    // 更新状态消息
                    if (!string.IsNullOrEmpty(_marketTab.StatusMessage))
                        _statusMessage = _marketTab.StatusMessage;
                    break;
                case Tab.Account:
                    _accountTab.Draw(b);
                    // 更新状态消息
                    if (!string.IsNullOrEmpty(_accountTab.StatusMessage))
                        _statusMessage = _accountTab.StatusMessage;
                    break;
                case Tab.Positions:
                    _positionsTab.Draw(b);
                    break;
                case Tab.News:
                    _newsTab.Draw(b);
                    break;
            }

            // 5. 绘制状态栏
            if (!string.IsNullOrEmpty(_statusMessage))
            {
                Utility.drawTextWithShadow(b, _statusMessage, Game1.smallFont,
                    new Vector2(xPositionOnScreen + width / 2 - Game1.smallFont.MeasureString(_statusMessage).X / 2, yPositionOnScreen + height - 60),
                    Color.DarkSlateGray);
            }

            drawMouse(b);
        }

        /// <summary>
        /// 处理鼠标左键点击事件
        /// 分发到标签切换或当前标签的具体处理方法
        /// </summary>
        public override void receiveLeftClick(int x, int y, bool playSound = true)
        {
            base.receiveLeftClick(x, y, playSound);

            // 1. 标签页切换
            foreach (var tab in _tabButtons)
            {
                if (tab.containsPoint(x, y))
                {
                    _currentTab = (Tab)tab.myID;
                    Game1.playSound("smallSelect");
                    _statusMessage = ""; // 切换标签时清除状态消息
                    return;
                }
            }

            // 2. 分发给当前标签页
            bool handled = false;
            switch (_currentTab)
            {
                case Tab.Market:
                    handled = _marketTab.ReceiveLeftClick(x, y);
                    break;
                case Tab.Account:
                    handled = _accountTab.ReceiveLeftClick(x, y);
                    break;
                case Tab.Positions:
                    handled = _positionsTab.ReceiveLeftClick(x, y);
                    break;
                case Tab.News:
                    handled = _newsTab.ReceiveLeftClick(x, y);
                    break;
            }
        }

        /// <summary>
        /// 处理鼠标滚轮事件
        /// </summary>
        public override void receiveScrollWheelAction(int direction)
        {
            base.receiveScrollWheelAction(direction);

            switch (_currentTab)
            {
                case Tab.Market:
                    _marketTab.ReceiveScrollWheelAction(direction);
                    break;
                case Tab.Account:
                    _accountTab.ReceiveScrollWheelAction(direction);
                    break;
                case Tab.Positions:
                    _positionsTab.ReceiveScrollWheelAction(direction);
                    break;
                case Tab.News:
                    _newsTab.ReceiveScrollWheelAction(direction);
                    break;
            }
        }
    }
}
