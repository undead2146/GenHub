# Game Profiles UI PR

## Summary

This PR implements the Game Profiles UI feature, enabling users to create, manage, and launch game profiles based on detected game installations. The implementation includes robust scanning, manifest generation, content storage, and profile launching with proper error handling and validation. Key fixes address detection accuracy by ensuring game executables exist before marking installations as valid.

## Changes Made

### Core Fixes Applied

1. **Publisher Logic Fix**: Corrected case-sensitive publisher detection in `ManifestGenerationService.cs` to properly identify "eaapp" publisher for EA App installations by changing "EA" to "ea" in the Contains check.
2. **Safe File Enumeration**: Added try-catch around `Directory.GetFiles` calls in `ManifestGenerationService.cs` to handle invalid paths gracefully without crashing.
3. **Early Installation Fetch**: Modified `GameInstallationService.cs` to call `Fetch()` on installations before client detection to ensure `Has*` flags are set correctly.
4. **Client Validation**: Enhanced `MainViewModel.cs` to skip profile creation for invalid game clients by checking `IsValid`.
5. **CAS Preflight**: Added CAS system availability check in `ProfileLauncherFacade.cs` validation to ensure storage system is operational before launch.
6. **Detector Executable Validation**: Updated all game installation detectors (Steam, EA App, Wine) to validate both directory existence and executable presence before setting `HasGenerals`/`HasZeroHour` flags, preventing false positives that were later overridden in domain conversion.

### Files Modified

- `GenHub/GenHub/Features/Manifest/ManifestGenerationService.cs`
- `GenHub/GenHub/Features/GameInstallations/GameInstallationService.cs`
- `GenHub/GenHub/Common/ViewModels/MainViewModel.cs`
- `GenHub/GenHub/Features/GameProfiles/Services/ProfileLauncherFacade.cs`
- `GenHub/GenHub.Windows/GameInstallations/SteamInstallation.cs`
- `GenHub/GenHub.Windows/GameInstallations/EaAppInstallation.cs`
- `GenHub/GenHub.Linux/GameInstallations/SteamInstallation.cs`
- `GenHub/GenHub.Linux/GameInstallations/WineInstallation.cs`

### Behavioral Details

- **Scanning**: Automatically detects game installations from Steam, EA App, and retail sources, validating both directory and executable existence.
- **Manifest Generation**: Creates content manifests with proper publisher identification and safe file handling.
- **Profile Creation**: Generates profiles only for valid installations with available game clients.
- **Launching**: Validates CAS availability and manifest integrity before launching profiles.
- **Error Handling**: Graceful degradation on invalid paths, missing files, or unavailable services.

## Testing

- All existing tests pass (798+ tests).
- Build succeeds without errors.
- End-to-end flow from scanning to launching works correctly.
- Invalid paths are handled without exceptions.
- CAS system is validated before profile launch.

## Architecture Notes

- Follows SOLID principles with service-oriented architecture.
- Uses Result pattern for error handling.
- Implements dependency injection for testability.
- Maintains modular design with clear separation of concerns.
