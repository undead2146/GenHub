using System;
using System.Threading.Tasks;
using GenHub.Core.Models.AdvancedLauncher;
using GenHub.Core.Models.Results;
using GenHub.Features.AdvancedLauncher.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace GenHub.Tests.Core.Features.AdvancedLauncher.Services
{
    public class LauncherArgumentParserTests
    {
        private readonly Mock<ILogger<LauncherArgumentParser>> _mockLogger;
        private readonly LauncherArgumentParser _parser;

        public LauncherArgumentParserTests()
        {
            _mockLogger = new Mock<ILogger<LauncherArgumentParser>>();
            _parser = new LauncherArgumentParser(_mockLogger.Object);
        }

        [Fact]
        public void ParseArguments_WithValidProtocolUrl_ShouldParseCorrectly()
        {
            // Arrange
            var args = new[] { "genhub://launch?profile=Test%20Profile&mode=direct&version=1.0" };

            // Act
            var result = _parser.ParseArguments(args);

            // Assert
            Assert.True(result.Success);
        }

        [Fact]
        public void ParseArguments_WithProfileNameArgument_ShouldParseCorrectly()
        {
            // Arrange
            var args = new[] { "--profile", "Test Profile", "--mode", "quick" };

            // Act
            var result = _parser.ParseArguments(args);

            // Assert
            Assert.True(result.Success);
        }

        [Fact]
        public void ParseArguments_WithHelpArgument_ShouldReturnHelpResult()
        {
            // Arrange
            var args = new[] { "--help" };

            // Act
            var result = _parser.ParseArguments(args);

            // Assert
            Assert.True(result.Success);
        }

        [Fact]
        public void ParseArguments_WithInvalidArguments_ShouldReturnError()
        {
            // Arrange
            var args = new[] { "--invalid-argument" };

            // Act
            var result = _parser.ParseArguments(args);

            // Assert
            Assert.False(result.Success);
            Assert.Contains("Unknown argument", result.Message);
        }

        [Fact]
        public void ParseArguments_WithEmptyArgs_ShouldReturnDefaultParameters()
        {
            // Arrange
            var args = Array.Empty<string>();

            // Act
            var result = _parser.ParseArguments(args);

            // Assert
            Assert.True(result.Success);
        }
    }
}
