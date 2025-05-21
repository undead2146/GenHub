# UI Services

This directory contains services related to UI functionality that are platform-specific and not part of the core domain model.

## Service Overview

- **FileDialogService**: Handles file and folder selection dialogs in a platform-independent way

## Design Goals

These services abstract platform-specific UI functionality to:

1. Make UI-related code more testable
2. Provide a consistent API for platform-specific operations
3. Decouple ViewModels from platform details
4. Allow for easier platform-specific customization

## Usage

The `FileDialogService` is used by ViewModels that need to allow users to:
- Select files (images, executables, etc.)
- Select folders (game directories, data paths, etc.)

This enables ViewModels to focus on business logic while delegating UI interactions to this service.

## Example

```csharp
// In a ViewModel
public async Task BrowseForIcon()
{
    var iconPath = await _fileDialogService.PickImageFileAsync("Select Profile Icon");
    if (iconPath != null)
    {
        // Handle selected icon
        Profile.IconPath = await _profileSettingsDataProvider.AddCustomIconAsync(iconPath);
    }
}
```
