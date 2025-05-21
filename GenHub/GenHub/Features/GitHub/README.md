# GitHub Feature

The `GitHub` directory contains all components related to integrating GitHub functionalities into the GenHub application. This includes browsing repositories, fetching workflow runs, managing artifacts (viewing, downloading, installing), and interacting with GitHub releases.

## Overview

This feature module is responsible for:

*   **Displaying GitHub Data:** Presenting information such as workflow runs, artifacts, and releases in the UI.
*   **User Interaction:** Allowing users to browse, search, and select items from GitHub.
*   **Artifact Management:** Handling the download and installation of game versions or other assets hosted as GitHub artifacts.
*   **Release Management:** Fetching and displaying GitHub releases and their assets.
*   **API Communication:** Interacting with the GitHub API via specialized services.

## Directory Structure

The `GitHub` feature is organized into the following subdirectories:

*   [**Factories**](./Factories/README.md): Contains factory classes responsible for creating objects, particularly ViewModels from Models, related to GitHub entities.
*   [**Helpers**](./Helpers/README.md): Provides utility classes and extension methods specific to GitHub-related operations or data transformations.
*   [**Services**](./Services/README.md): Houses services that encapsulate the logic for interacting with the GitHub API and processing GitHub data. This includes API clients, artifact handlers, and services for repositories, workflows, and releases.
*   [**ViewModels**](./ViewModels/README.md): Contains ViewModel classes that provide data and command logic for the GitHub-related views, adhering to the MVVM pattern.
*   [**Views**](./Views/README.md): Includes the Avalonia UI (AXAML) views and corresponding code-behind files that define the user interface for GitHub features.

## Key Components

*   **`GitHubServiceFacade`**: Acts as a central point of access for various GitHub operations, simplifying interactions with more specialized services like API clients, workflow readers, and artifact managers.
*   **ViewModels (e.g., `GitHubManagerViewModel`, `GitHubArtifactDisplayItemViewModel`)**: Manage the state and behavior of the GitHub UI components.
*   **Models (from `GenHub.Core`)**: Core data structures like `GitHubWorkflow`, `GitHubArtifact`, `GitHubRelease` are used to represent data retrieved from GitHub.

## Architectural Patterns

*   **MVVM (Model-View-ViewModel)**: Strictly followed for UI development, with clear separation of concerns between Views, ViewModels, and Models. ViewModels utilize `CommunityToolkit.Mvvm` for property and command generation.
*   **Facade Pattern**: Implemented via `GitHubServiceFacade` to provide a simplified interface to the GitHub interaction subsystem.
*   **Dependency Injection**: Services and ViewModels are typically resolved through DI, promoting loose coupling and testability.
*   **Repository Pattern**: While primary data comes from the GitHub API, caching mechanisms (potentially using a repository-like pattern for cached data) are employed to optimize performance and reduce API calls.

This feature aims to provide a robust and user-friendly interface for accessing and utilizing game-related resources hosted on GitHub.
