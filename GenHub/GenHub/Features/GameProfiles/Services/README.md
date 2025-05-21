# Game Profile Services

This directory contains concrete implementations of service interfaces related to the creation, management, and enrichment of game profiles. These services encapsulate the business logic for all game profile operations, interacting with data repositories and other core services.

## Service Implementations

*   **[`GameProfileFactory.cs`](GenHub/GenHub/Features/GameProfiles/Services/GameProfileFactory.cs)**:
    *   **Implements**: `IGameProfileFactory`
    *   **Purpose**: Responsible for creating new `GameProfile` instances. It provides methods to generate profiles from a `GameVersion`, an executable path, or a list of executables. It also handles the initial population of GitHub-specific metadata and normalization of game type strings.
    *   **Key Methods**: `CreateFromVersion`, `PopulateGitHubMetadata`, `CreateFromExecutableAsync`, `CreateFromExecutablesAsync`, `NormalizeGameType`.
    *   **Dependencies**: `ILogger<GameProfileFactory>`, [`ProfileResourceService`](GenHub/GenHub/Features/GameProfiles/Services/ProfileResourceService.cs), [`ProfileMetadataService`](GenHub/GenHub/Features/GameProfiles/Services/ProfileMetadataService.cs), `IGameExecutableLocator`.

*   **[`GameProfileManagerService.cs`](GenHub/GenHub/Features/GameProfiles/Services/GameProfileManagerService.cs)**:
    *   **Implements**: `IGameProfileManagerService`
    *   **Purpose**: The central service for managing the lifecycle (CRUD operations) of `GameProfile` objects. It handles saving, retrieving, updating, and deleting game profiles, interacting with an `IGameProfileRepository` for persistence. It also manages the creation of default profiles and raises an `ProfilesUpdated` event when the profile collection changes.
    *   **Key Methods**: `GetProfilesAsync`, `GetProfileAsync`, `CreateProfileFromVersionAsync`, `CreateDefaultProfilesAsync`, `DeleteProfileAsync`, `SaveProfileAsync`, `UpdateProfileAsync`, `AddProfileAsync`, `LoadCustomProfilesAsync`, `SaveCustomProfilesAsync`.
    *   **Dependencies**: `ILogger<GameProfileManagerService>`, `IGameProfileRepository`, `IGameVersionServiceFacade`, [`ProfileResourceService`](GenHub/GenHub/Features/GameProfiles/Services/ProfileResourceService.cs), [`ProfileMetadataService`](GenHub/GenHub/Features/GameProfiles/Services/ProfileMetadataService.cs).

*   **[`ProfileMetadataService.cs`](GenHub/GenHub/Features/GameProfiles/Services/ProfileMetadataService.cs)**:
    *   **Purpose**: Responsible for extracting, processing, and populating detailed metadata for game profiles. This includes deriving GitHub-related information (like PR numbers, commit SHAs, build presets) from various profile properties or associated `GameVersion` objects, generating user-friendly descriptions, and determining game type names.
    *   **Key Methods**: `ExtractGitHubInfo`, `LoadGitHubMetadataAsync`, `GenerateGameDescription`, `DetermineGameTypeName`.
    *   **Dependencies**: `ILogger<ProfileMetadataService>`, `IGameExecutableLocator`, `IGitHubApiClient`.

*   **[`ProfileResourceService.cs`](GenHub/GenHub/Features/GameProfiles/Services/ProfileResourceService.cs)**:
    *   **Purpose**: Manages visual resources associated with game profiles, such as icons and cover images. It scans for and provides lists of available built-in (embedded) and custom (file system) resources. It also offers methods to find default/appropriate resources for a given game type and to add new custom resources.
    *   **Key Methods**: `GetAvailableIcons`, `GetAvailableCovers`, `FindIconForGameType`, `FindCoverForGameType`, `AddCustomIconAsync`, `AddCustomCoverAsync`.
    *   **Dependencies**: `ILogger<ProfileResourceService>`, Avalonia's `AssetLoader`.

*   **[`ProfileSettingsDataProvider.cs`](GenHub/GenHub/Features/GameProfiles/Services/ProfileSettingsDataProvider.cs)**:
    *   **Implements**: `IProfileSettingsDataProvider`
    *   **Purpose**: Acts as a data source for the profile settings UI. It aggregates and provides various collections needed for user selections, such as available game versions, data paths, executable paths, icons, and cover images. It also includes functionality to scan for additional game versions.
    *   **Key Methods**: `GetAvailableVersionsAsync`, `GetAvailableDataPathsAsync`, `GetAvailableExecutablePathsAsync`, `GetAvailableIconsAsync`, `GetAvailableCoverImagesAsync`, `ScanForAdditionalVersionsAsync`.
    *   **Dependencies**: `ILogger<ProfileSettingsDataProvider>`, `IGameVersionServiceFacade`, [`ProfileResourceService`](GenHub/GenHub/Features/GameProfiles/Services/ProfileResourceService.cs), `IGameExecutableLocator`.

## Responsibilities and Interactions

*   These services implement the core logic for all game profile-related operations.
*   [`GameProfileFactory`](GenHub/GenHub/Features/GameProfiles/Services/GameProfileFactory.cs) creates profiles.
*   [`GameProfileManagerService`](GenHub/GenHub/Features/GameProfiles/Services/GameProfileManagerService.cs) manages their persistence and lifecycle.
*   [`ProfileMetadataService`](GenHub/GenHub/Features/GameProfiles/Services/ProfileMetadataService.cs) enriches profiles with relevant data.
*   [`ProfileResourceService`](GenHub/GenHub/Features/GameProfiles/Services/ProfileResourceService.cs) handles visual assets for profiles.
*   [`ProfileSettingsDataProvider`](GenHub/GenHub/Features/GameProfiles/Services/ProfileSettingsDataProvider.cs) supplies data to the settings UI.
*   They interact with repositories (e.g., `IGameProfileRepository`) for data storage and other services (e.g., `IGameVersionServiceFacade`, `IGameExecutableLocator`) to gather necessary information.
*   They are primarily consumed by view models in the UI layer (e.g., [`GameProfileSettingsViewModel`](GenHub/GenHub/Features/GameProfiles/ViewModels/GameProfileSettingsViewModel.cs), [`GameProfileLauncherViewModel`](GenHub/GenHub/Features/GameProfiles/ViewModels/GameProfileLauncherViewModel.cs)) to enable user interactions with game profiles.
