# Game Profile ViewModels

This directory contains view models specific to the game profiles feature. These view models manage the state and logic for UI components that display game profile information, allow users to create, edit, and manage profiles, and select profiles for launching games. They follow the MVVM pattern, typically using `CommunityToolkit.Mvvm` for observable properties and commands.

## ViewModels

*   **[`GameProfileItemViewModel.cs`](GenHub/GenHub/Features/GameProfiles/ViewModels/GameProfileItemViewModel.cs)**:
    *   **Purpose**: Represents a single `GameProfile` (by implementing `IGameProfile`) in a list or selection UI. It wraps the `GameProfile` model and adds UI-specific properties (e.g., `ShortCommitSha`, `SourceTypeName`) and may include a `LaunchCommand`.
    *   **Key Properties**: Exposes all `IGameProfile` properties as observable. Includes computed properties for display.
    *   **Dependencies**: `IGameLauncherService` (optional, for initializing `LaunchCommand`).

*   **[`GameProfileLauncherViewModel.cs`](GenHub/GenHub/Features/GameProfiles/ViewModels/GameProfileLauncherViewModel.cs)**:
    *   **Purpose**: Manages the main UI logic for displaying, selecting, launching, and managing game profiles. It handles a collection of `GameProfileItemViewModel` instances.
    *   **Key Responsibilities**:
        *   Loading and displaying available game profiles.
        *   Handling profile selection.
        *   Initiating game launch via `IGameLauncherService`.
        *   Managing an "edit mode" for profiles.
        *   Coordinating the creation, editing, and deletion of profiles through dialogs and services.
        *   Scanning for new game installations to create profiles.
    *   **Key Properties**: `Profiles` (ObservableCollection), `SelectedProfile`, `StatusMessage`, `IsLoading`, `IsLaunching`, `IsEditMode`, `IsScanning`.
    *   **Key Commands**: `InitializeAsync`, `ToggleEditMode`, `SaveProfiles`, `EditProfile`, `CreateNewProfile`, `DeleteProfile`, `ScanForGames`, `LaunchProfileAsync`.
    *   **Dependencies**: `ILogger`, `IGameLauncherService`, `IGameVersionServiceFacade`, `IGameProfileManagerService`, `GameDetectionFacade`, `IGameExecutableLocator`, `IGameProfileFactory`.

*   **[`GameProfileSettingsViewModel.cs`](GenHub/GenHub/Features/GameProfiles/ViewModels/GameProfileSettingsViewModel.cs)**:
    *   **Purpose**: The view model for the `GameProfileSettingsWindow`, responsible for creating a new `GameProfileItemViewModel` or editing an existing one. It binds to the properties of the profile and handles validation and saving.
    *   **Key Responsibilities**:
        *   Initializing with a new or existing `GameProfileItemViewModel`.
        *   Providing collections of selectable items (versions, paths, icons, covers) fetched via `IProfileSettingsDataProvider`.
        *   Handling user input for all profile properties.
        *   Validating user input and managing `CanSave` state.
        *   Saving changes via `IGameProfileManagerService`.
        *   Interacting with `IFileDialogService` for browsing paths.
    *   **Key Properties**: `Profile` (the `GameProfileItemViewModel` being edited), `AvailableVersions`, `SelectedVersion`, `AvailableDataPaths`, `SelectedDataPath`, `AvailableExecutablePaths`, `SelectedExecutablePath`, `AvailableIcons`, `SelectedIcon`, `AvailableCovers`, `SelectedCover`, `IsLoading`, `IsNewProfile`, `CanSave`, `StatusMessage`, various computed properties for displaying GitHub metadata.
    *   **Key Commands**: `SaveProfileCommand`, `CancelOperationCommand`, `RandomizeColorCommand`, `BrowseExecutablePathCommand`, `BrowseDataPathCommand`, `ScanForGamesCommand`, `DeleteProfileCommand`.
    *   **Dependencies**: `ILogger`, `GameProfileSettingsWindow` (view reference), `IFileDialogService`, `IProfileSettingsDataProvider`, `IGameProfileManagerService`, `ProfileMetadataService`.

*   **[`ProfileIconViewModel.cs`](GenHub/GenHub/Features/GameProfiles/ViewModels/ProfileIconViewModel.cs)**:
    *   **Purpose**: A simple view model representing a selectable icon or cover image in a list or grid, primarily used within `GameProfileSettingsViewModel` for users to pick visual resources for their profile.
    *   **Key Properties**: `DisplayName`, `Path`.
    *   **Usage**: Populates icon/cover selection controls in the profile settings UI.

## Responsibilities and Interactions

*   These view models prepare `GameProfile` data for display and handle user interactions related to profile management and launching.
*   They use services from `GenHub.Features.GameProfiles.Services` and relevant Core services/interfaces (e.g., `IGameLauncherService`, `IFileDialogService`).
*   They implement `INotifyPropertyChanged` (typically via `ObservableObject` from CommunityToolkit.Mvvm) and expose commands (typically `RelayCommand` or `AsyncRelayCommand`) for UI binding.
*   They act as intermediaries between the XAML Views and the underlying services and models, ensuring a clean separation of concerns according to the MVVM pattern.
