using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using GenHub.Core;
using GenHub.Core.Models;
using GenHub.Features.GameVersions;
using Xunit;

namespace GenHub.Tests.Core.Features.GameVersions
{
    public class GameExecutableLocatorTests : IDisposable
    {
        private readonly string _tempDir;

        public GameExecutableLocatorTests()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), $"gh-test-{Guid.NewGuid()}");
            Directory.CreateDirectory(_tempDir);
        }

        public void Dispose()
        {
            if (Directory.Exists(_tempDir))
                Directory.Delete(_tempDir, recursive: true);
        }

        [Fact]
        public async Task DetectVersionsFromInstallationsAsync_FindsGeneralsExe()
        {
            // Arrange
            var install = new GameInstallation
            {
                Id = "I1",
                InstallationType = GameInstallationType.Steam,
                InstallationPath = _tempDir,
            };
            var exePath = Path.Combine(_tempDir, "generals.exe");
            File.WriteAllText(exePath, "stub"); // create dummy exe

            var locator = new GameExecutableLocator();

            // Act
            var result = await locator.DetectVersionsFromInstallationsAsync(
                new[] { install });

            // Assert
            Assert.True(result.Success);
            var versions = result.Items;
            Assert.Single(versions);
            var v = versions[0];
            Assert.Equal(exePath, v.ExecutablePath);
            Assert.Equal(_tempDir, v.WorkingDirectory);
            Assert.Equal("Generals", v.GameType);
            Assert.False(v.IsZeroHour);
            Assert.Equal("I1", v.BaseInstallationId);
        }

        [Fact]
        public async Task DetectVersionsFromInstallationsAsync_NoExe_NoVersions()
        {
            var install = new GameInstallation
            {
                Id = "I1",
                InstallationType = GameInstallationType.Steam,
                InstallationPath = _tempDir,
            };
            var locator = new GameExecutableLocator();

            var result = await locator.DetectVersionsFromInstallationsAsync(
                new[] { install });

            Assert.True(result.Success);
            Assert.Empty(result.Items);
        }
    }
}
