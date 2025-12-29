using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GenHub.Core.Constants;
using GenHub.Core.Interfaces.Common;
using GenHub.Core.Interfaces.GameInstallations;
using GenHub.Core.Interfaces.GameProfiles;
using GenHub.Core.Interfaces.Notifications;
using GenHub.Core.Models.Enums;
using GenHub.Core.Models.GameProfile;
using GenHub.Core.Models.Notifications;
using GenHub.Features.AppUpdate.Interfaces;
using GenHub.Features.Downloads.ViewModels;
using GenHub.Features.GameProfiles.Services;
using GenHub.Features.GameProfiles.ViewModels;
using GenHub.Features.Notifications.ViewModels;
using GenHub.Features.Settings.ViewModels;
using GenHub.Features.Tools.ViewModels;
using Microsoft.Extensions.Logging;

namespace GenHub.Common.ViewModels;

/// <summary>
/// Main view model for the application.
/// </summary>
public partial class MainViewModel : ObservableObject, IDisposable
{
    private readonly ILogger<MainViewModel>? _logger;
    private readonly IGameInstallationDetectionOrchestrator _gameInstallationDetectionOrchestrator;
    private readonly IConfigurationProviderService _configurationProvider;
    private readonly IUserSettingsService _userSettingsService;
    private readonly IProfileEditorFacade _profileEditorFacade;
    private readonly IVelopackUpdateManager _velopackUpdateManager;
    private readonly ProfileResourceService _profileResourceService;
    private readonly INotificationService _notificationService;
    private readonly CancellationTokenSource _initializationCts = new();

    [ObservableProperty]
    private NavigationTab _selectedTab = NavigationTab.GameProfiles;



    /// <summary>
    /// Initializes a new instance of the <see cref="MainViewModel"/> class.
    /// </summary>
    /// <param name="gameProfilesViewModel">Game profiles view model.</param>
    /// <param name="downloadsViewModel">Downloads view model.</param>
    /// <param name="toolsViewModel">Tools view model.</param>
    /// <param name="settingsViewModel">Settings view model.</param>
    /// <param name="notificationManager">Notification manager view model.</param>
    /// <param name="gameInstallationDetectionOrchestrator">Game installation orchestrator.</param>
    /// <param name="configurationProvider">Configuration provider service.</param>
    /// <param name="userSettingsService">User settings service for persistence operations.</param>
    /// <param name="profileEditorFacade">Profile editor facade for automatic profile creation.</param>
    /// <param name="velopackUpdateManager">The Velopack update manager for checking updates.</param>
    /// <param name="profileResourceService">Service for accessing profile resources.</param>
    /// <param name="notificationService">Service for showing notifications.</param>
    /// <param name="logger">Logger instance.</param>
    public MainViewModel(
        GameProfileLauncherViewModel gameProfilesViewModel,
        DownloadsViewModel downloadsViewModel,
        ToolsViewModel toolsViewModel,
        SettingsViewModel settingsViewModel,
        NotificationManagerViewModel notificationManager,
        IGameInstallationDetectionOrchestrator gameInstallationDetectionOrchestrator,
        IConfigurationProviderService configurationProvider,
        IUserSettingsService userSettingsService,
        IProfileEditorFacade profileEditorFacade,
        IVelopackUpdateManager velopackUpdateManager,
        ProfileResourceService profileResourceService,
        INotificationService notificationService,
        ILogger<MainViewModel>? logger = null)
    {
        GameProfilesViewModel = gameProfilesViewModel;
        DownloadsViewModel = downloadsViewModel;
        ToolsViewModel = toolsViewModel;
        SettingsViewModel = settingsViewModel;
        NotificationManager = notificationManager;
        _gameInstallationDetectionOrchestrator = gameInstallationDetectionOrchestrator;
        _configurationProvider = configurationProvider;
        _userSettingsService = userSettingsService;
        _profileEditorFacade = profileEditorFacade ?? throw new ArgumentNullException(nameof(profileEditorFacade));
        _velopackUpdateManager = velopackUpdateManager ?? throw new ArgumentNullException(nameof(velopackUpdateManager));
        _profileResourceService = profileResourceService ?? throw new ArgumentNullException(nameof(profileResourceService));
        _notificationService = notificationService ?? throw new ArgumentNullException(nameof(notificationService));
        _logger = logger;

        // Load initial settings using unified configuration
        try
        {
            _selectedTab = _configurationProvider.GetLastSelectedTab();
            if (_selectedTab == NavigationTab.Tools)
            {
                _selectedTab = NavigationTab.GameProfiles;
            }

            _logger?.LogDebug("Initial settings loaded, selected tab: {Tab}", _selectedTab);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to load initial settings");
            _selectedTab = NavigationTab.GameProfiles;
        }

        // Tab change handled by ObservableProperty partial method
    }

    /// <summary>
    /// Gets the game profiles view model.
    /// </summary>
    public GameProfileLauncherViewModel GameProfilesViewModel { get; }

    /// <summary>
    /// Gets the downloads view model.
    /// </summary>
    public DownloadsViewModel DownloadsViewModel { get; }

