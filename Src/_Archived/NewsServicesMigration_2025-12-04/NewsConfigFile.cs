using System.Collections.Generic;
using StardewCapital.Core.Futures.Data;
using System.Text.Json.Serialization;
using StardewCapital.Core.Futures.Data;

namespace StardewCapital.Services.News
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

