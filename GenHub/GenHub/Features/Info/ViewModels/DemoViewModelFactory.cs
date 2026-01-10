using System;
using System.Threading.Tasks;
using GenHub.Core.Constants;
using GenHub.Core.Interfaces.Notifications;
using GenHub.Core.Models.Enums;
using GenHub.Core.Models.GameClients;
using GenHub.Core.Models.GameProfile;
using GenHub.Core.Models.Notifications;
using GenHub.Features.GameProfiles.ViewModels;

namespace GenHub.Features.Info.ViewModels;

/// <summary>
/// Factory for creating demo ViewModels with mock data for interactive demos.
/// </summary>
public static class DemoViewModelFactory
{
    /// <summary>
    /// Creates a demo GameProfileItemViewModel with sample data.
    /// </summary>
    /// <param name="notificationService">Optional notification service for demo actions.</param>
    /// <returns>A configured demo profile view model.</returns>
    public static GameProfileItemViewModel CreateDemoProfileCard(INotificationService? notificationService = null)
    {
        // Create a mock GameProfile
        var mockProfile = new GameProfile
        {
            Id = "demo-profile-001",
            Name = "Zero Hour Demo",
            Description = "This is a sample profile for demonstration purposes.",
            ThemeColor = "#00A3FF",
            WorkspaceStrategy = WorkspaceStrategy.SymlinkOnly,
            GameClient = new GameClient
            {
                Id = "1.104.steam.gameclient.zerohour",
                Name = "Zero Hour",
                Version = "v1.04",
                GameType = GameType.ZeroHour,
                PublisherType = PublisherTypeConstants.Steam,
            },
        };

        var vm = new GameProfileItemViewModel(
            mockProfile.Id,
            mockProfile,
            UriConstants.ZeroHourIconUri,
            $"{UriConstants.CoversBasePath}/usa-poster.png");

        // Wire up demo actions that show notifications instead of real operations
        vm.LaunchAction = async _ =>
        {
            notificationService?.Show(new NotificationMessage(
                NotificationType.Info,
                "Demo",
                "In a real profile, this would launch the game!",
                3000));
            await Task.CompletedTask;
        };

        vm.EditProfileAction = async _ =>
        {
            notificationService?.Show(new NotificationMessage(
                NotificationType.Info,
                "Demo",
                "This would open the profile editor.",
                3000));
            await Task.CompletedTask;
        };

        vm.DeleteProfileAction = async _ =>
        {
            notificationService?.Show(new NotificationMessage(
                NotificationType.Warning,
                "Demo",
                "Delete is disabled in demo mode.",
                3000));
            await Task.CompletedTask;
        };

        return vm;
    }

    /// <summary>
    /// Creates a demo UpdateNotificationViewModel with sample data.
    /// </summary>
    /// <returns>A configured demo update view model.</returns>
    public static GenHub.Features.AppUpdate.ViewModels.UpdateNotificationViewModel CreateDemoUpdateViewModel()
    {
        var mockVelopack = new GenHub.Features.Info.Services.MockVelopackUpdateManager();
        var mockSettings = new GenHub.Features.Info.Services.MockUserSettingsService();
        var mockLogger = new GenHub.Features.Info.Services.MockLogger<GenHub.Features.AppUpdate.ViewModels.UpdateNotificationViewModel>();

        var vm = new GenHub.Features.AppUpdate.ViewModels.UpdateNotificationViewModel(
            mockVelopack,
            mockLogger,
            mockSettings);

        // Manually configure the state to look like an update is available
        vm.IsChecking = false;
        vm.IsUpdateAvailable = true;
        vm.LatestVersion = "1.2.0";
        vm.StatusMessage = "New feature update available!";
        vm.ReleaseNotesUrl = "https://github.com/undead2146/GeneralsHub/releases";

        return vm;
    }

    /// <summary>
    /// Creates a demo GameSettingsViewModel with mock data.
    /// </summary>
    /// <returns>A configured demo settings view model.</returns>
    public static GameSettingsViewModel CreateDemoGameSettingsViewModel()
    {
        var mockService = new GenHub.Features.Info.Services.MockGameSettingsService();
        var mockLogger = new GenHub.Features.Info.Services.MockLogger<GameSettingsViewModel>();

        var vm = new GameSettingsViewModel(mockService, mockLogger);

        // Initialize with default mock data
        // Fire and forget is acceptable here as the mock service is synchronous
        _ = vm.InitializeForProfileAsync(null, null);

        return vm;
    }
}
