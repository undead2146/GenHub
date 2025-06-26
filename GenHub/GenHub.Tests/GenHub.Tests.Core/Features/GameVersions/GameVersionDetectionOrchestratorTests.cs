using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using GenHub.Core;
using GenHub.Core.Interfaces.GameInstallations;
using GenHub.Core.Interfaces.GameVersions;
using GenHub.Core.Models;
using GenHub.Core.Models.Results;
using GenHub.Features.GameVersions;
using Moq;
using Xunit;

namespace GenHub.Tests.Core.Features.GameVersions
{
    public class GameVersionDetectionOrchestratorTests
    {
        [Fact]
        public async Task DetectAllVersionsAsync_InstallationDetectionFails_ReturnsFailed()
        {
            var mockInst = new Mock<IGameInstallationDetectionOrchestrator>();
            mockInst.Setup(x => x.DetectAllInstallationsAsync(It.IsAny<CancellationToken>()))
                    .ReturnsAsync(DetectionResult<GameInstallation>.Failed("install error"));

            var mockVer = new Mock<IGameVersionDetector>();
            var svc = new GameVersionDetectionOrchestrator(mockInst.Object, mockVer.Object);

            var result = await svc.DetectAllVersionsAsync();

            Assert.False(result.Success);
            Assert.Contains(result.Errors, e => e.Contains("install error"));
        }

        [Fact]
        public async Task DetectAllVersionsAsync_VersionDetectionSucceeds_ReturnsVersions()
        {
            var installations = new List<GameInstallation>
            {
                new GameInstallation { Id = "I1", InstallationType = GameInstallationType.Steam, },
            };
            var mockInst = new Mock<IGameInstallationDetectionOrchestrator>();
            mockInst.Setup(x => x.DetectAllInstallationsAsync(It.IsAny<CancellationToken>()))
                    .ReturnsAsync(DetectionResult<GameInstallation>.Succeeded(
                        installations, TimeSpan.Zero));

            var versions = new List<GameVersion>
            {
                new GameVersion
                {
                    Id = "V1",
                    Name = "Generals (Steam)",
                    ExecutablePath = @"C:\\Games\\Generals\\generals.exe",
                    WorkingDirectory = @"C:\\Games\\Generals",
                    GameType = "Generals",
                    IsZeroHour = false,
                    BaseInstallationId = "I1",
                },
            };
            var mockVer = new Mock<IGameVersionDetector>();
            mockVer.Setup(x => x.DetectVersionsFromInstallationsAsync(
                        installations, It.IsAny<CancellationToken>()))
                   .ReturnsAsync(DetectionResult<GameVersion>.Succeeded(
                        versions, TimeSpan.Zero));

            var svc = new GameVersionDetectionOrchestrator(mockInst.Object, mockVer.Object);
            var result = await svc.DetectAllVersionsAsync();

            Assert.True(result.Success);
            Assert.Equal(versions, result.Items);
        }
    }
}
