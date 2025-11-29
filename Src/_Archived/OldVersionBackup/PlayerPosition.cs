// PlayerPosition.cs
using System;

namespace StardewCapital
{
    public class PlayerPosition
    {
        public Guid PositionId { get; set; }
        public bool IsLong; // true=做多(买入), false=做空(卖出)
        public double EntryPrice; // 开仓时的价格
        public int Contracts; // 合约数量
        public double MarginUsed; // 这个持仓占用了多少保证金
    }
}
