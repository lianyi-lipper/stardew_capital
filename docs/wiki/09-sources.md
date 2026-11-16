# 09 · 来源、术语与简化边界

[返回目录](README.md) · [上一页：扩展与决策](08-extension-and-decisions.md)

外部资料核对日期：2026-11-16。链接内容可能继续变化；实施时应再次核对版本与许可证。

## 1. 资料如何使用

本 Wiki 是针对《星露资本》的原创设计建议，不是任何交易所的规则复制品，也不是对现成研究框架的性能保证。

资料支持一般机制和架构思路；NPC 数量、唤醒频率、保证金处置窗口、结算采样、风险标记候选算法、UI 和里程碑均是本项目建议，不能称为“文献已经证明正确”。

本次没有复制第三方实现代码，没有安装新依赖，也没有运行这些研究框架。

## R1 · ABIDES：离散事件与交易代理

David Byrd、Maria Hybinette、Tucker Hybinette Balch，2019，*ABIDES: Towards High-Fidelity Market Simulation for AI Research*。

- [作者论文摘要与版本](https://arxiv.org/abs/1904.12066)
- [项目仓库](https://github.com/abides-sim/abides)

参考的是代理与交易所通过事件交互、时间和通信顺序明确建模的思路。ABIDES 的研究目标、规模和精细延迟模型并不意味着游戏需要同样的复杂度，也不证明本项目 32 个代理的行为或性能已经合格。

对应页面：03、05。

## R2 · Bristol Stock Exchange：小型订单簿市场

Dave Cliff，*Bristol Stock Exchange / BSE*。

- [作者仓库和 README](https://github.com/davecliff/BristolStockExchange)
- [布里斯托大学的论文记录](https://research-information.bris.ac.uk/en/publications/bse-a-minimal-simulation-of-a-limit-order-book-stock-exchange/)

参考的是较小规模的限价订单簿、交易者策略和可重复实验，不直接继承其产品范围、账户简化或策略收益假设。阅读与借鉴架构不等于可以无条件复制代码；实际复用时核对许可证和署名要求。

对应页面：02、03、07。

## R3 · CME：逐日盯市与现金结转

- [Mark-to-Market](https://www.cmegroup.com/education/courses/introduction-to-futures/mark-to-market)
- [Money Calculations for Futures and Options](https://www.cmegroup.com/education/articles-and-reports/money-calculations-for-futures-and-options)

用于核对期货按结算价格处理盈亏、变化保证金与现金结转的基本概念。04 中的示例是为游戏说明自行构造，未使用真实合约行情。

对应页面：02、04。

## R4 · CME：初始与维持保证金

- [Margin: Know What's Needed](https://www.cmegroup.com/education/courses/introduction-to-futures/margin-know-what-is-needed)
- [Fact Sheet: Margins](https://www.cmegroup.com/media-room/fact-sheet--margins.html)

用于区分初始资金要求、维持要求和风险变化。游戏中的固定比例、强平批量和宽限期限不代表实际券商或清算所的通用做法。

对应页面：04。

## R5 · CME：日结、最终结算与交割

- [Daily Settlements](https://www.cmegroup.com/market-data/daily-settlements.html)
- [Final Settlements](https://www.cmegroup.com/market-data/final-settlements.html)
- [Cash Settlement vs. Physical Delivery](https://www.cmegroup.com/education/articles-and-reports/agriculture-cash-settlement-vs-physical-delivery)
- [101 Overview: Delivery](https://www.cmegroup.com/articles/brochures-and-handbooks/101-overview-delivery.html)

用于核对日终结算和到期结算不同、现金结算与实物交割不同，以及交割需要实际资源与履约安排。不同产品规则不同；本 Wiki 的指数窗口、回退策略、无数据暂停和游戏库存流程是自己的设计。

对应页面：01、02、04。

## R6 · SMAPI / Stardew Valley Modder Guide

- [事件 API 文档](https://stardewvalleywiki.com/Modding:Modder_Guide/APIs/Events)
- [Context 等工具接口](https://stardewvalleywiki.com/Modding:Modder_Guide/APIs/Utilities)

使用社区维护的 Modder Guide 核对游戏生命周期、时间相关事件、存档时点和上下文判断。不能仅凭收到更新事件，就认定游戏时钟正在流逝；也不能把“世界已加载”理解成“玩家不处于暂停菜单”。

具体非暂停交易界面、时间类 Mod、目标游戏版本和多人主机同步仍需实现与实测。当前文档没有宣称这些兼容问题已经解决。

对应页面：05、06、07。

## R7 · Investor.gov：股票与基金净值

- [Stocks — FAQs](https://www.investor.gov/introduction-investing/investing-basics/investment-products/stocks)
- [Net Asset Value](https://www.investor.gov/introduction-investing/investing-basics/glossary/net-asset-value)

用于核对股票对应企业所有权、基金资产净值与每份净值的基本区别。08 中具体的商店经营、发行、基金申赎和银行机制都是未来游戏架构建议，不是投资建议或现实产品实现说明。

对应页面：08。

## 2. 本仓库的阅读基线

基线提交：[`05b52d6e8b5ae3b1bccb9b47450519e6e389b2c5`](https://github.com/lianyi-lipper/stardew_capital/tree/05b52d6e8b5ae3b1bccb9b47450519e6e389b2c5)。

主要对照文件：

- `期货参考文档.md`、`模型公式更新.md`、`参考资料.md`：原目标、生成式定价和历史讨论。
- `StardewCapital.Core/Futures/FuturesMarket.cs`：当前市场编排和价格更新入口。
- `Futures/Pricing/FuturesPriceGenerator.cs`、`FundamentalEngine.cs`：旧价格/新闻链路。
- `Common/Market/Base/BaseOrderBook.cs`、`Engines/PriceTimePriorityEngine.cs`：订单簿和撮合基础。
- `Futures/Market/FuturesOrderBook.cs`、`Models/FuturesContract.cs`：保证金检查、合约和状态。
- `Time/SimulationClock.cs`：当前模拟时间实现。
- `StardewCapital.Tests/Program.cs`、`SimulationExporter.cs`、`price_viewer.html`：现有实验与展示入口。

静态阅读只能确认代码形态和值得验证的路径，不等于证明系统可编译、可运行、金融行为正确或性能达标。新文档不会回写旧文件的“已完成”标记。

## 3. 术语速查

| 术语 | 本 Wiki 中的含义 |
|---|---|
| 智能体 / Agent | 有账户约束、观察和策略状态的决策对象，不特指大模型 |
| 订单簿 / LOB | 尚未成交的买卖订单按规则排列的集合 |
| Maker / Taker | 先挂在簿中提供流动性的一方 / 主动与之成交的一方 |
| Tick size | 允许的最小报价增量，不是渲染帧或模拟时间步 |
| Market step | 引擎的逻辑时间步，与价格 tick 不是一回事 |
| Spread | 同时可用的最优卖价与最优买价之差 |
| Slippage | 相对于明确参考报价的执行差异；报告必须说明参考时点 |
| VWAP | 按成交数量加权的平均成交价格 |
| IOC | 立即尝试成交，未成交余量取消 |
| Reduce-only | 只减少既有持仓，不允许穿过零开反向新仓 |
| Basis | 本 Wiki 统一定义为期货价减现货指数；其他资料可能采用相反符号 |
| Risk mark | 风险估值参考，不是可保证成交价格 |
| Variation settlement | 已有期货敞口按结算规则结转现金盈亏 |
| Open interest | 尚未平仓的合约对数/单边总手数，不是成交量 |
| Physical delivery | 正式货物交付与货款转移，不是画面上发一封邮件就算完成 |
| Replay | 用同一种子、配置、命令和规则重建事件与账户结果 |
| Legacy | 保留的旧价格路径模式，不与新市场混合写价 |

## 4. 明确不宣称的事项

本方案不宣称小规模规则代理天然等同真实市场；不保证模型产生所有泡沫、崩盘与统计特征；不保证 32 个代理在任何机器都流畅；不保证任何玩法策略盈利；不把现金结算原型称为完整实物期现套利；不把当前静态阅读称为代码审计或测试通过。

它提供的是一套可分阶段实现、可验证、能解释失败原因的参考设计。未来每一步应以测试、账本、真实交互和性能报告补充证据。
