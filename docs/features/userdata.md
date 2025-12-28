---
title: User Data Management
description: Comprehensive system for managing user-generated content across game profiles
---

# User Data Management

The User Data Management system provides intelligent tracking and isolation of user-generated content (maps, replays, save games) across game profiles. It ensures data integrity, prevents accidental loss, and optimizes disk usage through hard-link technology.

## Overview

When players use Command & Conquer Generals/Zero Hour, they create various types of user data:
- **Custom Maps**: Downloaded or created maps stored in `My Documents\Command and Conquer Generals Zero Hour Data\Maps`
- **Replays**: Recorded game sessions stored in `My Documents\Command and Conquer Generals Zero Hour Data\Replays`
- **Save Games**: Campaign progress and saved games

GenHub's User Data Management system ensures that each game profile can have its own isolated set of this content while avoiding unnecessary file duplication.

## Architecture

### Core Services

#### IProfileContentLinker
The primary interface for user data operations.

**Location**: `GenHub.Core.Interfaces.UserData.IProfileContentLinker`

**Key Methods**:
- `PrepareProfileUserDataAsync`: Installs and activates user data when launching a profile
- `SwitchProfileUserDataAsync`: Handles profile transitions with optional data retention
- `UpdateProfileUserDataAsync`: Manages content changes within a profile
- `CleanupDeletedProfileAsync`: Removes all user data when a profile is deleted
- `AnalyzeUserDataSwitchAsync`: Pre-flight analysis of data impact before switching

#### ProfileContentLinkerService
The concrete implementation of user data management.

**Location**: `GenHub.Features.UserData.Services.ProfileContentLinkerService`

**Responsibilities**:
- Tracks which user data files belong to which profiles
- Creates hard links to avoid file duplication
- Activates/deactivates user data based on active profile
- Verifies file integrity using hash validation
- Manages the lifecycle of user data across profile operations

#### IUserDataTracker
Low-level tracking service for user data files.

**Responsibilities**:
- Maintains database of installed user data files
- Tracks hard link relationships
- Provides activation/deactivation primitives
- Handles file verification and cleanup

## How It Works

### 1. Profile Launch (Preparation)

When a profile is launched, the system:

1. **Identifies User Data Content**: Scans the profile's enabled content for manifests containing user data files (maps, replays, etc.)
2. **Checks Installation Status**: Queries the tracker to see if files are already installed
3. **Verifies Integrity**: For existing installations, validates files using stored hashes
4. **Installs New Content**: For missing or corrupted files, creates hard links from CAS to user data directories
5. **Activates Profile Data**: Marks the profile's user data as active in the tracker

```csharp
// Simplified flow
var result = await profileContentLinker.PrepareProfileUserDataAsync(
    profileId: "my-profile-123",
    manifests: enabledContentManifests,
    targetGame: GameType.ZeroHour,
    cancellationToken: cancellationToken
);
```

### 2. Profile Switching

Profile switching is the most complex operation, involving data transition and optional retention.

#### Without User Data Retention (Default)

1. **Deactivate Old Profile**: Marks old profile's user data as inactive
2. **Remove Old Files**: Deletes hard links for files exclusive to old profile
3. **Activate New Profile**: Installs and activates new profile's user data

#### With User Data Retention (`skipCleanup = true`)

1. **Analyze Impact**: Identifies files that would be removed
2. **User Confirmation**: Displays non-blocking prompt with file count and size
3. **Adopt Files**: If user chooses "Add to Profile", re-registers old profile's files with new profile
4. **Preserve Hard Links**: Files remain on disk, now tracked for both profiles

```csharp
// Switching with optional retention
var result = await profileContentLinker.SwitchProfileUserDataAsync(
    oldProfileId: "profile-a",
    newProfileId: "profile-b",
    newManifests: profileBManifests,
    targetGame: GameType.ZeroHour,
    skipCleanup: userChoseToKeepData,
    cancellationToken: cancellationToken
);
```

### 3. Content Updates

When a profile's content selection changes (adding/removing mods with maps):

1. **Compute Delta**: Compares old and new manifest sets
2. **Install New Content**: Adds hard links for newly enabled content
3. **Remove Deselected Content**: Cleans up files from removed content
4. **Update Tracker**: Synchronizes the database with new state

### 4. Profile Deletion

When a profile is deleted:

1. **Deactivate All Data**: Marks all profile's user data as inactive
2. **Check References**: Identifies files used exclusively by this profile
3. **Remove Exclusive Files**: Deletes hard links for files not used by other profiles
4. **Preserve Shared Files**: Keeps files that other profiles still reference

## Hard Link Technology

### Why Hard Links?

Hard links allow multiple directory entries to point to the same file data on disk. Benefits:
- **Zero Duplication**: Same file appears in multiple locations without copying
- **Atomic Operations**: File exists or doesn't exist, no partial states
- **Transparent Access**: Applications see normal files, unaware of linking
- **Efficient Cleanup**: Deleting one link doesn't affect others

### Limitations

