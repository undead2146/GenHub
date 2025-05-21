# Game Profile Views

This directory contains the UI views (windows, user controls) specific to the game profiles feature. These views provide the user interface for creating, editing, managing, and launching game profiles. They are built using Avalonia UI (AXAML/XAML).

## Views

*   **[`GameProfileLauncherView.axaml`](GenHub/GenHub/Features/GameProfiles/Views/GameProfileLauncherView.axaml) / [`GameProfileLauncherView.axaml.cs`](GenHub/GenHub/Features/GameProfiles/Views/GameProfileLauncherView.axaml.cs)**:
    *   **Purpose**: A user control that serves as the main dashboard for game profiles. It displays a list or grid of available game profiles, allowing users to select one to launch, edit existing profiles, or initiate the creation of new profiles.
    *   **DataContext**: Typically an instance of [`GameProfileLauncherViewModel`](GenHub/GenHub/Features/GameProfiles/ViewModels/GameProfileLauncherViewModel.cs).
    *   **Content**: Features an `ItemsControl` or similar to display `GameProfileItemViewModel` instances as interactive cards. Includes buttons for global actions like "Add Profile", "Scan for Games", and toggling an "Edit Mode". Each profile card usually has "Launch" and "Edit" buttons.
    *   **Code-behind (`.axaml.cs`)**: Handles view-specific concerns such as initializing the ViewModel upon loading, proxying commands (e.g., `EditProfileProxyCommand`) for elements within data templates, and managing UI interactions like `OnProfileCardPressed` to toggle edit button visibility or directly trigger an edit action.

*   **[`GameProfileSettingsWindow.axaml`](GenHub/GenHub/Features/GameProfiles/Views/GameProfileSettingsWindow.axaml) / [`GameProfileSettingsWindow.axaml.cs`](GenHub/GenHub/Features/GameProfiles/Views/GameProfileSettingsWindow.axaml.cs)**:
    *   **Purpose**: A dialog window used for creating a new game profile or editing an existing one. It provides a comprehensive set of input fields and selection controls for all configurable profile properties.
    *   **DataContext**: An instance of [`GameProfileSettingsViewModel`](GenHub/GenHub/Features/GameProfiles/ViewModels/GameProfileSettingsViewModel.cs).
    *   **Content**: Organized using a `TabControl` (e.g., "General", "Game Version", "Advanced"). Contains input fields for profile name, description, executable path, data path, command-line arguments. Includes ComboBoxes or ListBoxes for selecting game versions, icons, and cover images. Provides a color picker or selection mechanism for the profile's theme color. Features "Save" and "Cancel" buttons. A loading overlay is shown during asynchronous operations.
    *   **Code-behind (`.axaml.cs`)**: Manages the dialog's lifecycle, including showing the window (`ShowAsync`), handling window closure, and returning a `DialogResult`. It wires up UI events (like button clicks for Save/Cancel and TabControl selection changes) to ViewModel commands or methods. It's responsible for passing the `GameProfile` to the ViewModel for initialization.

*(Note: `Readme.copy.md` seems like a duplicate or backup and is not typically part of the active view structure unless it serves a specific documentation purpose for a template.)*

## Responsibilities and Interactions

*   These views are responsible for the visual presentation of game profile data and for capturing user input related to profile configuration and selection.
*   They bind to properties and commands exposed by their corresponding view models from [`GenHub.Features.GameProfiles.ViewModels`](GenHub/GenHub/Features/GameProfiles/ViewModels/README.md).
*   Code-behind files (`.axaml.cs`) are generally kept minimal, with the majority of UI logic and state management handled by the view models, adhering to the MVVM (Model-View-ViewModel) pattern.
*   They utilize standard Avalonia UI controls and may employ custom value converters (from `GenHub.Infrastructure.Converters`) for data display formatting and conditional visibility.
