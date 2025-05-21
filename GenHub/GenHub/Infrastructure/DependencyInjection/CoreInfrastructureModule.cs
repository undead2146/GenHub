using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using GenHub.Infrastructure.Caching;
using System;
using System.Net.Http;
using Microsoft.Extensions.Logging;
using GenHub.Core.Interfaces.UI;
using GenHub.Features.UI;

namespace GenHub.Infrastructure.DependencyInjection
{
    /// <summary>
    /// Registration module for core infrastructure services
    /// </summary>
    public static class CoreInfrastructureModule
    {
        /// <summary>
        /// Adds all core infrastructure services that are shared across platforms
        /// </summary>
        public static IServiceCollection AddCoreInfrastructureModule(this IServiceCollection services, IConfiguration configuration, ILogger logger)
        {
            logger.LogInformation("Configuring core infrastructure services");
            
            // Register general-purpose HttpClient
            services.AddHttpClient();
            services.AddSingleton<HttpClient>();
            logger.LogDebug("HttpClient registered as singleton");
            
            // Register core UI services that are used application-wide
            services.AddSingleton<IDialogService, DialogService>();
            logger.LogDebug("IDialogService registered");
            
            // Add other infrastructure services as needed
            // TODO: Add any additional core services like notification service, etc.
            
            logger.LogInformation("Core infrastructure services configured successfully");
            return services;
        }
    }
}
