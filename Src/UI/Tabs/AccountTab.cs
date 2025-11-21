// ============================================================================
// 星露资本 (Stardew Capital)
// 模块：交易终端界面
// 作者：Stardew Capital Team
// 用途：实现账户标签页，提供资产概览和资金存取功能
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
    /// 账户标签页
    /// 
    /// 功能：
    /// 1. 显示账户资金状态（余额、权益、保证金）
    /// 2. 提供资金存入和取出功能
    /// </summary>
    public class AccountTab : BaseTradingTab
    {
        private readonly MarketManager _marketManager;
        private readonly BrokerageService _brokerageService;

        // UI组件
        private ClickableComponent _depositButton = null!;
        private ClickableComponent _withdrawButton = null!;

        // 状态反馈
        public string StatusMessage { get; private set; } = "";

        /// <summary>
        /// 构造函数
        /// </summary>
        public AccountTab(
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

            _depositButton = new ClickableComponent(new Rectangle(centerX - 160, centerY + 80, 150, 64), "Deposit");
            _withdrawButton = new ClickableComponent(new Rectangle(centerX + 10, centerY + 80, 150, 64), "Withdraw");
        }

        /// <summary>
        /// 绘制账户标签页
        /// </summary>
        public override void Draw(SpriteBatch b)
        {
            int leftX = XPositionOnScreen + 100;
            int topY = YPositionOnScreen + 200;

            var prices = GetCurrentPrices();
            var account = _brokerageService.Account;

            // 1. 绘制资金信息
            DrawStatRow(b, "Cash Balance:", $"{account.Cash:N0} g", leftX, topY);
            DrawStatRow(b, "Equity:", $"{account.GetTotalEquity(prices):N0} g", leftX, topY + 40);
            DrawStatRow(b, "Used Margin:", $"{account.GetUsedMargin(prices):N0} g", leftX, topY + 80);
            
            decimal freeMargin = account.GetFreeMargin(prices);
            DrawStatRow(b, "Free Margin:", $"{freeMargin:N0} g", leftX, topY + 120, freeMargin >= 0 ? Game1.textColor : Color.DarkRed);

            // 2. 绘制按钮
            DrawButton(b, _depositButton, Color.Orange);
            DrawButton(b, _withdrawButton, Color.Orange);
        }

        /// <summary>
        /// 处理点击事件
        /// </summary>
        public override bool ReceiveLeftClick(int x, int y)
        {
            if (_depositButton.containsPoint(x, y))
            {
                Game1.playSound("coin");
                _brokerageService.Deposit(1000);
                StatusMessage = "Deposited 1000g";
                return true;
            }
            else if (_withdrawButton.containsPoint(x, y))
            {
                Game1.playSound("coin");
                _brokerageService.Withdraw(1000);
                StatusMessage = "Withdrawn 1000g";
                return true;
            }

            return false;
        }

        /// <summary>
        /// 获取所有金融产品的当前价格
        /// </summary>
        /// <returns>价格字典（Symbol -> Price）</returns>
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
