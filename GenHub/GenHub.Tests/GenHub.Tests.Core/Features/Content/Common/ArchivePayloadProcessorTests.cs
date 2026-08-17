using System;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;
using GenHub.Core.Models.Enums;
using GenHub.Features.Content.Services.Common;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using ContentType = GenHub.Core.Models.Enums.ContentType;

namespace GenHub.Tests.Core.Features.Content.Common;

/// <summary>
/// Unit tests for archive payload processing and directory structure normalization.
/// </summary>
public sealed class ArchivePayloadProcessorTests : IDisposable
{
    private readonly string _stagingDirectory = Path.Combine(Path.GetTempPath(), "GenHubPayloadTests", Guid.NewGuid().ToString("N"));

    /// <summary>
    /// Verifies that extracting a valid ZIP archive unpacks all entries and removes the archive file.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Fact]
    public async Task ExtractArchivesSafelyAsync_ValidZip_ExtractsAllEntriesAndDeletesZipAsync()
    {
        // Arrange
        Directory.CreateDirectory(_stagingDirectory);
        var zipPath = Path.Combine(_stagingDirectory, "test.zip");
        using (var archive = ZipFile.Open(zipPath, ZipArchiveMode.Create))
        {
            using (var writer1 = new StreamWriter(archive.CreateEntry("Data/INI/GameData.ini").Open()))
            {
                await writer1.WriteAsync("GameData=1");
            }

            using (var writer2 = new StreamWriter(archive.CreateEntry("Art/Textures/test.tga").Open()))
            {
                await writer2.WriteAsync("Texture");
            }
        }

        var processor = CreateProcessor();

        // Act
        await processor.ExtractArchivesSafelyAsync(_stagingDirectory);

        // Assert
        Assert.False(File.Exists(zipPath));
        Assert.True(File.Exists(Path.Combine(_stagingDirectory, "Data", "INI", "GameData.ini")));
        Assert.True(File.Exists(Path.Combine(_stagingDirectory, "Art", "Textures", "test.tga")));
    }

    /// <summary>
    /// Verifies that multi-level nested wrapper directories (e.g. ModDB mods like C&amp;C Generals Undone)
    /// are recursively flattened so game assets end up directly at the workspace root.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Fact]
    public async Task NormalizeDirectoryStructureAsync_MultiLevelSingleWrapper_FlattensToRootAsync()
    {
        // Arrange
        var nestedDir = Path.Combine(_stagingDirectory, "C&C Generals Undone v1.0", "C&C Generals Undone v1.0");
        Directory.CreateDirectory(Path.Combine(nestedDir, "Art", "Textures"));
        Directory.CreateDirectory(Path.Combine(nestedDir, "Data", "INI"));
        Directory.CreateDirectory(Path.Combine(nestedDir, "Window"));

        await File.WriteAllTextAsync(Path.Combine(nestedDir, "Readme.txt"), "Generals Undone Readme");
        await File.WriteAllTextAsync(Path.Combine(nestedDir, "Art", "Textures", "test.tga"), "texture data");
        await File.WriteAllTextAsync(Path.Combine(nestedDir, "Data", "INI", "GameData.ini"), "data");
        await File.WriteAllTextAsync(Path.Combine(nestedDir, "Window", "MainMenu.wnd"), "window");

        var processor = CreateProcessor();

        // Act
        await processor.NormalizeDirectoryStructureAsync(_stagingDirectory, ContentType.Mod, GameType.ZeroHour);

        // Assert
        Assert.True(File.Exists(Path.Combine(_stagingDirectory, "Readme.txt")));
        Assert.True(File.Exists(Path.Combine(_stagingDirectory, "Art", "Textures", "test.tga")));
        Assert.True(File.Exists(Path.Combine(_stagingDirectory, "Data", "INI", "GameData.ini")));
        Assert.True(File.Exists(Path.Combine(_stagingDirectory, "Window", "MainMenu.wnd")));

        // Old wrapper paths should no longer exist
        Assert.False(Directory.Exists(Path.Combine(_stagingDirectory, "C&C Generals Undone v1.0")));
    }

