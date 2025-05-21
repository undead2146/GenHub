# Game Version Services

This directory contains concrete implementations of service interfaces related to game version management, discovery, installation, and launching. These services encapsulate the business logic for handling the lifecycle of game versions within the application.

## Service Implementations

*   **[`GameDetectionFacade.cs`](GenHub/GenHub/Features/GameVersions/Services/GameDetectionFacade.cs)**:
    *   **Implements**: `IGameDetector`, `IGameExecutableLocator` (acts as both a detector facade and an executable locator itself, though it also delegates to a dedicated `IGameExecutableLocator` for some tasks).
    *   **Purpose**: Orchestrates the detection of installed games from various sources (e.g., Steam, EA App, known registry paths, common directories, and custom GenHub version directories like `Versions/GitHub` and `Versions/Local`). It coordinates platform-specific `IGameDetector` implementations and its own scanning logic to create `GameVersion` objects from these findings. It can also process installation directories to load metadata from `.json` files or scan for executables.
    *   **Key Methods**: `CreateGameVersionsFromDetectedInstallationsAsync`, `DetectInstallationsAsync` (delegated), `ScanCustomVersionsDirectory`, `ProcessInstallDirectory`.
    *   **Dependencies**: `ILogger<GameDetectionFacade>`, `IGameDetector` (platform-specific), `IGameExecutableLocator` (for detailed executable analysis), `JsonSerializerOptions`.

*   **[`GameExecutableLocator.cs`](GenHub/GenHub/Features/GameVersions/Services/GameExecutableLocator.cs)**:
    *   **Implements**: `IGameExecutableLocator`
    *   **Purpose**: Responsible for finding and validating the main executable file (e.g., `generals.exe`, `generalsv.exe`, `generalszh.exe`) within a given game installation directory. It uses a predefined list of valid game executables and utility executables to avoid misidentification. It determines if a directory is for "Generals" or "Zero Hour" and can format version names based on source type and GitHub workflow information.
    *   **Key Methods**: `FindBestGameExecutableAsync`, `FindExecutableAsync`, `IsValidGameExecutable`, `IsZeroHourDirectory`, `GetExecutableInfo`, `ScanDirectoryForExecutables`.
    *   **Dependencies**: `ILogger<GameExecutableLocator>`.

*   **[`GameLauncherService.cs`](GenHub/GenHub/Features/GameVersions/Services/GameLauncherService.cs)**:
    *   **Implements**: `IGameLauncherService`
    *   **Purpose**: Handles the process of launching a game based on a selected `IGameProfile` or a `GameVersion` object. This includes preparing launch parameters (executable path, working directory, command-line arguments, admin privileges) and starting the game process. It includes logic to find alternative executables if the primary one is missing and can copy/link executables to working directories if needed.
    *   **Key Methods**: `LaunchVersionAsync` (overloads for ID and `IGameProfile`), `LaunchGameVersionAsync`, `PrepareGameLaunchAsync`.
    *   **Dependencies**: `ILogger<GameLauncherService>`, `IGameVersionRepository`, `IGameExecutableLocator`.

*   **[`GameVersionDiscoveryService.cs`](GenHub/GenHub/Features/GameVersions/Services/GameVersionDiscoveryService.cs)**:
    *   **Implements**: `IGameVersionDiscoveryService`
    *   **Purpose**: Discovers potential game versions from various sources. It uses the [`GameDetectionFacade`](GenHub/GenHub/Features/GameVersions/Services/GameDetectionFacade.cs) to get a list of detected installations and then saves these as `GameVersion` objects via `IGameVersionManager`. It can also scan specific directories and validate existing versions.
    *   **Key Methods**: `DiscoverVersionsAsync`, `GetDetectedVersionsAsync`, `GetDefaultGameVersionsAsync`, `ScanDirectoryForVersionsAsync`, `ValidateVersionAsync`.
    *   **Dependencies**: `ILogger<GameVersionDiscoveryService>`, `IGameDetector` (via constructor, likely for `GetDefaultGameVersionsAsync`), `IGameExecutableLocator`, [`GameDetectionFacade`](GenHub/GenHub/Features/GameVersions/Services/GameDetectionFacade.cs), `IGameVersionManager`.

