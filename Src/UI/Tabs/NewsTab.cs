// ============================================================================
// 星露资本 (Stardew Capital)
// 模块：交易终端界面
// 作者：Stardew Capital Team
// 用途：实现新闻标签页，显示市场新闻和生效事件
// ============================================================================

using System;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewCapital.Services;
using StardewCapital.Domain.Market;
using StardewValley;
using StardewValley.Menus;
using StardewModdingAPI;

namespace StardewCapital.UI.Tabs
{
    /// <summary>
    /// 新闻标签页
    /// 
    /// 功能：
    /// 1. 显示今日发生的市场新闻
    /// 2. 显示当前正在生效的新闻事件（影响供需）
    /// </summary>
    public class NewsTab : BaseTradingTab
    {
        private readonly MarketManager _marketManager;
        private readonly ScenarioManager _scenarioManager;
        private readonly ImpactService _impactService;
        // 滚动相关
        private int _scrollAmount = 0;
        private int _maxScroll = 0;
        private int _contentHeight = 0;
        private readonly int _visibleHeight = 300; // 减少为300，为顶部面板留空间
        /// <summary>
        /// 构造函数
        /// </summary>
        public NewsTab(
            IMonitor monitor, 
            int x, int y, int width, int height,
            MarketManager marketManager,
            ScenarioManager scenarioManager,
            ImpactService impactService) 
            : base(monitor, x, y, width, height)
        {
            _marketManager = marketManager;
            _scenarioManager = scenarioManager;
            _impactService = impactService;
        }

        /// <summary>
        /// 处理鼠标滚轮事件
        /// </summary>
        public override void ReceiveScrollWheelAction(int direction)
        {
            if (direction > 0)
                _scrollAmount -= 40;
            else if (direction < 0)
                _scrollAmount += 40;

            ValidateScroll();
        }

        private void ValidateScroll()
        {
            if (_scrollAmount < 0) _scrollAmount = 0;
            if (_scrollAmount > _maxScroll) _scrollAmount = _maxScroll;
        }

        private void DrawScrollBar(SpriteBatch b, int x, int y, int height)
        {
            // 绘制滚动条背景
            IClickableMenu.drawTextureBox(b, Game1.mouseCursors, new Rectangle(403, 373, 9, 9), 
                x, y, 12, height, Color.White, 4f, false);

            if (_maxScroll > 0)
            {
                // 计算滑块高度和位置
                float viewRatio = (float)_visibleHeight / (_contentHeight > 0 ? _contentHeight : _visibleHeight);
                int sliderHeight = (int)(height * viewRatio);
                if (sliderHeight < 20) sliderHeight = 20;

                float scrollRatio = (float)_scrollAmount / _maxScroll;
                int sliderY = y + (int)((height - sliderHeight) * scrollRatio);

                // 绘制滑块
                IClickableMenu.drawTextureBox(b, Game1.mouseCursors, new Rectangle(403, 373, 9, 9), 
                    x, sliderY, 12, sliderHeight, Color.Gold, 4f, false);
            }
        }

        /// <summary>
        /// 绘制新闻标签页
        /// </summary>
        public override void Draw(SpriteBatch b)
        {
            int leftX = XPositionOnScreen + 60;
            int topY = YPositionOnScreen + 180;
            // ========== 绘制固定的市场情绪面板 ==========
            DrawMarketSentimentPanel(b, leftX, topY - 100);
    
            // 为情绪面板腾出空间
            topY += 80;

            // 更新最大滚动值
            if (_contentHeight > _visibleHeight)
                _maxScroll = _contentHeight - _visibleHeight;
            else
                _maxScroll = 0;

            ValidateScroll();

            // 绘制滚动条
            if (_maxScroll > 0)
            {
                DrawScrollBar(b, leftX + 690, topY, _visibleHeight);
            }

            // 设置剪裁区域
            Rectangle originalScissor = b.GraphicsDevice.ScissorRectangle;
            Rectangle clipRect = new Rectangle(leftX - 10, topY, 700, _visibleHeight);
            
            // 确保剪裁区域在屏幕范围内
            clipRect = Rectangle.Intersect(clipRect, b.GraphicsDevice.Viewport.Bounds);

            b.End();
            b.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, null, new RasterizerState { ScissorTestEnable = true });
            b.GraphicsDevice.ScissorRectangle = clipRect;

            int currentY = topY - _scrollAmount;
            int startY = currentY; // 用于计算内容总高度

            // 获取新闻数据
            var newsHistory = _marketManager.GetNewsHistory();
            var activeNews = _marketManager.GetActiveNews();

            // 计算当前绝对日期
            int currentDay = GetAbsoluteDay();

            // 1. ========== 今日新闻 ==========
            Utility.drawTextWithShadow(b, "[!] Today's News", Game1.dialogueFont,
                new Vector2(leftX, currentY), Color.DarkGoldenrod);
            currentY += 40;

