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
        
        /// <summary>
        /// The maximum number of requests allowed per hour
        /// </summary>
        public int Limit { get; set; }

        /// <summary>
        /// The number of requests remaining in the current rate limit window
        /// </summary>
        public int Remaining { get; set; }

        /// <summary>
        /// The time at which the current rate limit window resets
        /// </summary>
        public DateTimeOffset Reset { get; set; }

        /// <summary>
        /// The number of seconds until the rate limit window resets
        /// </summary>
        public int ResetSeconds { get; set; }

        /// <summary>
        /// The type of rate limit (e.g., "core", "search", "graphql")
        /// </summary>
        public string? ResourceType { get; set; }

        /// <summary>
        /// Gets a value indicating whether the rate limit has been exceeded
        /// </summary>
        public bool IsExceeded => Remaining <= 0;

        /// <summary>
        /// Gets the time remaining until the rate limit resets
        /// </summary>
        public TimeSpan TimeUntilReset => Reset - DateTimeOffset.UtcNow;
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
