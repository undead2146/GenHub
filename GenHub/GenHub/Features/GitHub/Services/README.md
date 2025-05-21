# GitHub Feature Services

This directory contains all services responsible for interacting with the GitHub API, processing GitHub data, managing local GitHub-related configurations, and providing a structured interface for the application's ViewModels to access GitHub functionalities.

## Overview

The services within this module encapsulate the core logic for features such as:
- Fetching workflow runs, artifacts, and releases from GitHub.
- Downloading and installing artifacts as game versions.
- Managing GitHub repository configurations.
- Searching and filtering GitHub data.
- Providing data to the GitHub-related UI components in a ViewModel-friendly format.

These services are designed to be injectable and adhere to the principles of separation of concerns and the Facade pattern for simplified access.

## Directory Structure

The services are organized as follows:

*   **Root Level**: Contains the main facade, search services, and view-specific data providers.
    *   `GitHubServiceFacade.cs`
    *   `GitHubSearchService.cs`
    *   `GitHubViewDataProvider.cs`
*   [**ApiClients**](./ApiClients/README.md): Houses the low-level client for direct GitHub API communication.
*   [**ArtifactServices**](./ArtifactServices/README.md): Contains services for reading and installing GitHub artifacts.
*   [**ReleaseServices**](./ReleaseServices/README.md): Contains services for reading GitHub release information.
*   [**RepositoryServices**](./RepositoryServices/README.md): Manages GitHub repository configurations used by the application.
*   [**WorkflowServices**](./WorkflowServices/README.md): Contains services for reading GitHub Actions workflow run data.

## Key Service Components

### 1. `GitHubServiceFacade.cs`
*   **Interface**: `IGitHubServiceFacade`
*   **Purpose**: Acts as the primary entry point for GitHub operations. It simplifies interactions with the more specialized GitHub services by providing a unified API.
*   **Responsibilities**:
    *   Orchestrating calls to `IGitHubApiClient`, `IGitHubWorkflowReader`, `IGitHubArtifactReader`, `IGitHubArtifactInstaller`, and `IGitHubReleaseReader`.
    *   Managing GitHub authentication token (`SetAuthToken`, `GetAuthToken`, `HasAuthToken`).
    *   Providing methods to get workflow runs, artifacts, and download artifacts for configured or specified repositories.
    *   Exposing repository management functions from `GitHubRepositoryManager`.
*   **Pattern**: Implements the Facade pattern.

### 2. `ApiClients/GitHubApiClient.cs`
*   **Interface**: `IGitHubApiClient`
*   **Purpose**: Handles direct, low-level communication with the GitHub REST API.
*   **Responsibilities**:
    *   Constructing and sending HTTP requests to GitHub API endpoints.
    *   Handling authentication headers (using the token provided via `GitHubServiceFacade`).
    *   Deserializing JSON responses into `GenHub.Core.Models.GitHub` domain models (e.g., `GitHubWorkflow`, `GitHubArtifact`, `GitHubRelease`).
    *   Managing API rate limits and error handling at the HTTP level.

### 3. `ArtifactServices/GitHubArtifactReader.cs`
*   **Interface**: `IGitHubArtifactReader` (if defined, otherwise direct use)
*   **Purpose**: Specialized in fetching and processing information about GitHub workflow artifacts.
*   **Responsibilities**:
    *   Retrieving lists of artifacts for specific workflow runs using `IGitHubApiClient`.
    *   Potentially parsing `GitHubBuild` information from artifact names or metadata using helpers like `GitHubModelExtensions.ParseBuildInfo`.

### 4. `ArtifactServices/GitHubArtifactInstaller.cs`
*   **Interface**: `IGitHubArtifactInstaller` (if defined, otherwise direct use)
*   **Purpose**: Manages the download and installation of GitHub artifacts as game versions.
*   **Responsibilities**:
    *   Downloading the artifact ZIP archive (often via `IGitHubApiClient` or a download stream from `GitHubServiceFacade`).
    *   Extracting the contents of the archive.
    *   Interacting with `IGameVersionInstaller` or `IGameVersionManager` to register the extracted files as a new `GameVersion`.
    *   Reporting installation progress.

### 5. `ReleaseServices/GitHubReleaseReader.cs`
*   **Interface**: `IGitHubReleaseReader` (if defined, otherwise direct use)
*   **Purpose**: Dedicated to fetching and processing information about GitHub releases and their assets.
*   **Responsibilities**:
    *   Retrieving release data (including `GitHubReleaseAsset` lists) for specified repositories using `IGitHubApiClient`.

### 6. `RepositoryServices/GitHubRepositoryManager.cs`
*   **Interface**: `IGitHubRepositoryManager` (if defined, otherwise direct use)
*   **Purpose**: Manages the `GitHubRepoSettings` (repository configurations) that the application uses to fetch data.
*   **Responsibilities**:
    *   Loading and saving the list of `GitHubRepoSettings` (e.g., from application configuration or a dedicated JSON file via `ICacheService` or `IJsonRepository`).
    *   Providing access to the list of configured repositories and the currently selected/default repository.

### 7. `WorkflowServices/GitHubWorkflowReader.cs`
*   **Interface**: `IGitHubWorkflowReader`
*   **Purpose**: Focuses on retrieving and processing data related to GitHub Actions workflow runs.
*   **Responsibilities**:
    *   Fetching lists of workflow runs for specified repositories, including pagination, using `IGitHubApiClient`.
    *   Retrieving details for individual workflow runs.
    *   Potentially enriching workflow run data with artifact counts or summaries.

### 8. `GitHubSearchService.cs`
*   **Purpose**: Provides functionality to filter and search collections of GitHub display items (e.g., `IGitHubDisplayItem` representing workflows or artifacts).
*   **Responsibilities**:
    *   Applying search terms and criteria (`GitHubSearchCriteria`) to in-memory collections of GitHub items.
    *   Used by ViewModels like `GitHubManagerViewModel` to implement client-side filtering.

### 9. `GitHubViewDataProvider.cs`
*   **Purpose**: Acts as a higher-level data provider tailored for the GitHub UI components, particularly `GitHubManagerViewModel`.
*   **Responsibilities**:
    *   Orchestrating the fetching of data from various GitHub services (`GitHubWorkflowReader`, `GitHubReleaseReader`, etc.).
    *   Managing caching strategies for fetched data, likely interacting with `IGitHubCachingRepository` or `ICacheService`.
    *   Transforming raw GitHub models into displayable ViewModel items, often using `GitHubDisplayItemFactory`.
    *   Handling pagination and "load more" functionality for lists of GitHub items.

## Architectural Patterns

*   **Facade Pattern**: The `GitHubServiceFacade` simplifies access to the subsystem of GitHub services.
*   **Dependency Injection (DI)**: All services are designed to be resolved via DI, allowing for loose coupling and easier testing. Dependencies like `HttpClient`, `ILogger`, `ICacheService`, and other GitHub services are injected.
*   **Separation of Concerns**: Each service has a well-defined responsibility (e.g., API communication, artifact reading, release reading, installation).
*   **Repository Pattern (for Caching/Configuration)**: Services like `GitHubRepositoryManager` and `GitHubViewDataProvider` may interact with repository-pattern implementations (e.g., `IJsonRepository`, `IGitHubCachingRepository`) for persisting configurations or caching API responses.

These services work together to provide a comprehensive and robust backend for all GitHub-related features in the GenHub application.
