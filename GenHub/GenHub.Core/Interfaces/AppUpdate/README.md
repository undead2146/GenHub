# Application Update Interfaces

This directory defines the contracts for services and components involved in the application's self-update functionality. These interfaces ensure a decoupled architecture, allowing different implementations for checking, downloading, and installing application updates.

## Interfaces

*   **`IAppUpdateService.cs`**:
    *   **Purpose**: Defines the core contract for a service that manages the application update process.
    *   **Key Methods/Properties**:
        *   `CheckForUpdatesAsync()`: Checks a remote source (e.g., update server, GitHub releases) for new application versions. Returns information about available updates (e.g., `AppVersionInfo` or `UpdateManifest`).
        *   `DownloadUpdateAsync(AppVersionInfo versionInfo, IProgress<InstallProgress> progress)`: Downloads the update package for a specified version, reporting progress.
        *   `InstallUpdateAsync(AppVersionInfo versionInfo, string downloadedPackagePath)`: Initiates the installation of a downloaded update package. This might involve delegating to an `IUpdateInstaller`.
        *   `GetCurrentVersion()`: Gets the currently running application version.
    *   **Events (Optional)**: `UpdateAvailable`, `UpdateDownloaded`, `UpdateInstalled`.

*   **`IUpdateInstaller.cs`**:
    *   **Purpose**: Defines the contract for a component responsible for performing the actual installation of an application update package. This allows for platform-specific installation logic (e.g., Windows MSI, Linux tarball, macOS .dmg).
    *   **Key Methods**:
        *   `InstallUpdate(string packagePath, string? installDirectory = null)`: Executes the update installation from the given package path. May involve restarting the application.
        *   `CanInstallUpdate(string packagePath)`: Checks if the installer can handle the given package type or if prerequisites are met.

*   **`IVersionComparator.cs`**:
    *   **Purpose**: Defines the contract for a utility that compares application versions. This is crucial for determining if an available version is newer than the current version.
    *   **Key Methods**:
        *   `Compare(string versionA, string versionB)`: Compares two version strings (e.g., "1.0.0", "1.1.0") and returns an integer indicating their relationship (less than, equal to, or greater than).
        *   `IsNewer(string currentVersion, string availableVersion)`: A convenience method that returns true if `availableVersion` is newer than `currentVersion`.

## Responsibilities and Interactions

*   `IAppUpdateService` orchestrates the update process, using `IVersionComparator` to compare versions and potentially `IUpdateInstaller` to perform the installation.
*   Implementations of `IAppUpdateService` might fetch update manifests (e.g., `UpdateManifest` model) from various sources.
*   `IUpdateInstaller` implementations handle the platform-specific details of applying an update.
*   These interfaces allow for easy swapping of update strategies or sources without affecting the core application logic that consumes these services.
