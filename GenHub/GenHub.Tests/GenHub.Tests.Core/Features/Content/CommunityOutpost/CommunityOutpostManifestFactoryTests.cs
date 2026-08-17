using GenHub.Core.Interfaces.Common;
using GenHub.Core.Models.CommunityOutpost;
using GenHub.Core.Models.Enums;
using GenHub.Core.Models.Manifest;
using GenHub.Features.Content.Services.CommunityOutpost;
using Microsoft.Extensions.Logging;
using Moq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace GenHub.Tests.Core.Features.Content.CommunityOutpost;

/// <summary>
/// Tests for CommunityOutpostManifestFactory.
/// </summary>
public class CommunityOutpostManifestFactoryTests : IDisposable
{
    private readonly Mock<ILogger<CommunityOutpostManifestFactory>> _loggerMock;
    private readonly Mock<IFileHashProvider> _hashProviderMock;
    private readonly CommunityOutpostManifestFactory _factory;
    private readonly string _tempDir;

    /// <summary>
    /// Initializes a new instance of the <see cref="CommunityOutpostManifestFactoryTests"/> class.
    /// </summary>
    public CommunityOutpostManifestFactoryTests()
    {
        _loggerMock = new Mock<ILogger<CommunityOutpostManifestFactory>>();
        _hashProviderMock = new Mock<IFileHashProvider>();

        _hashProviderMock.Setup(x => x.ComputeFileHashAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("abc123hash");

        var payloadProcessor = new GenHub.Features.Content.Services.Common.ArchivePayloadProcessor(
            new Mock<ILogger<GenHub.Features.Content.Services.Common.ArchivePayloadProcessor>>().Object);

        _factory = new CommunityOutpostManifestFactory(_loggerMock.Object, _hashProviderMock.Object, payloadProcessor);
        _tempDir = Path.Combine(Path.GetTempPath(), "GenHubTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    /// <summary>
    /// Disposes of the test directory.
    /// </summary>
    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, true);
        }

        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Verifies that multiple variants are correctly split into manifests.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Fact]
    public async Task CreateManifestsFromExtractedContentAsync_WithHleiPackage_ShouldSplitIntoMultipleManifestsAsync()
    {
        // Arrange
        var zhEnDir = Path.Combine(_tempDir, "ZH", "BIG EN");
        var zhDeDir = Path.Combine(_tempDir, "ZH", "BIG DE");
        var ccgEnDir = Path.Combine(_tempDir, "CCG", "BIG EN");

        Directory.CreateDirectory(zhEnDir);
        Directory.CreateDirectory(zhDeDir);
        Directory.CreateDirectory(ccgEnDir);

        File.WriteAllText(Path.Combine(zhEnDir, "!HotkeysLeikezeENZH.big"), "mock content");
        File.WriteAllText(Path.Combine(zhDeDir, "!HotkeysLeikezeDEZH.big"), "mock content");
        File.WriteAllText(Path.Combine(ccgEnDir, "!HotkeysLeikezeEN.big"), "mock content");

        var originalManifest = new ContentManifest
        {
            Id = ManifestId.Create("1.0.communityoutpost.addon.hlei"),
            Name = "Leikeze's Hotkeys",
            ContentType = GenHub.Core.Models.Enums.ContentType.Addon,
            Publisher = new PublisherInfo { PublisherType = "communityoutpost" },
            Metadata = new ContentMetadata
            {
                Tags = ["contentCode:hlei"],
            },
        };

        // Act
        var manifests = await _factory.CreateManifestsFromExtractedContentAsync(originalManifest, _tempDir);

        // Assert
        Assert.Equal(3, manifests.Count);

        var zhEnManifest = manifests.FirstOrDefault(m => m.Id.Value.Contains("-zerohour-en"));
        Assert.NotNull(zhEnManifest);
        Assert.Equal(GameType.ZeroHour, zhEnManifest.TargetGame);
        Assert.Contains("(EN)", zhEnManifest.Name);
        Assert.Single(zhEnManifest.Files);

        var zhDeManifest = manifests.FirstOrDefault(m => m.Id.Value.Contains("-zerohour-de"));
        Assert.NotNull(zhDeManifest);
        Assert.Equal(GameType.ZeroHour, zhDeManifest.TargetGame);
        Assert.Contains("(DE)", zhDeManifest.Name);

        var ccgEnManifest = manifests.FirstOrDefault(m => m.Id.Value.Contains("-generals-en"));
        Assert.NotNull(ccgEnManifest);
        Assert.Equal(GameType.Generals, ccgEnManifest.TargetGame);
        Assert.Contains("[Generals]", ccgEnManifest.Name);
    }

