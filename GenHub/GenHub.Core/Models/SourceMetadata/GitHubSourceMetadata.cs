using System;
using System.Text.Json.Serialization;
using GenHub.Core.Models.GitHub;

namespace GenHub.Core.Models.SourceMetadata
{
    /// <summary>
    /// Holds GitHub-specific metadata for a GameVersion or GameProfile.
    /// This class primarily references the specific artifact and adds essential workflow context.
    /// </summary>
    public class GitHubSourceMetadata : BaseSourceMetadata
    {
        /// <summary>
        /// The specific GitHub artifact that this game version/profile is derived from.
        /// This object contains most of the granular details like commit, PR info (if related to the artifact's context),
        /// build info, download URLs, etc.
        /// This property will be serialized.
        /// </summary>
        public GitHubArtifact? AssociatedArtifact { get; set; }

        // --- Context from the GitHub Workflow Run that produced the AssociatedArtifact ---
        // These properties provide broader context about the workflow run itself,
        // which might not be directly on the artifact or might be more about the run's trigger/definition.
        // These will be serialized.

        /// <summary>
        /// The display name of the workflow definition (e.g., "Build and Release").
        /// (Corresponds to GitHubWorkflow.Name)
        /// </summary>
        public string? WorkflowDefinitionName { get; set; }

        /// <summary>
        /// The path to the workflow definition file (e.g., ".github/workflows/main.yml").
        /// (Corresponds to GitHubWorkflow.WorkflowPath)
        /// </summary>
        public string? WorkflowDefinitionPath { get; set; }

        /// <summary>
        /// The overall status of the workflow run (e.g., "completed", "in_progress").
        /// (Corresponds to GitHubWorkflow.Status)
        /// </summary>
        public string? WorkflowRunStatus { get; set; }

        /// <summary>
        /// The conclusion of the workflow run if completed (e.g., "success", "failure").
        /// (Corresponds to GitHubWorkflow.Conclusion)
        /// </summary>
        public string? WorkflowRunConclusion { get; set; }

        /// <summary>
        /// The head branch that triggered the workflow run.
        /// (Corresponds to GitHubWorkflow.HeadBranch)
        /// </summary>
        public string? SourceControlBranch { get; set; }
        public long? AssociatedReleaseId { get; set; }
        public string? ReleaseTag { get; set; }
        public string? ReleaseName { get; set; }
        public long? AssociatedReleaseAssetId { get; set; }
        public string? ReleaseAssetName { get; set; }

        public object? AssociatedReleaseAsset { get; set; }
        public string? ReleaseTagName { get; set; }
        public bool? ReleaseIsDraft { get; set; }
        public bool? ReleaseIsPrerelease { get; set; }
        public DateTimeOffset? ReleasePublishedAt { get; set; }
        public GitHubRepoSettings? RepositoryInfo { get; set; }


        // --- Convenience Accessors (derived from AssociatedArtifact or the properties above) ---
        // These are for ease of use in code and are NOT serialized to avoid redundancy.

        [JsonIgnore]
        public GitHubBuild? BuildInfo { get; set; } = null;

        [JsonIgnore]
        public long? ArtifactId 
        { 
            get => AssociatedArtifact?.Id; 
            set 
            {
                if (AssociatedArtifact == null && value.HasValue)
                    AssociatedArtifact = new GitHubArtifact { Id = value.Value };
                else if (AssociatedArtifact != null)
                    AssociatedArtifact.Id = value ?? 0;
            }
        }

        [JsonIgnore]
        public int? PullRequestNumber 
        { 
            get => AssociatedArtifact?.PullRequestNumber; 
            set 
            {
                if (AssociatedArtifact == null)
                    AssociatedArtifact = new GitHubArtifact();
                AssociatedArtifact.PullRequestNumber = value;
            }
        }

        [JsonIgnore]
        public string? PullRequestTitle 
        { 
            get => AssociatedArtifact?.PullRequestTitle; 
            set 
            {
                if (AssociatedArtifact == null)
                    AssociatedArtifact = new GitHubArtifact();
                AssociatedArtifact.PullRequestTitle = value;
            }
        }

        [JsonIgnore]
        public string? CommitSha 
        { 
            get => AssociatedArtifact?.CommitSha; 
            set 
            {
                if (AssociatedArtifact == null)
                    AssociatedArtifact = new GitHubArtifact();
                AssociatedArtifact.CommitSha = value;
            }
        }

        [JsonIgnore]
        public string ShortCommitSha => AssociatedArtifact?.ShortCommitSha ?? string.Empty;

        [JsonIgnore]
        public string? CommitMessage 
        { 
            get => AssociatedArtifact?.CommitMessage; 
            set 
            {
                if (AssociatedArtifact == null)
                    AssociatedArtifact = new GitHubArtifact();
                AssociatedArtifact.CommitMessage = value;
            }
        }

        [JsonIgnore]
        public int? WorkflowRunNumber 
        { 
            get => AssociatedArtifact?.WorkflowNumber; 
            set 
            {
                if (AssociatedArtifact == null)
                    AssociatedArtifact = new GitHubArtifact();
                AssociatedArtifact.WorkflowNumber = value ?? 0;
            }
        }

