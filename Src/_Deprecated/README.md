# ⚠️ 废弃的归档目录

**注意：此目录已清空，原计划归档的文件仍在使用中**

## 代码架构说明

项目中存在两条并行的价格计算路径：

### 1️⃣ **影子价格预生成**（离线）
- **入口**: `GamePriceCalculatorAdapter` 
- **核心**: `StandalonePriceCalculator`
- **用途**: 游戏启动时，预生成整个季度的价格轨迹
- **特点**: 
  - ✅ 完全从 `market_rules.json` 读取配置
  - ✅ 支持可重现性（Random seed）
  - ✅ 独立于游戏API

### 2️⃣ **实时价格更新**（在线）
- **入口**: `MarketPriceUpdater`
- **核心**: `PriceEngine`
- **用途**: 游戏运行时，每帧更新当前价格
- **特点**:
  - ⚠️ 使用硬编码的波动率常量
  - ⚠️ 依赖游戏时钟和实时状态
  - 📝 未来可能需要重构以使用配置

### 3️⃣ **ShadowPriceGenerator**
- **状态**: 旧API，可能不再使用
- **建议**: 保留但标记为过时，待确认无引用后再删除

## 改进建议

`PriceEngine` 应该从配置读取波动率参数，保持与 `StandalonePriceCalculator` 一致。
