using System;

namespace GenHub.Core.Models
{
    /// <summary>
    /// Configuration options for GitHub repository discovery
    /// </summary>
    public class DiscoveryOptions
    {
        /// <summary>
        /// Whether to include forks in discovery
        /// </summary>
        public bool IncludeForks { get; set; } = true;

        /// <summary>
        /// Whether to include search results
        /// </summary>
        public bool IncludeSearch { get; set; } = true;

        /// <summary>
        /// Maximum depth for fork discovery
        /// </summary>
        public int MaxForkDepth { get; set; } = 2;

        /// <summary>
        /// Maximum number of forks to process per repository
        /// </summary>
        public int MaxForksPerRepository { get; set; } = 20;

        /// <summary>
        /// Maximum number of forks to evaluate per repository (to prevent excessive API calls)
        /// </summary>
        public int MaxForksToEvaluate { get; set; } = 100; // Increased from 50

        /// <summary>
        /// Maximum number of search results to process
        /// </summary>
        public int MaxSearchResults { get; set; } = 50; // Increased for wider search

        /// <summary>
        /// Maximum number of search queries to execute for broader discovery
        /// </summary>
        public int MaxSearchQueries { get; set; } = 7;

        /// <summary>
        /// Timeout for individual API requests in seconds
        /// </summary>
        public int RequestTimeoutSeconds { get; set; } = 30;

        /// <summary>
        /// Rate limiting delay between requests in milliseconds
        /// </summary>
        public int RateLimitDelayMs { get; set; } = 1000;

        /// <summary>
        /// Whether to prioritize repositories based on recent activity, stars, and forks.
        /// </summary>
        public bool PrioritizeByActivity { get; set; } = true;

        /// <summary>
        /// The minimum number of stars a repository from a search result must have to be included.
        /// </summary>
        public int MinimumStarsForSearchResults { get; set; } = 5;

        /// <summary>
        /// How many months of inactivity to tolerate before a repository is considered dormant.
        /// </summary>
        public int MaxMonthsSinceLastPush { get; set; } = 12;
        
        /// <summary>
        /// If true, only repositories with recent GitHub Actions workflows or Releases will be included.
        /// </summary>
        public bool RequireActionableContent { get; set; } = true;

        /// <summary>
        /// Minimum activity score required for a repository to be considered active
        /// </summary>
        public int MinimumActivityScore { get; set; } = 10; // Reduced from 100

        /// <summary>
        /// Maximum number of results to return from discovery
        /// </summary>
        public int MaxResultsToReturn { get; set; } = 50; // Increased from 20
    }
}
