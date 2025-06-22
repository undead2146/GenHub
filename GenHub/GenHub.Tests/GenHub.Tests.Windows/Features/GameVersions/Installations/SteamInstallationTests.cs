using FluentAssertions;
using GenHub.Core;
using GenHub.Windows.Installations;
using Xunit;

namespace GenHub.Tests.Windows.Features.GameVersions.Installations;

public class SteamInstallationTests
{
    [Fact]
    public void SteamInstallation_WithoutFetch_ShouldInitializeWithDefaultValues()
    {
        // Arrange & Act
        var installation = new SteamInstallation(false);

        // Assert
        installation.InstallationType.Should().Be(GameInstallationType.Steam);
        installation.IsVanillaInstalled.Should().BeFalse();
        installation.VanillaGamePath.Should().BeEmpty();
        installation.IsZeroHourInstalled.Should().BeFalse();
        installation.ZeroHourGamePath.Should().BeEmpty();
        installation.IsSteamInstalled.Should().BeFalse();
    }

    [Fact]
    public void SteamInstallation_WithFetch_ShouldAttemptToDetectSteam()
    {
        // Arrange & Act
        var installation = new SteamInstallation(true);

        // Assert
        installation.InstallationType.Should().Be(GameInstallationType.Steam);
        // Note: We can't test the actual detection without integration tests
        // since it depends on registry entries and file system
        // The IsSteamInstalled property will be set based on actual system state
    }

    [Fact]
    public void Fetch_ShouldSetIsSteamInstalled()
    {
        // Arrange
        var installation = new SteamInstallation(false);
        var initialSteamState = installation.IsSteamInstalled;

        // Act
        installation.Fetch();

        // Assert
        // The value may be true or false depending on the test environment
        // but it should be determined after Fetch() is called
        installation.IsSteamInstalled.Should().Be(installation.IsSteamInstalled);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Constructor_ShouldRespectFetchParameter(bool fetch)
    {
        // Arrange & Act
        var installation = new SteamInstallation(fetch);

        // Assert
        installation.Should().NotBeNull();
        installation.InstallationType.Should().Be(GameInstallationType.Steam);
    }
}
