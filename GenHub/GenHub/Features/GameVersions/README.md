# Game Versions Feature

This feature module encapsulates all functionalities related to managing game versions within GenHub. This includes discovering existing local game installations, finding and installing game versions from various sources (like GitHub artifacts or local archives), managing the library of installed games, and launching them.

## Feature Components

The Game Versions feature is primarily organized into the following sub-directories:

*   **[`/Services`](GenHub/GenHub/Features/GameVersions/Services/README.md)**: Contains concrete implementations of service interfaces related to game version management. Key services include:
    *   [`GameVersionManager`](GenHub/GenHub/Features/GameVersions/Services/GameVersionManager.cs): Core CRUD operations for game versions.
    *   [`GameVersionDiscoveryService`](GenHub/GenHub/Features/GameVersions/Services/GameVersionDiscoveryService.cs): Finds potential game versions.
    *   [`GameVersionInstaller`](GenHub/GenHub/Features/GameVersions/Services/GameVersionInstaller.cs): Installs game versions from archives.
    *   [`GameLauncherService`](GenHub/GenHub/Features/GameVersions/Services/GameLauncherService.cs): Launches game versions, often using profiles.
    *   [`GameDetectionFacade`](GenHub/GenHub/Features/GameVersions/Services/GameDetectionFacade.cs): Detects existing game installations from various sources.
    *   [`GameExecutableLocator`](GenHub/GenHub/Features/GameVersions/Services/GameExecutableLocator.cs): Finds and validates game executables.
    *   [`GameVersionServiceFacade`](GenHub/GenHub/Features/GameVersions/Services/GameVersionServiceFacade.cs): A facade simplifying access to the other version-related services.
*   **[`/Json`](GenHub/GenHub/Features/GameVersions/Json/README.md)**: Contains custom JSON converters, such as the [`SourceMetadataJsonConverter`](GenHub/GenHub/Features/GameVersions/Json/SourceMetadataJsonConverter.cs) for handling polymorphic serialization of source metadata associated with game versions.

*(Note: UI components (Views/ViewModels) specific to game version management might reside in a more general UI feature module or be directly part of the main application UI, as they are not present in this feature's directory structure.)*

## Core Functionalities

*   **Game Detection**:
    *   Utilizes [`GameDetectionFacade`](GenHub/GenHub/Features/GameVersions/Services/GameDetectionFacade.cs) to scan for pre-existing game installations from sources like Steam, EA App, and known local paths by coordinating platform-specific `IGameDetector` implementations.
*   **Game Discovery**:
    *   [`GameVersionDiscoveryService`](GenHub/GenHub/Features/GameVersions/Services/GameVersionDiscoveryService.cs) identifies potential new game versions. This involves using the [`GameDetectionFacade`](GenHub/GenHub/Features/GameVersions/Services/GameDetectionFacade.cs) for local installations and can integrate with GitHub services to find downloadable versions.
*   **Game Installation**:
    *   [`GameVersionInstaller`](GenHub/GenHub/Features/GameVersions/Services/GameVersionInstaller.cs) handles installing game versions from archives (e.g., ZIP files from GitHub artifacts or local paths).
    *   This includes extracting files, placing them in designated version-controlled directories (e.g., under `GenHub\Versions\GitHub\{RepoOwner}\{RepoName}` or `GenHub\Versions\Local`), and using [`GameExecutableLocator`](GenHub/GenHub/Features/GameVersions/Services/GameExecutableLocator.cs) to find the main executable post-installation.
    *   Metadata about the installation (including `GitHubArtifact` details if applicable) is often saved as a JSON file within the installation directory (e.g., `InstallName.json`).
*   **Library Management**:
    *   [`GameVersionManager`](GenHub/GenHub/Features/GameVersions/Services/GameVersionManager.cs) is responsible for adding, updating, retrieving, and removing `GameVersion` objects from the user's library, persisting this data via `IGameVersionRepository`.
    *   It also validates the integrity of installed game versions (e.g., checking if `ExecutablePath` is valid).
*   **Game Launching**:
    *   [`GameLauncherService`](GenHub/GenHub/Features/GameVersions/Services/GameLauncherService.cs) prepares launch parameters based on `GameVersion` data and `IGameProfile` settings.
    *   It handles starting the game process, ensuring the correct working directory and command-line arguments are used.

## Interactions with Other Modules

*   **`GenHub.Core`**:
    *   Implements interfaces defined in `GenHub.Core.Interfaces.GameVersion` (e.g., `IGameVersionManager`, `IGameVersionDiscoveryService`, `IGameVersionInstaller`, `IGameLauncherService`, `IGameExecutableLocator`) and `GenHub.Core.Interfaces.Facades` (e.g., `IGameVersionServiceFacade`).
    *   Uses models from `GenHub.Core.Models.GameProfiles` (e.g., `GameVersion`, `GameInstallationType`, `ExtractOptions`), `GenHub.Core.Models.SourceMetadata` (e.g., `BaseSourceMetadata`, `GitHubSourceMetadata`), and `GenHub.Core.Models.Results` (e.g., `OperationResult`, `InstallProgress`).
*   **GitHub Feature**:
    *   [`GameVersionDiscoveryService`](GenHub/GenHub/Features/GameVersions/Services/GameVersionDiscoveryService.cs) and [`GameVersionInstaller`](GenHub/GenHub/Features/GameVersions/Services/GameVersionInstaller.cs) interact heavily with services from the GitHub feature (e.g., `IGitHubServiceFacade`, `IGitHubArtifactService`) to find, download, and process game versions hosted as GitHub artifacts or releases.
*   **Game Profiles Feature**:
    *   [`GameVersionManager`](GenHub/GenHub/Features/GameVersions/Services/GameVersionManager.cs) may interact with profile management services (e.g., `IGameProfileManagerService`) to handle profiles associated with game versions being deleted or modified.
    *   [`GameLauncherService`](GenHub/GenHub/Features/GameVersions/Services/GameLauncherService.cs) uses `IGameProfile` data, which is linked to a `GameVersion`, to configure and launch the game.
*   **Infrastructure Layer**:
    *   Relies on repository implementations (e.g., `IGameVersionRepository`) from the Infrastructure layer for data persistence.
    *   Uses logging (`ILogger`) and Dependency Injection services.
    *   The [`SourceMetadataJsonConverter`](GenHub/GenHub/Features/GameVersions/Json/SourceMetadataJsonConverter.cs) is registered and used by `System.Text.Json` for serializing/deserializing `GameVersion` objects that contain `BaseSourceMetadata`.
*   **UI Layer (ViewModels/Views)**:
    *   Services from this feature, particularly the [`GameVersionServiceFacade`](GenHub/GenHub/Features/GameVersions/Services/GameVersionServiceFacade.cs), are consumed by view models that present game libraries, installation progress, and launch options to the user.

This feature is central to GenHub's purpose, providing the tools to build and manage a user's collection of C&C Generals and Zero Hour game installations.
