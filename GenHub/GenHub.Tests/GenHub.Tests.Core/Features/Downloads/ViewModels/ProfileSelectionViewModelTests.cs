using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using GenHub.Core.Interfaces.GameProfiles;
using GenHub.Core.Interfaces.Manifest;
using GenHub.Core.Interfaces.Notifications;
using GenHub.Core.Models.Enums;
using GenHub.Core.Models.GameProfile;
using GenHub.Core.Models.Manifest;
using GenHub.Core.Models.Results;
using GenHub.Core.Models.Results.ProfileContent;
using GenHub.Features.Downloads.ViewModels;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace GenHub.Tests.Core.Features.Downloads.ViewModels;

/// <summary>
/// Unit tests for <see cref="ProfileSelectionViewModel"/>.
/// </summary>
public sealed class ProfileSelectionViewModelTests
{
    [Fact]
    public async Task InitializeAsync_PopulatesCompatibleAndOtherProfiles_BasedOnTargetGame()
    {
        // Arrange
        var profileManagerMock = new Mock<IGameProfileManager>();
        var profileContentMock = new Mock<IProfileContentService>();
        var manifestPoolMock = new Mock<IContentManifestPool>();
        var notificationMock = new Mock<INotificationService>();

        var zhProfile = new GameProfile
        {
            Id = "zh-profile-1",
            Name = "Zero Hour Profile",
            GameClient = new ProfileGameClient { GameType = GameType.ZeroHour },
        };

        var genProfile = new GameProfile
        {
            Id = "gen-profile-1",
            Name = "Generals Profile",
            GameClient = new ProfileGameClient { GameType = GameType.Generals },
        };

        profileManagerMock
            .Setup(x => x.GetAllProfilesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(OperationResult<IEnumerable<GameProfile>>.CreateSuccess([zhProfile, genProfile]));

        var vm = new ProfileSelectionViewModel(
            NullLogger<ProfileSelectionViewModel>.Instance,
            profileManagerMock.Object,
            profileContentMock.Object,
            manifestPoolMock.Object,
            notificationMock.Object);

        // Act: Initialize for ZeroHour content
        await vm.InitializeAsync(GameType.ZeroHour, "1.0.test.manifest", "Test Content");

        // Assert
        Assert.Single(vm.CompatibleProfiles);
        Assert.Equal("zh-profile-1", vm.CompatibleProfiles[0].Profile.Id);
        Assert.Single(vm.OtherProfiles);
        Assert.Equal("gen-profile-1", vm.OtherProfiles[0].Profile.Id);
        Assert.True(vm.HasCompatibleProfiles);
        Assert.True(vm.HasOtherProfiles);
        Assert.True(vm.HasAnyProfiles);
    }

    [Fact]
    public async Task AddToProfileCommand_SingleManifest_CallsSingleOverloadAndCloses()
    {
        // Arrange
        var profileManagerMock = new Mock<IGameProfileManager>();
        var profileContentMock = new Mock<IProfileContentService>();
        var manifestPoolMock = new Mock<IContentManifestPool>();
        var notificationMock = new Mock<INotificationService>();

        var zhProfile = new GameProfile
        {
            Id = "zh-profile-1",
            Name = "Zero Hour Profile",
            GameClient = new ProfileGameClient { GameType = GameType.ZeroHour },
        };

        profileManagerMock
            .Setup(x => x.GetAllProfilesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(OperationResult<IEnumerable<GameProfile>>.CreateSuccess([zhProfile]));

        profileContentMock
            .Setup(x => x.AddContentToProfileAsync("zh-profile-1", "1.0.test.manifest", It.IsAny<CancellationToken>()))
            .ReturnsAsync(ProfileContentResult.SuccessResult());

        var vm = new ProfileSelectionViewModel(
            NullLogger<ProfileSelectionViewModel>.Instance,
            profileManagerMock.Object,
            profileContentMock.Object,
            manifestPoolMock.Object,
            notificationMock.Object);

        var closeRequested = false;
        vm.RequestClose += (_, _) => closeRequested = true;

        await vm.InitializeAsync(GameType.ZeroHour, "1.0.test.manifest", "Test Content");

        // Act
        await vm.AddToProfileCommand.ExecuteAsync(vm.CompatibleProfiles[0]);

        // Assert
        Assert.True(vm.WasSuccessful);
        Assert.True(closeRequested);
        Assert.Equal("Zero Hour Profile", vm.SelectedProfileName);
        profileContentMock.Verify(
            x => x.AddContentToProfileAsync("zh-profile-1", "1.0.test.manifest", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task AddToProfileCommand_BundleManifests_CallsListOverloadAndCloses()
    {
        // Arrange
        var profileManagerMock = new Mock<IGameProfileManager>();
        var profileContentMock = new Mock<IProfileContentService>();
        var manifestPoolMock = new Mock<IContentManifestPool>();
        var notificationMock = new Mock<INotificationService>();

        var zhProfile = new GameProfile
        {
            Id = "zh-profile-1",
            Name = "Zero Hour Profile",
            GameClient = new ProfileGameClient { GameType = GameType.ZeroHour },
        };

        profileManagerMock
            .Setup(x => x.GetAllProfilesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(OperationResult<IEnumerable<GameProfile>>.CreateSuccess([zhProfile]));

        var bundleIds = new List<string> { "bundle.part.1", "bundle.part.2" };

        profileContentMock
            .Setup(x => x.AddContentToProfileAsync("zh-profile-1", It.Is<IReadOnlyList<string>>(l => l.Count == 2), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ProfileContentResult.SuccessResult());

        var vm = new ProfileSelectionViewModel(
            NullLogger<ProfileSelectionViewModel>.Instance,
            profileManagerMock.Object,
            profileContentMock.Object,
            manifestPoolMock.Object,
            notificationMock.Object);

        var closeRequested = false;
        vm.RequestClose += (_, _) => closeRequested = true;

        await vm.InitializeAsync(GameType.ZeroHour, "bundle.primary", "Test Bundle", additionalManifestIds: bundleIds);

        // Act
        await vm.AddToProfileCommand.ExecuteAsync(vm.CompatibleProfiles[0]);

        // Assert
        Assert.True(vm.WasSuccessful);
        Assert.True(closeRequested);
        profileContentMock.Verify(
            x => x.AddContentToProfileAsync("zh-profile-1", It.Is<IReadOnlyList<string>>(l => l.Count == 2), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task AddToProfileCommand_Failure_SetsErrorMessageAndDoesNotClose()
    {
        // Arrange
        var profileManagerMock = new Mock<IGameProfileManager>();
        var profileContentMock = new Mock<IProfileContentService>();
        var manifestPoolMock = new Mock<IContentManifestPool>();
        var notificationMock = new Mock<INotificationService>();

        var zhProfile = new GameProfile
        {
            Id = "zh-profile-1",
            Name = "Zero Hour Profile",
            GameClient = new ProfileGameClient { GameType = GameType.ZeroHour },
        };

        profileManagerMock
            .Setup(x => x.GetAllProfilesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(OperationResult<IEnumerable<GameProfile>>.CreateSuccess([zhProfile]));

        profileContentMock
            .Setup(x => x.AddContentToProfileAsync("zh-profile-1", "1.0.test.manifest", It.IsAny<CancellationToken>()))
            .ReturnsAsync(ProfileContentResult.FailureResult("Incompatible version conflict"));

        var vm = new ProfileSelectionViewModel(
            NullLogger<ProfileSelectionViewModel>.Instance,
            profileManagerMock.Object,
            profileContentMock.Object,
            manifestPoolMock.Object,
            notificationMock.Object);

        var closeRequested = false;
        vm.RequestClose += (_, _) => closeRequested = true;

        await vm.InitializeAsync(GameType.ZeroHour, "1.0.test.manifest", "Test Content");

        // Act
        await vm.AddToProfileCommand.ExecuteAsync(vm.CompatibleProfiles[0]);

        // Assert
        Assert.False(vm.WasSuccessful);
        Assert.False(closeRequested);
        Assert.Equal("Incompatible version conflict", vm.ErrorMessage);
    }
}
