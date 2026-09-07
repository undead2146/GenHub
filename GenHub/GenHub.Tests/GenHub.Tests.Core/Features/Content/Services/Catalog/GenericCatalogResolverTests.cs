using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using FluentAssertions;
using GenHub.Core.Constants;
using GenHub.Core.Interfaces.Common;
using GenHub.Core.Interfaces.Manifest;
using GenHub.Core.Interfaces.Tools;
using GenHub.Core.Models.Manifest;
using GenHub.Core.Models.Providers;
using GenHub.Core.Models.Results;
using GenHub.Core.Models.Results.Content;
using GenHub.Features.Content.Services.Catalog;
using GenHub.Features.Manifest;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using ContentType = GenHub.Core.Models.Enums.ContentType;
using GameType = GenHub.Core.Models.Enums.GameType;

namespace GenHub.Tests.Core.Features.Content.Services.Catalog;

/// <summary>
/// Unit and regression tests for <see cref="GenericCatalogResolver"/>.
/// </summary>
public class GenericCatalogResolverTests
{
    private readonly Func<IContentManifestBuilder> _builderFactory;

    /// <summary>
    /// Initializes a new instance of the <see cref="GenericCatalogResolverTests"/> class.
    /// </summary>
    public GenericCatalogResolverTests()
    {
        var manifestIdServiceMock = new Mock<IManifestIdService>();
        manifestIdServiceMock.Setup(x => x.GeneratePublisherContentId(It.IsAny<string>(), It.IsAny<ContentType>(), It.IsAny<string>(), It.IsAny<int>()))
            .Returns((string p, ContentType ct, string c, int v) =>
            {
                var generated = ManifestIdGenerator.GeneratePublisherContentId(p, ct, c, v);
                return OperationResult<ManifestId>.CreateSuccess(ManifestId.Create(generated));
            });

        _builderFactory = () => new ContentManifestBuilder(
            Mock.Of<ILogger<ContentManifestBuilder>>(),
            Mock.Of<IFileHashProvider>(),
            manifestIdServiceMock.Object,
            Mock.Of<IDownloadService>(),
            Mock.Of<IConfigurationProviderService>());
    }

    /// <summary>
    /// Verifies that colliding sanitized artifact filenames are disambiguated with a numeric index.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task ResolveAsync_CollidingFilenames_DisambiguatesWithNumericIndexAsync()
    {
        // Arrange
        var resolver = new GenericCatalogResolver(Mock.Of<ILogger<GenericCatalogResolver>>(), _builderFactory);

        var release = new ContentRelease
        {
            Version = "1.0",
            Artifacts =
            [
                new ReleaseArtifact { DownloadUrl = "https://example.com/downloads/file.zip", Filename = "file.zip", IsPrimary = true },
                new ReleaseArtifact { DownloadUrl = "https://example.com/mirror1/file.zip", Filename = "file.zip" },
                new ReleaseArtifact { DownloadUrl = "https://example.com/mirror2/file.zip", Filename = "file.zip" },
            ],
        };

        var contentItem = new CatalogContentItem
        {
            Id = "test-item",
            Name = "Test Item",
            ContentType = ContentType.Mod,
            TargetGame = GameType.ZeroHour,
        };

        var publisher = new PublisherProfile
        {
            Id = "community-outpost",
            Name = "Community Outpost",
        };

        var searchResult = new ContentSearchResult
        {
            Id = "1.0.community-outpost.mod.test-item",
        };
        searchResult.ResolverMetadata[CatalogConstants.ReleaseJsonMetadataKey] = JsonSerializer.Serialize(release);
        searchResult.ResolverMetadata[CatalogConstants.CatalogItemJsonMetadataKey] = JsonSerializer.Serialize(contentItem);
        searchResult.ResolverMetadata[CatalogConstants.PublisherProfileJsonMetadataKey] = JsonSerializer.Serialize(publisher);

        // Act
        var result = await resolver.ResolveAsync(searchResult);

        // Assert
        result.Success.Should().BeTrue(result.FirstError);
        result.Data.Should().NotBeNull();
        result.Data!.Files.Should().HaveCount(3);
        result.Data.Files.Select(f => f.RelativePath).Should().BeEquivalentTo([
            "file.zip",
            "file_1.zip",
            "file_2.zip",
        ]);
    }

