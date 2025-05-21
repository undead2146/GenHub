# GitHub Feature ViewModels

This directory and its `Items` subdirectory contain ViewModel classes specific to the GitHub integration feature. These ViewModels follow the orchestrator pattern to maintain a clean separation of concerns while facilitating communication between components.

## Architecture Overview

The GitHub feature implements an **orchestrator pattern**:

* A primary orchestrator (`GitHubManagerViewModel`) coordinates specialized child ViewModels
* Child ViewModels handle specific responsibilities and communicate via events
* The orchestrator manages the application flow and global state
* No direct coupling between View and ViewModel (event-based window management)

## Core ViewModels

### Orchestrator

*   **`GitHubManagerViewModel.cs`**:
    *   **Purpose**: Acts as an orchestrator that coordinates specialized child ViewModels.
    *   **Key Responsibilities**:
        *   Holding references to child ViewModels (RepositoryControlVM, ContentModeFilterVM, GitHubItemsTreeVM, DetailsVM, InstallationVM)
        *   Handling events from child ViewModels and coordinating responses
        *   Managing global UI states (loading indicators, status messages)
        *   Handling top-level commands that require coordination across components
        *   Managing GitHub token configuration
        *   Exposing the CloseRequested event for MVVM-compliant window closing
    *   **Dependencies**: `ILogger`, `ITokenStorageService`, and all child ViewModels

### Child ViewModels

*   **`RepositoryControlViewModel.cs`**:
    *   **Purpose**: Manages GitHub repository selection and discovery.
    *   **Key Responsibilities**:
        *   Loading and displaying available GitHub repositories
        *   Handling repository selection changes
        *   Adding new custom repositories
        *   Validating and saving repositories
    *   **Dependencies**: `ILogger`, `IGitHubRepositoryManager`
    *   **Events**: `RepositoryChanged`, `RepositoryAdded`

*   **`ContentModeFilterViewModel.cs`**:
    *   **Purpose**: Controls content filtering options.
    *   **Key Responsibilities**:
        *   Managing available workflow definitions
        *   Handling workflow selection
        *   Determining display mode (All, Releases, Workflows)
        *   Loading workflow files for repositories
    *   **Dependencies**: `ILogger`, `IGitHubViewDataProvider`, `IGitHubRepositoryManager`
    *   **Events**: `WorkflowChanged`, `DisplayModeChanged`

*   **`GitHubItemsTreeViewModel.cs`**:
    *   **Purpose**: Manages the main tree of GitHub items (workflows, releases, artifacts).
    *   **Key Responsibilities**:
        *   Loading content based on selected repository and filter criteria
        *   Managing and filtering the item collection
        *   Handling search functionality
        *   Managing item selection
    *   **Dependencies**: `ILogger`, `IGitHubServiceFacade`, `IGitHubViewDataProvider`, `IGitHubDisplayItemFactory`
    *   **Events**: `ItemSelected`

*   **`GitHubDetailsViewModel.cs`**:
    *   **Purpose**: Displays detailed information about selected items.
    *   **Key Responsibilities**:
        *   Handling display of different item types (artifacts, workflows, releases)
        *   Managing the display of child items (workflow artifacts, release assets)
        *   Providing commands for item-specific actions
    *   **Dependencies**: `ILogger`, `IGitHubServiceFacade`
    *   **Events**: `InstallationCompleted`

*   **`InstallationViewModel.cs`**:
    *   **Purpose**: Manages artifact installation process.
    *   **Key Responsibilities**:
        *   Tracking installation progress
        *   Managing installation state
        *   Providing installation commands
    *   **Dependencies**: `ILogger`, `IGitHubArtifactInstaller`
    *   **Events**: `InstallationCompleted`

*   **`WorkflowDefinitionViewModel.cs`**:
    *   **Purpose**: Simple model for workflow definitions displayed in filtering controls.
    *   **Key Properties**: `Name`, `Path`, `DisplayName`

## Display Item ViewModels (Located in `ViewModels/Items/`)

These ViewModels inherit from `GitHubDisplayItemViewModel` and represent individual items in hierarchical lists or trees within the GitHub UI.

*   **`GitHubDisplayItemViewModel.cs` (Base Class)**:
    *   **Purpose**: An abstract base ViewModel for all items displayed in the GitHub feature's lists/trees.
    *   **Key Features**:
        *   Implements `IGitHubDisplayItem`
        *   Common properties: `DisplayName`, `SortDate`, `IsExpanded`, `IsSelected`, `IsLoading`, `IconKey`
        *   Manages a collection of `Children` (`ObservableCollection<IGitHubDisplayItem>`)
        *   Handles asynchronous loading of child items when an item is expanded

*   **`GitHubWorkflowDisplayItemViewModel.cs`**:
    *   **Purpose**: Represents a single GitHub Actions workflow run.
    *   **Wraps**: `GitHubWorkflow` model.
    *   **Key Features**: Loads associated `GitHubArtifactDisplayItemViewModel` instances as children when expanded.

*   **`GitHubArtifactDisplayItemViewModel.cs`**:
    *   **Purpose**: Represents a downloadable and potentially installable GitHub artifact.
    *   **Wraps**: `GitHubArtifact` model.
    *   **Key Features**: Provides `DownloadAsync` and `InstallAsync` commands.

*   **`GitHubReleaseDisplayItemViewModel.cs`**:
    *   **Purpose**: Represents a GitHub release.
    *   **Wraps**: `GitHubRelease` model.
    *   **Key Features**: Loads associated `GitHubReleaseAssetViewModel` instances as children when expanded.

*   **`GitHubReleaseAssetViewModel.cs`**:
    *   **Purpose**: Represents a single asset (file) attached to a GitHub release.
    *   **Wraps**: `GitHubReleaseAsset` model.
    *   **Key Features**: Provides a `DownloadAsync` command.

## Communication Flow

1. **User Selects Repository**: `RepositoryControlViewModel` raises `RepositoryChanged` event → `GitHubManagerViewModel` handles by:
   - Setting repository in `GitHubItemsTreeViewModel`
   - Loading workflow files via `ContentModeFilterViewModel` 
   - Clearing selection in `GitHubDetailsViewModel`

2. **User Selects Workflow Filter**: `ContentModeFilterViewModel` raises `WorkflowChanged` event → `GitHubManagerViewModel` handles by:
   - Updating context in `GitHubItemsTreeViewModel`
   - Triggering content reload

3. **User Selects Item in Tree**: `GitHubItemsTreeViewModel` raises `ItemSelected` event → `GitHubManagerViewModel` handles by:
   - Updating selected item in `GitHubDetailsViewModel`
   - Updating current artifact in `InstallationViewModel` if applicable

4. **User Installs Artifact**: `InstallationViewModel` performs installation and raises `InstallationCompleted` event → `GitHubManagerViewModel` handles by:
   - Updating status message
   - Refreshing items to reflect installation status
