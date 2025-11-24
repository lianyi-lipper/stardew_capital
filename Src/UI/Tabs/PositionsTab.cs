// ============================================================================
// 星露资本 (Stardew Capital)
// 模块：交易终端界面
// 作者：Stardew Capital Team
// 用途：实现持仓标签页，显示当前持有的所有合约仓位
// ============================================================================

using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewCapital.Services.Market;
using StardewCapital.Services.Trading;
using StardewValley;
using StardewModdingAPI;

namespace StardewCapital.UI.Tabs
{
    /// <summary>
    /// 持仓标签页
    /// 
    /// 功能：
    /// 1. 列表显示所有未平仓合约
    /// 2. 显示持仓数量、平均成本、浮动盈亏
    /// </summary>
    public class PositionsTab : BaseTradingTab
    {
        private readonly MarketManager _marketManager;
        private readonly BrokerageService _brokerageService;

        /// <summary>
        /// 构造函数
        /// </summary>
        public PositionsTab(
            IMonitor monitor, 
            int x, int y, int width, int height,
            MarketManager marketManager, 
            BrokerageService brokerageService) 
            : base(monitor, x, y, width, height)
        {
            _marketManager = marketManager;
            _brokerageService = brokerageService;
        }

        /// <summary>
        /// 绘制持仓标签页
        /// </summary>
        public override void Draw(SpriteBatch b)
        {
            int leftX = XPositionOnScreen + 60;
            int topY = YPositionOnScreen + 180;

            // 1. 表头
            b.DrawString(Game1.smallFont, "Symbol", new Vector2(leftX, topY), Game1.textColor);
            b.DrawString(Game1.smallFont, "Qty", new Vector2(leftX + 200, topY), Game1.textColor);
            b.DrawString(Game1.smallFont, "Entry", new Vector2(leftX + 300, topY), Game1.textColor);
            b.DrawString(Game1.smallFont, "PnL", new Vector2(leftX + 450, topY), Game1.textColor);
            
            // 2. 分隔线
            b.Draw(Game1.staminaRect, new Rectangle(leftX, topY + 25, 600, 2), Color.DarkGray);

            // 3. 数据行
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

        /// <summary>
        /// 处理点击事件（持仓页暂无交互）
        /// </summary>
        public override bool ReceiveLeftClick(int x, int y)
        {
            return false;
        }

        /// <summary>
        /// 获取所有金融产品的当前价格
        /// </summary>
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
