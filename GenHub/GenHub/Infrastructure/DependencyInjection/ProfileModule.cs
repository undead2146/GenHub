using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using GenHub.Core.Interfaces;
using GenHub.Features.GameProfiles.Services;
using GenHub.Features.GameProfiles.ViewModels;
using Avalonia.Controls;

namespace GenHub.Infrastructure.DependencyInjection
{
    /// <summary>
    /// Registration module for game profile services and ViewModels
    /// </summary>
    public static class ProfileModule
    {
        /// <summary>
        /// Adds profile-related services and ViewModels to the DI container
        /// </summary>
        public static IServiceCollection AddProfileModule(this IServiceCollection services, ILogger logger)
        {
            logger.LogInformation("Configuring profile services and ViewModels");

            // Add profile management services
            services.AddSingleton<IGameProfileManagerService, GameProfileManagerService>();
            logger.LogDebug("GameProfileManagerService registered");
            
            services.AddSingleton<IGameProfileFactory, GameProfileFactory>();
            logger.LogDebug("GameProfileFactory registered");
            
            services.AddSingleton<ProfileMetadataService>();
            logger.LogDebug("ProfileMetadataService registered");
            
            // Add profile resources service
            services.AddSingleton<ProfileResourceService>();
            logger.LogDebug("ProfileResourceService registered");
            
            // Add profile settings data provider
            services.AddSingleton<IProfileSettingsDataProvider, ProfileSettingsDataProvider>();
            logger.LogDebug("ProfileSettingsDataProvider registered");

            // Register profile-specific ViewModels
            services.AddTransient<GameProfileItemViewModel>();
            services.AddSingleton<GameProfileLauncherViewModel>();
            
            // GameProfileSettingsViewModel requires special registration with owner window
            services.AddTransient<GameProfileSettingsViewModel>(provider =>
            {
                var profileSettingsDataProvider = provider.GetRequiredService<IProfileSettingsDataProvider>();
                var gameProfileManagerService = provider.GetRequiredService<IGameProfileManagerService>();
                var loggerGameProfileSettingsViewModel = provider.GetRequiredService<ILogger<GameProfileSettingsViewModel>>();
                
                var ownerWindow = provider.GetService<Window>();

                return new GameProfileSettingsViewModel(
                    null, // profile is set when needed
                    ownerWindow // ownerWindow
                );
            });
            logger.LogDebug("Profile ViewModels registered");
            
            logger.LogInformation("Profile services and ViewModels configured successfully");
            return services;
        }
    }
}
