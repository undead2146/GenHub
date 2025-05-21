# Operation Result Models

This directory contains data models designed to represent the outcome of various operations performed within the application. These models typically encapsulate whether an operation was successful, any resulting data, and error messages if the operation failed. This pattern promotes a clear and consistent way to handle and communicate the results of service method calls.

## Models

*   **`GameLaunchPrepResult.cs`**:
    *   **Purpose**: Represents the result of preparing a game for launch. This includes determining the correct executable path and working directory.
    *   **Key Properties**:
        *   `Success`: A boolean indicating if the preparation was successful.
        *   `ExecutablePath`: The resolved path to the game executable to be launched.
        *   `WorkingDirectory`: The appropriate working directory for the game launch.
        *   `ErrorMessage`: A message detailing why preparation failed, if applicable.
    *   **Static Factory Methods**: `Succeeded(string exePath, string workingDir)`, `Failed(string errorMessage)`.
    *   **Usage**: Returned by services like `IGameLauncherService` (or a helper method within it) after it has processed a `GameProfile` and `GameVersion` to determine the final launch parameters.

*   **`InstallProgress.cs`**:
    *   **Purpose**: Tracks and reports the progress of an ongoing installation (e.g., a game version from a GitHub artifact or a local ZIP).
    *   **Key Properties**:
        *   `Percentage`: Overall completion percentage (0-100).
        *   `CurrentOperation`: A string describing the current task (e.g., "Downloading", "Extracting files...").
        *   `Stage`: An `InstallStage` enum value indicating the current phase of installation.
        *   `ErrorMessage`: Details of any error encountered.
        *   `DetailMessage`: More specific information about the current operation.
        *   `BytesProcessed`, `TotalBytes`: For tracking download or file operation progress.
        *   `FilesProcessed`, `TotalFiles`: For tracking file count during extraction or verification.
        *   `IsCompleted`, `HasError`: Boolean flags indicating the final state.
        *   `ElapsedTime`: Duration of the installation.
        *   `Message`: A general status message for display.
        *   `Current`, `Total`: Generic progress values for the current operation.
    *   **Static Factory Methods**: `DownloadProgress(...)`, `ExtractionProgress(...)`, `VerificationProgress(...)`, `Error(...)`, `Completed(...)`, and others for different stages.
    *   **Usage**: Used by installation services (e.g., `IGameVersionInstaller`) to provide real-time feedback to the UI. Progress updates are typically reported via `IProgress<InstallProgress>` or events.

*   **`OperationResult.cs`**:
    *   **Purpose**: A generic model to represent the outcome of a simple operation that may succeed or fail, optionally carrying a message. It can be a base for more specific result types or used directly.
    *   **Key Properties (Typical)**:
        *   `Success`: Boolean indicating success or failure.
        *   `Message`: An optional message providing details about the result (e.g., error message, success confirmation).
        *   `ErrorCode`: An optional code for specific error types.
    *   **Generic Variant (Typical)**: `OperationResult<T>` to carry a data payload `T` upon success.
    *   **Usage**: Returned by various service methods to indicate the outcome of actions like saving data, deleting items, or performing a specific task.

*   **`VersionValidationResult.cs`**:
    *   **Purpose**: Represents the outcome of validating an installed `GameVersion`. This could involve checking file integrity, presence of essential files, etc.
    *   **Key Properties**:
        *   `IsValid`: A boolean indicating if the game version is considered valid.
        *   `ErrorMessage`: Details why validation failed, if applicable.
        *   `FileCount`: Number of files found in the installation.
        *   `SizeInBytes`: Total size of the installation.
        *   `FormattedSize`: Human-readable string of the installation size.
        *   `ValidationTime`: When the validation was performed.
    *   **Usage**: Returned by services like `IGameVersionManager` or a dedicated validation service when checking the health or completeness of a game installation.

## Responsibilities and Interactions

*   These result models provide a standardized way for services to communicate outcomes, making it easier for calling code (e.g., view models, other services) to handle successes and failures gracefully.
*   They often include static factory methods (e.g., `OperationResult.Success()`, `OperationResult.Failure("Error message")`) for easy instantiation.
*   `InstallProgress` is particularly important for long-running operations, enabling responsive UIs that keep the user informed.
*   Using specific result types like `GameLaunchPrepResult` and `VersionValidationResult` makes the intent and the data carried by the result explicit.
