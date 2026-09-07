using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using GenHub.Common.Services;
using GenHub.Core.Constants;
using GenHub.Core.Interfaces.Common;
using GenHub.Core.Interfaces.Content;
using GenHub.Core.Interfaces.GitHub;
using GenHub.Core.Interfaces.Manifest;
using GenHub.Core.Interfaces.Parsers;
using GenHub.Core.Interfaces.Providers;
using GenHub.Core.Interfaces.Publishers;
using GenHub.Core.Interfaces.Storage;
using GenHub.Core.Interfaces.Tools;
using GenHub.Core.Services.Content;
using GenHub.Core.Services.Providers;
using GenHub.Core.Services.Providers.VersionSchemes;
using GenHub.Core.Services.Publishers;
using GenHub.Features.Content.Services;
using GenHub.Features.Content.Services.Catalog;
using GenHub.Features.Content.Services.Common;
using GenHub.Features.Content.Services.CommunityOutpost;
using GenHub.Features.Content.Services.ContentDeliverers;
using GenHub.Features.Content.Services.ContentDiscoverers;
using GenHub.Features.Content.Services.ContentResolvers;
using GenHub.Features.Content.Services.GeneralsOnline;
using GenHub.Features.Content.Services.GitHub;
using GenHub.Features.Content.Services.Publishers;
using GenHub.Infrastructure.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace GenHub.Infrastructure.DependencyInjection;

/// <summary>
/// Registers content pipeline services, discoverers, resolvers, deliverers, and parsers.
/// </summary>
public static class ContentPipelineModule
{
    /// <summary>
    /// Registers all content pipeline related services with the DI container.
    /// </summary>
    /// <param name="services">The service collection to register with.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddContentPipeline(this IServiceCollection services)
    {
        // Core Content Services
        services.AddSingleton<IContentDeliveryService, ContentDeliveryService>();
        services.AddSingleton<IContentDiscoveryService, ContentDiscoveryService>();
        services.AddSingleton<IContentResolutionService, ContentResolutionService>();
        services.AddSingleton<IContentCacheService, ContentCacheService>();
        services.AddSingleton<IPublisherCatalogParser, PublisherCatalogParser>();
        services.AddSingleton<ICrossPublisherDependencyResolver, CrossPublisherDependencyResolver>();

        // Catalog & Subscription Services
        services.AddSingleton<IPublisherSubscriptionStore, PublisherSubscriptionStore>();
        services.AddSingleton<IPublisherDefinitionService, PublisherDefinitionService>();
        services.AddSingleton<GenericCatalogManifestFactory>();
        services.AddSingleton<GenericCatalogResolver>();
        services.AddSingleton<GenericCatalogDiscoverer>();

        // Helpers & Processors
        services.AddSingleton<ArchivePayloadProcessor>();

        // Discoverers
        services.AddSingleton<IContentDiscoverer, GitHubReleasesDiscoverer>();
        services.AddSingleton<IContentDiscoverer, GitHubTopicsDiscoverer>();
        services.AddSingleton<IContentDiscoverer, GeneralsOnlineDiscoverer>();
        services.AddSingleton<IContentDiscoverer, CommunityOutpostDiscoverer>();
        services.AddSingleton<IContentDiscoverer, CsvDiscoverer>();
        services.AddSingleton<IContentDiscoverer>(sp => sp.GetRequiredService<GenericCatalogDiscoverer>());

        // Resolvers
        services.AddSingleton<IContentResolver, GitHubResolver>();
        services.AddSingleton<IContentResolver, GeneralsOnlineResolver>();
        services.AddSingleton<IContentResolver, CommunityOutpostResolver>();
        services.AddSingleton<IContentResolver, CsvResolver>();
        services.AddSingleton<IContentResolver>(sp => sp.GetRequiredService<GenericCatalogResolver>());

        // Deliverers
        services.AddSingleton<IContentDeliverer, HttpContentDeliverer>();
        services.AddSingleton<IContentDeliverer, GitHubReleaseDeliverer>();

        // Parsers
        services.AddSingleton<IGeneralsOnlineJsonCatalogParser, GeneralsOnlineJsonCatalogParser>();
        services.AddSingleton<IGenPatcherDatCatalogParser, GenPatcherDatCatalogParser>();
        services.AddSingleton<ICsvCatalogParser, CsvCatalogParser>();

        // Content Providers
        services.AddSingleton<IContentProvider, GeneralsOnlineContentProvider>();
        services.AddSingleton<IContentProvider, CommunityOutpostContentProvider>();
        services.AddSingleton<IContentProvider, SuperHackersProvider>();

        // Content Manifest Factories
        services.AddSingleton<IContentManifestFactory, GeneralsOnlineManifestFactory>();
        services.AddSingleton<IContentManifestFactory, CommunityOutpostManifestFactory>();
        services.AddSingleton<IContentManifestFactory, SuperHackersManifestFactory>();
        services.AddSingleton<IContentManifestFactory>(sp => sp.GetRequiredService<GenericCatalogManifestFactory>());

        // Version Schemes
        services.AddSingleton<IVersionScheme, SemanticVersionScheme>();
        services.AddSingleton<IVersionScheme, MmddyyQfeVersionScheme>();

        return services;
    }
}
