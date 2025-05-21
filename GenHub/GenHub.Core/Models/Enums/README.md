# Enumeration Models

This directory contains various enumeration types used throughout the GenHub.Core project. Enums provide a way to define a set of named constants, improving code readability and maintainability by representing distinct states, types, or options.

## Enums

*   **`DialogResult.cs`**:
    *   **Purpose**: Represents the possible results of a dialog interaction (e.g., OK, Cancel, Yes, No).
    *   **Values (Typical)**: `None`, `OK`, `Cancel`, `Yes`, `No`, `Abort`, `Retry`, `Ignore`.
    *   **Usage**: Used by UI services or view models when displaying dialogs to capture user responses.

*   **`GameInstallationType.cs`**:
    *   **Purpose**: Defines the various sources or methods by which a game version can be installed or recognized.
    *   **Values**: `Unknown`, `Steam`, `EaApp`, `Origin`, `TheFirstDecade`, `RGMechanics`, `CDISO`, `GitHubArtifact`, `LocalZipFile`, `DirectoryImport`.
    *   **Usage**: Critical property of `GameVersion` and `GameProfile` models to indicate their origin and how they were acquired. Influences how versions are handled, updated, or launched.

*   **`GameVariant.cs`**:
    *   **Purpose**: Specifies different variants or editions of a game, particularly useful for games with multiple standalone versions or expansions treated as distinct entities.
    *   **Values (Example from `GitHubBuild.cs`)**: `Unknown`, `Generals`, `ZeroHour`. (This enum might be defined here or directly within `GitHubBuild.cs` or a more general game enum file).
    *   **Usage**: Used in `GitHubBuild` to categorize artifacts. Can also be used by `GameVersion` or `GameProfile` to specify the game variant.

*   **`InstallStage.cs`**:
    *   **Purpose**: Represents the different stages of an installation process for a game version or application update.
    *   **Values**: `None`, `Preparing`, `Downloading`, `Extracting`, `Verifying`, `Finalizing`, `Completed`, `Failed`, `Error`.
    *   **Usage**: Used by the `InstallProgress` model to track and report the current phase of an installation. Essential for providing feedback to the user.

## Responsibilities and Interactions

*   These enums provide strongly-typed, predefined sets of values for properties in other models and for controlling logic within services.
*   They help prevent errors that might arise from using magic strings or arbitrary integer values.
*   They are widely referenced across the application, from data modeling and service logic to UI display and state management.
