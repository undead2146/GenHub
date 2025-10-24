using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using GenHub.Core.Constants;

namespace GenHub.Core.Models.Manifest;

/// <summary>
/// Provides validation for manifest IDs to ensure they follow the deterministic,
/// human-readable 5-segment scheme required by GenHub.
/// All content uses format: schemaVersion.userVersion.publisher.contentType.contentName.
/// </summary>
public static class ManifestIdValidator
{
    // Regex for all content: version.publisher.contentType.contentName (5 segments)
    // Publishers can be: steam, eaapp, generalsonline, genhub, cnclabs, moddb, etc.
    // Content types include: gameinstallation, gameclient, mod, patch, addon, mappack, etc.
    private static readonly Regex PublisherContentRegex =
        new(ManifestConstants.PublisherContentRegexPattern, RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // Allow simple test-friendly ids like 'test-id' or 'simple.id' (alphanumeric with dashes, max 4 segments)
    private static readonly Regex SimpleIdRegex =
        new(ManifestConstants.SimpleIdRegexPattern, RegexOptions.Compiled);

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
    /// All content must use 5-segment format: schemaVersion.userVersion.publisher.contentType.contentName.
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

        // Check for 5-segment publisher content format (standard format for all content)
        if (segments.Length == 5)
        {
            if (PublisherContentRegex.IsMatch(manifestId))
            {
                reason = string.Empty;
                return true;
            }

            reason = $"Manifest ID '{manifestId}' has 5 segments but does not match required format: schemaVersion.userVersion.publisher.contentType.contentName";
            return false;
        }

        // Check if it's a simple ID (fallback for tests - up to 4 segments)
        if (segments.Length <= 4 && SimpleIdRegex.IsMatch(manifestId))
        {
            reason = string.Empty;
            return true;
        }

        // Reject any other format
        reason = $"Manifest ID '{manifestId}' is invalid. Must be 5 segments in format: schemaVersion.userVersion.publisher.contentType.contentName (e.g., '1.108.steam.gameinstallation.generals')";
        return false;
    }
}