            // 绘制分隔线
            b.Draw(Game1.staminaRect, new Rectangle(leftX, currentY, 680, 2), Color.DarkGray);
            currentY += 10;

            // 筛选今日新闻（announcement_day == 当前日期）
            var todayNews = newsHistory.Where(n => n.Day == currentDay).ToList();
            
            if (todayNews.Count > 0)
            {
                foreach (var news in todayNews) // 显示所有今日新闻
                {
                    currentY = DrawNewsItem(b, news, leftX, currentY, currentDay);
                }
            }
            else
            {
                b.DrawString(Game1.smallFont, "No news today.", new Vector2(leftX, currentY), Color.Gray);
                currentY += 30;
            }

            currentY += 20;

            // 2. ========== 当前生效新闻 ==========
            Utility.drawTextWithShadow(b, "[*] Active News Effects", Game1.dialogueFont,
                new Vector2(leftX, currentY), Color.DarkGreen);
            currentY += 40;

            // 绘制分隔线
            b.Draw(Game1.staminaRect, new Rectangle(leftX, currentY, 680, 2), Color.DarkGray);
            currentY += 10;

            if (activeNews.Count > 0)
            {
                foreach (var news in activeNews) // 显示所有生效新闻
                {
                    currentY = DrawNewsItem(b, news, leftX, currentY, currentDay);
                }
            }
            else
            {
                b.DrawString(Game1.smallFont, "No active news effects.", new Vector2(leftX, currentY), Color.Gray);
                currentY += 30; // 确保有高度
            }

            // 计算内容总高度
            _contentHeight = currentY - startY;
            // 增加底部padding
            _contentHeight += 20; 

