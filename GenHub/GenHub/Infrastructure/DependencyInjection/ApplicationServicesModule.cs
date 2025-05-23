using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;
using Microsoft.Extensions.Logging;
using GenHub.Features.GameProfiles.Services;

namespace GenHub.Infrastructure.DependencyInjection
{
    /// <summary>
    /// Main module that orchestrates registration of all application services
    /// </summary>
    public static class ApplicationServicesModule
    {
        /// <summary>
        /// Configures all application modules in the correct order
        /// </summary>
        public static IServiceCollection ConfigureApplicationServices(this IServiceCollection services, IConfiguration config)
        {
            Console.WriteLine("Configuring all application services");
            
            // Create a bootstrap logger factory for registration process
            var loggerFactory = LoggingModule.CreateBootstrapLoggerFactory();
            var logger = loggerFactory.CreateLogger("ServiceRegistration");
            
            try
            {
                // Add logging first so we can log issues with other services
                services.AddLoggingModule(config);
                logger.LogDebug("Logging module added");
                
                // Register JSON serialization options
                services.AddJsonSerializationModule(logger); 
                logger.LogDebug("JSON serialization module added");
                
                // Then add infrastructure before anything that depends on it
                services.AddCoreInfrastructureModule(config, logger);
                logger.LogDebug("Core infrastructure module added");
                
                // Add caching services
                services.AddCachingModule(config, logger);
                logger.LogDebug("Caching module added");
                
                // Add repositories before services that use them
                services.AddRepositoryModule(logger);
                logger.LogDebug("Repository module added");
                
                // Then add feature services and their ViewModels
                services.AddGitHubModule(config, logger);
                logger.LogDebug("GitHub module added");

                services.AddGameVersionModule(logger);
                logger.LogDebug("Game version module added");
                
                services.AddProfileModule(logger);
                logger.LogDebug("Profile module added");
                
                // Add app update services (must come after GitHub services)
                services.AddAppUpdateModule(config, logger);
                logger.LogDebug("App update module added");
                
                // Add shared ViewModels last since they depend on services
                services.AddSharedViewModelModule(logger);
                logger.LogDebug("Shared ViewModel module added");
                
                // Add GameProfileFactory if it's not already registered elsewhere
                services.AddSingleton<GameProfileFactory>();
                logger.LogDebug("GameProfileFactory added as fallback");
                
                logger.LogInformation("All application services configured successfully");
                return services;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error configuring application services");
                throw;
            }
        }
    }
}
