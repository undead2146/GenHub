# GitHub Data Models

This directory contains data models that represent entities and concepts from the GitHub API, specifically related to repositories, workflows, artifacts, and builds. These models are primarily used by services that interact with GitHub to fetch data, download artifacts, and provide context for game versions sourced from GitHub.

## Models

*   **`GitHubArtifact.cs`**:
    *   **Purpose**: Represents a downloadable build artifact from a GitHub Actions workflow run. This model aims to closely mirror the data structure provided by the GitHub API for an artifact, augmented with locally derived information.
    *   **Key Properties**: `Id`, `Name`, `WorkflowId`, `RunId`, `WorkflowNumber`, `SizeInBytes`, `ArchiveDownloadUrl`, `Expired`, `CreatedAt`, `ExpiresAt`, `PullRequestNumber`, `PullRequestTitle`, `CommitSha`, `CommitMessage`, `EventType`, `BuildPreset`.
    *   **Locally Populated**: `BuildInfo` (of type `GitHubBuild`), `RepositoryInfo` (of type `GitHubRepoSettings`).
    *   **Usage**: Central to identifying and downloading game versions. It's a core component of `GitHubSourceMetadata`. Populated by `IGitHubArtifactService`.

*   **`GitHubBuild.cs`**:
    *   **Purpose**: Represents structured build information parsed from an artifact's name or metadata (e.g., "ZeroHour-Release-VC6-t.zip").
    *   **Key Properties**: `GameVariant`, `Compiler`, `Configuration`, `Version`, `HasTFlag`, `HasEFlag`.
    *   **Usage**: Stored within `GitHubArtifact.BuildInfo` to provide detailed build characteristics. Created by parsing logic in `IGitHubArtifactService`.

*   **`GitHubDownloadException.cs`**:
    *   **Purpose**: A custom exception class specifically for errors encountered during the download of GitHub artifacts.
    *   **Usage**: Thrown by services like `IGitHubArtifactService` when artifact downloads fail, allowing for specific error handling.

*   **`GitHubRepoSettings.cs`**:
    *   **Purpose**: Stores configuration details for a GitHub repository that the application interacts with.
    *   **Key Properties**: `RepoOwner`, `RepoName`, `Token` (optional), `WorkflowFile` (optional), `Branch` (optional).
    *   **Usage**: Used by GitHub services to target specific repositories. Also attached to `GitHubArtifact` and `GitHubWorkflow` instances as `RepositoryInfo` for contextual reference.

*   **`GitHubWorkflow.cs`**:
    *   **Purpose**: Represents a single run of a GitHub Actions workflow.
    *   **Key Properties**: `RunId`, `WorkflowId` (of the definition), `Name` (of the workflow definition), `WorkflowPath`, `WorkflowNumber` (run number), `CreatedAt`, `CommitSha`, `CommitMessage`, `EventType`, `PullRequestNumber`, `PullRequestTitle`, `Status`, `Conclusion`, `HeadBranch`.
    *   **Locally Populated**: `Artifacts` (a list of `GitHubArtifact`), `RepositoryInfo` (of type `GitHubRepoSettings`).
    *   **Usage**: Provides the broader context for artifacts. Its details are used to populate parts of `GitHubSourceMetadata`. Populated by `IGitHubWorkflowRunService`.

## Responsibilities and Interactions

*   These models are primarily instantiated and populated by the GitHub-specific services (`IGitHubApiClient`, `IGitHubArtifactService`, `IGitHubWorkflowRunService`) by deserializing API responses and then enriching them.
*   `GitHubArtifact` and `GitHubWorkflow` are key inputs when creating `GitHubSourceMetadata` for a `GameVersion` or `GameProfile`.
*   `GitHubRepoSettings` provides the necessary configuration for the services to target the correct GitHub repositories.
*   `GitHubBuild` offers a structured way to understand the specifics of an artifact's contents based on naming conventions.
