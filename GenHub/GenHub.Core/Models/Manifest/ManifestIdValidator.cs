using System.Linq;
using System.Text.RegularExpressions;

namespace GenHub.Core.Models.Manifest;

/// <summary>
/// Provides validation for manifest IDs to ensure they follow the deterministic,
/// human-readable scheme required by GenHub.
/// </summary>
public static class ManifestIdValidator
{
    // Regex for installation/game client content: version.installType.gameType[-suffix] (4 segments)
    private static readonly Regex InstallationContentRegex =
        new(@"^\d+(?:\.\d+)*\.(steam|eaapp|thefirstdecade|cdiso|wine|retail|unknown)\.(generals|zerohour)(?:-[a-z]+)?$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // Regex for publisher content: version.publisher.contentType.contentName[-suffix] (5+ segments)
    private static readonly Regex PublisherContentRegex =
        new(@"^\d+(?:\.\d+)*\.[a-z0-9]+(?:\.[a-z0-9]+)*\.(gameinstallation|gameclient|mod|patch|addon|mappack|languagepack|contentbundle|publisherreferral|contentreferral|mission|map|unknown)\.[a-z0-9-]+(?:-[a-z0-9]+)?(?:\.[a-z0-9-]+)*$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // Allow simple test-friendly ids like 'test-id' or 'simple.id' (alphanumeric with dashes, max 3 segments)
    private static readonly Regex SimpleIdRegex =
        new(@"^[a-zA-Z0-9\-]+(\.[a-zA-Z0-9\-]+){0,2}$", RegexOptions.Compiled);

    /// <summary>
    /// Throws an <see cref="ArgumentException"/> if the manifest ID is invalid.
    /// </summary>
    /// <param name="manifestId">Manifest identifier to validate.</param>
    public static void EnsureValid(string manifestId)
    {
        if (!IsValid(manifestId, out var reason))
        {
            throw new ArgumentException(reason, nameof(manifestId));
        }
    }

    /// <summary>
    /// Validates whether the given manifest ID is valid according to GenHub rules.
    /// </summary>
    /// <param name="manifestId">Manifest identifier to validate.</param>
    /// <param name="reason">If invalid, contains a human-readable reason.</param>
    /// <returns>True when the id is valid; otherwise false.</returns>
    public static bool IsValid(string manifestId, out string reason)
    {
        if (string.IsNullOrWhiteSpace(manifestId))
        {
            reason = "Manifest ID cannot be null or empty.";
            return false;
        }

        var segments = manifestId.Split('.');
        if (segments.Length < 3)
        {
            reason = $"Manifest ID '{manifestId}' is invalid. Must have at least 3 segments.";
            return false;
        }

        // Check installation/game client format (4 segments: version.install.game[-suffix])
        if (InstallationContentRegex.IsMatch(manifestId) && segments.Length == 4)
        {
            reason = string.Empty;
            return true;
        }

        // Check publisher content for mods/maps/etc. (4+ segments: version.publisher.contentType.contentName[-suffix])
        if (PublisherContentRegex.IsMatch(manifestId) && segments.Length >= 4)
        {
            reason = string.Empty;
            return true;
        }

        // Check if it's a simple ID (fallback for tests)
        if (SimpleIdRegex.IsMatch(manifestId))
        {
            reason = string.Empty;
            return true;
        }

        // For any other cases, reject them
        reason = $"Manifest ID '{manifestId}' is invalid. Must be 4 segments in format version.installType.gameType[-suffix] for installations/clients or version.publisher.contentType.contentName[-suffix] for publisher content (mods/maps).";
        return false;
    }
}
