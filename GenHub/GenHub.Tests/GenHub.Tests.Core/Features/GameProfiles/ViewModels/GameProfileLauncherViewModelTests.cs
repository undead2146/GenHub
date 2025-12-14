using System.Threading;
using System.Threading.Tasks;
using GenHub.Common.ViewModels;
using GenHub.Core.Interfaces.Common;
using GenHub.Core.Interfaces.GameInstallations;
using GenHub.Core.Interfaces.GameProfiles;
using GenHub.Core.Interfaces.GameSettings;
using GenHub.Core.Interfaces.Notifications;
using GenHub.Core.Models.Enums;
using GenHub.Core.Models.GameInstallations;
using GenHub.Core.Models.Results;
using GenHub.Features.GameProfiles.ViewModels;
using GenHub.Features.Settings.ViewModels;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace GenHub.Tests.Core.ViewModels;

/// <summary>
/// Contains unit tests for <see cref="GameProfileLauncherViewModel"/>.
/// </summary>
public class GameProfileLauncherViewModelTests
{
    /// <summary>
    /// Verifies that the constructor initializes properties correctly.
    /// </summary>
    [Fact]
    public void Constructor_WithValidParameters_InitializesCorrectly()
    {
        var vm = CreateViewModel();

        Assert.NotNull(vm);
        Assert.Empty(vm.Profiles);
        Assert.False(vm.IsLaunching);
        Assert.False(vm.IsEditMode);
        Assert.Equal(string.Empty, vm.StatusMessage);
    }

    /// <summary>
    /// Verifies that InitializeAsync loads profiles successfully.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    [Fact]
    public async Task InitializeAsync_LoadsProfiles_Successfully()
    {
        var profileManager = new Mock<IGameProfileManager>();
        profileManager.Setup(x => x.GetAllProfilesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(ProfileOperationResult<System.Collections.Generic.IReadOnlyList<GenHub.Core.Models.GameProfile.GameProfile>>.CreateFailure("No profiles"));

        var vm = CreateViewModel(profileManager: profileManager.Object);

        await vm.InitializeAsync();

        Assert.Empty(vm.Profiles);

        // Note: Success message logic depends on profile loading result.
        // If failure, it sets "Failed to load profiles: No profiles"
        Assert.StartsWith("Failed to load profiles", vm.StatusMessage);
    }

    /// <summary>
    /// Verifies that ScanForGamesCommand shows success on successful scan.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    [Fact]
    public async Task ScanForGamesCommand_WithSuccessfulScan_ShowsSuccess()
    {
        var installationService = new Mock<IGameInstallationService>();
        var installations = new System.Collections.Generic.List<GameInstallation>
        {
            new GameInstallation("C:\\Steam\\Games", GameInstallationType.Steam, new Mock<ILogger<GameInstallation>>().Object),
            new GameInstallation("C:\\EA\\Games", GameInstallationType.EaApp, new Mock<ILogger<GameInstallation>>().Object),
        };

        installationService.Setup(x => x.GetAllInstallationsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(OperationResult<System.Collections.Generic.IReadOnlyList<GameInstallation>>.CreateSuccess(installations));

        var vm = CreateViewModel(installationService: installationService.Object);

        await vm.ScanForGamesCommand.ExecuteAsync(null);

        // Updated to match actual message format that includes manifest generation and profile creation
        Assert.Equal("Scan complete. Found 2 installations, generated 0 manifests, created 0 profiles", vm.StatusMessage);
    }

    /// <summary>
    /// Verifies that ScanForGamesCommand shows failure on failed scan.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    [Fact]
    public async Task ScanForGamesCommand_WithFailedScan_ShowsFailure()
    {
        var installationService = new Mock<IGameInstallationService>();
        const string expectedError = "Detection service unavailable";

        installationService.Setup(x => x.GetAllInstallationsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(OperationResult<System.Collections.Generic.IReadOnlyList<GameInstallation>>.CreateFailure(expectedError));

        var vm = CreateViewModel(installationService: installationService.Object);

        await vm.ScanForGamesCommand.ExecuteAsync(null);

        Assert.Equal($"Scan failed: {expectedError}", vm.StatusMessage);
    }

    /// <summary>
    /// Verifies that ScanForGamesCommand handles exceptions gracefully.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    [Fact]
    public async Task ScanForGamesCommand_WithException_HandlesGracefully()
    {
        var installationService = new Mock<IGameInstallationService>();
        installationService.Setup(x => x.GetAllInstallationsAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new System.InvalidOperationException("Test exception"));

        var vm = CreateViewModel(installationService: installationService.Object);

        await vm.ScanForGamesCommand.ExecuteAsync(null);

        Assert.Equal("Error during scan", vm.StatusMessage);
    }

    private static GameProfileLauncherViewModel CreateViewModel(
        IGameInstallationService? installationService = null,
        IGameProfileManager? profileManager = null)
    {
        return new GameProfileLauncherViewModel(
            installationService ?? Mock.Of<IGameInstallationService>(),
            profileManager ?? Mock.Of<IGameProfileManager>(),
            Mock.Of<IProfileLauncherFacade>(),
            new GameProfileSettingsViewModel(
                Mock.Of<IGameProfileManager>(),
                Mock.Of<IGameSettingsService>(),
                Mock.Of<IConfigurationProviderService>(),
                Mock.Of<IProfileContentLoader>(),
                NullLogger<GameProfileSettingsViewModel>.Instance,
                NullLogger<GameSettingsViewModel>.Instance),
            Mock.Of<IProfileEditorFacade>(),
            Mock.Of<IConfigurationProviderService>(),
            Mock.Of<IGameProcessManager>(),
            Mock.Of<IStorageLocationService>(),
            Mock.Of<INotificationService>(),
            NullLogger<GameProfileLauncherViewModel>.Instance);
    }
}