*   **[`GameVersionInstaller.cs`](GenHub/GenHub/Features/GameVersions/Services/GameVersionInstaller.cs)**:
    *   **Implements**: `IGameVersionInstaller`
    *   **Purpose**: Manages the installation of new game versions, primarily from archive files (e.g., ZIP files downloaded from GitHub artifacts/releases or local paths). It handles archive extraction, file placement into structured directories (e.g., `Versions/GitHub/{Owner}/{Repo}` or `Versions/Local`), and uses [`IGameExecutableLocator`](GenHub/GenHub/Features/GameVersions/Services/GameExecutableLocator.cs) to find the executable post-installation. It also creates/saves a JSON metadata file (e.g., `InstallName.json`) alongside the installed version, which includes details from `GitHubArtifact` if applicable.
    *   **Key Methods**: `InstallVersionAsync` (for GitHub artifacts), `InstallVersionFromZipAsync`, `InstallVersionFromReleaseAssetAsync`, `UninstallVersionAsync`.
    *   **Dependencies**: `ILogger<GameVersionInstaller>`, `IGameVersionRepository`, `IGameExecutableLocator`.

*   **[`GameVersionManager.cs`](GenHub/GenHub/Features/GameVersions/Services/GameVersionManager.cs)**:
    *   **Implements**: `IGameVersionManager`
    *   **Purpose**: The central service for managing the lifecycle (CRUD operations) of `GameVersion` objects. It handles adding, retrieving (with caching), updating, and deleting game versions, interacting with an `IGameVersionRepository` for persistence. It ensures versions are valid (e.g., `ExecutablePath` exists) and sorts them by preference. It also handles merging information if a version being saved already exists.
    *   **Key Methods**: `GetInstalledVersionsAsync`, `GetVersionByIdAsync`, `SaveVersionAsync`, `UpdateVersionAsync`, `DeleteVersionAsync`.
    *   **Dependencies**: `ILogger<GameVersionManager>`, `IGameVersionRepository`.

*   **[`GameVersionServiceFacade.cs`](GenHub/GenHub/Features/GameVersions/Services/GameVersionServiceFacade.cs)**:
    *   **Implements**: `IGameVersionServiceFacade`
    *   **Purpose**: Provides a simplified, higher-level interface for common game version operations. It coordinates actions across the other game version services (discovery, installation, management, launching) to fulfill complex user requests with fewer direct service calls, acting as a single point of contact for UI view models or other features.
    *   **Key Methods**: Exposes methods that delegate to `IGameVersionManager`, `IGameVersionDiscoveryService`, and `IGameLauncherService`.
    *   **Dependencies**: `ILogger<GameVersionServiceFacade>`, `IGameVersionManager`, `IGameVersionDiscoveryService`, `IGameLauncherService`.

## Responsibilities and Interactions

*   These services collectively implement the core logic for all game version-related operations within GenHub.
*   [`GameDetectionFacade`](GenHub/GenHub/Features/GameVersions/Services/GameDetectionFacade.cs) and [`GameExecutableLocator`](GenHub/GenHub/Features/GameVersions/Services/GameExecutableLocator.cs) are fundamental for identifying game files and installations.
*   [`GameVersionDiscoveryService`](GenHub/GenHub/Features/GameVersions/Services/GameVersionDiscoveryService.cs) uses detection results to find potential versions.
*   [`GameVersionInstaller`](GenHub/GenHub/Features/GameVersions/Services/GameVersionInstaller.cs) adds new versions to the system from archives.
*   [`GameVersionManager`](GenHub/GenHub/Features/GameVersions/Services/GameVersionManager.cs) maintains the library of known versions.
*   [`GameLauncherService`](GenHub/GenHub/Features/GameVersions/Services/GameLauncherService.cs) executes game versions.
*   The [`GameVersionServiceFacade`](GenHub/GenHub/Features/GameVersions/Services/GameVersionServiceFacade.cs) simplifies the usage of these services for clients like ViewModels.
*   They interact with repositories (e.g., `IGameVersionRepository`) for data persistence and may collaborate with services from other features (e.g., GitHub services for downloading artifacts).