    /// <summary>
    /// Verifies that multi-variant releases filter artifacts according to the primary artifact's variant.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task ResolveAsync_MultiVariantRelease_FiltersArtifactsToPrimaryVariantAsync()
    {
        // Arrange
        var resolver = new GenericCatalogResolver(Mock.Of<ILogger<GenericCatalogResolver>>(), _builderFactory);

        var release = new ContentRelease
        {
            Version = "1.0",
            Artifacts =
            [
                new ReleaseArtifact
                {
                    DownloadUrl = "https://example.com/zh.zip",
                    Filename = "zh.zip",
                    VariantAxis = "game-type",
                    Variant = "Zero Hour",
                    IsPrimary = true,
                },
                new ReleaseArtifact
                {
                    DownloadUrl = "https://example.com/gen.zip",
                    Filename = "gen.zip",
                    VariantAxis = "game-type",
                    Variant = "Generals",
                },
                new ReleaseArtifact
                {
                    DownloadUrl = "https://example.com/manual.pdf",
                    Filename = "manual.pdf",
                    VariantAxis = null,
                },
            ],
        };

        var contentItem = new CatalogContentItem
        {
            Id = "dual-mod",
            Name = "Dual Mod",
            ContentType = ContentType.Mod,
            TargetGame = GameType.ZeroHour,
        };

        var publisher = new PublisherProfile
        {
            Id = "community-outpost",
            Name = "Community Outpost",
        };

        var searchResult = new ContentSearchResult
        {
            Id = "1.0.community-outpost.mod.dual-mod",
        };
        searchResult.ResolverMetadata[CatalogConstants.ReleaseJsonMetadataKey] = JsonSerializer.Serialize(release);
        searchResult.ResolverMetadata[CatalogConstants.CatalogItemJsonMetadataKey] = JsonSerializer.Serialize(contentItem);
        searchResult.ResolverMetadata[CatalogConstants.PublisherProfileJsonMetadataKey] = JsonSerializer.Serialize(publisher);

        // Act
        var result = await resolver.ResolveAsync(searchResult);

        // Assert
        result.Success.Should().BeTrue(result.FirstError);
        result.Data.Should().NotBeNull();

        // Should include Zero Hour artifact and common manual, but EXCLUDE Generals artifact
        result.Data!.Files.Select(f => f.RelativePath).Should().BeEquivalentTo([
            "zh.zip",
            "manual.pdf",
        ]);
    }

    /// <summary>
    /// Verifies that SHA256 hashes are stamped per matching relative file path.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task ResolveAsync_MultipleArtifactsWithHashes_StampsSha256ByFilenameAsync()
    {
        // Arrange
        var resolver = new GenericCatalogResolver(Mock.Of<ILogger<GenericCatalogResolver>>(), _builderFactory);

        var release = new ContentRelease
        {
            Version = "1.0",
            Artifacts =
            [
                new ReleaseArtifact
                {
                    DownloadUrl = "https://example.com/primary.zip",
                    Filename = "primary.zip",
                    Sha256 = "1111111111111111111111111111111111111111111111111111111111111111",
                    IsPrimary = true,
                },
                new ReleaseArtifact
                {
                    DownloadUrl = "https://example.com/secondary.zip",
                    Filename = "secondary.zip",
                    Sha256 = "2222222222222222222222222222222222222222222222222222222222222222",
                },
            ],
        };

        var contentItem = new CatalogContentItem
        {
            Id = "hashed-item",
            Name = "Hashed Item",
            ContentType = ContentType.Mod,
            TargetGame = GameType.ZeroHour,
        };

        var publisher = new PublisherProfile
        {
            Id = "community-outpost",
            Name = "Community Outpost",
        };

        var searchResult = new ContentSearchResult
        {
            Id = "1.0.community-outpost.mod.hashed-item",
        };
        searchResult.ResolverMetadata[CatalogConstants.ReleaseJsonMetadataKey] = JsonSerializer.Serialize(release);
        searchResult.ResolverMetadata[CatalogConstants.CatalogItemJsonMetadataKey] = JsonSerializer.Serialize(contentItem);
        searchResult.ResolverMetadata[CatalogConstants.PublisherProfileJsonMetadataKey] = JsonSerializer.Serialize(publisher);

        // Act
        var result = await resolver.ResolveAsync(searchResult);

        // Assert
        result.Success.Should().BeTrue(result.FirstError);
        result.Data.Should().NotBeNull();
        result.Data!.Files.Should().HaveCount(2);

        var primaryFile = result.Data.Files.First(f => f.RelativePath == "primary.zip");
        var secondaryFile = result.Data.Files.First(f => f.RelativePath == "secondary.zip");

        primaryFile.Hash.Should().Be("1111111111111111111111111111111111111111111111111111111111111111");
        secondaryFile.Hash.Should().Be("2222222222222222222222222222222222222222222222222222222222222222");
    }
}
