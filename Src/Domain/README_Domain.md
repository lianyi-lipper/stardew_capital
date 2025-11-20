# Domain 模块说明

## 概述

Domain 模块定义了金融交易系统的核心业务实体，包括金融产品、账户管理和市场数据结构。

## 设计理念

- **面向接口编程**：所有金融产品实现 `IInstrument` 接口
- **领域驱动设计**：实体包含业务逻辑而非贫血模型
- **可扩展性**：易于添加新的金融产品类型

---

## 子模块

### 💼 Instruments - 金融产品

#### `IInstrument.cs` - 可交易资产接口
**核心属性**：
- `Symbol`：产品代码（如"PARSNIP-SPR-28"）
- `Name`：显示名称
- `UnderlyingItemId`：标的物品ID
- `CurrentPrice`：当前市场价格
- `MarginRatio`：保证金比例

**设计优势**：
```csharp
// 所有交易系统基于接口工作，易于扩展
public void ExecuteTrade(IInstrument asset, int quantity)
{
    // 无需关心具体是期货还是股票
    decimal margin = asset.CurrentPrice * quantity * asset.MarginRatio;
}
```

#### `CommodityFutures.cs` - 商品期货合约
**特有属性**：
- `DeliverySeason`：交割季节
- `DeliveryDay`：交割日期
- `MarginRatio`：默认10%（支持10倍杠杆）

**合约代码格式**：`商品名-季节-日期`
- 示例：`PARSNIP-SPR-28` → 防风草春季28号到期

**实物交割**：
- 多头：到期收到真实物品
- 空头：到期需交付真实物品

---

### 💰 Account - 账户管理

#### `Position.cs` - 交易仓位
**核心概念**：
```csharp
// Quantity 的符号决定多空方向
Quantity > 0  // 多头（看涨）：价格上涨盈利
Quantity < 0  // 空头（看跌）：价格下跌盈利
```

**关键方法**：
- `GetMarketValue()`：计算当前市值
- `GetUnrealizedPnL()`：计算浮动盈亏
- `GetMarginUsed()`：计算占用保证金

**盈亏计算公式**：
```
PnL = (CurrentPrice - AverageCost) × Quantity
```
注意：对于多头和空头，公式统一，因为空头的 Quantity 为负。

#### `TradingAccount.cs` - 交易账户
**账户结构**：
```
总资产（Equity） = 现金 + 未实现盈亏
已占用保证金 = Σ(|仓位市值| / 杠杆倍数)
可用保证金 = 总资产 - 已占用保证金
```

**核心方法**：
- `Deposit()` / `Withdraw()`：资金存取
- `GetTotalEquity()`：计算总资产
- `GetUsedMargin()`：计算已占用保证金
- `GetFreeMargin()`：计算可用保证金

**风险管理**：
- 提取资金时检查可用保证金
- 开仓时检查保证金充足性
- 支持 Mark-to-Market 估值

---

### 📊 Market - 市场数据

#### `TickData.cs` - K线数据点
**标准K线要素**：
```
Open  (开盘价)：第一个价格
High  (最高价)：最高价格
Low   (最低价)：最低价格
Close (收盘价)：最后一个价格
Volume(成交量)：交易量
```

**使用场景**：
- 价格历史记录
- K线图绘制
- 技术分析指标计算

---

## 使用示例

### 创建期货合约

```csharp
using StardewCapital.Domain.Instruments;

var parsnipFutures = new CommodityFutures(
    underlyingItemId: "24",      // 防风草的物品ID
    name: "Parsnip",
    season: "Spring",
    deliveryDay: 28,
    initialPrice: 35.0
);

// 合约代码：PARSNIP-SPR-28
Console.WriteLine(parsnipFutures.Symbol);
```

### 管理交易仓位

```csharp
using StardewCapital.Domain.Account;

// 开多头仓位：买入10单位，成本35，10倍杠杆
var longPosition = new Position(
    symbol: "PARSNIP-SPR-28",
    quantity: 10,          // 正数 = 多头
    averageCost: 35.0m,
    leverage: 10
);

// 计算浮动盈亏（假设当前价格37）
decimal currentPrice = 37.0m;
decimal pnl = longPosition.GetUnrealizedPnL(currentPrice);
// PnL = (37 - 35) × 10 = 20金币

// 开空头仓位：卖出5单位
var shortPosition = new Position(
    symbol: "PARSNIP-SPR-28",
    quantity: -5,          // 负数 = 空头
    averageCost: 40.0m,
    leverage: 10
);

// 空头盈亏（假设价格跌到38）
pnl = shortPosition.GetUnrealizedPnL(38.0m);
// PnL = (38 - 40) × (-5) = 10金币（价格下跌空头盈利）
```

### 账户管理

```csharp
using StardewCapital.Domain.Account;

var account = new TradingAccount();
account.Deposit(10000);  // 存入10000金币

// 添加仓位
account.Positions.Add(longPosition);

// 获取当前价格
var prices = new Dictionary<string, decimal>
{
    ["PARSNIP-SPR-28"] = 37.0m
};

// 计算账户状态
decimal equity = account.GetTotalEquity(prices);
decimal usedMargin = account.GetUsedMargin(prices);
decimal freeMargin = account.GetFreeMargin(prices);

Console.WriteLine($"总资产: {equity}");
Console.WriteLine($"已用保证金: {usedMargin}");
Console.WriteLine($"可用保证金: {freeMargin}");
```

---

## 依赖关系

```
Domain (仅依赖标准库)
  ├── Instruments
  │   ├── IInstrument
  │   └── CommodityFutures : IInstrument
  ├── Account
  │   ├── Position
  │   └── TradingAccount (持有多个 Position)
  └── Market
      └── TickData
```

---

## 未来扩展

### 计划中的金融产品

```csharp
// 股票
public class Stock : IInstrument
{
    public int SharesOutstanding { get; set; }
    public double DividendYield { get; set; }
}

// 期权
public class Option : IInstrument
{
    public double StrikePrice { get; set; }
    public DateTime ExpirationDate { get; set; }
    public OptionType Type { get; set; }  // Call/Put
}

// 债券
public class Bond : IInstrument
{
    public double CouponRate { get; set; }
    public DateTime MaturityDate { get; set; }
}
```

---

## 设计模式总结

- **接口隔离**：`IInstrument` 只定义必需属性
- **单一职责**：`Position` 只管仓位，`TradingAccount` 只管账户
- **开闭原则**：易于扩展新产品，无需修改现有代码
