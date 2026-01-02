using GenHub.Core.Interfaces.Tools;
using GenHub.Core.Interfaces.Tools.ReplayManager;
using GenHub.Features.Tools.ReplayManager;
using GenHub.Features.Tools.ReplayManager.Services;
using GenHub.Features.Tools.ReplayManager.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace GenHub.Infrastructure.DependencyInjection;

/// <summary>
/// Dependency injection module for the Replay Manager tool.
/// </summary>
public static class ReplayManagerModule
{
    /// <summary>
    /// Adds Replay Manager services to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The updated service collection.</returns>
    public static IServiceCollection AddReplayManagerServices(this IServiceCollection services)
    {
        // Services
        services.AddSingleton<IReplayDirectoryService, ReplayDirectoryService>();
        services.AddSingleton<IUrlParserService, UrlParserService>();
        services.AddSingleton<IReplayImportService, ReplayImportService>();
        services.AddSingleton<IReplayExportService, ReplayExportService>();
        services.AddSingleton<GenHub.Core.Interfaces.Common.IUploadHistoryService, GenHub.Features.Tools.Services.UploadHistoryService>();
        services.AddSingleton<IZipValidationService, ZipValidationService>();

        // ViewModel (Singleton to persist state across tool activations)
        services.AddSingleton<ReplayManagerViewModel>();

        // Tool Plugin (Registered as a singleton IToolPlugin)
        services.AddSingleton<IToolPlugin, ReplayManagerToolPlugin>();

        return services;
    }
}
