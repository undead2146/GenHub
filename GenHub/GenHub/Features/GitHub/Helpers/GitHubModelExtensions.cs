using System;
using System.Text.RegularExpressions;
using GenHub.Core.Models;
using GenHub.Core.Models.GameProfiles;
using GenHub.Core.Models.SourceMetadata;

namespace GenHub.Features.GitHub.Helpers
{
    /// <summary>
    /// Extension methods for GitHub-related model classes
    /// </summary>
    public static class GitHubModelExtensions
    {
        /// <summary>
        /// Creates a deep copy of a GitHubArtifact instance
        /// </summary>
        public static GitHubArtifact CreateCopy(this GitHubArtifact artifact)
        {
            if (artifact == null) return null;
            
            return new GitHubArtifact
            {
                Id = artifact.Id,
                Name = artifact.Name,
                WorkflowId = artifact.WorkflowId,
                RunId = artifact.RunId,
                WorkflowNumber = artifact.WorkflowNumber,
                SizeInBytes = artifact.SizeInBytes,
                IsRelease = artifact.IsRelease,
                DownloadUrl = artifact.DownloadUrl,
                ArchiveDownloadUrl = artifact.ArchiveDownloadUrl,
                Expired = artifact.Expired,
                CreatedAt = artifact.CreatedAt,
                ExpiresAt = artifact.ExpiresAt,
                PullRequestNumber = artifact.PullRequestNumber,
                PullRequestTitle = artifact.PullRequestTitle,
                CommitSha = artifact.CommitSha,
                CommitMessage = artifact.CommitMessage,
                EventType = artifact.EventType,
                BuildPreset = artifact.BuildPreset,
                BuildInfo = artifact.BuildInfo?.CreateCopy(),
                RepositoryInfo = artifact.RepositoryInfo?.CreateCopy(),
                IsActive = artifact.IsActive,
                IsInstalled = artifact.IsInstalled,
                IsInstalling = artifact.IsInstalling
            };
        }

        /// <summary>
        /// Gets a display name for an artifact based on its metadata
        /// </summary>
        public static string GetDisplayName(this GitHubArtifact artifact)
        {
            if (artifact == null)
                return "Unknown Artifact";
                
            if (artifact.PullRequestNumber.HasValue && !string.IsNullOrWhiteSpace(artifact.PullRequestTitle))
                return $"PR #{artifact.PullRequestNumber}: {artifact.PullRequestTitle} ({artifact.Name})";
            
            if (artifact.WorkflowNumber > 0)
                return $"Build #{artifact.WorkflowNumber} ({artifact.Name})";
                
            return artifact.Name;
        }
        
        /// <summary>
        /// Gets a formatted size string for an artifact
        /// </summary>
        public static string GetFormattedSize(this GitHubArtifact artifact)
        {
            if (artifact == null)
                return "0 B";
                
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            int order = 0;
            double size = artifact.SizeInBytes;
            
            while (size >= 1024 && order < sizes.Length - 1)
            {
                order++;
                size = size / 1024;
            }
            
            return $"{size:0.##} {sizes[order]}";
        }

        /// <summary>
        /// Creates a deep copy of a GitHubBuild instance
        /// </summary>
        public static GitHubBuild CreateCopy(this GitHubBuild build)
        {
            if (build == null) return null;
            
            return new GitHubBuild
            {
                GameVariant = build.GameVariant,
                Compiler = build.Compiler,
                Configuration = build.Configuration,
                Version = build.Version,
                HasTFlag = build.HasTFlag,
                HasEFlag = build.HasEFlag
            };
        }

        /// <summary>
        /// Creates a deep copy of a GitHubRepository instance
        /// </summary>
        public static GitHubRepository CreateCopy(this GitHubRepository settings)
        {
            if (settings == null) return null;
            
            return new GitHubRepository
            {
                RepoOwner = settings.RepoOwner,
                RepoName = settings.RepoName,
                DisplayName = settings.DisplayName
            };
        }

