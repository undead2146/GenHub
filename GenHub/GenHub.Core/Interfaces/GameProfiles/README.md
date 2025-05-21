# Game Profile Interfaces

This directory defines contracts related to the management and creation of game profiles. Game profiles allow users to have multiple configurations (e.g., different mods, command-line arguments, visual settings) for a single installed game version.

## Interfaces

*   **`IGameProfile.cs`**:
    *   **Purpose**: Defines the essential properties that constitute a game profile. This interface ensures that different implementations or data representations of a game profile adhere to a common structure.
    *   **Key Properties**: `Id`, `Name`, `Description`, `ExecutablePath`, `DataPath` (or working directory), `IconPath`, `CoverImagePath`, `ColorValue`, `CommandLineArguments`, `VersionId` (linking to an `IGameVersion`), `IsDefaultProfile`, `RunAsAdmin`, `SourceType`, `SourceSpecificMetadata`.
    *   **Usage**: Used as the type for game profile objects throughout the application, particularly by `IGameProfileManagerService` and UI components. The `GameProfile` model class implements this interface.

*   **`IGameProfileFactory.cs`**:
    *   **Purpose**: Defines the contract for a factory responsible for creating new `GameProfile` instances. This abstracts the creation logic, allowing for different ways to initialize profiles (e.g., from a `GameVersion`, by copying an existing profile, with default settings).
    *   **Key Methods**:
        *   `CreateNewProfile(IGameVersion gameVersion, string profileName)`: Creates a new game profile for a given game version, possibly pre-filling some details from the version.
        *   `CreateDefaultProfile(IGameVersion gameVersion)`: Creates a default profile for a game version.
        *   `DuplicateProfile(IGameProfile existingProfile, string newProfileName)`: Creates a new profile by copying an existing one.
    *   **Usage**: Used by UI actions or services when a new game profile needs to be generated.

*   **`IGameProfileManagerService.cs`**:
    *   **Purpose**: Defines the contract for a service that manages the lifecycle (CRUD operations - Create, Read, Update, Delete) of game profiles.
    *   **Key Methods**:
        *   `GetProfilesAsync(string? versionId = null)`: Retrieves all game profiles, optionally filtered by a `GameVersion` ID.
        *   `GetProfileByIdAsync(string profileId)`: Retrieves a specific game profile by its ID.
        *   `SaveProfileAsync(IGameProfile profile)`: Saves a new or updated game profile (handles both creation and update).
        *   `DeleteProfileAsync(string profileId)`: Deletes a game profile.
        *   `SetDefaultProfileAsync(string profileId, string versionId)`: Sets a specific profile as the default for its associated game version.
    *   **Usage**: This is the primary service for interacting with game profiles. UI components and other services use it to load, modify, and manage user-defined game configurations.

## Responsibilities and Interactions

*   `IGameProfile` provides the data contract for what a profile is.
*   `IGameProfileFactory` handles the instantiation of `IGameProfile` objects.
*   `IGameProfileManagerService` is responsible for the persistence and overall management of these profiles, often interacting with a repository (e.g., `IGameProfileRepository`).
*   These interfaces work together to provide a comprehensive system for managing user-specific game launch configurations.
