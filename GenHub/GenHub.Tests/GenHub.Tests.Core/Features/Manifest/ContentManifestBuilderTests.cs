using GenHub.Core.Constants;
using GenHub.Core.Interfaces.Common;
using GenHub.Core.Interfaces.Manifest;
using GenHub.Core.Models.Enums;
using GenHub.Core.Models.GameInstallations;
using GenHub.Core.Models.Manifest;
using GenHub.Core.Models.Results;
using GenHub.Features.Manifest;
using Microsoft.Extensions.Logging;
using Moq;

using ContentType = GenHub.Core.Models.Enums.ContentType;

namespace GenHub.Tests.Features.Manifest;

/// <summary>
/// Unit tests for the <see cref="ContentManifestBuilder"/> class.
/// </summary>
public class ContentManifestBuilderTests
{
    /// <summary>
    /// Mock logger for the content manifest builder.
    /// </summary>
    private readonly Mock<ILogger<ContentManifestBuilder>> _loggerMock;

    /// <summary>
    /// Mock for the file hash provider used in the builder.
    /// </summary>
    private readonly Mock<IFileHashProvider> _hashProviderMock;

    /// <summary>
    /// Mock for the manifest ID service used in the builder.
    /// </summary>
    private readonly Mock<IManifestIdService> _manifestIdServiceMock;

    /// <summary>
    /// The content manifest builder under test.
    /// </summary>
    private readonly ContentManifestBuilder _builder;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContentManifestBuilderTests"/> class.
    /// </summary>
    public ContentManifestBuilderTests()
    {
        _loggerMock = new Mock<ILogger<ContentManifestBuilder>>();
        _hashProviderMock = new Mock<IFileHashProvider>();
        _manifestIdServiceMock = new Mock<IManifestIdService>();

        // Set up mock to return success for ValidateAndCreateManifestId
        _manifestIdServiceMock.Setup(x => x.ValidateAndCreateManifestId(It.IsAny<string>()))
            .Returns((string id) => OperationResult<ManifestId>.CreateSuccess(ManifestId.Create(id)));

        _manifestIdServiceMock.Setup(x => x.GenerateGameInstallationId(It.IsAny<GameInstallation>(), It.IsAny<GameType>(), It.IsAny<int>()))
            .Returns((GameInstallation gi, GameType gt, int v) =>
                OperationResult<ManifestId>.CreateSuccess(ManifestId.Create("game-installation-id")));

        // Set up mock to return success for GeneratePublisherContentId
        _manifestIdServiceMock.Setup(x => x.GeneratePublisherContentId(It.IsAny<string>(), It.IsAny<GenHub.Core.Models.Enums.ContentType>(), It.IsAny<string>(), It.IsAny<int>()))
            .Returns((string p, GenHub.Core.Models.Enums.ContentType ct, string c, int v) =>
            {
                var generated = ManifestIdGenerator.GeneratePublisherContentId(p, ct, c, v);
                return OperationResult<ManifestId>.CreateSuccess(ManifestId.Create(generated));
            });

        _builder = new ContentManifestBuilder(_loggerMock.Object, _hashProviderMock.Object, _manifestIdServiceMock.Object);
    }

    /// <summary>
    /// Tests that WithBasicInfo sets properties correctly.
    /// </summary>
    [Fact]
    public void WithBasicInfo_SetsPropertiesCorrectly()
    {
        // Act
        var result = _builder
            .WithBasicInfo("Test Publisher", "Test Name", 1)
            .Build();

        // Assert
        Assert.Equal("1.1.test.publisher.mod.test.name", result.Id);
        Assert.Equal("Test Name", result.Name);
        Assert.Equal("1", result.Version);
    }

    /// <summary>
    /// Tests that WithContentType sets properties correctly.
    /// </summary>
    [Fact]
    public void WithContentType_SetsPropertiesCorrectly()
    {
        // Act
        var result = _builder
            .WithBasicInfo("Test Publisher", "Test Name", 1)
            .WithContentType(ContentType.Mod, GameType.Generals)
            .Build();

        // Assert
        Assert.Equal(ContentType.Mod, result.ContentType);
        Assert.Equal(GameType.Generals, result.TargetGame);
    }

