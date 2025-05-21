# UI Value Converters

This directory houses various `IValueConverter` and `IMultiValueConverter` implementations, primarily for use in XAML-based UI frameworks like Avalonia UI or WPF. These converters transform data from one type or format to another, facilitating data binding between view models and UI elements.

## Converters

*   **`BoolToColorConverter.cs`**: Converts a boolean value to a specific `IBrush` or `Color` (e.g., true to green, false to red).
*   **`BoolToValueConverter.cs`**: A generic converter that returns one of two specified values based on a boolean input (e.g., `TrueValue`, `FalseValue`).
*   **`BuildInfoValueConverter.cs`**: Converts a `GitHubBuild` object (or similar build information model) into a formatted string representation for display.
*   **`ContrastTextColorConverter.cs`**: Takes a background color (as `IBrush` or `Color`) and returns a contrasting text color (e.g., black or white) for optimal readability.
*   **`FileSizeConverter.cs`**: Converts a file size in bytes (long) into a human-readable string (e.g., "1.2 MB", "500 KB").
*   **`FirstNonNullConverter.cs`**: An `IMultiValueConverter` that takes multiple bound values and returns the first one that is not null. Useful for fallback mechanisms in bindings.
*   **`InstallStageConverter.cs`**: Converts an `InstallStage` enum value to a human-readable string, icon, or color representing the installation stage.
*   **`InstallStageMultiConverter.cs`**: An `IMultiValueConverter` that likely combines multiple inputs (e.g., `InstallStage`, `IsError`) to produce a specific UI representation (e.g., status text, progress bar color).
*   **`NotNullToBoolConverter.cs`**: Converts an object to a boolean (true if not null, false if null).
*   **`ProfileCoverConverter.cs`**: Converts a path to a cover image (or a `GameProfile` object) into a displayable `Bitmap` or `ImageSource`. Handles cases where the image might be missing or needs default representation.
*   **`StringToBrushConverter.cs`**: Converts a string representation of a color (e.g., hex code like "#FF0000", or color name like "Red") into an `IBrush`.
*   **`StringToImageConverter.cs`**: Converts a string (typically a URI or file path) into an `ImageSource` or `Bitmap`.
*   **`TreeViewItemLevelConverter.cs`**: Used with `TreeView` controls to apply different styling or indentation based on the level of an item in the tree.
*   **`WorkflowInstallationStatusConverter.cs`**: Converts properties of a `GitHubWorkflow` or `GitHubArtifact` (e.g., `Conclusion`, `Status`, `IsInstalled`) into a visual representation of its installation or download status (e.g., icon, color, text).

## Responsibilities and Interactions

*   These converters are primarily used in XAML bindings to adapt data from view models or models for display in the UI.
*   They help keep view models clean by offloading view-specific transformation logic to the view layer.
*   Each converter typically focuses on a specific type of transformation, promoting reusability.
*   They are instantiated as resources in XAML and referenced in binding expressions.
