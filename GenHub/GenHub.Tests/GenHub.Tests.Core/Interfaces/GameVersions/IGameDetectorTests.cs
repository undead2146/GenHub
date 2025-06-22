using FluentAssertions;
using GenHub.Core;
using Moq;
using Xunit;

namespace GenHub.Tests.Core.Interfaces.GameVersions;

public class IGameDetectorTests
{
    [Fact]
    public void IGameDetector_ShouldHaveInstallationsProperty()
    {
        // Arrange
        var mockDetector = new Mock<IGameDetector>();
        var mockInstallations = new List<IGameInstallation>();
        mockDetector.Setup(x => x.Installations).Returns(mockInstallations);

        // Act
        var installations = mockDetector.Object.Installations;

        // Assert
        installations.Should().BeSameAs(mockInstallations);
    }

    [Fact]
    public void IGameDetector_ShouldHaveDetectMethod()
    {
        // Arrange
        var mockDetector = new Mock<IGameDetector>();

        // Act & Assert - Should not throw
        mockDetector.Object.Detect();
        mockDetector.Verify(x => x.Detect(), Times.Once);
    }

    [Fact]
    public void IGameDetector_InstallationsProperty_ShouldBeGettable()
    {
        // Arrange
        var mockDetector = new Mock<IGameDetector>();
        var installations = new List<IGameInstallation>();
        mockDetector.Setup(x => x.Installations).Returns(installations);

        // Act
        var result = mockDetector.Object.Installations;

        // Assert
        result.Should().NotBeNull();
        result.Should().BeSameAs(installations);
        mockDetector.VerifyGet(x => x.Installations, Times.Once);
    }

    [Fact]
    public void IGameDetector_ShouldSupportMockingBehavior()
    {
        // Arrange
        var mockDetector = new Mock<IGameDetector>();
        var mockInstallation = new Mock<IGameInstallation>();
        
        mockDetector.Setup(x => x.Installations).Returns(new List<IGameInstallation> { mockInstallation.Object });
        mockDetector.Setup(x => x.Detect()).Verifiable();

        // Act
        var installations = mockDetector.Object.Installations;
        mockDetector.Object.Detect();

        // Assert
        installations.Should().HaveCount(1);
        installations.Should().Contain(mockInstallation.Object);
        mockDetector.Verify(x => x.Detect(), Times.Once);
    }
}
