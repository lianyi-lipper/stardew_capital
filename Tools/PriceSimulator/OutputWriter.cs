using System;
using System.IO;
using System.Text.Json;
using StardewCapital.Data.SaveData;

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
    }
}
