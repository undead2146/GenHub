# Game Version Management Interfaces

This directory contains interfaces that define contracts for services involved in discovering, installing, managing, and launching game versions. These interfaces are crucial for handling the lifecycle of game installations within the application.

## Interfaces

*   **`IGameDetector.cs`**:
    *   **Purpose**: Defines the contract for a service that can detect existing game installations on the user's system from various known sources (e.g., Steam, EA App, specific registry keys, common installation paths).
    *   **Key Methods**:
        *   `DetectGamesAsync(GameInstallationType? specificSource = null)`: Scans the system for installed games, potentially returning a list of `IGameInstallation` objects or paths. Can be filtered to a specific source.
    *   **Usage**: Used by `IGameVersionDiscoveryService` or during initial setup to find pre-existing game installations.

*   **`IGameExecutableLocator.cs`**:
    *   **Purpose**: Defines the contract for a service that can locate the main executable file within a given game installation directory. This is important as executable names can vary or be in subdirectories.
    *   **Key Methods**:
        *   `LocateExecutableAsync(string gameDirectoryPath, string? gameHint = null)`: Attempts to find the primary game executable within the specified directory. A hint (like game name or type) can be provided. Returns the full path to the executable or null if not found.
    *   **Usage**: Used by `IGameVersionInstaller` after installation or by `IGameVersionManager` when validating an existing `GameVersion` to ensure the executable path is correct.

*   **`IGameInstallation.cs`**:
    *   **Purpose**: Defines a contract for representing a detected or ongoing game installation, holding preliminary information before it's fully processed into a `GameVersion`.
    *   **Key Properties**: `Name`, `InstallPath`, `DetectedSource` (e.g., `GameInstallationType`), `VersionHint`, `ExecutablePathHint`.
    *   **Usage**: Objects implementing this interface might be returned by `IGameDetector` and then processed by `IGameVersionDiscoveryService` or `IGameVersionInstaller` to create a full `GameVersion` entry.

*   **`IGameLauncherService.cs`**:
    *   **Purpose**: Defines the contract for a service responsible for launching a game based on a selected `GameProfile` and its associated `GameVersion`.
    *   **Key Methods**:
        *   `PrepareLaunchAsync(IGameProfile profile)`: Prepares the necessary launch parameters (executable path, working directory, command-line arguments) and returns a `GameLaunchPrepResult`.
        *   `LaunchGameAsync(GameLaunchPrepResult launchParams)`: Executes the game using the prepared launch parameters.
    *   **Usage**: Used by the UI when the user clicks a "Play" button for a game profile.

*   **`IGameVersionDiscoveryService.cs`**:
    *   **Purpose**: Defines the contract for a service that discovers potential game versions from various sources (e.g., detected installations via `IGameDetector`, available GitHub artifacts via GitHub services).
    *   **Key Methods**:
        *   `DiscoverLocalVersionsAsync()`: Discovers game versions from local installations.
        *   `DiscoverRemoteVersionsAsync(GitHubRepository repoSettings)`: Discovers game versions available from a remote source like GitHub.
        *   `ProcessDiscoveredInstallationAsync(IGameInstallation detectedGame)`: Converts a raw `IGameInstallation` into a potential `GameVersion` model, possibly involving validation and metadata enrichment.
    *   **Usage**: Populates the list of available or known game versions that the user can then choose to manage or install.

*   **`IGameVersionInstaller.cs`**:
    *   **Purpose**: Defines the contract for a service that handles the installation of new game versions, for example, from a downloaded archive (like a ZIP file from a GitHub artifact).
    *   **Key Methods**:
        *   `InstallVersionAsync(string archivePath, ExtractOptions options, IProgress<InstallProgress> progress)`: Installs a game version from the given archive path using specified options, reporting progress. Returns the installed `GameVersion` object or null on failure.
        *   `CanInstall(string archivePath)`: Checks if the installer can handle the given archive type.
    *   **Usage**: Used when a user decides to install a new game version, often after it has been discovered by `IGameVersionDiscoveryService` and downloaded.

*   **`IGameVersionManager.cs`**:
    *   **Purpose**: Defines the contract for a service that manages the lifecycle (CRUD operations) of installed `GameVersion` objects. This is the central service for interacting with the user's library of game versions.
    *   **Key Methods**:
        *   `GetInstalledVersionsAsync()`: Retrieves all installed and managed game versions.
        *   `GetVersionByIdAsync(string versionId)`: Retrieves a specific game version.
        *   `SaveVersionAsync(GameVersion version)`: Adds a new or updates an existing game version to the managed library.
        *   `DeleteVersionAsync(string versionId)`: Deletes/unregisters a game version (may or may not delete files, depending on implementation).
        *   `ValidateVersionAsync(string versionId)`: Checks the integrity and validity of an installed game version.
    *   **Usage**: The primary interface for managing the collection of game versions known to the application.

## Responsibilities and Interactions

*   These interfaces collectively manage the entire lifecycle of game versions: detection (`IGameDetector`), discovery from various sources (`IGameVersionDiscoveryService`), installation (`IGameVersionInstaller`), ongoing management and persistence (`IGameVersionManager`), executable location (`IGameExecutableLocator`), and launching (`IGameLauncherService`).
*   They often interact with each other (e.g., `IGameVersionDiscoveryService` might use `IGameDetector`, `IGameVersionInstaller` might use `IGameExecutableLocator`).
*   They also interact with model classes like `GameVersion`, `GameProfile`, `InstallProgress`, and `ExtractOptions`.