    /// <summary>
    /// Verifies that loose documentation files at root alongside a single mod wrapper directory
    /// are reconciled by promoting the mod contents to the root and keeping the documentation files.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Fact]
    public async Task NormalizeDirectoryStructureAsync_LooseReadmeWithModWrapper_FlattensModWrapperAlongsideReadmeAsync()
    {
        // Arrange
        Directory.CreateDirectory(_stagingDirectory);
        await File.WriteAllTextAsync(Path.Combine(_stagingDirectory, "Readme.txt"), "Important instructions");
        await File.WriteAllTextAsync(Path.Combine(_stagingDirectory, "ModDB_Link.url"), "https://www.moddb.com");

        var modDir = Path.Combine(_stagingDirectory, "GeneralsUndone");
        Directory.CreateDirectory(Path.Combine(modDir, "Data", "INI"));
        Directory.CreateDirectory(Path.Combine(modDir, "Art", "Textures"));
        await File.WriteAllTextAsync(Path.Combine(modDir, "Data", "INI", "GameData.ini"), "inidata");
        await File.WriteAllTextAsync(Path.Combine(modDir, "Art", "Textures", "unit.tga"), "tgadata");

        var processor = CreateProcessor();

        // Act
        await processor.NormalizeDirectoryStructureAsync(_stagingDirectory, ContentType.Mod, GameType.ZeroHour);

        // Assert
        Assert.True(File.Exists(Path.Combine(_stagingDirectory, "Readme.txt")));
        Assert.True(File.Exists(Path.Combine(_stagingDirectory, "ModDB_Link.url")));
        Assert.True(File.Exists(Path.Combine(_stagingDirectory, "Data", "INI", "GameData.ini")));
        Assert.True(File.Exists(Path.Combine(_stagingDirectory, "Art", "Textures", "unit.tga")));
        Assert.False(Directory.Exists(modDir));
    }

    /// <summary>
    /// Verifies that game-specific subdirectories matching the target game (e.g. "Zero Hour")
    /// are promoted to the payload root.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Fact]
    public async Task NormalizeDirectoryStructureAsync_GameSpecificSubdirectory_PromotesMatchingTargetGameFolderAsync()
    {
        // Arrange
        var zhDir = Path.Combine(_stagingDirectory, "Zero Hour", "Data", "INI");
        Directory.CreateDirectory(zhDir);
        await File.WriteAllTextAsync(Path.Combine(zhDir, "ZHData.ini"), "zh config");

        var processor = CreateProcessor();

        // Act
        await processor.NormalizeDirectoryStructureAsync(_stagingDirectory, ContentType.Mod, GameType.ZeroHour);

        // Assert
        Assert.True(File.Exists(Path.Combine(_stagingDirectory, "Data", "INI", "ZHData.ini")));
        Assert.False(Directory.Exists(Path.Combine(_stagingDirectory, "Zero Hour")));
    }

    /// <summary>
    /// Verifies that single map directories for ContentType.Map are preserved with their map folder.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Fact]
    public async Task NormalizeDirectoryStructureAsync_MapContent_PreservesSingleMapDirectoryAsync()
    {
        // Arrange
        var mapDir = Path.Combine(_stagingDirectory, "Lemuria");
        Directory.CreateDirectory(mapDir);
        await File.WriteAllTextAsync(Path.Combine(mapDir, "Lemuria.map"), "map payload");
        await File.WriteAllTextAsync(Path.Combine(mapDir, "Lemuria.tga"), "preview payload");

        var processor = CreateProcessor();

        // Act
        await processor.NormalizeDirectoryStructureAsync(_stagingDirectory, ContentType.Map, GameType.ZeroHour);

        // Assert
        Assert.True(Directory.Exists(mapDir));
        Assert.True(File.Exists(Path.Combine(mapDir, "Lemuria.map")));
        Assert.True(File.Exists(Path.Combine(mapDir, "Lemuria.tga")));
    }

