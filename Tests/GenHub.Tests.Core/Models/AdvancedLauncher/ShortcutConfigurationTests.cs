using System;
using Xunit;
using GenHub.Core.Models.AdvancedLauncher;

namespace GenHub.Tests.Core.Models.AdvancedLauncher
{
    /// <summary>
    /// Unit tests for ShortcutConfiguration model
    /// </summary>
    public class ShortcutConfigurationTests
    {
        [Fact]
        public void Constructor_ShouldInitializeWithDefaults()
        {
            // Arrange & Act
            var config = new ShortcutConfiguration();

            // Assert
            Assert.False(string.IsNullOrEmpty(config.Id));
            Assert.Equal(string.Empty, config.Name);
            Assert.Equal(string.Empty, config.ProfileId);
            Assert.Equal(string.Empty, config.IconPath);
            Assert.Equal(ShortcutType.Profile, config.Type);
            Assert.Equal(ShortcutLaunchMode.Normal, config.LaunchMode);
            Assert.Equal(string.Empty, config.CustomArguments);
            Assert.False(config.RunAsAdmin);
            Assert.Equal("Games", config.Category);
            Assert.Equal(string.Empty, config.Description);
            Assert.NotNull(config.PlatformSpecificOptions);
            Assert.Empty(config.PlatformSpecificOptions);
            Assert.True((DateTime.UtcNow - config.CreatedAt).TotalSeconds < 1);
            Assert.True((DateTime.UtcNow - config.LastValidated).TotalSeconds < 1);
            Assert.True(config.IsValid);
            Assert.Equal(ShortcutLocation.Desktop, config.Location);
            Assert.Null(config.WorkingDirectory);
            Assert.False(config.ShowConsole);
        }

        [Fact]
        public void ForProfile_ShouldCreateCorrectConfiguration()
        {
            // Arrange
            const string profileId = "test-profile-id";
            const string profileName = "Test Profile";

            // Act
            var config = ShortcutConfiguration.ForProfile(profileId, profileName);

            // Assert
            Assert.Equal(profileId, config.ProfileId);
            Assert.Equal(profileName, config.Name);
            Assert.Equal(ShortcutType.Profile, config.Type);
            Assert.Equal($"Launch {profileName} via GenHub", config.Description);
            Assert.False(string.IsNullOrEmpty(config.Id));
        }

        [Fact]
        public void Clone_ShouldCreateExactCopy()
        {
            // Arrange
            var original = new ShortcutConfiguration
            {
                Id = "test-id",
                Name = "Test Shortcut",
                ProfileId = "test-profile",
                IconPath = @"C:\test\icon.ico",
                Type = ShortcutType.Game,
                LaunchMode = ShortcutLaunchMode.Direct,
                CustomArguments = "--test-arg",
                RunAsAdmin = true,
                Category = "Strategy Games",
                Description = "Test description",
                CreatedAt = new DateTime(2024, 1, 1, 12, 0, 0, DateTimeKind.Utc),
                LastValidated = new DateTime(2024, 1, 2, 12, 0, 0, DateTimeKind.Utc),
                IsValid = false,
                Location = ShortcutLocation.StartMenu,
                WorkingDirectory = @"C:\test",
                ShowConsole = true
            };
            original.PlatformSpecificOptions.Add("WindowsOption", "value1");
            original.PlatformSpecificOptions.Add("LinuxOption", "value2");

            // Act
            var clone = original.Clone();

            // Assert
            Assert.Equal(original.Id, clone.Id);
            Assert.Equal(original.Name, clone.Name);
            Assert.Equal(original.ProfileId, clone.ProfileId);
            Assert.Equal(original.IconPath, clone.IconPath);
            Assert.Equal(original.Type, clone.Type);
            Assert.Equal(original.LaunchMode, clone.LaunchMode);
            Assert.Equal(original.CustomArguments, clone.CustomArguments);
            Assert.Equal(original.RunAsAdmin, clone.RunAsAdmin);
            Assert.Equal(original.Category, clone.Category);
            Assert.Equal(original.Description, clone.Description);
            Assert.Equal(original.CreatedAt, clone.CreatedAt);
            Assert.Equal(original.LastValidated, clone.LastValidated);
            Assert.Equal(original.IsValid, clone.IsValid);
            Assert.Equal(original.Location, clone.Location);
            Assert.Equal(original.WorkingDirectory, clone.WorkingDirectory);
            Assert.Equal(original.ShowConsole, clone.ShowConsole);

            // Verify collections are copied, not referenced
            Assert.NotSame(original.PlatformSpecificOptions, clone.PlatformSpecificOptions);
            Assert.Equal(original.PlatformSpecificOptions, clone.PlatformSpecificOptions);
        }

