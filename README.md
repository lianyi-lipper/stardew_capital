# stardew_capital
推荐的“全能型”文件夹结构
StardewFinanceMod/
├── ModEntry.cs
├── Core/                       // [核心底层] 
│   ├── TimeSystem.cs           // 混合时间/睡觉补算逻辑
│   ├── MathHelpers.cs          // 正态分布、布朗运动等通用数学库
│   └── EventBus.cs             // 消息总线
├── Domain/                     // [业务领域 - 资产定义]
│   ├── Base/
│   │   ├── IInstrument.cs      // 接口：所有金融产品的基类 (Price, Symbol)
│   │   └── IExpirable.cs       // 接口：会过期的产品用这个 (期货/期权)
│   ├── Derivatives/            // 衍生品
│   │   ├── FuturesContract.cs  // 期货
│   │   └── OptionContract.cs   // 期权 (Call/Put)
│   ├── Equities/               // 权益类
│   │   ├── Stock.cs            // 股票 (Joja, Pierce's)
│   │   └── ETF.cs              // 基金 (指数)
│   └── Banking/                // 银行业务
│       ├── BankAccount.cs      // 存款
│       └── Loan.cs             // 贷款
├── PricingEngines/             // [定价工厂 - 不同的东西怎么算价格]
│   ├── IPricingModel.cs        // 定价接口
│   ├── CommodityModel.cs       // 商品期货模型 (Cost of Carry)
│   ├── EquityModel.cs          // 股票模型 (GBM + 股息)
│   ├── BlackScholes.cs         // 期权模型 (BS公式)
│   └── Microstructure/         // 通用的日内微观结构 (布朗桥/冲击)
│       └── OrderFlowEngine.cs
├── Market/                     // [交易场所 - 所有的买卖在这里发生]
│   ├── Exchange.cs             // 交易所总控 (分发订单)
│   ├── OrderBook.cs            // L2订单簿 (通用！不管是股票还是期货都用这个)
│   └── Order.cs                // 订单类
├── Portfolio/                  // [玩家账户]
│   ├── PositionManager.cs      // 持仓管理 (List<IInstrument>)
│   ├── MarginSystem.cs         // 保证金/风控 (爆仓计算)
│   └── CreditSystem.cs         // 信用分/银行借贷
└── UI/                         // [界面]
    ├── Apps/                   // 手机/电脑里的不同App
    │   ├── FuturesApp.cs
    │   ├── StockApp.cs
    │   └── BankApp.cs
    └── Components/
