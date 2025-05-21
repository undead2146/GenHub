# Game Profiles Feature

This module handles all aspects of game profile management, allowing users to create, customize, and manage multiple launch configurations for their installed game versions. It provides the UI and underlying logic for users to tailor their gaming experience for different scenarios, such as different mods or settings for Command & Conquer Generals and Zero Hour.

## Feature Components

The Game Profiles feature is organized into the following sub-directories:

*   **[`/Services`](GenHub/GenHub/Features/GameProfiles/Services/README.md)**: Contains concrete service implementations for profile creation ([`GameProfileFactory`](GenHub/GenHub/Features/GameProfiles/Services/GameProfileFactory.cs)), management ([`GameProfileManagerService`](GenHub/GenHub/Features/GameProfiles/Services/GameProfileManagerService.cs)), metadata handling ([`ProfileMetadataService`](GenHub/GenHub/Features/GameProfiles/Services/ProfileMetadataService.cs)), resource management ([`ProfileResourceService`](GenHub/GenHub/Features/GameProfiles/Services/ProfileResourceService.cs)), and data provision for settings UI ([`ProfileSettingsDataProvider`](GenHub/GenHub/Features/GameProfiles/Services/ProfileSettingsDataProvider.cs)).
*   **[`/ViewModels`](GenHub/GenHub/Features/GameProfiles/ViewModels/README.md)**: Includes view models that manage the state and logic for the profile UIs. Key view models are [`GameProfileItemViewModel`](GenHub/GenHub/Features/GameProfiles/ViewModels/GameProfileItemViewModel.cs) (for displaying individual profiles), [`GameProfileLauncherViewModel`](GenHub/GenHub/Features/GameProfiles/ViewModels/GameProfileLauncherViewModel.cs) (for the main profile dashboard, selecting, and launching), [`GameProfileSettingsViewModel`](GenHub/GenHub/Features/GameProfiles/ViewModels/GameProfileSettingsViewModel.cs) (for creating/editing profiles), and [`ProfileIconViewModel`](GenHub/GenHub/Features/GameProfiles/ViewModels/ProfileIconViewModel.cs) (for icon/cover selection).
*   **[`/Views`](GenHub/GenHub/Features/GameProfiles/Views/README.md)**: Contains the XAML views (e.g., [`GameProfileLauncherView`](GenHub/GenHub/Features/GameProfiles/Views/GameProfileLauncherView.axaml), [`GameProfileSettingsWindow`](GenHub/GenHub/Features/GameProfiles/Views/GameProfileSettingsWindow.axaml)) that provide the user interface for interacting with game profiles.

## Core Functionalities

*   **Profile Creation & Editing**:
    *   Allows users to create new profiles associated with a specific `GameVersion`.
    *   Enables editing of existing profile details: name, description, executable path, game data path, command-line arguments, administrative privileges.
*   **Profile Resource Management**:
    *   Assigning custom icons and cover images to profiles.
    *   Selecting a profile-specific color tint.
    *   Managing paths to these visual resources, utilizing both embedded and user-provided assets.
*   **Profile Management**:
    *   Listing available profiles, typically associated with game versions.
    *   Setting a default profile for a game.
    *   Deleting unwanted profiles.
    *   Persisting profile data through repository services.
    *   Scanning for existing game installations to automatically create basic profiles.
*   **Profile Launching**:
    *   Integrates with the `IGameLauncherService` to launch a game using the settings from a selected profile.
*   **Metadata Handling**:
    *   Populating profile metadata, potentially inheriting from the associated `GameVersion` (especially GitHub-related metadata like PR numbers, commit SHAs, build presets).
    *   Generating descriptive text for profiles based on their source and metadata.

## Interactions with Other Modules

*   **`GenHub.Core`**:
    *   Implements interfaces from `GenHub.Core.Interfaces.GameProfiles` (e.g., `IGameProfileFactory`, `IGameProfileManagerService`, `IProfileSettingsDataProvider`).
    *   Uses models from `GenHub.Core.Models.GameProfiles` (e.g., `GameProfile`, `ProfileResourceItem`) and `GenHub.Core.Models` (e.g., `GameVersion`, `DialogResult`).
*   **Game Versions Feature (`GenHub.Features.GameVersions`)**:
    *   Profiles are intrinsically linked to `GameVersion` objects.
    *   The `IGameLauncherService` (typically implemented in the GameVersions feature) consumes `IGameProfile` data to launch games.
    *   Services like `IGameVersionServiceFacade` are used to retrieve game version information.
    *   The `GameDetectionFacade` is used to help in scanning for games to create initial profiles.
*   **Infrastructure Layer (`GenHub.Infrastructure`)**:
    *   Relies on `IGameProfileRepository` (an implementation of `IRepository<GameProfile>` from `GenHub.Core.Interfaces.Repositories`) for data persistence.
    *   Uses `IFileDialogService` (from `GenHub.Core.Interfaces.UI`, implemented in UI features or Infrastructure) for UI interactions like file/folder picking.
*   **UI Layer (Avalonia)**:
    *   The Views and ViewModels in this feature provide the direct user interface for all profile-related actions, adhering to the MVVM pattern.

This feature significantly enhances user flexibility by allowing tailored launch experiences for different scenarios or mod configurations of the same game version.
