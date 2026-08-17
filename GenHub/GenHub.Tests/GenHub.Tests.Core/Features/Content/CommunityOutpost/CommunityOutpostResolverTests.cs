using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using GenHub.Core.Constants;
using GenHub.Core.Interfaces.Manifest;
using GenHub.Core.Interfaces.Providers;
using GenHub.Core.Models.Enums;
using GenHub.Core.Models.Manifest;
using GenHub.Core.Models.Providers;
using GenHub.Core.Models.Results.Content;
using GenHub.Features.Content.Services.CommunityOutpost;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;
using ContentType = GenHub.Core.Models.Enums.ContentType;

namespace GenHub.Tests.Core.Features.Content.CommunityOutpost;

/// <summary>
/// Unit tests for <see cref="CommunityOutpostResolver"/>.
/// </summary>
public sealed class CommunityOutpostResolverTests
{
    private readonly Mock<IProviderDefinitionLoader> _providerLoaderMock;

    /// <summary>
    /// Initializes a new instance of the <see cref="CommunityOutpostResolverTests"/> class.
    /// </summary>
    public CommunityOutpostResolverTests()
    {
        _providerLoaderMock = new Mock<IProviderDefinitionLoader>();

        var provider = new ProviderDefinition
        {
            ProviderId = CommunityOutpostConstants.PublisherId,
            PublisherType = CommunityOutpostConstants.PublisherType,
            DisplayName = CommunityOutpostConstants.PublisherName,
        };

        _providerLoaderMock
            .Setup(p => p.GetProvider(CommunityOutpostConstants.PublisherId))
            .Returns(provider);
    }

    /// <summary>
    /// Verifies that resolving a variant item populates SelectedVariantId and variant tags.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Fact]
    public async Task ResolveAsync_WithVariantSearchResult_PopulatesSelectedVariantIdAndTagsAsync()
    {
        // Arrange
        var searchResult = new ContentSearchResult
        {
            Id = "1.0.communityoutpost.addon.cbpr-1080p",
            Name = "Control Bar Pro (ExiLe) - 1080p (Recommended)",
            ProviderName = "communityoutpost",
            ContentType = ContentType.Addon,
            TargetGame = GameType.ZeroHour,
            SourceUrl = "https://example.com/cbpr.dat",
        };
        searchResult.ResolverMetadata["contentCode"] = "cbpr";
        searchResult.ResolverMetadata["selectedVariant"] = "1080p";

        var builtManifest = new ContentManifest
        {
            Id = ManifestId.Create("1.0.communityoutpost.addon.cbpr1080p"),
            Name = "Control Bar Pro (ExiLe)",
            ContentType = ContentType.Addon,
            TargetGame = GameType.ZeroHour,
            Files = [new ManifestFile { RelativePath = "cbpr.dat" }],
        };

        var builderMock = CreateBuilderMock(builtManifest);
        var resolver = new CommunityOutpostResolver(
            () => builderMock.Object,
            _providerLoaderMock.Object,
            NullLogger<CommunityOutpostResolver>.Instance);

        // Act
        var result = await resolver.ResolveAsync(searchResult, CancellationToken.None);

        // Assert
        Assert.True(result.Success);
        var manifest = result.Data;
        Assert.NotNull(manifest);
        Assert.Equal("1080p", manifest.Metadata?.SelectedVariantId);
        Assert.Contains(manifest.Metadata?.Tags ?? [], t => t == "requestedVariant:1080p");
        Assert.Contains(manifest.Metadata?.Tags ?? [], t => t == "selectedVariant:1080p");
        Assert.Contains(manifest.Metadata?.Tags ?? [], t => t == "contentCode:cbpr");
    }

