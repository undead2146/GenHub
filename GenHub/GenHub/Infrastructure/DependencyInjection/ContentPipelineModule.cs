using GenHub.Common.Services;
using GenHub.Core.Interfaces.Common;
using GenHub.Core.Interfaces.Content;
using GenHub.Core.Interfaces.GitHub;
using GenHub.Core.Interfaces.Manifest;
using GenHub.Features.Content.Services;
using GenHub.Features.Content.Services.ContentDeliverers;
using GenHub.Features.Content.Services.ContentDiscoverers;
using GenHub.Features.Content.Services.ContentProviders;
using GenHub.Features.Content.Services.ContentResolvers;
using GenHub.Features.GitHub.Services;
using GenHub.Features.Manifest;
using GenHub.Plugins.GeneralsOnline;
using Microsoft.Extensions.DependencyInjection;

namespace GenHub.Infrastructure.DependencyInjection;

/// <summary>
/// Provides extension methods for registering content pipeline services.
/// </summary>
public static class ContentPipelineModule
{
    /// <summary>
    /// Registers content pipeline services for dependency injection.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <returns>The updated service collection.</returns>
    public static IServiceCollection AddContentPipelineServices(this IServiceCollection services)
    {
        // Register core hash provider
        var hashProvider = new Sha256HashProvider();
        services.AddSingleton<IFileHashProvider>(hashProvider);
        services.AddSingleton<IStreamHashProvider>(hashProvider);

        // Register memory cache
        services.AddMemoryCache();

        // Register core storage and manifest services
        services.AddSingleton<IContentStorageService, ContentStorageService>();
        services.AddScoped<IContentManifestPool, ContentManifestPool>();

        // Register core orchestrator
        services.AddSingleton<IContentOrchestrator, ContentOrchestrator>();

        // Register cache
        services.AddSingleton<IDynamicContentCache, MemoryDynamicContentCache>();

        // Register GitHub API client
        services.AddSingleton<IGitHubApiClient, OctokitGitHubApiClient>();

        // Register concrete content providers only
        services.AddTransient<IContentProvider, GitHubContentProvider>();
        services.AddTransient<IContentProvider, CNCLabsContentProvider>();

        // TODO: Implement ModDB discoverer and resolver
        // services.AddTransient<IContentProvider, ModDBContentProvider>();
        services.AddTransient<IContentProvider, LocalFileSystemContentProvider>();
        
        // Register Generals Online provider
        services.AddTransient<IContentProvider, GeneralsOnlineProvider>();

        // Register content discoverers
        services.AddTransient<IContentDiscoverer, GitHubDiscoverer>();
        services.AddTransient<IContentDiscoverer, GitHubReleasesDiscoverer>();
        services.AddTransient<IContentDiscoverer, CNCLabsMapDiscoverer>();
        services.AddTransient<IContentDiscoverer, FileSystemDiscoverer>();
        
        // Register Generals Online discoverer
        services.AddTransient<IContentDiscoverer, GeneralsOnlineDiscoverer>();

        // Register content resolvers
        services.AddTransient<IContentResolver, GitHubResolver>();
        services.AddTransient<IContentResolver, CNCLabsMapResolver>();
        services.AddTransient<IContentResolver, LocalManifestResolver>();
        
        // Register Generals Online resolver
        services.AddTransient<IContentResolver, GeneralsOnlineResolver>();

        // Register content deliverers
        services.AddTransient<IContentDeliverer, HttpContentDeliverer>();
        services.AddTransient<IContentDeliverer, FileSystemDeliverer>();

        return services;
    }
}
