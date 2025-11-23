# UI 模块说明

## 概述

UI 模块负责所有用户界面相关的功能，包括交易菜单、图表显示、交易所箱子标记等。

## 设计理念

- **响应式UI**：基于 Stardew Valley 的菜单系统
- **实时更新**：价格和账户信息自动刷新
- **用户友好**：清晰的视觉反馈和操作提示

---

## 组件列表

### 📊 TradingMenu.cs
**主交易界面**

**功能**：
- 显示当前市场价格
- 执行买入/卖出操作
- 选择杠杆倍数（1x, 5x, 10x）
- 查看账户余额和持仓
- 打开Web交易终端

**界面布局**：
```
┌─────────────────────────────────────────┐
│    StardewCapital Terminal                │
├──────┬──────┬───────────────────────────┤
│Market│Account│Positions     [标签页]    │
├──────┴──────┴───────────────────────────┤
│                                         │
│  Market Tab:                            │
│    PARSNIP-SPR-28                       │
│    37.50 g                              │
│                                         │
│    Leverage: [1x] [5x] [10x]            │
│                                         │
│    [Buy]  [Sell]                        │
│    [Web Terminal]                       │
│                                         │
└─────────────────────────────────────────┘
```

**三个标签页**：

1. **Market（市场）**：
   - 显示当前价格
   - 杠杆选择器
   - 买入/卖出按钮
   - Web终端快捷方式

2. **Account（账户）**：
   - 现金余额
   - 总资产（Equity）
   - 已用保证金
   - 可用保证金
   - 存款/取款按钮

3. **Positions（持仓）**：
   - 仓位列表
   - 合约代码、数量、成本价
   - 实时盈亏（绿色/红色）

**快捷键**：
- `F10`：打开/关闭交易菜单

**交易逻辑**：
```csharp
// 买入1单位（做多）
ExecuteOrder(symbol, quantity: 1, leverage);

// 卖出1单位（做空）
ExecuteOrder(symbol, quantity: -1, leverage);
```

---

### 📦 ExchangeMenuController.cs
**交易所箱子UI控制器**

**功能**：
在箱子菜单右侧注入一个按钮，用于标记/取消标记交易所箱子。

**UI注入**：
```
箱子菜单              注入按钮
┌──────────┐      ┌───┐
│          │      │ $ │  ← 金色=$标记为交易所
│  箱子    │  →   └───┘
│  内容    │      
└──────────┘      鼠标悬停提示：
                  "Set as Exchange Box"
                  或
                  "Exchange Box (Active)"
```

**视觉反馈**：
- 未标记箱子：灰色 `$` 符号
- 已标记箱子：金色 `$` 符号 + 箱子变金色

**事件处理**：
1. `OnMenuChanged`：检测箱子菜单打开
2. `OnRenderedActiveMenu`：绘制按钮
3. `OnButtonPressed`：处理点击事件

---

## 使用示例

### 打开交易菜单

```csharp
// 在ModEntry中注册按键事件
helper.Events.Input.ButtonPressed += OnButtonPressed;

private void OnButtonPressed(object? sender, ButtonPressedEventArgs e)
{
    if (e.Button == SButton.F10 && Context.IsPlayerFree)
    {
        Game1.activeClickableMenu = new TradingMenu(
            _marketManager, 
            _brokerageService, 
            Monitor
        );
    }
}
```

### 标记交易所箱子

```csharp
// ExchangeMenuController自动注入UI
var exchangeService = new ExchangeService();
var controller = new ExchangeMenuController(
    helper, 
    Monitor, 
    exchangeService
);

// 当玩家打开箱子时，自动显示标记按钮
// 点击后调用：
exchangeService.ToggleExchangeStatus(chest);
```

---

## 绘图技术

### 使用 SpriteBatch 绘制

```csharp
public override void draw(SpriteBatch b)
{
    // 1. 绘制背景遮罩
    b.Draw(Game1.fadeToBlackRect, 
        Game1.graphics.GraphicsDevice.Viewport.Bounds, 
        Color.Black * 0.75f);
    
    // 2. 绘制对话框边框
    Game1.drawDialogueBox(xPositionOnScreen, yPositionOnScreen, 
        width, height, false, true);
    
    // 3. 绘制文字
    Utility.drawTextWithShadow(b, "Title", 
        Game1.dialogueFont, position, Color.White);
    
    // 4. 绘制按钮
    IClickableMenu.drawTextureBox(b, Game1.mouseCursors, 
        sourceRect, destRect, buttonColor, 4f, false);
}
```

### 颜色编码

