using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace GenHub.Core.Models
{
    /// <summary>
    /// Represents a GitHub workflow run following MVVM patterns
    /// </summary>
    public class GitHubWorkflow
    {
        /// <summary>
        /// The unique identifier for the workflow run
        /// </summary>
        public long RunId { get; set; }

        /// <summary>
        /// The unique identifier for the workflow
        /// </summary>
        public long WorkflowId { get; set; }

        /// <summary>
        /// The workflow run number
        /// </summary>
        public int WorkflowNumber { get; set; }

        /// <summary>
        /// Gets or sets the name of the workflow
        /// </summary>
        public string WorkflowName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the display name for the workflow (alias for WorkflowName for backward compatibility)
        /// </summary>
        [JsonIgnore]
        public string Name 
        { 
            get => WorkflowName; 
            set => WorkflowName = value; 
        }

        /// <summary>
        /// The display title of the workflow run
        /// </summary>
        public string DisplayTitle { get; set; } = string.Empty;

        /// <summary>
        /// The status of the workflow run (queued, in_progress, completed)
        /// </summary>
        public string Status { get; set; } = string.Empty;

        /// <summary>
        /// The conclusion of the workflow run (success, failure, cancelled, etc.)
        /// </summary>
        public string? Conclusion { get; set; }

        /// <summary>
        /// The event that triggered the workflow run
        /// </summary>
        public string EventType { get; set; } = string.Empty;

        /// <summary>
        /// When the workflow run was created
        /// </summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// When the workflow run was last updated
        /// </summary>
        public DateTime UpdatedAt { get; set; }

        /// <summary>
        /// The SHA of the commit that triggered the workflow
        /// </summary>
        public string CommitSha { get; set; } = string.Empty;

        /// <summary>
        /// The commit message
        /// </summary>
        public string CommitMessage { get; set; } = string.Empty;

        /// <summary>
        /// Pull request number if the workflow was triggered by a PR
        /// </summary>
        public int? PullRequestNumber { get; set; }

        /// <summary>
        /// Pull request title if the workflow was triggered by a PR
        /// </summary>
        public string? PullRequestTitle { get; set; }

        /// <summary>
        /// Repository information
        /// </summary>
        public GitHubRepository? RepositoryInfo { get; set; }

        /// <summary>
        /// The URL to view the workflow run on GitHub
        /// </summary>
        public string? HtmlUrl { get; set; }

        /// <summary>
        /// The branch or tag that triggered the workflow
        /// </summary>
        public string? HeadBranch { get; set; }

        /// <summary>
        /// The name of the actor who triggered the workflow
        /// </summary>
        public string? Actor { get; set; }

        /// <summary>
        /// The workflow file path
        /// </summary>
        public string? WorkflowPath { get; set; }

        /// <summary>
        /// Additional metadata for the workflow
        /// </summary>
        public Dictionary<string, object>? Metadata { get; set; }

        /// <summary>
        /// Whether this workflow run has artifacts
        /// </summary>
        public bool HasArtifacts { get; set; }

        /// <summary>
        /// Number of artifacts for this workflow run
        /// </summary>
        public int ArtifactCount { get; set; }

        /// <summary>
        /// Collection of artifacts associated with this workflow run
        /// </summary>
        public List<GitHubArtifact>? Artifacts { get; set; }

        /// <summary>
        /// Helper property to get a formatted display name
        /// </summary>
        [JsonIgnore]
        public string FormattedDisplayName => !string.IsNullOrEmpty(DisplayTitle) ? DisplayTitle : Name;

        /// <summary>
        /// Helper property to check if the workflow run is completed
        /// </summary>
        [JsonIgnore]
        public bool IsCompleted => Status?.Equals("completed", StringComparison.OrdinalIgnoreCase) == true;

        /// <summary>
        /// Helper property to check if the workflow run was successful
        /// </summary>
        [JsonIgnore]
        public bool IsSuccessful => IsCompleted && Conclusion?.Equals("success", StringComparison.OrdinalIgnoreCase) == true;

        /// <summary>
        /// Helper property to get a short commit SHA
        /// </summary>
        [JsonIgnore]
        public string ShortCommitSha => CommitSha?.Length > 7 ? CommitSha.Substring(0, 7) : CommitSha ?? string.Empty;

        /// <summary>
        /// Helper property to get formatted creation time
        /// </summary>
        [JsonIgnore]
        public string FormattedCreatedAt => CreatedAt.ToString("yyyy-MM-dd HH:mm:ss");

        public GitHubWorkflow()
        {
            Metadata = new Dictionary<string, object>();
            Artifacts = new List<GitHubArtifact>();
        }
    }
}