    /// <summary>
    /// Verifies that resolving an item with catalog version 0 generates version 0 manifest.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Fact]
    public async Task ResolveAsync_WithVersionZeroId_PreservesVersionZeroAsync()
    {
        // Arrange
        var searchResult = new ContentSearchResult
        {
            Id = "1.0.communityoutpost.addon.gent",
            Name = "GenTool",
            ProviderName = "communityoutpost",
            ContentType = ContentType.Addon,
            TargetGame = GameType.ZeroHour,
            Version = "1.0",
            SourceUrl = "https://example.com/gent.zip",
        };
        searchResult.ResolverMetadata["contentCode"] = "gent";

        var builtManifest = new ContentManifest
        {
            Id = ManifestId.Create("1.0.communityoutpost.addon.gent"),
            Name = "GenTool",
            ContentType = ContentType.Addon,
            TargetGame = GameType.ZeroHour,
            Files = [new ManifestFile { RelativePath = "gent.zip" }],
        };

        var builderMock = CreateBuilderMock(builtManifest);
        var resolver = new CommunityOutpostResolver(
            () => builderMock.Object,
            _providerLoaderMock.Object,
            NullLogger<CommunityOutpostResolver>.Instance);

        // Act
        var result = await resolver.ResolveAsync(searchResult, CancellationToken.None);

        // Assert
        Assert.True(result.Success);
        var manifest = result.Data;
        Assert.NotNull(manifest);
        Assert.StartsWith("1.0.communityoutpost.addon.", manifest.Id.Value);
    }

    /// <summary>
    /// Verifies that resolving an item with null or empty version falls back to a valid default version.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Fact]
    public async Task ResolveAsync_WithNullVersion_SetsValidDefaultVersionAsync()
    {
        // Arrange
        var searchResult = new ContentSearchResult
        {
            Id = "file:https://example.com/cbpx.dat",
            Name = "Control Bar Pro (Xezon) - 1080p (Recommended)",
            ProviderName = "communityoutpost",
            ContentType = ContentType.Addon,
            TargetGame = GameType.ZeroHour,
            Version = string.Empty,
            SourceUrl = "https://example.com/cbpx.dat",
        };
        searchResult.ResolverMetadata["contentCode"] = "cbpx";
        searchResult.ResolverMetadata["selectedVariant"] = "1080p";

        var builtManifest = new ContentManifest
        {
            Id = ManifestId.Create("1.0.communityoutpost.addon.cbpx1080p"),
            Name = "Control Bar Pro (Xezon)",
            ContentType = ContentType.Addon,
            TargetGame = GameType.ZeroHour,
            Files = [new ManifestFile { RelativePath = "cbpx.dat" }],
        };

        var builderMock = CreateBuilderMock(builtManifest);
        var resolver = new CommunityOutpostResolver(
            () => builderMock.Object,
            _providerLoaderMock.Object,
            NullLogger<CommunityOutpostResolver>.Instance);

        // Act
        var result = await resolver.ResolveAsync(searchResult, CancellationToken.None);

        // Assert
        Assert.True(result.Success);
        var manifest = result.Data;
        Assert.NotNull(manifest);
        Assert.False(string.IsNullOrWhiteSpace(manifest.Version));
        Assert.Equal("1.0", manifest.Version);
    }

    private static Mock<IContentManifestBuilder> CreateBuilderMock(ContentManifest builtManifest)
    {
        var builderMock = new Mock<IContentManifestBuilder>();
        builderMock.Setup(b => b.WithBasicInfo(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>()))
            .Returns(builderMock.Object);
        builderMock.Setup(b => b.WithContentType(It.IsAny<ContentType>(), It.IsAny<GameType>()))
            .Returns(builderMock.Object);
        builderMock.Setup(b => b.WithName(It.IsAny<string>()))
            .Returns(builderMock.Object);
        builderMock.Setup(b => b.WithId(It.IsAny<ManifestId>()))
            .Returns(builderMock.Object);
        builderMock.Setup(b => b.WithPublisher(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns(builderMock.Object);
        builderMock.Setup(b => b.WithMetadata(
                It.IsAny<string>(), It.IsAny<List<string>?>(), It.IsAny<string>(), It.IsAny<List<string>?>(), It.IsAny<string>()))
            .Returns(builderMock.Object);
        builderMock.Setup(b => b.WithInstallationInstructions(It.IsAny<WorkspaceStrategy>()))
            .Returns(builderMock.Object);
        builderMock.Setup(b => b.AddRemoteFileAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<ContentSourceType>(), It.IsAny<bool>(), It.IsAny<FilePermissions?>()))
            .ReturnsAsync(builderMock.Object);
        builderMock.Setup(b => b.AddDependency(
                It.IsAny<ManifestId>(),
                It.IsAny<string>(),
                It.IsAny<ContentType>(),
                It.IsAny<DependencyInstallBehavior>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<List<string>?>(),
                It.IsAny<bool>(),
                It.IsAny<List<ManifestId>?>(),
                It.IsAny<List<GameType>?>()))
            .Returns(builderMock.Object);
        builderMock.Setup(b => b.Build()).Returns(builtManifest);
        return builderMock;
    }
}
