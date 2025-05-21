# Installation Models

This directory contains data models related to the process of installing game versions, particularly from archives or downloaded packages.

## Models

*   **`ExtractOptions.cs`**:
    *   **Purpose**: Defines a set of configurable options that can be used when extracting a game version from an archive (e.g., a ZIP file).
    *   **Key Properties**:
        *   `CustomInstallName`: Allows specifying a custom name for the directory where the game version will be installed.
        *   `DeleteZipAfterExtraction`: A boolean indicating whether the source archive file should be deleted after a successful extraction.
        *   `PreferZeroHour`: A boolean flag that might influence how game executables are located or prioritized if the archive contains multiple game variants.
        *   `ArchivePath`: The path to the archive file that needs to be extracted.
        *   `AdditionalParams`: A dictionary for any other specific parameters that might be needed by the extraction logic.
    *   **Usage**:
        *   Instances of `ExtractOptions` are typically created and configured by services responsible for game installation (e.g., `IGameVersionInstaller` or a service handling GitHub artifact installations).
        *   These options are then passed to the underlying extraction mechanism (e.g., a library that handles ZIP files) to control its behavior.
        *   The `GameVersion` model has an optional `Options` property (often `[JsonIgnore]`) of this type, which can temporarily hold the options used during its installation process, primarily for logging or re-attempting an installation.

## Responsibilities and Interactions

*   The models in this directory are primarily concerned with the parameters and settings that govern the installation and extraction phase of acquiring a game version.
*   `ExtractOptions` is a key data structure used by installation services to customize how game archives are processed and placed onto the user's system.
*   It helps to decouple the specific settings for an extraction operation from the core installation logic, making the process more flexible.
