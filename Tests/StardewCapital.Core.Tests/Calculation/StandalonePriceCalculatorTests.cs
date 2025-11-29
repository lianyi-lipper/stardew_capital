using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Xunit;
using StardewCapital.Core.Calculation;
using StardewCapital.Core.Models;
using StardewCapital.Domain.Market;
using StardewCapital.Services.News;
using StardewCapital.Config;

namespace StardewCapital.Core.Tests.Calculation
{
    /// <summary>
    /// 单元测试: StandalonePriceCalculator
    /// </summary>
    public class StandalonePriceCalculatorTests
    {
        #region Test Data Helpers

        /// <summary>
        /// 创建标准测试输入
        /// </summary>
        private PriceCalculationInput CreateTestInput()
        {
            return new PriceCalculationInput
            {
                CommodityName = "TestCommodity",
                Season = Season.Spring,
                StartPrice = 100.0,
                TotalDays = 28,
                StepsPerDay = 120,
                BaseVolatility = 0.15,
                IntraVolatility = 0.08,
                CommodityConfig = LoadTestCommodityConfig(),
                NewsTemplates = LoadTestNewsTemplates().ToList(),
                MarketRules = CreateTestMarketRules(),
                RandomSeed = null // 默认无固定种子
            };
        }

        /// <summary>
        /// 加载测试用商品配置
        /// </summary>
        private CommodityConfig LoadTestCommodityConfig()
        {
            string jsonPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TestData", "test_commodity_config.json");
            string json = File.ReadAllText(jsonPath);
            var config = JsonSerializer.Deserialize<CommodityConfig>(json, new JsonSerializerOptions 
            { 
                PropertyNameCaseInsensitive = true,
                PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
            });
            
            if (config == null)
                throw new InvalidOperationException("Failed to load test commodity config");
            
            return config;
        }

        /// <summary>
        /// 加载测试用新闻模板
        /// </summary>
        private NewsTemplate[] LoadTestNewsTemplates()
        {
            string jsonPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TestData", "test_news_templates.json");
            string json = File.ReadAllText(jsonPath);
            
            var jsonDoc = JsonDocument.Parse(json);
            var templatesElement = jsonDoc.RootElement.GetProperty("templates");
            
            var templates = JsonSerializer.Deserialize<NewsTemplate[]>(templatesElement.GetRawText(), new JsonSerializerOptions 
            { 
                PropertyNameCaseInsensitive = true 
            });
            
            if (templates == null || templates.Length == 0)
                throw new InvalidOperationException("Failed to load test news templates");
            
            return templates;
        }

        /// <summary>
        /// 创建测试用市场规则
        /// </summary>
        private MarketRules? CreateTestMarketRules()
        {
            return new MarketRules
            {
                IntradayNews = new IntradayNewsConfig
                {
                    IntradayNewsProbability = 0.1 // 10% 概率
                }
            };
        }

        #endregion

        #region Test Cases

        /// <summary>
        /// 测试1: 相同种子产生相同输出 (可重现性)
        /// </summary>
        [Fact]
        public void SameSeed_ProducesSameOutput()
        {
            // Arrange
            var input = CreateTestInput();
            var calc1 = new StandalonePriceCalculator(seed: 12345);
            var calc2 = new StandalonePriceCalculator(seed: 12345);

            // Act
            var output1 = calc1.Calculate(input);
            var output2 = calc2.Calculate(input);

            // Assert: 价格数组必须完全一致
            Assert.Equal(output1.ShadowPrices.Length, output2.ShadowPrices.Length);
            for (int i = 0; i < output1.ShadowPrices.Length; i++)
            {
                Assert.Equal(output1.ShadowPrices[i], output2.ShadowPrices[i], precision: 5);
            }

            // Assert: 基本面数组也必须一致
            Assert.Equal(output1.FundamentalValues, output2.FundamentalValues);
        }

