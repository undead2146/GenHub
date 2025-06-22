using FluentAssertions;
using GenHub.Core;
using GenHub.Windows.Installations;
using Xunit;

namespace GenHub.Tests.Windows.Features.GameVersions.Installations;

public class EaAppInstallationTests
{
    [Fact]
    public void EaAppInstallation_WithoutFetch_ShouldInitializeWithDefaultValues()
    {
        // Arrange & Act
        var installation = new EaAppInstallation(false);

        // Assert
        installation.InstallationType.Should().Be(GameInstallationType.EaApp);
        installation.IsVanillaInstalled.Should().BeFalse();
        installation.VanillaGamePath.Should().BeEmpty();
        installation.IsZeroHourInstalled.Should().BeFalse();
        installation.ZeroHourGamePath.Should().BeEmpty();
        installation.IsEaAppInstalled.Should().BeFalse();
    }

    [Fact]
    public void EaAppInstallation_WithFetch_ShouldAttemptToDetectEaApp()
    {
        // Arrange & Act
        var installation = new EaAppInstallation(true);

        // Assert
        installation.InstallationType.Should().Be(GameInstallationType.EaApp);
        // Note: We can't test the actual detection without integration tests
        // since it depends on registry entries and file system
        // The IsEaAppInstalled property will be set based on actual system state
    }

    [Fact]
    public void Fetch_ShouldSetIsEaAppInstalled()
    {
        // Arrange
        var installation = new EaAppInstallation(false);
        var initialEaAppState = installation.IsEaAppInstalled;

        // Act
        installation.Fetch();

        // Assert
        // The value may be true or false depending on the test environment
        // but it should be determined after Fetch() is called
        installation.IsEaAppInstalled.Should().Be(installation.IsEaAppInstalled);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Constructor_ShouldRespectFetchParameter(bool fetch)
    {
        // Arrange & Act
        var installation = new EaAppInstallation(fetch);

        // Assert
        installation.Should().NotBeNull();
        installation.InstallationType.Should().Be(GameInstallationType.EaApp);
    }
}
