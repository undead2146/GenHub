using System.Security.Cryptography;
using System.Text;
using GenHub.Core.Constants;
using GenHub.Core.Helpers;
using GenHub.Core.Models.Enums;
using GenHub.Core.Models.Manifest;

namespace GenHub.Core.Models.Providers;

/// <summary>
/// Shared catalog identity helpers so discoverer search-result IDs, acquired manifest IDs,
/// and declared dependency IDs are generated from the same inputs.
/// </summary>
public static class CatalogManifestIdentity
{
    /// <summary>
    /// Builds a 5-segment publisher content ID from catalog coordinates.
    /// </summary>
    /// <param name="publisherId">Catalog publisher id (e.g. <c>genhub-test-publishers</c>).</param>
    /// <param name="contentType">The catalog item's content type.</param>
    /// <param name="catalogContentId">Stable catalog content id, not the display name.</param>
    /// <param name="version">Release version or version constraint (operators are stripped).</param>
    /// <returns>A normalized manifest identifier.</returns>
    public static string CreateContentId(
        string publisherId,
        ContentType contentType,
        string catalogContentId,
        string? version)
    {
        return ManifestIdGenerator.GeneratePublisherContentId(
            publisherId,
            contentType,
            catalogContentId,
            ExtractVersionNumber(version));
    }

    /// <summary>
    /// Builds a variant-specific catalog ID by folding the variant label into the content-name segment.
    /// </summary>
    /// <param name="publisherId">Catalog publisher id.</param>
    /// <param name="contentType">The catalog item's content type.</param>
    /// <param name="catalogContentId">Stable catalog content id.</param>
    /// <param name="variantLabel">Variant label (e.g. <c>720p</c>).</param>
    /// <param name="version">Release version.</param>
    /// <param name="variantAxis">Optional variant axis name (e.g. <c>Quality</c>).</param>
    /// <returns>A normalized manifest identifier unique to this variant.</returns>
    public static string CreateVariantContentId(
        string publisherId,
        ContentType contentType,
        string catalogContentId,
        string variantLabel,
        string? version,
        string? variantAxis = null)
    {
        var variantSuffix = string.IsNullOrWhiteSpace(variantAxis)
            ? variantLabel
            : $"{variantAxis}-{variantLabel}";

        return CreateContentId(
            publisherId,
            contentType,
            $"{catalogContentId}-{variantSuffix}",
            version);
    }

    /// <summary>
    /// Resolves the declared publisher type / native pipeline for a catalog item.
    /// Returns an allowlisted publisher type or defaults to <see cref="CatalogConstants.GenericCatalogResolverId"/>.
    /// </summary>
    /// <param name="item">The catalog content item.</param>
    /// <returns>The normalized publisher type string.</returns>
    public static string ResolveDeclaredPublisherType(CatalogContentItem? item)
    {
        if (item != null && !string.IsNullOrWhiteSpace(item.PublisherType))
        {
            var raw = item.PublisherType.Trim();
            if (raw.Equals(CatalogConstants.GenericCatalogResolverId, StringComparison.OrdinalIgnoreCase) ||
                raw.Equals(PublisherTypeConstants.TheSuperHackers, StringComparison.OrdinalIgnoreCase) ||
                raw.Equals(CommunityOutpostConstants.PublisherType, StringComparison.OrdinalIgnoreCase) ||
                raw.Equals(PublisherTypeConstants.GeneralsOnline, StringComparison.OrdinalIgnoreCase) ||
                raw.Equals(PublisherTypeConstants.GitHub, StringComparison.OrdinalIgnoreCase) ||
                raw.Equals(PublisherTypeConstants.ModDB, StringComparison.OrdinalIgnoreCase))
            {
                return raw.ToLowerInvariant();
            }
        }

        return CatalogConstants.GenericCatalogResolverId;
    }

    /// <summary>
    /// Converts a hyphen- or dot-separated slug into a human-readable title.
    /// </summary>
    /// <param name="contentId">The raw content identifier slug.</param>
    /// <returns>A title-cased display name.</returns>
    public static string HumanizeContentId(string contentId)
    {
        if (string.IsNullOrWhiteSpace(contentId))
        {
            return string.Empty;
        }

        var words = contentId.Split(['-', '.', '_'], StringSplitOptions.RemoveEmptyEntries);
        return string.Join(" ", words.Select(w =>
            w.Length > 0 ? char.ToUpperInvariant(w[0]) + w[1..] : w));
    }

