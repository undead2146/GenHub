using Microsoft.Extensions.DependencyInjection;
using System;
using GenHub.Core.Interfaces.Repositories;
using GenHub.Infrastructure.Repositories;
using Microsoft.Extensions.Logging;

namespace GenHub.Infrastructure.DependencyInjection
{
    /// <summary>
    /// Registration module for data repositories
    /// </summary>
    public static class RepositoryModule
    {
        /// <summary>
        /// Adds all repositories to the service collection
        /// </summary>
        public static IServiceCollection AddRepositoryModule(this IServiceCollection services, ILogger logger)
        {
            logger.LogInformation("Configuring data repositories");

            // Register base data repository first (needed by other repositories)
            services.AddSingleton<IDataRepository, DataRepository>();
            logger.LogDebug("Base DataRepository registered");
            
            // Register repository services
            services.AddSingleton<IGitHubCachingRepository, GitHubCachingRepository>();
            logger.LogDebug("GitHubCachingRepository registered");
            
            // Register the IGameVersionRepository
            services.AddSingleton<IGameVersionRepository, GameVersionRepository>();
            logger.LogDebug("GameVersionRepository registered");
            
            // Register other repositories
            services.AddSingleton<IGameProfileRepository, GameProfileRepository>();
            logger.LogDebug("GameProfileRepository registered");
            
            logger.LogInformation("Data repositories configured successfully");
            return services;
        }
    }
}
