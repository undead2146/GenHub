using GenHub.Features.GameInstallations;
using GenHub.Features.GameVersions;
using Microsoft.Extensions.DependencyInjection;

namespace GenHub.Infrastructure.DependencyInjection;

/// <summary>
/// Provides extension methods for registering game detection services.
/// </summary>
public static class GameDetectionModule
{
    /// <summary>
    /// Registers game detection orchestrators as singletons in the service collection.
    /// Specifically, adds <see cref="GameVersionDetectionOrchestrator"/> and <see cref="GameInstallationDetectionOrchestrator"/>.
    /// </summary>
    /// <param name="services">The service collection to add the orchestrators to.</param>
    /// <returns>The updated service collection.</returns>
    public static IServiceCollection AddGameDetectionService(this IServiceCollection services)
    {
        services.AddSingleton<GameVersionDetectionOrchestrator>();
        services.AddSingleton<GameInstallationDetectionOrchestrator>();

        return services;
    }
}
