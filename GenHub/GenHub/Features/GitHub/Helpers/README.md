# GitHub Feature Helpers

This directory contains utility classes and extension methods designed to support the GitHub feature module. These helpers provide supplementary functionalities, data transformations, and parsing logic related to GitHub domain models and associated metadata.

## Overview

The primary goal of these helpers is to encapsulate common operations and logic that don't belong directly within the core model classes or the main service/ViewModel logic. This promotes cleaner, more readable, and maintainable code by providing reusable utility functions.

## Key Components

*   **`GitHubModelExtensions.cs`**:
    *   **Purpose**: Provides extension methods for various GitHub-related models (e.g., `GitHubArtifact`, `GitHubBuild`, `GitHubRepoSettings` from `GenHub.Core.Models`).
    *   **Functionality**: Includes methods for:
        *   Creating deep copies of model instances (e.g., `CreateCopy`).
        *   Generating display-friendly strings (e.g., `GetDisplayName`, `GetFormattedSize`).
        *   Parsing structured information from less structured data (e.g., `ParseBuildInfo` from an artifact name).
        *   Updating metadata on models (e.g., `SetArtifactId`, `SetPullRequestInfo` on `GitHubSourceMetadata` via an artifact).
    *   **MVVM Role**: These extensions often prepare data from Models for easier consumption by ViewModels or assist in mapping data between different model types.

*   **`GitHubSourceMetadataExtensions.cs`**:
    *   **Purpose**: Provides extension methods for converting GitHub-specific models (`GitHubArtifact`, `GitHubReleaseAsset`) into a standardized `GitHubSourceMetadata` representation and vice-versa.
    *   **Functionality**: Includes methods for:
        *   Converting `GitHubArtifact` and `GitHubReleaseAsset` to `GitHubSourceMetadata`.
        *   Retrieving `GitHubSourceMetadata` from `GameVersion` or `IGameProfile`.
        *   Setting GitHub metadata onto `GameVersion` objects.
        *   Creating display names based on `GitHubSourceMetadata`.
    *   **Architectural Role**: Facilitates a consistent way of tracking the origin and context of game versions or profiles that come from GitHub, bridging the gap between raw GitHub data and the application's internal `SourceSpecificMetadata` system.

## Design Principles

*   **Extension Methods**: Leverages C# extension methods to add functionality to existing types without modifying their original source code. This keeps the core models clean.
*   **Encapsulation**: Encapsulates specific pieces of logic (e.g., parsing rules for build info, formatting rules for sizes) in one place.
*   **Reusability**: Provides reusable functions that can be called from multiple places (Services, ViewModels, Factories).

