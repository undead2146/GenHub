# Common Views

This directory contains the XAML definitions and code-behind for the main application window and its primary content view. These are the foundational visual elements of the GenHub application.

## Views

*   **[`MainWindow.axaml`](GenHub/GenHub/Common/Views/MainWindow.axaml)** & **[`MainWindow.axaml.cs`](GenHub/GenHub/Common/Views/MainWindow.axaml.cs)**:
    *   **Purpose**: Defines the main application window, which acts as the top-level container for the entire UI.
    *   **XAML (`.axaml`)**:
        *   Sets window properties like title, icon, initial size, and startup location.
        *   Typically hosts a `ContentControl` or a `NavigationVIew` that will display the content from `MainView.axaml` or other views based on the `MainViewModel`'s state.
        *   May define global styles or resources specific to the window.
    *   **Code-behind (`.axaml.cs`)**:
        *   Usually minimal, primarily containing the `InitializeComponent()` call in the constructor.
        *   May handle window-specific events if necessary, though most logic should reside in the `MainViewModel`.
        *   The `DataContext` is typically set to an instance of `MainViewModel` by `App.axaml.cs` upon application startup.

*   **[`MainView.axaml`](GenHub/GenHub/Common/Views/MainView.axaml)** & **[`MainView.axaml.cs`](GenHub/GenHub/Common/Views/MainView.axaml.cs)**:
    *   **Purpose**: Represents the primary content area within the `MainWindow`. This is where different feature views or dashboards are displayed.
    *   **XAML (`.axaml`)**:
        *   Defines the layout for the main content of the application. This could include navigation panels, status bars, and a central area for dynamic content.
        *   Binds to properties and commands exposed by `MainViewModel` to display data and handle user interactions.
        *   May use `ContentControl` elements bound to specific view model properties in `MainViewModel` to switch between different views (features).
    *   **Code-behind (`.axaml.cs`)**:
        *   Similar to `MainWindow.axaml.cs`, it's generally kept minimal.
        *   The `DataContext` is inherited from `MainWindow` or explicitly set if `MainView` is used independently with `MainViewModel`.

## Responsibilities and Interactions

*   `MainWindow` provides the application's main frame.
*   `MainView` defines the layout and content that appears within `MainWindow`.
*   Both views bind to `MainViewModel` (from `GenHub/GenHub/Common/ViewModels/`) for their data and actions.
*   `App.axaml.cs` is responsible for creating and showing `MainWindow` and setting its `DataContext` to `MainViewModel`.
*   Navigation and content switching within `MainView` are driven by changes in `MainViewModel`.
