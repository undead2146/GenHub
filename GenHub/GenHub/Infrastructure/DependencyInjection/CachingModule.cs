using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.IO;
using System;
using Microsoft.Extensions.Logging;
using GenHub.Infrastructure.Caching;
using GenHub.Core.Interfaces.Caching;

namespace GenHub.Infrastructure.DependencyInjection
{
    /// <summary>
    /// Registration module for caching services
    /// </summary>
    public static class CachingModule
    {
        /// <summary>
        /// Adds caching services to the service collection
        /// </summary>
        public static IServiceCollection AddCachingModule(this IServiceCollection services, IConfiguration configuration, ILogger logger)
        {
            logger.LogInformation("Configuring caching services");
            
            // Get cache directory from config if available, otherwise use default
            var cacheDir = configuration.GetSection("Caching")?.GetValue<string>("CacheDirectory");
            
            if (string.IsNullOrWhiteSpace(cacheDir))
            {
                cacheDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "GenHub", 
                    "cache");
                logger.LogDebug("Using default cache directory: {CacheDirectory}", cacheDir);
            }
            else
            {
                logger.LogDebug("Using configured cache directory: {CacheDirectory}", cacheDir);
            }
            
            // Ensure directory exists
            Directory.CreateDirectory(cacheDir);
            
            // Register the cache service as a singleton
            services.AddSingleton<ICacheService, CachingService>(provider =>
            {
                // Get a logger specifically for CachingService
                var cacheLogger = provider.GetRequiredService<ILogger<CachingService>>();
                
                // Create the caching service with only the logger parameter
                return new CachingService(cacheLogger);
            });
            
            logger.LogDebug("ICacheService registered");
            logger.LogInformation("Caching services configured successfully");
            return services;
        }
    }
}
