# Application Update Models

This directory contains data models specifically related to the application's self-update mechanism. These models define the structure of information about available updates, release notes, and versioning.

## Models

*   **`AppVersionInfo.cs`**:
    *   **Purpose**: Represents detailed information about a specific version of the application available for update.
    *   **Key Properties**:
        *   `Version` (e.g., "1.2.3"): The semantic version string.
        *   `ReleaseDate`: The date the version was released.
        *   `ReleaseNotesUrl`: A URL pointing to the detailed release notes or changelog.
        *   `DownloadUrl`: The URL from which the update package can be downloaded.
        *   `FileSize`: The size of the update package.
        *   `IsPrerelease`: A flag indicating if this version is a pre-release.
        *   `MinimumRequiredVersion`: The minimum version of the application required to apply this update.
    *   **Usage**: Used by the `AppUpdateService` to check for new versions, display update information to the user, and initiate the download process.

*   **`UpdateManifest.cs`**:
    *   **Purpose**: Represents the overall update manifest, typically fetched from a remote server. It can contain information about multiple available versions or channels (e.g., stable, beta).
    *   **Key Properties**:
        *   `LatestStableVersion`: An `AppVersionInfo` object for the latest stable release.
        *   `LatestPrereleaseVersion`: An `AppVersionInfo` object for the latest pre-release (if applicable).
        *   `Channels`: A list or dictionary of update channels, each potentially pointing to an `AppVersionInfo`.
    *   **Usage**: Fetched by the `AppUpdateService` to determine available updates based on user preferences or application settings.

## Responsibilities and Interactions

*   These models are primarily populated by deserializing data fetched from an update server (e.g., a JSON file or API endpoint).
*   The `IAppUpdateService` (and its implementations) consumes these models to:
    *   Check for available updates.
    *   Present update details to the user.
    *   Manage the download and installation of updates.
*   They ensure a consistent structure for update-related information, making the update process more robust and manageable.
