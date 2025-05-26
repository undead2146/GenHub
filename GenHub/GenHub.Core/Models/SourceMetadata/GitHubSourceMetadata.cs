using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using GenHub.Core.Models.GitHub;

namespace GenHub.Core.Models.SourceMetadata
{
    /// <summary>
    /// GitHub-specific source metadata for game versions
    /// </summary>
    public class GitHubSourceMetadata : BaseSourceMetadata
    {
        /// <summary>
        /// The associated GitHub artifact
        /// </summary>
        public GitHubArtifact? AssociatedArtifact { get; set; }

        /// <summary>
        /// The associated GitHub release asset
        /// </summary>
        public GitHubReleaseAsset? AssociatedReleaseAsset { get; set; }

        /// <summary>
        /// Build information parsed from the artifact
        /// </summary>
        public GitHubBuild? BuildInfo { get; set; }

        /// <summary>
        /// The repository information
        /// </summary>
        public GitHubRepoSettings? RepositoryInfo { get; set; }

        /// <summary>
        /// The name of the release (for release assets)
        /// </summary>
        public string? ReleaseName { get; set; }

        /// <summary>
        /// The tag name of the release (for release assets)
        /// </summary>
        public string? ReleaseTagName { get; set; }

        /// <summary>
        /// Whether the release is a prerelease
        /// </summary>
        public bool ReleaseIsPrerelease { get; set; }

        /// <summary>
        /// When the release was published
        /// </summary>
        public DateTime? ReleasePublishedAt { get; set; }

        /// <summary>
        /// The workflow definition name
        /// </summary>
        public string? WorkflowDefinitionName { get; set; }

        /// <summary>
        /// The workflow definition path
        /// </summary>
        public string? WorkflowDefinitionPath { get; set; }

        /// <summary>
        /// The workflow run status
        /// </summary>
        public string? WorkflowRunStatus { get; set; }

        /// <summary>
        /// The workflow run conclusion
        /// </summary>
        public string? WorkflowRunConclusion { get; set; }

        /// <summary>
        /// The source control branch
        /// </summary>
        public string? SourceControlBranch { get; set; }

        /// <summary>
        /// The build preset name
        /// </summary>
        public string? BuildPreset { get; set; }

        /// <summary>
        /// Convenience accessor for pull request number
        /// </summary>
        public int? PullRequestNumber => AssociatedArtifact?.PullRequestNumber;

        /// <summary>
        /// Convenience accessor for pull request title
        /// </summary>
        public string? PullRequestTitle => AssociatedArtifact?.PullRequestTitle;

        /// <summary>
        /// Convenience accessor for workflow run number
        /// </summary>
        public int? WorkflowRunNumber => AssociatedArtifact?.WorkflowNumber;

        /// <summary>
        /// Convenience accessor for commit SHA
        /// </summary>
        public string? CommitSha => AssociatedArtifact?.CommitSha;

        /// <summary>
        /// Convenience accessor for commit message
        /// </summary>
        public string? CommitMessage => AssociatedArtifact?.CommitMessage;

        /// <summary>
        /// Convenience accessor for artifact creation date
        /// </summary>
        public DateTime? ArtifactCreationDate => AssociatedArtifact?.CreatedAt;

        /// <summary>
        /// The workflow run ID (convenience accessor)
        /// </summary>
        public long? WorkflowRunId => AssociatedArtifact?.RunId;

        /// <summary>
        /// The workflow definition ID
        /// </summary>
        public long? WorkflowDefinitionId => AssociatedArtifact?.WorkflowId;

        /// <summary>
        /// The triggering event type for the workflow
        /// </summary>
        public string? TriggeringEventType => AssociatedArtifact?.EventType;

        /// <summary>
        /// Checks if this metadata has complete workflow context information
        /// </summary>
        [JsonIgnore]
        public bool HasCompleteWorkflowContext =>
            AssociatedArtifact != null &&
            !string.IsNullOrEmpty(WorkflowDefinitionName) &&
            WorkflowRunId.HasValue &&
            !string.IsNullOrEmpty(SourceControlBranch);

        public GitHubSourceMetadata()
        {
            SourceType = "GitHub";
        }

