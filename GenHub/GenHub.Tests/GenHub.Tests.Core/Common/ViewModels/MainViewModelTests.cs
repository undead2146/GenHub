using FluentAssertions;
using GenHub.Core;
using GenHub.Services;
using GenHub.ViewModels;
using Moq;
using Xunit;

namespace GenHub.Tests.Core.Common.ViewModels;

public class MainViewModelTests
{
    [Fact]
    public void MainViewModel_ShouldRequireGameDetectionService()
    {
        // Arrange
        var mockGameDetector = new Mock<IGameDetector>();
        var mockInstallation = new Mock<IGameInstallation>();
        mockGameDetector.Setup(x => x.Installations).Returns(new List<IGameInstallation> { mockInstallation.Object });
        var gameDetectionService = new GameDetectionService(mockGameDetector.Object);

        // Act
        var viewModel = new MainViewModel(gameDetectionService);

        // Assert
        viewModel.Should().NotBeNull();
        viewModel.Should().BeAssignableTo<ViewModelBase>();
    }

    [Fact]
    public void MainViewModel_ParameterlessConstructor_ShouldUseDummyGameDetector()
    {
        // Act
        var viewModel = new MainViewModel();

        // Assert
        viewModel.Should().NotBeNull();
        viewModel.Should().BeAssignableTo<ViewModelBase>();
    }

    [Fact]
    public void VanillaGamePath_ShouldInitializeAsNull()
    {
        // Arrange
        var mockGameDetector = new Mock<IGameDetector>();
        var mockInstallation = new Mock<IGameInstallation>();
        mockGameDetector.Setup(x => x.Installations).Returns(new List<IGameInstallation> { mockInstallation.Object });
        var gameDetectionService = new GameDetectionService(mockGameDetector.Object);

        // Act
        var viewModel = new MainViewModel(gameDetectionService);

        // Assert
        viewModel.VanillaGamePath.Should().BeNull();
    }

    [Fact]
    public void ZeroHourGamePath_ShouldInitializeAsNull()
    {
        // Arrange
        var mockGameDetector = new Mock<IGameDetector>();
        var mockInstallation = new Mock<IGameInstallation>();
        mockGameDetector.Setup(x => x.Installations).Returns(new List<IGameInstallation> { mockInstallation.Object });
        var gameDetectionService = new GameDetectionService(mockGameDetector.Object);

        // Act
        var viewModel = new MainViewModel(gameDetectionService);

        // Assert
        viewModel.ZeroHourGamePath.Should().BeNull();
    }

    [Fact]
    public void Detect_ShouldCallGameDetectionServiceDetectGames()
    {
        // Arrange
        var mockGameDetector = new Mock<IGameDetector>();
        var mockInstallation = new Mock<IGameInstallation>();
        mockInstallation.Setup(x => x.VanillaGamePath).Returns(@"C:\Games\Vanilla");
        mockInstallation.Setup(x => x.ZeroHourGamePath).Returns(@"C:\Games\ZeroHour");
        mockGameDetector.Setup(x => x.Installations).Returns(new List<IGameInstallation> { mockInstallation.Object });
        var gameDetectionService = new GameDetectionService(mockGameDetector.Object);

        var viewModel = new MainViewModel(gameDetectionService);

        // Act
        viewModel.DetectCommand.Execute(null);

        // Assert
        mockGameDetector.Verify(x => x.Detect(), Times.Once);
    }

    [Fact]
    public void Detect_ShouldUpdateVanillaGamePath()
    {
        // Arrange
        const string expectedPath = @"C:\Games\Vanilla";
        var mockGameDetector = new Mock<IGameDetector>();
        var mockInstallation = new Mock<IGameInstallation>();
        mockInstallation.Setup(x => x.VanillaGamePath).Returns(expectedPath);
        mockInstallation.Setup(x => x.ZeroHourGamePath).Returns(@"C:\Games\ZeroHour");
        mockGameDetector.Setup(x => x.Installations).Returns(new List<IGameInstallation> { mockInstallation.Object });
        var gameDetectionService = new GameDetectionService(mockGameDetector.Object);

        var viewModel = new MainViewModel(gameDetectionService);

        // Act
        viewModel.DetectCommand.Execute(null);

        // Assert
        viewModel.VanillaGamePath.Should().Be(expectedPath);
    }

    [Fact]
    public void Detect_ShouldUpdateZeroHourGamePath()
    {
        // Arrange
        const string expectedPath = @"C:\Games\ZeroHour";
        var mockGameDetector = new Mock<IGameDetector>();
        var mockInstallation = new Mock<IGameInstallation>();
        mockInstallation.Setup(x => x.VanillaGamePath).Returns(@"C:\Games\Vanilla");
        mockInstallation.Setup(x => x.ZeroHourGamePath).Returns(expectedPath);
        mockGameDetector.Setup(x => x.Installations).Returns(new List<IGameInstallation> { mockInstallation.Object });
        var gameDetectionService = new GameDetectionService(mockGameDetector.Object);

        var viewModel = new MainViewModel(gameDetectionService);

        // Act
        viewModel.DetectCommand.Execute(null);

        // Assert
        viewModel.ZeroHourGamePath.Should().Be(expectedPath);
    }

    [Fact]
    public void VanillaGamePath_ShouldBeObservableProperty()
    {
        // Arrange
        var mockGameDetector = new Mock<IGameDetector>();
        var mockInstallation = new Mock<IGameInstallation>();
        mockGameDetector.Setup(x => x.Installations).Returns(new List<IGameInstallation> { mockInstallation.Object });
        var gameDetectionService = new GameDetectionService(mockGameDetector.Object);
        var viewModel = new MainViewModel(gameDetectionService);

        var propertyChangedCount = 0;
        viewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(MainViewModel.VanillaGamePath))
                propertyChangedCount++;
        };

        // Act
        viewModel.VanillaGamePath = @"C:\Test\Path";

        // Assert
        propertyChangedCount.Should().Be(1);
        viewModel.VanillaGamePath.Should().Be(@"C:\Test\Path");
    }

    [Fact]
    public void ZeroHourGamePath_ShouldBeObservableProperty()
    {
        // Arrange
        var mockGameDetector = new Mock<IGameDetector>();
        var mockInstallation = new Mock<IGameInstallation>();
        mockGameDetector.Setup(x => x.Installations).Returns(new List<IGameInstallation> { mockInstallation.Object });
        var gameDetectionService = new GameDetectionService(mockGameDetector.Object);
        var viewModel = new MainViewModel(gameDetectionService);

        var propertyChangedCount = 0;
        viewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(MainViewModel.ZeroHourGamePath))
                propertyChangedCount++;
        };

        // Act
        viewModel.ZeroHourGamePath = @"C:\Test\ZeroHour";

        // Assert
        propertyChangedCount.Should().Be(1);
        viewModel.ZeroHourGamePath.Should().Be(@"C:\Test\ZeroHour");
    }
}
