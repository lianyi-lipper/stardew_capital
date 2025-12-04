// ============================================================================
// 星露谷资本 (Stardew Capital)
// 模块：市场剧本管理器
// 作者：Stardew Capital Team
// 用途：管理市场情绪剧本的切换和参数配置（模型五配套服务）
// ============================================================================

using StardewCapital.Core.Futures.Domain.Market;
using StardewModdingAPI;
using System;
using StardewCapital.Core.Futures.Services;
using System.Collections.Generic;
using StardewCapital.Core.Futures.Services;

namespace StardewCapital.Services.News
{
    /// <summary>
    /// 市场剧本管理器
    /// 
    /// 职责：
    /// 1. 存储和管理当前活跃的市场剧本
    /// 2. 定期（每日早晨）随机切换剧本
    /// 3. 提供预设剧本的参数配置
    /// 4. 供UI和ImpactService查询当前剧本状态
    /// </summary>
    public class ScenarioManager
    {
        private readonly IMonitor _monitor;
        private readonly Random _random;

        /// <summary>
        /// 当前市场剧本类型
        /// </summary>
        private ScenarioType _currentScenario;

        /// <summary>
        /// 当前剧本参数缓存
        /// </summary>
        private ScenarioParameters _currentParameters;

        /// <summary>
        /// 剧本切换概率（每日早晨触发）
        /// 默认值：0.3（30%）
        /// </summary>
        private double _switchProbability = 0.3;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="monitor">SMAPI日志接口</param>
        public ScenarioManager(IMonitor monitor)
        {
            _monitor = monitor;
            _random = new Random();

            // 初始化：随机选择一个初始剧本
            _currentScenario = GetRandomScenario();
            _currentParameters = GetScenarioParameters(_currentScenario);

            _monitor.Log($"[ScenarioManager] 初始化完成，初始剧本: {_currentScenario} - {_currentParameters.Description}", LogLevel.Info);
        }

        /// <summary>
        /// 每日新日触发（由 MarketManager.OnNewDay 调用）
        /// 
        /// 逻辑：
        /// 根据概率决定是否切换到新剧本
        /// 时间点：游戏内早晨 6:00 AM
        /// </summary>
        public void OnNewDay()
        {
            // 掷骰子：是否切换剧本？
            double roll = _random.NextDouble();

            if (roll < _switchProbability)
            {
                var oldScenario = _currentScenario;
                SwitchScenario();

                _monitor.Log(
                    $"[ScenarioManager] 市场剧本切换: {oldScenario} → {_currentScenario} | {_currentParameters.Description}",
                    LogLevel.Info
                );
            }
            else
            {
                _monitor.Log(
                    $"[ScenarioManager] 剧本保持不变: {_currentScenario}",
                    LogLevel.Debug
                );
            }
        }

        /// <summary>
        /// 立即切换到新剧本
        /// （随机选择，确保与当前剧本不同）
        /// </summary>
        public void SwitchScenario()
        {
            var candidates = new List<ScenarioType>
            {
                ScenarioType.DeadMarket,
                ScenarioType.IrrationalExuberance,
                ScenarioType.PanicSelling,
                ScenarioType.ShortSqueeze
            };

            // 移除当前剧本，避免重复
            candidates.Remove(_currentScenario);

            // 随机选择新剧本
            int index = _random.Next(candidates.Count);
            _currentScenario = candidates[index];

            // 更新参数缓存
            _currentParameters = GetScenarioParameters(_currentScenario);
        }

        /// <summary>
        /// 获取当前剧本类型
        /// </summary>
        public ScenarioType GetCurrentScenario()
        {
            return _currentScenario;
        }

        /// <summary>
        /// 获取当前剧本参数
        /// </summary>
        public ScenarioParameters GetCurrentParameters()
        {
            return _currentParameters;
        }

        /// <summary>
        /// 设置剧本切换概率
        /// </summary>
        /// <param name="probability">概率值（0.0 - 1.0）</param>
        public void SetSwitchProbability(double probability)
        {
            _switchProbability = Math.Clamp(probability, 0.0, 1.0);
        }

        /// <summary>
        /// 获取指定剧本的预设参数配置
        /// 
        /// 所有参数基于期货.md中的模型五设计
        /// </summary>
        /// <param name="scenario">剧本类型</param>
        /// <returns>剧本参数</returns>
        public ScenarioParameters GetScenarioParameters(ScenarioType scenario)
        {
            switch (scenario)
            {
                case ScenarioType.DeadMarket:
                    return new ScenarioParameters(
                        smartMoneyStrength: 0.8,    // 极高回归力
                        trendFollowerStrength: 0.1,
                        fomoStrength: 0.0,          // 无跟风
                        description: "市场交投清淡，价格被稳稳锁死"
                    );

                case ScenarioType.IrrationalExuberance:
                    return new ScenarioParameters(
                        smartMoneyStrength: 0.0,    // 理性消失
                        trendFollowerStrength: 0.3,
                        fomoStrength: 0.9,          // 极端情绪
                        description: "每个人都觉得自己是股神"
                    );

                case ScenarioType.PanicSelling:
                    return new ScenarioParameters(
                        smartMoneyStrength: 0.2,
                        trendFollowerStrength: 0.0,
                        fomoStrength: 0.7,
                        description: "恐惧支配了山谷",
                        asymmetricDown: 1.5         // 下跌时情绪放大1.5倍
                    );

                case ScenarioType.ShortSqueeze:
                    return new ScenarioParameters(
                        smartMoneyStrength: -0.5,   // 负数 = 聪明钱被迫反向操作
                        trendFollowerStrength: 0.4,
                        fomoStrength: 0.6,
                        description: "空头不死，多头不止"
                    );

                default:
                    // 默认返回死水一潭
                    return new ScenarioParameters(
                        smartMoneyStrength: 0.8,
                        trendFollowerStrength: 0.1,
                        fomoStrength: 0.0,
                        description: "市场交投清淡，价格被稳稳锁死"
                    );
            }
        }

        /// <summary>
        /// 随机选择一个剧本类型
        /// </summary>
        private ScenarioType GetRandomScenario()
        {
            var scenarioValues = Enum.GetValues(typeof(ScenarioType));
            int index = _random.Next(scenarioValues.Length);
            return (ScenarioType)scenarioValues.GetValue(index)!;
        }
    }
}


