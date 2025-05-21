# Game Profile & Version Models

This directory houses the core domain models for representing game installations (`GameVersion`) and user-configurable launch profiles (`GameProfile`). It also includes related models like `ThemeColors`, `ProfileResourceItem`, `DataPathItem`, and `ExecutablePathItem`.

## Models

*   **`GameVersion.cs`**:
    *   **Purpose**: Represents a specific, installed or discoverable version of a game. It can be sourced from various places, including GitHub artifacts or local installations.
    *   **Key Properties**: `Id`, `Name`, `Description`, `InstallPath`, `GamePath`, `ExecutablePath`, `InstallDate`, `SourceType` (enum `GameInstallationType`), `GameType`, `IsValid`, `InstallSizeBytes`, `BuildDate`.
    *   **Metadata**: `SourceSpecificMetadata` (of type `BaseSourceMetadata`), which can hold an instance of `GitHubSourceMetadata` if the version is from GitHub.
    *   **Convenience Accessors**: `GitHubMetadata` (casts `SourceSpecificMetadata`), `IsFromGitHub`.
    *   **Usage**: Managed by `IGameVersionManager`. Forms the basis for `GameProfile` instances. Its details are populated based on its source, potentially using `GitHubSourceMetadata`.

*   **`GameProfile.cs`**:
    *   **Purpose**: Represents a user-defined profile for launching a specific `GameVersion`. It allows customization of launch parameters, appearance, etc.
    *   **Key Properties**: `Id`, `Name`, `Description`, `ExecutablePath`, `DataPath`, `IconPath`, `CoverImagePath`, `ColorValue`, `CommandLineArguments`, `VersionId` (links to `GameVersion.Id`), `IsDefaultProfile`, `RunAsAdmin`, `SourceType`.
    *   **Metadata**: `SourceSpecificMetadata` (of type `BaseSourceMetadata`), typically mirroring or referencing the metadata from its associated `GameVersion`.
    *   **Convenience Accessors**: `GitHubMetadata`, `IsFromGitHub`.
    *   **Usage**: Managed by `IGameProfileManagerService`. Provides the configurations used by `IGameLauncherService` to start a game.

*   **`ThemeColors.cs`**:
    *   **Purpose**: Intended to centralize theme colors and appearance settings. (The provided file `ThemeColors.cs` was empty, so this is an assumed purpose based on the name).
    *   **Key Properties (Assumed)**: Could include properties for primary color, accent color, background color, text colors, etc.
    *   **Usage (Assumed)**: Could be referenced by `GameProfile` for profile-specific themes or used globally by the UI.

*   **`ProfileResourceItem.cs`**:
    *   **Purpose**: Represents a resource item (icon or cover).
    *   **Key Properties**: `Id`, `Path`, `DisplayName`, `IsBuiltIn`, `GameType`.
    *   **Usage**: Used to define and manage icons and cover images for game profiles.

*   **`ProfileFormItems.cs`**:
    *   **Purpose**: Defines models for data and executable paths used in profile settings UI. Includes comparers to prevent duplicates.
    *   **`DataPathItem`**:
        *   **Key Properties**: `Path`, `DisplayName`, `SourceType` (`GameInstallationType`), `IsBrowseOption`.
        *   **Usage**: Represents a data directory path in the profile settings UI.
    *   **`ExecutablePathItem`**:
        *   **Key Properties**: `Path`, `DisplayName`, `SourceType` (`GameInstallationType`), `IsBrowseOption`.
        *   **Usage**: Represents an executable file path in the profile settings UI.
    *   **`DataPathItemComparer`**:
        *   **Purpose**: Implements `IEqualityComparer<DataPathItem>` to compare `DataPathItem` objects based on their `Path` property.
        *   **Usage**: Used to prevent duplicate data paths in collections.
    *   **`ExecutablePathItemComparer`**:
        *   **Purpose**: Implements `IEqualityComparer<ExecutablePathItem>` to compare `ExecutablePathItem` objects based on their `Path` property.
        *   **Usage**: Used to prevent duplicate executable paths in collections.

*   **`GameInstallationType.cs`** (enum, likely defined within or alongside `GameVersion.cs` or in a shared enums file):
    *   **Purpose**: Defines the possible sources of a game installation.
    *   **Values**: `Unknown`, `Local`, `GitHubArtifact`, etc.
    *   **Usage**: Used by `GameVersion` and `GameProfile` in their `SourceType` property to indicate origin.

## Responsibilities and Interactions

*   `GameVersion` objects are discovered or created by services like `IGameVersionDiscoveryService` and `IGameVersionInstaller`. They are persisted and managed by `IGameVersionManager`.
*   `GameProfile` objects are created and managed by `IGameProfileFactory` and `IGameProfileManagerService`, typically associated with a `GameVersion`.
*   When a `GameVersion` is sourced from GitHub, its `SourceSpecificMetadata` property will hold a `GitHubSourceMetadata` object, providing rich details about its origin. `GameProfile` can then also reference this metadata.
*   `ProfileResourceItem` is used to manage and display icons and cover images associated with game profiles.
*   `DataPathItem` and `ExecutablePathItem` are used in the profile settings UI to allow users to select data and executable paths for their game profiles.
*   These models are central to the application's functionality, representing the user's game library and their configurations for playing games.
