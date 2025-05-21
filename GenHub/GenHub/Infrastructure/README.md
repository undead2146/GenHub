# Infrastructure Layer

The Infrastructure layer of GenHub contains concrete implementations of interfaces defined in `GenHub.Core`, as well as other cross-cutting concerns and utilities that support the application's core functionality. This layer deals with external concerns like data persistence, API communication, caching, UI value conversion, and dependency injection setup.

## Sub-Directories

*   **`/Caching`**: Contains caching service implementations (e.g., `CachingService`) for improving application performance by storing frequently accessed data.
*   **`/Converters`**: Houses various `IValueConverter` and `IMultiValueConverter` implementations used in XAML for data binding and UI display transformations.
*   **`/DependencyInjection`**: Includes classes responsible for registering services with the dependency injection container, organizing the application's startup and service resolution.
*   **`/Repositories`**: Provides concrete repository implementations (e.g., `JsonRepository`, `GameVersionRepository`, `GameProfileRepository`) that handle data access and persistence, abstracting the underlying storage mechanism.
*   **`/Security`**: Contains components related to application security, such as services for securely storing and retrieving sensitive data like API tokens (e.g., `TokenStorageService`).

## Key Responsibilities

*   **Implementing Core Interfaces**: Provides concrete classes for interfaces defined in `GenHub.Core.Interfaces` (e.g., repository implementations, GitHub API client implementations, token storage).
*   **Data Persistence**: Manages how application data (game versions, profiles, settings) is stored and retrieved, whether from files (like JSON) or potentially databases.
*   **External Service Communication**: Handles interactions with external APIs, most notably the GitHub API.
*   **Cross-Cutting Concerns**: Implements functionalities that are used across multiple parts of the application, such as caching, logging (setup often found in DependencyInjection), and UI data conversion.
*   **Application Startup and Configuration**: The `DependencyInjection` sub-directory plays a crucial role in wiring up the application's components during startup.

## Design Principles

*   **Adherence to Core Contracts**: Infrastructure components implement interfaces from `GenHub.Core`, ensuring that the core business logic remains independent of specific infrastructure concerns.
*   **Separation of Concerns**: This layer separates concerns like data access, external API interaction, and UI conversion logic from the core domain logic and application services.
*   **Configurability and Extensibility**: Through dependency injection and adherence to interfaces, components in this layer can often be configured or replaced with alternative implementations if needed.

The Infrastructure layer is vital for making the application functional, bridging the abstract core logic with concrete external systems and utilities.
