using GenHub.Core.Models.Content;
using GenHub.Core.Models.Enums;
using GenHub.Core.Models.GameProfile;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;

namespace GenHub.Core.Interfaces.GameProfiles;

/// <summary>
/// Service for loading profile-related content including game installations,
/// available content, and enabled content for profiles.
/// </summary>
/// <remarks>
/// This service encapsulates content loading logic to support game profile management
/// and UI presentation. It coordinates between game installation detection, manifest pools,
/// and profile configuration to provide comprehensive content data.
/// </remarks>
public interface IProfileContentLoader
{
    /// <summary>
    /// Loads available game installations from detected installations.
    /// Creates entries for each available GameClient within each installation.
    /// </summary>
    /// <returns>A collection of content display items representing available game installations.</returns>
    Task<ObservableCollection<Models.Content.ContentDisplayItem>> LoadAvailableGameInstallationsAsync();

    /// <summary>
    /// Loads available content filtered by the specified content type.
    /// </summary>
    /// <param name="contentType">The content type to filter by.</param>
    /// <param name="availableGameInstallations">Pre-loaded game installations for GameClient resolution.</param>
    /// <param name="enabledContentIds">List of already-enabled content IDs to mark items as enabled.</param>
    /// <returns>A collection of content display items for the specified type.</returns>
    Task<ObservableCollection<Models.Content.ContentDisplayItem>> LoadAvailableContentAsync(
        ContentType contentType,
        ObservableCollection<Models.Content.ContentDisplayItem> availableGameInstallations,
        IEnumerable<string> enabledContentIds);

    /// <summary>
    /// Loads enabled content for a specific game profile.
    /// </summary>
    /// <param name="profile">The game profile containing enabled content IDs.</param>
    /// <returns>A collection of content display items representing enabled content.</returns>
    Task<ObservableCollection<Models.Content.ContentDisplayItem>> LoadEnabledContentForProfileAsync(GameProfile profile);
}
