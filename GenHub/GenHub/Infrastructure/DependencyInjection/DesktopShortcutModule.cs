using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using GenHub.Core.Interfaces.DesktopShortcuts;
using GenHub.Features.DesktopShortcuts.Services;

namespace GenHub.Infrastructure.DependencyInjection
{
    /// <summary>
    /// Dependency injection module for desktop shortcut functionality
    /// </summary>
    public static class DesktopShortcutModule
    {
        /// <summary>
        /// Registers all desktop shortcut services
        /// </summary>
        /// <param name="services">Service collection</param>
        /// <param name="logger">Logger for registration process</param>
        /// <returns>Service collection for chaining</returns>
        public static IServiceCollection AddDesktopShortcutModule(this IServiceCollection services, ILogger logger)
        {
            logger.LogDebug("Registering Desktop Shortcut services");

            try
            {
                // Register core shortcut services
                services.AddScoped<IDesktopShortcutServiceFacade, DesktopShortcutServiceFacade>();
                logger.LogDebug("DesktopShortcutServiceFacade registered");

                services.AddSingleton<IShortcutCommandBuilder, ShortcutCommandBuilder>();
                logger.LogDebug("ShortcutCommandBuilder registered");

                services.AddSingleton<IShortcutIconExtractor, ShortcutIconExtractor>();
                logger.LogDebug("ShortcutIconExtractor registered");

                // Register cross-platform shortcut service that handles platform detection
                services.AddScoped<IShortcutPlatformService, PlatformShortcutService>();
                logger.LogDebug("PlatformShortcutService registered");

                logger.LogInformation("Desktop Shortcut module registered successfully");
                return services;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error registering Desktop Shortcut module");
                throw;
            }
        }
    }
}
