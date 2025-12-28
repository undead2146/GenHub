using GenHub.Core.Models.Enums;
using GenHub.Core.Models.GameClients;

namespace GenHub.Core.Interfaces.GameClients;

/// <summary>
/// Identifies a local executable and returns metadata about what it is.
/// Does NOT create manifests or download anything.
/// </summary>
public interface IGameClientIdentifier
{
    /// <summary>
    /// Gets the publisher this identifier handles.
    /// </summary>
    string PublisherId { get; }

    /// <summary>
    /// Checks if this identifier can handle the given executable.
    /// </summary>
    /// <param name="executablePath">The path to the executable file to check.</param>
    /// <returns>True if this identifier can handle the executable, false otherwise.</returns>
    bool CanIdentify(string executablePath);

    /// <summary>
    /// Identifies the executable and returns metadata.
    /// </summary>
    /// <param name="executablePath">The path to the executable file to identify.</param>
    /// <returns>Identification metadata if the executable is recognized, null otherwise.</returns>
    GameClientIdentification? Identify(string executablePath);
}