    /// <summary>
    /// Verifies that content with no variants returns a single manifest.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Fact]
    public async Task CreateManifestsFromExtractedContentAsync_WithNoVariants_ShouldReturnSingleManifestAsync()
    {
        // Arrange
        File.WriteAllText(Path.Combine(_tempDir, "mod.big"), "mock content");

        var originalManifest = new ContentManifest
        {
            Id = ManifestId.Create("1.0.communityoutpost.addon.gent"),
            Name = "GenTool",
            ContentType = GenHub.Core.Models.Enums.ContentType.Addon,
            Publisher = new PublisherInfo { PublisherType = "communityoutpost" },
            Metadata = new ContentMetadata
            {
                Tags = ["contentCode:gent"],
            },
        };

        // Act
        var manifests = await _factory.CreateManifestsFromExtractedContentAsync(originalManifest, _tempDir);

        // Assert
        Assert.Single(manifests);
        Assert.Equal("1.0.communityoutpost.addon.gent", manifests[0].Id.Value);
        Assert.Single(manifests[0].Files);
    }

    /// <summary>
    /// Verifies that a runtime dependency not bundled as a BIG file remains in the final manifest.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task CreateManifestsFromExtractedContentAsync_LegionnairesHotkeys_PreservesGenToolDependencyAsync()
    {
        // Arrange
        File.WriteAllText(Path.Combine(_tempDir, "!HotkeysLegionnaireZH.big"), "mock content");
        var originalManifest = new ContentManifest
        {
            Id = ManifestId.Create("1.0.communityoutpost.addon.hleg"),
            Name = "Legionnaire's Hotkeys",
            ContentType = GenHub.Core.Models.Enums.ContentType.Addon,
            Publisher = new PublisherInfo { PublisherType = "communityoutpost" },
            Metadata = new ContentMetadata { Tags = ["contentCode:hleg"] },
        };

        // Act
        var manifest = Assert.Single(await _factory.CreateManifestsFromExtractedContentAsync(originalManifest, _tempDir));

        // Assert
        Assert.Contains(manifest.Dependencies, dependency =>
            dependency.Id.Value.EndsWith(".gent", StringComparison.OrdinalIgnoreCase) &&
            dependency.InstallBehavior == DependencyInstallBehavior.AutoInstall);
    }

    /// <summary>
    /// Flat Control Bar layouts place BIGs at the extract root even when a ZH folder exists
    /// from merged language dependencies — storage must use the root, not ZH.
    /// </summary>
    [Fact]
    public void GetManifestDirectory_RootRelativeFiles_IgnoresSiblingZhFolder()
    {
        var zhDir = Path.Combine(_tempDir, "ZH");
        Directory.CreateDirectory(zhDir);
        File.WriteAllText(Path.Combine(_tempDir, "340_ControlBarPro1080ZH.big"), "mock");

        var manifest = new ContentManifest
        {
            Id = ManifestId.Create("1.0.communityoutpost.addon.cbpx-1080p"),
            Name = "Control Bar Pro (Xezon) - 1080p (Recommended)",
            TargetGame = GameType.ZeroHour,
            Files =
            [
                new ManifestFile
                {
                    RelativePath = "340_ControlBarPro1080ZH.big",
                    SourcePath = Path.Combine(_tempDir, "340_ControlBarPro1080ZH.big"),
                },
            ],
        };

        var directory = _factory.GetManifestDirectory(manifest, _tempDir);

        Assert.Equal(Path.GetFullPath(_tempDir), Path.GetFullPath(directory));
    }

