// ============================================================================
// 星露谷资本 (Stardew Capital)
// 模块：盘中新闻价格冲击服务 (Core Library)
// 作者：Stardew Capital Team
// 用途：处理突发新闻对价格的即时冲击
// ============================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using StardewCapital.Core.Common.Logging;
using StardewCapital.Core.Futures.Domain.Instruments;
using StardewCapital.Core.Futures.Domain.Market;

namespace StardewCapital.Core.Futures.Services
{
    /// <summary>
    /// 盘中新闻价格冲击服务 (Core Library Version)
    /// 负责在突发新闻触发时对价格施加即时冲击
    /// </summary>
    public class IntradayNewsImpactService
    {
        private readonly ILogger? _logger;

        public IntradayNewsImpactService(ILogger? logger = null)
        {
            _logger = logger;
        }

        /// <summary>
        /// 应用新闻冲击到期货合约
        /// </summary>
        /// <param name="news">突发新闻事件</param>
        /// <param name="futures">受影响的期货合约</param>
        /// <param name="impactSensitivity">价格冲击敏感度系数(默认0.01,即1%)</param>
        public void ApplyNewsShock(NewsEvent news, CommodityFutures futures, double impactSensitivity = 0.01)
        {
            if (news == null || futures == null)
                return;

            // 检查新闻是否影响此商品
            bool isAffected = news.Scope.IsGlobal ||
                             news.Scope.AffectedItems.Contains(futures.CommodityName);

            if (!isAffected)
                return;

            // 计算冲击幅度
            // 公式: 冲击% = (需求影响 - 供给影响) × 敏感度
            // 逻辑: 需求增加或供给减少 → 价格上涨
            double netImpact = news.Impact.DemandImpact - news.Impact.SupplyImpact;
            double shockPercentage = netImpact *impactSensitivity;

            // 记录冲击前价格
            double oldPrice = futures.CurrentPrice;

            // 应用价格冲击
            double shockMultiplier = 1.0 + shockPercentage;
            futures.CurrentPrice *= shockMultiplier;

            // 确保价格为正
            if (futures.CurrentPrice < 0.01)
                futures.CurrentPrice = 0.01;

            // 计算实际冲击金额
            double actualShock = futures.CurrentPrice - oldPrice;

            // 记录日志
            _logger?.Log(
                $"[IntradayNewsShock] {futures.Symbol}: " +
                $"{oldPrice:F2}g → {futures.CurrentPrice:F2}g " +
                $"({actualShock:+0.00;-0.00}g, {shockPercentage * 100:+0.0;-0.0}%) | " +
                $"News: \"{news.Title}\" (D:{news.Impact.DemandImpact:+0;-0;0} S:{news.Impact.SupplyImpact:+0;-0;0})",
                LogLevel.Info
            );
        }

        /// <summary>
        /// 批量应用新闻冲击到所有受影响的期货合约
        /// </summary>
        /// <param name="news">突发新闻事件</param>
        /// <param name="allFutures">所有期货合约列表</param>
        /// <param name="impactSensitivity">价格冲击敏感度系数</param>
        public void ApplyNewsShockToAll(NewsEvent news, List<CommodityFutures> allFutures, double impactSensitivity = 0.01)
        {
            if (news == null || allFutures == null)
                return;

            int affectedCount = 0;

            foreach (var futures in allFutures)
            {
                // 检查是否受影响
                bool isAffected = news.Scope.IsGlobal ||
                                 news.Scope.AffectedItems.Contains(futures.CommodityName);

                if (isAffected)
                {
                    ApplyNewsShock(news, futures, impactSensitivity);
                    affectedCount++;
                }
            }

            if (affectedCount > 0)
            {
                _logger?.Log(
                    $"[IntradayNewsShock] Applied breaking news \"{news.Title}\" to {affectedCount} contract(s)",
                    LogLevel.Warn
                );
            }
        }
    }
}
