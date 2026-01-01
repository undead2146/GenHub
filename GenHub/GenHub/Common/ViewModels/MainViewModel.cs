using System;
using System.Collections.ObjectModel;
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
using GenHub.Features.AppUpdate.Views;
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

            // 1. Check for standard GitHub releases (Default)
            if (string.IsNullOrEmpty(settings.SubscribedBranch))
            {
                var updateInfo = await _velopackUpdateManager.CheckForUpdatesAsync(cancellationToken);
                if (updateInfo != null)
                {
                    _logger?.LogInformation("GitHub release update available: {Version}", updateInfo.TargetFullRelease.Version);
                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        _notificationService.Show(new NotificationMessage(
                            NotificationType.Info,
                            "Update Available",
                            $"A new version ({updateInfo.TargetFullRelease.Version}) is available.",
                            null, // Persistent
                            "View Updates",
                            () => { SettingsViewModel.OpenUpdateWindowCommand.Execute(null); }));
                    });
                    return;
                }
            }
            else
            {
                // 2. Check for Subscribed Branch Artifacts
                _logger?.LogDebug("User subscribed to branch '{Branch}', checking for artifact updates", settings.SubscribedBranch);
                _velopackUpdateManager.SubscribedBranch = settings.SubscribedBranch;
                _velopackUpdateManager.SubscribedPrNumber = null; // Clear PR to avoid ambiguity

                var artifactUpdate = await _velopackUpdateManager.CheckForArtifactUpdatesAsync(cancellationToken);

                if (artifactUpdate != null)
                {
                    var newVersionBase = artifactUpdate.Version.Split('+')[0];

                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        _notificationService.Show(new NotificationMessage(
                            NotificationType.Info,
                            "Branch Update Available",
                            $"A new build ({newVersionBase}) is available on branch '{settings.SubscribedBranch}'.",
                            null, // Persistent
                            "View Updates",
                            () => { SettingsViewModel.OpenUpdateWindowCommand.Execute(null); }));
                    });
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

        // Refresh Tabs when they become visible
        if (value == NavigationTab.GameProfiles)
        {
            GameProfilesViewModel.OnTabActivated();
        }
        else if (value == NavigationTab.Downloads)
        {
            _ = DownloadsViewModel.OnTabActivatedAsync();
        }

        SaveSelectedTab(value);
    }
}
