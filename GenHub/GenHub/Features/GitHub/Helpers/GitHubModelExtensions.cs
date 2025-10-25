using System;
using System.Text.RegularExpressions;
using GenHub.Core.Constants;
using GenHub.Core.Models.Enums;
using GenHub.Core.Models.GameClients;
using GenHub.Core.Models.GitHub;
using GenHub.Core.Models.SourceMetadata;

namespace GenHub.Features.GitHub.Helpers;

/// <summary>
/// Extension methods for GitHub-related model classes.
/// </summary>
public static class GitHubModelExtensions
{
    /// <summary>
    /// Creates a deep copy of a GitHubArtifact instance.
    /// </summary>
    /// <param name="artifact">The artifact to copy.</param>
    /// <returns>A deep copy of the artifact.</returns>
    public static GitHubArtifact CreateCopy(this GitHubArtifact artifact)
    {
        if (artifact == null) return new GitHubArtifact();

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
            BuildInfo = artifact.BuildInfo?.CreateCopy() ?? new GitHubBuild(),
            RepositoryInfo = artifact.RepositoryInfo?.CreateCopy() ?? new GitHubRepository(),
            IsActive = artifact.IsActive,
            IsInstalled = artifact.IsInstalled,
            WorkflowRun = artifact.WorkflowRun?.CreateCopy(),
        };
    }

    /// <summary>
    /// Gets a display name for an artifact based on its metadata.
    /// </summary>
    /// <param name="artifact">The artifact.</param>
    /// <returns>The display name.</returns>
    public static string GetDisplayName(this GitHubArtifact artifact)
    {
        if (artifact == null)
            return GitHubConstants.UnknownArtifactDisplayName;

        if (artifact.PullRequestNumber.HasValue && !string.IsNullOrWhiteSpace(artifact.PullRequestTitle))
            return string.Format(GitHubConstants.PullRequestDisplayFormat, artifact.PullRequestNumber, artifact.PullRequestTitle, artifact.Name);

        if (artifact.WorkflowNumber > 0)
            return string.Format(GitHubConstants.BuildDisplayFormat, artifact.WorkflowNumber, artifact.Name);

        return artifact.Name;
    }

    /// <summary>
    /// Gets a formatted size string for an artifact.
    /// </summary>
    /// <param name="artifact">The artifact.</param>
    /// <returns>The formatted size.</returns>
    public static string GetFormattedSize(this GitHubArtifact artifact)
    {
        if (artifact == null)
            return $"0 {GitHubConstants.BytesUnit}";

        string[] sizes = { GitHubConstants.BytesUnit, GitHubConstants.KilobytesUnit, GitHubConstants.MegabytesUnit, GitHubConstants.GigabytesUnit, GitHubConstants.TerabytesUnit };
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
    /// Creates a deep copy of a GitHubBuild instance.
    /// </summary>
    /// <param name="build">The build to copy.</param>
    /// <returns>A deep copy of the build.</returns>
    public static GitHubBuild CreateCopy(this GitHubBuild build)
    {
        if (build == null) return new GitHubBuild();

        return new GitHubBuild
        {
            GameVariant = build.GameVariant,
            Compiler = build.Compiler,
            Configuration = build.Configuration,
            Version = build.Version,
            HasTFlag = build.HasTFlag,
            HasEFlag = build.HasEFlag,
        };
    }

    /// <summary>
    /// Creates a deep copy of a GitHubRepository instance.
    /// </summary>
    /// <param name="settings">The repository to copy.</param>
    /// <returns>A deep copy of the repository.</returns>
    public static GitHubRepository CreateCopy(this GitHubRepository settings)
    {
        if (settings == null) return new GitHubRepository();

        return new GitHubRepository
        {
            RepoOwner = settings.RepoOwner,
            RepoName = settings.RepoName,
            DisplayName = settings.DisplayName,
        };
    }

    /// <summary>
    /// Creates a copy of the GitHubWorkflow instance.
    /// </summary>
    /// <param name="workflow">The workflow to copy.</param>
    /// <returns>A new GitHubWorkflow instance with copied values.</returns>
    public static GitHubWorkflow CreateCopy(this GitHubWorkflow workflow)
    {
        if (workflow == null) return new GitHubWorkflow();

        return new GitHubWorkflow
        {
            Id = workflow.Id,
            Name = workflow.Name,
            Path = workflow.Path,
            State = workflow.State,
            CreatedAt = workflow.CreatedAt,
            UpdatedAt = workflow.UpdatedAt,
        };
    }

    /// <summary>
    /// Creates a deep copy of a GitHubWorkflowRun instance.
    /// </summary>
    /// <param name="run">The workflow run to copy.</param>
    /// <returns>A new GitHubWorkflowRun instance with copied values.</returns>
    public static GitHubWorkflowRun CreateCopy(this GitHubWorkflowRun run)
    {
        if (run == null) return new GitHubWorkflowRun();

        return new GitHubWorkflowRun
        {
            Id = run.Id,
            WorkflowId = run.WorkflowId,
            RunNumber = run.RunNumber,
            RunAttempt = run.RunAttempt,
            Status = run.Status,
            Conclusion = run.Conclusion,
            HeadBranch = run.HeadBranch,
            HeadSha = run.HeadSha,
            Event = run.Event,
            CreatedAt = run.CreatedAt,
            UpdatedAt = run.UpdatedAt,
            HtmlUrl = run.HtmlUrl,
            Workflow = run.Workflow?.CreateCopy() ?? new GitHubWorkflow(),
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
        if (artifactName.Contains(GitHubConstants.ZeroHourIdentifier, StringComparison.OrdinalIgnoreCase))
            build.GameVariant = GameType.ZeroHour;
        else if (artifactName.Contains(GitHubConstants.GeneralsIdentifier, StringComparison.OrdinalIgnoreCase))
            build.GameVariant = GameType.Generals;

        // Extract configuration (Debug/Release)
        if (artifactName.Contains(GitHubConstants.DebugConfiguration, StringComparison.OrdinalIgnoreCase))
            build.Configuration = GitHubConstants.DebugConfiguration;
        else if (artifactName.Contains(GitHubConstants.ReleaseConfiguration, StringComparison.OrdinalIgnoreCase))
            build.Configuration = GitHubConstants.ReleaseConfiguration;

        // Extract compiler (if available)
        if (artifactName.Contains(GitHubConstants.MsvcCompiler, StringComparison.OrdinalIgnoreCase))
            build.Compiler = GitHubConstants.MsvcCompiler;
        else if (artifactName.Contains(GitHubConstants.GccCompiler, StringComparison.OrdinalIgnoreCase))
            build.Compiler = GitHubConstants.GccCompiler;

        // Extract version (if using format like "v1.2.3")
        var versionMatch = Regex.Match(artifactName, GitHubConstants.VersionRegexPattern);
        if (versionMatch.Success)
            build.Version = versionMatch.Groups[1].Value;

        // Extract special flags
        build.HasTFlag = artifactName.Contains(GitHubConstants.TFlag, StringComparison.OrdinalIgnoreCase);
        build.HasEFlag = artifactName.Contains(GitHubConstants.EFlag, StringComparison.OrdinalIgnoreCase);

        return build;
    }

    /// <summary>
    /// Sets GitHub metadata information on a GameClient.
    /// </summary>
    /// <param name="gameClient">The game version.</param>
    /// <param name="artifact">The artifact.</param>
    public static void SetGitHubMetadata(this GameClient gameClient, GitHubArtifact artifact)
    {
        if (gameClient == null || artifact == null) return;

        var metadata = new GitHubSourceMetadata
        {
            AssociatedArtifact = artifact,
            BuildInfo = artifact.BuildInfo,
        };

        gameClient.GitHubMetadata = metadata;
        gameClient.BuildDate = artifact.CreatedAt;
    }

    /// <summary>
    /// Updates the associated artifact or creates a new one with the specified ID.
    /// </summary>
    /// <param name="metadata">The metadata.</param>
    /// <param name="artifactId">The artifact ID.</param>
    public static void SetArtifactId(this GitHubSourceMetadata metadata, long artifactId)
    {
        if (metadata.AssociatedArtifact == null)
            metadata.AssociatedArtifact = new GitHubArtifact();

        metadata.AssociatedArtifact.Id = artifactId;
    }

    /// <summary>
    /// Updates the pull request information in the metadata.
    /// </summary>
    /// <param name="metadata">The metadata.</param>
    /// <param name="pullRequestNumber">The pull request number.</param>
    /// <param name="pullRequestTitle">The pull request title.</param>
    public static void SetPullRequestInfo(this GitHubSourceMetadata metadata, int? pullRequestNumber, string? pullRequestTitle = null)
    {
        if (metadata.AssociatedArtifact == null)
            metadata.AssociatedArtifact = new GitHubArtifact();

        metadata.AssociatedArtifact.PullRequestNumber = pullRequestNumber;

        if (pullRequestTitle != null)
            metadata.AssociatedArtifact.PullRequestTitle = pullRequestTitle;
    }

    /// <summary>
    /// Updates the commit information in the metadata.
    /// </summary>
    /// <param name="metadata">The metadata.</param>
    /// <param name="commitSha">The commit SHA.</param>
    /// <param name="commitMessage">The commit message.</param>
    public static void SetCommitInfo(this GitHubSourceMetadata metadata, string? commitSha, string? commitMessage = null)
    {
        if (metadata.AssociatedArtifact == null)
            metadata.AssociatedArtifact = new GitHubArtifact();

        if (commitSha != null)
            metadata.AssociatedArtifact.CommitSha = commitSha;

        if (commitMessage != null)
            metadata.AssociatedArtifact.CommitMessage = commitMessage;
    }

    /// <summary>
    /// Updates the workflow run information in the metadata.
    /// </summary>
    /// <param name="metadata">The metadata.</param>
    /// <param name="runId">The run ID.</param>
    /// <param name="runNumber">The run number.</param>
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
    /// Updates the artifact creation date in the metadata.
    /// </summary>
    /// <param name="metadata">The metadata.</param>
    /// <param name="creationDate">The creation date.</param>
    public static void SetArtifactCreationDate(this GitHubSourceMetadata metadata, DateTime creationDate)
    {
        if (metadata.AssociatedArtifact == null)
            metadata.AssociatedArtifact = new GitHubArtifact();

        metadata.AssociatedArtifact.CreatedAt = creationDate;
    }
}
