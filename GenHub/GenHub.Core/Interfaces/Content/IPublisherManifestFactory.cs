using GenHub.Core.Models.Manifest;

namespace GenHub.Core.Interfaces.Content;

/// <summary>
/// Interface for publisher-specific manifest factories that handle content extraction,
/// manifest generation, and multi-variant support for GitHub releases.
/// </summary>
public interface IPublisherManifestFactory
{
    /// <summary>
    /// Gets the publisher identifier this factory handles (e.g., "thesuperhackers", "generalsonline").
    /// </summary>
    string PublisherId { get; }

    /// <summary>
    /// Determines if this factory can handle the given manifest based on publisher and content type.
    /// </summary>
    /// <param name="manifest">The manifest to check.</param>
    /// <returns>True if this factory can handle the manifest.</returns>
    bool CanHandle(ContentManifest manifest);

    /// <summary>
    /// Creates manifests from extracted GitHub release content.
    /// May return multiple manifests for multi-variant releases (e.g., Generals + Zero Hour).
    /// </summary>
    /// <param name="originalManifest">The original manifest from GitHub resolution.</param>
    /// <param name="extractedDirectory">The directory containing extracted files.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of content manifests (one or more depending on variants detected).</returns>
    Task<List<ContentManifest>> CreateManifestsFromExtractedContentAsync(
        ContentManifest originalManifest,
        string extractedDirectory,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the subdirectory for a specific manifest variant.
    /// Used to determine where files should be stored for each variant.
    /// </summary>
    /// <param name="manifest">The manifest to get the directory for.</param>
    /// <param name="extractedDirectory">The root extracted directory.</param>
    /// <returns>The subdirectory path for this manifest's files.</returns>
    string GetManifestDirectory(ContentManifest manifest, string extractedDirectory);
}
