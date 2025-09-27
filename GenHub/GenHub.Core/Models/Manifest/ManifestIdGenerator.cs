using System.Text.RegularExpressions;
using GenHub.Core.Constants;
using GenHub.Core.Models.Enums;
using GenHub.Core.Models.GameInstallations;

namespace GenHub.Core.Models.Manifest;

/// <summary>
/// Utility for generating deterministic, human-readable manifest IDs.
/// </summary>
public static class ManifestIdGenerator
{
    /// <summary>
    /// Generates a manifest ID for publisher-provided content.
    /// Format: schemaVersion.manifestVersion.publisher.contentType.contentName[-suffix].
    /// </summary>
    /// <param name="publisherId">Publisher identifier used as the first segment (e.g., 'cnclabs', 'moddb-westwood').</param>
    /// <param name="contentType">The type of content being identified.</param>
    /// <param name="contentName">Human readable content name used as the second segment.</param>
    /// <param name="userVersion">User-specified version number (e.g., 1, 2, 20). Defaults to 0 for first version.</param>
    /// <param name="suffix">Optional suffix for content type (e.g., '-mod', '-mappack'). Defaults to empty.</param>
    /// <returns>A normalized manifest identifier in the form 'schemaVersion.manifestVersion.publisher.contentType.content[suffix]'.</returns>
    public static string GeneratePublisherContentId(string publisherId, ContentType contentType, string contentName, int userVersion = 0, string suffix = "")
    {
        if (publisherId == null)
            throw new ArgumentNullException(nameof(publisherId));
        if (contentName == null)
            throw new ArgumentNullException(nameof(contentName));
        if (userVersion < 0)
            throw new ArgumentException("User version cannot be negative", nameof(userVersion));

        var safePublisher = Normalize(publisherId);
        var contentTypeString = GetContentTypeString(contentType);
        var safeName = Normalize(contentName);
        var fullVersion = $"{ManifestConstants.DefaultManifestFormatVersion}.{userVersion}";

        var contentPart = string.IsNullOrEmpty(suffix) ? safeName : $"{safeName}{suffix}";
        return $"{fullVersion}.{safePublisher}.{contentTypeString}.{contentPart}";
    }

    /// <summary>
    /// Generates a manifest ID for a game installation.
    /// Format: manifestVersion.userVersion.installationType.gameType[-suffix].
    /// </summary>
    /// <param name="installation">The game installation used to derive the installation segment.</param>
    /// <param name="gameType">The specific game type (Generals or ZeroHour) for the manifest ID.</param>
    /// <param name="userVersion">User-specified version number (e.g., 1, 2, 20). Defaults to 0 for first version.</param>
    /// <param name="suffix">Optional suffix for content type (e.g., '-installation', '-client'). Defaults to '-installation'.</param>
    /// <returns>A normalized manifest identifier in the form 'manifestVersion.userVersion.installation.game[suffix]'.</returns>
    public static string GenerateGameInstallationId(GameInstallation installation, GameType gameType, int userVersion = 0, string suffix = "-installation")
    {
        if (installation == null)
            throw new ArgumentNullException(nameof(installation));
        if (userVersion < 0)
            throw new ArgumentException("User version cannot be negative", nameof(userVersion));

        var installType = GetInstallationTypeString(installation.InstallationType);
        var gameTypeString = gameType == GameType.ZeroHour ? "zerohour" : "generals";
        var fullVersion = $"{ManifestConstants.DefaultManifestFormatVersion}.{userVersion}";

        var gamePart = $"{gameTypeString}{suffix}";
        return $"{fullVersion}.{installType}.{gamePart}";
    }

    /// <summary>
    /// Normalizes a string to lowercase alphanumeric with dots as separators.
    /// </summary>
    private static string Normalize(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return "unknown";

        var lower = input.ToLowerInvariant().Trim();

        // Replace non-alphanumeric characters (except dots) with dots
        var normalized = Regex.Replace(lower, "[^a-zA-Z0-9.]", ".");

        // Remove leading/trailing dots
        normalized = normalized.Trim('.');

        // Replace multiple consecutive dots with single dots
        normalized = Regex.Replace(normalized, "\\.+", ".");

        return string.IsNullOrEmpty(normalized) ? "unknown" : normalized;
    }

    /// <summary>
    /// Gets a string representation for GameInstallationType.
    /// </summary>
    /// <param name="installationType">The installation type enum value.</param>
    /// <returns>A stable lowercase string representation.</returns>
    private static string GetInstallationTypeString(GameInstallationType installationType)
    {
        return installationType switch
        {
            GameInstallationType.Steam => "steam",
            GameInstallationType.EaApp => "eaapp",
            GameInstallationType.TheFirstDecade => "thefirstdecade",
            GameInstallationType.CDISO => "cdiso",
            GameInstallationType.Wine => "wine",
            GameInstallationType.Retail => "retail",
            GameInstallationType.Unknown => "unknown",
            _ => throw new ArgumentOutOfRangeException(nameof(installationType), installationType, "Unknown installation type")
        };
    }

    /// <summary>
    /// Gets a string representation for ContentType.
    /// </summary>
    /// <param name="contentType">The content type enum value.</param>
    /// <returns>A stable lowercase string representation.</returns>
    private static string GetContentTypeString(ContentType contentType)
    {
        return contentType switch
        {
            ContentType.GameInstallation => "gameinstallation",
            ContentType.GameClient => "gameclient",
            ContentType.Mod => "mod",
            ContentType.Patch => "patch",
            ContentType.Addon => "addon",
            ContentType.MapPack => "mappack",
            ContentType.LanguagePack => "languagepack",
            ContentType.ContentBundle => "contentbundle",
            ContentType.PublisherReferral => "publisherreferral",
            ContentType.ContentReferral => "contentreferral",
            ContentType.Mission => "mission",
            ContentType.Map => "map",
            ContentType.UnknownContentType => "unknown",
            _ => throw new ArgumentOutOfRangeException(nameof(contentType), contentType, "Unknown content type")
        };
    }
}
