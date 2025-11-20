# Services 模块说明

## 概述

Services 模块是 HedgeHarvest 的"大脑"，负责协调各个组件、驱动市场运转、处理业务逻辑。

## 设计理念

- **服务导向架构**：每个服务专注单一职责
- **依赖注入**：服务间通过构造函数注入依赖
- **事件驱动**：响应游戏事件触发业务逻辑

---

## 核心服务

### 🕐 StardewTimeProvider.cs
**职责**：Core层与游戏层的时间桥梁

**功能**：
- 从 `Game1` 获取游戏时间
- 将 Stardew 时间格式（600-2600）转换为标准格式
- 检测游戏暂停状态

**时间转换**：
```
Stardew格式  →  分钟数
600 (6:00)      360分钟
1350 (13:50)    810分钟
2600 (次日2:00) 1560分钟
```

---

### 📈 MarketManager.cs
**职责**：市场总管，协调所有市场服务

**核心职责**：
1. 管理所有可交易金融产品
2. 维护每日目标价格
3. 协调价格引擎更新
4. 处理新一天的市场初始化

**工作流程**：
```
每日开盘
  ↓
计算新的目标价格 (GBM)
  ↓
每帧更新
  ↓
驱动价格引擎 (BrownianBridge)
  ↓
价格实时变化
```

**更新机制**：
- 节流更新：每60个tick更新一次（约1秒）
- 暂停检测：游戏暂停或市场关闭时停止更新
- 目标跟踪：确保价格向目标价收敛

---

### 💹 PriceEngine.cs
**职责**：价格驱动引擎

**价格模型**：
- **模型2（GBM）**：计算每日目标价（日级别）
- **模型4（布朗桥）**：计算tick价格（秒级别）

**配置参数**：
```csharp
BASE_VOLATILITY = 0.02    // 2% 日波动率
INTRA_VOLATILITY = 0.005  // 0.5% 日内噪声
```

**调用流程**：
```
Services/PriceEngine
    ↓ 调用
Core/Math/{GBM, BrownianBridge}
    ↓ 调用
Core/Math/StatisticsUtils
```

---

### 💼 BrokerageService.cs
**职责**：经纪服务，处理玩家交易

**核心功能**：
1. **资金管理**：
   - `Deposit()`：从游戏钱包转入交易账户
   - `Withdraw()`：从交易账户提取到游戏钱包
   
2. **订单执行**：
   - `ExecuteOrder()`：开仓/平仓/加仓
   - 保证金检查
   - 仓位合并逻辑

3. **账户查询**：
   - 提供只读访问 `Account` 属性

**设计模式**：Facade模式
- 封装 `TradingAccount` 和 `MarketManager` 的复杂交互
- 简化UI层的调用

---

### 📦 DeliveryService.cs
**职责**：期货实物交割处理

**交割流程**：

**多头仓位（Long）** - 接收物品：
```
1. 检查合约到期日
2. 优先放入交易所箱子
3. 箱子满了放在床边
4. 显示交割通知
```

**空头仓位（Short）** - 交付物品：
```
1. 检查合约到期日
2. 优先从交易所箱子扣除
3. 不足从玩家背包扣除
4. 还不足扣除金币（罚金）
5. 显示交割通知
```

**关键设计**：
- 与 `ExchangeService` 协作查找箱子
- 使用 `ItemRegistry` 创建/删除物品
- 使用 `HUDMessage` 显示通知

---

### 🏦 ExchangeService.cs
**职责**：交易所箱子管理

**核心功能**：
1. **标记箱子**：`ToggleExchangeStatus()`
   - 使用 `modData` 存储标记
   - 用金色作为视觉提示
   
2. **查找箱子**：`FindAllExchangeBoxes()`
   - 遍历所有游戏位置
   - 返回所有标记的箱子

**存储键**：`"HedgeHarvest.Exchange"`

---

### 💾 PersistenceService.cs
**职责**：存档管理

**保存内容**：
- 账户现金余额
- 所有开仓仓位

**存档时机**：
- `SaveData()`：游戏存档时调用
- `LoadData()`：游戏读档时调用

**存档键**：`"HedgeHarvest-SaveData"`

---

### 🌐 WebServer.cs
**职责**：HTTP服务器（可选功能）

**功能**：
- 提供Web界面查看市场数据
- 响应 `/api/ticker` 获取价格
- 服务静态HTML文件

**端口**：`http://localhost:5000`

**API接口**：
```json
GET /api/ticker
返回: [
  {
    "symbol": "PARSNIP-SPR-28",
    "price": 37.5,
    "name": "Parsnip"
  }
]
```

---

## 服务依赖图

```
MarketManager
  ├── 依赖 MixedTimeClock
  ├── 依赖 PriceEngine
  └── 管理 List<IInstrument>

BrokerageService
  ├── 依赖 MarketManager
  └── 管理 TradingAccount

DeliveryService
  ├── 依赖 BrokerageService
  ├── 依赖 MarketManager
  └── 依赖 ExchangeService

PersistenceService
  └── 依赖 BrokerageService

WebServer
  └── 依赖 MarketManager
```

---

## 初始化顺序

ModEntry 中的初始化顺序很重要：

```csharp
// 1. 时间系统（Core层）
timeProvider → clock → priceEngine

// 2. 市场管理（Services层）
marketManager

// 3. 交易服务（Services层）
brokerageService

// 4. 辅助服务（Services层）
persistenceService
exchangeService
deliveryService

// 5. Web服务（可选）
webServer
```

---

## 使用示例

### 执行交易

```csharp
// 通过BrokerageService执行
brokerageService.ExecuteOrder(
    symbol: "PARSNIP-SPR-28",
    quantity: 10,     // 正数=做多
    leverage: 10      // 10倍杠杆
);
```

### 处理交割

```csharp
// 每日结束时自动调用
deliveryService.ProcessDailyDelivery();
// 会检查所有到期合约并执行实物交割
```

### 存档数据

```csharp
// 保存
persistenceService.SaveData();

// 读取
persistenceService.LoadData();
```

---

## 事件处理流程

```
游戏事件              →  Services响应
─────────────────────────────────────
SaveLoaded           →  LoadData()
DayStarted           →  OnNewDay()
UpdateTicked         →  Update(ticks)
DayEnding            →  ProcessDelivery()
Saving               →  SaveData()
```

---

## 扩展建议

### 未来可添加的服务

1. **NewsService** - 新闻事件生成器
   - 生成随机新闻影响基本面价值
   - 模拟供需冲击

2. **ClearingHouse** - 清算所
   - 每日盯市结算
   - 追加保证金通知
   - 强制平仓机制

3. **ImpactService** - 市场冲击模型
   - 计算大额订单对价格的影响
   - 模拟流动性深度

4. **AITraderService** - AI交易员
   - 模拟Smart Money行为
   - 模拟FOMO散户
   - 增加市场深度

---

## 最佳实践

1. **服务间通信**：通过依赖注入，避免静态引用
2. **错误处理**：使用 `IMonitor.Log()` 记录关键操作
3. **性能优化**：使用节流机制避免过度更新
4. **状态管理**：服务应该是无状态或状态最小化
