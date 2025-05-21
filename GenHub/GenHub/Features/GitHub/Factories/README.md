# GitHub Feature Factories

This directory contains factory classes responsible for creating objects within the GitHub feature module, primarily focusing on the instantiation of ViewModel items from core domain models.

## Overview

Factories in this context serve to decouple the process of object creation from the code that uses these objects. This is particularly important for creating ViewModel instances that represent various GitHub entities (like artifacts, workflows, and releases) for display in the UI.

## Key Components

*   **`GitHubDisplayItemFactory.cs`**:
    *   **Purpose**: This is the central factory for creating different types of `IGitHubDisplayItem` ViewModels (e.g., `GitHubArtifactDisplayItemViewModel`, `GitHubWorkflowDisplayItemViewModel`, `GitHubReleaseDisplayItemViewModel`).
    *   **Functionality**: It takes GitHub domain models (from `GenHub.Core.Models`) as input and returns fully initialized ViewModel instances ready for data binding in the UI. This includes injecting necessary services (like `IGitHubServiceFacade`, `ILogger`) into the created ViewModels.
    *   **MVVM Role**: Encapsulates the logic for transforming Models into ViewModels, which is a core aspect of the MVVM pattern. It helps keep other ViewModels and services cleaner by abstracting away the construction details of these display items.

## Architectural Patterns

*   **Factory Pattern**: Directly implements the factory pattern to provide a centralized way to create complex objects (ViewModels with dependencies).
*   **Dependency Injection (DI)**: The factory itself is designed to be injectable, and it, in turn, resolves and injects dependencies into the ViewModels it creates. This promotes loose coupling and testability.
*   **Separation of Concerns**: Separates the concern of ViewModel creation from the ViewModels or services that will consume them.

