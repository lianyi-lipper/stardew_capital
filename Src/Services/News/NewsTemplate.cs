using System.Text.Json.Serialization;
using StardewCapital.Domain.Market;

namespace StardewCapital.Services.News
{
    /// <summary>
    /// 新闻模板（从JSON加载）
    /// </summary>
    public class NewsTemplate
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;
        
        [JsonPropertyName("title")]
        public string Title { get; set; } = string.Empty;
        
        [JsonPropertyName("description")]
        public string Description { get; set; } = string.Empty;
        
        [JsonPropertyName("severity")]
        public string Severity { get; set; } = string.Empty;
        
        [JsonPropertyName("news_type")]
        public string NewsTypeString { get; set; } = string.Empty;
        
        [JsonPropertyName("impact")]
        public NewsImpact Impact { get; set; } = new();
        
        [JsonPropertyName("scope")]
        public NewsScope Scope { get; set; } = new();
        
        [JsonPropertyName("timing")]
        public NewsTiming Timing { get; set; } = new();
        
        [JsonPropertyName("conditions")]
        public NewsConditions Conditions { get; set; } = new();
    }
}
