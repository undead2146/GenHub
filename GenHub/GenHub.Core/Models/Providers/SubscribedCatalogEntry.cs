using System;
using System.Text.Json.Serialization;

namespace GenHub.Core.Models.Providers;

/// <summary>
/// Tracks per-catalog state within a multi-catalog subscription.
/// </summary>
public class SubscribedCatalogEntry
{
    /// <summary>
    /// Gets or sets the catalog ID within the publisher's definition.
    /// </summary>
    [JsonPropertyName("catalogId")]
    public string CatalogId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the human-readable catalog name.
    /// </summary>
    [JsonPropertyName("catalogName")]
    public string CatalogName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the URL to the catalog JSON.
    /// </summary>
    [JsonPropertyName("catalogUrl")]
    public string CatalogUrl { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the SHA256 hash of the catalog content for change detection.
    /// </summary>
    [JsonPropertyName("cachedCatalogHash")]
    public string? CachedCatalogHash { get; set; }

    /// <summary>
    /// Gets or sets when this catalog was last fetched.
    /// </summary>
    [JsonPropertyName("lastFetched")]
    public DateTime? LastFetched { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this catalog is enabled for the subscription.
    /// </summary>
    [JsonPropertyName("isEnabled")]
    public bool IsEnabled { get; set; } = true;
}
