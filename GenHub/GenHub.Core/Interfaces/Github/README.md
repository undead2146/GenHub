# GitHub Integration Interfaces

## Overview

This directory contains the interface definitions for GitHub-related services. These interfaces define the contract between the service implementations and their consumers.

## Key Interfaces

### API Communication

- **IGitHubApiClient**: Interface for the low-level GitHub API client that handles HTTP communication.
    - **Key Methods**:
        - `GetStreamAsync(GitHubRepository repo, string url)`: Fetches a resource as a stream (e.g., for downloading artifacts), potentially returning content length.
        - `GetArtifactsForRunAsync(GitHubRepository repo, long runId)`: Specifically fetches artifacts for a workflow run.
        - `HandleRateLimiting(HttpResponseMessage response)`: Logic to inspect response headers for rate limit information and potentially delay subsequent requests.
    - **Events**: `TokenMissing`, `TokenInvalid` (to signal issues with authentication).
    - **Usage**: Used by higher-level GitHub services (`IGitHubArtifactService`, `IGitHubWorkflowRunService`, etc.) to perform actual API calls.

### Feature-specific Interfaces

- **IGitHubArtifactService**
    - **Purpose**: Defines the contract for a service specializing in operations related to GitHub artifacts. This includes fetching artifact details, downloading artifacts, and parsing artifact-related metadata.
    - **Key Methods**:
        - `GetArtifactsForWorkflowRunAsync(GitHubRepository repo, long workflowRunId)`: Retrieves all artifacts associated with a specific workflow run.
        - `GetArtifactDetailsAsync(GitHubRepository repo, long artifactId)`: Fetches detailed information for a single artifact.
        - `DownloadArtifactAsync(GitHubArtifact artifact, string destinationPath, IProgress<InstallProgress> progress)`: Downloads an artifact to a specified path, reporting progress.
        - `ParseBuildInfo(string artifactName)`: Parses a `GitHubBuild` object from an artifact's name.
    - **Usage**: Used by `IGameVersionDiscoveryService` when sourcing game versions from GitHub artifacts, and by installation services to download game files.

- **IGitHubRepoService**
    - **Purpose**: Defines the contract for a service that handles operations related to GitHub repositories themselves, such as fetching repository details or listing repository contents.
    - **Key Methods**:
        - `GetRepositoryDetailsAsync(GitHubRepository repoSettings)`: Fetches general information about a repository.
        - `GetLatestReleaseAsync(GitHubRepository repoSettings)`: Fetches the latest release for a repository.
        - `ListBranchesAsync(GitHubRepository repoSettings)`: Lists branches in a repository.
    - **Usage**: Can be used to validate repository settings or to discover available branches/releases.

- **IGitHubSearchService**
    - **Purpose**: Defines the contract for a service that utilizes GitHub's search capabilities.
    - **Key Methods**:
        - `SearchRepositoriesAsync(string query)`: Searches for repositories matching a query.
        - `SearchCodeAsync(string query, GitHubRepository repoContext)`: Searches for code within a specific repository or across GitHub.
    - **Usage**: Could be used for features allowing users to find game repositories or specific game-related code on GitHub.

- **IGitHubWorkflowRunService**
    - **Purpose**: Defines the contract for a service specializing in operations related to GitHub Actions workflows and their runs.
    - **Key Methods**:
        - `GetWorkflowRunsAsync(GitHubRepository repo, string? workflowFileName = null, string? branch = null, string? eventType = null, int count = 30)`: Fetches a list of workflow runs for a repository, with optional filters.
        - `GetWorkflowRunDetailsAsync(GitHubRepository repo, long runId)`: Fetches detailed information for a specific workflow run, including its jobs and steps.
        - `GetArtifactCountsForWorkflowsAsync(GitHubRepository repo, IEnumerable<long> runIds)`: Efficiently gets artifact counts for multiple workflow runs.
    - **Usage**: Crucial for finding workflow runs that may contain game build artifacts. Works in conjunction with `IGitHubArtifactService`.

- **ITokenStorageService**
    - **Purpose**: Defines the contract for a service responsible for securely storing and retrieving a GitHub Personal Access Token (PAT).
    - **Key Methods**:
        - `SaveToken(string token)`: Saves/encrypts the GitHub token.
        - `RetrieveToken()`: Retrieves/decrypts the stored GitHub token.
        - `ClearToken()`: Removes the stored token.
    - **Usage**: Used by `IGitHubApiClient` to get the token needed for authenticated requests to the GitHub API, especially for private repositories or to increase rate limits.

### Facade Pattern

- **IGitHubServiceFacade**: High-level facade interface that simplifies access to all GitHub functionality.

## Responsibilities and Interactions

- `ITokenStorageService` provides authentication tokens to `IGitHubApiClient`.
- `IGitHubApiClient` is the foundational layer, making raw API calls.
- `IGitHubArtifactService`, `IGitHubWorkflowRunService`, `IGitHubRepoService`, and `IGitHubSearchService` are higher-level services that use `IGitHubApiClient` to provide more specific, domain-oriented functionalities related to GitHub entities.
- These services work together to enable the application to discover, download, and manage game versions sourced from GitHub. They populate the GitHub-specific data models (`GitHubArtifact`, `GitHubWorkflow`, etc.).
