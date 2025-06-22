using FluentAssertions;
using GenHub.Core;
using Xunit;

namespace GenHub.Tests.Core.Helpers;

public class DummyGameDetectorTests
{
    [Fact]
    public void DummyGameDetector_ShouldImplementIGameDetector()
    {
        // Arrange & Act
        var detector = new DummyGameDetector();

        // Assert
        detector.Should().BeAssignableTo<IGameDetector>();
    }

    [Fact]
    public void Installations_ShouldReturnNull()
    {
        // Arrange
        var detector = new DummyGameDetector();

        // Act
        var installations = detector.Installations;

        // Assert
        installations.Should().BeNull();
    }

    [Fact]
    public void Detect_ShouldThrowNotImplementedException()
    {
        // Arrange
        var detector = new DummyGameDetector();

        // Act & Assert
        var action = () => detector.Detect();
        action.Should().Throw<System.NotImplementedException>();
    }
}
