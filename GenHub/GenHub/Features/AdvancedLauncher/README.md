# Advanced Launcher Module

## Overview

The Advanced Launcher module provides sophisticated game launching capabilities including direct execution, validation, and command-line interface support. This module enables users to launch games directly via CLI arguments, protocol URLs, and advanced validation mechanisms.

## Features

### Core Capabilities
- **Direct Launch Service**: Launch games directly with minimal UI interaction
- **Launch Validation**: Pre-flight checks to ensure successful game launches
- **CLI Argument Parsing**: Robust command-line argument processing
- **Protocol Support**: Handle `genhub://` protocol URLs for seamless integration
- **Diagnostic Launches**: Comprehensive diagnostics for troubleshooting

### Components

#### Services
- `DirectLaunchService`: Core service for direct game launching
- `LauncherArgumentParser`: Parses and validates CLI arguments
- `LauncherProtocolService`: Handles protocol URL processing
- `QuickLaunchOrchestrator`: Coordinates quick launch operations

#### Models
- `LaunchParameters`: Configuration for launch operations
- `QuickLaunchResult`: Results from quick launch operations
- `LaunchValidationResult`: Validation results and diagnostics
- `LaunchContext`: Contextual information for launches

### Usage Examples

#### Direct Launch via CLI
```bash
GenHub.exe --profile "my-game" --mode quick
GenHub.exe --profile-name "Command & Conquer" --skip-validation
```

#### Protocol URLs
```
genhub://launch?profile=abc123&mode=quick
genhub://validate?profile=abc123
```

#### Programmatic Launch
```csharp
var parameters = new LaunchParameters
{
    ProfileId = "abc123",
    Mode = LaunchMode.Quick,
    SkipValidation = false
};

var result = await directLaunchService.LaunchDirectlyAsync(parameters);
```

## Architecture

### Dependency Injection
The module is registered via `AdvancedLauncherModule` which configures:
- Scoped services for launch operations
- Singleton argument parser
- Protocol handlers

### Platform Support
- **Windows**: Full support including admin privilege elevation
- **Linux**: Native Linux game launching with proper environment handling

## Configuration

### Launch Modes
- `Normal`: Standard launch with UI
- `Quick`: Bypass UI and launch directly
- `Validate`: Validation only (no launch)
- `Background`: Silent background launch
- `Diagnostic`: Full diagnostic information

### Validation Levels
- `None`: Skip all validation
- `Basic`: Essential file existence checks
- `Full`: Comprehensive validation including integrity checks

## Error Handling

The module provides comprehensive error handling with:
- Detailed error messages for common failure scenarios
- Diagnostic information for troubleshooting
- Graceful fallbacks for missing dependencies
- Performance metrics for optimization

## Testing

Comprehensive unit tests cover:
- CLI argument parsing edge cases
- Launch validation scenarios
- Error handling paths
- Performance benchmarks

## Dependencies

- `GenHub.Core.Interfaces.GameProfiles`
- `GenHub.Core.Interfaces.GameVersions`
- `Microsoft.Extensions.Logging`
- `Microsoft.Extensions.DependencyInjection`

## Related Modules

- [Desktop Shortcuts](../DesktopShortcuts/README.md): Creating shortcuts that use advanced launcher
- [Game Profiles](../GameProfiles/README.md): Source of game configuration
- [Game Versions](../GameVersions/README.md): Game installation management