    /// <summary>
    /// Strips constraint operators (<c>&gt;=</c>, <c>^</c>, etc.) so version hashing matches the
    /// release version the discoverer used.
    /// </summary>
    /// <param name="constraint">A raw version or constraint string.</param>
    /// <returns>The bare version token, or <c>0</c> when empty.</returns>
    public static string StripVersionConstraint(string? constraint)
    {
        if (string.IsNullOrWhiteSpace(constraint))
        {
            return "0";
        }

        var value = constraint.Trim();
        while (value.Length > 0 && value[0] is '>' or '<' or '=' or '^' or '~')
        {
            value = value[1..].TrimStart();
        }

        return string.IsNullOrWhiteSpace(value) ? "0" : value;
    }

    /// <summary>
    /// Converts a version or constraint into the integer segment used by manifest IDs.
    /// Handles semantic versions (1.04 -> 104, 1.3 -> 103), date-based versions (2026.07.31 -> 20260731,
    /// 2026-08-02 -> 20260802), weekly tags (weekly-2026-07-31 -> 20260731), and direct integers.
    /// </summary>
    /// <param name="version">Release version or constraint.</param>
    /// <returns>A deterministic non-negative integer.</returns>
    public static int ExtractVersionNumber(string? version)
    {
        var cleanVersion = StripVersionConstraint(version).Trim();
        if (string.IsNullOrWhiteSpace(cleanVersion) || cleanVersion == "0")
        {
            return 0;
        }

        if (cleanVersion.StartsWith("weekly-", StringComparison.OrdinalIgnoreCase))
        {
            cleanVersion = cleanVersion["weekly-".Length..].Trim();
        }
        else
        {
            cleanVersion = cleanVersion.TrimStart('v', 'V').Trim();
        }

        try
        {
            // 1. Try date parse with delimiters: "2026.07.31", "2026-08-02", "02-08-2026", "2026/08/02"
            if (cleanVersion.Contains('.') || cleanVersion.Contains('-') || cleanVersion.Contains('/'))
            {
                var delims = new[] { '.', '-', '/' };
                var parts = cleanVersion.Split(delims, StringSplitOptions.RemoveEmptyEntries);

                // YYYY-MM-DD or YYYY.MM.DD or semver X.Y.Z (3 parts)
                if (parts.Length == 3 &&
                    int.TryParse(parts[0], out var p0) &&
                    int.TryParse(parts[1], out var p1) &&
                    int.TryParse(parts[2], out var p2))
                {
                    // Check if parts[0] is year (e.g. 2026.07.31 or 2026-08-02)
                    if (p0 >= 1990 && p0 <= 2100 && p1 >= 1 && p1 <= 12 && p2 >= 1 && p2 <= 31)
                    {
                        return (p0 * 10000) + (p1 * 100) + p2;
                    }

                    // Check if parts[2] is year (e.g. 02-08-2026 -> day 2, month 8, year 2026)
                    if (p2 >= 1990 && p2 <= 2100 && p1 >= 1 && p1 <= 12 && p0 >= 1 && p0 <= 31)
                    {
                        return (p2 * 10000) + (p1 * 100) + p0;
                    }

                    // Standard 3-part semantic version (e.g. 1.0.0 -> 10000, 1.2.3 -> 10203)
                    if (p0 >= 0 && p1 >= 0 && p2 >= 0)
                    {
                        var val = ((long)p0 * 10000) + ((long)p1 * 100) + p2;
                        if (val <= int.MaxValue)
                        {
                            return (int)val;
                        }
                    }
                }

                // Standard 4-part semantic version (e.g. 1.0.0.0 -> 10000)
                if (parts.Length == 4 &&
                    int.TryParse(parts[0], out var m0) && m0 >= 0 &&
                    int.TryParse(parts[1], out var m1) && m1 >= 0 &&
                    int.TryParse(parts[2], out var m2) && m2 >= 0 &&
                    int.TryParse(parts[3], out var m3) && m3 >= 0)
                {
                    var val = ((long)m0 * 10000) + ((long)m1 * 100) + m2;
                    if (val <= int.MaxValue)
                    {
                        return (int)val;
                    }
                }

                // Standard 2-part semantic version (e.g. 1.04 -> 104, 1.3 -> 103, 8.9 -> 809)
                if (parts.Length == 2 &&
                    int.TryParse(parts[0], out var major) && major >= 0 &&
                    int.TryParse(parts[1], out var minor) && minor >= 0)
                {
                    var normalized = $"{major}{minor.ToString().PadLeft(2, '0')}";
                    if (int.TryParse(normalized, out var dotted) && dotted >= 0)
                    {
                        return dotted;
                    }
                }
            }

            // 2. Try Generals Online / underscore composite format (e.g. 081326_QFE2)
            if (cleanVersion.Contains('_'))
            {
                var goVersion = GameVersionHelper.GetGeneralsOnlineManifestIdComponent(cleanVersion);
                if (goVersion > 0)
                {
                    return goVersion;
                }
            }

            // 3. Try integer directly (e.g. 20260802, 104, 0, 5)
            if (int.TryParse(cleanVersion, out var intVersion) && intVersion >= 0)
            {
                return intVersion;
            }
        }
        catch
        {
            // Fall through to hash-based approach
        }

        var bytes = Encoding.UTF8.GetBytes(cleanVersion);
        var hash = MD5.HashData(bytes);
        return (int)((uint)BitConverter.ToInt32(hash, 0) % 1_000_000);
    }