    /// <summary>
    /// Gets the tools view model.
    /// </summary>
    public ToolsViewModel ToolsViewModel { get; }

    /// <summary>
    /// Gets the settings view model.
    /// </summary>
    public SettingsViewModel SettingsViewModel { get; }

    /// <summary>
    /// Gets the notification manager view model.
    /// </summary>
    public NotificationManagerViewModel NotificationManager { get; }

    /// <summary>
    /// Gets the collection of detected game installations.
    /// </summary>
    public ObservableCollection<string> GameInstallations { get; } = [];

    /// <summary>
    /// Gets the available navigation tabs.
    /// </summary>
    public NavigationTab[] AvailableTabs { get; } =
    [
        NavigationTab.GameProfiles,
        NavigationTab.Downloads,
        NavigationTab.Settings,
    ];

    /// <summary>
    /// Gets the current tab's ViewModel for ContentControl binding.
    /// </summary>
    public object CurrentTabViewModel => SelectedTab switch
    {
        NavigationTab.GameProfiles => GameProfilesViewModel,
        NavigationTab.Downloads => DownloadsViewModel,
        NavigationTab.Tools => ToolsViewModel,
        NavigationTab.Settings => SettingsViewModel,
        _ => GameProfilesViewModel,
    };

    /// <summary>
    /// Gets the display name for a navigation tab.
    /// </summary>
    /// <param name="tab">The navigation tab.</param>
    /// <returns>The display name.</returns>
    public static string GetTabDisplayName(NavigationTab tab) => tab switch
    {
        NavigationTab.GameProfiles => "Game Profiles",
        NavigationTab.Downloads => "Downloads",
        NavigationTab.Tools => "Tools",
        NavigationTab.Settings => "Settings",
        _ => tab.ToString(),
    };

    /// <summary>
    /// Selects the specified navigation tab.
    /// </summary>
    /// <param name="tab">The navigation tab to select.</param>
    [RelayCommand]
    public void SelectTab(NavigationTab tab)
    {
        SelectedTab = tab;
    }



    /// <summary>
    /// Performs asynchronous initialization for the shell and all tabs.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    public async Task InitializeAsync()
    {
        await GameProfilesViewModel.InitializeAsync();
        await DownloadsViewModel.InitializeAsync();
        await ToolsViewModel.InitializeAsync();
        _logger?.LogInformation("MainViewModel initialized");

        // Start background check with cancellation support
        _ = CheckForUpdatesInBackgroundAsync(_initializationCts.Token);

        await Task.CompletedTask;
    }

