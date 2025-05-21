# Core Interfaces

This directory is the root for all interface definitions within the `GenHub.Core` project. Interfaces define contracts that concrete classes must implement, promoting a decoupled, modular, and testable architecture. By programming to interfaces rather than concrete implementations, the application gains flexibility and maintainability.

## Sub-Directories

This directory organizes interfaces into sub-directories based on their functional domain:

*   **`/AppUpdate`**: Contains interfaces related to the application's self-updating mechanism (e.g., `IAppUpdateService`, `IUpdateInstaller`).
*   **`/Facades`**: Contains facade interfaces that provide simplified entry points to more complex subsystems (e.g., `IGameVersionServiceFacade`, `IGitHubServiceFacade`).
*   **`/GameProfiles`**: Contains interfaces for managing game profiles (e.g., `IGameProfile`, `IGameProfileManagerService`).
*   **`/GameVersion`**: Contains interfaces for discovering, installing, managing, and launching game versions (e.g., `IGameVersionManager`, `IGameVersionInstaller`, `IGameLauncherService`).
*   **`/Github`**: Contains interfaces for interacting with the GitHub API (e.g., `IGitHubApiClient`, `IGitHubArtifactService`, `ITokenStorageService`).
*   **`/Repositories`**: Contains interfaces defining the repository pattern for data access (e.g., `IDataRepository`, `IGameVersionRepository`).

## General Principles

*   **Abstraction**: Interfaces abstract the "what" (the contract) from the "how" (the implementation).
*   **Decoupling**: Components depend on interfaces, not concrete classes, reducing direct dependencies and making it easier to swap implementations.
*   **Testability**: Interfaces facilitate unit testing by allowing mock or stub implementations to be used in place of real services or data stores.
*   **Dependency Injection**: Interfaces are fundamental to Dependency Injection (DI), where concrete implementations are provided to dependent classes at runtime.

By adhering to these principles, the interfaces defined within `GenHub.Core` contribute to a robust and scalable application design.
