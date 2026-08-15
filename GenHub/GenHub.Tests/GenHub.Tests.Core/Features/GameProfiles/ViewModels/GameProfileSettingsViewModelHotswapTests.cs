using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GenHub.Core.Interfaces.Common;
using GenHub.Core.Interfaces.GameProfiles;
using GenHub.Core.Interfaces.GameSettings;
using GenHub.Core.Interfaces.Launching;
using GenHub.Core.Interfaces.Manifest;
using GenHub.Core.Interfaces.UserData;
using GenHub.Core.Models.GameClients;
using GenHub.Core.Models.GameProfile;
using GenHub.Core.Models.Launching;
using GenHub.Core.Models.Manifest;
using GenHub.Core.Models.Results;
using GenHub.Features.GameProfiles.ViewModels;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;
using ContentType = GenHub.Core.Models.Enums.ContentType;
using CoreContentDisplayItem = GenHub.Core.Models.Content.ContentDisplayItem;
using GameType = GenHub.Core.Models.Enums.GameType;

namespace GenHub.Tests.Core.Features.GameProfiles.ViewModels;

/// <summary>
/// Unit tests for <see cref="GameProfileSettingsViewModel"/> runtime hotswap mode behavior.
/// </summary>
public class GameProfileSettingsViewModelHotswapTests
{
    private readonly Mock<IGameProfileManager> _gameProfileManagerMock = new();
    private readonly Mock<IGameSettingsService> _gameSettingsServiceMock = new();
    private readonly Mock<IConfigurationProviderService> _configProviderMock = new();
    private readonly Mock<IProfileContentLoader> _contentLoaderMock = new();
    private readonly Mock<IContentManifestPool> _manifestPoolMock = new();
    private readonly Mock<IProfileContentLinker> _profileContentLinkerMock = new();
    private readonly Mock<ILaunchRegistry> _launchRegistryMock = new();
    private readonly GameProfileSettingsViewModel _viewModel;

    /// <summary>
    /// Initializes a new instance of the <see cref="GameProfileSettingsViewModelHotswapTests"/> class.
    /// </summary>
    public GameProfileSettingsViewModelHotswapTests()
    {
        _viewModel = new GameProfileSettingsViewModel(
            _gameProfileManagerMock.Object,
            _gameSettingsServiceMock.Object,
            _configProviderMock.Object,
            _contentLoaderMock.Object,
            null,
            null,
            _manifestPoolMock.Object,
            null,
            null,
            null,
            null,
            NullLogger<GameProfileSettingsViewModel>.Instance,
            NullLogger<GameSettingsViewModel>.Instance,
            _profileContentLinkerMock.Object,
            _launchRegistryMock.Object);
    }

