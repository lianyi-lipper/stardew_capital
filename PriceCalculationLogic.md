# 价格计算逻辑文档 (Price Calculation Logic)

本文档详细梳理了 StardewCapital 项目中商品期货价格的计算逻辑，包括“影子价格”（Shadow Price）的预生成机制和“实时价格”（Real-time Price）的动态更新流程。

## 1. 核心概念概览

价格系统主要由两部分组成：
1.  **影子价格 (Shadow Price)**：基于数学模型（GBM + Brownian Bridge）预先计算的“理论价格轨迹”。它代表了在没有任何玩家干预下的市场自然走势。
2.  **实时价格 (Real-time Price)**：在影子价格的基础上，叠加了玩家交易行为产生的“市场冲击”（Market Impact）后的最终交易价格。

---

## 2. 影子价格生成 (Shadow Price Generation)

影子价格在季节开始时（或初始化时）一次性生成，涵盖整个季度的每一天、每一刻（10分钟粒度）。

### 2.1 生成流程
代码位置：`Src/Services/Pricing/Generators/FuturesShadowGenerator.cs`

1.  **基本面价值计算 (Fundamental Value)**
    *   首先计算该商品在整个季度的基本面价值轨迹。这取决于季节性因素和预定的新闻事件。
    *   代码参考：`GenerateSeasonalData` 方法。

2.  **每日收盘价生成 (GBM模型)**
    *   使用 **几何布朗运动 (GBM)** 计算每一天的目标收盘价。
    *   **逻辑**：价格具有均值回归特性，随着交割日临近，价格会逐渐收敛到基本面价值。
    *   **公式**：$ \ln(S_{t+1}) = \ln(S_t) + \alpha (\ln(Target) - \ln(S_t)) + \sigma_t \epsilon $
    *   代码支持：`Src/Core/Math/GBM.cs` -> `CalculateNextPrice`

3.  **日内价格轨迹 (布朗桥模型)**
    *   确定了当天的开盘价和收盘价后，使用 **布朗桥 (Brownian Bridge)** 模型填充两者之间的日内走势（每10分钟一个点）。
    *   **特性**：确保开盘时波动大（不确定性高），收盘时波动小并精准收敛到目标价。
    *   代码支持：`Src/Core/Math/BrownianBridge.cs` -> `CalculateNextTickPrice`

4.  **状态存储**
    *   生成的所有价格点被存储在 `FuturesMarketState` 对象中，供运行时快速读取。
    *   代码支持：`Src/Domain/Market/MarketState/FuturesMarketState.cs`

---

## 3. 实时价格更新 (Real-time Price Update)

游戏运行时，每一帧（经过节流处理，约每秒一次）都会更新当前价格。

### 3.1 更新流程
代码位置：`Src/Services/Market/MarketPriceUpdater.cs`

1.  **读取影子价格**
    *   根据当前的游戏日期和时间进度（`timeRatio`），从 `MarketStateManager` 中读取预计算好的影子价格。
    *   `double shadowPrice = _marketManager.GetMarketStateManager().GetCurrentPrice(...);`
    *   此步骤确立了价格的基准。

2.  **叠加市场冲击 (Market Impact)**
    *   获取当前累积的市场冲击值 $I(t)$（由玩家的大额买卖单导致）。
    *   **公式**：$ P_{RealTime} = P_{Shadow} + I(t) $
    *   `instrument.CurrentPrice += impact;`
    *   这意味着玩家的交易会使价格暂时偏离理论轨道。

3.  **熔断检查 (Circuit Breaker)**
    *   在价格最终确定前，`CircuitBreakerService` 会检查价格是否偏离当日开盘价过多。
    *   如果触发熔断，价格将被锁定在熔断限价上。

---

## 4. 期货定价 (Futures Pricing)

在 StardewCapital 中，`instrument.CurrentPrice` 通常代表**现货价格 (Spot Price)**。期货价格 (Futures Price) 是基于现货价格通过**持有成本模型 (Cost of Carry)** 衍生出来的。

### 4.1 计算公式
代码位置：`Src/Services/Pricing/PriceEngine.cs` -> `CalculateFuturesPrice`

$$ F_t = S_t \times e^{(r + \phi - q) \times \tau} $$

*   $F_t$: 期货价格
*   $S_t$: 现货价格 (`CurrentPrice`)
*   $r$: 无风险利率 (Risk-free Rate)
*   $\phi$: 仓储成本 (Storage Cost)
*   $q$: **便利收益率 (Convenience Yield)**
*   $\tau$: 距离交割日的时间 (Time to Maturity)

### 4.2 便利收益率 (q)
代码位置：`Src/Services/Pricing/ConvenienceYieldService.cs`

便利收益率是该模型的核心动态参数，受以下因素影响：
*   **NPC生日**：如果今天是某NPC生日且其喜爱该商品，$q$ 会大幅上升（持有现货送礼价值高），导致期货贴水（Futures < Spot）。
*   **社区中心任务**：如果该商品是未完成Bundle的需求物，$q$ 会小幅上升。

---

## 5. 每日开盘逻辑 (Daily Cycle)

代码位置：`Src/Services/Market/DailyMarketOpener.cs`

每天早上（OnNewDay）：
1.  从 `MarketStateManager` 获取当日的目标收盘价。
2.  检查是否有隔夜的“跳空”（Gap），通常由前一日的熔断或重大新闻导致。
3.  如果有 Gap，将其应用到开盘价上。
4.  基于新的开盘现货价，重新计算期货价格初始值。
5.  重置订单簿深度。