```csharp
// 盈亏颜色
Color pnlColor = pnl >= 0 ? Color.DarkGreen : Color.DarkRed;

// 保证金警告
Color marginColor = freeMargin >= 0 ? Color.White : Color.Red;

// 选中状态
Color btnColor = isSelected ? Color.Gold : Color.LightGray;
```

---

## 布局计算

### 居中对齐

```csharp
int centerX = xPositionOnScreen + width / 2;
int centerY = yPositionOnScreen + height / 2;

Vector2 textSize = font.MeasureString(text);
Vector2 textPos = new Vector2(
    centerX - textSize.X / 2,
    centerY - textSize.Y / 2
);
```

### 网格布局

```csharp
int leftX = xPositionOnScreen + 60;
int topY = yPositionOnScreen + 180;
int rowHeight = 30;

for (int row = 0; row < positions.Count; row++)
{
    int y = topY + (row * rowHeight);
    // 绘制这一行...
}
```

---

## 交互处理

### 点击检测

```csharp
public override void receiveLeftClick(int x, int y, bool playSound = true)
{
    foreach (var button in _buttons)
    {
        if (button.containsPoint(x, y))
        {
            Game1.playSound("coin");
            // 处理点击...
        }
    }
}
```

### 悬停检测

```csharp
public override void performHoverAction(int x, int y)
{
    if (button.containsPoint(x, y))
    {
        // 显示提示文字
        hoverText = "Click to buy";
    }
}
```

---

## 实时更新机制

### 价格刷新

```csharp
// TradingMenu 每次绘制时获取最新价格
private void DrawMarketTab(SpriteBatch b)
{
    var instruments = _marketManager.GetInstruments();
    if (instruments.Count > 0)
    {
        var inst = instruments[0];
        string priceText = $"{inst.CurrentPrice:F2} g";
        // 绘制价格...
    }
}
```

### 账户状态更新

```csharp
private void DrawAccountTab(SpriteBatch b)
{
    var prices = GetCurrentPrices();
    var account = _brokerageService.Account;
    
    // 实时计算
    decimal equity = account.GetTotalEquity(prices);
    decimal usedMargin = account.GetUsedMargin(prices);
    decimal freeMargin = account.GetFreeMargin(prices);
    
    // 绘制数据...
}
```

---

## 性能优化

### 避免频繁计算

```csharp
// ❌ 不好：每帧都创建字典
private void draw(SpriteBatch b)
{
    var prices = new Dictionary<string, decimal>();  // 频繁分配
    // ...
}

// ✓ 好：复用或按需计算
private Dictionary<string, decimal>? _cachedPrices;
private int _lastPriceCacheTick;

private Dictionary<string, decimal> GetCurrentPrices()
{
    if (currentTick - _lastPriceCacheTick > 60)
    {
        _cachedPrices = CalculatePrices();
        _lastPriceCacheTick = currentTick;
    }
    return _cachedPrices;
}
```

---

## 未来扩展

### 计划中的UI组件

1. **CandlestickChart.cs** - K线图组件
   - 绘制OHLC蜡烛图
   - 支持缩放和滚动
   - 显示成交量

2. **OrderBookView.cs** - 订单簿视图
   - 显示买卖挂单深度
   - 实时更新
   - 可点击快速下单

3. **PortfolioWindow.cs** - 持仓详情窗口
   - 分品种显示持仓
   - 历史交易记录
   - 盈亏曲线图

4. **NewsPanel.cs** - 新闻面板
   - 显示市场新闻
   - 新闻对价格的影响
   - 滚动显示

---

## 调试技巧

### 日志记录

```csharp
_monitor.Log($"Button clicked: {button.name}", LogLevel.Debug);
_monitor.Log($"Price updated: {price:F2}", LogLevel.Trace);
_monitor.Log($"Order executed: {quantity}", LogLevel.Info);
```

### 边界调试

```csharp
// 绘制边界框（调试用）
b.Draw(Game1.staminaRect, button.bounds, Color.Red * 0.3f);
```

---

## 最佳实践

1. **响应式设计**：使用百分比计算位置，而非硬编码
2. **颜色一致性**：定义颜色常量，统一UI风格
3. **音效反馈**：每个操作都应该有音效
4. **错误提示**：使用HUDMessage显示操作结果
5. **性能优先**：避免在draw()中进行复杂计算

---

## 资源引用

- `Game1.mouseCursors`：鼠标光标和UI元素纹理
- `Game1.dialogueFont`：大字体
- `Game1.smallFont`：小字体
- `Game1.fadeToBlackRect`：黑色矩形（用于遮罩）
- `Game1.staminaRect`：单像素矩形（用于绘制线条）
