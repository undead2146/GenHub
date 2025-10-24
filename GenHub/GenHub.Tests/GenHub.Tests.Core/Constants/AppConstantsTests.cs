using GenHub.Core.Constants;
using GenHub.Core.Models.Enums;

namespace GenHub.Tests.Core.Constants;

/// <summary>
/// Tests for <see cref="AppConstants"/> constants.
/// </summary>
public class AppConstantsTests
{
    /// <summary>
    /// Tests that all App constants have expected values.
    /// </summary>
    [Fact]
    public void AppConstants_Constants_ShouldHaveExpectedValues()
    {
        // Arrange & Act & Assert
        Assert.Multiple(() =>
        {
            // Application name and version
            Assert.Equal("GenHub", AppConstants.AppName);
            Assert.Equal("1.0", AppConstants.AppVersion);

            // Theme constants
            Assert.Equal(Theme.Dark, AppConstants.DefaultTheme);
            Assert.Equal("Dark", AppConstants.DefaultThemeName);
        });
    }

    /// <summary>
    /// Tests that application name constants are not null or empty.
    /// </summary>
    [Fact]
    public void AppConstants_AppNameConstants_ShouldNotBeNullOrEmpty()
    {
        // Arrange & Act & Assert
        Assert.Multiple(() =>
        {
            Assert.NotNull(AppConstants.AppName);
            Assert.NotEmpty(AppConstants.AppName);
            Assert.NotNull(AppConstants.AppVersion);
            Assert.NotEmpty(AppConstants.AppVersion);
        });
    }

    /// <summary>
    /// Tests that theme constants are not null or empty.
    /// </summary>
    [Fact]
    public void AppConstants_ThemeConstants_ShouldNotBeNullOrEmpty()
    {
        // Arrange & Act & Assert
        Assert.Multiple(() =>
        {
            Assert.NotNull(AppConstants.DefaultThemeName);
            Assert.NotEmpty(AppConstants.DefaultThemeName);
        });
    }

    /// <summary>
    /// Tests that application name follows proper naming conventions.
    /// </summary>
    [Fact]
    public void AppConstants_AppName_ShouldFollowNamingConventions()
    {
        // Arrange & Act & Assert
        Assert.Multiple(() =>

            // Should not contain spaces
            Assert.DoesNotContain(" ", AppConstants.AppName));
    }

    /// <summary>
    /// Tests that application version follows proper version format.
    /// </summary>
    [Fact]
    public void AppConstants_AppVersion_ShouldFollowVersionFormat()
    {
        // Arrange & Act & Assert
        Assert.Multiple(() =>
        {
            // Should not be null or empty
            Assert.NotNull(AppConstants.AppVersion);
            Assert.NotEmpty(AppConstants.AppVersion);

            // Should contain a dot (basic version format check)
            Assert.Contains(".", AppConstants.AppVersion);

            // Should not contain spaces
            Assert.DoesNotContain(" ", AppConstants.AppVersion);
        });
    }

    /// <summary>
    /// Tests that string constants are of correct type.
    /// </summary>
    [Fact]
    public void AppConstants_StringConstants_ShouldBeCorrectType()
    {
        // Arrange & Act & Assert
        Assert.Multiple(() =>
        {
            Assert.IsType<string>(AppConstants.AppName);
            Assert.IsType<string>(AppConstants.AppVersion);
            Assert.IsType<string>(AppConstants.DefaultThemeName);
        });
    }

    /// <summary>
    /// Tests that enum constants are of correct type.
    /// </summary>
    [Fact]
    public void AppConstants_EnumConstants_ShouldBeCorrectType()
    {
        // Arrange & Act & Assert
        Assert.Multiple(() =>
        {
            Assert.IsType<Theme>(AppConstants.DefaultTheme);
        });
    }

    /// <summary>
    /// Tests that theme constants are consistent.
    /// </summary>
    [Fact]
    public void AppConstants_ThemeConstants_ShouldBeConsistent()
    {
        // Arrange & Act & Assert
        Assert.Multiple(() =>
        {
            // Default theme name should match the string representation of default theme
            Assert.Equal(AppConstants.DefaultThemeName, AppConstants.DefaultTheme.ToString());
        });
    }

    /// <summary>
    /// Tests that GameClientHashRegistry correctly identifies known versions.
    /// </summary>
    [Fact]
    public void GameClientHashRegistry_GetVersionFromHash_ShouldIdentifyKnownVersions()
    {
        // Arrange & Act & Assert
        Assert.Multiple(() =>
        {
            // Test Generals 1.08
            Assert.Equal("1.08", GameClientHashRegistry.GetVersionFromHash(GameClientHashRegistry.Generals108Hash, GameType.Generals));

            // Test Zero Hour 1.04
            Assert.Equal("1.04", GameClientHashRegistry.GetVersionFromHash(GameClientHashRegistry.ZeroHour104Hash, GameType.ZeroHour));

            // Test Zero Hour 1.05 - the new correct hash
            Assert.Equal("1.05", GameClientHashRegistry.GetVersionFromHash(GameClientHashRegistry.ZeroHour105Hash, GameType.ZeroHour));

            // Test unknown hash
            Assert.Equal("Unknown", GameClientHashRegistry.GetVersionFromHash("unknownhash", GameType.Generals));

            // Test all known hashes are recognized
            Assert.True(GameClientHashRegistry.IsKnownHash(GameClientHashRegistry.Generals108Hash));
            Assert.True(GameClientHashRegistry.IsKnownHash(GameClientHashRegistry.ZeroHour104Hash));

            Assert.True(GameClientHashRegistry.IsKnownHash(GameClientHashRegistry.ZeroHour105Hash));

            // Additional hash checks can be added here
            Assert.False(GameClientHashRegistry.IsKnownHash("unknownhash"));

            // Test that executable names array is populated
            Assert.NotEmpty(GameClientHashRegistry.PossibleExecutableNames);
            Assert.Contains("generals.exe", GameClientHashRegistry.PossibleExecutableNames);
            Assert.Contains("Game.dat.bak", GameClientHashRegistry.PossibleExecutableNames);
        });
    }
}
