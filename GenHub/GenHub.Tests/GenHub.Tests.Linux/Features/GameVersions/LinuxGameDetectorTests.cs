using FluentAssertions;
using GenHub.Core;
using GenHub.Linux;
using Moq;
using Xunit;

namespace GenHub.Tests.Linux.Features.GameVersions;

public class LinuxGameDetectorTests
{
    [Fact]
    public void LinuxGameDetector_ShouldInitializeWithEmptyInstallations()
    {
        // Arrange & Act
        var detector = new LinuxGameDetector();

        // Assert
        detector.Installations.Should().NotBeNull();
        detector.Installations.Should().BeEmpty();
    }

    [Fact]
    public void Detect_ShouldThrowNotImplementedException()
    {
        // Arrange
        var detector = new LinuxGameDetector();

        // Act & Assert
        var action = () => detector.Detect();
        action.Should().Throw<System.NotImplementedException>();
    }

    [Fact]
    public void Installations_ShouldBeModifiable()
    {
        // Arrange
        var detector = new LinuxGameDetector();
        var mockInstallation = new Mock<IGameInstallation>();

        // Act
        detector.Installations.Add(mockInstallation.Object);

        // Assert
        detector.Installations.Should().HaveCount(1);
        detector.Installations.Should().Contain(mockInstallation.Object);
    }

    [Fact]
    public void Installations_ShouldSupportMultipleInstallations()
    {
        // Arrange
        var detector = new LinuxGameDetector();
        var mockInstallation1 = new Mock<IGameInstallation>();
        var mockInstallation2 = new Mock<IGameInstallation>();

        // Act
        detector.Installations.Add(mockInstallation1.Object);
        detector.Installations.Add(mockInstallation2.Object);

        // Assert
        detector.Installations.Should().HaveCount(2);
        detector.Installations.Should().Contain(mockInstallation1.Object);
        detector.Installations.Should().Contain(mockInstallation2.Object);
    }
}
