using System;
using GenHub.Core.Constants;
using GenHub.Core.Interfaces.GeneralsOnline;
using GenHub.Features.GeneralsOnline.Services;
using GenHub.Features.GeneralsOnline.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace GenHub.Infrastructure.DependencyInjection;

/// <summary>
/// Provides extension methods for registering Generals Online services and ViewModels.
/// </summary>
public static class GeneralsOnlineModule
{
    /// <summary>
    /// Registers Generals Online services and ViewModels for dependency injection.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <returns>The updated service collection.</returns>
    public static IServiceCollection AddGeneralsOnlineServices(this IServiceCollection services)
    {
        // Register HTTP client for Generals Online API
        services.AddHttpClient<IGeneralsOnlineApiClient, GeneralsOnlineApiClient>(static (serviceProvider, client) =>
        {
            client.BaseAddress = new Uri(GeneralsOnlineConstants.ApiBaseUrl);
            client.Timeout = TimeSpan.FromSeconds(30);
            client.DefaultRequestHeaders.Add(GeneralsOnlineConstants.AcceptHeader, GeneralsOnlineConstants.AcceptHeaderValue);
        });

        // Register authentication service as singleton (monitors credentials file)
        services.AddSingleton<IGeneralsOnlineAuthService, GeneralsOnlineAuthService>();

        // Register HTML parsing service for scraping website data
        services.AddSingleton<HtmlParsingService>();

        // Register child ViewModels
        services.AddSingleton<LeaderboardViewModel>();
        services.AddSingleton<MatchHistoryViewModel>();
        services.AddSingleton<LobbiesViewModel>();
        services.AddSingleton<ServiceStatusViewModel>();

        // Register main ViewModel (depends on child ViewModels)
        services.AddSingleton<GeneralsOnlineViewModel>();

        return services;
    }
}