    /// <summary>
    /// Tests that WithPublisher sets publisher information correctly.
    /// </summary>
    [Fact]
    public void WithPublisher_SetsPublisherInfo()
    {
        // Act
        var result = _builder
            .WithBasicInfo("Test Publisher", "Test Name", 1)
            .WithPublisher("Test Publisher", "https://test.com", "https://support.test.com", "support@test.com")
            .Build();

        // Assert
        Assert.NotNull(result.Publisher);
        Assert.Equal("Test Publisher", result.Publisher.Name);
        Assert.Equal("https://test.com", result.Publisher.Website);
        Assert.Equal("https://support.test.com", result.Publisher.SupportUrl);
        Assert.Equal("support@test.com", result.Publisher.ContactEmail);
    }

    /// <summary>
    /// Tests that AddDependency adds a dependency correctly.
    /// </summary>
    [Fact]
    public void AddDependency_AddsDependencyCorrectly()
    {
        // Act
        var result = _builder
            .WithBasicInfo("Test Publisher", "Test Name", 1)
            .AddDependency(
                id: ManifestId.Create("1.0.dep.publisher.content"),
                name: "Dependency Name",
                dependencyType: ContentType.GameInstallation,
                installBehavior: DependencyInstallBehavior.AutoInstall,
                minVersion: "1.0",
                maxVersion: "2.0",
                compatibleVersions: new List<string> { "1.1", "1.2" },
                isExclusive: true,
                conflictsWith: new List<ManifestId> { ManifestId.Create("1.0.conflict.publisher.content") })
            .Build();

        // Assert
        Assert.Single(result.Dependencies);
        var dependency = result.Dependencies[0];
        Assert.Equal(ManifestId.Create("1.0.dep.publisher.content"), dependency.Id);
        Assert.Equal("Dependency Name", dependency.Name);
        Assert.Equal(ContentType.GameInstallation, dependency.DependencyType);
        Assert.Equal("1.0", dependency.MinVersion);
        Assert.Equal("2.0", dependency.MaxVersion);
        Assert.Equal(new List<string> { "1.1", "1.2" }, dependency.CompatibleVersions);
        Assert.True(dependency.IsExclusive);
        Assert.Equal(new List<ManifestId> { ManifestId.Create("1.0.conflict.publisher.content") }, dependency.ConflictsWith);
        Assert.Equal(DependencyInstallBehavior.AutoInstall, dependency.InstallBehavior);
    }

    /// <summary>
    /// Tests that AddRequiredDirectories adds directories correctly.
    /// </summary>
    [Fact]
    public void AddRequiredDirectories_AddsDirectoriesCorrectly()
    {
        // Act
        var result = _builder
            .WithBasicInfo("Test Publisher", "Test Name", 1)
            .AddRequiredDirectories(DirectoryNames.Data, "Maps", "Models")
            .Build();

        // Assert
        Assert.Equal(3, result.RequiredDirectories.Count);
        Assert.Contains(DirectoryNames.Data, result.RequiredDirectories);
        Assert.Contains("Maps", result.RequiredDirectories);
        Assert.Contains("Models", result.RequiredDirectories);
    }

    /// <summary>
    /// Tests that WithInstallationInstructions sets workspace strategy.
    /// </summary>
    [Fact]
    public void WithInstallationInstructions_SetsWorkspaceStrategy()
    {
        // Act
        var result = _builder
            .WithBasicInfo("Test Publisher", "Test Name", 1)
            .WithInstallationInstructions(WorkspaceStrategy.FullCopy)
            .Build();

        // Assert
        Assert.NotNull(result.InstallationInstructions);
        Assert.Equal(WorkspaceStrategy.FullCopy, result.InstallationInstructions.WorkspaceStrategy);
    }

    /// <summary>
    /// Tests that Build returns a valid manifest with minimal configuration.
    /// </summary>
    [Fact]
    public void Build_ReturnsValidManifest_WithMinimalConfiguration()
    {
        // Act
        var result = _builder
            .WithBasicInfo("Minimal Publisher", "Minimal Manifest", 1)
            .Build();

        // Assert
        Assert.NotNull(result);
        Assert.Equal("1.1.minimal.publisher.mod.minimal.manifest", result.Id);
        Assert.Equal("Minimal Manifest", result.Name);
        Assert.Equal("1", result.Version);
        Assert.NotNull(result.Dependencies);
        Assert.NotNull(result.Files);
        Assert.NotNull(result.RequiredDirectories);
    }
}
