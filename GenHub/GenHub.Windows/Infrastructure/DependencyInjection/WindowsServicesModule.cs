using GenHub.Core.Features.ActionSets;
using GenHub.Core.Interfaces.GameInstallations;
using GenHub.Core.Interfaces.GitHub;
using GenHub.Core.Interfaces.Shortcuts;
using GenHub.Core.Interfaces.Storage;
using GenHub.Core.Interfaces.Tools;
using GenHub.Core.Interfaces.Workspace;
using GenHub.Features.GameInstallations;
using GenHub.Features.Workspace;
using GenHub.Windows.Features.ActionSets;
using GenHub.Windows.Features.ActionSets.Fixes;
using GenHub.Windows.Features.ActionSets.Infrastructure;
using GenHub.Windows.Features.ActionSets.UI;
using GenHub.Windows.Features.GitHub.Services;
using GenHub.Windows.Features.Shortcuts;
using GenHub.Windows.Features.Workspace;
using GenHub.Windows.GameInstallations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace GenHub.Windows.Infrastructure.DependencyInjection;

/// <summary>
/// Provides extension methods for registering Windows-specific services.
/// </summary>
public static class WindowsServicesModule
{
    /// <summary>
    /// Registers Windows-specific services in the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddWindowsServices(this IServiceCollection services)
    {
        // Register Windows-specific services
        services.AddSingleton<IGameInstallationDetector, WindowsInstallationDetector>();
        services.AddSingleton<IGitHubTokenStorage, WindowsGitHubTokenStorage>();
        services.AddSingleton<IShortcutService, WindowsShortcutService>();
        services.AddSingleton<IManualInstallationStorage, ManualInstallationStorage>();

        // Register WindowsFileOperationsService with factory to avoid circular dependency
        services.AddScoped<IFileOperationsService>(serviceProvider =>
        {
            var baseService = serviceProvider.GetRequiredService<FileOperationsService>();
            var casService = serviceProvider.GetRequiredService<ICasService>();
            var logger = serviceProvider.GetRequiredService<ILogger<WindowsFileOperationsService>>();
            return new WindowsFileOperationsService(baseService, casService, logger);
        });

        // Register ActionSet Infrastructure
        services.AddSingleton<IRegistryService, RegistryService>();
        services.AddSingleton<IActionSetOrchestrator, ActionSetOrchestrator>();

        // Register ActionSets
        services.AddSingleton<IActionSet, BrowserEngineFix>();
        services.AddSingleton<IActionSet, DbgHelpFix>();
        services.AddSingleton<IActionSet, EAAppRegistryFix>();
        services.AddSingleton<IActionSet, MyDocumentsPathCompatibility>();
        services.AddSingleton<IActionSet, VCRedist2010Fix>();

        // NOTE: GenPatcherContentActionSetProvider is NOT registered here.
        // Content from GenPatcherContentRegistry is already available in the Downloads UI
        // and should not be duplicated as ActionSets. Only the 4 core fixes above are needed.

        // Register GenPatcher Tool
        services.AddSingleton<IToolPlugin, GenPatcherTool>();
        services.AddTransient<GenPatcherViewModel>();

        return services;
    }
}
