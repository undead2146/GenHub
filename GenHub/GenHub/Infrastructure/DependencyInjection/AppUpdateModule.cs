using System;
using System.Net.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using GenHub.Core.Interfaces.AppUpdate;
using GenHub.Core.Interfaces.Caching;
using GenHub.Core.Interfaces.GitHub;
using GenHub.Core.Interfaces.Repositories;
using GenHub.Features.AppUpdate.Services;
using GenHub.Features.AppUpdate.ViewModels;
using GenHub.Features.AppUpdate.Factories;
using GenHub.Infrastructure.Repositories;

namespace GenHub.Infrastructure.DependencyInjection
{
    /// <summary>
    /// Registration module for application update services and ViewModels
    /// </summary>
    public static class AppUpdateModule
    {
        /// <summary>
        /// Adds all app update related services and ViewModels
        /// </summary>
        public static IServiceCollection AddAppUpdateModule(this IServiceCollection services, IConfiguration configuration, ILogger logger)
        {
            logger.LogInformation("Configuring app update services and ViewModels");

            // Register HttpClient for update checks with consistent configuration
            services.AddHttpClient("UpdateClient", client =>
            {
                // Use consistent user agent format across the application
                client.DefaultRequestHeaders.UserAgent.ParseAdd("GenHub/1.0");

                // Ensure proper Accept header for GitHub API
                client.DefaultRequestHeaders.Accept.Clear();
                client.DefaultRequestHeaders.Accept.Add(
                    new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/vnd.github.v3+json"));

                // Set reasonable timeout for update operations
                client.Timeout = TimeSpan.FromMinutes(2);

                // Add GitHub API token if available in configuration
                var githubToken = configuration["GitHub:ApiToken"];
                if (!string.IsNullOrEmpty(githubToken))
                {
                    client.DefaultRequestHeaders.Authorization =
                        new System.Net.Http.Headers.AuthenticationHeaderValue("token", githubToken);
                }
            })
            .ConfigurePrimaryHttpMessageHandler(() =>
            {
                // Use specific HttpClientHandler configuration to optimize performance
                return new HttpClientHandler
                {
                    // Enable compression for better performance
                    AutomaticDecompression = System.Net.DecompressionMethods.GZip | System.Net.DecompressionMethods.Deflate,

                    // Use default credentials for proxy servers if needed
                    UseDefaultCredentials = true,

                    // Set reasonable limits
                    MaxConnectionsPerServer = 20
                };
            })
            .SetHandlerLifetime(TimeSpan.FromMinutes(5)); // Keep handler alive for longer than default

            logger.LogDebug("Configured HttpClient with appropriate headers and timeouts for update checks");

            // Register app version services
            services.AddSingleton<IAppVersionService, AppVersionService>();
            logger.LogDebug("IAppVersionService registered");

            // Register version comparator
            services.AddSingleton<IVersionComparator, SemVerComparator>();
            logger.LogDebug("IVersionComparator registered (SemVerComparator)");

            // Register update installer factory and default installer
            services.AddSingleton<DefaultUpdateInstaller>();
            services.AddSingleton<UpdateInstallerFactory>();
            logger.LogDebug("Update installer components registered");

            // Register IUpdateInstaller with the factory pattern
            services.AddSingleton<IUpdateInstaller>(provider =>
            {
                try
                {
                    logger.LogDebug("Creating UpdateInstallerFactory to resolve IUpdateInstaller");
                    var factory = provider.GetRequiredService<UpdateInstallerFactory>();
                    return factory.CreateInstaller();
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error in IUpdateInstaller factory - falling back to DefaultUpdateInstaller");
                    return provider.GetRequiredService<DefaultUpdateInstaller>();
                }
            });
            logger.LogDebug("IUpdateInstaller registered with factory pattern");

            // Register app update service
            services.AddSingleton<IAppUpdateService>(provider =>
            {
                var httpClient = provider.GetRequiredService<IHttpClientFactory>().CreateClient("UpdateClient");
                var versionComparator = provider.GetRequiredService<IVersionComparator>();
                var updateInstaller = provider.GetRequiredService<IUpdateInstaller>();
                var versionService = provider.GetRequiredService<IAppVersionService>();
                var cacheService = provider.GetRequiredService<ICacheService>();
                var serviceLogger = provider.GetRequiredService<ILogger<AppUpdateService>>();
                var repositoryManager = provider.GetRequiredService<IGitHubRepositoryManager>();
                var releaseReader = provider.GetService<IGitHubReleaseReader>(); // Optional

                return new AppUpdateService(
                    httpClient,
                    versionComparator,
                    updateInstaller,
                    versionService,
                    cacheService,
                    serviceLogger,
                    repositoryManager,
                    releaseReader
                );
            });
            logger.LogDebug("IAppUpdateService registered with all dependencies");

            // Register view models for update notification
            services.AddTransient<UpdateNotificationViewModel>(provider =>
            {
                return new UpdateNotificationViewModel(
                    provider.GetRequiredService<IAppUpdateService>(),
                    provider.GetRequiredService<ILogger<UpdateNotificationViewModel>>(),
                    provider.GetRequiredService<IGitHubCachingRepository>(),
                    provider.GetRequiredService<ICacheService>()
                );
            });
            logger.LogDebug("Update ViewModels registered");

            logger.LogInformation("App update services configured successfully");
            return services;
        }
    }
}
