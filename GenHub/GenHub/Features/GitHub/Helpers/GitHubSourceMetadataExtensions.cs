using System;
using System.Linq;
using GenHub.Core.Interfaces;
using GenHub.Core.Models;
using GenHub.Core.Models.GitHub;
using GenHub.Core.Models.GameProfiles;
using GenHub.Core.Models.SourceMetadata;


namespace GenHub.Features.GitHub.Helpers
{
    /// <summary>
    /// Extension methods for working with GitHub data models and source metadata
    /// </summary>
    public static class GitHubSourceMetadataExtensions
    {
        /// <summary>
        /// Converts a GitHubArtifact to GitHubSourceMetadata
        /// </summary>
        public static GitHubSourceMetadata ToSourceMetadata(this GitHubArtifact artifact, GitHubWorkflow? workflow = null)
        {
            var metadata = new GitHubSourceMetadata
            {
                AssociatedArtifact = artifact,
                BuildInfo = artifact.BuildInfo,
                RepositoryInfo = artifact.RepositoryInfo,
                BuildPreset = artifact.BuildPreset
            };
            
            // Add workflow context if available
            if (workflow != null)
            {
                metadata.WorkflowDefinitionName = workflow.WorkflowName;
                metadata.WorkflowDefinitionPath = workflow.WorkflowPath;
                metadata.WorkflowRunStatus = workflow.Status;
                metadata.WorkflowRunConclusion = workflow.Conclusion;
                metadata.SourceControlBranch = workflow.HeadBranch;
            }
            
            return metadata;
        }
        
        /// <summary>
        /// Converts a GitHubReleaseAsset to GitHubSourceMetadata
        /// </summary>
        public static GitHubSourceMetadata ToSourceMetadata(this GitHubReleaseAsset asset, GitHubRelease release)
        {
            var metadata = new GitHubSourceMetadata
            {
                AssociatedReleaseAsset = asset,
                ReleaseName = release.Name,
                ReleaseTagName = release.TagName,
                ReleaseIsPrerelease = release.Prerelease,
                ReleasePublishedAt = release.PublishedAt
            };

            return metadata;
        }
        
        /// <summary>
        /// Gets GitHub metadata from a GameVersion if available
        /// </summary>
        public static GitHubSourceMetadata? GetGitHubMetadata(this GameVersion version)
        {
            return version.SourceSpecificMetadata as GitHubSourceMetadata;
        }
        
        /// <summary>
        /// Gets GitHub metadata from a GameProfile if available
        /// </summary>
        public static GitHubSourceMetadata? GetGitHubMetadata(this IGameProfile profile)
        {
            return profile.SourceSpecificMetadata as GitHubSourceMetadata;
        }
        
        /// <summary>
        /// Sets GitHub metadata from a GitHubArtifact
        /// </summary>
        public static void SetGitHubMetadata(this GameVersion version, GitHubArtifact artifact, GitHubWorkflow? workflow = null)
        {
            version.SourceSpecificMetadata = artifact.ToSourceMetadata(workflow);
            version.SourceType = GameInstallationType.GitHubArtifact;
        }

        /// <summary>
        /// Sets GitHub metadata from a GitHubReleaseAsset
        /// </summary>
        public static void SetGitHubMetadata(this GameVersion version, GitHubReleaseAsset asset, GitHubRelease release)
        {
            version.SourceSpecificMetadata = asset.ToSourceMetadata(release);
            version.SourceType = GameInstallationType.GitHubRelease;
        }
        
        /// <summary>
        /// Creates a display name for a GitHub-sourced game version
        /// </summary>
        public static string CreateDisplayName(this GitHubSourceMetadata metadata)
        {
            if (metadata.PullRequestNumber.HasValue)
                return $"PR #{metadata.PullRequestNumber} - {GetBuildDescription(metadata)}";
            
            if (!string.IsNullOrEmpty(metadata.BuildPreset))
                return $"Build #{metadata.WorkflowRunNumber} - {metadata.BuildPreset}";
            
            if (metadata.AssociatedArtifact != null)
                return metadata.AssociatedArtifact.Name;

            if (metadata.AssociatedReleaseAsset != null)
                return $"Release: {metadata.ReleaseName} - {metadata.AssociatedReleaseAsset.Name}";
                
            return $"Build #{metadata.WorkflowRunNumber}";
        }
        
        private static string GetBuildDescription(GitHubSourceMetadata metadata)
        {
            if (metadata.BuildInfo != null)
            {
                string variant = metadata.BuildInfo.GameVariant.ToString();
                string config = metadata.BuildInfo.Configuration ?? "Unknown";
                string compiler = metadata.BuildInfo.Compiler ?? "Unknown";
                
                return $"{variant} {config} ({compiler})";
            }
            
            return metadata.BuildPreset ?? "Unknown Build";
        }
    }
}
