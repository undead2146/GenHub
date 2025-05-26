using System.IO;
using System;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

using GenHub.Core.Interfaces;
using GenHub.Features.GameVersions.Services;
using GenHub.Core.Interfaces.GameVersions;


namespace GenHub.Infrastructure.DependencyInjection
{
    /// <summary>
    /// Registration module for game version services and ViewModels
    /// </summary>
    public static class GameVersionModule
    {
        /// <summary>
        /// Registers game version services and ViewModels with the DI container
        /// </summary>
        public static IServiceCollection AddGameVersionModule(this IServiceCollection services, ILogger logger)
        {
            logger.LogInformation("Configuring game version services and ViewModels");
            
            // Register game executable locator
            services.AddSingleton<IGameExecutableLocator, GameExecutableLocator>();
            logger.LogDebug("GameExecutableLocator registered");
            
            // Register the game version manager and other related services
            services.AddSingleton<IGameVersionManager, GameVersionManager>();
            services.AddSingleton<IGameVersionDiscoveryService, GameVersionDiscoveryService>();
            services.AddSingleton<IGameVersionInstaller, GameVersionInstaller>();
            logger.LogDebug("Game version management services registered");
            
            // Register GameLauncherService
            services.AddSingleton<IGameLauncherService, GameLauncherService>();
            logger.LogDebug("GameLauncherService registered");
            
            // Register the facade service that combines them
            services.AddSingleton<IGameVersionServiceFacade, GameVersionServiceFacade>();
            logger.LogDebug("GameVersionServiceFacade registered");
            
            // Register GameDetectionFacade
            services.AddSingleton(sp => {
                var facadeLogger = sp.GetRequiredService<ILogger<GameDetectionFacade>>();
                var gameDetector = sp.GetRequiredService<IGameDetector>();
                var executableLocator = sp.GetRequiredService<IGameExecutableLocator>();
                var jsonOptions = sp.GetRequiredService<JsonSerializerOptions>();
                
                // Default path without requiring configuration
                string versionsPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), 
                    "GenHub", 
                    "Versions");
                
                // Try to get configuration if available, but don't require it
                try {
                    var config = sp.GetService<IConfiguration>();  // Use GetService instead of GetRequiredService
                    if (config != null) {
                        var configPath = config["Paths:Versions"];
                        if (!string.IsNullOrEmpty(configPath)) {
                            versionsPath = configPath;
                        }
                    }
                } 
                catch (Exception ex) {
                    logger.LogWarning(ex, "Error getting configuration for versions path, using default");
                }
                
                logger.LogDebug("Using versions path: {VersionsPath}", versionsPath);
                return new GameDetectionFacade(facadeLogger, gameDetector, executableLocator, versionsPath, jsonOptions);
            });
            
            // Register game version related ViewModels if any
            RegisterGameVersionViewModels(services, logger);
            
            logger.LogInformation("Game version services and ViewModels configured successfully");
            return services;
        }
        
        /// <summary>
        /// Registers ViewModels specific to game versions
        /// </summary>
        private static void RegisterGameVersionViewModels(IServiceCollection services, ILogger logger)
        {
            // Register any game version specific ViewModels here
            // For example:
            // services.AddTransient<GameVersionDetailViewModel>();
            
            logger.LogDebug("Game version ViewModels registered (if any)");
        }
    }
}
