using System;
using System.Text.Json.Serialization;

namespace GenHub.Core.Models.GitHub
{
    /// <summary>
    /// Represents GitHub API rate limit information
    /// </summary>
    public class RateLimitInfo
    {
        /// <summary>
        /// The maximum number of requests permitted per hour
        /// </summary>
        [JsonPropertyName("limit")]
        public int Limit { get; set; }

        /// <summary>
        /// The number of requests remaining in the current rate limit window
        /// </summary>
        [JsonPropertyName("remaining")]
        public int Remaining { get; set; }

        /// <summary>
        /// The time at which the current rate limit window resets in UTC epoch seconds
        /// </summary>
        [JsonPropertyName("reset")]
        public long Reset { get; set; }

        /// <summary>
        /// The number of requests used in the current rate limit window
        /// </summary>
        [JsonPropertyName("used")]
        public int Used { get; set; }

        /// <summary>
        /// Gets the reset time as a DateTime
        /// </summary>
        [JsonIgnore]
        public DateTime ResetTime => DateTimeOffset.FromUnixTimeSeconds(Reset).DateTime;

        /// <summary>
        /// Gets the percentage of rate limit used
        /// </summary>
        [JsonIgnore]
        public double UsagePercentage => Limit > 0 ? (Used / (double)Limit) * 100 : 0;

        /// <summary>
        /// Gets whether the rate limit is close to being exceeded (90%+)
        /// </summary>
        [JsonIgnore]
        public bool IsNearLimit => UsagePercentage >= 90;

        /// <summary>
        /// Gets whether the rate limit has been exceeded
        /// </summary>
        [JsonIgnore]
        public bool IsExceeded => Remaining <= 0;

        /// <summary>
        /// Gets the time remaining until rate limit reset
        /// </summary>
        [JsonIgnore]
        public TimeSpan TimeUntilReset => ResetTime - DateTime.UtcNow;
    }
}
