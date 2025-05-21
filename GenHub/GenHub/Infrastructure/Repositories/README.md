# Repository Implementations

This directory contains concrete implementations of the repository interfaces defined in `GenHub.Core.Interfaces.Repositories`. These classes handle the actual data access logic, interacting with specific data storage mechanisms like JSON files, databases, or other persistence layers.

## Repository Classes

*   **`DataRepository.cs`**:
    *   **Purpose**: A generic base class or a concrete implementation for `IDataRepository<T, TId>`. It might provide common CRUD operations that can be inherited by more specialized repositories.
    *   **Implementation Details**: Could be abstract, requiring derived classes to implement specific data loading/saving logic, or it could be a concrete implementation for a common storage type (e.g., an in-memory repository for testing).

*   **`GameProfileRepository.cs`**:
    *   **Purpose**: Implements `IGameProfileRepository`. Responsible for persisting and retrieving `GameProfile` entities.
    *   **Implementation Details**: Likely uses `JsonRepository<GameProfile, string>` as a base or directly handles serialization/deserialization of game profiles to/from JSON files. Manages a collection of game profiles, typically stored in a dedicated file or directory.

*   **`GameVersionRepository.cs`**:
    *   **Purpose**: Implements `IGameVersionRepository`. Responsible for persisting and retrieving `GameVersion` entities.
    *   **Implementation Details**: Similar to `GameProfileRepository`, it probably uses `JsonRepository<GameVersion, string>` or custom JSON handling to store game version data.

*   **`GitHubDataRepository.cs`**:
    *   **Purpose**: Implements `IGitHubDataRepository`. Handles caching or persistent storage of data fetched from GitHub, such as workflow run information, artifact lists, or user-specific repository settings.
    *   **Implementation Details**: Could use JSON files, a lightweight database, or the `CachingService` to store this data. Aims to reduce redundant API calls to GitHub.

*   **`JsonRepository.cs`**:
    *   **Purpose**: A generic repository implementation that uses JSON files as the data store. It likely implements `IJsonRepository<T, TId>` or `IDataRepository<T, TId>`.
    *   **Key Features**:
        *   Handles serialization of entities of type `T` to JSON format and deserialization from JSON back to `T`.
        *   Manages reading from and writing to specific JSON files.
        *   Provides the core CRUD operations by manipulating the JSON data.
    *   **Usage**: Serves as a base class or is directly used by specialized repositories like `GameVersionRepository` and `GameProfileRepository` if they store their data in JSON files.

## Responsibilities and Interactions

*   These repository implementations are responsible for the "how" of data persistence, abstracting these details from the service layer.
*   Services (e.g., `GameVersionManagerService`, `GameProfileManagerService`) depend on the repository *interfaces* (`IGameVersionRepository`, `IGameProfileRepository`) and are provided with concrete instances of these classes by the dependency injection container.
*   This separation allows the data storage mechanism to be changed (e.g., from JSON files to a SQLite database) by simply providing a different repository implementation, without altering the service layer code.
*   They interact with the file system (for JSON repositories), database drivers, or other persistence APIs.