- **Same Volume Only**: Hard links require source and target on same drive
- **File-Level Only**: Cannot hard link directories
- **Windows/Linux Only**: Platform-specific implementation

### Fallback Strategy

If hard links fail (cross-drive scenario), the system falls back to file copying with appropriate warnings.

## Data Models

### UserDataSwitchInfo

Analysis results for profile switch impact.

```csharp
public class UserDataSwitchInfo
{
    public string OldProfileId { get; set; }
    public string NewProfileId { get; set; }
    public int FileCount { get; set; }
    public long TotalSizeBytes { get; set; }
}
```

### UserDataManifest

Tracks installed user data for a profile.

```csharp
public class UserDataManifest
{
    public string ManifestId { get; set; }
    public string ProfileId { get; set; }
    public List<InstalledFile> InstalledFiles { get; set; }
    public bool IsActive { get; set; }
    public DateTime InstalledAt { get; set; }
}
```

### InstalledFile

Individual file tracking with hash verification.

```csharp
public class InstalledFile
{
    public string SourcePath { get; set; }
    public string TargetPath { get; set; }
    public string Hash { get; set; }
    public long SizeBytes { get; set; }
    public bool IsHardLink { get; set; }
}
```

## User Experience

### Switch Confirmation UI

When switching profiles would result in data loss, a non-blocking confirmation appears:

**Visual Elements**:
- Semi-transparent overlay on profile card
- File count and total size display
- Two action buttons: "Add to Profile" and "Remove"
- Large data warning (>100 files) with loading indicator

**User Choices**:
- **Add to Profile**: Adopts old profile's user data into new profile (preserves files)
- **Remove**: Proceeds with standard cleanup (removes files)
- **Cancel**: Aborts the profile switch

### Large Data Warning

When adopting >100 files, the system:
1. Logs a warning about potential delay
2. Displays visual indicator on confirmation overlay
3. Shows "Loading maps..." notification during the operation

## Integration Points

### GameProfileLauncherViewModel

Orchestrates the user data confirmation flow:

1. **Pre-Launch Analysis**: Calls `AnalyzeUserDataSwitchAsync` before switching
2. **UI Update**: Populates `GameProfileItemViewModel` with switch info
3. **Pause Launch**: Sets `ShowUserDataConfirmation = true` to display overlay
4. **Handle Response**: Executes appropriate command based on user choice
5. **Resume Launch**: Continues with `ExecuteLaunchAsync` after confirmation

### ProfileLauncherFacade

High-level launch orchestration that delegates to `IProfileContentLinker`:

```csharp
// Simplified launch flow
var launchResult = await gameLauncher.LaunchProfileAsync(
    profile,
    progress: null,
    skipUserDataCleanup: userChoseToKeepData,
    cancellationToken: cancellationToken
);
```

### GameLauncher

Core launcher that invokes user data operations:

```csharp
// During profile switch
await profileContentLinker.SwitchProfileUserDataAsync(
    oldProfileId: previousProfile,
    newProfileId: currentProfile,
    newManifests: profileManifests,
    targetGame: profile.GameClient.GameType,
    skipCleanup: skipUserDataCleanup,
    cancellationToken: cancellationToken
);
```

## Performance Considerations

### File Count Threshold

The system uses a threshold of 100 files to determine "large" transfers:
- Below threshold: Silent operation
- Above threshold: Warning notification and visual indicator

### Database Queries

The tracker uses indexed queries for:
- Profile ID lookups
- Manifest ID lookups
- Active status filtering
- Hash verification

### Concurrent Access

All operations use proper locking to prevent race conditions:
- Profile-level locks during switches
- File-level locks during installation
- Database transaction isolation

## Error Handling

### Common Scenarios

**Hard Link Failure (Cross-Drive)**:
- Logs detailed error with drive information
- Falls back to file copying
- Notifies user of performance impact

**File Verification Failure**:
- Logs hash mismatch
- Triggers automatic reinstallation
- Continues with other files

**Insufficient Disk Space**:
- Returns failure result with clear message
- Rolls back partial operations
- Preserves existing state

## Best Practices

### For Developers

1. **Always Use Cancellation Tokens**: All async operations support cancellation
2. **Check Result Success**: Never assume operations succeed
3. **Log Appropriately**: Use structured logging with profile/manifest IDs
4. **Handle Partial Failures**: Some files may succeed while others fail

### For Users

1. **Same Drive Recommended**: Keep game installation and GenHub data on same drive for hard link support
2. **Review Large Transfers**: Pay attention to warnings when adopting many files
3. **Regular Cleanup**: Delete unused profiles to free up disk space
4. **Backup Important Data**: While the system is robust, external backups are always recommended

## Future Enhancements

Potential improvements under consideration:

- **Cloud Sync**: Synchronize user data across machines
- **Compression**: Compress inactive user data to save space
- **Deduplication**: Identify and merge identical files across profiles
- **Import/Export**: Package user data for sharing with other users
- **Selective Retention**: Choose specific files to keep during switches