        /// <summary>
        /// Creates a deep copy of this GitHubSourceMetadata instance
        /// </summary>
        public new GitHubSourceMetadata Clone()
        {
            var cloned = new GitHubSourceMetadata
            {
                SourceType = this.SourceType,
                CreatedAt = this.CreatedAt,
                UpdatedAt = this.UpdatedAt,
                ExtensionData = this.ExtensionData?.ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
                
                // Clone GitHubArtifact manually
                AssociatedArtifact = this.AssociatedArtifact == null ? null : new GitHubArtifact
                {
                    Id = this.AssociatedArtifact.Id,
                    Name = this.AssociatedArtifact.Name,
                    WorkflowId = this.AssociatedArtifact.WorkflowId,
                    RunId = this.AssociatedArtifact.RunId,
                    WorkflowNumber = this.AssociatedArtifact.WorkflowNumber,
                    SizeInBytes = this.AssociatedArtifact.SizeInBytes,
                    IsRelease = this.AssociatedArtifact.IsRelease,
                    DownloadUrl = this.AssociatedArtifact.DownloadUrl,
                    ArchiveDownloadUrl = this.AssociatedArtifact.ArchiveDownloadUrl,
                    Expired = this.AssociatedArtifact.Expired,
                    CreatedAt = this.AssociatedArtifact.CreatedAt,
                    ExpiresAt = this.AssociatedArtifact.ExpiresAt,
                    PullRequestNumber = this.AssociatedArtifact.PullRequestNumber,
                    PullRequestTitle = this.AssociatedArtifact.PullRequestTitle,
                    CommitSha = this.AssociatedArtifact.CommitSha,
                    CommitMessage = this.AssociatedArtifact.CommitMessage,
                    EventType = this.AssociatedArtifact.EventType,
                    BuildPreset = this.AssociatedArtifact.BuildPreset,
                    IsActive = this.AssociatedArtifact.IsActive,
                    IsInstalled = this.AssociatedArtifact.IsInstalled,
                    IsInstalling = this.AssociatedArtifact.IsInstalling,
                    // Clone nested objects manually
                    BuildInfo = this.AssociatedArtifact.BuildInfo == null ? null : new GitHubBuild
                    {
                        GameVariant = this.AssociatedArtifact.BuildInfo.GameVariant,
                        Compiler = this.AssociatedArtifact.BuildInfo.Compiler,
                        Configuration = this.AssociatedArtifact.BuildInfo.Configuration,
                        Version = this.AssociatedArtifact.BuildInfo.Version,
                        HasTFlag = this.AssociatedArtifact.BuildInfo.HasTFlag,
                        HasEFlag = this.AssociatedArtifact.BuildInfo.HasEFlag
                    },
                    RepositoryInfo = this.AssociatedArtifact.RepositoryInfo == null ? null : new GitHubRepoSettings
                    {
                        RepoOwner = this.AssociatedArtifact.RepositoryInfo.RepoOwner,
                        RepoName = this.AssociatedArtifact.RepositoryInfo.RepoName,
                        DisplayName = this.AssociatedArtifact.RepositoryInfo.DisplayName
                    }
                },
                
                // Clone GitHubReleaseAsset manually
                AssociatedReleaseAsset = this.AssociatedReleaseAsset == null ? null : new GitHubReleaseAsset
                {
                    Id = this.AssociatedReleaseAsset.Id,
                    Name = this.AssociatedReleaseAsset.Name,
                    Label = this.AssociatedReleaseAsset.Label,
                    ContentType = this.AssociatedReleaseAsset.ContentType,
                    Size = this.AssociatedReleaseAsset.Size,
                    DownloadCount = this.AssociatedReleaseAsset.DownloadCount,
                    BrowserDownloadUrl = this.AssociatedReleaseAsset.BrowserDownloadUrl,
                    CreatedAt = this.AssociatedReleaseAsset.CreatedAt,
                    UpdatedAt = this.AssociatedReleaseAsset.UpdatedAt,
                    State = this.AssociatedReleaseAsset.State
                },
                
                // Clone BuildInfo manually
                BuildInfo = this.BuildInfo == null ? null : new GitHubBuild
                {
                    GameVariant = this.BuildInfo.GameVariant,
                    Compiler = this.BuildInfo.Compiler,
                    Configuration = this.BuildInfo.Configuration,
                    Version = this.BuildInfo.Version,
                    HasTFlag = this.BuildInfo.HasTFlag,
                    HasEFlag = this.BuildInfo.HasEFlag
                },
                
                // Clone RepositoryInfo manually
                RepositoryInfo = this.RepositoryInfo == null ? null : new GitHubRepoSettings
                {
                    RepoOwner = this.RepositoryInfo.RepoOwner,
                    RepoName = this.RepositoryInfo.RepoName,
                    DisplayName = this.RepositoryInfo.DisplayName
                },
                
                // Copy simple properties
                ReleaseName = this.ReleaseName,
                ReleaseTagName = this.ReleaseTagName,
                ReleaseIsPrerelease = this.ReleaseIsPrerelease,
                ReleasePublishedAt = this.ReleasePublishedAt,
                WorkflowDefinitionName = this.WorkflowDefinitionName,
                WorkflowDefinitionPath = this.WorkflowDefinitionPath,
                WorkflowRunStatus = this.WorkflowRunStatus,
                WorkflowRunConclusion = this.WorkflowRunConclusion,
                SourceControlBranch = this.SourceControlBranch,
                BuildPreset = this.BuildPreset
            };

            return cloned;
        }

        public override string ToString()
        {
            if (BuildInfo != null)
            {
                return $"GitHub: {BuildInfo}";
            }

            if (AssociatedArtifact != null)
            {
                return $"GitHub: {AssociatedArtifact.Name}";
            }

            if (AssociatedReleaseAsset != null)
            {
                return $"GitHub Release: {AssociatedReleaseAsset.Name}";
            }

            return "GitHub Source";
        }

        /// <summary>
        /// Gets a shortened version of the commit SHA (first 8 characters)
        /// </summary>
        [JsonIgnore]
        public string? ShortCommitSha => 
            string.IsNullOrEmpty(CommitSha) ? null : 
            CommitSha.Length > 8 ? CommitSha.Substring(0, 8) : CommitSha;
    }
}

