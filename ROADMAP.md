# Stardew Capital 开发路线图与架构评审

## 一、 文件结构评审 (Architecture Review)

你的文件结构设计非常出色，遵循了 **Clean Architecture (整洁架构)** 的原则，将核心逻辑与游戏引擎解耦。这对于一个复杂的金融 Mod 来说是至关重要的。

### ✅ 优点
1.  **Core 层纯净性**：明确指出 `Core` 不依赖 `StardewValley`，这使得数学模型（GBM、布朗桥）可以独立于游戏进行单元测试，极大地降低了调试难度。
2.  **Domain 层扩展性**：通过 `IInstrument` 接口预留了未来扩展股票、期权的空间。
3.  **Services 层职责分明**：`PriceEngine` 负责生成价格，`ClearingHouse` 负责结算，`ImpactService` 负责市场冲击，职责划分清晰。

### 💡 改进建议 (面向未来功能)

为了更好地支持你提到的“股票、期货、期权、银行”等未来功能，建议对结构做微调：

#### 1. 增加 `Banking` 模块 (Domain 层)
银行系统通常涉及借贷、利率和信用评分，与交易市场（Market）略有不同。
```text
Src/Domain/
├── Banking/                 # [NEW] 银行系统
│   ├── IBankAccount.cs      # 存款/贷款账户接口
│   ├── LoanContract.cs      # 贷款合约
│   └── InterestRateModel.cs # 利率模型
```

#### 2. 细化 `Instruments` (Domain 层)
股票和期权有其特殊性，建议提前规划：
```text
Src/Domain/Instruments/
├── Derivatives/             # [NEW] 衍生品
│   ├── FuturesContract.cs
│   └── OptionContract.cs    # 期权 (Call/Put, Strike, Expiry)
├── Equities/                # [NEW] 权益类
│   └── Stock.cs             # 股票 (Dividends, CorporateActions)
└── IInstrument.cs

建议分为 **6 个阶段**，每个阶段都有可运行的产出，避免“写了半年代码运行不起来”的情况。

- [ ] **SMAPI 集成**：在 `ModEntry.cs` 中挂载 `GameLoop.UpdateTicked`，每帧调用 MarketManager。
- [ ] **验证**：在 SMAPI 控制台打印日志：`[Market] Parsnip Futures: 40g -> 40.5g`。

- [ ] **未来扩展**：此时再开始考虑股票和期权。

## 三、 关键技术难点提醒

1.  **UI 线程与逻辑线程**：
    星露谷是单线程游戏。你的 `RealTimeMenu` 必须非常小心，不要在 `draw` 方法里做复杂的数学计算，否则游戏会掉帧。所有的计算应在 `UpdateTicked` 中完成，UI 只负责渲染当前状态。

2.  **浮点数精度**：
    金融计算尽量使用 `decimal` 而不是 `double`，但在游戏开发中为了性能和数学库兼容，常使用 `double` 或 `float`。建议：**核心账本（钱、持仓）用 `decimal`，价格模拟和图形渲染用 `double`**。

3.  **时间跳变处理**：
    玩家睡觉、过场动画会导致游戏时间瞬间跳变。你的 `MixedTimeClock` 需要能优雅地处理 `TimeSkip` 事件，避免 K 线图出现断层或异常直线。
