# Core 模块说明

## 概述

Core 模块是 HedgeHarvest 的数学和时间引擎核心，**完全不依赖** Stardew Valley 的代码，可以独立进行单元测试。

## 设计原则

- **纯函数式**：所有数学计算都是无副作用的纯函数
- **依赖反转**：通过 `IGameTimeProvider` 接口与游戏层解耦
- **可测试性**：可以使用 Mock 实现进行完整的单元测试

## 子模块

### 📐 Math - 数学模型

包含金融价格模拟的核心算法：

#### `StatisticsUtils.cs`
- **功能**：生成正态分布随机数
- **算法**：Box-Muller 变换
- **用途**：为价格模型提供随机波动

#### `GBM.cs` - 几何布朗运动
- **功能**：计算每日目标价格
- **公式**：`ln(S_{t+1}) = ln(S_t) + alpha * (ln(Target) - ln(S_t)) + sigma_t * epsilon`
- **特性**：
  - 均值回归：价格向目标价收敛
  - 动态波动率：接近到期日时波动降低
  - 到期强制收敛：确保价格在到期日等于目标价

#### `BrownianBridge.cs` - 布朗桥模型
- **功能**：计算日内tick价格
- **公式**：`P_{tau+1} = P_tau + Gravity + Noise`
- **特性**：
  - 引力机制：将价格拉向日终目标
  - 波动率微笑：开盘波动大，收盘波动小
  - 时间依赖：波动随时间递减

---

### ⏰ Time - 时间系统

统一管理游戏时间和真实时间的转换：

#### `IGameTimeProvider.cs`
- **设计目的**：依赖反转，使 Core 层不依赖 Game1
- **提供接口**：
  - `CurrentTimeOfDay`：游戏时间（600-2600格式）
  - `TimeRatio`：归一化时间进度（0.0-1.0）
  - `IsPaused`：游戏暂停状态
  - `TotalMinutesToday`：今日总分钟数

#### `MixedTimeClock.cs`
- **核心功能**：市场模拟的"心脏"
- **职责**：
  - 提供归一化时间进度
  - 判断市场开放/关闭
  - 检测游戏暂停状态

#### `TimeConstants.cs`
- **定义常量**：
  - 开盘时间：600（早上6:00）
  - 收盘时间：2600（次日凌晨2:00）
  - 每日总分钟数：1200分钟
  - 更新间隔：0.7秒

---

## 使用示例

### 计算每日目标价格

```csharp
using StardewCapital.Core.Math;

// 当前价格35，目标基本面价值40，还有10天到期，波动率2%
double currentPrice = 35.0;
double fundamentalValue = 40.0;
int daysRemaining = 10;
double baseVolatility = 0.02;

double nextDayPrice = GBM.CalculateNextPrice(
    currentPrice, 
    fundamentalValue, 
    daysRemaining, 
    baseVolatility
);
```

### 计算日内Tick价格

```csharp
using StardewCapital.Core.Math;

// 当前价格36，目标37，时间进度50%，日内波动率0.5%
double currentPrice = 36.0;
double targetPrice = 37.0;
double timeRatio = 0.5;  // 已过去一半天
double intraVolatility = 0.005;

double nextTickPrice = BrownianBridge.CalculateNextTickPrice(
    currentPrice, 
    targetPrice, 
    timeRatio, 
    intraVolatility
);
```

### 使用时间系统

```csharp
using StardewCapital.Core.Time;

// 创建时间时钟
var timeProvider = new StardewTimeProvider(config);
var clock = new MixedTimeClock(timeProvider, config);

// 检查市场状态
if (clock.IsMarketOpen() && !clock.IsPaused())
{
    double progress = clock.GetDayProgress();  // 0.0 - 1.0
    double timeRemaining = clock.GetTimeRemaining();
}
```

---

## 依赖关系

```
Core (无外部依赖)
  ├── Math
  │   ├── StatisticsUtils ← 被 GBM, BrownianBridge 使用
  │   ├── GBM
  │   └── BrownianBridge
  └── Time
      ├── IGameTimeProvider ← 接口定义
      ├── MixedTimeClock ← 依赖 IGameTimeProvider
      └── TimeConstants
```

---

## 未来扩展

- **BlackScholes.cs**：期权定价公式
- **JumpDiffusion.cs**：跳跃扩散模型（模拟突发新闻）
- **GARCH.cs**：条件异方差模型（更真实的波动率）

---

## 测试建议

由于 Core 层完全独立，建议为每个数学模型编写单元测试：

```csharp
[Test]
public void GBM_ShouldConvergeToTarget()
{
    double currentPrice = 100;
    double targetPrice = 110;
    int daysRemaining = 1;
    
    double result = GBM.CalculateNextPrice(
        currentPrice, targetPrice, daysRemaining, 0.02
    );
    
    // 最后一天应该强制收敛
    Assert.AreEqual(targetPrice, result);
}
```
