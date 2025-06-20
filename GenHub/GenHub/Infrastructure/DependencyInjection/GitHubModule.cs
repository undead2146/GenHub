using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Net.Http;
using System.Net.Http.Headers;

using GenHub.Core.Models;
using GenHub.Core.Interfaces;
using GenHub.Core.Interfaces.GitHub;
using GenHub.Core.Interfaces.Repositories;
using GenHub.Features.GitHub.Services;
using GenHub.Features.GitHub.Factories;
using GenHub.Features.GitHub.ViewModels;
using GenHub.Infrastructure.Security;
using GenHub.Infrastructure.Repositories;

namespace GenHub.Infrastructure.DependencyInjection
{
    /// <summary>
    /// Registration module for GitHub feature services and ViewModels
    /// </summary>
    public static class GitHubModule
    {
        /// <summary>
        /// Adds GitHub related services and ViewModels
        /// </summary>
        public static IServiceCollection AddGitHubModule(this IServiceCollection services, IConfiguration config, ILogger logger)
        {
            logger.LogInformation("Configuring GitHub services and ViewModels");

            // Register GitHub configuration
            services.Configure<GitHubRepository>(config.GetSection("GitHub"));
            logger.LogDebug("GitHub configuration registered from config section");

            // Register TokenStorageService first since other services might need it
            services.AddSingleton<ITokenStorageService, TokenStorageService>();
            logger.LogDebug("TokenStorageService registered");

            // Register GitHub Token Service early
            services.AddSingleton<IGitHubTokenService, GitHubTokenService>();
            logger.LogDebug("GitHubTokenService registered");

            // Register HttpClient for GitHub API
            services.AddHttpClient("GitHubApi", (sp, client) =>
            {
                client.BaseAddress = new Uri("https://api.github.com/");
                client.DefaultRequestHeaders.UserAgent.TryParseAdd("GenHub");
                client.DefaultRequestHeaders.Accept.TryParseAdd("application/vnd.github.v3+json");
                logger.LogDebug("GitHub API client registered (initial configuration without token)");
            });
            logger.LogDebug("GitHub API HttpClient registered");
            
            // Register GitHubCachingRepository
            services.AddSingleton<IGitHubCachingRepository, GitHubCachingRepository>();
            logger.LogDebug("GitHubCachingRepository registered");
            
            // Register the concrete GitHubApiClient implementation
            services.AddSingleton<GitHubApiClient>(sp => {
                var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
                var httpClient = httpClientFactory.CreateClient("GitHubApi");
                var apiLogger = sp.GetRequiredService<ILogger<GitHubApiClient>>();
                var tokenStorage = sp.GetRequiredService<ITokenStorageService>();
                return new GitHubApiClient(httpClient, apiLogger, tokenStorage);
            });
            
            // Register the interface with the concrete implementation
            services.AddSingleton<IGitHubApiClient>(sp => sp.GetRequiredService<GitHubApiClient>());
            logger.LogDebug("GitHubApiClient registered");
            
            // Register remaining GitHub services
            services.AddSingleton<IGitHubRepositoryManager, GitHubRepositoryManager>();
            // Register repository discovery service
            services.AddSingleton<IGitHubRepositoryDiscoveryService, GitHubRepositoryDiscoveryService>();

            services.AddSingleton<IGitHubReleaseReader, GitHubReleaseReader>();
            services.AddSingleton<IGitHubWorkflowReader, GitHubWorkflowReader>();
            services.AddSingleton<IGitHubArtifactReader, GitHubArtifactReader>();
            services.AddSingleton<IGitHubSearchService, GitHubSearchService>();
            logger.LogDebug("GitHub reader services registered");

            // Add data providers and installers
            services.AddSingleton<IGitHubViewDataProvider, GitHubViewDataProvider>();
            services.AddSingleton<IGitHubArtifactInstaller, GitHubArtifactInstaller>();
            logger.LogDebug("GitHub view data and artifact installer services registered");
            
            // Register the unified GitHubDisplayItemFactory 
            services.AddSingleton<IGitHubDisplayItemFactory, GitHubDisplayItemFactory>();
            logger.LogDebug("GitHubDisplayItemFactory registered");
            
            // Facade combines all services
            services.AddSingleton<GitHubServiceFacade>();
            services.AddSingleton<IGitHubServiceFacade>(sp => sp.GetRequiredService<GitHubServiceFacade>());
            logger.LogDebug("GitHubServiceFacade registered");

            // Register all GitHub ViewModels
            RegisterGitHubViewModels(services, logger);
            
            logger.LogInformation("GitHub services and ViewModels configured successfully");
            return services;
        }

        /// <summary>
        /// Registers all ViewModels specific to GitHub feature
        /// </summary>
        private static void RegisterGitHubViewModels(IServiceCollection services, ILogger logger)
        {
            // Register child ViewModels first (dependencies of the main orchestrator)
            services.AddSingleton<RepositoryControlViewModel>();
            services.AddSingleton<ContentModeFilterViewModel>();
            services.AddSingleton<GitHubItemsTreeViewModel>();
            services.AddSingleton<GitHubDetailsViewModel>();
            services.AddSingleton<InstallationViewModel>();
            
            // Register the main GitHub Manager ViewModel (orchestrator)
            services.AddSingleton<GitHubManagerViewModel>();
            
            // Register supporting ViewModels
            services.AddTransient<WorkflowDefinitionViewModel>();
            services.AddTransient<GitHubTokenDialogViewModel>();
            
            logger.LogDebug("GitHub ViewModels registered");
        }
    }
}
