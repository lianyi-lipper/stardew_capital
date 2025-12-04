namespace StardewCapital.Core.Common.Time
{
    /// <summary>
    /// 游戏时间提供者接口
    /// 为Core层提供时间信息，且不依赖于StardewValley命名空间。
    /// 
    /// 设计目的：
    /// 1. 依赖反转：Core层不直接依赖Game1，而是依赖此接口
    /// 2. 可测试性：可以替换为Mock实现，进行单元测试
    /// 3. 灵活性：支持多种时间源（游戏时间、真实时间、测试时间）
    /// </summary>
    public interface IGameTimeProvider
    {
        /// <summary>
        /// 获取游戏当前时间（例如：600表示早上6点，1350表示下午1点50分）
        /// </summary>
        int CurrentTimeOfDay { get; }

        /// <summary>
        /// 获取归一化的时间比例，从0.0（一天开始）到1.0（一天结束）
        /// 用于插值计算和进度计算
        /// </summary>
        double TimeRatio { get; }

        /// <summary>
        /// 获取游戏当前是否处于暂停状态
        /// </summary>
        bool IsPaused { get; }

        /// <summary>
        /// 获取当前游戏日已经过的总分钟数
        /// 用于连续时间计算
        /// </summary>
        int TotalMinutesToday { get; }
    }
}