        /// <summary>
        /// Parses build information from an artifact name.
        /// </summary>
        /// <param name="artifactName">The name of the artifact.</param>
        /// <returns>Parsed GitHubBuild info or a default instance if parsing fails.</returns>
        public static GitHubBuild ParseBuildInfo(string artifactName)
        {
            if (string.IsNullOrEmpty(artifactName))
                return new GitHubBuild();
                
            var build = new GitHubBuild();
            
            // Extract game variant (e.g., Zero Hour, Generals)
            if (artifactName.Contains("ZH", StringComparison.OrdinalIgnoreCase))
                build.GameVariant = GameVariant.ZeroHour;
            else if (artifactName.Contains("Gen", StringComparison.OrdinalIgnoreCase))
                build.GameVariant = GameVariant.Generals;
            
            // Extract configuration (Debug/Release)
            if (artifactName.Contains("Debug", StringComparison.OrdinalIgnoreCase))
                build.Configuration = "Debug";
            else if (artifactName.Contains("Release", StringComparison.OrdinalIgnoreCase))
                build.Configuration = "Release";
                
            // Extract compiler (if available)
            if (artifactName.Contains("MSVC", StringComparison.OrdinalIgnoreCase))
                build.Compiler = "MSVC";
            else if (artifactName.Contains("GCC", StringComparison.OrdinalIgnoreCase))
                build.Compiler = "GCC";
                
            // Extract version (if using format like "v1.2.3")
            var versionMatch = Regex.Match(artifactName, @"v(\d+\.\d+\.\d+)");
            if (versionMatch.Success)
                build.Version = versionMatch.Groups[1].Value;
                
            // Extract special flags
            build.HasTFlag = artifactName.Contains("-T", StringComparison.OrdinalIgnoreCase);
            build.HasEFlag = artifactName.Contains("-E", StringComparison.OrdinalIgnoreCase);
                
            return build;
        }
        
        /// <summary>
        /// Sets GitHub metadata information on a GameVersion
        /// </summary>
        public static void SetGitHubMetadata(this GameVersion gameVersion, GitHubArtifact artifact)
        {
            if (gameVersion == null || artifact == null) return;
            
            var metadata = new GitHubSourceMetadata
            {
                AssociatedArtifact = artifact,
                BuildInfo = artifact.BuildInfo
            };
            
            gameVersion.GitHubMetadata = metadata;
            gameVersion.BuildDate = artifact.CreatedAt;
        }
        
        /// <summary>
        /// Updates the associated artifact or creates a new one with the specified ID
        /// </summary>
        public static void SetArtifactId(this GitHubSourceMetadata metadata, long artifactId)
        {
            if (metadata.AssociatedArtifact == null)
                metadata.AssociatedArtifact = new GitHubArtifact();
                
            metadata.AssociatedArtifact.Id = artifactId;
        }
        
        /// <summary>
        /// Updates the pull request information in the metadata
        /// </summary>
        public static void SetPullRequestInfo(this GitHubSourceMetadata metadata, int? pullRequestNumber, string? pullRequestTitle = null)
        {
            if (metadata.AssociatedArtifact == null)
                metadata.AssociatedArtifact = new GitHubArtifact();
                
            metadata.AssociatedArtifact.PullRequestNumber = pullRequestNumber;
            
            if (pullRequestTitle != null)
                metadata.AssociatedArtifact.PullRequestTitle = pullRequestTitle;
        }
        
        /// <summary>
        /// Updates the commit information in the metadata
        /// </summary>
        public static void SetCommitInfo(this GitHubSourceMetadata metadata, string? commitSha, string? commitMessage = null)
        {
            if (metadata.AssociatedArtifact == null)
                metadata.AssociatedArtifact = new GitHubArtifact();
                
            metadata.AssociatedArtifact.CommitSha = commitSha;
            
            if (commitMessage != null)
                metadata.AssociatedArtifact.CommitMessage = commitMessage;
        }
        
        /// <summary>
        /// Updates the workflow run information in the metadata
        /// </summary>
        public static void SetWorkflowRunInfo(this GitHubSourceMetadata metadata, long? runId, int? runNumber = null)
        {
            if (metadata.AssociatedArtifact == null)
                metadata.AssociatedArtifact = new GitHubArtifact();
                
            if (runId.HasValue)
                metadata.AssociatedArtifact.RunId = runId.Value;
                
            if (runNumber.HasValue)
                metadata.AssociatedArtifact.WorkflowNumber = runNumber.Value;
        }
        
        /// <summary>
        /// Updates the artifact creation date in the metadata
        /// </summary>
        public static void SetArtifactCreationDate(this GitHubSourceMetadata metadata, DateTime creationDate)
        {
            if (metadata.AssociatedArtifact == null)
                metadata.AssociatedArtifact = new GitHubArtifact();
                
            metadata.AssociatedArtifact.CreatedAt = creationDate;
        }
    }
}
