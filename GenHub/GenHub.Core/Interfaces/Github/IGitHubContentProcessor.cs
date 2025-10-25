using System.Threading;
using System.Threading.Tasks;
using GenHub.Core.Models.Enums;
using GenHub.Core.Models.GitHub;
using GenHub.Core.Models.Manifest;
using GenHub.Core.Models.Results;

namespace GenHub.Core.Interfaces.Github;

/// <summary>
/// Request object for content processing operations.
/// </summary>
public class ContentProcessingRequest
{
    /// <summary>
    /// Gets the repository owner.
    /// </summary>
    public string Owner { get; init; } = string.Empty;

    /// <summary>
    /// Gets the repository name.
    /// </summary>
    public string Repository { get; init; } = string.Empty;

    /// <summary>
    /// Gets the GitHub asset to process.
    /// </summary>
    public object Asset { get; init; } = null!;

    /// <summary>
    /// Gets the display name for the content.
    /// </summary>
    public string ContentName { get; init; } = string.Empty;

    /// <summary>
    /// Gets the manifest version.
    /// </summary>
    public int ManifestVersion { get; init; }

    /// <summary>
    /// Gets the type of content.
    /// </summary>
    public ContentType ContentType { get; init; }

    /// <summary>
    /// Gets the target game type.
    /// </summary>
    public GameType TargetGame { get; init; }

    /// <summary>
    /// Gets the cancellation token.
    /// </summary>
    public CancellationToken CancellationToken { get; init; }
}

/// <summary>
/// Service for processing GitHub content through the complete pipeline:
/// Download → Extract → CAS Storage → Manifest Generation.
/// </summary>
public interface IGitHubContentProcessor
{
    /// <summary>
    /// Processes a GitHub release asset through the complete pipeline.
    /// </summary>
    /// <param name="request">The content processing request.</param>
    /// <returns>The generated content manifest.</returns>
    Task<OperationResult<ContentManifest>> ProcessReleaseAssetAsync(
        ContentProcessingRequest request);

    /// <summary>
    /// Processes a GitHub workflow artifact through the complete pipeline.
    /// </summary>
    /// <param name="request">The content processing request.</param>
    /// <returns>The generated content manifest.</returns>
    Task<OperationResult<ContentManifest>> ProcessWorkflowArtifactAsync(
        ContentProcessingRequest request);
}
