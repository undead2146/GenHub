# Dependency Injection Modules

This directory contains modular service registration classes (modules) for configuring the application's dependency injection container. Each module groups service registrations for a specific feature or application layer, making the DI setup modular, organized, and maintainable.

## Core Infrastructure Modules

*   **`ApplicationServicesModule.cs`** (formerly CommonServiceRegistration.cs):
    *   **Responsibility**: Acts as the main entry point and orchestrator that composes the entire application's service collection.
    *   **Key Function**: `AddApplicationModules()` - calls registration methods from all other modules in the correct order.
    *   **Dependencies**: Depends on all other modules to properly build the service collection.

*   **`CoreInfrastructureModule.cs`** (formerly InfrastructureServiceRegistration.cs):
    *   **Responsibility**: Registers fundamental, non-UI, non-feature-specific infrastructure services.
    *   **Key Services**: Base HttpClient configurations, cross-cutting concerns.

*   **`LoggingModule.cs`** (formerly LoggingServiceRegistration.cs):
    *   **Responsibility**: Configures application logging services.
    *   **Key Services**: ILogger implementations, logging factory, bootstrap logger creation.

*   **`JsonSerializationModule.cs`** (formerly JsonRegistrationService.cs):
    *   **Responsibility**: Configures JSON serialization/deserialization options.
    *   **Key Services**: JsonSerializerOptions, custom converters, serialization helpers.

*   **`CachingModule.cs`** (formerly CachingServiceRegistration.cs):
    *   **Responsibility**: Registers caching infrastructure.
    *   **Key Services**: ICacheService, memory and disk caching implementations.

*   **`RepositoryModule.cs`** (formerly RepositoryServiceRegistration.cs):
    *   **Responsibility**: Registers data repository implementations.
    *   **Key Services**: IDataRepository, IGitHubCachingRepository, IGameVersionRepository, IGameProfileRepository.

## Feature Modules

*   **`AppUpdateModule.cs`** (formerly AppUpdateServiceRegistration.cs):
    *   **Responsibility**: Registers application update services and related ViewModels.
    *   **Key Services**: IAppUpdateService, IUpdateInstaller, IVersionComparator, UpdateNotificationViewModel.

*   **`GameVersionModule.cs`** (formerly GameVersionServiceRegistration.cs):
    *   **Responsibility**: Registers game version management services.
    *   **Key Services**: IGameVersionServiceFacade, IGameVersionManager, IGameDetector, IGameExecutableLocator.

*   **`GitHubModule.cs`** (consolidated from GitHubServiceRegistration.cs):
    *   **Responsibility**: Registers GitHub API services, caching, and related ViewModels.
    *   **Key Services**: IGitHubServiceFacade, IGitHubApiClient, GitHubManagerViewModel and related child ViewModels.

*   **`ProfileModule.cs`** (formerly ProfileServiceRegistration.cs):
    *   **Responsibility**: Registers game profile management services and ViewModels.
    *   **Key Services**: IGameProfileManagerService, IGameProfileFactory, ProfileResourceService, GameProfileSettingsViewModel.

## UI and View Model Modules

*   **`SharedViewModelModule.cs`** (formerly ViewModelServiceRegistration.cs):
    *   **Responsibility**: Registers application-wide ViewModels and UI services.
    *   **Key Services**: MainViewModel, ViewLocator, and other shared UI components.

## Module Design Principles

1. **Single Responsibility**: Each module handles one coherent feature area or infrastructure concern.
2. **Explicit Dependencies**: Modules declare their dependencies clearly in method parameters.
3. **Extension Methods**: Modules use extension methods on IServiceCollection for fluent registration.
4. **Logging**: Modules log their registration activities for diagnostic purposes.
5. **Error Handling**: Registration errors are caught and logged appropriately.
6. **Order Independence**: Where possible, modules are designed to be registered in any order, though ApplicationServicesModule handles the correct sequence.

## Typical Registration Pattern

```csharp
// Common pattern for module extension method
public static IServiceCollection Add[FeatureName]Services(
    this IServiceCollection services, 
    IConfiguration config, 
    ILogger logger)
{
    logger.LogInformation("Configuring [feature name] services");
    
    // Register services here...
    services.AddSingleton<IFeatureService, FeatureServiceImplementation>();
    
    logger.LogInformation("[Feature name] services configured successfully");
    return services;
}
```

This modular approach makes the dependency injection configuration easier to manage as the application grows and helps maintain clear boundaries between different parts of the system.
