using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace StardewCapital.Core.Futures.Data
{
    /// <summary>
    /// 新闻配置文件结构
    /// </summary>
    public class NewsConfigFile
    {
        [JsonPropertyName("news_items")]
        public List<NewsTemplate> NewsTemplates { get; set; } = new();
        
        [JsonPropertyName("metadata")]
        public Dictionary<string, string> Metadata { get; set; } = new();
    }
}
