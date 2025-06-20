using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using GenHub.Core.Interfaces;
using GenHub.Core.Interfaces.GameVersions;
using GenHub.Core.Models.GameProfiles;
using GenHub.Core.Models.Results;
using GenHub.Features.GameVersions.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace GenHub.Tests.Core.Features.GameVersions.Services
{
    public class GameVersionServiceFacadeTests
    {
        private readonly Mock<ILogger<GameVersionServiceFacade>> _mockLogger;
        private readonly Mock<IGameVersionManager> _mockVersionManager;
        private readonly Mock<IGameVersionDiscoveryService> _mockDiscoveryService;
        private readonly Mock<IGameLauncherService> _mockGameLauncherService;
        private readonly GameVersionServiceFacade _facade;

        public GameVersionServiceFacadeTests()
        {
            _mockLogger = new Mock<ILogger<GameVersionServiceFacade>>();
            _mockVersionManager = new Mock<IGameVersionManager>();
            _mockDiscoveryService = new Mock<IGameVersionDiscoveryService>();
            _mockGameLauncherService = new Mock<IGameLauncherService>();

            _facade = new GameVersionServiceFacade(
                _mockLogger.Object,
                _mockVersionManager.Object,
                _mockDiscoveryService.Object,
                _mockGameLauncherService.Object);
        }

        #region Constructor Tests

        [Fact]
        public void Constructor_WithNullLogger_ThrowsArgumentNullException()
        {
            // Act & Assert
            var exception = Assert.Throws<ArgumentNullException>(() =>
                new GameVersionServiceFacade(
                    null!,
                    _mockVersionManager.Object,
                    _mockDiscoveryService.Object,
                    _mockGameLauncherService.Object));

            exception.ParamName.Should().Be("logger");
        }

        [Fact]
        public void Constructor_WithNullVersionManager_ThrowsArgumentNullException()
        {
            // Act & Assert
            var exception = Assert.Throws<ArgumentNullException>(() =>
                new GameVersionServiceFacade(
                    _mockLogger.Object,
                    null!,
                    _mockDiscoveryService.Object,
                    _mockGameLauncherService.Object));

            exception.ParamName.Should().Be("versionManager");
        }

        [Fact]
        public void Constructor_WithNullDiscoveryService_ThrowsArgumentNullException()
        {
            // Act & Assert
            var exception = Assert.Throws<ArgumentNullException>(() =>
                new GameVersionServiceFacade(
                    _mockLogger.Object,
                    _mockVersionManager.Object,
                    null!,
                    _mockGameLauncherService.Object));

            exception.ParamName.Should().Be("discoveryService");
        }

        [Fact]
        public void Constructor_WithNullGameLauncherService_ThrowsArgumentNullException()
        {
            // Act & Assert
            var exception = Assert.Throws<ArgumentNullException>(() =>
                new GameVersionServiceFacade(
                    _mockLogger.Object,
                    _mockVersionManager.Object,
                    _mockDiscoveryService.Object,
                    null!));

            exception.ParamName.Should().Be("gameLauncherService");
        }

        [Fact]
        public void Constructor_WithValidParameters_CreatesInstanceAndLogsInitialization()
        {
            // Act & Assert
            _facade.Should().NotBeNull();

            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("GameVersionServiceFacade initialized (facade)")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        #endregion

        #region GetVersionsStoragePath Tests

        [Fact]
        public void GetVersionsStoragePath_DelegatesToVersionManager()
        {
            // Arrange
            var expectedPath = @"C:\Games\Versions";
            _mockVersionManager.Setup(x => x.GetVersionsStoragePath())
                .Returns(expectedPath);

            // Act
            var result = _facade.GetVersionsStoragePath();

            // Assert
            result.Should().Be(expectedPath);
            _mockVersionManager.Verify(x => x.GetVersionsStoragePath(), Times.Once);
        }

        #endregion

        #region GetInstalledVersionsAsync Tests

        [Fact]
        public async Task GetInstalledVersionsAsync_DelegatesToVersionManager()
        {
            // Arrange
            var expectedVersions = new List<GameVersion>
            {
                new GameVersion { Id = "version1", Name = "Version 1" },
                new GameVersion { Id = "version2", Name = "Version 2" }
            };

            _mockVersionManager.Setup(x => x.GetInstalledVersionsAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedVersions);

            // Act
            var result = await _facade.GetInstalledVersionsAsync();

            // Assert
            result.Should().BeEquivalentTo(expectedVersions);
            _mockVersionManager.Verify(x => x.GetInstalledVersionsAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task GetInstalledVersionsAsync_PassesCancellationToken()
        {
            // Arrange
            var cancellationToken = new CancellationToken();
            _mockVersionManager.Setup(x => x.GetInstalledVersionsAsync(cancellationToken))
                .ReturnsAsync(new List<GameVersion>());

            // Act
            await _facade.GetInstalledVersionsAsync(cancellationToken);

            // Assert
            _mockVersionManager.Verify(x => x.GetInstalledVersionsAsync(cancellationToken), Times.Once);
        }

        #endregion

        #region GetVersionByIdAsync Tests

        [Fact]
        public async Task GetVersionByIdAsync_WithValidId_DelegatesToVersionManager()
        {
            // Arrange
            var versionId = "test-version-id";
            var expectedVersion = new GameVersion { Id = versionId, Name = "Test Version" };

            _mockVersionManager.Setup(x => x.GetVersionByIdAsync(versionId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedVersion);

            // Act
            var result = await _facade.GetVersionByIdAsync(versionId);

            // Assert
            result.Should().Be(expectedVersion);
            _mockVersionManager.Verify(x => x.GetVersionByIdAsync(versionId, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task GetVersionByIdAsync_WithNonExistentId_ReturnsNull()
        {
            // Arrange
            var versionId = "non-existent-id";

            _mockVersionManager.Setup(x => x.GetVersionByIdAsync(versionId, It.IsAny<CancellationToken>()))
                .ReturnsAsync((GameVersion?)null);

            // Act
            var result = await _facade.GetVersionByIdAsync(versionId);

            // Assert
            result.Should().BeNull();
        }

        #endregion

        #region DiscoverVersionsAsync Tests

        [Fact]
        public async Task DiscoverVersionsAsync_DelegatesToDiscoveryService()
        {
            // Arrange
            var expectedVersions = new List<GameVersion>
            {
                new GameVersion { Id = "discovered1", Name = "Discovered 1" },
                new GameVersion { Id = "discovered2", Name = "Discovered 2" }
            };

            _mockDiscoveryService.Setup(x => x.DiscoverVersionsAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedVersions);

            // Act
            var result = await _facade.DiscoverVersionsAsync();

            // Assert
            result.Should().BeEquivalentTo(expectedVersions);
            _mockDiscoveryService.Verify(x => x.DiscoverVersionsAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task DiscoverVersionsAsync_PassesCancellationToken()
        {
            // Arrange
            var cancellationToken = new CancellationToken();
            _mockDiscoveryService.Setup(x => x.DiscoverVersionsAsync(cancellationToken))
                .ReturnsAsync(new List<GameVersion>());

            // Act
            await _facade.DiscoverVersionsAsync(cancellationToken);

            // Assert
            _mockDiscoveryService.Verify(x => x.DiscoverVersionsAsync(cancellationToken), Times.Once);
        }

        #endregion

        #region SaveVersionAsync Tests

        [Fact]
        public async Task SaveVersionAsync_WithValidVersion_DelegatesToVersionManager()
        {
            // Arrange
            var version = new GameVersion { Id = "test-version", Name = "Test Version" };
            _mockVersionManager.Setup(x => x.SaveVersionAsync(version, It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            // Act
            var result = await _facade.SaveVersionAsync(version);

            // Assert
            result.Should().BeTrue();
            _mockVersionManager.Verify(x => x.SaveVersionAsync(version, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task SaveVersionAsync_WhenSaveFails_ReturnsFalse()
        {
            // Arrange
            var version = new GameVersion { Id = "test-version", Name = "Test Version" };
            _mockVersionManager.Setup(x => x.SaveVersionAsync(version, It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);

            // Act
            var result = await _facade.SaveVersionAsync(version);

            // Assert
            result.Should().BeFalse();
        }

        #endregion

        #region UpdateVersionAsync Tests

        [Fact]
        public async Task UpdateVersionAsync_WithValidVersion_DelegatesToVersionManager()
        {
            // Arrange
            var version = new GameVersion { Id = "test-version", Name = "Updated Version" };
            _mockVersionManager.Setup(x => x.UpdateVersionAsync(version, It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            // Act
            var result = await _facade.UpdateVersionAsync(version);

            // Assert
            result.Should().BeTrue();
            _mockVersionManager.Verify(x => x.UpdateVersionAsync(version, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task UpdateVersionAsync_WhenUpdateFails_ReturnsFalse()
        {
            // Arrange
            var version = new GameVersion { Id = "test-version", Name = "Updated Version" };
            _mockVersionManager.Setup(x => x.UpdateVersionAsync(version, It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);

            // Act
            var result = await _facade.UpdateVersionAsync(version);

            // Assert
            result.Should().BeFalse();
        }

        #endregion

        #region DeleteVersionAsync Tests

        [Fact]
        public async Task DeleteVersionAsync_WithValidId_DelegatesToVersionManager()
        {
            // Arrange
            var versionId = "test-version-id";
            _mockVersionManager.Setup(x => x.DeleteVersionAsync(versionId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            // Act
            var result = await _facade.DeleteVersionAsync(versionId);

            // Assert
            result.Should().BeTrue();
            _mockVersionManager.Verify(x => x.DeleteVersionAsync(versionId, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task DeleteVersionAsync_WhenDeleteFails_ReturnsFalse()
        {
            // Arrange
            var versionId = "test-version-id";
            _mockVersionManager.Setup(x => x.DeleteVersionAsync(versionId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);

            // Act
            var result = await _facade.DeleteVersionAsync(versionId);

            // Assert
            result.Should().BeFalse();
        }

        #endregion

        #region ValidateVersionAsync Tests

        [Fact]
        public async Task ValidateVersionAsync_WithValidVersion_DelegatesToDiscoveryService()
        {
            // Arrange
            var version = new GameVersion { Id = "test-version", ExecutablePath = @"C:\Games\Test\test.exe" };
            _mockDiscoveryService.Setup(x => x.ValidateVersionAsync(version, It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            // Act
            var result = await _facade.ValidateVersionAsync(version);

            // Assert
            result.Should().BeTrue();
            _mockDiscoveryService.Verify(x => x.ValidateVersionAsync(version, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task ValidateVersionAsync_WithInvalidVersion_ReturnsFalse()
        {
            // Arrange
            var version = new GameVersion { Id = "invalid-version", ExecutablePath = @"C:\NonExistent\test.exe" };
            _mockDiscoveryService.Setup(x => x.ValidateVersionAsync(version, It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);

            // Act
            var result = await _facade.ValidateVersionAsync(version);

            // Assert
            result.Should().BeFalse();
        }

        #endregion

        #region LaunchVersionAsync Tests

        [Fact]
        public async Task LaunchVersionAsync_WithValidProfile_DelegatesToGameLauncherService()
        {
            // Arrange
            var profile = new GameProfile { Id = "test-profile", ExecutablePath = @"C:\Games\Test\test.exe" };
            var expectedResult = OperationResult.Succeeded("Launch successful");

            _mockGameLauncherService.Setup(x => x.LaunchVersionAsync(profile))
                .ReturnsAsync(expectedResult);

            // Act
            var result = await _facade.LaunchVersionAsync(profile);

            // Assert
            result.Should().Be(expectedResult);
            result.Success.Should().BeTrue();
            _mockGameLauncherService.Verify(x => x.LaunchVersionAsync(profile), Times.Once);
        }

        [Fact]
        public async Task LaunchVersionAsync_WhenLaunchFails_ReturnsFailureResult()
        {
            // Arrange
            var profile = new GameProfile { Id = "test-profile", ExecutablePath = @"C:\NonExistent\test.exe" };
            var expectedResult = OperationResult.Failed("Launch failed");

            _mockGameLauncherService.Setup(x => x.LaunchVersionAsync(profile))
                .ReturnsAsync(expectedResult);

            // Act
            var result = await _facade.LaunchVersionAsync(profile);

            // Assert
            result.Should().Be(expectedResult);            result.Success.Should().BeFalse();
            result.Message.Should().Be("Launch failed");
        }

        #endregion

        #region GetDetectedVersionsAsync Tests

        [Fact]
        public async Task GetDetectedVersionsAsync_DelegatesToDiscoveryService()
        {
            // Arrange
            var expectedVersions = new List<GameVersion>
            {
                new GameVersion { Id = "detected1", Name = "Detected 1" },
                new GameVersion { Id = "detected2", Name = "Detected 2" }
            };

            _mockDiscoveryService.Setup(x => x.GetDetectedVersionsAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedVersions);

            // Act
            var result = await _facade.GetDetectedVersionsAsync();

            // Assert
            result.Should().BeEquivalentTo(expectedVersions);
            _mockDiscoveryService.Verify(x => x.GetDetectedVersionsAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        #endregion

        #region GetDefaultGameVersionsAsync Tests

        [Fact]
        public async Task GetDefaultGameVersionsAsync_DelegatesToDiscoveryService()
        {
            // Arrange
            var expectedVersions = new List<GameVersion>
            {                new GameVersion { Id = "default1", Name = "Default Steam", SourceType = GameInstallationType.Steam },
                new GameVersion { Id = "default2", Name = "Default Origin", SourceType = GameInstallationType.EaApp }
            };

            _mockDiscoveryService.Setup(x => x.GetDefaultGameVersionsAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedVersions);

            // Act
            var result = await _facade.GetDefaultGameVersionsAsync();

            // Assert
            result.Should().BeEquivalentTo(expectedVersions);
            _mockDiscoveryService.Verify(x => x.GetDefaultGameVersionsAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        #endregion

        #region ScanDirectoryForVersionsAsync Tests

        [Fact]
        public async Task ScanDirectoryForVersionsAsync_WithValidDirectory_DelegatesToDiscoveryService()
        {
            // Arrange
            var directoryPath = @"C:\Games";
            var expectedVersions = new List<GameVersion>
            {
                new GameVersion { Id = "scanned1", Name = "Scanned 1", InstallPath = @"C:\Games\Game1" },
                new GameVersion { Id = "scanned2", Name = "Scanned 2", InstallPath = @"C:\Games\Game2" }
            };

            _mockDiscoveryService.Setup(x => x.ScanDirectoryForVersionsAsync(directoryPath, It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedVersions);

            // Act
            var result = await _facade.ScanDirectoryForVersionsAsync(directoryPath);

            // Assert
            result.Should().BeEquivalentTo(expectedVersions);
            _mockDiscoveryService.Verify(x => x.ScanDirectoryForVersionsAsync(directoryPath, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task ScanDirectoryForVersionsAsync_WithEmptyDirectory_ReturnsEmptyCollection()
        {
            // Arrange
            var directoryPath = @"C:\EmptyDirectory";

            _mockDiscoveryService.Setup(x => x.ScanDirectoryForVersionsAsync(directoryPath, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<GameVersion>());

            // Act
            var result = await _facade.ScanDirectoryForVersionsAsync(directoryPath);

            // Assert
            result.Should().BeEmpty();
        }

        #endregion

        #region Integration Tests

        [Fact]
        public async Task MultipleOperations_WorkCorrectlyTogether()
        {
            // Arrange
            var version = new GameVersion { Id = "integration-test", Name = "Integration Test Version" };
            var savedVersions = new List<GameVersion> { version };

            _mockVersionManager.Setup(x => x.SaveVersionAsync(version, It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);
            _mockVersionManager.Setup(x => x.GetInstalledVersionsAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(savedVersions);
            _mockVersionManager.Setup(x => x.GetVersionByIdAsync(version.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(version);

            // Act
            var saveResult = await _facade.SaveVersionAsync(version);
            var installedVersions = await _facade.GetInstalledVersionsAsync();
            var retrievedVersion = await _facade.GetVersionByIdAsync(version.Id);

            // Assert
            saveResult.Should().BeTrue();
            installedVersions.Should().Contain(version);
            retrievedVersion.Should().Be(version);
        }

        #endregion

        #region Error Handling Tests

        [Fact]
        public async Task GetInstalledVersionsAsync_WhenVersionManagerThrows_PropagatesException()
        {
            // Arrange
            _mockVersionManager.Setup(x => x.GetInstalledVersionsAsync(It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("Version manager error"));

            // Act & Assert
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                _facade.GetInstalledVersionsAsync());

            exception.Message.Should().Be("Version manager error");
        }

        [Fact]
        public async Task DiscoverVersionsAsync_WhenDiscoveryServiceThrows_PropagatesException()
        {
            // Arrange
            _mockDiscoveryService.Setup(x => x.DiscoverVersionsAsync(It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("Discovery service error"));

            // Act & Assert
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                _facade.DiscoverVersionsAsync());

            exception.Message.Should().Be("Discovery service error");
        }

        [Fact]
        public async Task LaunchVersionAsync_WhenGameLauncherServiceThrows_PropagatesException()
        {
            // Arrange
            var profile = new GameProfile { Id = "test-profile" };

            _mockGameLauncherService.Setup(x => x.LaunchVersionAsync(profile))
                .ThrowsAsync(new InvalidOperationException("Game launcher error"));

            // Act & Assert
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                _facade.LaunchVersionAsync(profile));

            exception.Message.Should().Be("Game launcher error");
        }

        #endregion

        #region Cancellation Token Tests

        [Fact]
        public async Task AllAsyncMethods_PassCancellationTokenCorrectly()
        {
            // Arrange
            var cancellationToken = new CancellationToken();
            var version = new GameVersion { Id = "test-version" };
            var profile = new GameProfile { Id = "test-profile" };

            _mockVersionManager.Setup(x => x.GetInstalledVersionsAsync(cancellationToken))
                .ReturnsAsync(new List<GameVersion>());
            _mockVersionManager.Setup(x => x.GetVersionByIdAsync("test-id", cancellationToken))
                .ReturnsAsync((GameVersion?)null);
            _mockVersionManager.Setup(x => x.SaveVersionAsync(version, cancellationToken))
                .ReturnsAsync(true);
            _mockVersionManager.Setup(x => x.UpdateVersionAsync(version, cancellationToken))
                .ReturnsAsync(true);
            _mockVersionManager.Setup(x => x.DeleteVersionAsync("test-id", cancellationToken))
                .ReturnsAsync(true);

            _mockDiscoveryService.Setup(x => x.DiscoverVersionsAsync(cancellationToken))
                .ReturnsAsync(new List<GameVersion>());
            _mockDiscoveryService.Setup(x => x.ValidateVersionAsync(version, cancellationToken))
                .ReturnsAsync(true);
            _mockDiscoveryService.Setup(x => x.GetDetectedVersionsAsync(cancellationToken))
                .ReturnsAsync(new List<GameVersion>());
            _mockDiscoveryService.Setup(x => x.GetDefaultGameVersionsAsync(cancellationToken))
                .ReturnsAsync(new List<GameVersion>());
            _mockDiscoveryService.Setup(x => x.ScanDirectoryForVersionsAsync("test-path", cancellationToken))
                .ReturnsAsync(new List<GameVersion>());

            _mockGameLauncherService.Setup(x => x.LaunchVersionAsync(profile))
                .ReturnsAsync(OperationResult.Succeeded("Success"));

            // Act
            await _facade.GetInstalledVersionsAsync(cancellationToken);
            await _facade.GetVersionByIdAsync("test-id", cancellationToken);
            await _facade.SaveVersionAsync(version, cancellationToken);
            await _facade.UpdateVersionAsync(version, cancellationToken);
            await _facade.DeleteVersionAsync("test-id", cancellationToken);
            await _facade.DiscoverVersionsAsync(cancellationToken);
            await _facade.ValidateVersionAsync(version, cancellationToken);
            await _facade.GetDetectedVersionsAsync(cancellationToken);
            await _facade.GetDefaultGameVersionsAsync(cancellationToken);
            await _facade.ScanDirectoryForVersionsAsync("test-path", cancellationToken);
            await _facade.LaunchVersionAsync(profile, cancellationToken);

            // Assert - Verify all methods were called with the correct cancellation token
            _mockVersionManager.Verify(x => x.GetInstalledVersionsAsync(cancellationToken), Times.Once);
            _mockVersionManager.Verify(x => x.GetVersionByIdAsync("test-id", cancellationToken), Times.Once);
            _mockVersionManager.Verify(x => x.SaveVersionAsync(version, cancellationToken), Times.Once);
            _mockVersionManager.Verify(x => x.UpdateVersionAsync(version, cancellationToken), Times.Once);
            _mockVersionManager.Verify(x => x.DeleteVersionAsync("test-id", cancellationToken), Times.Once);

            _mockDiscoveryService.Verify(x => x.DiscoverVersionsAsync(cancellationToken), Times.Once);
            _mockDiscoveryService.Verify(x => x.ValidateVersionAsync(version, cancellationToken), Times.Once);
            _mockDiscoveryService.Verify(x => x.GetDetectedVersionsAsync(cancellationToken), Times.Once);
            _mockDiscoveryService.Verify(x => x.GetDefaultGameVersionsAsync(cancellationToken), Times.Once);
            _mockDiscoveryService.Verify(x => x.ScanDirectoryForVersionsAsync("test-path", cancellationToken), Times.Once);
        }

        #endregion
    }
}
