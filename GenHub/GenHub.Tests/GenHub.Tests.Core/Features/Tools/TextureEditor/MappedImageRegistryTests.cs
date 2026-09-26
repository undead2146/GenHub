using GenHub.Core.Services.Tools.TextureEditor;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace GenHub.Tests.Core.Features.Tools.TextureEditor;

/// <summary>
/// Unit tests for <see cref="MappedImageRegistry"/>.
/// </summary>
public sealed class MappedImageRegistryTests
{
    private readonly MappedImageRegistry _registry = new(
        new SageMappedImageParser(NullLogger<SageMappedImageParser>.Instance),
        NullLogger<MappedImageRegistry>.Instance);

    /// <summary>
    /// Verifies that scanning indexes entries from nested INI files.
    /// </summary>
    /// <returns>A task representing the asynchronous unit test.</returns>
    [Fact]
    public async Task ScanDirectoryAsync_NestedInis_IndexesAllEntriesAsync()
    {
        string directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(Path.Combine(directory, "Nested"));
        try
        {
            await File.WriteAllTextAsync(Path.Combine(directory, "First.ini"), Block("Alpha", "a.tga"));
            await File.WriteAllTextAsync(Path.Combine(directory, "Nested", "Second.ini"), Block("Beta", "b.tga"));

            var result = await _registry.ScanDirectoryAsync(directory);

            Assert.True(result.Success);
            Assert.NotNull(result.Data);
            Assert.Equal(2, result.Data.FilesScanned);
            Assert.Equal(2, _registry.Count);
            Assert.NotNull(_registry.GetByName("Alpha"));
            Assert.NotNull(_registry.GetByName("Beta"));
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    /// <summary>
    /// Verifies that later alphabetical files override earlier entries like SAGE load order.
    /// </summary>
    /// <returns>A task representing the asynchronous unit test.</returns>
    [Fact]
    public async Task ScanDirectoryAsync_DuplicateNames_LastAlphabeticalWinsAsync()
    {
        string directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(directory);
        try
        {
            await File.WriteAllTextAsync(Path.Combine(directory, "a_first.ini"), Block("Dupe", "early.tga"));
            await File.WriteAllTextAsync(Path.Combine(directory, "z_last.ini"), Block("Dupe", "late.tga"));

            var result = await _registry.ScanDirectoryAsync(directory);

            Assert.True(result.Success);
            Assert.Equal("late.tga", _registry.GetByName("Dupe")?.TextureFileName);
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    /// <summary>
    /// Verifies that HandCreated overrides win over TextureSize files regardless of alphabetical order.
    /// </summary>
    /// <returns>A task representing the asynchronous unit test.</returns>
    [Fact]
    public async Task ScanDirectoryAsync_HandCreatedOverride_WinsOverTextureSizeAsync()
    {
        string directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(Path.Combine(directory, "TextureSize_512"));
        Directory.CreateDirectory(Path.Combine(directory, "HandCreated"));
        try
        {
            await File.WriteAllTextAsync(Path.Combine(directory, "TextureSize_512", "z_size.ini"), Block("Dupe", "size.tga"));
            await File.WriteAllTextAsync(Path.Combine(directory, "HandCreated", "a_hand.ini"), Block("Dupe", "hand.tga"));
            await File.WriteAllTextAsync(Path.Combine(directory, "z_base.ini"), Block("Dupe", "base.tga"));

            var result = await _registry.ScanDirectoryAsync(directory);

            Assert.True(result.Success);
            Assert.Equal("hand.tga", _registry.GetByName("Dupe")?.TextureFileName);
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    /// <summary>
    /// Verifies that name lookups are case-insensitive.
    /// </summary>
    /// <returns>A task representing the asynchronous unit test.</returns>
    [Fact]
    public async Task GetByName_MixedCase_FindsEntryAsync()
    {
        string directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(directory);
        try
        {
            await File.WriteAllTextAsync(Path.Combine(directory, "names.ini"), Block("SCC Ranger", "a.tga"));

            await _registry.ScanDirectoryAsync(directory);

            Assert.NotNull(_registry.GetByName("scc ranger"));
            Assert.NotNull(_registry.GetByName("SCC RANGER"));
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    /// <summary>
    /// Verifies that texture lookups return matching entries ordered by name.
    /// </summary>
    /// <returns>A task representing the asynchronous unit test.</returns>
    [Fact]
    public async Task GetByTexture_SharedTexture_ReturnsOrderedMatchesAsync()
    {
        string directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(directory);
        try
        {
            await File.WriteAllTextAsync(Path.Combine(directory, "shared.ini"), Block("Zebra", "shared.tga") + Block("Apple", "shared.tga") + Block("Other", "other.tga"));

            await _registry.ScanDirectoryAsync(directory);
            var matches = _registry.GetByTexture("SHARED.tga");

            Assert.Equal(2, matches.Count);
            Assert.Equal("Apple", matches[0].Name);
            Assert.Equal("Zebra", matches[1].Name);
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    /// <summary>
    /// Verifies that scanning a missing directory returns a failure.
    /// </summary>
    /// <returns>A task representing the asynchronous unit test.</returns>
    [Fact]
    public async Task ScanDirectoryAsync_MissingDirectory_ReturnsFailureAsync()
    {
        var result = await _registry.ScanDirectoryAsync(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString()));

        Assert.True(result.Failed);
        Assert.Equal(0, _registry.Count);
    }

    /// <summary>
    /// Verifies that clear removes all indexed entries.
    /// </summary>
    /// <returns>A task representing the asynchronous unit test.</returns>
    [Fact]
    public async Task Clear_AfterScan_RemovesAllEntriesAsync()
    {
        string directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(directory);
        try
        {
            await File.WriteAllTextAsync(Path.Combine(directory, "one.ini"), Block("Solo", "a.tga"));
            await _registry.ScanDirectoryAsync(directory);
            Assert.Equal(1, _registry.Count);

            _registry.Clear();

            Assert.Equal(0, _registry.Count);
            Assert.Empty(_registry.All);
            Assert.Null(_registry.GetByName("Solo"));
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    private static string Block(string name, string texture) =>
        $"MappedImage {name}\n  Texture = {texture}\n  TextureWidth = 64\n  TextureHeight = 64\n  Coords = Left:0 Top:0 Right:63 Bottom:63\n  Status = NONE\nEnd\n";
}
