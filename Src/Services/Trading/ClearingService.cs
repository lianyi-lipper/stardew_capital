using System;
using System.Linq;
using StardewCapital.Domain.Account;
using StardewCapital.Services.Market;
using StardewModdingAPI;
using StardewValley;

namespace StardewCapital.Services.Trading
{
    /// <summary>
    /// 结算服务
    /// 负责每日结算、保证金检查和强平逻辑。
    /// 
    /// 核心职责：
    /// - 每日结算（Mark-to-Market）：虽然不直接划转现金，但会更新账户状态
    /// - 保证金检查：计算维持保证金，发出追保通知
    /// - 强平机制：当保证金水平过低时强制平仓
    /// 
    /// 风险控制参数：
    /// - 警告线：保证金水平 < 80%
    /// - 强平线：保证金水平 < 50%
    /// </summary>
    public class ClearingService
    {
        private readonly IMonitor _monitor;
        private readonly BrokerageService _brokerageService;
        private readonly MarketManager _marketManager;

        // 风险参数
        private const decimal MARGIN_CALL_LEVEL = 0.80m; // 80%
        private const decimal LIQUIDATION_LEVEL = 0.50m; // 50%

        public ClearingService(IMonitor monitor, BrokerageService brokerageService, MarketManager marketManager)
        {
            _monitor = monitor;
            _brokerageService = brokerageService;
            _marketManager = marketManager;
        }

        /// <summary>
        /// 执行每日结算流程
        /// 在每日结束或新一天开始时调用
        /// </summary>
        public void DailySettlement()
        {
            _monitor.Log("[Clearing] Starting daily settlement...", LogLevel.Info);

            // 1. 获取当前市场价格
            var prices = _brokerageService.GetCurrentPrices();

            // 2. 计算账户权益和保证金状态
            decimal equity = _brokerageService.GetEquity(prices);
            decimal usedMargin = _brokerageService.Account.UsedMargin;

            // 如果没有持仓，无需检查
            if (usedMargin == 0)
            {
                _monitor.Log("[Clearing] No open positions. Settlement complete.", LogLevel.Info);
                return;
            }

            // 3. 计算保证金水平 (Margin Level = Equity / Used Margin)
            decimal marginLevel = equity / usedMargin;

            _monitor.Log(
                $"[Clearing] Equity: {equity:F2}g, UsedMargin: {usedMargin:F2}g, Level: {marginLevel:P2}",
                LogLevel.Info
            );

            // 4. 风险检查
            if (marginLevel < LIQUIDATION_LEVEL)
            {
                // 触发强平
                LiquidatePositions(equity, usedMargin);
            }
            else if (marginLevel < MARGIN_CALL_LEVEL)
            {
                // 发出追保通知
                IssueMarginCall(marginLevel);
            }
        }

        /// <summary>
        /// 发出追加保证金通知 (Margin Call)
        /// </summary>
        private void IssueMarginCall(decimal marginLevel)
        {
            string message = $"警告：保证金水平低 ({marginLevel:P0})！请补充资金或减仓。";
            
            // 1. HUD 消息
            Game1.addHUDMessage(new HUDMessage(message, 3)); // 3 = Warning/Error

            // 2. 邮件通知（可选，为了更强的提醒）
            // TODO: 发送邮件逻辑
            
            _monitor.Log($"[Risk] MARGIN CALL! Level: {marginLevel:P2}", LogLevel.Warn);
        }

        /// <summary>
        /// 强制平仓逻辑
        /// 当账户风险过高时，强制平仓以避免穿仓
        /// </summary>
        private void LiquidatePositions(decimal equity, decimal usedMargin)
        {
            _monitor.Log("[Risk] LIQUIDATION TRIGGERED!", LogLevel.Alert);

            // 简单策略：平掉所有仓位（未来可以优化为只平掉亏损最大的）
            // 为了避免修改集合时的迭代问题，先复制列表
            var positions = _brokerageService.Account.Positions.ToList();

            foreach (var pos in positions)
            {
                _monitor.Log($"[Risk] Liquidating {pos.Symbol} x{pos.Quantity}...", LogLevel.Warn);
                
                // 强制平仓：不检查保证金，直接反向交易
                // 使用 BrokerageService 的强平辅助方法
                _brokerageService.LiquidatePosition(pos.Symbol);
            }

            // 通知玩家
            Game1.addHUDMessage(new HUDMessage("严重警告：账户触发强平！所有仓位已关闭。", 3));
            
            // 发送邮件通知
            // TODO: 实现邮件通知
        }
    }
}
