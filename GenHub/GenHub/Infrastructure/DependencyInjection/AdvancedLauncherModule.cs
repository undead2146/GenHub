using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using GenHub.Core.Interfaces.AdvancedLauncher;
using GenHub.Features.AdvancedLauncher.Services;
using System;

namespace GenHub.Infrastructure.DependencyInjection
{
    /// <summary>
    /// Dependency injection module for advanced launcher functionality
    /// </summary>
    public static class AdvancedLauncherModule
    {
        /// <summary>
        /// Registers all advanced launcher services
        /// </summary>
        /// <param name="services">Service collection</param>
        /// <param name="logger">Logger for registration process</param>
        /// <returns>Service collection for chaining</returns>
        public static IServiceCollection AddAdvancedLauncherModule(this IServiceCollection services, ILogger logger)
        {
            logger.LogDebug("Registering Advanced Launcher services");

            try
            {
                // Register core launcher services
                services.AddSingleton<ILauncherArgumentParser, LauncherArgumentParser>();
                logger.LogDebug("LauncherArgumentParser registered");

                services.AddScoped<IDirectLaunchService, DirectLaunchService>();
                logger.LogDebug("DirectLaunchService registered");

                services.AddSingleton<ILauncherProtocolService, LauncherProtocolService>();
                logger.LogDebug("LauncherProtocolService registered");

                services.AddScoped<IQuickLaunchOrchestrator, QuickLaunchOrchestrator>();
                logger.LogDebug("QuickLaunchOrchestrator registered");

                logger.LogInformation("Advanced Launcher module registered successfully");
                return services;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error registering Advanced Launcher module");
                throw;
            }
        }
    }
}
