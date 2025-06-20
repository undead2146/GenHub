using System;
using System.Collections.Generic;
using Xunit;
using GenHub.Core.Models.AdvancedLauncher;

namespace GenHub.Tests.Core.Models.AdvancedLauncher
{
    /// <summary>
    /// Unit tests for LaunchParameters model
    /// </summary>
    public class LaunchParametersTests
    {
        [Fact]
        public void Constructor_ShouldInitializeWithDefaults()
        {
            // Arrange & Act
            var parameters = new LaunchParameters();

            // Assert
            Assert.Equal(LaunchMode.Normal, parameters.Mode);
            Assert.False(parameters.SkipValidation);
            Assert.False(parameters.QuietMode);
            Assert.False(parameters.RunAsAdmin);
            Assert.True(parameters.ShowLaunchDialog);
            Assert.False(parameters.CreateShortcut);
            Assert.False(parameters.RegisterProtocol);
            Assert.False(parameters.Verbose);
            Assert.Equal("launch", parameters.Action);
            Assert.NotNull(parameters.EnvironmentVariables);
            Assert.NotNull(parameters.CustomParameters);
            Assert.Empty(parameters.EnvironmentVariables);
            Assert.Empty(parameters.CustomParameters);
        }

        [Fact]
        public void Clone_ShouldCreateExactCopy()
        {
            // Arrange
            var original = new LaunchParameters
            {
                ProfileId = "test-profile",
                ProfileName = "Test Profile",
                Mode = LaunchMode.Quick,
                SkipValidation = true,
                QuietMode = true,
                RunAsAdmin = true,
                CustomArguments = "--test-arg",
                WorkingDirectory = @"C:\Test",
                ShowLaunchDialog = false,
                LaunchDelay = TimeSpan.FromSeconds(5),
                CreateShortcut = true,
                RegisterProtocol = true,
                Verbose = true,
                Action = "validate"
            };
            original.EnvironmentVariables.Add("TEST_VAR", "test_value");
            original.CustomParameters.Add("custom_param", "custom_value");

            // Act
            var clone = original.Clone();

            // Assert
            Assert.Equal(original.ProfileId, clone.ProfileId);
            Assert.Equal(original.ProfileName, clone.ProfileName);
            Assert.Equal(original.Mode, clone.Mode);
            Assert.Equal(original.SkipValidation, clone.SkipValidation);
            Assert.Equal(original.QuietMode, clone.QuietMode);
            Assert.Equal(original.RunAsAdmin, clone.RunAsAdmin);
            Assert.Equal(original.CustomArguments, clone.CustomArguments);
            Assert.Equal(original.WorkingDirectory, clone.WorkingDirectory);
            Assert.Equal(original.ShowLaunchDialog, clone.ShowLaunchDialog);
            Assert.Equal(original.LaunchDelay, clone.LaunchDelay);
            Assert.Equal(original.CreateShortcut, clone.CreateShortcut);
            Assert.Equal(original.RegisterProtocol, clone.RegisterProtocol);
            Assert.Equal(original.Verbose, clone.Verbose);
            Assert.Equal(original.Action, clone.Action);

            // Verify collections are copied, not referenced
            Assert.NotSame(original.EnvironmentVariables, clone.EnvironmentVariables);
            Assert.NotSame(original.CustomParameters, clone.CustomParameters);
            Assert.Equal(original.EnvironmentVariables, clone.EnvironmentVariables);
            Assert.Equal(original.CustomParameters, clone.CustomParameters);
        }

        [Theory]
        [InlineData(LaunchMode.Normal)]
        [InlineData(LaunchMode.Quick)]
        [InlineData(LaunchMode.Validate)]
        [InlineData(LaunchMode.Background)]
        [InlineData(LaunchMode.Diagnostic)]
        public void Mode_ShouldAcceptAllValidValues(LaunchMode mode)
        {
            // Arrange
            var parameters = new LaunchParameters();

            // Act
            parameters.Mode = mode;

            // Assert
            Assert.Equal(mode, parameters.Mode);
        }

        [Fact]
        public void EnvironmentVariables_ShouldBeIndependentCollections()
        {
            // Arrange
            var parameters1 = new LaunchParameters();
            var parameters2 = new LaunchParameters();

            // Act
            parameters1.EnvironmentVariables.Add("VAR1", "value1");
            parameters2.EnvironmentVariables.Add("VAR2", "value2");

            // Assert
            Assert.Single(parameters1.EnvironmentVariables);
            Assert.Single(parameters2.EnvironmentVariables);
            Assert.True(parameters1.EnvironmentVariables.ContainsKey("VAR1"));
            Assert.False(parameters1.EnvironmentVariables.ContainsKey("VAR2"));
            Assert.True(parameters2.EnvironmentVariables.ContainsKey("VAR2"));
            Assert.False(parameters2.EnvironmentVariables.ContainsKey("VAR1"));
        }

        [Fact]
        public void CustomParameters_ShouldBeIndependentCollections()
        {
            // Arrange
            var parameters1 = new LaunchParameters();
            var parameters2 = new LaunchParameters();

            // Act
            parameters1.CustomParameters.Add("param1", "value1");
            parameters2.CustomParameters.Add("param2", "value2");

            // Assert
            Assert.Single(parameters1.CustomParameters);
            Assert.Single(parameters2.CustomParameters);
            Assert.True(parameters1.CustomParameters.ContainsKey("param1"));
            Assert.False(parameters1.CustomParameters.ContainsKey("param2"));
            Assert.True(parameters2.CustomParameters.ContainsKey("param2"));
            Assert.False(parameters2.CustomParameters.ContainsKey("param1"));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("test-profile-id")]
        [InlineData("profile-with-special-chars-!@#$%")]
        public void ProfileId_ShouldAcceptValidValues(string? profileId)
        {
            // Arrange
            var parameters = new LaunchParameters();

            // Act
            parameters.ProfileId = profileId;

            // Assert
            Assert.Equal(profileId, parameters.ProfileId);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("Test Profile")]
        [InlineData("Profile with special chars !@#$%")]
        public void ProfileName_ShouldAcceptValidValues(string? profileName)
        {
            // Arrange
            var parameters = new LaunchParameters();

            // Act
            parameters.ProfileName = profileName;

            // Assert
            Assert.Equal(profileName, parameters.ProfileName);
        }
    }
}
