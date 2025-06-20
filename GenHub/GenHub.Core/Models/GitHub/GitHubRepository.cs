using System;
using System.Text.Json.Serialization;

namespace GenHub.Core.Models
{
    /// <summary>
    /// Configuration settings for a GitHub repository
    /// </summary>
    public class GitHubRepository
    {
        /// <summary>
        /// Repository owner/organization name
        /// </summary>
        [JsonPropertyName("repoOwner")]
        public string RepoOwner { get; set; } = string.Empty;

        /// <summary>
        /// Repository name
        /// </summary>
        [JsonPropertyName("repoName")]
        public string RepoName { get; set; } = string.Empty;

        /// <summary>
        /// Display name for the repository
        /// </summary>
        [JsonPropertyName("displayName")]
        public string DisplayName { get; set; } = string.Empty;

        /// <summary>
        /// GitHub access token for this repository
        /// </summary>
        [JsonPropertyName("token")]
        public string? Token { get; set; }

        /// <summary>
        /// Default workflow file to monitor
        /// </summary>
        [JsonPropertyName("workflowFile")]
        public string? WorkflowFile { get; set; }

        /// <summary>
        /// Repository ID from GitHub API
        /// </summary>
        [JsonPropertyName("id")]
        public long? Id { get; set; }

        /// <summary>
        /// Repository description
        /// </summary>
        [JsonPropertyName("description")]
        public string? Description { get; set; }

        /// <summary>
        /// Whether the repository is private
        /// </summary>
        [JsonPropertyName("private")]
        public bool IsPrivate { get; set; }

        /// <summary>
        /// Whether the repository is a fork
        /// </summary>
        [JsonPropertyName("fork")]
        public bool IsFork { get; set; }

        /// <summary>
        /// Repository clone URL
        /// </summary>
        [JsonPropertyName("cloneUrl")]
        public string? CloneUrl { get; set; }

        /// <summary>
        /// Repository creation date
        /// </summary>
        [JsonPropertyName("createdAt")]
        public DateTime? CreatedAt { get; set; }

        /// <summary>
        /// Repository last update date
        /// </summary>
        [JsonPropertyName("updatedAt")]
        public DateTime? UpdatedAt { get; set; }

        /// <summary>
        /// Primary programming language
        /// </summary>
        [JsonPropertyName("language")]
        public string? Language { get; set; }

        /// <summary>
        /// Number of stars
        /// </summary>
        [JsonPropertyName("stargazersCount")]
        public int StargazersCount { get; set; }

        /// <summary>
        /// Number of forks
        /// </summary>
        [JsonPropertyName("forksCount")]
        public int ForksCount { get; set; }

        /// <summary>
        /// Default branch to monitor
        /// </summary>
        [JsonPropertyName("branch")]
        public string? Branch { get; set; } = "main";

        /// <summary>
        /// Whether this repository is enabled
        /// </summary>
        [JsonPropertyName("enabled")]
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// Last time this repository was accessed
        /// </summary>
        [JsonPropertyName("lastAccessed")]
        public DateTime? LastAccessed { get; set; }
        /// <summary>
        /// Repository last push date
        /// </summary>
        [JsonPropertyName("pushedAt")]
        public DateTime? PushedAt { get; set; }

        /// <summary>
        /// Repository topics from GitHub
        /// </summary>
        [JsonPropertyName("topics")]
        public string[]? Topics { get; set; }

        /// <summary>
        /// Repository size in kilobytes 
        /// </summary>
        [JsonPropertyName("size")]
        public long Size { get; set; }

        /// <summary>
        /// Number of watchers
        /// </summary>
        [JsonPropertyName("watchersCount")]
        public int WatchersCount { get; set; }

        /// <summary>
        /// Number of open issues
        /// </summary>
        [JsonPropertyName("openIssuesCount")]
        public int OpenIssuesCount { get; set; }

        /// <summary>
        /// Whether the repository has issues enabled
        /// </summary>
        [JsonPropertyName("hasIssues")]
        public bool HasIssues { get; set; }

        /// <summary>
        /// Whether the repository has projects enabled
        /// </summary>
        [JsonPropertyName("hasProjects")]
        public bool HasProjects { get; set; }

        /// <summary>
        /// Whether the repository has wiki enabled
        /// </summary>
        [JsonPropertyName("hasWiki")]
        public bool HasWiki { get; set; }

        /// <summary>
        /// Repository license information
        /// </summary>
        [JsonPropertyName("license")]
        public string? License { get; set; }

        /// <summary>
        /// Default branch name
        /// </summary>
        [JsonPropertyName("defaultBranch")]
        public string? DefaultBranch { get; set; }

        /// <summary>
        /// Whether the repository is archived
        /// </summary>
        [JsonPropertyName("archived")]
        public bool IsArchived { get; set; }

        /// <summary>
        /// Whether the repository is disabled
        /// </summary>
        [JsonPropertyName("disabled")]
        public bool IsDisabled { get; set; }

        /// <summary>
        /// Gets the full repository name in owner/name format
        /// </summary>
        [JsonIgnore]
        public string FullName => IsValid ? $"{RepoOwner}/{RepoName}" : "Invalid Repository";

