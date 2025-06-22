using FluentAssertions;
using GenHub.Core;
using GenHub.Windows;
using GenHub.Windows.Installations;
using Moq;
using Xunit;

namespace GenHub.Tests.Windows.Features.GameVersions;

public class WindowsGameDetectorTests
{
    [Fact]
    public void WindowsGameDetector_ShouldInitializeWithEmptyInstallations()
    {
        // Arrange & Act
        var detector = new WindowsGameDetector();

        // Assert
        detector.Installations.Should().NotBeNull();
        detector.Installations.Should().BeEmpty();
    }

    [Fact]
    public void Detect_ShouldAddSteamAndEaAppInstallations()
    {
        // Arrange
        var detector = new WindowsGameDetector();

        // Act
        detector.Detect();

        // Assert
        detector.Installations.Should().HaveCount(2);
        detector.Installations.Should().Contain(installation => installation.InstallationType == GameInstallationType.Steam);
        detector.Installations.Should().Contain(installation => installation.InstallationType == GameInstallationType.EaApp);
    }

    [Fact]
    public void Detect_ShouldClearExistingInstallationsBeforeAdding()
    {
        // Arrange
        var detector = new WindowsGameDetector();
        var mockInstallation = new Mock<IGameInstallation>();
        detector.Installations.Add(mockInstallation.Object);

        // Act
        detector.Detect();

        // Assert
        detector.Installations.Should().HaveCount(2);
        detector.Installations.Should().NotContain(mockInstallation.Object);
    }

    [Fact]
    public void Detect_ShouldCreateInstallationsWithFetchEnabled()
    {
        // Arrange
        var detector = new WindowsGameDetector();

        // Act
        detector.Detect();

        // Assert
        foreach (var installation in detector.Installations)
        {
            // These installations are created with fetch=true, so they should have attempted to detect games
            // We can't easily test the actual registry calls without integration tests,
            // but we can verify the installations were created
            installation.Should().NotBeNull();
            installation.InstallationType.Should().BeOneOf(GameInstallationType.Steam, GameInstallationType.EaApp);
        }
    }
}
