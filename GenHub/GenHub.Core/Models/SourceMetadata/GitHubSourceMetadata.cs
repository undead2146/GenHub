using GenHub.Core.Models.GitHub;

namespace GenHub.Core.Models.SourceMetadata;

/// <summary>
/// Represents source metadata for GitHub-related content.
/// </summary>
public class GitHubSourceMetadata
{
    /// <summary>
    /// Initializes a new instance of the <see cref="GitHubSourceMetadata"/> class.
    /// </summary>
    public GitHubSourceMetadata()
    {
        AssociatedArtifact = new GitHubArtifact();
        BuildInfo = new GitHubBuild();
    }

    /// <summary>
    /// Gets or sets the associated artifact.
    /// </summary>
    public GitHubArtifact AssociatedArtifact { get; set; }

    /// <summary>
    /// Gets or sets the build info.
    /// </summary>
    public GitHubBuild BuildInfo { get; set; }
}
