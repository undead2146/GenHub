using GenHub.Core.Constants;
using GenHub.Core.Models.GameClients;
using GenHub.Core.Models.GitHub;
using GenHub.Core.Models.SourceMetadata;

namespace GenHub.Features.GitHub.Helpers;

/// <summary>
/// Extension methods for working with GitHub data models and source metadata.
/// </summary>
public static class GitHubSourceMetadataExtensions
{
    /// <summary>
    /// Converts a GitHubArtifact to GitHubSourceMetadata.
    /// </summary>
    /// <param name="artifact">The GitHub artifact to convert.</param>
    /// <param name="workflow">The optional GitHub workflow.</param>
    /// <returns>The GitHub source metadata.</returns>
    public static GitHubSourceMetadata ToSourceMetadata(this GitHubArtifact artifact, GitHubWorkflow? workflow = null)
    {
        var metadata = new GitHubSourceMetadata
        {
            AssociatedArtifact = artifact,
            BuildInfo = artifact.BuildInfo,
        };

        return metadata;
    }

    /// <summary>
    /// Converts a GitHubReleaseAsset to GitHubSourceMetadata.
    /// </summary>
    /// <param name="asset">The GitHub release asset to convert.</param>
    /// <param name="release">The GitHub release.</param>
    /// <returns>The GitHub source metadata.</returns>
    public static GitHubSourceMetadata ToSourceMetadata(this GitHubReleaseAsset asset, GitHubRelease release)
    {
        var metadata = new GitHubSourceMetadata
        {
            // Note: AssociatedReleaseAsset would need to be added to GitHubSourceMetadata if needed
            BuildInfo = new GitHubBuild(),
        };

        return metadata;
    }

    /// <summary>
    /// Gets GitHub metadata from a GameClient if available.
    /// </summary>
    /// <param name="version">The game version.</param>
    /// <returns>The GitHub source metadata or null.</returns>
    public static GitHubSourceMetadata? GetGitHubMetadata(this GameClient version)
    {
        return version.GitHubMetadata;
    }

    /// <summary>
    /// Sets GitHub metadata from a GitHubArtifact.
    /// </summary>
    /// <param name="version">The game version to update.</param>
    /// <param name="artifact">The GitHub artifact.</param>
    /// <param name="workflow">The optional GitHub workflow.</param>
    public static void SetGitHubMetadata(this GameClient version, GitHubArtifact artifact, GitHubWorkflow? workflow = null)
    {
        version.GitHubMetadata = artifact.ToSourceMetadata(workflow);
        version.BuildDate = artifact.CreatedAt;
    }

    /// <summary>
    /// Sets GitHub metadata from a GitHubReleaseAsset.
    /// </summary>
    /// <param name="version">The game version to update.</param>
    /// <param name="asset">The GitHub release asset.</param>
    /// <param name="release">The GitHub release.</param>
    public static void SetGitHubMetadata(this GameClient version, GitHubReleaseAsset asset, GitHubRelease release)
    {
        version.GitHubMetadata = asset.ToSourceMetadata(release);
        version.BuildDate = asset.CreatedAt.DateTime;
    }

    /// <summary>
    /// Creates a display name for a GitHub-sourced game version.
    /// </summary>
    /// <param name="metadata">The GitHub source metadata.</param>
    /// <returns>The display name.</returns>
    public static string CreateDisplayName(this GitHubSourceMetadata metadata)
    {
        if (metadata.AssociatedArtifact != null)
        {
            if (metadata.AssociatedArtifact.PullRequestNumber.HasValue)
                return string.Format(GitHubConstants.PullRequestDisplayFormat, metadata.AssociatedArtifact.PullRequestNumber, GetBuildDescription(metadata));

            if (!string.IsNullOrEmpty(metadata.AssociatedArtifact.BuildPreset))
                return string.Format(GitHubConstants.BuildDisplayFormat, metadata.AssociatedArtifact.WorkflowNumber, metadata.AssociatedArtifact.BuildPreset);

            return metadata.AssociatedArtifact.Name;
        }

        return GitHubConstants.DefaultContentDisplayName;
    }

    private static string GetBuildDescription(GitHubSourceMetadata metadata)
    {
        if (metadata.BuildInfo != null)
        {
            string variant = metadata.BuildInfo.GameVariant.ToString();
            string config = metadata.BuildInfo.Configuration ?? GitHubConstants.UnknownItemType;
            string compiler = metadata.BuildInfo.Compiler ?? GitHubConstants.UnknownItemType;

            return string.Format(GitHubConstants.BuildDescriptionFormat, variant, config, compiler);
        }

        return GitHubConstants.UnknownBuildDescription;
    }
}
