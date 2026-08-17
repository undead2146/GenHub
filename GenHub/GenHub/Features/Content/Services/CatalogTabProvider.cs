using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using GenHub.Core.Constants;
using GenHub.Core.Interfaces.Content;
using GenHub.Core.Interfaces.Providers;
using GenHub.Core.Models.Content;
using GenHub.Core.Models.Providers;
using GenHub.Core.Models.Results.Content;
using GenHub.Features.Content.Services.Catalog;
using Microsoft.Extensions.Logging;

namespace GenHub.Features.Content.Services;

/// <summary>
/// Loads dynamic custom tab definitions from remote publisher catalog manifests for the downloads browser content detail view.
/// When a user opens the detail page for a specific mod, map, or patch in the downloads section, publishers can display extra custom UI tabs (e.g. documentation, server stats, sub-addons, or custom web views) defined in their catalog json.
/// </summary>
/// <param name="subscriptionStore">The publisher subscription store.</param>
/// <param name="catalogParser">The publisher catalog parser.</param>
/// <param name="httpClientFactory">The HTTP client factory.</param>
/// <param name="logger">The logger instance.</param>
public class CatalogTabProvider(
    IPublisherSubscriptionStore subscriptionStore,
    IPublisherCatalogParser catalogParser,
    IHttpClientFactory httpClientFactory,
    ILogger<CatalogTabProvider> logger) : ITabProvider
{
    /// <inheritdoc/>
    public string ProviderId => "catalog-tabs";

    /// <inheritdoc/>
    public bool CanProvideTabsFor(ContentSearchResult searchResult)
    {
        // Verifies that the search result selected in the downloads browser has a non-empty provider name to perform catalog subscription lookups
        return !string.IsNullOrEmpty(searchResult.ProviderName);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<CustomTabDefinition>> GetTabsAsync(
        ContentSearchResult searchResult,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Generic catalog search results show a human-readable publisher name, while the
            // subscription store is keyed by the catalog publisher ID. Prefer the ID preserved
            // in resolver metadata and retain the name lookup for older/non-catalog providers.
            var publisherId = searchResult.ProviderName;
            if (searchResult.ResolverMetadata.TryGetValue(CatalogConstants.PublisherProfileJsonMetadataKey, out var publisherProfileJson))
            {
                var publisherProfile = JsonSerializer.Deserialize<PublisherProfile>(publisherProfileJson);
                if (!string.IsNullOrWhiteSpace(publisherProfile?.Id))
                {
                    publisherId = publisherProfile.Id;
                }
            }

            // Query subscription store to retrieve publisher catalog details.
            var subscriptionResult = await subscriptionStore.GetSubscriptionAsync(
                publisherId,
                cancellationToken);

            if (!subscriptionResult.Success || subscriptionResult.Data == null)
            {
                // Return empty read-only list if publisher subscription is not found or fails to resolve
                return [];
            }

            var subscription = subscriptionResult.Data;

            // Download raw publisher catalog json manifest over http
            var httpClient = httpClientFactory.CreateClient();
            var catalogJson = await CatalogDocumentReader.ReadAsync(
                httpClient,
                subscription.CatalogUrl,
                CatalogConstants.MaxCatalogSizeBytes,
                cancellationToken: cancellationToken);

            // Parse raw json into structured publisher catalog model
            var catalogResult = await catalogParser.ParseCatalogAsync(catalogJson, cancellationToken);

            if (!catalogResult.Success || catalogResult.Data?.CustomTabs == null || catalogResult.Data.CustomTabs.Count == 0)
            {
                // Return empty read-only list if publisher catalog defines no custom tabs
                return [];
            }

            // Convert parsed catalog tab definitions into runtime tab definitions for the downloads detail view
            var tabs = new List<CustomTabDefinition>();

            foreach (var catalogTab in catalogResult.Data.CustomTabs)
            {
                searchResult.ResolverMetadata.TryGetValue(CatalogConstants.CatalogContentIdMetadataKey, out var catalogContentId);
                var contentId = !string.IsNullOrWhiteSpace(catalogContentId) ? catalogContentId : searchResult.Id ?? string.Empty;

                if (catalogTab.AppliesTo is { Count: > 0 } &&
                    !catalogTab.AppliesTo.Any(a => a.Equals(contentId, StringComparison.OrdinalIgnoreCase) ||
                                                   a.Equals(searchResult.Id ?? string.Empty, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                // Parse tab content type with fallback to custom tab type if an unknown value is specified in catalog json
                if (!Enum.TryParse<TabContentType>(catalogTab.ContentType, true, out var contentType))
                {
                    logger.LogWarning("invalid content type '{ContentType}' for tab '{TabId}' in publisher '{Publisher}'", catalogTab.ContentType, catalogTab.TabId, searchResult.ProviderName);
                    contentType = TabContentType.Custom;
                }

                tabs.Add(new CustomTabDefinition
                {
                    TabId = catalogTab.TabId,
                    Header = catalogTab.Header,
                    Icon = catalogTab.Icon,
                    Order = catalogTab.Order,
                    ContentType = contentType,
                    DataSourceUrl = catalogTab.DataSourceUrl,
                    ContentTemplate = catalogTab.ContentTemplate,
                    Intro = catalogTab.Intro,
                    Cards = catalogTab.Cards?.ConvertAll(card => new CustomTabCardDefinition
                    {
                        Title = card.Title,
                        Description = card.Description,
                        ImageUrl = card.ImageUrl,
                        Label = card.Label,
                        AccentColor = card.AccentColor,
                    }) ?? [],
                    Metadata = catalogTab.Metadata,
                    IsVisible = catalogTab.IsVisible,
                    LazyLoad = catalogTab.LazyLoad,
                });
            }

            return tabs;
        }
        catch (Exception ex)
        {
            // Log error and return empty read-only list to ensure failure loading custom tabs does not crash the downloads detail view
            logger.LogError(ex, "error loading custom tabs for content '{ContentId}' from publisher '{Publisher}'", searchResult.Id, searchResult.ProviderName);
            return [];
        }
    }
}
