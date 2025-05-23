using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Linq;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;

using GenHub.Common.Views;
using GenHub.Core.Interfaces;
using GenHub.Core.Interfaces.AppUpdate;
using GenHub.Features.AppUpdate.ViewModels;
using GenHub.Features.AppUpdate.Views;
using GenHub.Features.GameProfiles.ViewModels;
using GenHub.Features.Downloads.ViewModels;
using GenHub.Features.Settings.ViewModels;

namespace GenHub.Common.ViewModels
{
    public partial class MainViewModel : ViewModelBase
    {
        private readonly IGameVersionServiceFacade _gameVersionService;
        private readonly ILogger<MainViewModel> _logger;
        private readonly IAppUpdateService _updateService;

        [ObservableProperty]
        private bool _isVanillaInstalled;

        [ObservableProperty]
        private bool _isZeroHourInstalled;

        [ObservableProperty]
        private int _selectedTabIndex = 0;

        [ObservableProperty]
        private string? _vanillaGamePath;

        [ObservableProperty]
        private string? _zeroHourGamePath;

        [ObservableProperty]
        private string _selectedGeneralsExecutable;

        [ObservableProperty]
        private string _selectedZeroHourExecutable;

        [ObservableProperty]
        private bool _hasUpdateAvailable;

        [ObservableProperty]
        private UpdateNotificationViewModel? _updateViewModel;

        // Tab ViewModels
        public GameProfileLauncherViewModel GameProfilesViewModel { get; }
        public DownloadsViewModel DownloadsViewModel { get; }
        public SettingsViewModel SettingsViewModel { get; }

        public MainViewModel(
            IGameVersionServiceFacade gameVersionService,
            ILogger<MainViewModel> logger,
            IAppUpdateService updateService,
            GameProfileLauncherViewModel gameProfilesViewModel,
            DownloadsViewModel downloadsViewModel,
            SettingsViewModel settingsViewModel)
        {
            _gameVersionService = gameVersionService;
            _logger = logger;
            _updateService = updateService;

            // Initialize tab ViewModels
            GameProfilesViewModel = gameProfilesViewModel;
            DownloadsViewModel = downloadsViewModel;
            SettingsViewModel = settingsViewModel;

            _selectedGeneralsExecutable = "generals.exe";
            _selectedZeroHourExecutable = "generals.exe";

            // Verify command generation
            _logger.LogInformation("MainViewModel constructor - SelectTabCommand is null: {IsNull}", SelectTabCommand == null);
            if (SelectTabCommand != null)
            {
                _logger.LogInformation("SelectTabCommand exists and CanExecute: {CanExecute}", SelectTabCommand.CanExecute(0));
            }

            // Initialize the update notification check
            Task.Run(CheckForUpdatesAsync);
        }

        // Add a method to initialize async operations
        public async Task InitializeAsync()
        {
            await Detect();
        }

        partial void OnVanillaGamePathChanged(string? value)
        {
            // Update installation status when path changes
            UpdateInstallationStatus();
        }

        partial void OnZeroHourGamePathChanged(string? value)
        {
            // Update installation status when path changes
            UpdateInstallationStatus();
        }

        [RelayCommand]
        private async Task Detect()
        {
            _logger.LogInformation("Detecting games...");

            try
            {
                // Discover versions using the game version service
                var detectedVersions = (await _gameVersionService.DiscoverVersionsAsync()).ToList();
                _logger.LogInformation("Found {Count} game versions during discovery.", detectedVersions.Count);

                // Reset paths before attempting to set them
                VanillaGamePath = null;
                ZeroHourGamePath = null;

                // Find paths from detected versions
                var vanillaVersion = detectedVersions.FirstOrDefault(v =>
                    (v.GameType?.Equals("Generals", StringComparison.OrdinalIgnoreCase) == true ||
                     v.GameType?.Equals("Command & Conquer Generals", StringComparison.OrdinalIgnoreCase) == true) &&
                    !string.IsNullOrEmpty(v.InstallPath));

                if (vanillaVersion != null)
                {
                    VanillaGamePath = vanillaVersion.InstallPath;
                    _logger.LogInformation("Found Vanilla Generals at: {Path}", VanillaGamePath);
                }

                var zeroHourVersion = detectedVersions.FirstOrDefault(v =>
                    (v.GameType?.Equals("Zero Hour", StringComparison.OrdinalIgnoreCase) == true ||
                     v.GameType?.Equals("Command & Conquer Generals Zero Hour", StringComparison.OrdinalIgnoreCase) == true) &&
                    !string.IsNullOrEmpty(v.InstallPath));

                if (zeroHourVersion != null)
                {
                    ZeroHourGamePath = zeroHourVersion.InstallPath;
                    _logger.LogInformation("Found Zero Hour at: {Path}", ZeroHourGamePath);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during game detection.");
            }
            finally
            {
                // Update installation status regardless of detection outcome
                UpdateInstallationStatus();
                _logger.LogDebug($"Vanilla Game Path: {VanillaGamePath ?? "Not found"}");
                _logger.LogDebug($"Zero Hour Game Path: {ZeroHourGamePath ?? "Not found"}");
            }
        }

        [RelayCommand]
        private void SelectTab(int tabIndex)
        {
            _logger.LogInformation("Selecting tab {TabIndex}", tabIndex);
            
            var oldIndex = SelectedTabIndex;
            SelectedTabIndex = tabIndex;
            
            _logger.LogInformation("Tab changed from {OldIndex} to {NewIndex}", oldIndex, SelectedTabIndex);
            
            // Force property change notification to ensure UI updates
            OnPropertyChanged(nameof(SelectedTabIndex));
        }

        [RelayCommand]
        private async Task ShowUpdateNotificationAsync()
        {
            try
            {
                if (UpdateViewModel == null)
                {
                    // Use the service provider to get the view model with all required dependencies
                    UpdateViewModel = AppLocator.Services?.GetService<UpdateNotificationViewModel>();

                    if (UpdateViewModel == null)
                    {
                        _logger.LogError("Could not resolve UpdateNotificationViewModel from service container");
                        return;
                    }

                    // Initialize the view model
                    await UpdateViewModel.InitializeAsync();
                }

                var dialog = new UpdateNotificationWindow
                {
                    DataContext = UpdateViewModel
                };

                var mainWindow = GetMainWindow();
                if (mainWindow != null)
                {
                    await dialog.ShowDialog(mainWindow);
                }
                else
                {
                    _logger.LogWarning("Cannot show update notification - main window not found");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error showing update notification: {Message}", ex.Message);
            }
        }

        private async Task CheckForUpdatesAsync()
        {
            try
            {
                var result = await _updateService.CheckForUpdatesAsync();
                HasUpdateAvailable = result.IsUpdateAvailable;

                if (HasUpdateAvailable)
                {
                    _logger.LogInformation("Updates available: {Version}", result.LatestRelease?.Version);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking for updates");
            }
        }

        private Window? GetMainWindow()
        {
            if (Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
            {
                return desktop.MainWindow;
            }
            return null;
        }

        private void UpdateInstallationStatus()
        {
            IsVanillaInstalled = !string.IsNullOrEmpty(VanillaGamePath) && Directory.Exists(VanillaGamePath);
            IsZeroHourInstalled = !string.IsNullOrEmpty(ZeroHourGamePath) && Directory.Exists(ZeroHourGamePath);
            _logger.LogDebug("Installation status updated: Vanilla Installed = {IsVanillaInstalled}, Zero Hour Installed = {IsZeroHourInstalled}", IsVanillaInstalled, IsZeroHourInstalled);
        }
    }
}
