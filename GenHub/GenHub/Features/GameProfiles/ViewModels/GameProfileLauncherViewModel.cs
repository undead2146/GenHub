using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GenHub.Common.ViewModels;
using GenHub.Core.Constants;
using GenHub.Core.Interfaces.Common;
using GenHub.Core.Interfaces.GameInstallations;
using GenHub.Core.Interfaces.GameProfiles;
using GenHub.Core.Interfaces.Manifest;
using GenHub.Core.Models.Enums;
using GenHub.Core.Models.GameInstallations;
using GenHub.Core.Models.Manifest;
using GenHub.Features.GameProfiles.Views;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace GenHub.Features.GameProfiles.ViewModels;

/// <summary>
/// ViewModel for launching game profiles.
/// </summary>
public partial class GameProfileLauncherViewModel(
    IGameInstallationService? installationService,
    IGameProfileManager? gameProfileManager,
    IProfileLauncherFacade? profileLauncherFacade,
    GameProfileSettingsViewModel? settingsViewModel,
    IProfileEditorFacade? profileEditorFacade,
    IManifestGenerationService? manifestGenerationService,
    IContentManifestPool? contentManifestPool,
    IConfigurationProviderService? configService,
    IGameProcessManager? gameProcessManager,
    ILogger<GameProfileLauncherViewModel>? logger) : ViewModelBase
{
    private readonly IGameInstallationService? _installationService = installationService;
    private readonly IGameProfileManager? _gameProfileManager = gameProfileManager;
    private readonly IProfileLauncherFacade? _profileLauncherFacade = profileLauncherFacade;
    private readonly GameProfileSettingsViewModel? _settingsViewModel = settingsViewModel;
    private readonly IProfileEditorFacade? _profileEditorFacade = profileEditorFacade;
    private readonly IManifestGenerationService? _manifestGenerationService = manifestGenerationService;
    private readonly IContentManifestPool? _contentManifestPool = contentManifestPool;
    private readonly IConfigurationProviderService? _configService = configService;
    private readonly IGameProcessManager? _gameProcessManager = gameProcessManager;
    private readonly ILogger<GameProfileLauncherViewModel> _logger = logger ?? NullLogger<GameProfileLauncherViewModel>.Instance;

    [ObservableProperty]
    private ObservableCollection<GameProfileItemViewModel> _profiles = new();

    [ObservableProperty]
    private bool _isLaunching;

    [ObservableProperty]
    private bool _isPreparingWorkspace;

    [ObservableProperty]
    private bool _isEditMode;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    [ObservableProperty]
    private bool _isServiceAvailable = true;

    [ObservableProperty]
    private bool _isScanning;

    /// <summary>
    /// Initializes a new instance of the <see cref="GameProfileLauncherViewModel"/> class for design-time or test usage.
    /// This constructor is only for design-time and testing scenarios.
    /// </summary>
    public GameProfileLauncherViewModel()
        : this(null, null, null, null, null, null, null, null, null, null)
    {
        // Initialize with sample data for design-time
        StatusMessage = "Design-time preview";
        IsServiceAvailable = false;
    }

    /// <summary>
    /// Gets the command to create a shortcut for the selected profile.
    /// </summary>
    public IRelayCommand CreateShortcutCommand { get; } = new RelayCommand(() => { });

    /// <summary>
    /// Performs asynchronous initialization for the Downloads tab.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    public virtual async Task InitializeAsync()
    {
        try
        {
            // Subscribe to process exit events
            if (_gameProcessManager != null)
            {
                _gameProcessManager.ProcessExited += OnProcessExited;
            }

            StatusMessage = "Loading profiles...";
            ErrorMessage = string.Empty;
            Profiles.Clear();

            if (_gameProfileManager == null)
            {
                StatusMessage = "Profile manager not available";
                ErrorMessage = "Game Profile Manager service is not initialized";
                IsServiceAvailable = false;
                _logger.LogWarning("GameProfileManager not available for profile loading");
                return;
            }

            IsServiceAvailable = true;
            var profilesResult = await _gameProfileManager.GetAllProfilesAsync();
            if (profilesResult.Success && profilesResult.Data != null)
            {
                foreach (var profile in profilesResult.Data)
                {
                    // Use profile's IconPath if available, otherwise fall back to generalshub icon
                    var iconPath = !string.IsNullOrEmpty(profile.IconPath)
                        ? $"avares://GenHub/{profile.IconPath}"
                        : "avares://GenHub/Assets/Icons/generalshub-icon.png";

                    var item = new GameProfileItemViewModel(
                        profile.Id,
                        profile,
                        iconPath,
                        iconPath); // Using same icon for both icon and cover for now
                    Profiles.Add(item);
                }

                StatusMessage = $"Loaded {Profiles.Count} profiles";
                _logger.LogInformation("Loaded {Count} game profiles", Profiles.Count);
            }
            else
            {
                var errors = string.Join(", ", profilesResult.Errors);
                StatusMessage = $"Failed to load profiles: {errors}";
                ErrorMessage = errors;
                _logger.LogWarning("Failed to load profiles: {Errors}", errors);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error initializing profiles");
            StatusMessage = "Error loading profiles";
            ErrorMessage = ex.Message;
            IsServiceAvailable = false;
        }
    }

    /// <summary>
    /// Gets the default theme color for a game type.
    /// </summary>
    /// <param name="gameType">The game type.</param>
    /// <returns>The hex color code.</returns>
    private static string GetThemeColorForGameType(GameType gameType)
    {
        return gameType == GameType.Generals ? "#BD5A0F" : "#1B6575"; // Orange for Generals, Blue for Zero Hour
    }

    /// <summary>
    /// Gets the icon path for a game type and installation type.
    /// </summary>
    /// <param name="gameType">The game type.</param>
    /// <param name="installationType">The installation type.</param>
    /// <returns>The relative icon path.</returns>
    private static string GetIconPathForGame(GameType gameType, GameInstallationType installationType)
    {
        var gameIcon = gameType == GameType.Generals ? "generals-icon.png" : "zerohour-icon.png";
        var platformIcon = installationType switch
        {
            GameInstallationType.Steam => "steam-icon.png",
            GameInstallationType.EaApp => "eaapp-icon.png",
            _ => "genhub-icon.png"
        };

        // For now, return the game-specific icon - could be enhanced to combine with platform icon
        return $"Assets/Icons/{gameIcon}";
    }

    /// <summary>
    /// Gets the main window for opening dialogs.
    /// </summary>
    private static Window? GetMainWindow()
    {
        return Avalonia.Application.Current?.ApplicationLifetime
            is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop
            ? desktop.MainWindow
            : null;
    }

    /// <summary>
    /// Refreshes a single profile without reloading all profiles (preserves running state).
    /// </summary>
    /// <param name="profileId">The ID of the profile to refresh.</param>
    private async Task RefreshSingleProfileAsync(string profileId)
    {
        try
        {
            if (_gameProfileManager == null)
            {
                _logger.LogWarning("GameProfileManager not available for profile refresh");
                return;
            }

            var profileResult = await _gameProfileManager.GetProfileAsync(profileId);
            if (profileResult.Success && profileResult.Data != null)
            {
                var profile = profileResult.Data;
                var existingItem = Profiles.FirstOrDefault(p => p.ProfileId == profileId);

                if (existingItem != null)
                {
                    // Preserve the running state before updating
                    var wasRunning = existingItem.IsProcessRunning;
                    var processId = existingItem.ProcessId;
                    var workspaceId = existingItem.ActiveWorkspaceId;

                    // Update the profile data
                    var iconPath = !string.IsNullOrEmpty(profile.IconPath)
                        ? $"avares://GenHub/{profile.IconPath}"
                        : "avares://GenHub/Assets/Icons/generalshub-icon.png";

                    var newItem = new GameProfileItemViewModel(
                        profile.Id,
                        profile,
                        iconPath,
                        iconPath);

                    // Restore the running state
                    if (wasRunning)
                    {
                        newItem.IsProcessRunning = true;
                        newItem.ProcessId = processId;
                    }

                    // Restore workspace state
                    if (!string.IsNullOrEmpty(workspaceId))
                    {
                        newItem.UpdateWorkspaceStatus(workspaceId, profile.WorkspaceStrategy);
                    }

                    var index = Profiles.IndexOf(existingItem);
                    Profiles[index] = newItem;

                    _logger.LogInformation("Refreshed profile {ProfileId} (Running: {IsRunning})", profileId, wasRunning);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error refreshing profile {ProfileId}", profileId);
        }
    }

    /// <summary>
    /// Scans for games and automatically creates profiles for detected installations.
    /// </summary>
    [RelayCommand]
    private async Task ScanForGames()
    {
        if (_installationService == null)
        {
            StatusMessage = "Game installation service not available";
            ErrorMessage = "Game Installation Service is not initialized";
            IsServiceAvailable = false;
            return;
        }

        if (IsScanning)
        {
            return; // Prevent multiple concurrent scans
        }

        try
        {
            IsScanning = true;
            StatusMessage = "Scanning for games...";
            ErrorMessage = string.Empty;

            // Scan for all installations
            var installations = await _installationService.GetAllInstallationsAsync();
            if (installations.Success && installations.Data != null)
            {
                var installationCount = installations.Data.Count;
                var generalsCount = installations.Data.Count(i => i.HasGenerals);
                var zeroHourCount = installations.Data.Count(i => i.HasZeroHour);

                _logger.LogInformation(
                    "Game scan completed successfully. Found {Count} installations ({GeneralsCount} Generals, {ZeroHourCount} Zero Hour)",
                    installationCount,
                    generalsCount,
                    zeroHourCount);

                // Generate manifests and populate versions for detected installations
                int manifestsGenerated = 0;
                int profilesCreated = 0;

                if (_profileEditorFacade != null && _gameProfileManager != null)
                {
                    foreach (var installation in installations.Data)
                    {
                        manifestsGenerated += installation.AvailableGameClients?.Count() * 2 ?? 0;

                        if (installation.HasGenerals)
                        {
                            var generalsProfileCreated = await TryCreateProfileForInstallation(installation, GameType.Generals);
                            if (generalsProfileCreated) profilesCreated++;
                        }

                        if (installation.HasZeroHour)
                        {
                            var zeroHourProfileCreated = await TryCreateProfileForInstallation(installation, GameType.ZeroHour);
                            if (zeroHourProfileCreated) profilesCreated++;
                        }
                    }
                }

                // Refresh the profiles list to show the newly created ones
                await InitializeAsync();

                StatusMessage = $"Scan complete. Found {installationCount} installations, generated {manifestsGenerated} manifests, created {profilesCreated} profiles";
            }
            else
            {
                var errors = string.Join(", ", installations.Errors);
                StatusMessage = $"Scan failed: {errors}";
                ErrorMessage = errors;
                _logger.LogWarning("Game scan failed: {Errors}", errors);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error scanning for games");
            StatusMessage = "Error during scan";
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsScanning = false;
        }
    }

    /// <summary>
    /// Attempts to create a profile for a specific installation and game type.
    /// </summary>
    /// <param name="installation">The game installation.</param>
    /// <param name="gameType">The game type (Generals or ZeroHour).</param>
    /// <returns>True if profile was created successfully, false otherwise.</returns>
    private async Task<bool> TryCreateProfileForInstallation(GameInstallation installation, GameType gameType)
    {
        try
        {
            if (_profileEditorFacade == null || _gameProfileManager == null)
                return false;

            // Find the appropriate GameClient for this game type
            var gameClient = gameType == GameType.Generals ? installation.GeneralsClient : installation.ZeroHourClient;
            if (gameClient == null)
            {
                _logger.LogWarning(
                    "No {GameType} client available for installation {InstallationId}. AvailableVersions count: {VersionCount}, HasGenerals: {HasGenerals}, HasZeroHour: {HasZeroHour}",
                    gameType,
                    installation.Id,
                    installation.AvailableGameClients.Count,
                    installation.HasGenerals,
                    installation.HasZeroHour);
                return false;
            }

            // Define profile name
            var profileName = $"{installation.InstallationType} {gameType}";

            // Check if a profile already exists for this exact name and installation
            var existingProfiles = await _gameProfileManager.GetAllProfilesAsync();
            if (existingProfiles.Success && existingProfiles.Data != null)
            {
                // Check by name AND installation ID
                bool profileExists = existingProfiles.Data.Any(p =>
                    p.Name.Equals(profileName, StringComparison.OrdinalIgnoreCase) &&
                    p.GameInstallationId.Equals(installation.Id, StringComparison.OrdinalIgnoreCase));

                if (profileExists)
                {
                    _logger.LogDebug("Profile already exists for {InstallationType} {GameType} (matched by Name+InstallationId), skipping", installation.InstallationType, gameType);
                    return false;
                }

                // Also check by name AND game client ID (in case installation ID changed but it's the same logical profile)
                bool profileExistsByClient = existingProfiles.Data.Any(p =>
                    p.Name.Equals(profileName, StringComparison.OrdinalIgnoreCase) &&
                    p.GameClient != null &&
                    p.GameClient.Id.Equals(gameClient.Id, StringComparison.OrdinalIgnoreCase));

                if (profileExistsByClient)
                {
                    _logger.LogDebug("Profile already exists for {InstallationType} {GameType} (matched by Name+ClientId), skipping", installation.InstallationType, gameType);
                    return false;
                }
            }

            // Get user's preferred workspace strategy or default
            var preferredStrategy = _configService?.GetDefaultWorkspaceStrategy() ?? WorkspaceStrategy.FullCopy;

            var manifestVersion = gameType == GameType.ZeroHour
                ? ManifestConstants.ZeroHourManifestVersion
                : ManifestConstants.GeneralsManifestVersion;

            // Generate the GameInstallation manifest ID for this specific game type
            // This must match what ManifestProvider generates
            var installationManifestId = ManifestIdGenerator.GenerateGameInstallationId(installation, gameType, manifestVersion);

            // Create enabled content list: GameInstallation manifest + GameClient manifest
            var enabledContentIds = new List<string>
            {
                installationManifestId, // GameInstallation manifest (required for launch validation)
                gameClient.Id,          // GameClient manifest (required for launch validation)
            };

            // Create the profile request using the client manifest ID for GameClientId
            var createRequest = new Core.Models.GameProfile.CreateProfileRequest
            {
                Name = profileName,
                GameInstallationId = installation.Id, // The actual installation GUID
                GameClientId = gameClient.Id, // Client manifest ID
                Description = $"Auto-created profile for {installation.InstallationType} {gameType} installation",
                PreferredStrategy = preferredStrategy,
                EnabledContentIds = enabledContentIds, // Both GameInstallation and GameClient manifests
                ThemeColor = GetThemeColorForGameType(gameType),
                IconPath = GetIconPathForGame(gameType, installation.InstallationType),
            };

            var profileResult = await _profileEditorFacade.CreateProfileWithWorkspaceAsync(createRequest);
            if (profileResult.Success && profileResult.Data != null)
            {
                _logger.LogInformation("Successfully created profile '{ProfileName}' for {InstallationType} {GameType}", profileResult.Data.Name, installation.InstallationType, gameType);
                return true;
            }
            else
            {
                var errors = string.Join(", ", profileResult.Errors);
                _logger.LogWarning("Failed to create profile for {InstallationType} {GameType}: {Errors}", installation.InstallationType, gameType, errors);
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating profile for {InstallationType} {GameType}", installation.InstallationType, gameType);
            return false;
        }
    }

    /// <summary>
    /// Launches the specified game profile.
    /// </summary>
    /// <param name="profile">The game profile to launch.</param>
    [RelayCommand]
    private async Task LaunchProfile(GameProfileItemViewModel profile)
    {
        if (_profileLauncherFacade == null)
        {
            StatusMessage = "Profile launcher not available";
            ErrorMessage = "Profile Launcher service is not initialized";
            return;
        }

        try
        {
            IsLaunching = true;
            StatusMessage = $"Launching {profile.Name}...";
            ErrorMessage = string.Empty;

            var launchResult = await _profileLauncherFacade.LaunchProfileAsync(profile.ProfileId);

            if (launchResult.Success && launchResult.Data != null)
            {
                // Update IsProcessRunning to show Stop button and hide Launch/Edit buttons
                profile.IsProcessRunning = true;
                profile.ProcessId = launchResult.Data.ProcessInfo.ProcessId;
                OnPropertyChanged(nameof(profile.CanLaunch));
                OnPropertyChanged(nameof(profile.CanEdit));

                StatusMessage = $"{profile.Name} launched successfully (Process ID: {launchResult.Data.ProcessInfo.ProcessId})";
                _logger.LogInformation(
                    "Profile {ProfileName} launched successfully with process ID {ProcessId}",
                    profile.Name,
                    launchResult.Data.ProcessInfo.ProcessId);
            }
            else
            {
                var errors = string.Join(", ", launchResult.Errors);
                StatusMessage = $"Failed to launch {profile.Name}: {errors}";
                ErrorMessage = errors;
                _logger.LogWarning(
                    "Failed to launch profile {ProfileName}: {Errors}",
                    profile.Name,
                    errors);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error launching profile {ProfileName}", profile.Name);
            StatusMessage = $"Error launching {profile.Name}";
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsLaunching = false;
        }
    }

    /// <summary>
    /// Stops the specified game profile.
    /// </summary>
    /// <param name="profile">The game profile to stop.</param>
    [RelayCommand]
    private async Task StopProfile(GameProfileItemViewModel profile)
    {
        if (_profileLauncherFacade == null)
        {
            StatusMessage = "Profile launcher not available";
            return;
        }

        try
        {
            StatusMessage = $"Stopping {profile.Name}...";

            var stopResult = await _profileLauncherFacade.StopProfileAsync(profile.ProfileId);

            if (stopResult.Success)
            {
                // Update IsProcessRunning to hide Stop button and show Launch button
                profile.IsProcessRunning = false;
                profile.ProcessId = 0;
                OnPropertyChanged(nameof(profile.CanLaunch));
                OnPropertyChanged(nameof(profile.CanEdit));

                StatusMessage = $"{profile.Name} stopped successfully";
                _logger.LogInformation("Profile {ProfileName} stopped successfully", profile.Name);
            }
            else
            {
                var errors = string.Join(", ", stopResult.Errors);
                StatusMessage = $"Failed to stop {profile.Name}: {errors}";
                _logger.LogWarning(
                    "Failed to stop profile {ProfileName}: {Errors}",
                    profile.Name,
                    errors);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error stopping profile {ProfileName}", profile.Name);
            StatusMessage = $"Error stopping {profile.Name}";
        }
    }

    /// <summary>
    /// Toggles edit mode for the profiles list.
    /// </summary>
    [RelayCommand]
    private void ToggleEditMode()
    {
        IsEditMode = !IsEditMode;
        StatusMessage = IsEditMode ? "Edit mode enabled" : "Edit mode disabled";
        _logger?.LogInformation("Toggled edit mode to {IsEditMode}", IsEditMode);
    }

    /// <summary>
    /// Saves changes made in edit mode.
    /// </summary>
    [RelayCommand]
    private async Task SaveProfiles()
    {
        if (_gameProfileManager == null)
        {
            StatusMessage = "Profile manager not available";
            return;
        }

        try
        {
            StatusMessage = "Saving profiles...";

            // Implementation for saving changes would go here
            // For now, just refresh the list
            await InitializeAsync();
            StatusMessage = "Profiles saved successfully";
            _logger?.LogInformation("Saved profiles in edit mode");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error saving profiles");
            StatusMessage = "Error saving profiles";
        }
    }

    /// <summary>
    /// Deletes the selected profile.
    /// </summary>
    [RelayCommand]
    private async Task DeleteProfile(GameProfileItemViewModel profile)
    {
        if (_profileLauncherFacade == null || string.IsNullOrEmpty(profile.ProfileId))
        {
            StatusMessage = "Profile launcher not available";
            return;
        }

        try
        {
            StatusMessage = $"Deleting {profile.Name}...";
            var deleteResult = await _profileLauncherFacade.DeleteProfileAsync(profile.ProfileId);

            if (deleteResult.Success)
            {
                Profiles.Remove(profile);
                StatusMessage = $"{profile.Name} deleted successfully";
                _logger?.LogInformation("Deleted profile {ProfileName}", profile.Name);
            }
            else
            {
                var errors = string.Join(", ", deleteResult.Errors);
                StatusMessage = $"Failed to delete {profile.Name}: {errors}";
                _logger?.LogWarning("Failed to delete profile {ProfileName}: {Errors}", profile.Name, errors);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error deleting profile {ProfileName}", profile.Name);
            StatusMessage = $"Error deleting {profile.Name}";
        }
    }

    /// <summary>
    /// Edits the specified game profile.
    /// </summary>
    /// <param name="profile">The game profile to edit.</param>
    [RelayCommand]
    private async Task EditProfile(GameProfileItemViewModel profile)
    {
        if (_settingsViewModel == null)
        {
            StatusMessage = "Profile settings not available";
            return;
        }

        try
        {
            if (_profileEditorFacade == null)
            {
                StatusMessage = "Profile editor not available";
                return;
            }

            // Load the profile using the profile editor facade
            var loadResult = await _profileEditorFacade.GetProfileWithWorkspaceAsync(profile.ProfileId);
            if (!loadResult.Success || loadResult.Data == null)
            {
                StatusMessage = $"Failed to load profile: {string.Join(", ", loadResult.Errors)}";
                return;
            }

            // Initialize the settings view model for this profile
            await _settingsViewModel.InitializeForProfileAsync(profile.ProfileId);

            // For now, just show the settings window - profile data loading into view model needs more implementation
            var mainWindow = GetMainWindow();
            if (mainWindow != null)
            {
                var settingsWindow = new GameProfileSettingsWindow
                {
                    DataContext = _settingsViewModel,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                };

                await settingsWindow.ShowDialog(mainWindow);

                // Refresh only the edited profile to preserve running state
                await RefreshSingleProfileAsync(profile.ProfileId);
                StatusMessage = "Profile updated successfully";
            }
            else
            {
                StatusMessage = "Could not find main window to open settings";
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error editing profile {ProfileName}", profile.Name);
            StatusMessage = $"Error editing {profile.Name}";
        }
    }

    /// <summary>
    /// Creates a new game profile.
    /// </summary>
    [RelayCommand]
    private async Task CreateNewProfile()
    {
        if (_settingsViewModel == null)
        {
            StatusMessage = "Profile settings not available";
            return;
        }

        try
        {
            // Initialize settings view model for new profile creation
            await _settingsViewModel.InitializeForNewProfileAsync();

            var mainWindow = GetMainWindow();
            if (mainWindow != null)
            {
                var settingsWindow = new GameProfileSettingsWindow
                {
                    DataContext = _settingsViewModel,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                };

                await settingsWindow.ShowDialog(mainWindow);

                // Refresh the profiles list after the window closes to show newly created profile
                await InitializeAsync();
                StatusMessage = "New profile window closed";
            }
            else
            {
                StatusMessage = "Could not find main window to open settings";
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error creating new profile");
            StatusMessage = "Error creating new profile";
        }
    }

    /// <summary>
    /// Prepares the workspace for the specified game profile.
    /// </summary>
    /// <param name="profile">The game profile to prepare workspace for.</param>
    [RelayCommand]
    private async Task PrepareWorkspace(GameProfileItemViewModel profile)
    {
        if (_profileLauncherFacade == null)
        {
            StatusMessage = "Profile launcher not available";
            return;
        }

        try
        {
            IsPreparingWorkspace = true;
            profile.IsPreparingWorkspace = true;
            StatusMessage = $"Preparing workspace for {profile.Name}...";
            var prepareResult = await _profileLauncherFacade.PrepareWorkspaceAsync(profile.ProfileId);

            if (prepareResult.Success && prepareResult.Data != null)
            {
                if (_gameProfileManager != null)
                {
                    var profileResult = await _gameProfileManager.GetProfileAsync(profile.ProfileId);
                    if (profileResult.Success && profileResult.Data != null)
                    {
                        var loadedProfile = profileResult.Data;

                        // Update the existing item's status
                        profile.UpdateWorkspaceStatus(loadedProfile.ActiveWorkspaceId, loadedProfile.WorkspaceStrategy);

                        // Force UI refresh by removing and re-adding to ObservableCollection
                        var index = Profiles.IndexOf(profile);
                        if (index >= 0)
                        {
                            Profiles.RemoveAt(index);
                            Profiles.Insert(index, profile);
                            _logger?.LogDebug("Forced UI refresh for profile {ProfileName} at index {Index}", profile.Name, index);
                        }
                    }
                }

                StatusMessage = $"Workspace prepared for {profile.Name} at {prepareResult.Data.WorkspacePath}";
                _logger?.LogInformation("Prepared workspace for profile {ProfileName} at {Path}", profile.Name, prepareResult.Data.WorkspacePath);
            }
            else
            {
                var errors = string.Join(", ", prepareResult.Errors);
                StatusMessage = $"Failed to prepare workspace for {profile.Name}: {errors}";
                _logger?.LogWarning("Failed to prepare workspace for profile {ProfileName}: {Errors}", profile.Name, errors);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error preparing workspace for profile {ProfileName}", profile.Name);
            StatusMessage = $"Error preparing workspace for {profile.Name}";
        }
        finally
        {
            IsPreparingWorkspace = false;
            profile.IsPreparingWorkspace = false;
        }
    }

    /// <summary>
    /// Handles the process exited event to update profile state when a game exits.
    /// </summary>
    private void OnProcessExited(object? sender, Core.Models.Events.GameProcessExitedEventArgs e)
    {
        try
        {
            _logger?.LogInformation("Game process {ProcessId} exited with code {ExitCode}", e.ProcessId, e.ExitCode);

            // Find the profile that was running this process
            var profile = Profiles.FirstOrDefault(p => p.ProcessId == e.ProcessId);
            if (profile != null)
            {
                profile.IsProcessRunning = false;
                profile.ProcessId = 0;
                _logger?.LogInformation("Updated profile {ProfileName} - process no longer running", profile.Name);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error handling process exit event for process {ProcessId}", e.ProcessId);
        }
    }
}
