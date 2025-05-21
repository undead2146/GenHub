# Repository Interfaces

This directory defines contracts for data repositories. The repository pattern abstracts the data access logic, providing a clean API for Create, Read, Update, and Delete (CRUD) operations on domain entities without exposing the underlying data storage mechanism (e.g., JSON files, SQLite database, cloud storage).

## Interfaces

*   **`IDataRepository.cs`**:
    *   **Purpose**: A generic repository interface that can be used for various data types, often focusing on a single entity type `T`.
    *   **Key Methods (Typical for a generic repository)**:
        *   `GetByIdAsync(TId id)`: Retrieves an entity by its unique identifier.
        *   `GetAllAsync()`: Retrieves all entities of type `T`.
        *   `AddAsync(T entity)`: Adds a new entity.
        *   `UpdateAsync(T entity)`: Updates an existing entity.
        *   `DeleteAsync(TId id)`: Deletes an entity by its identifier.
        *   `SaveChangesAsync()`: Persists any pending changes to the underlying data store.
    *   **Usage**: Serves as a base or a direct contract for simple entity management.

*   **`IEntityIdentifier.cs`**:
    *   **Purpose**: A simple contract for entities that have a unique identifier. This helps in creating generic repository methods or constraints.
    *   **Key Properties**: `Id` (of type `TId`, which is generic).
    *   **Usage**: Implemented by domain models like `GameVersion` and `GameProfile` to signify they have an ID that repositories can use.

*   **`IGameProfileRepository.cs`**:
    *   **Purpose**: A specialized repository interface for managing the persistence of `IGameProfile` entities. It might inherit from a more generic `IDataRepository<IGameProfile, string>` or define its own specific methods.
    *   **Key Methods (Beyond basic CRUD, if any)**:
        *   `GetProfilesByVersionIdAsync(string versionId)`: Retrieves all profiles associated with a specific `GameVersion`.
        *   `GetDefaultProfileForVersionAsync(string versionId)`: Retrieves the default profile for a game version.
    *   **Usage**: Used by `IGameProfileManagerService` to load and save game profile data.

*   **`IGameVersionRepository.cs`**:
    *   **Purpose**: A specialized repository interface for managing the persistence of `GameVersion` entities.
    *   **Key Methods (Beyond basic CRUD, if any)**:
        *   `FindByInstallPathAsync(string installPath)`: Finds a game version by its installation path.
    *   **Usage**: Used by `IGameVersionManager` to load and save game version data.

*   **`IGitHubDataRepository.cs`**:
    *   **Purpose**: A repository interface specifically for caching or storing data fetched from GitHub, such as `GitHubWorkflow` or `GitHubArtifact` information, to reduce API calls or store user-specific GitHub settings.
    *   **Key Methods (Conceptual)**:
        *   `GetCachedWorkflowRunsAsync(string repoFullName)`: Retrieves cached workflow runs.
        *   `CacheWorkflowRunsAsync(string repoFullName, IEnumerable<GitHubWorkflow> runs)`: Caches workflow runs.
        *   `GetRepoSettingsAsync(string repoFullName)`: Retrieves stored settings for a repository.
        *   `SaveRepoSettingsAsync(GitHubRepoSettings settings)`: Saves repository settings.
    *   **Usage**: Could be used by GitHub services to implement a caching layer.

*   **`IJsonRepository.cs`**:
    *   **Purpose**: A more specific repository interface indicating that the underlying data storage mechanism is JSON files. This might offer methods tailored to JSON serialization/deserialization if needed, or simply act as a marker interface.
    *   **Key Methods (If specialized)**: Could involve methods that take JSON paths or handle JSON-specific queries if the abstraction is very thin. More likely, it's implemented by concrete classes that handle JSON persistence for `IDataRepository<T, TId>`.
    *   **Usage**: Implemented by concrete repository classes that store data in JSON files (e.g., `JsonGameVersionRepository`).

*   **`IRepository.cs`**:
    *   **Purpose**: A marker interface or a very basic, non-generic repository contract. It's often used as a common base for all repository interfaces if a DI container needs to register all repositories under a common type.
    *   **Usage**: Can be used for dependency injection scanning or as a base for more specific repository interfaces.

## Responsibilities and Interactions

*   Repository interfaces define how services interact with the data persistence layer.
*   Services (like `IGameVersionManagerService`, `IGameProfileManagerService`) depend on these repository interfaces, not on concrete repository implementations. This adheres to the Dependency Inversion Principle.
*   Concrete implementations of these repositories (e.g., `JsonGameVersionRepository`, `SQLiteGameProfileRepository`) handle the actual data storage and retrieval logic.
*   This pattern allows for easier testing (by mocking repositories) and flexibility in changing the data storage technology without affecting the service layer.
