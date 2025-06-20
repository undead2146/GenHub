using System;
using Xunit;
using GenHub.Core.Models.AdvancedLauncher;

namespace GenHub.Tests.Core.Models.AdvancedLauncher
{
    /// <summary>
    /// Unit tests for QuickLaunchResult model
    /// </summary>
    public class QuickLaunchResultTests
    {
        [Fact]
        public void Constructor_ShouldInitializeWithDefaults()
        {
            // Arrange & Act
            var result = new QuickLaunchResult();

            // Assert
            Assert.False(result.Success);
            Assert.Null(result.ProfileId);
            Assert.Null(result.ProfileName);
            Assert.Null(result.ProcessId);
            Assert.True((DateTime.UtcNow - result.LaunchTime).TotalSeconds < 1);
            Assert.Equal(TimeSpan.Zero, result.LaunchDuration);
            Assert.Null(result.ErrorMessage);
            Assert.NotNull(result.DiagnosticInfo);
            Assert.Empty(result.DiagnosticInfo);
            Assert.Null(result.ExecutablePath);
            Assert.Null(result.WorkingDirectory);
            Assert.Null(result.CommandLineArguments);
            Assert.False(result.UsedAdminPrivileges);
            Assert.NotNull(result.Warnings);
            Assert.Empty(result.Warnings);
            Assert.NotNull(result.PerformanceMetrics);
            Assert.Empty(result.PerformanceMetrics);
        }        [Fact]
        public void Success_ShouldCreateSuccessfulResult()
        {
            // Arrange
            const string profileId = "test-profile";
            const int processId = 12345;

            // Act
            var result = QuickLaunchResult.Succeeded(profileId, processId);

            // Assert
            Assert.True(result.Success);
            Assert.Equal(profileId, result.ProfileId);
            Assert.Equal(processId, result.ProcessId);
            Assert.Null(result.ErrorMessage);
        }        [Fact]
        public void Failure_ShouldCreateFailedResult()
        {
            // Arrange
            const string profileId = "test-profile";
            const string errorMessage = "Test error message";

            // Act
            var result = QuickLaunchResult.Failed(profileId, errorMessage);

            // Assert
            Assert.False(result.Success);
            Assert.Equal(profileId, result.ProfileId);
            Assert.Equal(errorMessage, result.ErrorMessage);
            Assert.Null(result.ProcessId);
        }

        [Fact]
        public void DiagnosticInfo_ShouldAcceptMultipleEntries()
        {
            // Arrange
            var result = new QuickLaunchResult();

            // Act
            result.DiagnosticInfo.Add("Step 1: Validation completed");
            result.DiagnosticInfo.Add("Step 2: Profile loaded");
            result.DiagnosticInfo.Add("Step 3: Game launched");

            // Assert
            Assert.Equal(3, result.DiagnosticInfo.Count);
            Assert.Contains("Step 1: Validation completed", result.DiagnosticInfo);
            Assert.Contains("Step 2: Profile loaded", result.DiagnosticInfo);
            Assert.Contains("Step 3: Game launched", result.DiagnosticInfo);
        }

        [Fact]
        public void Warnings_ShouldAcceptMultipleEntries()
        {
            // Arrange
            var result = new QuickLaunchResult();

            // Act
            result.Warnings.Add("Warning 1: Deprecated setting used");
            result.Warnings.Add("Warning 2: Performance may be affected");

            // Assert
            Assert.Equal(2, result.Warnings.Count);
            Assert.Contains("Warning 1: Deprecated setting used", result.Warnings);
            Assert.Contains("Warning 2: Performance may be affected", result.Warnings);
        }

        [Fact]
        public void PerformanceMetrics_ShouldAcceptVariousTypes()
        {
            // Arrange
            var result = new QuickLaunchResult();

            // Act
            result.PerformanceMetrics.Add("LaunchTimeMs", 1500);
            result.PerformanceMetrics.Add("ValidationTimeMs", 250.5);
            result.PerformanceMetrics.Add("CacheHit", true);
            result.PerformanceMetrics.Add("ProfileSize", "Large");

            // Assert
            Assert.Equal(4, result.PerformanceMetrics.Count);
            Assert.Equal(1500, result.PerformanceMetrics["LaunchTimeMs"]);
            Assert.Equal(250.5, result.PerformanceMetrics["ValidationTimeMs"]);
            Assert.Equal(true, result.PerformanceMetrics["CacheHit"]);
            Assert.Equal("Large", result.PerformanceMetrics["ProfileSize"]);
        }

        [Theory]
        [InlineData(true, 12345, null)]
        [InlineData(false, null, "Error occurred")]
        [InlineData(true, 0, null)]
        public void Properties_ShouldAcceptValidValues(bool success, int? processId, string? errorMessage)
        {
            // Arrange
            var result = new QuickLaunchResult();

            // Act
            result.Success = success;
            result.ProcessId = processId;
            result.ErrorMessage = errorMessage;

            // Assert
            Assert.Equal(success, result.Success);
            Assert.Equal(processId, result.ProcessId);
            Assert.Equal(errorMessage, result.ErrorMessage);
        }

        [Fact]
        public void LaunchTime_ShouldBeSettable()
        {
            // Arrange
            var result = new QuickLaunchResult();
            var specificTime = new DateTime(2024, 1, 1, 12, 0, 0, DateTimeKind.Utc);

            // Act
            result.LaunchTime = specificTime;

            // Assert
            Assert.Equal(specificTime, result.LaunchTime);
        }

        [Fact]
        public void LaunchDuration_ShouldBeSettable()
        {
            // Arrange
            var result = new QuickLaunchResult();
            var duration = TimeSpan.FromSeconds(5.5);

            // Act
            result.LaunchDuration = duration;

            // Assert
            Assert.Equal(duration, result.LaunchDuration);
        }
    }
}
