# Facade Interfaces

This directory contains facade interfaces. A facade provides a simplified, higher-level interface to a more complex subsystem or a group of related services. The goal is to make common operations easier to perform and to reduce coupling between different parts of the application.

## Interfaces

*   **`IGameVersionServiceFacade.cs`**:
    *   **Purpose**: Provides a simplified interface for common operations related to game versions, potentially combining functionalities from `IGameVersionManager`, `IGameVersionDiscoveryService`, `IGameVersionInstaller`, and `IGameExecutableLocator`.
    *   **Key Methods (Conceptual)**:
        *   `DiscoverAndAddLocalGamesAsync()`: A high-level method to scan for local game installations, validate them, and add them to the library.
        *   `InstallGameFromSourceAsync(SourceDetails source, InstallOptions options)`: A unified method to install a game from a specified source (e.g., GitHub artifact URL, local ZIP path) with given options.
        *   `GetPlayableGameVersionsAsync()`: Retrieves a list of all installed and valid game versions ready for play.
        *   `LocateExecutableForVersionAsync(string versionId)`: A direct way to get the executable path for a game version.
    *   **Usage**: Used by higher-level application logic (e.g., UI view models) that needs to perform common game version tasks without interacting with multiple granular services.

*   **`IGitHubServiceFacade.cs`**:
    *   **Purpose**: Provides a simplified interface for common GitHub-related operations, potentially combining functionalities from `IGitHubApiClient`, `IGitHubArtifactService`, `IGitHubWorkflowRunService`, `IGitHubRepoService`, and `ITokenStorageService`.
    *   **Key Methods (Conceptual)**:
        *   `FetchAndProcessGameArtifactsAsync(GitHubRepository repoSettings, string workflowName)`: A high-level method to fetch workflow runs for a specific workflow, get their artifacts, parse build info, and return a list of potential game versions (e.g., as `GitHubArtifact` objects enriched with `BuildInfo`).
        *   `DownloadAndPrepareArtifactAsync(GitHubArtifact artifact, string downloadPath, IProgress<InstallProgress> progress)`: Downloads a specific artifact and prepares it for installation, potentially returning the path to the downloaded file.
        *   `GetRepositoryDetailsAsync(GitHubRepository repoSettings)`: Fetches and returns comprehensive details about a repository, including recent workflows or releases relevant to game updates.
        *   `SetGitHubToken(string token)` / `ClearGitHubToken()`: Simplified token management.
    *   **Usage**: Used by services like `IGameVersionDiscoveryService` (if it's discovering versions from GitHub) or UI components that display GitHub information, simplifying interactions with the GitHub subsystem.

## Responsibilities and Interactions

*   Facade interfaces aim to reduce the complexity of interacting with a set of related services by providing a single entry point for common use cases.
*   Implementations of these facades will delegate calls to the more specialized services within their respective subsystems.
*   They help in abstracting the underlying complexity and can make the codebase easier to understand and maintain, especially for new developers or when working on features that span multiple service responsibilities.
*   Facades can also help in managing dependencies, as clients only need to depend on the facade rather than multiple individual services.