    /// <summary>
    /// Verifies that initializing for a running profile activates Hotswap Mode and locks non-hotswappable content.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task InitializeForProfileAsync_WhenProfileIsRunning_SetsIsHotswapModeTrueAndLocksNonHotswappableContent()
    {
        // Arrange
        const string profileId = "profile-live-1";
        const string installId = "1.108.steam.gameinstallation.zh";
        const string mapId = "1.0.0.map.desert";
        const string modId = "1.0.0.mod.shockwave";

        var profile = new GameProfile
        {
            Id = profileId,
            Name = "Live Game Profile",
            EnabledContentIds = [installId, mapId, modId],
            GameClient = new GameClient
            {
                Id = "client-zh",
                Name = "Zero Hour",
                GameType = GameType.ZeroHour,
            },
        };

        _gameProfileManagerMock.Setup(m => m.GetProfileAsync(profileId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ProfileOperationResult<GameProfile>.CreateSuccess(profile));
        _gameProfileManagerMock.Setup(m => m.UpdateProfileAsync(profileId, It.IsAny<UpdateProfileRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ProfileOperationResult<GameProfile>.CreateSuccess(profile));

        _launchRegistryMock.Setup(l => l.GetAllActiveLaunchesAsync())
            .ReturnsAsync([CreateActiveLaunch(profileId)]);

        var enabledItems = new ObservableCollection<CoreContentDisplayItem>
        {
            new()
            {
                Id = installId,
                ManifestId = installId,
                DisplayName = "Command & Conquer: Zero Hour",
                ContentType = ContentType.GameInstallation,
                GameType = GameType.ZeroHour,
            },
            new()
            {
                Id = mapId,
                ManifestId = mapId,
                DisplayName = "Tournament Desert",
                ContentType = ContentType.Map,
                GameType = GameType.ZeroHour,
            },
            new()
            {
                Id = modId,
                ManifestId = modId,
                DisplayName = "ShockWave Mod",
                ContentType = ContentType.Mod,
                GameType = GameType.ZeroHour,
            },
        };

        _contentLoaderMock.Setup(c => c.LoadEnabledContentForProfileAsync(profile))
            .ReturnsAsync(enabledItems);
        _contentLoaderMock.Setup(c => c.LoadAvailableGameInstallationsAsync())
            .ReturnsAsync([]);
        _contentLoaderMock.Setup(c => c.LoadAvailableContentAsync(It.IsAny<ContentType>(), It.IsAny<ObservableCollection<CoreContentDisplayItem>>(), It.IsAny<IReadOnlyList<string>>()))
            .ReturnsAsync([]);

        _manifestPoolMock.Setup(m => m.GetAllManifestsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(OperationResult<IEnumerable<ContentManifest>>.CreateSuccess([]));

        // Act
        await _viewModel.InitializeForProfileAsync(profileId);

        // Assert
        Assert.True(_viewModel.IsHotswapMode);
        Assert.False(_viewModel.CanEditImmutableMetadata);

        var mapItem = _viewModel.EnabledContent.FirstOrDefault(c => c.ManifestId.Value == mapId);
        Assert.NotNull(mapItem);
        Assert.False(mapItem.IsLocked);
        Assert.True(mapItem.CanToggle);

        var modItem = _viewModel.EnabledContent.FirstOrDefault(c => c.ManifestId.Value == modId);
        Assert.NotNull(modItem);
        Assert.True(modItem.IsLocked);
        Assert.False(modItem.CanToggle);
    }

    /// <summary>
    /// Verifies that initializing for an idle profile sets Hotswap Mode to false.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task InitializeForProfileAsync_WhenProfileIsNotRunning_SetsIsHotswapModeFalse()
    {
        // Arrange
        const string profileId = "profile-idle-1";
        const string installId = "1.108.steam.gameinstallation.zh";
        const string modId = "1.0.0.mod.shockwave";

        var profile = new GameProfile
        {
            Id = profileId,
            Name = "Idle Profile",
            EnabledContentIds = [installId, modId],
            GameClient = new GameClient
            {
                Id = "client-zh",
                Name = "Zero Hour",
                GameType = GameType.ZeroHour,
            },
        };

        _gameProfileManagerMock.Setup(m => m.GetProfileAsync(profileId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ProfileOperationResult<GameProfile>.CreateSuccess(profile));
        _gameProfileManagerMock.Setup(m => m.UpdateProfileAsync(profileId, It.IsAny<UpdateProfileRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ProfileOperationResult<GameProfile>.CreateSuccess(profile));

        _launchRegistryMock.Setup(l => l.GetAllActiveLaunchesAsync())
            .ReturnsAsync(new List<GameLaunchInfo>());

        var enabledItems = new ObservableCollection<CoreContentDisplayItem>
        {
            new()
            {
                Id = installId,
                ManifestId = installId,
                DisplayName = "Command & Conquer: Zero Hour",
                ContentType = ContentType.GameInstallation,
                GameType = GameType.ZeroHour,
            },
            new()
            {
                Id = modId,
                ManifestId = modId,
                DisplayName = "ShockWave Mod",
                ContentType = ContentType.Mod,
                GameType = GameType.ZeroHour,
            },
        };

        _contentLoaderMock.Setup(c => c.LoadEnabledContentForProfileAsync(profile))
            .ReturnsAsync(enabledItems);
        _contentLoaderMock.Setup(c => c.LoadAvailableGameInstallationsAsync())
            .ReturnsAsync([]);
        _contentLoaderMock.Setup(c => c.LoadAvailableContentAsync(It.IsAny<ContentType>(), It.IsAny<ObservableCollection<CoreContentDisplayItem>>(), It.IsAny<IReadOnlyList<string>>()))
            .ReturnsAsync([]);

        _manifestPoolMock.Setup(m => m.GetAllManifestsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(OperationResult<IEnumerable<ContentManifest>>.CreateSuccess([]));

        // Act
        await _viewModel.InitializeForProfileAsync(profileId);

        // Assert
        Assert.False(_viewModel.IsHotswapMode);
        Assert.True(_viewModel.CanEditImmutableMetadata);

        var modItem = _viewModel.EnabledContent.FirstOrDefault(c => c.ManifestId.Value == modId);
        Assert.NotNull(modItem);
        Assert.False(modItem.IsLocked);
        Assert.True(modItem.CanToggle);
    }

    /// <summary>
    /// Verifies that SaveAsync in Hotswap Mode invokes UpdateProfileUserDataAsync on the profile content linker.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task SaveAsync_WhenInHotswapMode_CallsUpdateProfileUserDataAsync()
    {
        // Arrange
        const string profileId = "profile-live-2";
        const string installId = "1.108.steam.gameinstallation.zh";
        const string mapId = "1.0.0.map.desert";

        var profile = new GameProfile
        {
            Id = profileId,
            Name = "Live Profile",
            EnabledContentIds = [installId, mapId],
            GameClient = new GameClient
            {
                Id = "client-zh",
                Name = "Zero Hour",
                GameType = GameType.ZeroHour,
            },
        };

        _gameProfileManagerMock.Setup(m => m.GetProfileAsync(profileId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ProfileOperationResult<GameProfile>.CreateSuccess(profile));
        _gameProfileManagerMock.Setup(m => m.UpdateProfileAsync(profileId, It.IsAny<UpdateProfileRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ProfileOperationResult<GameProfile>.CreateSuccess(profile));

        _launchRegistryMock.Setup(l => l.GetAllActiveLaunchesAsync())
            .ReturnsAsync([CreateActiveLaunch(profileId)]);

        var enabledItems = new ObservableCollection<CoreContentDisplayItem>
        {
            new()
            {
                Id = installId,
                ManifestId = installId,
                DisplayName = "Command & Conquer: Zero Hour",
                ContentType = ContentType.GameInstallation,
                GameType = GameType.ZeroHour,
            },
            new()
            {
                Id = mapId,
                ManifestId = mapId,
                DisplayName = "Tournament Desert",
                ContentType = ContentType.Map,
                GameType = GameType.ZeroHour,
            },
        };

        _contentLoaderMock.Setup(c => c.LoadEnabledContentForProfileAsync(profile))
            .ReturnsAsync(enabledItems);
        _contentLoaderMock.Setup(c => c.LoadAvailableGameInstallationsAsync())
            .ReturnsAsync([]);
        _contentLoaderMock.Setup(c => c.LoadAvailableContentAsync(It.IsAny<ContentType>(), It.IsAny<ObservableCollection<CoreContentDisplayItem>>(), It.IsAny<IReadOnlyList<string>>()))
            .ReturnsAsync([]);
        _manifestPoolMock.Setup(m => m.GetAllManifestsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(OperationResult<IEnumerable<ContentManifest>>.CreateSuccess([]));

        var mapManifest = new ContentManifest
        {
            Id = ManifestId.Create(mapId),
            Name = "Tournament Desert",
            ContentType = ContentType.Map,
        };
        var installManifest = new ContentManifest
        {
            Id = ManifestId.Create(installId),
            Name = "Zero Hour",
            ContentType = ContentType.GameInstallation,
        };

        _manifestPoolMock.Setup(m => m.GetManifestAsync(ManifestId.Create(mapId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(OperationResult<ContentManifest?>.CreateSuccess(mapManifest));
        _manifestPoolMock.Setup(m => m.GetManifestAsync(ManifestId.Create(installId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(OperationResult<ContentManifest?>.CreateSuccess(installManifest));

        _profileContentLinkerMock.Setup(p => p.UpdateProfileUserDataAsync(
            profileId,
            It.IsAny<IEnumerable<ContentManifest>>(),
            It.IsAny<GameType>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(OperationResult<bool>.CreateSuccess(true));

        await _viewModel.InitializeForProfileAsync(profileId);

        // Act
        await _viewModel.SaveCommand.ExecuteAsync(null);

        // Assert
        _profileContentLinkerMock.Verify(
            p => p.UpdateProfileUserDataAsync(
                profileId,
                It.Is<IEnumerable<ContentManifest>>(m => m.Any(x => x.Id.Value == mapId)),
                It.IsAny<GameType>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    /// <summary>
    /// Verifies that SaveAsync reports a failure status message when live content sync fails.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task SaveAsync_WhenLiveSyncFails_SetsStatusMessageWarning()
    {
        // Arrange
        const string profileId = "profile-live-3";
        const string installId = "1.108.steam.gameinstallation.zh";
        const string mapId = "1.0.0.map.desert";

        var profile = new GameProfile
        {
            Id = profileId,
            Name = "Live Profile",
            EnabledContentIds = [installId, mapId],
            GameClient = new GameClient
            {
                Id = "client-zh",
                Name = "Zero Hour",
                GameType = GameType.ZeroHour,
            },
        };

        _gameProfileManagerMock.Setup(m => m.GetProfileAsync(profileId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ProfileOperationResult<GameProfile>.CreateSuccess(profile));
        _gameProfileManagerMock.Setup(m => m.UpdateProfileAsync(profileId, It.IsAny<UpdateProfileRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ProfileOperationResult<GameProfile>.CreateSuccess(profile));

        _launchRegistryMock.Setup(l => l.GetAllActiveLaunchesAsync())
            .ReturnsAsync([CreateActiveLaunch(profileId)]);

        var enabledItems = new ObservableCollection<CoreContentDisplayItem>
        {
            new()
            {
                Id = installId,
                ManifestId = installId,
                DisplayName = "Command & Conquer: Zero Hour",
                ContentType = ContentType.GameInstallation,
                GameType = GameType.ZeroHour,
            },
            new()
            {
                Id = mapId,
                ManifestId = mapId,
                DisplayName = "Tournament Desert",
                ContentType = ContentType.Map,
                GameType = GameType.ZeroHour,
            },
        };

        _contentLoaderMock.Setup(c => c.LoadEnabledContentForProfileAsync(profile))
            .ReturnsAsync(enabledItems);
        _contentLoaderMock.Setup(c => c.LoadAvailableGameInstallationsAsync())
            .ReturnsAsync([]);
        _contentLoaderMock.Setup(c => c.LoadAvailableContentAsync(It.IsAny<ContentType>(), It.IsAny<ObservableCollection<CoreContentDisplayItem>>(), It.IsAny<IReadOnlyList<string>>()))
            .ReturnsAsync([]);
        _manifestPoolMock.Setup(m => m.GetAllManifestsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(OperationResult<IEnumerable<ContentManifest>>.CreateSuccess([]));

        var mapManifest = new ContentManifest
        {
            Id = ManifestId.Create(mapId),
            Name = "Tournament Desert",
            ContentType = ContentType.Map,
        };
        var installManifest = new ContentManifest
        {
            Id = ManifestId.Create(installId),
            Name = "Zero Hour",
            ContentType = ContentType.GameInstallation,
        };

        _manifestPoolMock.Setup(m => m.GetManifestAsync(ManifestId.Create(mapId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(OperationResult<ContentManifest?>.CreateSuccess(mapManifest));
        _manifestPoolMock.Setup(m => m.GetManifestAsync(ManifestId.Create(installId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(OperationResult<ContentManifest?>.CreateSuccess(installManifest));

        _profileContentLinkerMock.Setup(p => p.UpdateProfileUserDataAsync(
            profileId,
            It.IsAny<IEnumerable<ContentManifest>>(),
            It.IsAny<GameType>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(OperationResult<bool>.CreateFailure("Live file locked by process"));

        await _viewModel.InitializeForProfileAsync(profileId);

        // Act
        await _viewModel.SaveCommand.ExecuteAsync(null);

        // Assert
        Assert.Contains("live sync failed", _viewModel.StatusMessage, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Verifies that SaveAsync aborts live sync without calling UpdateProfileUserDataAsync if any enabled manifest cannot be resolved.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task SaveAsync_WhenManifestResolutionFails_AbortsLiveSyncAndDoesNotInvokeLinker()
    {
        // Arrange
        const string profileId = "profile-live-4";
        const string installId = "1.108.steam.gameinstallation.zh";
        const string mapId = "1.0.0.map.desert";

        var profile = new GameProfile
        {
            Id = profileId,
            Name = "Live Profile",
            EnabledContentIds = [installId, mapId],
            GameClient = new GameClient
            {
                Id = "client-zh",
                Name = "Zero Hour",
                GameType = GameType.ZeroHour,
            },
        };

        _gameProfileManagerMock.Setup(m => m.GetProfileAsync(profileId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ProfileOperationResult<GameProfile>.CreateSuccess(profile));
        _gameProfileManagerMock.Setup(m => m.UpdateProfileAsync(profileId, It.IsAny<UpdateProfileRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ProfileOperationResult<GameProfile>.CreateSuccess(profile));

        _launchRegistryMock.Setup(l => l.GetAllActiveLaunchesAsync())
            .ReturnsAsync([CreateActiveLaunch(profileId)]);

        var enabledItems = new ObservableCollection<CoreContentDisplayItem>
        {
            new()
            {
                Id = installId,
                ManifestId = installId,
                DisplayName = "Command & Conquer: Zero Hour",
                ContentType = ContentType.GameInstallation,
                GameType = GameType.ZeroHour,
            },
            new()
            {
                Id = mapId,
                ManifestId = mapId,
                DisplayName = "Tournament Desert",
                ContentType = ContentType.Map,
                GameType = GameType.ZeroHour,
            },
        };

        _contentLoaderMock.Setup(c => c.LoadEnabledContentForProfileAsync(profile))
            .ReturnsAsync(enabledItems);
        _contentLoaderMock.Setup(c => c.LoadAvailableGameInstallationsAsync())
            .ReturnsAsync([]);
        _contentLoaderMock.Setup(c => c.LoadAvailableContentAsync(It.IsAny<ContentType>(), It.IsAny<ObservableCollection<CoreContentDisplayItem>>(), It.IsAny<IReadOnlyList<string>>()))
            .ReturnsAsync([]);
        _manifestPoolMock.Setup(m => m.GetAllManifestsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(OperationResult<IEnumerable<ContentManifest>>.CreateSuccess([]));

        // Setup install manifest resolution success, but map manifest resolution failure
        var installManifest = new ContentManifest
        {
            Id = ManifestId.Create(installId),
            Name = "Zero Hour",
            ContentType = ContentType.GameInstallation,
        };

        _manifestPoolMock.Setup(m => m.GetManifestAsync(ManifestId.Create(installId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(OperationResult<ContentManifest?>.CreateSuccess(installManifest));
        _manifestPoolMock.Setup(m => m.GetManifestAsync(ManifestId.Create(mapId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(OperationResult<ContentManifest?>.CreateFailure("Manifest not found in pool"));

        await _viewModel.InitializeForProfileAsync(profileId);

        // Act
        await _viewModel.SaveCommand.ExecuteAsync(null);

        // Assert
        Assert.Contains("failed to resolve manifests", _viewModel.StatusMessage, StringComparison.OrdinalIgnoreCase);
        _profileContentLinkerMock.Verify(
            p => p.UpdateProfileUserDataAsync(
                It.IsAny<string>(),
                It.IsAny<IEnumerable<ContentManifest>>(),
                It.IsAny<GameType>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    private static GameLaunchInfo CreateActiveLaunch(string profileId, string launchId = "launch-1", string workspaceId = "ws-1") => new()
    {
        LaunchId = launchId,
        ProfileId = profileId,
        WorkspaceId = workspaceId,
        ProcessInfo = new GameProcessInfo
        {
            ProcessId = 1234,
            ProcessName = "generals.exe",
            StartTime = DateTime.UtcNow,
        },
    };
}
