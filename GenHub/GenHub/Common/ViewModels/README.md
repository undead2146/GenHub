# Common ViewModels

This directory houses base view model classes and the primary view model for the main application window.

## ViewModels

*   **[`ViewModelBase.cs`](GenHub/GenHub/Common/ViewModels/ViewModelBase.cs)**:
    *   **Purpose**: Serves as a base class for all other view models in the application.
    *   **Logic**:
        *   Typically inherits from `ObservableObject` (from the CommunityToolkit.Mvvm library) to provide `INotifyPropertyChanged` implementation, enabling data binding.
        *   May include common properties or methods that are shared across multiple view models (e.g., `IsBusy` flags, common commands, or base initialization/cleanup logic).
        *   Promotes code reuse and a consistent structure for view models.

*   **[`MainViewModel.cs`](GenHub/GenHub/Common/ViewModels/MainViewModel.cs)**:
    *   **Purpose**: The main view model for the application's primary window or view (`MainWindow` or `MainView`).
    *   **Logic**:
        *   Orchestrates the overall UI state and navigation.
        *   May hold references to other view models representing different sections or features of the application.
        *   Handles global commands or application-level events.
        *   Its properties are bound to the UI elements in `MainView.axaml` or `MainWindow.axaml`.

## Responsibilities and Interactions

*   `ViewModelBase` provides the foundational MVVM capabilities. All feature-specific view models should inherit from it.
*   `MainViewModel` acts as the root view model for the user interface, managing the content displayed in the main application window. It interacts with various services to fetch and present data.
*   These view models are instantiated and their dependencies are resolved by the dependency injection container, typically configured in `App.axaml.cs`.
