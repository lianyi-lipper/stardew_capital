# Phase 11: Risk Management System Walkthrough

## Overview
This phase implemented the Risk Management System, ensuring market stability and preventing player insolvency. Key components include Daily Settlement, Margin Calls, Liquidation, and Delivery Default handling.

## Changes

### 1. Clearing Service (`ClearingService.cs`)
- **Daily Settlement**: Implemented `DailySettlement()` to perform Mark-to-Market checks at the end of each day.
- **Margin Logic**:
    - Calculates **Equity** (Cash + Unrealized P&L) and **Margin Level** (Equity / Used Margin).
    - **Margin Call**: Triggers warning if Margin Level < 80%.
    - **Liquidation**: Force closes positions if Margin Level < 50%.

### 2. Brokerage Service Updates (`BrokerageService.cs`)
- Added `GetTotalUnrealizedPnL(prices)` and `GetEquity(prices)` to support real-time risk calculation.
- Added `LiquidatePosition(symbol)` helper to forcibly close positions without margin checks (used during liquidation).
- Exposed `GetCurrentPrices()` for settlement calculations.

### 3. Delivery Service Updates (`DeliveryService.cs`)
- Enhanced `ProcessShortPosition` to handle defaults.
- Added **Mail Notification** for delivery defaults, serving as a permanent record and severe warning.

### 4. Market Manager Integration (`MarketManager.cs`)
- Integrated `ClearingService` into the daily loop via `OnNewDay()`.

### 5. Domain Model Updates
- Updated `TradingAccount` and `Position` to include `UsedMargin` properties and necessary calculation methods.

## Verification Results

### Compilation
- **Status**: ✅ Passed
- The project compiles successfully after fixing missing property definitions in `TradingAccount` and `Position`.

### Manual Verification Steps (To be performed in-game)
1.  **Margin Call**: Open a high-leverage position, wait for adverse price move, verify HUD warning.
2.  **Liquidation**: Continue holding losing position, verify forced closure when margin level drops below 50%.
3.  **Default**: Hold short position to maturity without items, verify cash penalty and mail notification.

## Next Steps
- Proceed to **Phase 12: UI Enhancements** to visualize these risk metrics (Margin Level, Liquidation Price) in the Web UI.
