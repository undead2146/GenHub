using GenHub.Core.Models.Enums;

namespace GenHub.Core.Models.GameClients;

/// <summary>
/// Identification result for a game client executable.
/// Contains metadata about which publisher and variant the executable represents.
/// </summary>
/// <param name="publisherId">The publisher identifier (e.g., "GeneralsOnline", "TheSuperHackers").</param>
/// <param name="variant">The specific variant of the content (e.g., "30hz", "60hz", "generals", "zerohour").</param>
/// <param name="displayName">Human-readable display name for the client (e.g., "GeneralsOnline 30Hz").</param>
/// <param name="gameType">The game type this client is for.</param>
/// <param name="localVersion">Version from the file if detectable, null otherwise.</param>
public record GameClientIdentification(
    string publisherId,
    string variant,
    string displayName,
    GameType gameType,
    string? localVersion)
{
    /// <summary>
    /// Gets the publisher identifier.
    /// </summary>
    public string PublisherId => publisherId;

    /// <summary>
    /// Gets the specific variant of the content.
    /// </summary>
    public string Variant => variant;

    /// <summary>
    /// Gets the display name for the client.
    /// </summary>
    public string DisplayName => displayName;

    /// <summary>
    /// Gets the game type this client is for.
    /// </summary>
    public GameType GameType => gameType;

    /// <summary>
    /// Gets the local version from the file, if detectable.
    /// </summary>
    public string? LocalVersion => localVersion;
}
