using System;
using System.IO;
using System.Text.Json;
using StardewCapital.Core.Futures.Data;

namespace StardewCapital.Simulator
{
    /// <summary>
    /// 输出数据写入器
    /// 将模拟结果写入到文件（JSON格式，与游戏一致）
    /// </summary>
    public class OutputWriter
    {
        /// <summary>
        /// 写入JSON格式的MarketStateSaveData（与游戏存档格式一致）
        /// </summary>
        public static void WriteJson(MarketStateSaveData data, string outputPath)
        {
            try
            {
                // 确保输出目录存在
                string? directory = Path.GetDirectoryName(outputPath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                // 序列化为JSON（与游戏使用的System.Text.Json保持一致）
                string json = JsonSerializer.Serialize(data, new JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });

                // 写入文件
                File.WriteAllText(outputPath, json);

                Console.WriteLine($"\n✓ 数据已保存到: {Path.GetFullPath(outputPath)}");
                Console.WriteLine($"  文件大小: {new FileInfo(outputPath).Length / 1024.0:F2} KB");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n✗ 保存失败: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// 打印摘要信息到控制台
        /// </summary>
        public static void PrintSummary(MarketStateSaveData data)
        {
            Console.WriteLine($"\n========== 输出摘要 ==========");
            Console.WriteLine($"季节: {data.CurrentSeason} Year {data.CurrentYear}");
            Console.WriteLine($"期货品种数: {data.FuturesStates.Count}");
            Console.WriteLine($"存档时间: {data.SaveTimestamp:yyyy-MM-dd HH:mm:ss}");
            Console.WriteLine($"版本: {data.Version}");

            foreach (var futuresState in data.FuturesStates)
            {
                Console.WriteLine($"\n{futuresState.Symbol} ({futuresState.CommodityName}):");
                Console.WriteLine($"  价格点数: {futuresState.ShadowPrices.Length}");
                Console.WriteLine($"  基本面点数: {futuresState.FundamentalValues?.Length ?? 0}");
                Console.WriteLine($"  新闻事件数: {futuresState.ScheduledNews.Count}");
                Console.WriteLine($"  到期日: Day {futuresState.DeliveryDay}");
            }
        }

        /// <summary>
        /// 写入实时模拟结果到JSON
        /// </summary>
        public static void WriteRealtimeJson(RealtimeSimulationResult data, string outputPath)
        {
            try
            {
                string? directory = Path.GetDirectoryName(outputPath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                string json = JsonSerializer.Serialize(data, new JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });

                File.WriteAllText(outputPath, json);

                Console.WriteLine($"\n✓ 实时数据已保存到: {Path.GetFullPath(outputPath)}");
                Console.WriteLine($"  文件大小: {new FileInfo(outputPath).Length / 1024.0:F2} KB");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n✗ 保存失败: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// 打印实时模拟摘要
        /// </summary>
        public static void PrintRealtimeSummary(RealtimeSimulationResult result)
        {
            if (result.FrameData.Count == 0)
            {
                Console.WriteLine("\n⚠ 没有生成任何数据");
                return;
            }

            var firstFrame = result.FrameData[0];
            var lastFrame = result.FrameData[^1];

            Console.WriteLine($"\n========== 实时模拟摘要 ==========");
            Console.WriteLine($"商品: {result.CommodityName}");
            Console.WriteLine($"数据点数: {result.FrameData.Count}");
            Console.WriteLine($"\n价格统计:");
            Console.WriteLine($"  开始价格: {firstFrame.RealtimePrice:F2}g");
            Console.WriteLine($"  结束价格: {lastFrame.RealtimePrice:F2}g");
            Console.WriteLine($"  价格变化: {(lastFrame.RealtimePrice - firstFrame.RealtimePrice):+0.00;-0.00}g");
            
            Console.WriteLine($"\n冲击统计:");
            Console.WriteLine($"  开始冲击: {firstFrame.Impact:+0.00;-0.00}g");
            Console.WriteLine($"  结束冲击: {lastFrame.Impact:+0.00;-0.00}g");
            
            Console.WriteLine($"\nNPC力量（最后时刻）:");
            Console.WriteLine($"  基础流量: {lastFrame.NPCForces.BaseFlow:F0}");
            Console.WriteLine($"  聪明钱: {lastFrame.NPCForces.SmartMoneyFlow:F0}");
            Console.WriteLine($"  趋势派: {lastFrame.NPCForces.TrendFlow:F0}");
            Console.WriteLine($"  FOMO: {lastFrame.NPCForces.FomoFlow:F0}");
            Console.WriteLine($"  总流量: {lastFrame.NPCForces.TotalFlow:F0}");
        }
    }
}

