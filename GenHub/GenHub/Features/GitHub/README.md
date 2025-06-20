# GitHub Feature

The `GitHub` directory contains all components related to integrating GitHub functionalities into the GenHub application. This includes discovering repositories, browsing repositories, fetching workflow runs, managing artifacts (viewing, downloading, installing), and interacting with GitHub releases.

## Overview

This feature module is responsible for:

* **Repository Discovery:** Intelligently discovering Command & Conquer: Generals Zero Hour repositories with downloadable content through fork network traversal.
* **Displaying GitHub Data:** Presenting information such as repositories, workflow runs, artifacts, and releases in the UI.
* **User Interaction:** Allowing users to browse, search, and select items from GitHub.
* **Artifact Management:** Handling the download and installation of game versions or other assets hosted as GitHub artifacts.
* **Release Management:** Fetching and displaying GitHub releases and their assets.
* **API Communication:** Interacting with the GitHub API via specialized services.

## Directory Structure

The `GitHub` feature is organized into the following subdirectories:

* [**Factories**](./Factories/README.md): Contains factory classes responsible for creating objects, particularly ViewModels from Models, related to GitHub entities.
* [**Helpers**](./Helpers/README.md): Provides utility classes and extension methods specific to GitHub-related operations or data transformations.
* [**Services**](./Services/README.md): Houses services that encapsulate the logic for interacting with the GitHub API and processing GitHub data. This includes API clients, artifact handlers, and services for repositories, workflows, and releases.
* [**ViewModels**](./ViewModels/README.md): Contains ViewModel classes that provide data and command logic for the GitHub-related views, adhering to the MVVM pattern.
* [**Views**](./Views/README.md): Includes the Avalonia UI (AXAML) views and corresponding code-behind files that define the user interface for GitHub features.

## Key Components

*   **`GitHubRepositoryDiscoveryService`**: Intelligently discovers C&C repositories through fork network traversal, ensuring all active repositories with downloadable content are found.
*   **`GitHubServiceFacade`**: Acts as a central point of access for various GitHub operations, simplifying interactions with more specialized services like API clients, workflow readers, and artifact managers.
*   **ViewModels (e.g., `GitHubManagerViewModel`, `GitHubArtifactDisplayItemViewModel`)**: Manage the state and behavior of the GitHub UI components.
*   **Models (from `GenHub.Core`)**: Core data structures like `GitHubRepository`, `GitHubWorkflow`, `GitHubArtifact`, `GitHubRelease` are used to represent data retrieved from GitHub.

## Repository Discovery

The repository discovery service is a key component that:

- **Discovers All Forks**: Uses intelligent network traversal to find all forks of base repositories
- **Validates Content**: Ensures repositories have releases with assets or successful workflow runs
- **Respects API Limits**: Implements rate limiting and efficient API usage
- **Provides Comprehensive Logging**: Detailed logging for debugging and monitoring
- **Follows Clean Architecture**: Implements SOLID principles and clean code practices

### Base Repositories

Discovery starts from these known repositories:
- **TheAssemblyArmada/Vanilla-Conquer**: Official C&C remaster source
- **TheSuperHackers/GeneralsGamePatch**: Community game patch
- **xezon/CnC_GeneralsGameCode**: Game code repository

### Discovery Process

1. **Base Repository Discovery**: Always includes official and community base repositories
2. **Network Expansion**: Traverses fork networks to discover all related repositories
3. **Content Validation**: Validates repositories for downloadable content (releases/workflows)
4. **Smart Filtering**: Applies secondary filters only after content validation

## Architectural Patterns

*   **MVVM (Model-View-ViewModel)**: Strictly followed for UI development, with clear separation of concerns between Views, ViewModels, and Models. ViewModels utilize `CommunityToolkit.Mvvm` for property and command generation.
*   **Facade Pattern**: Implemented via `GitHubServiceFacade` to provide a simplified interface to the GitHub interaction subsystem.
*   **Dependency Injection**: Services and ViewModels are typically resolved through DI, promoting loose coupling and testability.
*   **Repository Pattern**: While primary data comes from the GitHub API, caching mechanisms (potentially using a repository-like pattern for cached data) are employed to optimize performance and reduce API calls.

This feature aims to provide a robust and user-friendly interface for accessing and utilizing game-related resources hosted on GitHub.
