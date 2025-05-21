using System;
using System.Text.Json.Serialization;

namespace GenHub.Core.Models
{
    /// <summary>
    /// Represents a build artifact from GitHub.
    /// This model should closely mirror the data structure provided by the GitHub API for an artifact,
    /// plus any locally derived information like BuildInfo or RepositoryInfo.
    /// </summary>
    public class GitHubArtifact
    {
        /// <summary>
        /// Gets or sets the artifact ID.
        /// </summary>
        public long Id { get; set; }

        /// <summary>
        /// Gets or sets the artifact name.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the workflow ID that produced this artifact.
        /// </summary>
        public long WorkflowId { get; set; } // ID of the workflow definition

        /// <summary>
        /// Gets or sets the run ID of the workflow that produced this artifact.
        /// </summary>
        public long RunId { get; set; }

        /// <summary>
        /// Gets or sets the workflow run number.
        /// </summary>
        public int WorkflowNumber { get; set; }

        /// <summary>
        /// Gets or sets the size of the artifact in bytes.
        /// </summary>
        public long SizeInBytes { get; set; }
        
        /// <summary>
        /// Flag indicating if this is a release asset (not a workflow artifact)
        /// </summary>
        public bool IsRelease { get; set; }

        /// <summary>
        /// Direct download URL for release assets
        /// </summary>
        public string DownloadUrl { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the URL to download the artifact archive.
        /// </summary>
        public string? ArchiveDownloadUrl { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the artifact has expired.
        /// </summary>
        public bool Expired { get; set; }

        /// <summary>
        /// Gets or sets the creation time of the artifact.
        /// </summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// Gets or sets the expiration time of the artifact.
        /// </summary>
        public DateTime? ExpiresAt { get; set; }

        // Contextual information often included with artifact data by GitHub API
        /// <summary>
        /// Gets or sets the pull request number associated with this artifact's workflow run.
        /// </summary>
        public int? PullRequestNumber { get; set; }

        /// <summary>
        /// Gets or sets the pull request title.
        /// </summary>
        public string? PullRequestTitle { get; set; }

        /// <summary>
        /// Gets or sets the commit SHA that triggered the workflow run.
        /// </summary>
        public string? CommitSha { get; set; }

        /// <summary>
        /// Gets or sets the commit message.
        /// </summary>
        public string? CommitMessage { get; set; }
        
        /// <summary>
        /// Gets the event type that triggered the workflow (e.g., pull_request, push).
        /// </summary>
        public string? EventType { get; set; }

        /// <summary>
        /// Gets or sets the build preset parsed or identified for this artifact (e.g., Debug, Release).
        /// This might be derived from BuildInfo.Configuration or set directly.
        /// </summary>
        public string? BuildPreset { get; set; }


        // Locally populated or UI-related properties
        /// <summary>
        /// Gets or sets the parsed build information from the artifact name or metadata.
        /// This should be populated by a service (e.g., GitHubArtifactReader).
        /// </summary>
        [JsonIgnore]
        public GitHubBuild? BuildInfo { get; set; }

        /// <summary>
        /// Gets or sets the repository information this artifact belongs to.
        /// This should be populated by a service when the artifact is fetched.
        /// </summary>
        [JsonIgnore]
        public GitHubRepoSettings? RepositoryInfo { get; set; }
        
        [JsonIgnore]
        public bool IsActive { get; set; } // UI state or similar

        [JsonIgnore]
        public bool IsInstalled { get; set; } // UI state or similar

        [JsonIgnore]
        public bool IsInstalling { get; set; } // UI state or similar


        // Convenience Accessors
        [JsonIgnore]
        public string ShortCommitSha => !string.IsNullOrEmpty(CommitSha) 
            ? CommitSha.Substring(0, Math.Min(7, CommitSha.Length)) 
            : string.Empty;

        [JsonIgnore]
        public bool HasBuildInfo => BuildInfo != null;
        
        public string GetDisplayName()
        {
            if (PullRequestNumber.HasValue && !string.IsNullOrWhiteSpace(PullRequestTitle))
                return $"PR #{PullRequestNumber}: {PullRequestTitle} ({Name})";
            
            if (WorkflowNumber > 0)
                return $"Build #{WorkflowNumber} ({Name})";
                
            return Name;
        }
    }
}