        /// <summary>
        /// 测试2: 不同种子产生不同输出
        /// </summary>
        [Fact]
        public void DifferentSeeds_ProduceDifferentOutputs()
        {
            // Arrange
            var input = CreateTestInput();
            var calc1 = new StandalonePriceCalculator(seed: 111);
            var calc2 = new StandalonePriceCalculator(seed: 222);

            // Act
            var output1 = calc1.Calculate(input);
            var output2 = calc2.Calculate(input);

            // Assert: 至少有一个价格点不同
            bool hasDifference = false;
            for (int i = 0; i < output1.ShadowPrices.Length && i < output2.ShadowPrices.Length; i++)
            {
                if (System.Math.Abs(output1.ShadowPrices[i] - output2.ShadowPrices[i]) > 0.01)
                {
                    hasDifference = true;
                    break;
                }
            }

            Assert.True(hasDifference, "Different seeds should produce different outputs");
        }

        /// <summary>
        /// 测试3: 零波动率产生相对稳定的价格
        /// </summary>
        [Fact]
        public void ZeroVolatility_ProducesStablePrices()
        {
            // Arrange
            var input = CreateTestInput();
            input.BaseVolatility = 0.0;
            input.IntraVolatility = 0.0;

            var calculator = new StandalonePriceCalculator(seed: 555);

            // Act
            var output = calculator.Calculate(input);

            // Assert: 价格波动应该很小
            var firstPrice = output.ShadowPrices[0];
            var allPricesStable = output.ShadowPrices.All(p =>
                System.Math.Abs(p - firstPrice) < firstPrice * 0.1); // 允许10%偏差（GBM的随机项仍存在）

            Assert.True(allPricesStable, "With zero volatility, prices should be relatively stable");
        }

        /// <summary>
        /// 测试4: 所有价格必须为正
        /// </summary>
        [Fact]
        public void AllPrices_ArePositive()
        {
            // Arrange
            var input = CreateTestInput();
            var calculator = new StandalonePriceCalculator(seed: 999);

            // Act
            var output = calculator.Calculate(input);

            // Assert: 所有价格 > 0
            Assert.All(output.ShadowPrices, price => Assert.True(price > 0, $"Price {price} should be positive"));
        }

        /// <summary>
        /// 测试5: 输出数据点数量正确 (参数化测试)
        /// </summary>
        [Theory]
        [InlineData(28, 120)] // 标准季度
        [InlineData(14, 60)]  // 半季度
        [InlineData(7, 30)]   // 一周
        public void OutputLength_MatchesInputDays(int days, int stepsPerDay)
        {
            // Arrange
            var input = CreateTestInput();
            input.TotalDays = days;
            input.StepsPerDay = stepsPerDay;

            var calculator = new StandalonePriceCalculator(seed: 777);

            // Act
            var output = calculator.Calculate(input);

            // Assert
            int expectedPoints = days * stepsPerDay;
            Assert.Equal(expectedPoints, output.ShadowPrices.Length);
            Assert.Equal(days, output.FundamentalValues.Length);
            Assert.Equal(stepsPerDay, output.StepsPerDay);
        }

        /// <summary>
        /// 测试6: 新闻事件生成与调度正确
        /// </summary>
        [Fact]
        public void NewsEvents_AreScheduledCorrectly()
        {
            // Arrange
            var input = CreateTestInput();
            var calculator = new StandalonePriceCalculator(seed: 888);

            // Act
            var output = calculator.Calculate(input);

            // Assert: 应该有新闻事件（至少有开盘新闻的可能性）
            // 注意: 由于新闻是随机的，可能为0，但对于28天和3个模板，几乎不可能为0
            // 这里我们只验证如果有新闻，它们的TriggerDay必须在有效范围内
            if (output.ScheduledNews.Count > 0)
            {
                Assert.All(output.ScheduledNews, newsEvent =>
                {
                    Assert.InRange(newsEvent.TriggerDay, 1, input.TotalDays);
                });
            }
        }

        /// <summary>
        /// 测试7: 输出包含必要的元数据
        /// </summary>
        [Fact]
        public void Output_ContainsRequiredMetadata()
        {
            // Arrange
            var input = CreateTestInput();
            var calculator = new StandalonePriceCalculator(seed: 666);

            // Act
            var output = calculator.Calculate(input);

            // Assert
            Assert.NotNull(output.ShadowPrices);
            Assert.NotNull(output.FundamentalValues);
            Assert.NotNull(output.ScheduledNews);
            Assert.True(output.GeneratedAt != default(DateTime));
            Assert.Equal(input.StepsPerDay, output.StepsPerDay);
        }

        #endregion
    }
}
