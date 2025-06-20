# Desktop Shortcuts Module

## Overview

The Desktop Shortcuts module provides comprehensive desktop shortcut management for GenHub game profiles. It supports creating, validating, and managing smart shortcuts across Windows (.lnk) and Linux (.desktop) platforms with advanced features like self-healing and batch operations.

## Features

### Core Capabilities

- **Cross-Platform Support**: Windows (.lnk) and Linux (.desktop) formats
- **Smart Shortcuts**: Self-healing shortcuts that adapt to moved files
- **Icon Extraction**: Automatic icon extraction from game executables
- **Validation System**: Comprehensive shortcut validation and repair
- **Batch Operations**: Create/manage multiple shortcuts efficiently
- **Protocol Integration**: Support for `genhub://` protocol URLs

### Components

#### Services

- `DesktopShortcutServiceFacade`: Main orchestrator for all shortcut operations
- `WindowsShortcutService`: Windows-specific shortcut implementation (.lnk)
- `LinuxShortcutService`: Linux-specific shortcut implementation (.desktop)
- `ShortcutCommandBuilder`: Builds optimized command lines for shortcuts
- `ShortcutIconExtractor`: Extracts and processes icons from executables

#### Models

- `ShortcutConfiguration`: Complete shortcut metadata and settings
- `ShortcutValidationSummary`: Aggregate validation results
- `ShortcutValidationResult`: Individual shortcut validation results

### Usage Examples

#### Create Single Shortcut

```csharp
var config = new ShortcutConfiguration
{
    Name = "Command & Conquer",
    ProfileId = "abc123",
    Type = ShortcutType.Profile,
    LaunchMode = ShortcutLaunchMode.Direct
};

var result = await shortcutService.CreateShortcutAsync(config);
```

#### Batch Shortcut Creation

```csharp
var configs = new[]
{
    new ShortcutConfiguration { Name = "Game 1", ProfileId = "id1" },
    new ShortcutConfiguration { Name = "Game 2", ProfileId = "id2" }
};

var result = await shortcutService.CreateBatchShortcutsAsync(configs);
```

#### Validate All Shortcuts

```csharp
var summary = await shortcutService.ValidateAllShortcutsAsync();
var brokenShortcuts = summary.GetInvalidShortcuts();
```

## Architecture

### Platform Abstraction

The module uses a platform abstraction pattern:

```
IDesktopShortcutServiceFacade (Common Interface)
├── IShortcutPlatformService (Platform Interface)
│   ├── WindowsShortcutService (.lnk files)
│   └── LinuxShortcutService (.desktop files)
├── IShortcutCommandBuilder (Command building)
└── IShortcutIconExtractor (Icon processing)
```

### Dependency Injection

Registered via `DesktopShortcutModule` with platform-specific implementations:

- Windows: `WindowsShortcutService` for .lnk file management
- Linux: `LinuxShortcutService` for .desktop file management
- Cross-platform: `ShortcutCommandBuilder` and `ShortcutIconExtractor`

## Configuration

### Shortcut Types

- `Profile`: Launch a specific game profile
- `Game`: Launch the game directly (bypassing profile)
- `QuickLauncher`: Open GenHub quick launcher
- `Manager`: Open GenHub main interface
- `Diagnostics`: Run diagnostics for a profile

### Launch Modes

- `Normal`: Standard launch through GenHub UI
- `Direct`: Direct game launch bypassing UI
- `Validate`: Validate profile before launching
- `Ask`: Prompt user for launch options

### Locations

- `Desktop`: User desktop
- `StartMenu`: Start menu (Windows) / Applications menu (Linux)
- `Both`: Both desktop and start menu
- `Custom`: User-specified location

## Platform-Specific Features

### Windows (.lnk files)

- COM-based shortcut creation using Windows Shell objects
- Support for custom icons, descriptions, and working directories
- Admin privilege elevation support
- Protocol registration in Windows registry

### Linux (.desktop files)

- XDG Desktop Entry specification compliance
- Automatic installation to `~/.local/share/applications/`
- MIME type associations for protocol handling
- Icon theme integration

## Validation & Self-Healing

### Validation Checks

- Shortcut file existence
- Target executable existence
- Icon file availability
- Profile validity
- Command line argument correctness

### Self-Healing Features

- Automatic path resolution for moved executables
- Icon re-extraction when original icons are missing
- Profile ID resolution when profiles are renamed
- Command line argument updates

## Error Handling

Comprehensive error handling with:

- Detailed validation reports
- User-friendly error messages
- Automatic fallback mechanisms
- Logging for troubleshooting

## Testing

Test coverage includes:

- Platform-specific shortcut creation
- Validation algorithm accuracy
- Self-healing mechanism reliability
- Cross-platform compatibility
- Performance benchmarks

## Dependencies

- `GenHub.Core.Interfaces.GameProfiles`
- `GenHub.Core.Models.AdvancedLauncher`
- `Microsoft.Extensions.Logging`
- Platform-specific dependencies for shell integration

## Related Modules

- [Advanced Launcher](../AdvancedLauncher/README.md): Provides launching capabilities
- [Game Profiles](../GameProfiles/README.md): Source of profile information
- [Game Versions](../GameVersions/README.md): Game installation details

## Future Enhancements

- Shortcut templates and themes
- Advanced icon customization
- Shortcut analytics and usage tracking
- Cloud synchronization of shortcut configurations
