# Source Metadata Models

This directory contains models designed to hold metadata specific to the source of a `GameVersion` or `GameProfile`. This allows for a flexible way to store detailed information from various origins (e.g., GitHub, local builds, other repositories) without cluttering the core `GameVersion` or `GameProfile` models.

## Models

*   **`BaseSourceMetadata.cs`**:
    *   **Purpose**: An abstract base class for all types of source-specific metadata. It enables `GameVersion` and `GameProfile` to hold different kinds of metadata polymorphically via their `SourceSpecificMetadata` property.
    *   **Properties**: Can be extended with common properties if any are identified across all potential source types (e.g., `SourceSystemName`, `RetrievalDate`).
    *   **Usage**: Subclassed by concrete metadata types like `GitHubSourceMetadata`.

*   **`GitHubSourceMetadata.cs`**:
    *   **Purpose**: Holds GitHub-specific metadata for a `GameVersion` or `GameProfile` when its `SourceType` is `GameInstallationType.GitHubArtifact`. It acts as a curated container, primarily composing a `GitHubArtifact` object and adding essential workflow-level context that might not be directly on the artifact itself.
    *   **Key Properties (Serialized)**:
        *   `AssociatedArtifact` (type `GitHubArtifact`): The core link to the detailed artifact information.
        *   `WorkflowDefinitionName`, `WorkflowDefinitionPath`, `WorkflowRunStatus`, `WorkflowRunConclusion`, `SourceControlBranch`: Context from the parent `GitHubWorkflow` run.
    *   **Convenience Accessors (`[JsonIgnore]`)**: Provides easy access to many properties derived from `AssociatedArtifact` (e.g., `BuildInfo`, `ArtifactId`, `PullRequestNumber`, `CommitSha`, `WorkflowRunNumber`).
    *   **Creation**: Typically created using the `GitHubExtensions.ToSourceMetadata()` extension method, which takes a `GitHubArtifact` and an optional `GitHubWorkflow`.
    *   **Usage**: Stored in `GameVersion.SourceSpecificMetadata` (and subsequently in `GameProfile.SourceSpecificMetadata`). Provides all necessary GitHub context to the domain models and UI.

## Responsibilities and Interactions

*   The primary role of these models is to decouple source-specific details from the main `GameVersion` and `GameProfile` models.
*   `GitHubSourceMetadata` is populated by domain services or factories (e.g., `IGameVersionDiscoveryService`, `IGameProfileFactory`) using data fetched by GitHub-specific services (`IGitHubArtifactService`, `IGitHubWorkflowRunService`).
*   The `GameVersion` and `GameProfile` models can then access this rich, typed metadata through their `SourceSpecificMetadata` property (often via a casted convenience property like `GitHubMetadata`).
*   This structure allows for future expansion to other source types (e.g., `GitLabSourceMetadata`, `LocalBuildSourceMetadata`) by simply creating new classes that inherit from `BaseSourceMetadata`.