        [Theory]
        [InlineData(ShortcutType.Profile)]
        [InlineData(ShortcutType.Game)]
        [InlineData(ShortcutType.QuickLauncher)]
        [InlineData(ShortcutType.Manager)]
        [InlineData(ShortcutType.Diagnostics)]
        public void Type_ShouldAcceptAllValidValues(ShortcutType type)
        {
            // Arrange
            var config = new ShortcutConfiguration();

            // Act
            config.Type = type;

            // Assert
            Assert.Equal(type, config.Type);
        }

        [Theory]
        [InlineData(ShortcutLaunchMode.Normal)]
        [InlineData(ShortcutLaunchMode.Direct)]
        [InlineData(ShortcutLaunchMode.Validate)]
        [InlineData(ShortcutLaunchMode.Ask)]
        public void LaunchMode_ShouldAcceptAllValidValues(ShortcutLaunchMode launchMode)
        {
            // Arrange
            var config = new ShortcutConfiguration();

            // Act
            config.LaunchMode = launchMode;

            // Assert
            Assert.Equal(launchMode, config.LaunchMode);
        }

        [Theory]
        [InlineData(ShortcutLocation.Desktop)]
        [InlineData(ShortcutLocation.StartMenu)]
        [InlineData(ShortcutLocation.Both)]
        [InlineData(ShortcutLocation.Custom)]
        public void Location_ShouldAcceptAllValidValues(ShortcutLocation location)
        {
            // Arrange
            var config = new ShortcutConfiguration();

            // Act
            config.Location = location;

            // Assert
            Assert.Equal(location, config.Location);
        }

        [Fact]
        public void PlatformSpecificOptions_ShouldBeIndependentCollections()
        {
            // Arrange
            var config1 = new ShortcutConfiguration();
            var config2 = new ShortcutConfiguration();

            // Act
            config1.PlatformSpecificOptions.Add("Option1", "value1");
            config2.PlatformSpecificOptions.Add("Option2", "value2");

            // Assert
            Assert.Single(config1.PlatformSpecificOptions);
            Assert.Single(config2.PlatformSpecificOptions);
            Assert.True(config1.PlatformSpecificOptions.ContainsKey("Option1"));
            Assert.False(config1.PlatformSpecificOptions.ContainsKey("Option2"));
            Assert.True(config2.PlatformSpecificOptions.ContainsKey("Option2"));
            Assert.False(config2.PlatformSpecificOptions.ContainsKey("Option1"));
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void RunAsAdmin_ShouldAcceptBooleanValues(bool runAsAdmin)
        {
            // Arrange
            var config = new ShortcutConfiguration();

            // Act
            config.RunAsAdmin = runAsAdmin;

            // Assert
            Assert.Equal(runAsAdmin, config.RunAsAdmin);
        }

        [Theory]
        [InlineData("")]
        [InlineData("Games")]
        [InlineData("Strategy")]
        [InlineData("Action Games")]
        [InlineData("Utilities")]
        public void Category_ShouldAcceptStringValues(string category)
        {
            // Arrange
            var config = new ShortcutConfiguration();

            // Act
            config.Category = category;

            // Assert
            Assert.Equal(category, config.Category);
        }
    }
}
