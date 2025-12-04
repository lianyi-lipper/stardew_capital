using System;
using System.Collections.Generic;
using StardewCapital.Core.Futures.Config;
using StardewCapital.Core.Common.Logging;
using StardewCapital.Core.Futures.Domain.Market;
using StardewCapital.Core.Futures.Services;
using StardewCapital.Simulator.MockServices;

namespace StardewCapital.Simulator
{
    /// <summary>
    /// 实时价格模拟运行器（简化版）
    /// 模拟核心实时价格系统：NPC行为 + 市场冲击
    /// 不包含订单簿系统（避免SMAPI依赖）
    /// </summary>
    public class RealtimeSimulationRunner
    {
        private readonly ILogger _logger;
        private readonly SimulatorConfig _config;
        private readonly List<CommodityConfig> _commodityConfigs;
        private readonly MarketRules _marketRules;
        
        // 核心服务（已解耦，可独立运行）
        private readonly ImpactService _impactService;
        private readonly NPCAgentManager _npcAgentManager;

        public RealtimeSimulationRunner(
            SimulatorConfig config,
            List<CommodityConfig> commodityConfigs,
            MarketRules marketRules)
        {
            _config = config;
            _commodityConfigs = commodityConfigs;
            _marketRules = marketRules;
            
            // 创建控制台日志
            _logger = new ConsoleLogger(enableTrace: config.advanced.verboseOutput);
            
            // 初始化核心服务
            _impactService = new ImpactService(_logger);
            _impactService.Configure(marketRules.MarketMicrostructure);
            
            _npcAgentManager = new NPCAgentManager(_logger, marketRules);
        }

        /// <summary>
        /// 运行实时价格模拟
        /// </summary>
        public RealtimeSimulationResult Run()
        {
            Console.WriteLine("\n========== 实时价格模拟 ==========");
            Console.WriteLine($"商品: {_config.simulation.commodity}");
            Console.WriteLine($"模拟: 核心价格系统（NPC + 冲击）");
            Console.WriteLine($"模拟帧数: 600 (约10分钟游戏时间)");
            
            var result = new RealtimeSimulationResult
            {
                CommodityName = _config.simulation.commodity,
                FrameData = new List<RealtimeFrameData>()
            };
            
            // 获取商品配置
            var commodityConfig = _commodityConfigs.Find(c => c.Name == _config.simulation.commodity);
            if (commodityConfig == null)
            {
                throw new Exception($"未找到商品配置: {_config.simulation.commodity}");
            }
            
            // 设置初始价格
            double currentPrice = commodityConfig.BasePrice;
            double shadowPrice = currentPrice;
            double fundamentalValue = currentPrice;
            
            string symbol = $"{_config.simulation.commodity}_F";
            
            Console.WriteLine($"\n初始价格: {currentPrice:F2}g");
            Console.WriteLine("开始模拟...\n");
            
            // 模拟600帧（一个游戏日的交易时段）
            var random = new Random(_config.simulation.randomSeed ?? Environment.TickCount);
            
            for (int frame = 0; frame < 600; frame++)
            {
                // 1. 模拟影子价格小幅波动（布朗运动简化版）
                double drift = 0.0;
                double volatility = 0.005;
                double dt = 1.0 / 60.0; // 每帧约1分钟
                double dW = random.NextDouble() - 0.5;
                shadowPrice += drift * dt + volatility * Math.Sqrt(dt) * dW;
                
                // 2. 计算NPC虚拟流量
                var scenarioData = _marketRules.MarketMicrostructure.Scenarios["Normal"];
                var scenarioParams = new ScenarioParameters
                {
                    SmartMoneyStrength = scenarioData.SmartMoneyStrength,
                    TrendFollowerStrength = scenarioData.TrendFollowerStrength,
                    FOMOStrength = scenarioData.FomoStrength,
                    AsymmetricDown = scenarioData.AsymmetricDown,
                    Description = scenarioData.Description
                };
                
                int virtualFlow = _npcAgentManager.CalculateNetVirtualFlow(
                    symbol,
                    currentPrice,
                    shadowPrice,
                    fundamentalValue,
                    scenarioParams
                );
                
                // 3. 更新市场冲击
                _impactService.UpdateImpact(
                    commodityConfig.ItemId,
                    currentPrice,
                    fundamentalValue,
                    scenarioParams
                );
                
                double impact = _impactService.GetCurrentImpact(commodityConfig.ItemId);
                
                // 4. 应用冲击到价格
                currentPrice = shadowPrice + impact;
                
                // 5. 记录帧数据（每10帧记录一次，减少数据量）
                if (frame % 10 == 0)
                {
                    var frameData = new RealtimeFrameData
                    {
                        Frame = frame,
                        Time = $"{6 + frame / 60:D2}:{frame % 60:D2}",
                        ShadowPrice = shadowPrice,
                        Impact = impact,
                        RealtimePrice = currentPrice,
                        VirtualFlow = virtualFlow,
                        NPCForces = _npcAgentManager.LastForces.ContainsKey(symbol) 
                            ? _npcAgentManager.LastForces[symbol] 
                            : default
                    };
                    
                    result.FrameData.Add(frameData);
                    
                    // 输出采样日志
                    if (frame % 60 == 0 && _config.advanced.verboseOutput)
                    {
                        Console.WriteLine(
                            $"[{frameData.Time}] " +
                            $"Shadow={shadowPrice:F2}g, " +
                            $"Impact={impact:+0.00;-0.00}g, " +
                            $"Real={currentPrice:F2}g, " +
                            $"Flow={virtualFlow:+0;-0}"
                        );
                    }
                }
            }
            
            Console.WriteLine("\n✓ 实时模拟完成");
            Console.WriteLine($"记录了 {result.FrameData.Count} 个数据点");
            Console.WriteLine("\n说明：此版本仅模拟核心价格系统（NPC行为+市场冲击）");
            Console.WriteLine("      订单簿系统需要在完整游戏环境中测试");
            
            return result;
        }
    }

    /// <summary>
    /// 实时模拟结果
    /// </summary>
    public class RealtimeSimulationResult
    {
        public string CommodityName { get; set; } = "";
        public List<RealtimeFrameData> FrameData { get; set; } = new();
    }

    /// <summary>
    /// 每帧的实时数据
    /// </summary>
    public class RealtimeFrameData
    {
        public int Frame { get; set; }
        public string Time { get; set; } = "";
        public double ShadowPrice { get; set; }
        public double Impact { get; set; }
        public double RealtimePrice { get; set; }
        public int VirtualFlow { get; set; }
        public AgentForces NPCForces { get; set; }
    }
}



