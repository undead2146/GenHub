using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using GenHub.Common.ViewModels;
using GenHub.Core.Interfaces.UI;
using GenHub.Features.Downloads.ViewModels;
using GenHub.Features.Settings.ViewModels;
using GenHub.Features.GameProfiles.ViewModels;

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
                
                // Register tab ViewModels
                services.AddSingleton<GameProfileLauncherViewModel>();
                logger.LogDebug("GameProfileLauncherViewModel registered");
                
                services.AddSingleton<DownloadsViewModel>();
                logger.LogDebug("DownloadsViewModel registered");
                
                services.AddSingleton<SettingsViewModel>();
                logger.LogDebug("SettingsViewModel registered");

                // Register ViewLocator to help resolve views for ViewModels
                services.AddSingleton<ViewLocator>();
                logger.LogDebug("ViewLocator registered");
                
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