        /// <summary>
        /// Gets the repository URL
        /// </summary>
        [JsonIgnore]
        public string Url => IsValid ? $"https://github.com/{RepoOwner}/{RepoName}" : string.Empty;

        /// <summary>
        /// Gets the API base URL for this repository
        /// </summary>
        [JsonIgnore]
        public string ApiUrl => IsValid ? $"https://api.github.com/repos/{RepoOwner}/{RepoName}" : string.Empty;

        /// <summary>
        /// Validates if the repository settings are valid
        /// </summary>
        [JsonIgnore]
        public bool IsValid => !string.IsNullOrWhiteSpace(RepoOwner) && 
                               !string.IsNullOrWhiteSpace(RepoName) &&
                               RepoOwner.Trim().Length > 0 &&
                               RepoName.Trim().Length > 0;

        /// <summary>
        /// Gets the computed display name with fallback
        /// </summary>
        [JsonIgnore]
        public string ComputedDisplayName
        {
            get
            {
                // Use explicit DisplayName if provided and valid
                if (!string.IsNullOrWhiteSpace(DisplayName))
                    return DisplayName.Trim();
                
                // Use FullName if repository is valid
                if (IsValid)
                    return FullName;
                
                // Fallback for invalid repositories
                return "Invalid Repository";
            }
        }



        /// <summary>
        /// Constructor for JSON deserialization
        /// </summary>
        public GitHubRepository()
        {
        }

        /// <summary>
        /// Constructor for creating valid repositories
        /// </summary>
        public GitHubRepository(string repoOwner, string repoName, string? displayName = null)
        {
            RepoOwner = repoOwner?.Trim() ?? throw new ArgumentNullException(nameof(repoOwner));
            RepoName = repoName?.Trim() ?? throw new ArgumentNullException(nameof(repoName));
            DisplayName = displayName?.Trim() ?? $"{RepoOwner}/{RepoName}";
            
            if (!IsValid)
                throw new ArgumentException("Repository settings are not valid");
        }

        /// <summary>
        /// Returns the display name for UI binding
        /// </summary>
        public override string ToString()
        {
            return ComputedDisplayName;
        }

        /// <summary>
        /// Equality comparison based on owner and name
        /// </summary>
        public override bool Equals(object? obj)
        {
            if (obj is GitHubRepository other)
            {
                return string.Equals(RepoOwner?.Trim(), other.RepoOwner?.Trim(), StringComparison.OrdinalIgnoreCase) &&
                       string.Equals(RepoName?.Trim(), other.RepoName?.Trim(), StringComparison.OrdinalIgnoreCase);
            }
            return false;
        }

        /// <summary>
        /// Hash code based on owner and name
        /// </summary>
        public override int GetHashCode()
        {
            return HashCode.Combine(
                RepoOwner?.Trim()?.ToLowerInvariant() ?? string.Empty, 
                RepoName?.Trim()?.ToLowerInvariant() ?? string.Empty);
        }

        /// <summary>
        /// Creates a copy of the repository settings
        /// </summary>
        public GitHubRepository Clone()
        {
            return new GitHubRepository
            {
                RepoOwner = RepoOwner,
                RepoName = RepoName,
                DisplayName = DisplayName,
                Token = Token,
                WorkflowFile = WorkflowFile,
                Branch = Branch,
                Enabled = Enabled,
                LastAccessed = LastAccessed
            };
        }

        /// <summary>
        /// Validates and fixes common issues
        /// </summary>
        public void Normalize()
        {
            RepoOwner = RepoOwner?.Trim() ?? string.Empty;
            RepoName = RepoName?.Trim() ?? string.Empty;
            DisplayName = DisplayName?.Trim() ?? string.Empty;
            Branch = Branch?.Trim() ?? "main";
            
            // Auto-generate DisplayName if empty but repository is valid
            if (string.IsNullOrWhiteSpace(DisplayName) && IsValid)
            {
                DisplayName = FullName;
            }
        }

        /// <summary>
        /// Gets whether this repository has substantial content (indicates real development)
        /// </summary>
        [JsonIgnore]
        public bool HasSubstantialContent => Size > 500; // More than 500KB indicates real content

        /// <summary>
        /// Gets whether this repository shows signs of active development
        /// </summary>
        [JsonIgnore]
        public bool HasDevelopmentSigns => 
            HasSubstantialContent || 
            OpenIssuesCount > 0 || 
            (Language == "C++" && Size > 100) ||
            (Description?.Contains("game", StringComparison.OrdinalIgnoreCase) == true && Size > 50);

        /// <summary>
        /// Gets whether this repository is likely a real project vs empty fork
        /// </summary>
        [JsonIgnore]
        public bool IsLikelyRealProject => 
            Size > 1000 || // More than 1MB
            StargazersCount > 5 ||
            ForksCount > 2 ||
            OpenIssuesCount > 0 ||
            (HasRecentActivity && Size > 100);

        /// <summary>
        /// Gets whether this repository has recent activity
        /// </summary>
        [JsonIgnore]
        public bool HasRecentActivity => 
            PushedAt.HasValue && PushedAt.Value > DateTime.UtcNow.AddYears(-2);
    }
}
