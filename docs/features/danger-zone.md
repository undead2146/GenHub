---
title: Danger Zone
description: Maintenance and data cleanup tools for GeneralsHub
---

# Danger Zone

The **Danger Zone** is a specialized section within the Settings menu providing powerful maintenance tools for advanced users and developers. These operations are destructive and should be used with caution.

## Overview

As GeneralsHub manages large amounts of game content, workspaces, and cached data, it may occasionally be necessary to perform deep cleanups. The Danger Zone centralizes these operations to ensure they are performed safely and with proper user confirmation.

## Available Operations

### 1. Delete All Workspaces
Removes all assembled game environments.
- **Effect**: Deletes the `Workspaces` directory from your configured workspace path.
- **When to use**: If you want to free up space or if profile launching fails due to workspace corruption.
- **Safety**: Does not delete your actual content (mods/games) or profiles; workspaces will be re-created on the next launch.

### 2. Clear CAS Storage
Performs a deep garbage collection of the Content-Addressable Storage.
- **Effect**: Scans all stored objects and deletes those no longer referenced by any manifest.
- **When to use**: To reclaim disk space from old versions of mods or unused content.
- **Logic**:
  - Objects used by current profiles are preserved.
  - Objects used by existing manifests are preserved.
  - Only truly orphaned files are deleted.

### 3. Delete All Manifests
Wipes the local metadata pool of content definitions.
- **Effect**: Deletes all `.manifest.json` files from the internal metadata storage.
- **When to use**: If the content browser is showing corrupted data or if you want to perform a fresh discovery of all repositories.
- **Safety**: Requires re-running "Discover Repositories" or "Scan Local Files" to restore content visibility.

## Data Indicators

The Danger Zone provides real-time indicators for:
- **Workspace Count**: Number of currently active isolated environments.
- **CAS Usage**: Total disk space consumed by the content pool.
- **Manifest Count**: Number of indexed content items (mods, patches, maps).

## Best Practices

> [!WARNING]
> Always ensure GenHub operations (downloads, launches) are idle before performing Danger Zone cleanups.

1. **Before Uninstalling**: Run "Delete All Workspaces" to ensure all symbolic links and copies are cleaned up.
2. **After Mod Updates**: Run "Clear CAS Storage" to remove the old versions of mod files that are no longer needed.
3. **If in Doubt**: "Delete All Workspaces" is the safest operation as it has zero data loss for persistent settings or mods.

## Technical Details

These operations are implemented in the `SettingsViewModel` and leverage core services:
- `IWorkspaceManager.CleanupAllAsync()`
- `ICasService.RunGarbageCollectionAsync()`
- `IContentManifestPool` for metadata management

---
*See also:*
- [Storage & CAS](./storage.md)
- [Workspace Management](./workspace.md)
- [Validation System](./validation.md)
