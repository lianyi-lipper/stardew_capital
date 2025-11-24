# Phase 11: Risk Management System Implementation Plan

## Goal Description
Implement a comprehensive Risk Management System to ensure market stability and prevent player insolvency abuse. This includes a Daily Settlement system (Mark-to-Market), Margin Maintenance checks (Margin Calls & Liquidation), and a robust Delivery Default handling mechanism.

## User Review Required
> [!IMPORTANT]
> **Settlement Logic Decision**: I am implementing a **Margin Level** based system rather than a strict cash-flow Mark-to-Market (MTM) for simplicity and better gameplay flow.
> - **Equity** = Cash + Unrealized P&L
> - **Margin Level** = Equity / Used Margin
> - **Margin Call**: If Margin Level < 80%
> - **Liquidation**: If Margin Level < 50%
>
> Strict MTM (crediting/debiting cash daily) might be too confusing for casual players.

## Proposed Changes

### Services Layer

#### [NEW] [ClearingService.cs](file:///e:/work/stardew_capital/Src/Services/Trading/ClearingService.cs)
- **Responsibilities**:
    - `DailySettlement()`: Performs daily margin checks and settlement logic.
    - `CheckMarginStatus()`: Calculates current Equity and Margin Level.
    - `IssueMarginCall()`: Sends warning messages (HUD/Mail) if margin is low.
    - `LiquidatePositions()`: Forcibly closes positions if margin is critical (< 50%).
- **Dependencies**: `BrokerageService`, `MarketManager`, `IMonitor`.

#### [MODIFY] [MarketManager.cs](file:///e:/work/stardew_capital/Src/Services/Market/MarketManager.cs)
- Inject `ClearingService`.
- Call `ClearingService.DailySettlement()` in `OnNewDay()` (or hook into `DayEnding` if accessible, but `OnNewDay` is currently used for daily logic).

#### [MODIFY] [DeliveryService.cs](file:///e:/work/stardew_capital/Src/Services/Trading/DeliveryService.cs)
- Enhance `ProcessShortPosition`:
    - Send a **Mail** notification in case of default (insufficient items + cash penalty) to serve as a permanent record/severe warning.
    - Ensure penalty calculation is robust (already implemented as cash deduction, will verify).

#### [MODIFY] [BrokerageService.cs](file:///e:/work/stardew_capital/Src/Services/Trading/BrokerageService.cs)
- Add helper methods:
    - `GetTotalUnrealizedPnL(prices)`: Calculate total floating P&L.
    - `GetEquity(prices)`: Calculate Account Equity.
    - `LiquidatePosition(symbol)`: Helper to force close a specific position (ignoring margin checks).

## Verification Plan

### Automated Tests
- Since I cannot run unit tests easily in this environment, I will rely on manual verification.

### Manual Verification
1.  **Margin Call Test**:
    - Open a large position (high leverage).
    - Wait for price to move against the position (or use a debug command/cheat to force price change).
    - Verify HUD warning when Margin Level drops below 80%.
2.  **Liquidation Test**:
    - Continue holding losing position until Margin Level drops below 50%.
    - Verify position is forcibly closed.
    - Verify "Liquidation" message in HUD/Log.
3.  **Delivery Default Test**:
    - Open a Short position.
    - Ensure no items in Inventory or Exchange Box.
    - Sleep until delivery day.
    - Verify Cash deduction (penalty).
    - Verify "Default" mail received.
