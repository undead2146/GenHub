# GitHub Integration Feature

This feature module encapsulates all functionalities related to integrating with GitHub. This includes fetching information about repositories, discovering and downloading game versions from GitHub Actions artifacts, managing GitHub workflow runs, and handling GitHub Personal Access Tokens for authentication.

## Feature Components

The GitHub feature is organized into the following sub-directories:

*   **`/Helpers`**: Contains utility classes and extension methods specific to GitHub data processing, such as `GitHubSourceMetadataExtensions` for converting and accessing GitHub-specific metadata.
*   **`/Services`**: Provides concrete implementations of GitHub service interfaces (e.g., `GitHubApiClient`, `GitHubArtifactService`, `GitHubWorkflowRunService`, `GitHubServiceFacade`). These services handle all direct communication with the GitHub API and related business logic.
*   **`/ViewModels`**: Includes view models (e.g., `GitHubManagerViewModel`, `WorkflowItemViewModel`) that manage the state and logic for the GitHub feature's user interface. They mediate between the views and the GitHub services.
*   **`/Views`**: Contains the XAML views (windows, user controls, dialogs like `GitHubManagerWindow`, `GitHubTokenDialogWindow`) that provide the user interface for interacting with GitHub functionalities.

## Core Functionalities

*   **Repository Management**: Configuring and interacting with specific GitHub repositories.
*   **Workflow Discovery**: Fetching and displaying information about GitHub Actions workflow runs.
*   **Artifact Management**:
    *   Discovering build artifacts associated with workflow runs.
    *   Parsing build information (e.g., game variant, configuration) from artifact names.
    *   Downloading artifact ZIP files.
    *   Initiating the installation of game versions from downloaded artifacts.
*   **Authentication**: Managing GitHub Personal Access Tokens (PATs) for authenticated API requests, enabling access to private repositories and higher rate limits.
*   **UI Presentation**: Providing user interfaces for browsing GitHub content, managing settings, and monitoring downloads.

## Interactions with Other Modules

*   **`GenHub.Core`**:
    *   Implements interfaces defined in `GenHub.Core.Interfaces.Github` and `GenHub.Core.Interfaces.Facades`.
    *   Uses models from `GenHub.Core.Models.GitHub` (e.g., `GitHubArtifact`, `GitHubWorkflow`) and `GenHub.Core.Models.SourceMetadata` (e.g., `GitHubSourceMetadata`).
*   **Game Version Feature**: Works closely with the game version management feature to discover, download, and register game versions sourced from GitHub. Services like `IGameVersionDiscoveryService` and `IGameVersionInstaller` will consume services from this GitHub feature.
*   **Infrastructure Layer**:
    *   Uses `ITokenStorageService` (implemented in Infrastructure) for secure token handling.
    *   May use `ICachingService` for caching API responses.
    *   Relies on DI registrations for its services.

This feature is central to enabling GenHub to source game versions directly from GitHub build pipelines, providing users with easy access to the latest builds and development versions.
