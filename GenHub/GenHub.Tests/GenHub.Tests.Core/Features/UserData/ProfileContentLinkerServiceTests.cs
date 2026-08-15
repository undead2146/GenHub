using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using GenHub.Core.Interfaces.UserData;
using GenHub.Core.Models.Enums;
using GenHub.Core.Models.Manifest;
using GenHub.Core.Models.Results;
using GenHub.Core.Models.UserData;
using GenHub.Features.UserData.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using ContentType = GenHub.Core.Models.Enums.ContentType;

namespace GenHub.Tests.Core.Features.UserData;

/// <summary>
/// Contains unit tests for <see cref="ProfileContentLinkerService"/>.
/// </summary>
public class ProfileContentLinkerServiceTests
{
    private readonly Mock<IUserDataTracker> _userDataTrackerMock = new();
    private readonly Mock<ILogger<ProfileContentLinkerService>> _loggerMock = new();
    private readonly ProfileContentLinkerService _linkerService;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProfileContentLinkerServiceTests"/> class.
    /// </summary>
    public ProfileContentLinkerServiceTests()
    {
        _linkerService = new ProfileContentLinkerService(
            _userDataTrackerMock.Object,
            _loggerMock.Object);
    }

    /// <summary>
    /// Verifies that SwitchProfileUserDataAsync deactivates lingering active user data from other profiles even when oldProfileId is null (e.g. after GenHub/game crash).
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task SwitchProfileUserDataAsync_WhenOldProfileIsNull_DeactivatesLingeringActiveUserDataFromOtherProfiles()
    {
        // Arrange
        const string newProfileId = "profile-new";
        const string lingeringProfileId = "profile-crashed";
        const GameType gameType = GameType.ZeroHour;

        var lingeringManifest = new UserDataManifest
        {
            ManifestId = "1.0.0.patch.crashed",
            ProfileId = lingeringProfileId,
            TargetGame = gameType,
            IsActive = true,
            InstalledFiles = [new UserDataFileEntry { AbsolutePath = "C:\\path\\GameData.ini", RelativePath = "Data\\INI\\GameData.ini", InstallTarget = ContentInstallTarget.UserDataDirectory }],
        };

        _userDataTrackerMock.Setup(t => t.GetGameUserDataAsync(gameType, It.IsAny<CancellationToken>()))
            .ReturnsAsync(OperationResult<IReadOnlyList<UserDataManifest>>.CreateSuccess([lingeringManifest]));

        _userDataTrackerMock.Setup(t => t.DeactivateProfileUserDataAsync(lingeringProfileId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(OperationResult<bool>.CreateSuccess(true));

        _userDataTrackerMock.Setup(t => t.ActivateProfileUserDataAsync(newProfileId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(OperationResult<bool>.CreateSuccess(true));

        var newManifest = new ContentManifest
        {
            Id = ManifestId.Create("1.0.0.map.desert"),
            Name = "Desert Map",
            ContentType = ContentType.Map,
            Files = [new ManifestFile { RelativePath = "Maps\\Desert.map", InstallTarget = ContentInstallTarget.UserMapsDirectory, Hash = "hash-1" }],
        };

        _userDataTrackerMock.Setup(t => t.GetUserDataManifestAsync(newManifest.Id.Value, newProfileId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(OperationResult<UserDataManifest?>.CreateSuccess(null));

        _userDataTrackerMock.Setup(t => t.InstallUserDataAsync(
            newManifest.Id.Value,
            newProfileId,
            gameType,
            It.IsAny<IEnumerable<ManifestFile>>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(OperationResult<UserDataManifest>.CreateSuccess(new UserDataManifest { ManifestId = newManifest.Id.Value, ProfileId = newProfileId }));

        // Act
        var result = await _linkerService.SwitchProfileUserDataAsync(
            oldProfileId: null,
            newProfileId: newProfileId,
            newManifests: [newManifest],
            targetGame: gameType);

        // Assert
        Assert.True(result.Success);
        _userDataTrackerMock.Verify(t => t.DeactivateProfileUserDataAsync(lingeringProfileId, It.IsAny<CancellationToken>()), Times.Once);
        _userDataTrackerMock.Verify(t => t.ActivateProfileUserDataAsync(newProfileId, It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// Verifies that PrepareProfileUserDataAsync cleans up any lingering active user data from other profiles before activating the target profile.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task PrepareProfileUserDataAsync_DeactivatesLingeringActiveUserDataFromOtherProfiles()
    {
        // Arrange
        const string targetProfileId = "profile-target";
        const string lingeringProfileId = "profile-lingering";
        const GameType gameType = GameType.ZeroHour;

        var lingeringManifest = new UserDataManifest
        {
            ManifestId = "1.0.0.patch.old",
            ProfileId = lingeringProfileId,
            TargetGame = gameType,
            IsActive = true,
            InstalledFiles = [new UserDataFileEntry { AbsolutePath = "C:\\path\\GameData.ini", RelativePath = "Data\\INI\\GameData.ini", InstallTarget = ContentInstallTarget.UserDataDirectory }],
        };

        _userDataTrackerMock.Setup(t => t.GetGameUserDataAsync(gameType, It.IsAny<CancellationToken>()))
            .ReturnsAsync(OperationResult<IReadOnlyList<UserDataManifest>>.CreateSuccess([lingeringManifest]));

        _userDataTrackerMock.Setup(t => t.DeactivateProfileUserDataAsync(lingeringProfileId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(OperationResult<bool>.CreateSuccess(true));

        _userDataTrackerMock.Setup(t => t.ActivateProfileUserDataAsync(targetProfileId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(OperationResult<bool>.CreateSuccess(true));

        var manifest = new ContentManifest
        {
            Id = ManifestId.Create("1.0.0.map.desert"),
            Name = "Desert Map",
            ContentType = ContentType.Map,
            Files = [new ManifestFile { RelativePath = "Maps\\Desert.map", InstallTarget = ContentInstallTarget.UserMapsDirectory, Hash = "hash-1" }],
        };

        _userDataTrackerMock.Setup(t => t.GetUserDataManifestAsync(manifest.Id.Value, targetProfileId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(OperationResult<UserDataManifest?>.CreateSuccess(null));

        _userDataTrackerMock.Setup(t => t.InstallUserDataAsync(
            manifest.Id.Value,
            targetProfileId,
            gameType,
            It.IsAny<IEnumerable<ManifestFile>>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(OperationResult<UserDataManifest>.CreateSuccess(new UserDataManifest { ManifestId = manifest.Id.Value, ProfileId = targetProfileId }));

        // Act
        var result = await _linkerService.PrepareProfileUserDataAsync(targetProfileId, [manifest], gameType);

        // Assert
        Assert.True(result.Success);
        _userDataTrackerMock.Verify(t => t.DeactivateProfileUserDataAsync(lingeringProfileId, It.IsAny<CancellationToken>()), Times.Once);
        _userDataTrackerMock.Verify(t => t.ActivateProfileUserDataAsync(targetProfileId, It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// Verifies that UpdateProfileUserDataAsync returns failure when activation fails.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task UpdateProfileUserDataAsync_WhenActivationFails_ReturnsFailure()
    {
        // Arrange
        const string profileId = "profile-live";
        const GameType gameType = GameType.ZeroHour;

        _userDataTrackerMock.Setup(t => t.GetGameUserDataAsync(gameType, It.IsAny<CancellationToken>()))
            .ReturnsAsync(OperationResult<IReadOnlyList<UserDataManifest>>.CreateSuccess([]));

        _userDataTrackerMock.Setup(t => t.GetProfileUserDataAsync(profileId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(OperationResult<IReadOnlyList<UserDataManifest>>.CreateSuccess([]));

        _userDataTrackerMock.Setup(t => t.ActivateProfileUserDataAsync(profileId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(OperationResult<bool>.CreateFailure("Activation locked by file"));

        // Simulate that this profile is the active profile (empty manifest list sets active profile without activating user data)
        await _linkerService.PrepareProfileUserDataAsync(profileId, [], gameType);

        var manifest = new ContentManifest
        {
            Id = ManifestId.Create("1.0.0.map.desert"),
            Name = "Desert Map",
            ContentType = ContentType.Map,
            Files = [new ManifestFile { RelativePath = "Maps\\Desert.map", InstallTarget = ContentInstallTarget.UserMapsDirectory, Hash = "hash-1" }],
        };

        _userDataTrackerMock.Setup(t => t.GetUserDataManifestAsync(manifest.Id.Value, profileId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(OperationResult<UserDataManifest?>.CreateSuccess(null));

        _userDataTrackerMock.Setup(t => t.InstallUserDataAsync(
            manifest.Id.Value,
            profileId,
            gameType,
            It.IsAny<IEnumerable<ManifestFile>>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(OperationResult<UserDataManifest>.CreateSuccess(new UserDataManifest { ManifestId = manifest.Id.Value, ProfileId = profileId }));

        _userDataTrackerMock.Setup(t => t.UninstallUserDataAsync(manifest.Id.Value, profileId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(OperationResult<bool>.CreateSuccess(true));

        // Act
        var result = await _linkerService.UpdateProfileUserDataAsync(profileId, [manifest], gameType);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("Failed to activate user data", result.FirstError, StringComparison.OrdinalIgnoreCase);
        _userDataTrackerMock.Verify(t => t.UninstallUserDataAsync(manifest.Id.Value, profileId, It.IsAny<CancellationToken>()), Times.Once);
    }
}
