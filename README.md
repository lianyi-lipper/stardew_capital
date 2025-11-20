# stardew_capital
这套结构的核心原则是：**将“纯数学/金融逻辑”与“星露谷游戏逻辑”彻底分离**。

这样设计的好处是：未来你想加“期权”或“银行”，只需要在 `Domain` 层加几个类，而不需要重写底层的数学引擎或 UI 框架。



### 项目根目录结构



Plaintext

```
StardewCapital/
├── Assets/                  # [资源] 贴图、数据JSON、音效
│   ├── Data/                # 初始配置 (e.g., 基础商品定义)
│   └── Sprites/             # K线图背景、UI图标
├── i18n/                    # [国际化] 支持中文/英文切换 (default.json, zh.json)
├── Manifest.json            # SMAPI 必须的清单文件
├── StardewCapital.csproj      # C# 项目文件
└── Src/                     # [源代码] 核心代码逻辑
    ├── Core/                # [核心层] 纯数学与底层引擎 (不依赖 Stardew 代码)
    ├── Domain/              # [领域层] 金融业务逻辑 (定义什么是期货、什么是订单簿)
    ├── Services/            # [服务层] 游戏内的动态系统 (新闻、做市商、时间管理)
    ├── UI/                  # [表现层] 所有的菜单、图表绘制
    ├── Data/                # [数据层] 存档/读档模型
    ├── Integration/         # [适配层] 与星露谷原版逻辑的交互 (Patch, Events)
    └── ModEntry.cs          # 程序入口
```

------



### `Src/` 源代码详细拆解



这是你开发的重头戏。请务必严格遵守分层。



#### 1. `Core/` (数学与物理引擎)



这里面的代码**不应该引用** `StardewValley` 的任何命名空间。它是纯 C# 逻辑，方便你单独做单元测试。

Plaintext

```
Src/Core/
├── Math/
│   ├── GBM.cs               # 几何布朗运动公式 (模型二)
│   ├── BrownianBridge.cs    # 增量递归布朗桥 (模型四)
│   ├── BlackScholes.cs      # (未来) 期权定价公式
│   └── StatisticsUtils.cs   # 正态分布随机数生成器
├── Time/
│   ├── MixedTimeClock.cs    # 混合时间心脏 (真实时间 vs 游戏时间转换)
│   └── TimeConstants.cs     # 定义什么是“一帧”，什么是“收盘时间”
└── Simulation/
    └── L2MatchingEngine.cs  # 纯粹的订单撮合算法 (无关游戏，只管数字)
```



#### 2. `Domain/` (金融业务实体)



定义你的“游戏对象”。这里开始引入业务规则，但尽量少涉及 UI。

Plaintext

```
Src/Domain/
├── Instruments/             # [金融资产定义]
│   ├── IInstrument.cs       # 接口：代码, 现价, 历史数据
│   ├── CommodityFutures.cs  # 具体类：防风草期货 (含交割日, 保证金率)
│   ├── Stock.cs             # (未来) 股票
│   └── Option.cs            # (未来) 期权
├── Market/
│   ├── OrderBook.cs         # L2 订单簿数据结构 (Bid/Ask List)
│   ├── Order.cs             # 订单实体 (价格, 数量, 来源类型)
│   └── TickData.cs          # 记录每一帧的价格/量 (用于画K线)
├── Participants/            # [市场参与者]
│   ├── ParticipantType.cs   # 枚举: Player, SmartMoney, FOMO
│   └── Portfolio.cs         # 玩家的持仓账户 (多单/空单/保证金)
└── Economy/
    └── MacroFactors.cs      # 宏观因子 (S_T, D_news 等参数集合)
```



#### 3. `Services/` (系统运转逻辑)



这是 Mod 的“大脑”，每一帧都在运行。

Plaintext

```
Src/Services/
├── MarketManager.cs         # [总管] 协调各个服务，处理 UpdateTicked
├── PriceEngine.cs           # [价格驱动] 调用 Core.Math 计算下一帧价格 (模型 1-4)
├── ImpactService.cs         # [冲击系统] 计算 I(t) 和 NPC 行为 (模型 5)
├── NewsService.cs           # [新闻] 生成随机事件 (海莉生日、虫灾)
├── ClearingHouse.cs         # [清算所] 每日结算、追保、强平逻辑
└── DeliveryService.cs       # [交割] 负责发邮件给道具、检查箱子
```



#### 4. `UI/` (交互界面)



这是最繁琐的部分，与星露谷的绘图函数深度绑定。

Plaintext

```
Src/UI/
├── Base/
│   └── RealTimeMenu.cs      # 基类：重写 update() 以支持不暂停游戏
├── Components/
│   ├── CandlestickChart.cs  # K线图绘制组件
│   ├── OrderBookView.cs     # 动态深度图绘制
│   └── TradePanel.cs        # 下单按钮、输入框
└── Windows/
    ├── TradingTerminal.cs   # 交易主界面
    └── PortfolioWindow.cs   # 持仓与账户概览
```



#### 5. `Integration/` (与原版游戏接驳)



Plaintext

```
Src/Integration/
├── EventListeners.cs        # 监听 DayEnding, DayStarted, ButtonPressed
├── InventoryScanner.cs      # 扫描玩家箱子寻找交割物
└── HarmonyPatches/          # (如果有) 需要修改原版代码的补丁
    └── ShopMenuPatch.cs     # 例如：修改皮埃尔商店显示期货价格
```



#### 6. `Data/` (存档系统)



Plaintext

```
Src/Data/
├── SaveModel.cs             # 总存档结构
├── PlayerPositionData.cs    # 玩家持仓存档
└── MarketHistoryData.cs     # 历史K线存档 (防止读档导致K线重画)
```

------



### 开发建议：接口优先原则



为了实现你说的“未来加上股票、期权”，你在写代码时要养成使用 **Interface (接口)** 的习惯。

**错误写法 (把路堵死了)：**

C#

```
// 你的交易函数只认期货
public void ExecuteTrade(FuturesContract contract, int quantity) { ... }
```

**正确写法 (面向未来)：**

C#

```
// 1. 定义一个通用接口
public interface ITradable {
    string Symbol { get; }      // 代码 (e.g., "PARSNIP-SPR-28")
    double CurrentPrice { get; }
    double MarginRatio { get; } // 保证金率 (股票是100%，期货是10%)
}

// 2. 期货实现它
public class FuturesContract : ITradable { ... }

// 3. 股票实现它 (未来)
public class Stock : ITradable { ... }

// 4. 交易函数只认接口
public void ExecuteTrade(ITradable asset, int quantity) { ... }
```



### 这里的坑在哪里？



1. **`Core` 和 `Services` 的依赖关系**：`Services` 引用 `Core`，但 `Core` 绝对不能引用 `Services`。保持数学层的纯净。
2. **时间管理**：你的 `Time` 模块非常关键。建议在 `Src/Core/Time` 里写一个 `TimeProvider` 单例，所有其他服务都问它要时间，而不是直接问 `Game1`。这样你可以轻松实现“时间加速”或“时间冻结”的调试。
