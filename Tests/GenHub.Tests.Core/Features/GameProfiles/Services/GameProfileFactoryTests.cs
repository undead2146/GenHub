using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using GenHub.Core.Interfaces;
using GenHub.Core.Interfaces.GameVersions;
using GenHub.Core.Models;
using GenHub.Core.Models.GameProfiles;
using GenHub.Core.Models.SourceMetadata;
using GenHub.Features.GameProfiles.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace GenHub.Tests.Core.Features.GameProfiles.Services
{
    public class GameProfileFactoryTests
    {
        private readonly Mock<ILogger<GameProfileFactory>> _mockLogger;
        private readonly Mock<ProfileResourceService> _mockResourceService;
        private readonly Mock<ProfileMetadataService> _mockMetadataService;
        private readonly Mock<IGameExecutableLocator> _mockExecutableLocator;
        private readonly GameProfileFactory _factory;

        public GameProfileFactoryTests()
        {
            _mockLogger = new Mock<ILogger<GameProfileFactory>>();
            _mockResourceService = new Mock<ProfileResourceService>();
            _mockMetadataService = new Mock<ProfileMetadataService>();
            _mockExecutableLocator = new Mock<IGameExecutableLocator>();
            
            _factory = new GameProfileFactory(
                _mockLogger.Object,
                _mockResourceService.Object,
                _mockMetadataService.Object,
                _mockExecutableLocator.Object);
        }

        #region Constructor Tests

        [Fact]
        public void Constructor_WithNullLogger_ThrowsArgumentNullException()
        {
            // Act & Assert
            var exception = Assert.Throws<ArgumentNullException>(() => 
                new GameProfileFactory(
                    null!,
                    _mockResourceService.Object,
                    _mockMetadataService.Object,
                    _mockExecutableLocator.Object));
            
            exception.ParamName.Should().Be("logger");
        }

        [Fact]
        public void Constructor_WithNullResourceService_ThrowsArgumentNullException()
        {
            // Act & Assert
            var exception = Assert.Throws<ArgumentNullException>(() => 
                new GameProfileFactory(
                    _mockLogger.Object,
                    null!,
                    _mockMetadataService.Object,
                    _mockExecutableLocator.Object));
            
            exception.ParamName.Should().Be("resourceService");
        }

        [Fact]
        public void Constructor_WithNullMetadataService_ThrowsArgumentNullException()
        {
            // Act & Assert
            var exception = Assert.Throws<ArgumentNullException>(() => 
                new GameProfileFactory(
                    _mockLogger.Object,
                    _mockResourceService.Object,
                    null!,
                    _mockExecutableLocator.Object));
            
            exception.ParamName.Should().Be("metadataService");
        }

        [Fact]
        public void Constructor_WithNullExecutableLocator_ThrowsArgumentNullException()
        {
            // Act & Assert
            var exception = Assert.Throws<ArgumentNullException>(() => 
                new GameProfileFactory(
                    _mockLogger.Object,
                    _mockResourceService.Object,
                    _mockMetadataService.Object,
                    null!));
            
            exception.ParamName.Should().Be("executableLocator");
        }

        [Fact]
        public void Constructor_WithValidParameters_CreatesInstance()
        {
            // Act & Assert
            _factory.Should().NotBeNull();
        }

        #endregion

        #region CreateFromVersion Tests

        [Fact]
        public void CreateFromVersion_WithNullVersion_ThrowsArgumentNullException()
        {
            // Act & Assert
            var exception = Assert.Throws<ArgumentNullException>(() => 
                _factory.CreateFromVersion(null!));
            
            exception.ParamName.Should().Be("version");
        }

        [Fact]
        public void CreateFromVersion_WithValidGeneralsVersion_CreatesProfileWithCorrectProperties()
        {
            // Arrange
            var gameVersion = new GameVersion
            {
                Id = "test-version-id",
                Name = "Test Generals Version",
                ExecutablePath = @"C:\Games\Generals\generals.exe",                InstallPath = @"C:\Games\Generals",
                IsZeroHour = false,
                InstallationType = GameInstallationType.GitHubArtifact
            };var mockIcon = new ProfileResourceItem { Path = "test-icon.png" };
            var mockCover = new ProfileResourceItem { Path = "test-cover.png" };
            var testDescription = "Test game description";

            _mockResourceService.Setup(x => x.FindIconForGameType("Generals"))
                .Returns(mockIcon);
            _mockResourceService.Setup(x => x.FindCoverForGameType("Generals"))
                .Returns(mockCover);
            _mockMetadataService.Setup(x => x.GenerateGameDescription(gameVersion))
                .Returns(testDescription);

            // Act
            var result = _factory.CreateFromVersion(gameVersion);

            // Assert
            result.Should().NotBeNull();
            result.Id.Should().NotBeNullOrEmpty();
            result.Name.Should().Be("Test Generals Version");
            result.ExecutablePath.Should().Be(@"C:\Games\Generals\generals.exe");
            result.DataPath.Should().Be(@"C:\Games\Generals");
            result.IconPath.Should().Be("test-icon.png");
            result.CoverImagePath.Should().Be("test-cover.png");
            result.VersionId.Should().Be("test-version-id");
            result.IsCustomProfile.Should().BeTrue();
            result.IsDefaultProfile.Should().BeFalse();
            result.SourceType.Should().Be(GameInstallationType.GitHubArtifact);
            result.Description.Should().Be(testDescription);
            result.ColorValue.Should().NotBeNullOrEmpty();
        }

        [Fact]
        public void CreateFromVersion_WithValidZeroHourVersion_CreatesProfileWithCorrectProperties()
        {
            // Arrange
            var gameVersion = new GameVersion
            {
                Id = "test-zh-version-id",
                Name = "Test Zero Hour Version",
                ExecutablePath = @"C:\Games\ZeroHour\ZH.exe",
                InstallPath = @"C:\Games\ZeroHour",
                IsZeroHour = true,
                SourceType = GameInstallationType.Steam
            };            var mockIcon = new ProfileResourceItem { Path = "zh-icon.png" };
            var mockCover = new ProfileResourceItem { Path = "zh-cover.png" };
            var testDescription = "Test Zero Hour description";

            _mockResourceService.Setup(x => x.FindIconForGameType("Zero Hour"))
                .Returns(mockIcon);
            _mockResourceService.Setup(x => x.FindCoverForGameType("Zero Hour"))
                .Returns(mockCover);
            _mockMetadataService.Setup(x => x.GenerateGameDescription(gameVersion))
                .Returns(testDescription);

            // Act
            var result = _factory.CreateFromVersion(gameVersion);

            // Assert
            result.Should().NotBeNull();
            result.Name.Should().Be("Test Zero Hour Version");
            result.ExecutablePath.Should().Be(@"C:\Games\ZeroHour\ZH.exe");
            result.DataPath.Should().Be(@"C:\Games\ZeroHour");
            result.IconPath.Should().Be("zh-icon.png");
            result.CoverImagePath.Should().Be("zh-cover.png");
            result.VersionId.Should().Be("test-zh-version-id");
            result.SourceType.Should().Be(GameInstallationType.Steam);
            result.GameVariant.Should().Be(GameVariant.ZeroHour);
        }

        [Fact]
        public void CreateFromVersion_WithNoIcon_UsesDefaultIcon()
        {
            // Arrange
            var gameVersion = new GameVersion
            {
                Id = "test-version-id",
                Name = "Test Version",
                ExecutablePath = @"C:\Games\Test\test.exe",
                InstallPath = @"C:\Games\Test",
                IsZeroHour = false
            };            _mockResourceService.Setup(x => x.FindIconForGameType(It.IsAny<string>()))
                .Returns((ProfileResourceItem?)null);
            _mockResourceService.Setup(x => x.FindCoverForGameType(It.IsAny<string>()))
                .Returns((ProfileResourceItem?)null);

            // Act
            var result = _factory.CreateFromVersion(gameVersion);

            // Assert
            result.IconPath.Should().Be("avares://GenHub/Assets/icon-default.png");
            result.CoverImagePath.Should().Be("avares://GenHub/Assets/default-cover.png");
        }

        [Theory]
        [InlineData(true, "Zero Hour")]
        [InlineData(false, "Generals")]
        public void CreateFromVersion_DeterminesCorrectGameType(bool isZeroHour, string expectedGameType)
        {
            // Arrange
            var gameVersion = new GameVersion
            {
                Id = "test-version-id",
                Name = "Test Version",
                ExecutablePath = @"C:\Games\Test\test.exe",
                InstallPath = @"C:\Games\Test",
                IsZeroHour = isZeroHour
            };

            // Act
            _factory.CreateFromVersion(gameVersion);

            // Assert
            _mockResourceService.Verify(x => x.FindIconForGameType(expectedGameType), Times.Once);
            _mockResourceService.Verify(x => x.FindCoverForGameType(expectedGameType), Times.Once);
        }

        #endregion

        #region PopulateGitHubMetadata Tests

        [Fact]
        public void PopulateGitHubMetadata_WithNullProfile_DoesNotThrow()
        {
            // Arrange
            var version = new GameVersion();

            // Act & Assert
            var action = () => _factory.PopulateGitHubMetadata(null!, version);
            action.Should().NotThrow();
        }

        [Fact]
        public void PopulateGitHubMetadata_WithNullVersion_DoesNotThrow()
        {
            // Arrange
            var profile = new GameProfile();

            // Act & Assert
            var action = () => _factory.PopulateGitHubMetadata(profile, null!);
            action.Should().NotThrow();
        }        [Fact]
        public void PopulateGitHubMetadata_WithValidGitHubMetadata_PopulatesProfile()
        {
            // Arrange
            var profile = new GameProfile { Id = "test-profile" };
            var artifact = new GitHubArtifact 
            { 
                Name = "test-artifact",
                Id = 123,
                SizeInBytes = 1024
            };
            
            var gitHubMetadata = new GitHubSourceMetadata
            {
                AssociatedArtifact = artifact
            };

            var version = new GameVersion
            {
                Id = "test-version",
                GitHubMetadata = gitHubMetadata
            };

            // Act
            _factory.PopulateGitHubMetadata(profile, version);

            // Assert
            profile.SourceSpecificMetadata.Should().NotBeNull();
            profile.SourceSpecificMetadata.Should().BeOfType<GitHubSourceMetadata>();
            
            var populatedMetadata = profile.SourceSpecificMetadata as GitHubSourceMetadata;
            populatedMetadata!.AssociatedArtifact.Should().NotBeNull();
            populatedMetadata.AssociatedArtifact!.Id.Should().Be(123);
            populatedMetadata.AssociatedArtifact.Name.Should().Be("test-artifact");
        }

        #endregion

        #region CreateFromExecutableAsync Tests

        [Fact]
        public async Task CreateFromExecutableAsync_WithNullExecutablePath_ThrowsArgumentNullException()
        {
            // Act & Assert
            var exception = await Assert.ThrowsAsync<ArgumentNullException>(() => 
                _factory.CreateFromExecutableAsync(null!, "Generals"));
            
            exception.ParamName.Should().Be("executablePath");
        }

        [Fact]
        public async Task CreateFromExecutableAsync_WithEmptyExecutablePath_ThrowsArgumentNullException()
        {
            // Act & Assert
            var exception = await Assert.ThrowsAsync<ArgumentNullException>(() => 
                _factory.CreateFromExecutableAsync(string.Empty, "Generals"));
            
            exception.ParamName.Should().Be("executablePath");
        }

        [Fact]
        public async Task CreateFromExecutableAsync_WithValidParameters_CreatesProfile()
        {
            // Arrange
            var executablePath = @"C:\Games\Generals\generals.exe";
            var gameType = "Generals";            var mockIcon = new ProfileResourceItem { Path = "generals-icon.png" };
            var mockCover = new ProfileResourceItem { Path = "generals-cover.png" };

            _mockResourceService.Setup(x => x.FindIconForGameType("Generals"))
                .Returns(mockIcon);
            _mockResourceService.Setup(x => x.FindCoverForGameType("Generals"))
                .Returns(mockCover);

            // Act
            var result = await _factory.CreateFromExecutableAsync(executablePath, gameType);

            // Assert
            result.Should().NotBeNull();
            result.Id.Should().NotBeNullOrEmpty();
            result.ExecutablePath.Should().Be(executablePath);
            result.IconPath.Should().Be("generals-icon.png");
            result.CoverImagePath.Should().Be("generals-cover.png");
            result.IsCustomProfile.Should().BeTrue();
        }

        #endregion

        #region CreateFromExecutablesAsync Tests

        [Fact]
        public async Task CreateFromExecutablesAsync_WithNullExecutablePaths_ThrowsArgumentNullException()
        {
            // Act & Assert
            var exception = await Assert.ThrowsAsync<ArgumentNullException>(() => 
                _factory.CreateFromExecutablesAsync(null!));
            
            exception.ParamName.Should().Be("executablePaths");
        }

        [Fact]
        public async Task CreateFromExecutablesAsync_WithEmptyList_ReturnsEmptyList()
        {
            // Arrange
            var executablePaths = new List<string>();

            // Act
            var result = await _factory.CreateFromExecutablesAsync(executablePaths);

            // Assert
            result.Should().NotBeNull();
            result.Should().BeEmpty();
        }

        [Fact]
        public async Task CreateFromExecutablesAsync_WithValidPaths_CreatesMultipleProfiles()
        {
            // Arrange
            var executablePaths = new List<string>
            {
                @"C:\Games\Generals\generals.exe",
                @"C:\Games\ZH\ZH.exe"
            };            var mockIcon = new ProfileResourceItem { Path = "test-icon.png" };
            var mockCover = new ProfileResourceItem { Path = "test-cover.png" };

            _mockResourceService.Setup(x => x.FindIconForGameType(It.IsAny<string>()))
                .Returns(mockIcon);
            _mockResourceService.Setup(x => x.FindCoverForGameType(It.IsAny<string>()))
                .Returns(mockCover);

            // Act
            var result = await _factory.CreateFromExecutablesAsync(executablePaths);

            // Assert
            result.Should().NotBeNull();
            result.Should().HaveCount(2);
            result.All(p => p.Id != null).Should().BeTrue();
            result.All(p => p.IsCustomProfile).Should().BeTrue();
        }

        #endregion

        #region NormalizeGameType Tests

        [Theory]
        [InlineData("generals", "Generals")]
        [InlineData("GENERALS", "Generals")]
        [InlineData("Generals", "Generals")]
        [InlineData("zero hour", "Zero Hour")]
        [InlineData("ZERO HOUR", "Zero Hour")]
        [InlineData("Zero Hour", "Zero Hour")]
        [InlineData("zh", "Zero Hour")]
        [InlineData("ZH", "Zero Hour")]
        [InlineData("unknown", "Generals")]
        [InlineData("", "Generals")]
        [InlineData(null, "Generals")]
        public void NormalizeGameType_WithVariousInputs_ReturnsExpectedOutput(string? input, string expected)
        {
            // Act
            var result = _factory.NormalizeGameType(input!);

            // Assert
            result.Should().Be(expected);
        }

        #endregion

        #region Error Handling Tests

        [Fact]
        public void CreateFromVersion_WhenResourceServiceThrows_PropagatesException()
        {
            // Arrange
            var gameVersion = new GameVersion
            {
                Id = "test-version-id",
                Name = "Test Version",
                ExecutablePath = @"C:\Games\Test\test.exe",
                InstallPath = @"C:\Games\Test",
                IsZeroHour = false
            };

            _mockResourceService.Setup(x => x.FindIconForGameType(It.IsAny<string>()))
                .Throws(new InvalidOperationException("Resource service error"));

            // Act & Assert
            var exception = Assert.Throws<InvalidOperationException>(() => 
                _factory.CreateFromVersion(gameVersion));
            
            exception.Message.Should().Be("Resource service error");
        }

        [Fact]
        public void CreateFromVersion_WhenMetadataServiceThrows_PropagatesException()
        {
            // Arrange
            var gameVersion = new GameVersion
            {
                Id = "test-version-id",
                Name = "Test Version",
                ExecutablePath = @"C:\Games\Test\test.exe",
                InstallPath = @"C:\Games\Test",
                IsZeroHour = false
            };

            _mockMetadataService.Setup(x => x.GenerateGameDescription(It.IsAny<GameVersion>()))
                .Throws(new InvalidOperationException("Metadata service error"));

            // Act & Assert
            var exception = Assert.Throws<InvalidOperationException>(() => 
                _factory.CreateFromVersion(gameVersion));
            
            exception.Message.Should().Be("Metadata service error");
        }

        #endregion

        #region Logging Tests

        [Fact]
        public void CreateFromVersion_LogsInformationMessage()
        {
            // Arrange
            var gameVersion = new GameVersion
            {
                Id = "test-version-id",
                Name = "Test Version",
                ExecutablePath = @"C:\Games\Test\test.exe",
                InstallPath = @"C:\Games\Test",
                IsZeroHour = false
            };

            // Act
            _factory.CreateFromVersion(gameVersion);

            // Assert
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Creating profile from version: test-version-id")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        #endregion
    }
}
