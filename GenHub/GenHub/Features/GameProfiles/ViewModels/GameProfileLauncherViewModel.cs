using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using GenHub.Common.ViewModels;
using GenHub.Core.Constants;
using GenHub.Core.Helpers;
using GenHub.Core.Interfaces.Common;
using GenHub.Core.Interfaces.GameInstallations;
using GenHub.Core.Interfaces.GameProfiles;
using GenHub.Core.Interfaces.Notifications;
using GenHub.Core.Interfaces.Shortcuts;
using GenHub.Core.Interfaces.Steam;
using GenHub.Core.Messages;
using GenHub.Core.Models.Enums;
using GenHub.Core.Models.GameClients;
using GenHub.Core.Models.GameInstallations;
using GenHub.Core.Models.GameProfile;
using GenHub.Core.Models.Manifest;
using GenHub.Features.GameProfiles.Services;
using GenHub.Features.GameProfiles.Views;
using Microsoft.Extensions.Logging;

namespace GenHub.Features.GameProfiles.ViewModels;

/// <summary>
/// ViewModel for launching game profiles.
/// </summary>
public partial class GameProfileLauncherViewModel(
    IGameInstallationService installationService,
    IGameProfileManager gameProfileManager,
    IProfileLauncherFacade profileLauncherFacade,
    GameProfileSettingsViewModel settingsViewModel,
    IProfileEditorFacade profileEditorFacade,
    IConfigurationProviderService configService,
    IGameProcessManager gameProcessManager,
    IShortcutService shortcutService,
    IPublisherProfileOrchestrator publisherProfileOrchestrator,
    ISteamManifestPatcher steamManifestPatcher,
    ProfileResourceService profileResourceService,
    INotificationService notificationService,
    ILogger<GameProfileLauncherViewModel> logger) : ViewModelBase,
    IRecipient<ProfileCreatedMessage>,
    IRecipient<ProfileUpdatedMessage>,
    IRecipient<ProfileListUpdatedMessage>
{
    private readonly SemaphoreSlim _launchSemaphore = new(1, 1);

    [ObservableProperty]
    private ObservableCollection<GameProfileItemViewModel> _profiles = [];

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

    [ObservableProperty]
    private bool _isShowingPatchPrompt;

    [ObservableProperty]
    private string _promptMessage = string.Empty;

    [ObservableProperty]
    private string _promptTitle = string.Empty;

    private GameInstallation? _pendingInstallation;
    private TaskCompletionSource<bool>? _promptCompletionSource;

    /// <summary>
    /// Performs asynchronous initialization for the GameProfileLauncherViewModel.
    /// Loads all game profiles and subscribes to process exit events.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    public virtual async Task InitializeAsync()
    {
        try
        {
            gameProcessManager.ProcessExited += OnProcessExited;

            StatusMessage = "Loading profiles...";
            ErrorMessage = string.Empty;
            Profiles.Clear();

            var profilesResult = await gameProfileManager.GetAllProfilesAsync();
            if (profilesResult.Success && profilesResult.Data != null)
            {
                foreach (var profile in profilesResult.Data)
                {
                    // Use ProfileResourceService to get default paths based on game type if profile paths are missing
                    var gameTypeStr = profile.GameClient?.GameType.ToString() ?? "ZeroHour";

                    var iconPath = !string.IsNullOrEmpty(profile.IconPath)
                        ? profile.IconPath
                        : UriConstants.DefaultIconUri;

                    var coverPath = !string.IsNullOrEmpty(profile.CoverPath)
                        ? profile.CoverPath
                        : profileResourceService.GetDefaultCoverPath(gameTypeStr);

                    var item = new GameProfileItemViewModel(
                        profile.Id,
                        profile,
                        iconPath,
                        coverPath)
                    {
                        LaunchAction = LaunchProfileAsync,
                        EditProfileAction = EditProfile,
                        DeleteProfileAction = DeleteProfile,
                        CreateShortcutAction = CreateShortcut,
                    };

                    // Add to collection before the "Add New Profile" button (which is always at the end)
                    // If the last item is AddProfileItemViewModel, insert before it
                    if (Profiles.Count > 0 && Profiles[^1] is AddProfileItemViewModel)
                    {
                        Profiles.Insert(Profiles.Count - 1, item);
                    }
                    else
                    {
                        Profiles.Add(item);
                    }
                }

                // Add "Add New Profile" item at the end
                Profiles.Add(new AddProfileItemViewModel());

                var profileCount = Profiles.Count - 1;
                StatusMessage = $"Loaded {profileCount} profiles";
                logger.LogInformation("Loaded {Count} game profiles", profileCount);
            }
            else
            {
                var errors = string.Join(", ", profilesResult.Errors);
                StatusMessage = $"Failed to load profiles: {errors}";
                ErrorMessage = errors;
                logger.LogWarning("Failed to load profiles: {Errors}", errors);
            }

            // Register for profile messages on first initialization only
            if (!WeakReferenceMessenger.Default.IsRegistered<ProfileCreatedMessage>(this))
            {
                WeakReferenceMessenger.Default.RegisterAll(this);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error initializing profiles");
            StatusMessage = "Error loading profiles";
            ErrorMessage = ex.Message;
            IsServiceAvailable = false;
        }
    }

    /// <summary>
    /// Receives notification when a new profile is created and refreshes the profiles list.
    /// </summary>
    /// <param name="message">The profile created message.</param>
    public void Receive(ProfileCreatedMessage message)
    {
        logger.LogInformation("Profile created notification received for {ProfileName}, adding to UI", message.Profile.Name);

        // Add profile to UI on UI thread
        Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
        {
            try
            {
                AddProfileToUI(message.Profile);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error adding profile to UI after creation");
            }
        });
    }

    /// <summary>
    /// Receives notification when a profile is updated and refreshes the profiles list.
    /// </summary>
    /// <param name="message">The profile updated message.</param>
    public void Receive(ProfileUpdatedMessage message)
    {
        logger.LogInformation("Profile updated notification received for {ProfileName}, refreshing list", message.Profile.Name);

        // Refresh specific profile on UI thread to preserve state of others
        Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(async () =>
        {
            try
            {
                await RefreshSingleProfileAsync(message.Profile.Id);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error refreshing profile after update");
            }
        });
    }

    /// <summary>
    /// Receives notification when the profile list has been updated (bulk changes).
    /// </summary>
    /// <param name="message">The profile list updated message.</param>
    public void Receive(ProfileListUpdatedMessage message)
    {
        logger.LogInformation("Profile list updated notification received, refreshing list");

        // Refresh profiles on UI thread
        Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(async () =>
        {
            try
            {
                await InitializeAsync();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error refreshing profiles after list update");
            }
        });
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
    /// <returns>The relative icon path.</returns>
    private static string GetIconPathForGame(GameType gameType)
    {
        var gameIcon = gameType == GameType.Generals ? UriConstants.GeneralsIconFilename : UriConstants.ZeroHourIconFilename;

        // For now, return the game-specific icon - could be enhanced to combine with platform icon
        return $"{UriConstants.IconsBasePath}/{gameIcon}";
    }

    /// <summary>
    /// Checks if the installation has any publisher-based game clients (GeneralsOnline, TheSuperHackers).
    /// </summary>
    private static bool HasPublisherClients(GameInstallation installation)
    {
        return installation.AvailableGameClients != null &&
               installation.AvailableGameClients.Any(c => c.IsPublisherClient);
    }

    /// <summary>
    /// Checks if a game client is a standard base game client (not a publisher client).
    /// </summary>
    private static bool IsStandardGameClient(GameClient client)
    {
        return !client.IsPublisherClient;
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
            var profileResult = await gameProfileManager.GetProfileAsync(profileId);
            if (profileResult.Success && profileResult.Data != null)
            {
                var profile = profileResult.Data;
                var existingItem = Profiles.OfType<GameProfileItemViewModel>().FirstOrDefault(p => p.ProfileId == profileId);

                if (existingItem != null)
                {
                    // Preserve the running state before updating
                    var wasRunning = existingItem.IsProcessRunning;
                    var processId = existingItem.ProcessId;
                    var workspaceId = existingItem.ActiveWorkspaceId;

                    // Update the profile data
                    var gameTypeStr = profile.GameClient?.GameType.ToString() ?? "ZeroHour";

                    var iconPath = !string.IsNullOrEmpty(profile.IconPath)
                        ? profile.IconPath
                        : UriConstants.DefaultIconUri;

                    var coverPath = !string.IsNullOrEmpty(profile.CoverPath)
                        ? profile.CoverPath
                        : profileResourceService.GetDefaultCoverPath(gameTypeStr);

                    var newItem = new GameProfileItemViewModel(
                        profile.Id,
                        profile,
                        iconPath,
                        coverPath)
                    {
                        LaunchAction = LaunchProfileAsync,
                        EditProfileAction = EditProfile,
                        DeleteProfileAction = DeleteProfile,
                        CreateShortcutAction = CreateShortcut,
                    };

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

                    logger.LogInformation("Refreshed profile {ProfileId} (Running: {IsRunning})", profileId, wasRunning);
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error refreshing profile {ProfileId}", profileId);
        }
    }

    /// <summary>
    /// Scans for games and automatically creates profiles for detected installations.
    /// Implements smart profile creation: skips base Generals/ZeroHour if publisher clients exist,
    /// or prompts for Community Patch installation.
    /// </summary>
    [RelayCommand]
    private async Task ScanForGamesAsync()
    {
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
            var installations = await installationService.GetAllInstallationsAsync();
            if (installations.Success && installations.Data != null)
            {
                var installationCount = installations.Data.Count;
                var generalsCount = installations.Data.Count(i => i.HasGenerals);
                var zeroHourCount = installations.Data.Count(i => i.HasZeroHour);

                logger.LogInformation(
                    "Game scan completed. Found {Count} installations ({GeneralsCount} Generals, {ZeroHourCount} Zero Hour)",
                    installationCount,
                    generalsCount,
                    zeroHourCount);

                int profilesCreated = 0;

                // Track user preference for publisher clients during this scan to avoid multiple prompts
                bool? wantsGeneralsOnline = null;
                bool? wantsSuperHackers = null;

                foreach (var installation in installations.Data)
                {
                    if (installation.AvailableGameClients == null || installation.AvailableGameClients.Count == 0)
                    {
                        continue;
                    }

                    // Check if this installation has any publisher clients (GeneralsOnline, TheSuperHackers)
                    bool hasPublisherClients = HasPublisherClients(installation);

                    // Track if GeneralsOnline was already detected in this installation
                    bool hasGeneralsOnline = installation.AvailableGameClients.Any(c =>
                        c.PublisherType?.Equals(PublisherTypeConstants.GeneralsOnline, StringComparison.OrdinalIgnoreCase) == true);

                    if (hasPublisherClients)
                    {
                        // Publisher clients detected - create profiles only for publisher clients, skip base games
                        logger.LogInformation(
                            "Installation {InstallationId} has publisher clients, skipping base game profiles",
                            installation.Id);

                        notificationService.ShowInfo(
                            "Existing Patches Found",
                            "Creating profiles for existing patches...",
                            null);

                        foreach (var gameClient in installation.AvailableGameClients)
                        {
                            if (gameClient.IsPublisherClient)
                            {
                                logger.LogInformation(
                                    "Processing publisher client: {ClientName} (PublisherType: '{PublisherType}')",
                                    gameClient.Name,
                                    gameClient.PublisherType ?? "null");

                                // Special handling for GeneralsOnline:
                                // If profiles don't exist, PROMPT the user instead of auto-creating.
                                // This handles the case where Community Patch installs GO files but the user declined the GO profile.
                                if (gameClient.PublisherType == PublisherTypeConstants.GeneralsOnline)
                                {
                                    bool profileExists = await ProfileExistsAsync(installation, gameClient);
                                    if (!profileExists)
                                    {
                                        // If user already declined during this scan, skip
                                        if (wantsGeneralsOnline == false)
                                        {
                                            continue;
                                        }

                                        // If not yet asked, prompt
                                        if (wantsGeneralsOnline == null)
                                        {
                                            // TODO: Localization - Move these strings to localization system when implemented
                                            logger.LogInformation("GeneralsOnline files detected but no profile exists. Prompting user.");
                                            wantsGeneralsOnline = await ShowPromptAsync(
                                                "GeneralsOnline Detected",
                                                "GeneralsOnline files were found in your installation. Do you want to create profiles for them?",
                                                installation);
                                        }

                                        // If declined, skip
                                        if (wantsGeneralsOnline == false)
                                        {
                                            logger.LogInformation("User declined GeneralsOnline profile creation during scan.");
                                            continue;
                                        }
                                    }
                                }

                                // Special handling for TheSuperHackers:
                                // If profiles don't exist, PROMPT the user instead of auto-creating.
                                else if (gameClient.PublisherType == PublisherTypeConstants.TheSuperHackers)
                                {
                                    bool profileExists = await ProfileExistsAsync(installation, gameClient);
                                    if (!profileExists)
                                    {
                                        // If user already declined during this scan, skip
                                        if (wantsSuperHackers == false)
                                        {
                                            continue;
                                        }

                                        // If not yet asked, prompt
                                        if (wantsSuperHackers == null)
                                        {
                                            // TODO: Localization - Move these strings to localization system when implemented
                                            logger.LogInformation("SuperHackers files detected but no profile exists. Prompting user.");
                                            wantsSuperHackers = await ShowPromptAsync(
                                                "SuperHackers Weekly Release Detected",
                                                "SuperHackers weekly release files were found in your installation. Do you want to create profiles for them?",
                                                installation);
                                        }

                                        // If declined, skip
                                        if (wantsSuperHackers == false)
                                        {
                                            logger.LogInformation("User declined SuperHackers profile creation during scan.");
                                            continue;
                                        }
                                    }
                                }

                                var profileCreated = await TryCreateProfileForGameClientAsync(installation, gameClient);
                                if (profileCreated) profilesCreated++;
                            }
                            else
                            {
                                logger.LogDebug(
                                    "Skipping base game client {ClientName} - publisher clients exist",
                                    gameClient.Name);
                            }
                        }
                    }
                    else
                    {
                        // No publisher clients - prompt for Community Patch installation
                        logger.LogInformation(
                            "Installation {InstallationId} has no publisher clients, prompting for Community Patch",
                            installation.Id);

                        // TODO: Localization - Move these strings to localization system when implemented
                        var wantsCommunityPatch = await ShowPromptAsync(
                            "Install Community Patch?",
                            "Would you like to install the Community Patch? This includes the latest fixes and improvements and is recommended over base game profiles.",
                            installation);

                        if (wantsCommunityPatch)
                        {
                            // User wants Community Patch - use the orchestrator to acquire and create profiles
                            logger.LogInformation("User accepted Community Patch installation");
                            notificationService.ShowInfo(
                                "Installing Community Patch",
                                "Downloading and installing Community Patch...",
                                null);

                            // Create synthetic GameClient for Community Patch
                            var communityPatchClient = new GameClient
                            {
                                Id = $"communityoutpost.gameclient.communitypatch",
                                Name = "Community Patch",
                                PublisherType = CommunityOutpostConstants.PublisherType,
                                GameType = GameType.ZeroHour,
                                InstallationId = installation.Id,
                            };

                            var cpResult = await publisherProfileOrchestrator.CreateProfilesForPublisherClientAsync(
                                installation, communityPatchClient);

                            if (cpResult.Success && cpResult.Data > 0)
                            {
                                profilesCreated += cpResult.Data;
                                logger.LogInformation("Community Patch installed, created {Count} profiles", cpResult.Data);

                                // Only prompt for GeneralsOnline if it wasn't already detected in the installation
                                // This prevents duplicate prompts when GO files already exist
                                if (!hasGeneralsOnline)
                                {
                                    // Check global preference before prompting
                                    // TODO: Localization - Move these strings to localization system when implemented
                                    wantsGeneralsOnline ??= await ShowPromptAsync(
                                        "Install GeneralsOnline?",
                                        "Would you also like to install GeneralsOnline for online multiplayer?",
                                        installation);

                                    if (wantsGeneralsOnline == true)
                                    {
                                        notificationService.ShowInfo(
                                            "Installing GeneralsOnline",
                                            "Downloading and installing GeneralsOnline...",
                                            null);

                                        var goClient = new GameClient
                                        {
                                            Id = $"generalsonline.gameclient",
                                            Name = "GeneralsOnline",
                                            PublisherType = PublisherTypeConstants.GeneralsOnline,
                                            GameType = GameType.ZeroHour,
                                            InstallationId = installation.Id,
                                        };

                                        var goResult = await publisherProfileOrchestrator.CreateProfilesForPublisherClientAsync(
                                            installation, goClient);

                                        if (goResult.Success && goResult.Data > 0)
                                        {
                                            profilesCreated += goResult.Data;
                                            logger.LogInformation("GeneralsOnline installed, created {Count} profiles", goResult.Data);
                                        }
                                    }
                                }
                                else
                                {
                                    logger.LogInformation("GeneralsOnline already detected in installation, skipping installation prompt");
                                }
                            }
                            else
                            {
                                logger.LogWarning("Failed to create Community Patch profiles");
                                notificationService.ShowWarning(
                                    "Community Patch Failed",
                                    "Community Patch installation failed. Creating base game profiles as fallback.");

                                // Fallback to base profiles if acquisition failed
                                foreach (var gameClient in installation.AvailableGameClients)
                                {
                                    var profileCreated = await TryCreateProfileForGameClientAsync(installation, gameClient);
                                    if (profileCreated) profilesCreated++;
                                }
                            }
                        }
                        else
                        {
                            // User declined - create base game profiles as fallback
                            logger.LogInformation("User declined Community Patch, creating base game profiles");
                            notificationService.ShowInfo(
                                "Creating Base Profiles",
                                "Creating profiles for base game installations...",
                                null);

                            foreach (var gameClient in installation.AvailableGameClients)
                            {
                                var profileCreated = await TryCreateProfileForGameClientAsync(installation, gameClient);
                                if (profileCreated) profilesCreated++;
                            }
                        }
                    }
                }

                StatusMessage = $"Scan complete. Found {installations.Data.Count} installations, created {profilesCreated} profiles";

                notificationService.ShowSuccess(
                    "Scan Complete",
                    $"Created {profilesCreated} profile(s) for your game installations.");
            }
            else
            {
                var errors = string.Join(", ", installations.Errors);
                StatusMessage = $"Scan failed: {errors}";
                ErrorMessage = errors;
                logger.LogWarning("Game scan failed: {Errors}", errors);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error scanning for games");
            StatusMessage = "Error during scan";
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsScanning = false;
        }
    }

    /// <summary>
    /// Attempts to create a profile for a specific game client within an installation.
    /// </summary>
    /// <param name="installation">The game installation.</param>
    /// <param name="gameClient">The game client to create a profile for.</param>
    /// <returns>True if profile was created successfully, false otherwise.</returns>
    private async Task<bool> TryCreateProfileForGameClientAsync(GameInstallation installation, GameClient gameClient)
    {
        try
        {
            if (gameClient == null)
            {
                logger.LogWarning(
                    "GameClient is null for installation {InstallationId}",
                    installation.Id);
                return false;
            }

            // For publisher clients, handle specially to create all variant profiles
            if (gameClient.IsPublisherClient)
            {
                var result = await publisherProfileOrchestrator.CreateProfilesForPublisherClientAsync(installation, gameClient);
                if (result.Success && result.Data > 0)
                {
                    // No need to manually refresh - ProfileCreatedMessage handles UI updates
                    return true;
                }

                return false;
            }

            // Define profile name based on game client name and installation type
            var profileName = $"{installation.InstallationType} {gameClient.Name}";

            // Check if a profile already exists for this exact name and installation
            var existingProfiles = await gameProfileManager.GetAllProfilesAsync();
            if (existingProfiles.Success && existingProfiles.Data != null)
            {
                // Check by name AND installation ID
                bool profileExists = existingProfiles.Data.Any(p =>
                    p.Name.Equals(profileName, StringComparison.OrdinalIgnoreCase) &&
                    p.GameInstallationId.Equals(installation.Id, StringComparison.OrdinalIgnoreCase));

                if (profileExists)
                {
                    logger.LogDebug("Profile already exists for {InstallationType} {GameClientName} (matched by Name+InstallationId), skipping", installation.InstallationType, gameClient.Name);
                    return false;
                }

                // Also check by name AND game client ID (in case installation ID changed but it's the same logical profile)
                bool profileExistsByClient = existingProfiles.Data.Any(p =>
                    p.Name.Equals(profileName, StringComparison.OrdinalIgnoreCase) &&
                    p.GameClient != null &&
                    p.GameClient.Id.Equals(gameClient.Id, StringComparison.OrdinalIgnoreCase));

                if (profileExistsByClient)
                {
                    logger.LogDebug("Profile already exists for {InstallationType} {GameClientName} (matched by Name+ClientId), skipping", installation.InstallationType, gameClient.Name);
                    return false;
                }
            }

            var preferredStrategy = configService.GetDefaultWorkspaceStrategy();

            // Generate the GameInstallation manifest ID
            // Logic must match GameInstallationService.GenerateAndPoolManifestForGameTypeAsync to ensure ID alignment
            string installationManifestId;
            if (string.IsNullOrEmpty(gameClient.Version) ||
                gameClient.Version.Equals("Unknown", StringComparison.OrdinalIgnoreCase) ||
                gameClient.Version.Equals("Auto-Updated", StringComparison.OrdinalIgnoreCase) ||
                gameClient.Version.Equals(GameClientConstants.AutoDetectedVersion, StringComparison.OrdinalIgnoreCase))
            {
                // For unknown/auto versions, GameInstallationService uses version 0
                installationManifestId = ManifestIdGenerator.GenerateGameInstallationId(installation, gameClient.GameType, 0);
            }
            else
            {
                try
                {
                    // Use string overload which handles normalization (e.g. "1.0" -> "100") consistent with ManifestIdGenerator rules
                    installationManifestId = ManifestIdGenerator.GenerateGameInstallationId(installation, gameClient.GameType, gameClient.Version);
                }
                catch (ArgumentException)
                {
                    // If normalization fails (invalid format), fallback to 0
                    installationManifestId = ManifestIdGenerator.GenerateGameInstallationId(installation, gameClient.GameType, 0);
                }
            }

            // Create enabled content list: GameInstallation manifest + GameClient manifest
            var enabledContentIds = new List<string>
            {
                installationManifestId, // GameInstallation manifest (required for launch validation)
                gameClient.Id,          // GameClient manifest (required for launch validation)
            };

            // Determine assets based on game type using ProfileResourceService
            var gameTypeStr = gameClient.GameType.ToString();
            var iconPath = profileResourceService.GetDefaultIconPath(gameTypeStr);
            var coverPath = profileResourceService.GetDefaultCoverPath(gameTypeStr);

            // Create the profile request using the client manifest ID for GameClientId
            var createRequest = new CreateProfileRequest
            {
                Name = profileName,
                GameInstallationId = installation.Id, // The actual installation GUID
                GameClientId = gameClient.Id, // Client manifest ID
                Description = $"Auto-created profile for {installation.InstallationType} {gameClient.Name}",
                PreferredStrategy = preferredStrategy,
                EnabledContentIds = enabledContentIds, // Both GameInstallation and GameClient manifests
                ThemeColor = GetThemeColorForGameType(gameClient.GameType),
                IconPath = iconPath,
                CoverPath = coverPath,
            };

            var profileResult = await profileEditorFacade.CreateProfileWithWorkspaceAsync(createRequest);
            if (profileResult.Success && profileResult.Data != null)
            {
                logger.LogInformation("Successfully created profile '{ProfileName}' for {InstallationType} {GameClientName}", profileResult.Data.Name, installation.InstallationType, gameClient.Name);

                // Add profile to UI immediately
                AddProfileToUI(profileResult.Data);

                return true;
            }
            else
            {
                var errors = ManifestHelper.FormatErrors(profileResult.Errors);
                logger.LogWarning("Failed to create profile for {InstallationType} {GameClientName}: {Errors}", installation.InstallationType, gameClient.Name, errors);
                return false;
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error creating profile for {InstallationType} {GameClientName}", installation.InstallationType, gameClient?.Name ?? "Unknown");
            return false;
        }
    }

    /// <summary>
    /// Checks if a profile already exists for a game client.
    /// Handles special matching for publisher clients (e.g., GeneralsOnline)
    /// when the profile name differs slightly from the detected client name (e.g., "GeneralsOnline" vs "GeneralsOnline 30Hz").
    /// </summary>
    private async Task<bool> ProfileExistsAsync(GameInstallation installation, GameClient gameClient)
    {
        try
        {
            var profileName = $"{installation.InstallationType} {gameClient.Name}";
            var existingProfiles = await gameProfileManager.GetAllProfilesAsync();

            if (existingProfiles.Success && existingProfiles.Data != null)
            {
                // For publisher clients, check if any profile exists for this publisher type
                if (gameClient.IsPublisherClient && !string.IsNullOrEmpty(gameClient.PublisherType))
                {
                    bool publisherProfileExists = existingProfiles.Data.Any(p =>
                        p.GameInstallationId.Equals(installation.Id, StringComparison.OrdinalIgnoreCase) &&
                        p.GameClient != null &&
                        p.GameClient.PublisherType?.Equals(gameClient.PublisherType, StringComparison.OrdinalIgnoreCase) == true);

                    if (publisherProfileExists)
                    {
                        logger.LogDebug(
                            "Profile already exists for publisher {PublisherType} in installation {InstallationId} (looser match)",
                            gameClient.PublisherType,
                            installation.Id);
                        return true;
                    }
                }

                // Standard matching: Check by name AND installation ID
                bool profileExists = existingProfiles.Data.Any(p =>
                    p.Name.Equals(profileName, StringComparison.OrdinalIgnoreCase) &&
                    p.GameInstallationId.Equals(installation.Id, StringComparison.OrdinalIgnoreCase));

                if (profileExists) return true;

                // Standard matching: Check by name and game client ID
                bool profileExistsByClient = existingProfiles.Data.Any(p =>
                    p.Name.Equals(profileName, StringComparison.OrdinalIgnoreCase) &&
                    p.GameClient != null &&
                    p.GameClient.Id.Equals(gameClient.Id, StringComparison.OrdinalIgnoreCase));

                if (profileExistsByClient) return true;
            }

            return false;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error checking if profile exists for {ClientName}", gameClient.Name);
            return false;
        }
    }

    /// <summary>
    /// Adds a newly created profile to the UI immediately (without waiting for full refresh).
    /// </summary>
    private void AddProfileToUI(Core.Models.GameProfile.GameProfile profile)
    {
        try
        {
            // Check if profile already exists in UI
            if (Profiles.OfType<GameProfileItemViewModel>().Any(p => p.ProfileId.Equals(profile.Id, StringComparison.OrdinalIgnoreCase)))
            {
                logger.LogDebug("Profile {ProfileId} already in UI, skipping add", profile.Id);
                return;
            }

            var iconPath = !string.IsNullOrEmpty(profile.IconPath)
                ? profile.IconPath
                : UriConstants.DefaultIconUri;

            var coverPath = !string.IsNullOrEmpty(profile.CoverPath)
                ? profile.CoverPath
                : iconPath;

            var item = new GameProfileItemViewModel(
                profile.Id,
                profile,
                iconPath,
                coverPath)
            {
                LaunchAction = LaunchProfileAsync,
                EditProfileAction = EditProfile,
                DeleteProfileAction = DeleteProfile,
                CreateShortcutAction = CreateShortcut,
            };

            // Add to collection before the "Add New Profile" button (which is always at the end)
            // If the last item is not AddProfileItemViewModel, just Add
            if (Profiles.Count > 0 && Profiles[^1] is AddProfileItemViewModel)
            {
                Profiles.Insert(Profiles.Count - 1, item);
            }
            else
            {
                Profiles.Add(item);
            }

            logger.LogDebug("Added profile {ProfileName} to UI (Total: {Count})", profile.Name, Profiles.Count);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Error adding profile {ProfileId} to UI", profile.Id);
        }
    }

    /// <summary>
    /// Launches the specified game profile.
    /// </summary>
    /// <param name="profile">The game profile to launch.</param>
    [RelayCommand]
    private async Task LaunchProfileAsync(GameProfileItemViewModel profile)
    {
        // Try without blocking
        if (!await _launchSemaphore.WaitAsync(0))
        {
            StatusMessage = "A profile is already launching...";
            return;
        }

        try
        {
            try
            {
                IsLaunching = true;
                StatusMessage = $"Validating {profile.Name}...";
                ErrorMessage = string.Empty;

                // With CAS hardlinks, profile switching is instant - maps are just symlinks
                logger.LogDebug("[Launch] Launching profile {ProfileName} (ID: {ProfileId})", profile.Name, profile.ProfileId);

                // Normal launch
                await ExecuteLaunchAsync(profile, skipUserDataCleanup: false);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error starting launch process for {ProfileName}", profile.Name);
                StatusMessage = $"Error launching {profile.Name}";
                ErrorMessage = ex.Message;
                notificationService.ShowError("Launch Error", $"Error starting launch for {profile.Name}: {ex.Message}");
            }
            finally
            {
                IsLaunching = false;
            }
        }
        finally
        {
            _launchSemaphore.Release();
        }
    }

    /// <summary>
    /// Executes the actual launch operation.
    /// </summary>
    private async Task ExecuteLaunchAsync(GameProfileItemViewModel profile, bool skipUserDataCleanup)
    {
        StatusMessage = $"Launching {profile.Name}...";

        // Show "taking a while" message if many maps are being linked
        if (skipUserDataCleanup && profile.IsLargeMapCount)
        {
            StatusMessage = "Adding maps to profile (this might take a while)...";
            notificationService.ShowInfo("Loading Maps", "Adding many maps to this profile. This may take a moment...", NotificationDurations.Long);
        }

        var launchResult = await profileLauncherFacade.LaunchProfileAsync(profile.ProfileId, skipUserDataCleanup);

        if (launchResult.Success && launchResult.Data != null)
        {
            // Look up the profile in the collection in case it was replaced by an update during launch
            var liveProfile = Profiles.FirstOrDefault(p => p.ProfileId == profile.ProfileId) ?? profile;

            liveProfile.IsProcessRunning = true;
            liveProfile.ProcessId = launchResult.Data.ProcessInfo.ProcessId;
            liveProfile.ShowUserDataConfirmation = false; // Hide confirmation if it was shown

            // Ensure notifications are sent for binding updates
            liveProfile.NotifyCanLaunchChanged();

            StatusMessage = $"{liveProfile.Name} launched successfully (Process ID: {launchResult.Data.ProcessInfo.ProcessId})";
        }
        else
        {
            var errors = string.Join(", ", launchResult.Errors);
            StatusMessage = $"Failed to launch {profile.Name}: {errors}";
            ErrorMessage = errors;
            notificationService.ShowError("Launch Failed", $"Failed to launch {profile.Name}: {errors}");
        }
    }

    /// <summary>
    /// Confirms that user data should be kept and added to the new profile.
    /// </summary>
    [RelayCommand]
    private async Task ConfirmUserDataKeepAsync(GameProfileItemViewModel profile)
    {
        profile.ShowUserDataConfirmation = false;

        if (!await _launchSemaphore.WaitAsync(0))
        {
            StatusMessage = "A profile is already launching...";
            return;
        }

        try
        {
            IsLaunching = true;
            await ExecuteLaunchAsync(profile, skipUserDataCleanup: true);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during confirmed launch (Keep) for {ProfileName}", profile.Name);
            StatusMessage = $"Error launching {profile.Name}";
            ErrorMessage = ex.Message;
            notificationService.ShowError("Launch Error", $"Error launching {profile.Name}: {ex.Message}");
        }
        finally
        {
            IsLaunching = false;
            _launchSemaphore.Release();
        }
    }

    /// <summary>
    /// Confirms that user data should be removed (normal switch).
    /// </summary>
    [RelayCommand]
    private async Task ConfirmUserDataRemoveAsync(GameProfileItemViewModel profile)
    {
        profile.ShowUserDataConfirmation = false;

        if (!await _launchSemaphore.WaitAsync(0))
        {
            StatusMessage = "A profile is already launching...";
            return;
        }

        try
        {
            IsLaunching = true;
            await ExecuteLaunchAsync(profile, skipUserDataCleanup: false);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during confirmed launch (Remove) for {ProfileName}", profile.Name);
            StatusMessage = $"Error launching {profile.Name}";
            ErrorMessage = ex.Message;
            notificationService.ShowError("Launch Error", $"Error launching {profile.Name}: {ex.Message}");
        }
        finally
        {
            IsLaunching = false;
            _launchSemaphore.Release();
        }
    }

    /// <summary>
    /// Cancels the user data confirmation and stops the launch.
    /// </summary>
    [RelayCommand]
    private void CancelUserDataConfirmation(GameProfileItemViewModel profile)
    {
        profile.ShowUserDataConfirmation = false;
        profile.UserDataSwitchInfo = null;
        StatusMessage = "Launch cancelled";
    }

    /// <summary>
    /// Stops the specified game profile.
    /// </summary>
    /// <param name="profile">The game profile to stop.</param>
    [RelayCommand]
    private async Task StopProfile(GameProfileItemViewModel profile)
    {
        try
        {
            StatusMessage = $"Stopping {profile.Name}...";

            var stopResult = await profileLauncherFacade.StopProfileAsync(profile.ProfileId);

            if (stopResult.Success)
            {
                // Update IsProcessRunning to hide Stop button and show Launch button
                profile.IsProcessRunning = false;
                profile.ProcessId = 0;
                OnPropertyChanged(nameof(profile.CanLaunch));
                OnPropertyChanged(nameof(profile.CanEdit));

                StatusMessage = $"{profile.Name} stopped successfully";
                logger.LogInformation("Profile {ProfileName} stopped successfully", profile.Name);
            }
            else
            {
                var errors = string.Join(", ", stopResult.Errors);
                StatusMessage = $"Failed to stop {profile.Name}: {errors}";
                logger.LogWarning(
                    "Failed to stop profile {ProfileName}: {Errors}",
                    profile.Name,
                    errors);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error stopping profile {ProfileName}", profile.Name);
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
        logger.LogInformation("Toggled edit mode to {IsEditMode}", IsEditMode);
    }

    /// <summary>
    /// Saves changes made in edit mode.
    /// </summary>
    [RelayCommand]
    private async Task SaveProfiles()
    {
        try
        {
            StatusMessage = "Saving profiles...";

            // Implementation for saving changes would go here
            // For now, just refresh the list
            await InitializeAsync();
            StatusMessage = "Profiles saved successfully";
            logger.LogInformation("Saved profiles in edit mode");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error saving profiles");
            StatusMessage = "Error saving profiles";
        }
    }

    /// <summary>
    /// Deletes the selected profile.
    /// </summary>
    [RelayCommand]
    private async Task DeleteProfile(GameProfileItemViewModel profile)
    {
        if (string.IsNullOrEmpty(profile.ProfileId))
        {
            StatusMessage = "Invalid profile";
            return;
        }

        try
        {
            StatusMessage = $"Deleting {profile.Name}...";
            var deleteResult = await profileLauncherFacade.DeleteProfileAsync(profile.ProfileId);

            if (deleteResult.Success)
            {
                Profiles.Remove(profile);
                StatusMessage = $"{profile.Name} deleted successfully";
                logger.LogInformation("Deleted profile {ProfileName}", profile.Name);

                try
                {
                    WeakReferenceMessenger.Default.Send(
                        new ProfileDeletedMessage(profile.ProfileId, profile.Name), 0);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to send ProfileDeletedMessage");
                }
            }
            else
            {
                var errors = string.Join(", ", deleteResult.Errors);
                StatusMessage = $"Failed to delete {profile.Name}: {errors}";
                logger.LogWarning("Failed to delete profile {ProfileName}: {Errors}", profile.Name, errors);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error deleting profile {ProfileName}", profile.Name);
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
        try
        {
            // Load the profile using the profile editor facade
            var loadResult = await profileEditorFacade.GetProfileWithWorkspaceAsync(profile.ProfileId);
            if (!loadResult.Success || loadResult.Data == null)
            {
                StatusMessage = $"Failed to load profile: {string.Join(", ", loadResult.Errors)}";
                return;
            }

            // Initialize the settings view model for this profile
            await settingsViewModel.InitializeForProfileAsync(profile.ProfileId);

            // For now, just show the settings window - profile data loading into view model needs more implementation
            var mainWindow = GetMainWindow();
            if (mainWindow != null)
            {
                var settingsWindow = new GameProfileSettingsWindow
                {
                    DataContext = settingsViewModel,
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
            logger.LogError(ex, "Error editing profile {ProfileName}", profile.Name);
            StatusMessage = $"Error editing {profile.Name}";
        }
    }

    /// <summary>
    /// Creates a new game profile.
    /// </summary>
    [RelayCommand]
    private async Task CreateNewProfile()
    {
        try
        {
            // Initialize settings view model for new profile creation
            await settingsViewModel.InitializeForNewProfileAsync();

            var mainWindow = GetMainWindow();
            if (mainWindow != null)
            {
                var settingsWindow = new GameProfileSettingsWindow
                {
                    DataContext = settingsViewModel,
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
            logger.LogError(ex, "Error creating new profile");
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
        try
        {
            IsPreparingWorkspace = true;
            profile.IsPreparingWorkspace = true;
            StatusMessage = $"Preparing workspace for {profile.Name}...";
            var prepareResult = await profileLauncherFacade.PrepareWorkspaceAsync(profile.ProfileId);

            if (prepareResult.Success && prepareResult.Data != null)
            {
                var profileResult = await gameProfileManager.GetProfileAsync(profile.ProfileId);
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
                        logger.LogDebug("Forced UI refresh for profile {ProfileName} at index {Index}", profile.Name, index);
                    }
                }

                StatusMessage = $"Workspace prepared for {profile.Name} at {prepareResult.Data.WorkspacePath}";
                logger.LogInformation("Prepared workspace for profile {ProfileName} at {Path}", profile.Name, prepareResult.Data.WorkspacePath);
            }
            else
            {
                var errors = string.Join(", ", prepareResult.Errors);
                StatusMessage = $"Failed to prepare workspace for {profile.Name}: {errors}";
                logger.LogWarning("Failed to prepare workspace for profile {ProfileName}: {Errors}", profile.Name, errors);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error preparing workspace for profile {ProfileName}", profile.Name);
            StatusMessage = $"Error preparing workspace for {profile.Name}";
        }
        finally
        {
            IsPreparingWorkspace = false;
            profile.IsPreparingWorkspace = false;
        }
    }

    /// <summary>
    /// Creates a desktop shortcut for the specified game profile.
    /// </summary>
    /// <param name="profile">The game profile to create a shortcut for.</param>
    [RelayCommand]
    private async Task CreateShortcut(GameProfileItemViewModel profile)
    {
        try
        {
            StatusMessage = $"Creating desktop shortcut for {profile.Name}...";

            // Get the full profile to pass to the shortcut service
            var profileResult = await gameProfileManager.GetProfileAsync(profile.ProfileId);
            if (!profileResult.Success || profileResult.Data == null)
            {
                StatusMessage = $"Failed to load profile: {string.Join(", ", profileResult.Errors)}";
                return;
            }

            var result = await shortcutService.CreateDesktopShortcutAsync(profileResult.Data);
            if (result.Success)
            {
                StatusMessage = $"Desktop shortcut created for {profile.Name}";
                logger.LogInformation("Created desktop shortcut for profile {ProfileName} at {Path}", profile.Name, result.Data);
            }
            else
            {
                StatusMessage = $"Failed to create shortcut: {string.Join(", ", result.Errors)}";
                logger.LogWarning("Failed to create shortcut for profile {ProfileName}: {Errors}", profile.Name, string.Join(", ", result.Errors));
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error creating shortcut for profile {ProfileName}", profile.Name);
            StatusMessage = $"Error creating shortcut for {profile.Name}";
        }
    }

    /// <summary>
    /// Toggles the Steam launch mode for a profile and updates the manifest on disk.
    /// </summary>
    /// <param name="profile">The profile to update.</param>
    [RelayCommand]
    private async Task ToggleSteamLaunch(GameProfileItemViewModel profile)
    {
        try
        {
            if (profile.Profile is Core.Models.GameProfile.GameProfile gameProfile && !string.IsNullOrEmpty(gameProfile.GameClient?.Id))
            {
                StatusMessage = $"Updating launch mode for {profile.Name}...";

                // Update the persisted profile
                var updateRequest = new Core.Models.GameProfile.UpdateProfileRequest
                {
                    UseSteamLaunch = profile.UseSteamLaunch,
                };
                await gameProfileManager.UpdateProfileAsync(profile.ProfileId, updateRequest);

                // Patch the manifest on disk immediately
                await steamManifestPatcher.PatchManifestAsync(gameProfile.GameClient.Id, profile.UseSteamLaunch);

                StatusMessage = $"Launch mode updated for {profile.Name}";
                logger.LogInformation("Toggled Steam launch to {UseSteam} for profile {ProfileName}", profile.UseSteamLaunch, profile.Name);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error toggling Steam launch for {ProfileName}", profile.Name);
            StatusMessage = "Error updating launch mode";

            // Revert UI if failed
            profile.UseSteamLaunch = !profile.UseSteamLaunch;
        }
    }

    /// <summary>
    /// Handles the process exited event to update profile state when a game exits.
    /// </summary>
    private void OnProcessExited(object? sender, Core.Models.Events.GameProcessExitedEventArgs e)
    {
        try
        {
            logger.LogInformation("Game process {ProcessId} exited with code {ExitCode}", e.ProcessId, e.ExitCode);

            // Find the profile that was running this process
            var profile = Profiles.OfType<GameProfileItemViewModel>().FirstOrDefault(p => p.ProcessId == e.ProcessId);
            if (profile != null)
            {
                profile.IsProcessRunning = false;
                profile.ProcessId = 0;
                logger.LogInformation("Updated profile {ProfileName} - process no longer running", profile.Name);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error handling process exit event for process {ProcessId}", e.ProcessId);
        }
    }

    /// <summary>
    /// User accepts the patch installation prompt.
    /// </summary>
    [RelayCommand]
    private void AcceptPatchPrompt()
    {
        IsShowingPatchPrompt = false;
        _promptCompletionSource?.TrySetResult(true);
    }

    /// <summary>
    /// User declines the patch installation prompt.
    /// </summary>
    [RelayCommand]
    private void DeclinePatchPrompt()
    {
        IsShowingPatchPrompt = false;
        _promptCompletionSource?.TrySetResult(false);
    }

    /// <summary>
    /// Shows a prompt to the user and waits for their response.
    /// </summary>
    private async Task<bool> ShowPromptAsync(string title, string message, GameInstallation installation)
    {
        _pendingInstallation = installation;
        _promptCompletionSource = new TaskCompletionSource<bool>();
        PromptTitle = title;
        PromptMessage = message;
        IsShowingPatchPrompt = true;

        return await _promptCompletionSource.Task;
    }
}
