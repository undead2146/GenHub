using System;
using System.Linq;
using GenHub.Core.Models.Results.Content;

namespace GenHub.Core.Messages;

/// <summary>
/// Message broadcast when an in-flight content download completes (success, cancellation, or failure).
/// </summary>
public sealed record ContentDownloadCompletedMessage(
    string ContentKey,
    string? ContentId,
    string? ProviderName,
    string? ContentName,
    bool Success,
    string? ErrorMessage = null)
{
    /// <summary>
    /// Checks whether this download message matches the specified content item.
    /// </summary>
    /// <param name="item">The search result to match.</param>
    /// <returns>True if the message matches the item; otherwise false.</returns>
    public bool Matches(ContentSearchResult? item)
    {
        if (item == null)
        {
            return false;
        }

        if (!string.IsNullOrEmpty(ContentKey) &&
            !string.IsNullOrEmpty(item.ProviderName))
        {
            var itemKeyWithId = !string.IsNullOrEmpty(item.Id) ? $"{item.ProviderName}::{item.Id}" : null;
            if (itemKeyWithId != null && string.Equals(ContentKey, itemKeyWithId, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            var itemKeyWithName = !string.IsNullOrEmpty(item.Name) ? $"{item.ProviderName}::{item.Name}" : null;
            if (itemKeyWithName != null && string.Equals(ContentKey, itemKeyWithName, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        if (!string.IsNullOrEmpty(ContentId))
        {
            if (string.Equals(ContentId, item.Id, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (item.Variants != null && item.Variants.Any(v => string.Equals(ContentId, v.ManifestId, StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }
        }

        if (!string.IsNullOrEmpty(ProviderName) && !string.IsNullOrEmpty(ContentName) &&
            string.Equals(ProviderName, item.ProviderName, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(ContentName, item.Name, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }
}