        [JsonIgnore]
        public long? WorkflowRunId 
        { 
            get => AssociatedArtifact?.RunId; 
            set 
            {
                if (AssociatedArtifact == null)
                    AssociatedArtifact = new GitHubArtifact();
                AssociatedArtifact.RunId = value ?? 0;
            }
        }

        [JsonIgnore]
        public long? WorkflowDefinitionId => AssociatedArtifact?.WorkflowId;

        [JsonIgnore]
        public string? TriggeringEventType => AssociatedArtifact?.EventType;

        [JsonIgnore]
        public DateTime? ArtifactCreationDate 
        { 
            get => AssociatedArtifact?.CreatedAt; 
            set 
            {
                if (AssociatedArtifact == null)
                    AssociatedArtifact = new GitHubArtifact();
                if (value.HasValue)
                    AssociatedArtifact.CreatedAt = value.Value;
            }
        }

        [JsonIgnore]
        public string? BuildPreset => AssociatedArtifact?.BuildPreset ?? AssociatedArtifact?.BuildInfo?.Configuration;

        /// <summary>
        /// Gets a value indicating whether there is meaningful workflow information available,
        /// such as a workflow run ID, definition name, or run status.
        /// </summary>
        [JsonIgnore]
        public bool HasWorkflowInfo =>
            WorkflowRunId.HasValue ||
            !string.IsNullOrEmpty(WorkflowDefinitionName) ||
            !string.IsNullOrEmpty(WorkflowRunStatus);

        [JsonIgnore]
        public bool HasCompleteWorkflowContext => 
            !string.IsNullOrEmpty(WorkflowDefinitionName) && 
            WorkflowRunId.HasValue &&
            !string.IsNullOrEmpty(WorkflowRunStatus);

        /// <summary>
        /// Creates a deep copy of this metadata instance
        /// </summary>
        public override BaseSourceMetadata? Clone()
        {
            // Create a new instance
            var clone = new GitHubSourceMetadata
            {
                // Copy all simple properties
                WorkflowDefinitionName = this.WorkflowDefinitionName,
                WorkflowDefinitionPath = this.WorkflowDefinitionPath,
                WorkflowRunStatus = this.WorkflowRunStatus,
                WorkflowRunConclusion = this.WorkflowRunConclusion,
                SourceControlBranch = this.SourceControlBranch,
                AssociatedReleaseId = this.AssociatedReleaseId,
                ReleaseTag = this.ReleaseTag,
                ReleaseName = this.ReleaseName,
                ReleaseTagName = this.ReleaseTagName,
                ReleaseIsDraft = this.ReleaseIsDraft,
                ReleaseIsPrerelease = this.ReleaseIsPrerelease,
                ReleasePublishedAt = this.ReleasePublishedAt
            };
            
            // Deep copy artifact if present
            if (this.AssociatedArtifact != null)
            {
                clone.AssociatedArtifact = new GitHubArtifact
                {
                    Id = this.AssociatedArtifact.Id,
                    Name = this.AssociatedArtifact.Name,
                    WorkflowId = this.AssociatedArtifact.WorkflowId,
                    RunId = this.AssociatedArtifact.RunId,
                    WorkflowNumber = this.AssociatedArtifact.WorkflowNumber,
                    SizeInBytes = this.AssociatedArtifact.SizeInBytes,
                    ArchiveDownloadUrl = this.AssociatedArtifact.ArchiveDownloadUrl,
                    Expired = this.AssociatedArtifact.Expired,
                    CreatedAt = this.AssociatedArtifact.CreatedAt,
                    ExpiresAt = this.AssociatedArtifact.ExpiresAt,
                    PullRequestNumber = this.AssociatedArtifact.PullRequestNumber,
                    PullRequestTitle = this.AssociatedArtifact.PullRequestTitle,
                    CommitSha = this.AssociatedArtifact.CommitSha,
                    CommitMessage = this.AssociatedArtifact.CommitMessage,
                    EventType = this.AssociatedArtifact.EventType,
                    BuildPreset = this.AssociatedArtifact.BuildPreset
                };
                
                // Clone repository info if present
                if (this.AssociatedArtifact.RepositoryInfo != null)
                {
                    clone.AssociatedArtifact.RepositoryInfo = new GitHubRepoSettings
                    {
                        RepoOwner = this.AssociatedArtifact.RepositoryInfo.RepoOwner,
                        RepoName = this.AssociatedArtifact.RepositoryInfo.RepoName,
                        DisplayName = this.AssociatedArtifact.RepositoryInfo.DisplayName
                    };
                }
            }
            
            // Clone build info if present
            if (this.BuildInfo != null)
            {
                clone.BuildInfo = new GitHubBuild
                {
                    Version = this.BuildInfo.Version,
                    GameVariant = this.BuildInfo.GameVariant,
                    HasEFlag = this.BuildInfo.HasEFlag,
                    HasTFlag = this.BuildInfo.HasTFlag,
                    Compiler = this.BuildInfo.Compiler,
                    Configuration = this.BuildInfo.Configuration
                };
            }
            
            return clone;
        }
    }
}

