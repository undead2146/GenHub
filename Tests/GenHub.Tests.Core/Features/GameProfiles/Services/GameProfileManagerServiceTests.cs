using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using GenHub.Core.Interfaces;
using GenHub.Core.Interfaces.Repositories;
using GenHub.Core.Models;
using GenHub.Core.Models.GameProfiles;
using GenHub.Core.Models.SourceMetadata;
using GenHub.Features.GameProfiles.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace GenHub.Tests.Core.Features.GameProfiles.Services
{
    public class GameProfileManagerServiceTests
    {
        private readonly Mock<ILogger<GameProfileManagerService>> _mockLogger;
        private readonly Mock<IGameProfileRepository> _mockProfileRepository;
        private readonly Mock<IGameVersionServiceFacade> _mockGameVersionService;
        private readonly Mock<IGameProfileFactory> _mockProfileFactory;
        private readonly Mock<ProfileMetadataService> _mockMetadataService;
        private readonly Mock<ProfileResourceService> _mockResourceService;
        private readonly GameProfileManagerService _service;

        public GameProfileManagerServiceTests()
        {
            _mockLogger = new Mock<ILogger<GameProfileManagerService>>();
            _mockProfileRepository = new Mock<IGameProfileRepository>();
            _mockGameVersionService = new Mock<IGameVersionServiceFacade>();
            _mockProfileFactory = new Mock<IGameProfileFactory>();
            _mockMetadataService = new Mock<ProfileMetadataService>();
            _mockResourceService = new Mock<ProfileResourceService>();

            _service = new GameProfileManagerService(
                _mockLogger.Object,
                _mockProfileRepository.Object,
                _mockGameVersionService.Object,
                _mockResourceService.Object,
                _mockMetadataService.Object);
        }

        #region Constructor Tests

        [Fact]
        public void Constructor_WithNullLogger_ThrowsArgumentNullException()
        {
            // Act & Assert
            var exception = Assert.Throws<ArgumentNullException>(() =>
                new GameProfileManagerService(
                    null!,
                    _mockProfileRepository.Object,
                    _mockGameVersionService.Object,
                    _mockResourceService.Object,
                    _mockMetadataService.Object));

            exception.ParamName.Should().Be("logger");
        }

        [Fact]
        public void Constructor_WithNullProfileRepository_ThrowsArgumentNullException()
        {
            // Act & Assert
            var exception = Assert.Throws<ArgumentNullException>(() =>
                new GameProfileManagerService(
                    _mockLogger.Object,
                    null!,
                    _mockGameVersionService.Object,
                    _mockResourceService.Object,
                    _mockMetadataService.Object));

            exception.ParamName.Should().Be("profileRepository");
        }

        [Fact]
        public void Constructor_WithNullGameVersionService_ThrowsArgumentNullException()
        {
            // Act & Assert
            var exception = Assert.Throws<ArgumentNullException>(() =>
                new GameProfileManagerService(
                    _mockLogger.Object,
                    _mockProfileRepository.Object,
                    null!,
                    _mockResourceService.Object,
                    _mockMetadataService.Object));

            exception.ParamName.Should().Be("gameVersionService");
        }

        [Fact]
        public void Constructor_WithNullResourceService_ThrowsArgumentNullException()
        {
            // Act & Assert
            var exception = Assert.Throws<ArgumentNullException>(() =>
                new GameProfileManagerService(
                    _mockLogger.Object,
                    _mockProfileRepository.Object,
                    _mockGameVersionService.Object,
                    null!,
                    _mockMetadataService.Object));

            exception.ParamName.Should().Be("profileResourceService");
        }

        [Fact]
        public void Constructor_WithNullMetadataService_ThrowsArgumentNullException()
        {
            // Act & Assert
            var exception = Assert.Throws<ArgumentNullException>(() =>
                new GameProfileManagerService(
                    _mockLogger.Object,
                    _mockProfileRepository.Object,
                    _mockGameVersionService.Object,
                    _mockResourceService.Object,
                    null!));

            exception.ParamName.Should().Be("profileMetadataService");
        }

        [Fact]
        public void Constructor_WithValidParameters_CreatesInstance()
        {
            // Act & Assert
            _service.Should().NotBeNull();
        }

        #endregion

        #region GetProfilesAsync Tests

        [Fact]
        public async Task GetProfilesAsync_WithValidRepository_ReturnsProfiles()
        {            // Arrange
            var expectedProfiles = new List<GameProfile>
            {
                new GameProfile { Id = "profile1", Name = "Profile 1" },
                new GameProfile { Id = "profile2", Name = "Profile 2" }
            };_mockProfileRepository.Setup(x => x.GetAllAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedProfiles);

            // Act
            var result = await _service.GetProfilesAsync();

            // Assert
            result.Should().NotBeNull();
            result.Should().HaveCount(2);
            result.Should().BeEquivalentTo(expectedProfiles);
        }

        [Fact]
        public async Task GetProfilesAsync_WhenRepositoryThrows_ReturnsEmptyEnumerable()
        {
            // Arrange
            _mockProfileRepository.Setup(x => x.GetAllAsync(It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("Repository error"));

            // Act
            var result = await _service.GetProfilesAsync();

            // Assert
            result.Should().NotBeNull();
            result.Should().BeEmpty();
        }

        [Fact]
        public async Task GetProfilesAsync_PassesCancellationToken()
        {
            // Arrange
            var cancellationToken = new CancellationToken();            _mockProfileRepository.Setup(x => x.GetAllAsync(cancellationToken))
                .ReturnsAsync(new List<GameProfile>());

            // Act
            await _service.GetProfilesAsync(cancellationToken);

            // Assert
            _mockProfileRepository.Verify(x => x.GetAllAsync(cancellationToken), Times.Once);
        }

        #endregion

        #region GetProfileAsync Tests

        [Fact]
        public async Task GetProfileAsync_WithValidId_ReturnsProfile()
        {
            // Arrange
            var profileId = "test-profile-id";
            var expectedProfile = new GameProfile { Id = profileId, Name = "Test Profile" };

            _mockProfileRepository.Setup(x => x.GetByIdAsync(profileId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedProfile);

            // Act
            var result = await _service.GetProfileAsync(profileId);

            // Assert
            result.Should().NotBeNull();
            result.Should().Be(expectedProfile);
        }        [Fact]
        public async Task GetProfileAsync_WithNonExistentId_ReturnsNull()
        {
            // Arrange
            var profileId = "non-existent-id";

            _mockProfileRepository.Setup(x => x.GetByIdAsync(profileId, It.IsAny<CancellationToken>()))
                .ReturnsAsync((GameProfile?)null);

            // Act
            var result = await _service.GetProfileAsync(profileId);

            // Assert
            result.Should().BeNull();
        }

        [Fact]
        public async Task GetProfileAsync_WhenRepositoryThrows_ReturnsNull()
        {
            // Arrange
            var profileId = "test-profile-id";

            _mockProfileRepository.Setup(x => x.GetByIdAsync(profileId, It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("Repository error"));

            // Act
            var result = await _service.GetProfileAsync(profileId);

            // Assert
            result.Should().BeNull();
        }

        #endregion

        #region GetProfileByExecutablePathAsync Tests

        [Fact]
        public async Task GetProfileByExecutablePathAsync_WithValidPath_ReturnsFirstProfile()
        {
            // Arrange
            var executablePath = @"C:\Games\Test\test.exe";
            var profiles = new List<IGameProfile>
            {
                new GameProfile { Id = "profile1", ExecutablePath = executablePath },
                new GameProfile { Id = "profile2", ExecutablePath = executablePath }
            };

            _mockProfileRepository.Setup(x => x.GetProfilesByExecutablePathAsync(executablePath, It.IsAny<CancellationToken>()))
                .ReturnsAsync(profiles);

            // Act
            var result = await _service.GetProfileByExecutablePathAsync(executablePath);

            // Assert
            result.Should().NotBeNull();
            result!.Id.Should().Be("profile1");
        }

        [Fact]
        public async Task GetProfileByExecutablePathAsync_WhenNoProfilesFound_ReturnsNull()
        {
            // Arrange
            var executablePath = @"C:\Games\Test\test.exe";

            _mockProfileRepository.Setup(x => x.GetProfilesByExecutablePathAsync(executablePath, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<IGameProfile>());

            // Act
            var result = await _service.GetProfileByExecutablePathAsync(executablePath);

            // Assert
            result.Should().BeNull();
        }

        [Fact]
        public async Task GetProfileByExecutablePathAsync_WhenRepositoryThrows_ReturnsNull()
        {
            // Arrange
            var executablePath = @"C:\Games\Test\test.exe";

            _mockProfileRepository.Setup(x => x.GetProfilesByExecutablePathAsync(executablePath, It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("Repository error"));

            // Act
            var result = await _service.GetProfileByExecutablePathAsync(executablePath);

            // Assert
            result.Should().BeNull();
        }

        #endregion

        #region CreateProfileFromVersionAsync Tests

        [Fact]
        public async Task CreateProfileFromVersionAsync_WithValidVersion_CreatesAndSavesProfile()
        {
            // Arrange
            var gameVersion = new GameVersion
            {
                Id = "test-version",
                Name = "Test Version",
                ExecutablePath = @"C:\Games\Test\test.exe",
                GameType = "Generals",
                IsFromGitHub = false
            };

            var createdProfile = new GameProfile { Id = "new-profile", Name = "New Profile" };
            var testDescription = "Test description";            var mockIcon = new ProfileResourceItem { Path = "icon.png" };
            var mockCover = new ProfileResourceItem { Path = "cover.png" };

            _mockProfileFactory.Setup(x => x.CreateFromVersion(gameVersion))
                .Returns(createdProfile);
            _mockMetadataService.Setup(x => x.GenerateGameDescription(gameVersion))
                .Returns(testDescription);
            _mockResourceService.Setup(x => x.FindIconForGameType("Generals"))
                .Returns(mockIcon);
            _mockResourceService.Setup(x => x.FindCoverForGameType("Generals"))
                .Returns(mockCover);

            // Act
            var result = await _service.CreateProfileFromVersionAsync(gameVersion);

            // Assert
            result.Should().NotBeNull();
            result.Description.Should().Be(testDescription);
            result.IconPath.Should().Be("icon.png");
            result.CoverImagePath.Should().Be("cover.png");

            _mockProfileRepository.Verify(x => x.AddAsync(It.IsAny<GameProfile>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task CreateProfileFromVersionAsync_WithGitHubVersion_SetsGitHubMetadata()
        {            // Arrange
            var gitHubMetadata = new GitHubSourceMetadata
            {
                AssociatedArtifact = new GitHubArtifact { CommitSha = "abc123" },
                BuildPreset = "Release"
            };

            var gameVersion = new GameVersion
            {
                Id = "github-version",
                Name = "GitHub Version",
                ExecutablePath = @"C:\Games\Test\test.exe",
                GameType = "Generals",                SourceType = GameInstallationType.GitHubArtifact,
                SourceSpecificMetadata = gitHubMetadata
            };

            var createdProfile = new GameProfile { Id = "new-profile", Name = "New Profile" };

            _mockProfileFactory.Setup(x => x.CreateFromVersion(gameVersion))
                .Returns(createdProfile);
            _mockMetadataService.Setup(x => x.GenerateGameDescription(gameVersion))
                .Returns("Test description");            _mockResourceService.Setup(x => x.FindIconForGameType(It.IsAny<string>()))
                .Returns(new ProfileResourceItem { Path = "icon.png" });
            _mockResourceService.Setup(x => x.FindCoverForGameType(It.IsAny<string>()))
                .Returns(new ProfileResourceItem { Path = "cover.png" });

            // Act
            var result = await _service.CreateProfileFromVersionAsync(gameVersion);

            // Assert
            result.SourceSpecificMetadata.Should().NotBeNull();
            result.SourceSpecificMetadata.Should().BeOfType<GitHubSourceMetadata>();
        }

        [Fact]
        public async Task CreateProfileFromVersionAsync_WhenFactoryThrows_PropagatesException()
        {
            // Arrange
            var gameVersion = new GameVersion { Id = "test-version" };

            _mockProfileFactory.Setup(x => x.CreateFromVersion(gameVersion))
                .Throws(new InvalidOperationException("Factory error"));

            // Act & Assert
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                _service.CreateProfileFromVersionAsync(gameVersion));

            exception.Message.Should().Be("Factory error");
        }

        #endregion

        #region CreateDefaultProfilesAsync Tests

        [Fact]
        public async Task CreateDefaultProfilesAsync_WithInstalledVersions_CreatesProfilesForNewVersions()
        {
            // Arrange
            var installedVersions = new List<GameVersion>
            {
                new GameVersion { Id = "version1", ExecutablePath = @"C:\Games\V1\game.exe" },
                new GameVersion { Id = "version2", ExecutablePath = @"C:\Games\V2\game.exe" }
            };

            var profile1 = new GameProfile { Id = "profile1", IsDefaultProfile = false };
            var profile2 = new GameProfile { Id = "profile2", IsDefaultProfile = false };

            _mockGameVersionService.Setup(x => x.GetInstalledVersionsAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(installedVersions);

            // Setup no existing profiles
            _mockProfileRepository.Setup(x => x.GetProfilesByExecutablePathAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<IGameProfile>());

            _mockProfileFactory.Setup(x => x.CreateFromVersion(installedVersions[0]))
                .Returns(profile1);
            _mockProfileFactory.Setup(x => x.CreateFromVersion(installedVersions[1]))
                .Returns(profile2);

            // Act
            var result = await _service.CreateDefaultProfilesAsync();

            // Assert
            result.Should().HaveCount(2);
            result.All(p => p.IsDefaultProfile).Should().BeTrue();

            _mockProfileRepository.Verify(x => x.AddAsync(It.IsAny<GameProfile>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
        }

        [Fact]
        public async Task CreateDefaultProfilesAsync_WithExistingProfiles_SkipsExistingVersions()
        {
            // Arrange
            var installedVersions = new List<GameVersion>
            {
                new GameVersion { Id = "version1", ExecutablePath = @"C:\Games\V1\game.exe" },
                new GameVersion { Id = "version2", ExecutablePath = @"C:\Games\V2\game.exe" }
            };

            var existingProfile = new GameProfile { Id = "existing", ExecutablePath = @"C:\Games\V1\game.exe" };

            _mockGameVersionService.Setup(x => x.GetInstalledVersionsAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(installedVersions);

            // Setup existing profile for version1, none for version2
            _mockProfileRepository.Setup(x => x.GetProfilesByExecutablePathAsync(@"C:\Games\V1\game.exe", It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<IGameProfile> { existingProfile });
            _mockProfileRepository.Setup(x => x.GetProfilesByExecutablePathAsync(@"C:\Games\V2\game.exe", It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<IGameProfile>());

            var profile2 = new GameProfile { Id = "profile2", IsDefaultProfile = false };
            _mockProfileFactory.Setup(x => x.CreateFromVersion(installedVersions[1]))
                .Returns(profile2);

            // Act
            var result = await _service.CreateDefaultProfilesAsync();

            // Assert
            result.Should().HaveCount(1);
            result.First().IsDefaultProfile.Should().BeTrue();

            _mockProfileRepository.Verify(x => x.AddAsync(It.IsAny<GameProfile>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task CreateDefaultProfilesAsync_WhenGameVersionServiceThrows_ReturnsEmptyCollection()
        {
            // Arrange
            _mockGameVersionService.Setup(x => x.GetInstalledVersionsAsync(It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("Service error"));

            // Act
            var result = await _service.CreateDefaultProfilesAsync();

            // Assert
            result.Should().BeEmpty();
        }

        #endregion

        #region DeleteProfileAsync Tests

        [Fact]
        public async Task DeleteProfileAsync_WithValidNonDefaultProfile_DeletesProfile()
        {
            // Arrange
            var profileId = "test-profile-id";
            var profile = new GameProfile { Id = profileId, IsDefaultProfile = false };

            _mockProfileRepository.Setup(x => x.GetByIdAsync(profileId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(profile);

            // Act
            await _service.DeleteProfileAsync(profileId);

            // Assert
            _mockProfileRepository.Verify(x => x.DeleteAsync(profileId, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task DeleteProfileAsync_WithDefaultProfile_DoesNotDelete()
        {
            // Arrange
            var profileId = "default-profile-id";
            var profile = new GameProfile { Id = profileId, IsDefaultProfile = true };

            _mockProfileRepository.Setup(x => x.GetByIdAsync(profileId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(profile);

            // Act
            await _service.DeleteProfileAsync(profileId);

            // Assert
            _mockProfileRepository.Verify(x => x.DeleteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task DeleteProfileAsync_WithNonExistentProfile_DoesNotThrow()
        {
            // Arrange
            var profileId = "non-existent-id";

            _mockProfileRepository.Setup(x => x.GetByIdAsync(profileId, It.IsAny<CancellationToken>()))
                .ReturnsAsync((IGameProfile?)null);

            // Act & Assert
            var action = async () => await _service.DeleteProfileAsync(profileId);
            await action.Should().NotThrowAsync();
        }

        [Fact]
        public async Task DeleteProfileAsync_WhenRepositoryThrows_PropagatesException()
        {
            // Arrange
            var profileId = "test-profile-id";
            var profile = new GameProfile { Id = profileId, IsDefaultProfile = false };

            _mockProfileRepository.Setup(x => x.GetByIdAsync(profileId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(profile);
            _mockProfileRepository.Setup(x => x.DeleteAsync(profileId, It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("Repository error"));

            // Act & Assert
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                _service.DeleteProfileAsync(profileId));

            exception.Message.Should().Be("Repository error");
        }

        #endregion

        #region SaveProfileAsync Tests

        [Fact]
        public async Task SaveProfileAsync_WithNullProfile_ThrowsArgumentNullException()
        {
            // Act & Assert
            var exception = await Assert.ThrowsAsync<ArgumentNullException>(() =>
                _service.SaveProfileAsync(null!));

            exception.ParamName.Should().Be("profile");
        }

        [Fact]
        public async Task SaveProfileAsync_WithNewProfile_AddsProfile()
        {
            // Arrange
            var newProfile = new GameProfile { Id = "new-profile", Name = "New Profile" };
            var existingProfiles = new List<IGameProfile>
            {
                new GameProfile { Id = "existing1", DisplayOrder = 1 },
                new GameProfile { Id = "existing2", DisplayOrder = 2 }
            };

            _mockProfileRepository.Setup(x => x.GetByIdAsync(newProfile.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync((IGameProfile?)null);
            _mockProfileRepository.Setup(x => x.GetAllAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(existingProfiles);

            // Act
            await _service.SaveProfileAsync(newProfile);

            // Assert
            _mockProfileRepository.Verify(x => x.AddAsync(It.Is<GameProfile>(p => p.DisplayOrder == 3), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task SaveProfileAsync_WithExistingProfile_UpdatesProfile()
        {
            // Arrange
            var existingProfile = new GameProfile { Id = "existing-profile", Name = "Existing Profile" };
            var updatedProfile = new GameProfile { Id = "existing-profile", Name = "Updated Profile" };

            _mockProfileRepository.Setup(x => x.GetByIdAsync(existingProfile.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(existingProfile);

            // Act
            await _service.SaveProfileAsync(updatedProfile);

            // Assert
            _mockProfileRepository.Verify(x => x.UpdateAsync(It.IsAny<GameProfile>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task SaveProfileAsync_WithNewProfile_FiresProfilesUpdatedEvent()
        {
            // Arrange
            var newProfile = new GameProfile { Id = "new-profile", Name = "New Profile" };
            var eventFired = false;

            _mockProfileRepository.Setup(x => x.GetByIdAsync(newProfile.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync((IGameProfile?)null);
            _mockProfileRepository.Setup(x => x.GetAllAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<IGameProfile>());

            _service.ProfilesUpdated += (sender, args) => eventFired = true;

            // Act
            await _service.SaveProfileAsync(newProfile);

            // Assert
            eventFired.Should().BeTrue();
        }

        #endregion

        #region UpdateProfileAsync Tests

        [Fact]
        public async Task UpdateProfileAsync_WithNullProfile_ThrowsArgumentNullException()
        {
            // Act & Assert
            var exception = await Assert.ThrowsAsync<ArgumentNullException>(() =>
                _service.UpdateProfileAsync(null!));

            exception.ParamName.Should().Be("profile");
        }

        [Fact]
        public async Task UpdateProfileAsync_WithValidProfile_UpdatesAndFiresEvent()
        {
            // Arrange
            var profile = new GameProfile { Id = "test-profile", Name = "Test Profile" };
            var existingProfile = new GameProfile { Id = "test-profile", DisplayOrder = 5 };
            var eventFired = false;

            _mockProfileRepository.Setup(x => x.GetByIdAsync(profile.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(existingProfile);

            _service.ProfilesUpdated += (sender, args) => eventFired = true;

            // Act
            await _service.UpdateProfileAsync(profile);

            // Assert
            _mockProfileRepository.Verify(x => x.UpdateAsync(It.Is<GameProfile>(p => p.DisplayOrder == 5), It.IsAny<CancellationToken>()), Times.Once);
            _mockResourceService.Verify(x => x.FixResourcePaths(It.IsAny<GameProfile>()), Times.Once);
            eventFired.Should().BeTrue();
        }

        [Fact]
        public async Task UpdateProfileAsync_WithGitHubProfile_InitializesMetadata()
        {
            // Arrange
            var profile = new GameProfile 
            { 
                Id = "github-profile", 
                Name = "GitHub Profile",
                SourceType = GameInstallationType.GitHubArtifact,
                SourceSpecificMetadata = null
            };

            // Act
            await _service.UpdateProfileAsync(profile);

            // Assert
            _mockProfileRepository.Verify(x => x.UpdateAsync(
                It.Is<GameProfile>(p => p.SourceSpecificMetadata != null && p.SourceSpecificMetadata is GitHubSourceMetadata), 
                It.IsAny<CancellationToken>()), Times.Once);
        }

        #endregion

        #region AddProfileAsync Tests

        [Fact]
        public async Task AddProfileAsync_WithValidProfile_AddsAndFiresEvent()
        {
            // Arrange
            var profile = new GameProfile { Id = "new-profile", Name = "New Profile" };
            var eventFired = false;

            _service.ProfilesUpdated += (sender, args) => eventFired = true;

            // Act
            await _service.AddProfileAsync(profile);

            // Assert
            _mockProfileRepository.Verify(x => x.AddAsync(profile, It.IsAny<CancellationToken>()), Times.Once);
            eventFired.Should().BeTrue();
        }

        [Fact]
        public async Task AddProfileAsync_WithIGameProfileInterface_ConvertsToGameProfile()
        {
            // Arrange
            var profile = new Mock<IGameProfile>();
            profile.Setup(x => x.Id).Returns("interface-profile");
            profile.Setup(x => x.Name).Returns("Interface Profile");

            // Act
            await _service.AddProfileAsync(profile.Object);

            // Assert
            _mockProfileRepository.Verify(x => x.AddAsync(It.IsAny<GameProfile>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        #endregion

        #region SaveCustomProfilesAsync Tests

        [Fact]
        public async Task SaveCustomProfilesAsync_WithValidProfiles_SavesAllAndFiresEvent()
        {
            // Arrange
            var profiles = new List<IGameProfile>
            {
                new GameProfile { Id = "profile1", Name = "Profile 1" },
                new GameProfile { Id = "profile2", Name = "Profile 2" }
            };
            var eventFired = false;

            _service.ProfilesUpdated += (sender, args) => eventFired = true;

            // Act
            await _service.SaveCustomProfilesAsync(profiles);

            // Assert
            _mockProfileRepository.Verify(x => x.UpdateAsync(It.IsAny<GameProfile>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
            eventFired.Should().BeTrue();
        }

        [Fact]
        public async Task SaveCustomProfilesAsync_WhenRepositoryThrows_PropagatesException()
        {
            // Arrange
            var profiles = new List<IGameProfile>
            {
                new GameProfile { Id = "profile1", Name = "Profile 1" }
            };

            _mockProfileRepository.Setup(x => x.UpdateAsync(It.IsAny<GameProfile>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("Repository error"));

            // Act & Assert
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                _service.SaveCustomProfilesAsync(profiles));

            exception.Message.Should().Be("Repository error");
        }

        #endregion

        #region LoadCustomProfilesAsync Tests

        [Fact]
        public async Task LoadCustomProfilesAsync_WithValidProfiles_ReturnsOrderedProfiles()
        {
            // Arrange
            var profiles = new List<IGameProfile>
            {
                new GameProfile { Id = "profile2", Name = "Profile 2", DisplayOrder = 2 },
                new GameProfile { Id = "profile1", Name = "Profile 1", DisplayOrder = 1 },
                new GameProfile { Id = "profile3", Name = "Profile 3", DisplayOrder = 3 }
            };

            _mockProfileRepository.Setup(x => x.GetAllAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(profiles);

            // Act
            var result = await _service.LoadCustomProfilesAsync();

            // Assert
            result.Should().HaveCount(3);
            var orderedResult = result.ToList();
            orderedResult[0].DisplayOrder.Should().Be(1);
            orderedResult[1].DisplayOrder.Should().Be(2);
            orderedResult[2].DisplayOrder.Should().Be(3);
        }

        [Fact]
        public async Task LoadCustomProfilesAsync_WhenRepositoryThrows_ReturnsEmptyCollection()
        {
            // Arrange
            _mockProfileRepository.Setup(x => x.GetAllAsync(It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("Repository error"));

            // Act
            var result = await _service.LoadCustomProfilesAsync();

            // Assert
            result.Should().BeEmpty();
        }

        #endregion

        #region Event Tests

        [Fact]
        public void ProfilesUpdated_EventSubscription_WorksCorrectly()
        {
            // Arrange
            var eventFired = false;
            IGameProfileManagerService.ProfilesUpdatedEventArgs? receivedArgs = null;

            _service.ProfilesUpdated += (sender, args) =>
            {
                eventFired = true;
                receivedArgs = args;
            };

            var profile = new GameProfile { Id = "test-profile" };

            _mockProfileRepository.Setup(x => x.GetByIdAsync(profile.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync((IGameProfile?)null);
            _mockProfileRepository.Setup(x => x.GetAllAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<IGameProfile>());

            // Act
            _service.SaveProfileAsync(profile, "test-source").Wait();

            // Assert
            eventFired.Should().BeTrue();
            receivedArgs.Should().NotBeNull();
            receivedArgs!.Source.Should().Be("test-source");
        }

        #endregion

        #region Error Handling and Logging Tests

        [Fact]
        public async Task GetProfilesAsync_WhenExceptionOccurs_LogsError()
        {
            // Arrange
            _mockProfileRepository.Setup(x => x.GetAllAsync(It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("Repository error"));

            // Act
            await _service.GetProfilesAsync();

            // Assert
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Error getting all profiles")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task CreateProfileFromVersionAsync_WhenExceptionOccurs_LogsErrorAndRethrows()
        {
            // Arrange
            var gameVersion = new GameVersion { Id = "test-version" };

            _mockProfileFactory.Setup(x => x.CreateFromVersion(gameVersion))
                .Throws(new InvalidOperationException("Factory error"));

            // Act & Assert
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                _service.CreateProfileFromVersionAsync(gameVersion));

            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Error creating profile from version test-version")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);

            exception.Message.Should().Be("Factory error");
        }

        #endregion
    }
}