            b.End();
            b.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, null, null);
            b.GraphicsDevice.ScissorRectangle = originalScissor;
        }

        /// <summary>
        /// 处理点击事件（新闻页暂无交互）
        /// </summary>
        public override bool ReceiveLeftClick(int x, int y)
        {
            return false;
        }

        /// <summary>
        /// 绘制单条新闻
        /// </summary>
        /// <returns>下一个Y坐标</returns>
        private int DrawNewsItem(SpriteBatch b, NewsEvent news, int x, int y, int currentDay)
        {
            // 绘制背景卡片
            var cardRect = new Rectangle(x - 5, y - 5, 670, 70);
            b.Draw(Game1.staminaRect, cardRect, Color.Black * 0.1f);
            
            // 新闻图标（根据严重程度选择颜色）
            Color severityColor = news.Severity switch
            {
                "critical" => Color.DarkRed,
                "high" => Color.OrangeRed,
                "medium" => Color.Orange,
                "low" => Color.Gold,
                _ => Color.Gray
            };

            // 新闻图标
            string icon = GetNewsIcon(news.Type);
            Utility.drawTextWithShadow(b, icon, Game1.dialogueFont,
                new Vector2(x, y), severityColor);

            // 新闻标题和商品
            string affectedItem = news.Scope.AffectedItems.FirstOrDefault() ?? "ALL";
            string titleText = $"{news.Title} ({affectedItem})";
            Utility.drawTextWithShadow(b, titleText, Game1.smallFont,
                new Vector2(x + 40, y + 2), Game1.textColor);
            
            // 严重程度标签（右侧）
            string severityLabel = news.Severity.ToUpper();
            Vector2 severitySize = Game1.tinyFont.MeasureString(severityLabel);
            Utility.drawTextWithShadow(b, severityLabel, Game1.tinyFont,
                new Vector2(x + 640 - severitySize.X, y + 5), severityColor);
            
            y += 28;

            // 影响信息（第二行）
            string impactText = "  ";
            Color impactColor = Color.DarkSlateGray;
            
            if (news.Impact.DemandImpact != 0)
            {
                impactText += $"Demand: {news.Impact.DemandImpact:+0;-0;0}";
                impactColor = news.Impact.DemandImpact > 0 ? Color.DarkGreen : Color.DarkRed;
            }
            
            if (news.Impact.SupplyImpact != 0)
            {
                if (news.Impact.DemandImpact != 0) impactText += "     ";
                impactText += $"Supply: {news.Impact.SupplyImpact:+0;-0;0}";
                if (news.Impact.DemandImpact == 0)
                    impactColor = news.Impact.SupplyImpact > 0 ? Color.DarkGreen : Color.DarkRed;
            }

            b.DrawString(Game1.smallFont, impactText, new Vector2(x + 40, y), impactColor);
            y += 22;

            // 剩余天数（第三行）
            int remainingDays = news.Timing.EffectiveDays[1] - currentDay;
            string expiryText = "";
            Color expiryColor = Color.Gray;
            
            if (remainingDays > 7)
            {
                expiryText = $"  Expires in: {remainingDays} days";
                expiryColor = Color.DarkSlateGray;
            }
            else if (remainingDays > 0)
            {
                expiryText = $"  Expires in: {remainingDays} day{(remainingDays > 1 ? "s" : "")}";
                expiryColor = Color.DarkOrange;
            }
            else if (remainingDays == 0)
            {
                expiryText = "  Expires today!";
                expiryColor = Color.OrangeRed;
            }

            b.DrawString(Game1.tinyFont, expiryText, new Vector2(x + 40, y), expiryColor);
            y += 25;

            return y;
        }

        /// <summary>
        /// 根据新闻类型获取图标（使用颜文字替代 Emoji）
        /// </summary>
        private string GetNewsIcon(NewsType newsType)
        {
            return newsType switch
            {
                NewsType.PestCrisis => "(OoO)",     // 虫害
                NewsType.BumperHarvest => "(*^_^*)", // 丰收
                NewsType.ZuzuCityOrder => "[___]",   // 订单
                NewsType.Drought => "(~_~;)",      // 干旱 (流汗)
                NewsType.Flood => "~~~~",          // 洪水
                NewsType.Festival => "\\(^o^)/",   // 节日
                NewsType.MayorPromotion => "(!!)",   // 公告
                NewsType.StorageSpoilage => "(>_<)", // 腐烂
                _ => "[=]"                           // 默认新闻
            };
        }

        /// <summary>
        /// 计算当前绝对日期
        /// </summary>
        private int GetAbsoluteDay()
        {
            string season = Game1.currentSeason;
            int dayOfMonth = Game1.dayOfMonth;
            
            int seasonIndex = season.ToLower() switch
            {
                "spring" => 0,
                "summer" => 1,
                "fall" => 2,
                "winter" => 3,
                _ => 0
            };
            
            return (seasonIndex * 28) + dayOfMonth;
        }
        /// <summary>
        /// 绘制市场情绪面板（固定在顶部）
        /// </summary>
        private void DrawMarketSentimentPanel(SpriteBatch b, int x, int y)
        {
            int panelWidth = 680;
            int panelHeight = 80;
    
            // 绘制背景卡片
            IClickableMenu.drawTextureBox(b, Game1.mouseCursors, 
                new Rectangle(384, 373, 18, 18),
                x - 10, y, panelWidth + 20, panelHeight, 
                Color.White, 4f, false);
    
            // 半透明深色叠加层
            b.Draw(Game1.staminaRect, 
                new Rectangle(x - 5, y + 5, panelWidth + 10, panelHeight - 10), 
                Color.Black * 0.15f);
    
            // 标题
            Utility.drawTextWithShadow(b, "🎭 Market Sentiment", 
                Game1.dialogueFont,
                new Vector2(x, y + 8), 
                Color.Gold);
    
            // 获取当前剧本和冲击值
            var scenario = _scenarioManager.GetCurrentScenario();
            var scenarioParams = _scenarioManager.GetCurrentParameters();
    
            // 获取示例商品的冲击值（防风草 itemId=24）
            double impact = _impactService.GetCurrentImpact("24");
    
            // 剧本名称（左侧）
            string scenarioName = GetScenarioChineseName(scenario);
            Color scenarioColor = GetScenarioColor(scenario);
    
            string scenarioText = $"当前剧本: {scenarioName}";
            Utility.drawTextWithShadow(b, scenarioText, 
                Game1.smallFont,
                new Vector2(x + 10, y + 35), 
                scenarioColor);
    
            // 剧本描述（左侧第二行）
            b.DrawString(Game1.tinyFont, scenarioParams.Description, 
                new Vector2(x + 10, y + 58), 
                Color.Gray);
    
            // 冲击值（右侧）
            string impactText = impact >= 0 
                ? $"Impact: +{impact:F2}g ↑" 
                : $"Impact: {impact:F2}g ↓";
    
            Color impactColor = impact > 0 
                ? Color.LimeGreen 
                : (impact < 0 ? Color.OrangeRed : Color.Gray);
    
            Vector2 impactSize = Game1.dialogueFont.MeasureString(impactText);
            Utility.drawTextWithShadow(b, impactText, 
                Game1.dialogueFont,
                new Vector2(x + panelWidth - impactSize.X - 10, y + 35), 
                impactColor);
        }
        /// <summary>
        /// 获取剧本中文名称
        /// </summary>
        private string GetScenarioChineseName(ScenarioType scenario)
        {
            return scenario switch
            {
                ScenarioType.DeadMarket => "死水一潭",
                ScenarioType.IrrationalExuberance => "非理性繁荣",
                ScenarioType.PanicSelling => "恐慌踩踏",
                ScenarioType.ShortSqueeze => "轧空风暴",
                _ => "未知"
            };
        }

        /// <summary>
        /// 获取剧本对应的颜色
        /// </summary>
        private Color GetScenarioColor(ScenarioType scenario)
        {
            return scenario switch
            {
                ScenarioType.DeadMarket => Color.SteelBlue,
                ScenarioType.IrrationalExuberance => Color.OrangeRed,
                ScenarioType.PanicSelling => Color.DarkRed,
                ScenarioType.ShortSqueeze => Color.Purple,
                _ => Color.White
            };
        }
    }
}