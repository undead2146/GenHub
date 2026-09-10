using System;
using System.Linq;
using GenHub.Core.Models.Results.Content;

namespace GenHub.Core.Messages;

/// <summary>
/// Helper for matching download message identifiers against content search results.
/// </summary>
public static class DownloadMessageMatchHelper
{
    /// <summary>
    /// Checks whether the specified download message parameters match the target content search result.
    /// </summary>
    /// <param name="contentKey">The message content key.</param>
    /// <param name="contentId">The message content ID.</param>
    /// <param name="providerName">The message provider name.</param>
    /// <param name="contentName">The message content name.</param>
    /// <param name="item">The search result to match.</param>
    /// <param name="parentContentId">Optional parent content ID for child release/addon downloads.</param>
    /// <returns>True if the message matches the item; otherwise, false.</returns>
    public static bool Matches(
        string? contentKey,
        string? contentId,
        string? providerName,
        string? contentName,
        ContentSearchResult? item,
        string? parentContentId = null)
    {
        if (item == null)
        {
            return false;
        }

        if (MatchesContentKey(contentKey, item))
        {
            return true;
        }

        if (MatchesContentId(contentId, item))
        {
            return true;
        }

        return MatchesProviderAndNameFallback(providerName, contentName, item);
    }

    private static bool MatchesContentKey(string? contentKey, ContentSearchResult item)
    {
        if (string.IsNullOrEmpty(contentKey) || string.IsNullOrEmpty(item.ProviderName))
        {
            return false;
        }

        if (!string.IsNullOrEmpty(item.Id))
        {
            var itemKeyWithId = $"{item.ProviderName}::{item.Id}";
            if (string.Equals(contentKey, itemKeyWithId, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        if (!string.IsNullOrEmpty(item.Name))
        {
            var itemKeyWithName = $"{item.ProviderName}::{item.Name}";
            if (string.Equals(contentKey, itemKeyWithName, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static bool MatchesContentId(string? contentId, ContentSearchResult item)
    {
        if (string.IsNullOrEmpty(contentId))
        {
            return false;
        }

        if (string.Equals(contentId, item.Id, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return item.Variants != null &&
               item.Variants.Any(v => string.Equals(contentId, v.ManifestId, StringComparison.OrdinalIgnoreCase));
    }

    private static bool MatchesProviderAndNameFallback(string? providerName, string? contentName, ContentSearchResult item)
    {
        if (!string.IsNullOrEmpty(item.Id))
        {
            return false;
        }

        return !string.IsNullOrEmpty(providerName) &&
               !string.IsNullOrEmpty(contentName) &&
               string.Equals(providerName, item.ProviderName, StringComparison.OrdinalIgnoreCase) &&
               string.Equals(contentName, item.Name, StringComparison.OrdinalIgnoreCase);
    }
}