    /// <summary>
    /// Scans for game installations and automatically creates profiles.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [RelayCommand]
    public async Task ScanAndCreateProfilesAsync()
    {
        _logger?.LogInformation("Starting automatic profile creation from game installations");

        try
        {
            // First scan for installations
            var scanResult = await _gameInstallationDetectionOrchestrator.DetectAllInstallationsAsync();

            if (!scanResult.Success)
            {
                _logger?.LogWarning("Game installation scan failed: {Errors}", string.Join(", ", scanResult.Errors));
                return;
            }

            if (scanResult.Items.Count == 0)
            {
                _logger?.LogInformation("No game installations found");
                return;
            }

            _logger?.LogInformation("Found {Count} game installations, creating profiles", scanResult.Items.Count);

            int createdCount = 0;
            int failedCount = 0;

            foreach (var installation in scanResult.Items)
            {
                if (installation == null) continue;

                try
                {
                    // Skip installations that don't have available game clients
                    if (installation.AvailableGameClients.Count == 0)
                    {
                        _logger?.LogWarning("Skipping installation {InstallationId} - no available GameClients found", installation.Id);
                        continue;
                    }

                    // Create profiles for ALL available game clients (standard, GeneralsOnline, SuperHackers, etc.)
                    foreach (var gameClient in installation.AvailableGameClients)
                    {
                        if (!gameClient.IsValid)
                        {
                            _logger?.LogWarning("Skipping GameClient {ClientId} in installation {InstallationId} - not valid", gameClient.Id, installation.Id);
                            continue;
                        }

                        var gameClientId = gameClient.Id;

                        // Determine assets based on game type using ProfileResourceService
                        var gameTypeStr = gameClient.GameType.ToString();
                        var iconPath = _profileResourceService.GetDefaultIconPath(gameTypeStr);
                        var coverPath = _profileResourceService.GetDefaultCoverPath(gameTypeStr);

                        // Create a profile request for this game client
                        var createRequest = new CreateProfileRequest
                        {
                            Name = $"{installation.InstallationType} {gameClient.Name}",
                            GameInstallationId = installation.Id,
                            GameClientId = gameClientId,
                            Description = $"Auto-created profile for {gameClient.Name} in {installation.InstallationType} installation",
                            PreferredStrategy = WorkspaceStrategy.HybridCopySymlink,
                            IconPath = iconPath,
                            CoverPath = coverPath,
                        };

                        var profileResult = await _profileEditorFacade.CreateProfileWithWorkspaceAsync(createRequest);

                        if (profileResult.Success)
                        {
                            createdCount++;
                            _logger?.LogInformation(
                                "Created profile '{ProfileName}' for {GameClientName}",
                                profileResult.Data?.Name,
                                gameClient.Name);
                        }
                        else
                        {
                            // Profile might already exist - don't count as failure
                            var errors = string.Join(", ", profileResult.Errors);
                            if (errors.Contains("already exists", StringComparison.OrdinalIgnoreCase))
                            {
                                _logger?.LogDebug("Profile already exists for {GameClientName}", gameClient.Name);
                            }
                            else
                            {
                                failedCount++;
                                _logger?.LogWarning(
                                    "Failed to create profile for {GameClientName}: {Errors}",
                                    gameClient.Name,
                                    errors);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    failedCount++;
                    _logger?.LogError(ex, "Error creating profile for installation {InstallationId}", installation.Id);
                }
            }

            _logger?.LogInformation(
                "Profile creation complete: {Created} created, {Failed} failed",
                createdCount,
                failedCount);

            // Refresh the game profiles view model to show new profiles
            await GameProfilesViewModel.InitializeAsync();
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error occurred during automatic profile creation");
        }
    }

    /// <summary>
    /// Disposes of managed resources.
    /// </summary>
    public void Dispose()
    {
        _initializationCts?.Cancel();
        _initializationCts?.Dispose();
        GC.SuppressFinalize(this);
    }

    private static Window? GetMainWindow()
    {
        return Avalonia.Application.Current?.ApplicationLifetime
            is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime dt
            ? dt.MainWindow
            : null;
    }

    /// <summary>
    /// Checks for available updates using Velopack.
    /// </summary>
    private async Task CheckForUpdatesAsync(CancellationToken cancellationToken = default)
    {
        _logger?.LogDebug("Starting background update check");

        try
        {
            var settings = _userSettingsService.Get();

            // Push settings to update manager (important context for other components)
            if (settings.SubscribedPrNumber.HasValue)
            {
                _velopackUpdateManager.SubscribedPrNumber = settings.SubscribedPrNumber;
            }

            // Check if subscribed to a specific branch
            if (!string.IsNullOrEmpty(settings.SubscribedBranch))
            {
                _logger?.LogDebug("User subscribed to branch '{Branch}', checking for artifact updates", settings.SubscribedBranch);
                _velopackUpdateManager.SubscribedBranch = settings.SubscribedBranch;

                // Ensure PR number is cleared to avoid ambiguity
                _velopackUpdateManager.SubscribedPrNumber = null;

                var artifactUpdate = await _velopackUpdateManager.CheckForArtifactUpdatesAsync(cancellationToken);

                if (artifactUpdate != null)
                {
                    var newVersionBase = artifactUpdate.Version.Split('+')[0];
                    var dismissedVersionBase = settings.DismissedUpdateVersion?.Split('+')[0];

                    if (string.IsNullOrEmpty(dismissedVersionBase) ||
                        !string.Equals(newVersionBase, dismissedVersionBase, StringComparison.OrdinalIgnoreCase))
                    {
                        _logger?.LogInformation("Branch '{Branch}' artifact update available: {Version}", settings.SubscribedBranch, newVersionBase);

                        await Dispatcher.UIThread.InvokeAsync(() =>
                        {
                            _notificationService.Show(new NotificationMessage(
                                NotificationType.Info,
                                "Update Available",
                                $"A new version ({newVersionBase}) is available on branch '{settings.SubscribedBranch}'.",
                                null,
                                "View Updates",
                                () => { SettingsViewModel.OpenUpdateWindowCommand.Execute(null); }));
                        });
                        return;
                    }
                    else
                    {
                        _logger?.LogDebug("Branch '{Branch}' artifact update {Version} was dismissed", settings.SubscribedBranch, newVersionBase);
                        return;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Exception in CheckForUpdatesAsync");
        }
    }

    private async Task CheckForUpdatesInBackgroundAsync(CancellationToken ct)
    {
        try
        {
            await CheckForUpdatesAsync(ct);
        }
        catch (OperationCanceledException)
        {
            // Expected on cancellation
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Unhandled exception in background update check");
        }
    }

    private void SaveSelectedTab(NavigationTab selectedTab)
    {
        try
        {
            _userSettingsService.Update(settings =>
            {
                settings.LastSelectedTab = selectedTab;
            });

            _ = _userSettingsService.SaveAsync();
            _logger?.LogDebug("Updated last selected tab to: {Tab}", selectedTab);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to update selected tab setting");
        }
    }

    partial void OnSelectedTabChanged(NavigationTab value)
    {
        OnPropertyChanged(nameof(CurrentTabViewModel));

        // Notify SettingsViewModel when it becomes visible/invisible
        SettingsViewModel.IsViewVisible = value == NavigationTab.Settings;

        // Refresh Downloads tab when it becomes visible
        if (value == NavigationTab.Downloads)
        {
            _ = DownloadsViewModel.OnTabActivatedAsync();
        }

        SaveSelectedTab(value);
    }
}