    /// <summary>
    /// Detects a semantic base-game dependency (EA/any Zero Hour or Generals installation).
    /// </summary>
    /// <param name="dependency">The catalog dependency.</param>
    /// <returns><see langword="true"/> when this is a GameInstallation type constraint.</returns>
    public static bool IsBaseGameDependency(CatalogDependency dependency)
    {
        ArgumentNullException.ThrowIfNull(dependency);

        if (!string.IsNullOrWhiteSpace(dependency.ContentType) &&
            Enum.TryParse<ContentType>(dependency.ContentType, ignoreCase: true, out var declared) &&
            declared == ContentType.GameInstallation)
        {
            return true;
        }

        var publisher = dependency.PublisherId ?? string.Empty;
        var contentId = dependency.ContentId ?? string.Empty;
        var isEaOrAny = publisher.Equals("ea", StringComparison.OrdinalIgnoreCase) ||
                        publisher.Equals("any", StringComparison.OrdinalIgnoreCase);
        if (!isEaOrAny)
        {
            return false;
        }

        return contentId.Equals("zerohour", StringComparison.OrdinalIgnoreCase) ||
               contentId.Equals("generals", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Resolves the content type a catalog dependency should use when minting its manifest ID.
    /// </summary>
    /// <param name="dependency">The catalog dependency.</param>
    /// <param name="parent">The content item that declared the dependency.</param>
    /// <param name="catalogItems">Optional catalog index keyed by content id.</param>
    /// <returns>The content type to encode in the dependency ID.</returns>
    public static ContentType ResolveDependencyContentType(
        CatalogDependency dependency,
        CatalogContentItem parent,
        IReadOnlyDictionary<string, CatalogContentItem>? catalogItems = null)
    {
        ArgumentNullException.ThrowIfNull(dependency);
        ArgumentNullException.ThrowIfNull(parent);

        if (!string.IsNullOrWhiteSpace(dependency.ContentType) &&
            Enum.TryParse<ContentType>(dependency.ContentType, ignoreCase: true, out var declared))
        {
            return declared;
        }

        if (IsBaseGameDependency(dependency))
        {
            return ContentType.GameInstallation;
        }

        if (catalogItems != null &&
            !string.IsNullOrWhiteSpace(dependency.ContentId) &&
            catalogItems.TryGetValue(dependency.ContentId, out var sibling))
        {
            return sibling.ContentType;
        }

        // A game client's undeclared leftover dependency is on the base game it requires.
        if (parent.ContentType == ContentType.GameClient)
        {
            return ContentType.GameInstallation;
        }

        return ContentType.Mod;
    }
}
