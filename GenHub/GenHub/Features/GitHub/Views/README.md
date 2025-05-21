# GitHub Feature Views

This directory contains the user interface (UI) components for the GitHub feature module, implemented using Avalonia UI (AXAML files and their C# code-behind). These views are responsible for presenting GitHub-related data to the user and capturing user input, forming the "View" layer in the MVVM architecture.

## Overview

Views in the GitHub feature are designed to be lightweight and primarily declarative (XAML-based). They bind to ViewModel properties for data display and to ViewModel commands for user actions. The logic for how to respond to user interactions and manage application state resides within the corresponding ViewModels.

## Key Components

*   **`GitHubManagerWindow.axaml` / `GitHubManagerWindow.axaml.cs`**:
    *   **Purpose**: This is the main UI component for browsing and interacting with GitHub workflows, releases, and artifacts. It typically displays hierarchical data (e.g., workflows containing artifacts) and provides filtering and selection capabilities.
    *   **ViewModel**: Binds to `GitHubManagerViewModel`.
    *   **Functionality**: Displays lists of GitHub items, allows selection to view details, and may include search/filter inputs. The code-behind handles some view-specific event wiring and initialization.

*   **`GitHubArtifactDetailsView.axaml` / `GitHubArtifactDetailsView.axaml.cs`**:
    *   **Purpose**: A `UserControl` designed to display detailed information about a currently selected GitHub artifact (or potentially a release asset).
    *   **ViewModel**: Typically binds to a ViewModel like `GitHubArtifactDetailsViewModel` or directly to properties of a selected item ViewModel (e.g., `GitHubArtifactDisplayItemViewModel`).
    *   **Functionality**: Shows properties like name, size, run number, build information, and provides action buttons (e.g., Download, Install).

*   **`GitHubTokenDialogWindow.axaml` / `GitHubTokenDialogWindow.axaml.cs`**:
    *   **Purpose**: A dialog window (`Window`) used to prompt the user to enter their GitHub Personal Access Token (PAT).
    *   **Functionality**: Provides an input field for the token and OK/Cancel buttons. The code-behind includes logic for basic token validation and returning the entered token. This is a self-contained UI component for a specific interaction.

*   **`LoadingOverlay.axaml` / `LoadingOverlay.axaml.cs`**:
    *   **Purpose**: A reusable `UserControl` that displays a loading indicator (e.g., a progress ring or animation) when the application is performing background tasks, such as fetching data from the GitHub API.
    *   **Functionality**: Its visibility is typically bound to an `IsLoading` property in a ViewModel.

## MVVM Adherence

*   **Data Binding**: Views extensively use Avalonia's data binding features to connect UI elements to ViewModel properties and commands.
*   **Commanding**: User actions (button clicks, etc.) are typically bound to `ICommand` implementations in the ViewModels.
*   **Minimal Code-Behind**: Code-behind files (`.axaml.cs`) are kept minimal. They are primarily used for:
    *   Component initialization (`InitializeComponent()`).
    *   View-specific event handling that is difficult to achieve with pure XAML or commands (though this is minimized).
    *   Interactions with the Avalonia framework that are inherently view-related (e.g., `AttachedToVisualTree`, dialog management).

These views provide the visual interface for the GitHub functionalities, ensuring a separation from the application's business logic and state, which are managed by the ViewModels.
