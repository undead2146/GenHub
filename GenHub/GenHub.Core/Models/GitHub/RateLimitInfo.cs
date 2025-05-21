using System;
using System.Text.Json.Serialization;

namespace GenHub.Core.Models.GitHub
{
    /// <summary>
    /// Information about GitHub API rate limits
    /// </summary>
    public class RateLimitInfo
    {
        [JsonPropertyName("resources")]
        public RateLimitResources? Resources { get; set; }
        
        [JsonPropertyName("rate")]
        public RateLimit? Rate { get; set; }
    }
    
    public class RateLimitResources
    {
        [JsonPropertyName("core")]
        public RateLimit? Core { get; set; }
        
        [JsonPropertyName("search")]
        public RateLimit? Search { get; set; }
        
        [JsonPropertyName("graphql")]
        public RateLimit? GraphQL { get; set; }
    }
    
    public class RateLimit
    {
        [JsonPropertyName("limit")]
        public int Limit { get; set; }
        
        [JsonPropertyName("remaining")]
        public int Remaining { get; set; }
        
        [JsonPropertyName("reset")]
        public long Reset { get; set; }
        
        [JsonPropertyName("used")]
        public int Used { get; set; }
        
        /// <summary>
        /// Gets the reset time as a DateTime
        /// </summary>
        [JsonIgnore]
        public DateTime ResetTime => DateTimeOffset.FromUnixTimeSeconds(Reset).DateTime;
    }
}
