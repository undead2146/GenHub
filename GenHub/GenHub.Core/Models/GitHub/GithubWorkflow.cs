using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace GenHub.Core.Models
{
    /// <summary>
    /// Represents a GitHub Actions workflow run.
    /// </summary>
    public class GitHubWorkflow
    {
        /// <summary>
        /// Unique ID of the workflow run.
        /// </summary>
        public long RunId { get; set; }

        /// <summary>
        /// ID of the workflow definition.
        /// </summary>
        public long WorkflowId { get; set; }

        /// <summary>
        /// The display name of the workflow definition (e.g., "Build and Test", "Release").
        /// </summary>
        public string Name { get; set; } = string.Empty; // Name of the workflow definition

        /// <summary>
        /// Path to the workflow definition file (e.g., .github/workflows/ci.yml).
        /// </summary>
        public string WorkflowPath { get; set; } = string.Empty;

        /// <summary>
        /// Run number of this specific workflow execution.
        /// </summary>
        public int WorkflowNumber { get; set; }

        /// <summary>
        /// When the workflow run was created.
        /// </summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// The commit SHA that triggered the workflow.
        /// </summary>
        public string? CommitSha { get; set; }

        /// <summary>
        /// Commit message that triggered the workflow.
        /// </summary>
        public string? CommitMessage { get; set; }

        /// <summary>
        /// Event type that triggered the workflow (e.g., push, pull_request).
        /// </summary>
        public string EventType { get; set; } = string.Empty;

        /// <summary>
        /// Pull request number if the workflow was triggered by a pull request.
        /// </summary>
        public int? PullRequestNumber { get; set; }

        /// <summary>
        /// Pull request title if available.
        /// </summary>
        public string? PullRequestTitle { get; set; }

        /// <summary>
        /// Status of the workflow run (e.g., "queued", "in_progress", "completed").
        /// </summary>
        public string? Status { get; set; }

        /// <summary>
        /// Conclusion of the workflow run if completed (e.g., "success", "failure", "cancelled").
        /// </summary>
        public string? Conclusion { get; set; }

        /// <summary>
        /// The head branch of the workflow run.
        /// </summary>
        public string? HeadBranch { get; set; }

        /// <summary>
        /// Artifacts produced by this workflow run.
        /// This list should be populated by the GitHubWorkflowReader.
        /// </summary>
        public List<GitHubArtifact> Artifacts { get; set; } = new List<GitHubArtifact>();

        /// <summary>
        /// Count of artifacts associated with this workflow run. 
        /// Can be set from API summary before fetching all artifacts.
        /// </summary>
        public int ArtifactCount { get; set; }


        /// <summary>
        /// Repository information this workflow run belongs to.
        /// This should be populated by a service when the workflow is fetched.
        /// </summary>
        [JsonIgnore]
        public GitHubRepoSettings? RepositoryInfo { get; set; }


        // Convenience Accessors
        [JsonIgnore]
        public string ShortCommitSha => !string.IsNullOrEmpty(CommitSha) && CommitSha.Length > 7
            ? CommitSha.Substring(0, 7)
            : CommitSha ?? string.Empty;

        [JsonIgnore]
        public string ShortName => Name.Contains("/") ? Name.Split("/")[^1] : Name; // Short name of the workflow definition

        [JsonIgnore]
        public bool HasPullRequestInfo => PullRequestNumber.HasValue;

        [JsonIgnore]
        public string DisplayName
        {
            get
            {
                if (HasPullRequestInfo && !string.IsNullOrEmpty(PullRequestTitle))
                    return $"PR #{PullRequestNumber}: {PullRequestTitle}";
                if (!string.IsNullOrEmpty(CommitMessage))
                    return $"Run #{WorkflowNumber}: {CommitMessageShort}";
                return $"Run #{WorkflowNumber} - {Name}"; // Fallback if no PR title and no commit message
            }
        }

        [JsonIgnore]
        public string CommitMessageShort =>
            !string.IsNullOrEmpty(CommitMessage) && CommitMessage.Length > 70
            ? CommitMessage.Substring(0, 67) + "..."
            : CommitMessage ?? string.Empty;

        public override string ToString()
        {
            return $"{Name} Run #{WorkflowNumber} (ID: {RunId})";
        }
    }
}
