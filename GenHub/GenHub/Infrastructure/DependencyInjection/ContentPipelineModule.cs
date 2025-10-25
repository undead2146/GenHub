using System;
using System.Net.Http;
using GenHub.Common.Services;
using GenHub.Core.Constants;
using GenHub.Core.Interfaces.Common;
using GenHub.Core.Interfaces.Content;
using GenHub.Core.Interfaces.GitHub;
using GenHub.Core.Interfaces.Manifest;
using GenHub.Core.Options;
using GenHub.Features.Content.Services;
using GenHub.Features.Content.Services.ContentDeliverers;
using GenHub.Features.Content.Services.ContentDiscoverers;
using GenHub.Features.Content.Services.ContentProviders;
using GenHub.Features.Content.Services.ContentResolvers;
using GenHub.Features.GitHub.Factories;
using GenHub.Features.GitHub.Services;
using GenHub.Features.Manifest;
using GenHub.Plugins.GeneralsOnline;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Octokit;

namespace GenHub.Infrastructure.DependencyInjection;

/// <summary>
/// Provides extension methods for registering content pipeline services.
/// </summary>
public static class ContentPipelineModule
{
    /// <summary>
    /// Configures GitHub options with default values.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <returns>The updated service collection.</returns>
    public static IServiceCollection ConfigureGitHubOptions(this IServiceCollection services)
    {
        services.Configure<GitHubOptions>(options =>
        {
            options.ApiBaseUrl = "https://api.github.com";
            options.ProductHeader = AppConstants.AppName;
            options.TokenEnvironmentVariable = "GITHUB_TOKEN";
            options.DefaultTimeoutSeconds = 30;
        });
        return services;
    }

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

        // Configure HttpClient for GitHub API
        services.AddHttpClient("GitHubApi", client =>
        {
            client.DefaultRequestHeaders.Add("User-Agent", "GenHub/1.0");
            client.Timeout = TimeSpan.FromMinutes(5);
        });

        // Register GitHub client (required by OctokitGitHubApiClient)
        services.AddSingleton<IGitHubClient>(provider =>
        {
            var options = provider.GetRequiredService<IOptions<GitHubOptions>>();
            return new GitHubClient(new ProductHeaderValue(options.Value.ProductHeader));
        });

        // Register GitHub API client with automatic token loading
        services.AddSingleton<IGitHubApiClient>(provider =>
        {
            var gitHubClient = provider.GetRequiredService<IGitHubClient>();
            var httpClientFactory = provider.GetRequiredService<IHttpClientFactory>();
            var logger = provider.GetService<ILogger<OctokitGitHubApiClient>>();
            var options = provider.GetRequiredService<IOptions<GitHubOptions>>();
            var client = new OctokitGitHubApiClient(gitHubClient, httpClientFactory, logger!);

            // Automatically configure GitHub token from environment if available
            var token = Environment.GetEnvironmentVariable(options.Value.TokenEnvironmentVariable);
            if (!string.IsNullOrWhiteSpace(token))
            {
                try
                {
                    var secureToken = new System.Security.SecureString();
                    foreach (char c in token)
                    {
                        secureToken.AppendChar(c);
                    }
                    client.SetAuthenticationToken(secureToken);

                    logger?.LogInformation("GitHub authentication configured from environment variable");
                }
                catch (Exception ex)
                {
                    logger?.LogWarning(ex, "Failed to set GitHub token from environment variable");
                }
            }

            return client;
        });

        // Register GitHub service facade
        services.AddScoped<IGitHubServiceFacade, GitHubServiceFacade>();

        // Register GitHub display item factory
        services.AddTransient<GitHubDisplayItemFactory>();

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