    /// <summary>
    /// Verifies that double-wrapped map archives (e.g. MapDownload/MapName/MapName.map)
    /// strip only the outer wrapper while preserving the inner map folder.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Fact]
    public async Task NormalizeDirectoryStructureAsync_MapContentWithDoubleWrapper_FlattensOuterWrapperOnlyAsync()
    {
        // Arrange
        var outerWrapper = Path.Combine(_stagingDirectory, "MapDownloadWrapper");
        var mapDir = Path.Combine(outerWrapper, "Lemuria");
        Directory.CreateDirectory(mapDir);
        await File.WriteAllTextAsync(Path.Combine(mapDir, "Lemuria.map"), "map payload");
        await File.WriteAllTextAsync(Path.Combine(mapDir, "Lemuria.tga"), "preview payload");

        var processor = CreateProcessor();

        // Act
        await processor.NormalizeDirectoryStructureAsync(_stagingDirectory, ContentType.Map, GameType.ZeroHour);

        // Assert
        Assert.False(Directory.Exists(outerWrapper));
        Assert.True(Directory.Exists(Path.Combine(_stagingDirectory, "Lemuria")));
        Assert.True(File.Exists(Path.Combine(_stagingDirectory, "Lemuria", "Lemuria.map")));
    }

    /// <summary>
    /// Verifies that system junk files (.DS_Store, Thumbs.db, desktop.ini, __MACOSX)
    /// are purged during normalization.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Fact]
    public async Task NormalizeDirectoryStructureAsync_PurgesSystemJunkAsync()
    {
        // Arrange
        Directory.CreateDirectory(Path.Combine(_stagingDirectory, "__MACOSX"));
        Directory.CreateDirectory(Path.Combine(_stagingDirectory, "Data"));

        await File.WriteAllTextAsync(Path.Combine(_stagingDirectory, ".DS_Store"), "junk");
        await File.WriteAllTextAsync(Path.Combine(_stagingDirectory, "Thumbs.db"), "junk");
        await File.WriteAllTextAsync(Path.Combine(_stagingDirectory, "desktop.ini"), "junk");
        await File.WriteAllTextAsync(Path.Combine(_stagingDirectory, "__MACOSX", "._something"), "junk");
        await File.WriteAllTextAsync(Path.Combine(_stagingDirectory, "Data", "GameData.ini"), "real data");

        var processor = CreateProcessor();

        // Act
        await processor.NormalizeDirectoryStructureAsync(_stagingDirectory, ContentType.Mod, GameType.ZeroHour);

        // Assert
        Assert.False(File.Exists(Path.Combine(_stagingDirectory, ".DS_Store")));
        Assert.False(File.Exists(Path.Combine(_stagingDirectory, "Thumbs.db")));
        Assert.False(File.Exists(Path.Combine(_stagingDirectory, "desktop.ini")));
        Assert.False(Directory.Exists(Path.Combine(_stagingDirectory, "__MACOSX")));
        Assert.True(File.Exists(Path.Combine(_stagingDirectory, "Data", "GameData.ini")));
    }

    /// <summary>
    /// Verifies that an HTML error page pretending to be an archive is rejected.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Fact]
    public async Task ExtractArchivesSafelyAsync_HtmlErrorPayload_ThrowsInvalidDataExceptionAsync()
    {
        // Arrange
        Directory.CreateDirectory(_stagingDirectory);
        var fakeZip = Path.Combine(_stagingDirectory, "broken.zip");
        await File.WriteAllTextAsync(fakeZip, "<!DOCTYPE html><html><body>Error 404 Not Found</body></html>");

        var processor = CreateProcessor();

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidDataException>(
            () => processor.ExtractArchivesSafelyAsync(_stagingDirectory));
        Assert.Contains("HTML", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Cleans up test staging directory.
    /// </summary>
    public void Dispose()
    {
        if (Directory.Exists(_stagingDirectory))
        {
            Directory.Delete(_stagingDirectory, recursive: true);
        }
    }

    private static ArchivePayloadProcessor CreateProcessor()
    {
        return new ArchivePayloadProcessor(new Mock<ILogger<ArchivePayloadProcessor>>().Object);
    }
}