    /// <summary>
    /// Verifies that unextracted zip archives in the staging directory are automatically unpacked.
    /// </summary>
    /// <returns>A task representing the asynchronous unit test.</returns>
    [Fact]
    public async Task CreateManifestsFromExtractedContentAsync_ZipArchivePresent_ExtractsFilesAndDeletesZipAsync()
    {
        // Arrange
        var zipPath = Path.Combine(_tempDir, "community-patch.zip");
        {
            using var zip = System.IO.Compression.ZipFile.Open(zipPath, System.IO.Compression.ZipArchiveMode.Create);
            {
                using var writer1 = new StreamWriter(zip.CreateEntry("generals.exe").Open());
                writer1.Write("mock exe content");
            }

            {
                using var writer2 = new StreamWriter(zip.CreateEntry("Patch.big").Open());
                writer2.Write("mock big content");
            }
        }

        var originalManifest = new ContentManifest
        {
            Id = ManifestId.Create("1.0.communityoutpost.gameclient.communitypatch"),
            Name = "Community Patch",
            ContentType = GenHub.Core.Models.Enums.ContentType.GameClient,
            TargetGame = GameType.ZeroHour,
            Publisher = new PublisherInfo { PublisherType = "communityoutpost" },
            Metadata = new ContentMetadata { Tags = ["contentCode:community-patch"] },
        };

        // Act
        var manifests = await _factory.CreateManifestsFromExtractedContentAsync(originalManifest, _tempDir);
        var manifest = Assert.Single(manifests);

        // Assert
        Assert.False(File.Exists(zipPath), "Archive file should have been deleted after extraction.");
        Assert.Contains(manifest.Files, f => f.RelativePath.Equals("generals.exe", StringComparison.OrdinalIgnoreCase) && f.IsExecutable);
        Assert.Contains(manifest.Files, f => f.RelativePath.Equals("Patch.big", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Verifies that when a single variant is requested via SelectedVariantId, only that variant manifest is created.
    /// </summary>
    /// <returns>A task representing the asynchronous unit test.</returns>
    [Fact]
    public async Task CreateManifestsFromExtractedContentAsync_WithSelectedVariantId_BuildsOnlyRequestedVariantAsync()
    {
        // Arrange
        File.WriteAllText(Path.Combine(_tempDir, "340_ControlBarPro1080ZH.big"), "mock big content");

        var originalManifest = new ContentManifest
        {
            Id = ManifestId.Create("1.0.communityoutpost.addon.cbpr1080p"),
            Name = "Control Bar Pro (ExiLe)",
            ContentType = GenHub.Core.Models.Enums.ContentType.Addon,
            TargetGame = GameType.ZeroHour,
            Publisher = new PublisherInfo { PublisherType = "communityoutpost" },
            Metadata = new ContentMetadata
            {
                Tags = ["contentCode:cbpr"],
                SelectedVariantId = "1080p",
            },
        };

        // Act
        var manifests = await _factory.CreateManifestsFromExtractedContentAsync(originalManifest, _tempDir);

        // Assert
        var manifest = Assert.Single(manifests);
        Assert.Equal("1.0.communityoutpost.addon.cbpr-1080p", manifest.Id.Value);
        Assert.Equal("communityoutpost.addon.cbpr", manifest.Metadata?.VariantGroupId);
        Assert.Equal("1080p", manifest.Metadata?.SelectedVariantId);
        Assert.Contains("1080p", manifest.Name);
    }

    /// <summary>
    /// Verifies that when a single variant is requested via tags, only that variant manifest is created.
    /// </summary>
    /// <returns>A task representing the asynchronous unit test.</returns>
    [Fact]
    public async Task CreateManifestsFromExtractedContentAsync_WithRequestedVariantTag_BuildsOnlyRequestedVariantAsync()
    {
        // Arrange
        File.WriteAllText(Path.Combine(_tempDir, "340_ControlBarPro1440ZH.big"), "mock big content");

        var originalManifest = new ContentManifest
        {
            Id = ManifestId.Create("1.0.communityoutpost.addon.cbpr"),
            Name = "Control Bar Pro (ExiLe)",
            ContentType = GenHub.Core.Models.Enums.ContentType.Addon,
            TargetGame = GameType.ZeroHour,
            Publisher = new PublisherInfo { PublisherType = "communityoutpost" },
            Metadata = new ContentMetadata
            {
                Tags = ["contentCode:cbpr", "requestedVariant:1440p"],
            },
        };

        // Act
        var manifests = await _factory.CreateManifestsFromExtractedContentAsync(originalManifest, _tempDir);

        // Assert
        var manifest = Assert.Single(manifests);
        Assert.Equal("1.0.communityoutpost.addon.cbpr-1440p", manifest.Id.Value);
        Assert.Equal("communityoutpost.addon.cbpr", manifest.Metadata?.VariantGroupId);
        Assert.Equal("1440p", manifest.Metadata?.SelectedVariantId);
    }

    /// <summary>
    /// Verifies that when manifest ID has a hyphenated variant suffix, only that variant manifest is created.
    /// </summary>
    /// <returns>A task representing the asynchronous unit test.</returns>
    [Fact]
    public async Task CreateManifestsFromExtractedContentAsync_WithHyphenatedVariantInId_BuildsOnlyRequestedVariantAsync()
    {
        // Arrange
        File.WriteAllText(Path.Combine(_tempDir, "340_ControlBarPro1080ZH.big"), "mock big content");

        var originalManifest = new ContentManifest
        {
            Id = ManifestId.Create("1.0.communityoutpost.addon.cbpr-1080p"),
            Name = "Control Bar Pro (ExiLe) - 1080p (Recommended)",
            ContentType = GenHub.Core.Models.Enums.ContentType.Addon,
            TargetGame = GameType.ZeroHour,
            Publisher = new PublisherInfo { PublisherType = "communityoutpost" },
            Metadata = new ContentMetadata
            {
                Tags = ["contentCode:cbpr"],
            },
        };

        // Act
        var manifests = await _factory.CreateManifestsFromExtractedContentAsync(originalManifest, _tempDir);

        // Assert
        var manifest = Assert.Single(manifests);
        Assert.Equal("1.0.communityoutpost.addon.cbpr-1080p", manifest.Id.Value);
        Assert.Equal("communityoutpost.addon.cbpr", manifest.Metadata?.VariantGroupId);
        Assert.Equal("1080p", manifest.Metadata?.SelectedVariantId);
    }

    /// <summary>
    /// Verifies that when original manifest has an empty version, a valid default version is set on the created manifest.
    /// </summary>
    /// <returns>A task representing the asynchronous unit test.</returns>
    [Fact]
    public async Task CreateManifestsFromExtractedContentAsync_WithEmptyVersion_SetsValidDefaultVersionAsync()
    {
        // Arrange
        File.WriteAllText(Path.Combine(_tempDir, "340_ControlBarPro1080ZH.big"), "mock big content");

        var originalManifest = new ContentManifest
        {
            Id = ManifestId.Create("1.0.communityoutpost.addon.cbpx-1080p"),
            Name = "Control Bar Pro (Xezon) - 1080p (Recommended)",
            Version = string.Empty,
            ContentType = GenHub.Core.Models.Enums.ContentType.Addon,
            TargetGame = GameType.ZeroHour,
            Publisher = new PublisherInfo { PublisherType = "communityoutpost" },
            Metadata = new ContentMetadata
            {
                Tags = ["contentCode:cbpx"],
            },
        };

        // Act
        var manifests = await _factory.CreateManifestsFromExtractedContentAsync(originalManifest, _tempDir);

        // Assert
        var manifest = Assert.Single(manifests);
        Assert.False(string.IsNullOrWhiteSpace(manifest.Version));
        Assert.Equal("1.0", manifest.Version);
    }
}
