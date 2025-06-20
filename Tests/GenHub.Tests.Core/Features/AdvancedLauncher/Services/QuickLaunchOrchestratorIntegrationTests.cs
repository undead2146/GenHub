using System;
using System.Threading;
using System.Threading.Tasks;
using GenHub.Core.Interfaces.AdvancedLauncher;
using GenHub.Core.Models.AdvancedLauncher;
using GenHub.Features.AdvancedLauncher.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace GenHub.Tests.Core.Features.AdvancedLauncher.Services
{
    public class QuickLaunchOrchestratorIntegrationTests
    {
        private readonly Mock<ILauncherArgumentParser> _mockArgumentParser;
        private readonly Mock<IDirectLaunchService> _mockDirectLaunchService;
        private readonly Mock<ILogger<QuickLaunchOrchestrator>> _mockLogger;
        private readonly QuickLaunchOrchestrator _orchestrator;

        public QuickLaunchOrchestratorIntegrationTests()
        {
            _mockArgumentParser = new Mock<ILauncherArgumentParser>();
            _mockDirectLaunchService = new Mock<IDirectLaunchService>();
            _mockLogger = new Mock<ILogger<QuickLaunchOrchestrator>>();
            
            _orchestrator = new QuickLaunchOrchestrator(
                _mockArgumentParser.Object,
                _mockDirectLaunchService.Object,
                _mockLogger.Object);
        }

        [Fact]
        public async Task ProcessLaunchRequestAsync_WithValidArguments_ShouldSucceed()
        {
            // Arrange
            var args = new[] { "--profile", "Test Profile", "--mode", "quick" };
            var expectedParameters = new LaunchParameters
            {
                ProfileName = "Test Profile",
                LaunchMode = LaunchMode.Quick
            };

            _mockArgumentParser.Setup(x => x.ParseArguments(args))
                .Returns(Core.Models.Results.OperationResult<LaunchParameters>.Success(expectedParameters));

            var expectedContext = new LaunchContext
            {
                ProfileName = "Test Profile",
                LaunchMode = LaunchMode.Quick,
                RequestId = Guid.NewGuid().ToString()
            };

            var expectedResult = new QuickLaunchResult
            {
                IsSuccess = true,
                LaunchContext = expectedContext
            };

            _mockDirectLaunchService.Setup(x => x.LaunchAsync(It.IsAny<LaunchContext>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedResult);

            // Act
            var result = await _orchestrator.ProcessLaunchRequestAsync(args);

            // Assert
            Assert.True(result.IsSuccess);
            Assert.NotNull(result.LaunchContext);
            Assert.Equal("Test Profile", result.LaunchContext.ProfileName);
            Assert.Equal(LaunchMode.Quick, result.LaunchContext.LaunchMode);
        }

        [Fact]
        public async Task ProcessLaunchRequestAsync_WithInvalidArguments_ShouldReturnError()
        {
            // Arrange
            var args = new[] { "--invalid-arg" };
            
            _mockArgumentParser.Setup(x => x.ParseArguments(args))
                .Returns(Core.Models.Results.OperationResult<LaunchParameters>.Failed("Invalid arguments"));

            // Act
            var result = await _orchestrator.ProcessLaunchRequestAsync(args);

            // Assert
            Assert.False(result.IsSuccess);
            Assert.Contains("Invalid arguments", result.ErrorMessage);
        }

        [Fact]
        public async Task ProcessLaunchRequestAsync_WithLaunchFailure_ShouldReturnFailureResult()
        {
            // Arrange
            var args = new[] { "--profile", "Test Profile" };
            var expectedParameters = new LaunchParameters
            {
                ProfileName = "Test Profile",
                LaunchMode = LaunchMode.Quick
            };

            _mockArgumentParser.Setup(x => x.ParseArguments(args))
                .Returns(Core.Models.Results.OperationResult<LaunchParameters>.Success(expectedParameters));

            var failedResult = new QuickLaunchResult
            {
                IsSuccess = false,
                ErrorMessage = "Launch failed"
            };

            _mockDirectLaunchService.Setup(x => x.LaunchAsync(It.IsAny<LaunchContext>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(failedResult);

            // Act
            var result = await _orchestrator.ProcessLaunchRequestAsync(args);

            // Assert
            Assert.False(result.IsSuccess);
            Assert.Contains("Launch failed", result.ErrorMessage);
        }

        [Fact]
        public async Task ProcessLaunchRequestAsync_WithNullArguments_ShouldReturnError()
        {
            // Act
            var result = await _orchestrator.ProcessLaunchRequestAsync(null!);

            // Assert
            Assert.False(result.IsSuccess);
            Assert.Contains("Arguments cannot be null", result.ErrorMessage);
        }

        [Fact]
        public async Task ProcessLaunchRequestAsync_WithEmptyArguments_ShouldUseDefaults()
        {
            // Arrange
            var args = Array.Empty<string>();
            var defaultParameters = new LaunchParameters
            {
                LaunchMode = LaunchMode.Quick
            };

            _mockArgumentParser.Setup(x => x.ParseArguments(args))
                .Returns(Core.Models.Results.OperationResult<LaunchParameters>.Success(defaultParameters));

            var expectedResult = new QuickLaunchResult
            {
                IsSuccess = true,
                LaunchContext = new LaunchContext
                {
                    LaunchMode = LaunchMode.Quick,
                    RequestId = Guid.NewGuid().ToString()
                }
            };

            _mockDirectLaunchService.Setup(x => x.LaunchAsync(It.IsAny<LaunchContext>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedResult);

            // Act
            var result = await _orchestrator.ProcessLaunchRequestAsync(args);

            // Assert
            Assert.True(result.IsSuccess);
            Assert.NotNull(result.LaunchContext);
            Assert.Equal(LaunchMode.Quick, result.LaunchContext.LaunchMode);
        }
    }
}
