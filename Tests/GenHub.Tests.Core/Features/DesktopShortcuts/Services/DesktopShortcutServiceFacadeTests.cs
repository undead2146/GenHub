using System;
using System.Threading;
using System.Threading.Tasks;
using GenHub.Core.Interfaces;
using GenHub.Core.Interfaces.DesktopShortcuts;
using GenHub.Core.Models.AdvancedLauncher;
using GenHub.Core.Models.Results;
using GenHub.Features.DesktopShortcuts.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace GenHub.Tests.Core.Features.DesktopShortcuts.Services
{
    public class DesktopShortcutServiceFacadeTests
    {
        private readonly Mock<IShortcutPlatformService> _mockPlatformService;
        private readonly Mock<IShortcutCommandBuilder> _mockCommandBuilder;
        private readonly Mock<IShortcutIconExtractor> _mockIconExtractor;
        private readonly Mock<IGameProfileManagerService> _mockProfileService;
        private readonly Mock<ILogger<DesktopShortcutServiceFacade>> _mockLogger;
        private readonly DesktopShortcutServiceFacade _facade;

        public DesktopShortcutServiceFacadeTests()
        {
            _mockPlatformService = new Mock<IShortcutPlatformService>();
            _mockCommandBuilder = new Mock<IShortcutCommandBuilder>();
            _mockIconExtractor = new Mock<IShortcutIconExtractor>();
            _mockProfileService = new Mock<IGameProfileManagerService>();
            _mockLogger = new Mock<ILogger<DesktopShortcutServiceFacade>>();
            
            _facade = new DesktopShortcutServiceFacade(
                _mockPlatformService.Object,
                _mockCommandBuilder.Object,
                _mockIconExtractor.Object,
                _mockProfileService.Object,
                _mockLogger.Object);
        }

        [Fact]
        public async Task CreateShortcutAsync_WithValidProfileId_ShouldSucceed()
        {
            // Arrange
            var profileId = "test-profile-id";
            var config = new ShortcutConfiguration
            {
                Name = "Test Game"
            };

            _mockPlatformService.Setup(x => x.CreateShortcutAsync(It.IsAny<ShortcutConfiguration>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(OperationResult.Succeeded());

            // Act
            var result = await _facade.CreateShortcutAsync(profileId, config);

            // Assert
            Assert.True(result.Success);
        }        [Fact]
        public async Task CreateShortcutAsync_WithEmptyProfileId_ShouldReturnError()
        {
            // Act
            var result = await _facade.CreateShortcutAsync(string.Empty);

            // Assert            Assert.False(result.Success);
            Assert.Contains("Profile ID cannot be empty", result.Message);
        }
    }
}
