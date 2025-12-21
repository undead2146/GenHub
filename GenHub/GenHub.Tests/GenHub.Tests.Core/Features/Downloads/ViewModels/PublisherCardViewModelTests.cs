using System.Collections.ObjectModel;
using FluentAssertions;
using GenHub.Core.Interfaces.Content;
using GenHub.Core.Interfaces.GameProfiles;
using GenHub.Core.Interfaces.Manifest;
using GenHub.Core.Interfaces.Notifications;
using GenHub.Core.Models.Manifest;
using GenHub.Core.Models.Results;
using GenHub.Features.Content.ViewModels;
using GenHub.Features.Downloads.ViewModels;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace GenHub.Tests.Core.Features.Downloads.ViewModels;

public class PublisherCardViewModelTests
{
    private readonly Mock<ILogger<PublisherCardViewModel>> _loggerMock;
    private readonly Mock<IContentOrchestrator> _contentOrchestratorMock;
    private readonly Mock<IContentManifestPool> _manifestPoolMock;
    private readonly Mock<IProfileContentService> _profileContentServiceMock;
    private readonly Mock<IGameProfileManager> _gameProfileManagerMock;
    private readonly Mock<INotificationService> _notificationServiceMock;

    public PublisherCardViewModelTests()
    {
        _loggerMock = new Mock<ILogger<PublisherCardViewModel>>();
        _contentOrchestratorMock = new Mock<IContentOrchestrator>();
        _manifestPoolMock = new Mock<IContentManifestPool>();
        _profileContentServiceMock = new Mock<IProfileContentService>();
        _gameProfileManagerMock = new Mock<IGameProfileManager>();
        _notificationServiceMock = new Mock<INotificationService>();
    }

    private PublisherCardViewModel CreateSystem()
    {
        return new PublisherCardViewModel(
            _loggerMock.Object,
            _contentOrchestratorMock.Object,
            _manifestPoolMock.Object,
            _profileContentServiceMock.Object,
            _gameProfileManagerMock.Object,
            _notificationServiceMock.Object);
    }

    [Fact]
    public async Task RefreshInstallationStatus_DifferentAddonsSameVersion_DoNotCollide()
    {
        // Arrange
        var vm = CreateSystem();
        vm.PublisherId = "test_publisher";

        // Item 1: Camera Mod v1.0
        var cameraItem = new ContentItemViewModel(new ContentSearchResult
        {
            Id = "test_publisher.camera",
            Name = "Camera Mod",
            Version = "1.0",
            ContentType = GenHub.Core.Models.Enums.ContentType.Addon,
            ProviderName = "test_provider",
            AuthorName = "Test Author",
            LastUpdated = DateTime.Now
        });

        // Item 2: HUD Mod v1.0
        var hudItem = new ContentItemViewModel(new ContentSearchResult
        {
            Id = "test_publisher.hud",
            Name = "HUD Mod",
            Version = "1.0",
            ContentType = GenHub.Core.Models.Enums.ContentType.Addon,
            ProviderName = "test_provider",
            AuthorName = "Test Author",
            LastUpdated = DateTime.Now
        });

        vm.ContentTypes.Add(new ContentTypeGroup
        {
            DisplayName = "Addons",
            Type = GenHub.Core.Models.Enums.ContentType.Addon,
            Items = new ObservableCollection<ContentItemViewModel> { cameraItem, hudItem }
        });

        // Manifest for ONLY Camera Mod
        var cameraManifest = new ContentManifest
        {
            Id = ManifestId.Create("1.10.test_publisher.addon.camera"),
            Name = "Camera Mod",
            Version = "1.0",
            ContentType = GenHub.Core.Models.Enums.ContentType.Addon,
            Publisher = new PublisherInfo { PublisherType = "test_publisher" }
        };

        _manifestPoolMock.Setup(x => x.GetAllManifestsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(OperationResult<IEnumerable<ContentManifest>>.CreateSuccess(new[] { cameraManifest }));

        // Act
        await vm.RefreshInstallationStatusAsync();

        // Assert
        // Camera mod should be installed
        cameraItem.IsInstalled.Should().BeTrue("Camera mod should be identified as installed");
        cameraItem.AvailableVariants.Should().ContainSingle();

        // HUD mod should NOT be installed (previously failing assertion)
        hudItem.IsInstalled.Should().BeFalse("HUD mod should NOT be identified as installed just because version matches");
        hudItem.AvailableVariants.Should().BeEmpty();
    }

    [Fact]
    public async Task RefreshInstallationStatus_GameClient_AllowsVersionMatch()
    {
        // Arrange
        var vm = CreateSystem();
        vm.PublisherId = "test_publisher";

        // GameClient Item with flexible name
        var clientItem = new ContentItemViewModel(new ContentSearchResult
        {
            Id = "test_publisher.weekly",
            Name = "Weekly Build 2025",
            Version = "2025-01-01",
            ContentType = GenHub.Core.Models.Enums.ContentType.GameClient,
            ProviderName = "test_provider",
            AuthorName = "Test Author",
            LastUpdated = DateTime.Now
        });

        vm.ContentTypes.Add(new ContentTypeGroup
        {
            DisplayName = "Game Clients",
            Type = GenHub.Core.Models.Enums.ContentType.GameClient,
            Items = new ObservableCollection<ContentItemViewModel> { clientItem }
        });

        // Manifest with different name but same version/publisher
        var clientManifest = new ContentManifest
        {
            Id = ManifestId.Create("1.20250101.test_publisher.gameclient.release"),
            Name = "Full Release Client", // Name differs significantly
            Version = "2025-01-01",
            ContentType = GenHub.Core.Models.Enums.ContentType.GameClient,
            Publisher = new PublisherInfo { PublisherType = "test_publisher" }
        };

        _manifestPoolMock.Setup(x => x.GetAllManifestsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(OperationResult<IEnumerable<ContentManifest>>.CreateSuccess(new[] { clientManifest }));

        // Act
        await vm.RefreshInstallationStatusAsync();

        // Assert
        clientItem.IsInstalled.Should().BeTrue("GameClient should match based on version even if name differs");
        clientItem.AvailableVariants.Should().ContainSingle();
    }
}
