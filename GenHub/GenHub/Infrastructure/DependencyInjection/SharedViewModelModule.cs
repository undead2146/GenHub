using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using GenHub.Common.ViewModels;
using GenHub.Core.Interfaces.UI;

namespace GenHub.Infrastructure.DependencyInjection
{
    /// <summary>
    /// Registration module for shared, application-wide ViewModels
    /// </summary>
    public static class SharedViewModelModule
    {
        /// <summary>
        /// Adds shared ViewModels for UI layer
        /// </summary>
        public static IServiceCollection AddSharedViewModelModule(this IServiceCollection services, ILogger logger)
        {
            logger.LogInformation("Configuring shared ViewModels");
            
            try
            {
                // Register MainViewModel (critical for app startup)
                services.AddSingleton<MainViewModel>();
                logger.LogDebug("MainViewModel registered (critical for startup)");
                
                // Register ViewLocator to help resolve views for ViewModels
                services.AddSingleton<ViewLocator>();
                logger.LogDebug("ViewLocator registered");
                
                // Register any other application-wide/shell ViewModels here
                
                logger.LogInformation("Shared ViewModels configured successfully");
                return services;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error in AddSharedViewModelModule");
                throw;
            }
        }
    }
}
