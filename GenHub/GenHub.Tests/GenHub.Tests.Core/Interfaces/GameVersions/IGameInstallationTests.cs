using FluentAssertions;
using GenHub.Core;
using Moq;
using Xunit;

namespace GenHub.Tests.Core.Interfaces.GameVersions;

public class IGameInstallationTests
{
    [Fact]
    public void IGameInstallation_ShouldHaveInstallationTypeProperty()
    {
        // Arrange
        var mockInstallation = new Mock<IGameInstallation>();
        mockInstallation.Setup(x => x.InstallationType).Returns(GameInstallationType.Steam);

        // Act
        var installationType = mockInstallation.Object.InstallationType;

        // Assert
        installationType.Should().Be(GameInstallationType.Steam);
    }

    [Fact]
    public void IGameInstallation_ShouldHaveVanillaProperties()
    {
        // Arrange
        var mockInstallation = new Mock<IGameInstallation>();
        mockInstallation.Setup(x => x.IsVanillaInstalled).Returns(true);
        mockInstallation.Setup(x => x.VanillaGamePath).Returns(@"C:\Games\Vanilla");

        // Act
        var isInstalled = mockInstallation.Object.IsVanillaInstalled;
        var path = mockInstallation.Object.VanillaGamePath;

        // Assert
        isInstalled.Should().BeTrue();
        path.Should().Be(@"C:\Games\Vanilla");
    }

    [Fact]
    public void IGameInstallation_ShouldHaveZeroHourProperties()
    {
        // Arrange
        var mockInstallation = new Mock<IGameInstallation>();
        mockInstallation.Setup(x => x.IsZeroHourInstalled).Returns(true);
        mockInstallation.Setup(x => x.ZeroHourGamePath).Returns(@"C:\Games\ZeroHour");

        // Act
        var isInstalled = mockInstallation.Object.IsZeroHourInstalled;
        var path = mockInstallation.Object.ZeroHourGamePath;

        // Assert
        isInstalled.Should().BeTrue();
        path.Should().Be(@"C:\Games\ZeroHour");
    }

    [Fact]
    public void IGameInstallation_ShouldHaveFetchMethod()
    {
        // Arrange
        var mockInstallation = new Mock<IGameInstallation>();

        // Act & Assert - Should not throw
        mockInstallation.Object.Fetch();
        mockInstallation.Verify(x => x.Fetch(), Times.Once);
    }

    [Fact]
    public void IGameInstallation_ShouldSupportAllGameInstallationTypes()
    {
        // Arrange & Act
        var mockInstallation = new Mock<IGameInstallation>();
        
        foreach (GameInstallationType installationType in Enum.GetValues<GameInstallationType>())
        {
            mockInstallation.Setup(x => x.InstallationType).Returns(installationType);
            
            // Act
            var result = mockInstallation.Object.InstallationType;
            
            // Assert
            result.Should().Be(installationType);
        }
    }

    [Fact]
    public void IGameInstallation_PropertiesShouldBeReadable()
    {
        // Arrange
        var mockInstallation = new Mock<IGameInstallation>();
        mockInstallation.Setup(x => x.InstallationType).Returns(GameInstallationType.EaApp);
        mockInstallation.Setup(x => x.IsVanillaInstalled).Returns(false);
        mockInstallation.Setup(x => x.VanillaGamePath).Returns(string.Empty);
        mockInstallation.Setup(x => x.IsZeroHourInstalled).Returns(false);
        mockInstallation.Setup(x => x.ZeroHourGamePath).Returns(string.Empty);

        // Act
        var installation = mockInstallation.Object;

        // Assert
        installation.InstallationType.Should().Be(GameInstallationType.EaApp);
        installation.IsVanillaInstalled.Should().BeFalse();
        installation.VanillaGamePath.Should().BeEmpty();
        installation.IsZeroHourInstalled.Should().BeFalse();
        installation.ZeroHourGamePath.Should().BeEmpty();
        
        // Verify property getters were called
        mockInstallation.VerifyGet(x => x.InstallationType, Times.Once);
        mockInstallation.VerifyGet(x => x.IsVanillaInstalled, Times.Once);
        mockInstallation.VerifyGet(x => x.VanillaGamePath, Times.Once);
        mockInstallation.VerifyGet(x => x.IsZeroHourInstalled, Times.Once);
        mockInstallation.VerifyGet(x => x.ZeroHourGamePath, Times.Once);
    }
}
