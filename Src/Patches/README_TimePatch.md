# 时间补丁实现说明

## 功能概述

实现了**交易UI打开时游戏时间不暂停**的核心功能，这是实现高频交易和防止作弊的前提。

## 技术方案

### 1. 核心原理

Stardew Valley 默认在打开菜单时会暂停游戏时间。我们通过 **Harmony 补丁**拦截游戏底层方法，强制让时间继续流逝。

### 2. 实现方式

使用 Harmony 拦截两个关键方法：

#### 补丁 1: `Game1.shouldTimePass()`
- **作用**：控制游戏是否应该让时间流逝
- **原始行为**：打开菜单时返回 `false`（暂停时间）
- **修改后**：检测到 `TradingMenu` 打开时返回 `true`（时间继续）

```csharp
[HarmonyPostfix]
[HarmonyPatch(typeof(Game1), nameof(Game1.shouldTimePass))]
public static void ShouldTimePass_Postfix(ref bool __result)
{
    if (!__result && Game1.activeClickableMenu is TradingMenu)
    {
        __result = true; // 强制时间流逝
    }
}
```

#### 补丁 2: `Game1._update(GameTime gameTime)`
- **作用**：游戏主循环
- **原始行为**：检测到菜单打开时跳过时钟更新
- **修改后**：手动调用 `Game1.UpdateGameClock(gameTime)` 更新时钟

```csharp
[HarmonyPostfix]
[HarmonyPatch(typeof(Game1), "_update")]
public static void Update_Postfix(GameTime gameTime)
{
    if (!Game1.IsMultiplayer && Game1.activeClickableMenu != null)
    {
        if (Game1.shouldTimePass()) // 触发补丁1
        {
            Game1.UpdateGameClock(gameTime); // 手动更新时钟
        }
    }
}
```

### 3. 文件变更

#### 新增文件
- **`Src/Patches/TimePatch.cs`**：Harmony 补丁实现类

#### 修改文件
- **`Src/ModEntry.cs`**：
  - 添加 `HarmonyLib` 和 `StardewCapital.Patches` 命名空间引用
  - 在 `Entry()` 方法开头调用 `ApplyHarmonyPatches()`
  - 新增 `ApplyHarmonyPatches()` 方法

- **`StardewCapital.csproj`**：
  - 添加 `Lib.Harmony` NuGet 包引用（版本 2.2.2）
  - 排除 `stardew_capital_old` 目录避免编译冲突

### 4. 技术优势

#### 🎯 使用 Harmony 特性标注（推荐）
```csharp
// 自动应用所有补丁
var harmony = new Harmony(ModManifest.UniqueID);
harmony.PatchAll();
```

**优点**：
- ✅ 代码简洁，自动扫描所有标注了 `[HarmonyPatch]` 的方法
- ✅ 易于维护，新增补丁只需添加特性标注
- ✅ 类型安全，编译时检查方法签名

#### ⚙️ 手动注册补丁（旧版本方式）
```csharp
// 手动指定每个补丁
harmony.Patch(
    original: AccessTools.Method(typeof(Game1), nameof(Game1.shouldTimePass)),
    postfix: new HarmonyMethod(AccessTools.Method(typeof(TimePatch), nameof(TimePatch.ShouldTimePass_Postfix)))
);
```

**缺点**：
- ❌ 需要手动注册每个补丁，代码冗长
- ❌ 容易出错，方法名字符串硬编码

## 为什么需要这个功能？

### 设计理念

根据项目设计文档（`期货.md`）：

> **UI不暂停**：Mod的交易UI界面**必须设置**为"打开时不暂停游戏时间"，这是实现高频交易和防止作弊的前提。

### 核心原因

1. **防止作弊**
   - 如果UI暂停时间，玩家可以：
     - 打开菜单查看当前价格
     - 关闭菜单等待时机
     - 再次打开菜单执行交易
   - 这会破坏"实时交易"的核心设计

2. **实时交易体验**
   - 价格必须在UI打开状态下继续波动
   - 模拟真实金融市场的实时性
   - 玩家需要在价格波动中快速决策

3. **高频交易支持**
   - 只有时间不暂停，才能实现基于真实时间的 Tick 更新
   - 价格每 0.7 秒更新一次（独立于游戏帧率）
   - 布朗桥模型需要连续的时间进度

## 测试验证

### 验证步骤

1. **编译项目**
   ```bash
   dotnet build
   ```
   应该看到编译成功，没有错误。

2. **运行游戏**
   - 加载存档
   - 按 **F10** 打开交易菜单

3. **检查时间流逝**
   - 观察游戏内时钟（右上角）
   - 应该看到时间继续流逝（如 6:00 AM → 6:10 AM）
   - 市场价格应该继续波动

4. **检查日志**
   在 SMAPI 控制台应该看到：
   ```
   [StardewCapital] Harmony patches applied successfully (Time system enabled)
   ```

### 预期行为

| 菜单状态       | 游戏时间 | 价格波动 | 玩家位置 |
|--------------|---------|---------|---------|
| 关闭菜单      | 正常流逝 | ✅ 继续  | 可移动   |
| 打开其他菜单   | 暂停    | ❌ 暂停  | 不可移动 |
| **打开交易菜单** | **继续流逝** | **✅ 继续** | **不可移动** |

## 兼容性说明

### ✅ 兼容场景

- **骷髅洞穴**：游戏时间变慢 → 价格趋势变慢 → 市场横盘
- **时间加速 Mod**：游戏时间变快 → 价格趋势加速 → 市场趋势极强
- **单人模式**：完全支持
- **多人模式**：自动禁用补丁（时间由服务器控制）

### ⚠️ 注意事项

1. **多人游戏**
   - 补丁会自动检测多人模式并禁用
   - 避免与服务器时间同步冲突

2. **其他菜单**
   - 补丁只影响 `TradingMenu`
   - 其他游戏菜单仍然会暂停时间

3. **性能影响**
   - Harmony 补丁开销极小（纳秒级）
   - 对游戏性能无明显影响

## 参考资料

- **Harmony 文档**：https://harmony.pardeike.net/
- **SMAPI 文档**：https://stardewvalleywiki.com/Modding:Modder_Guide
- **旧版本实现**：`stardew_capital_old/TimePatch.cs`

## 变更历史

- **2025-11-25**：初始实现，使用 Harmony 2.2.2 和特性标注方式
