using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using GenHub.Core.Constants;
using GenHub.Core.Helpers;
using GenHub.Core.Interfaces.Content;
using GenHub.Core.Models.Enums;
using GenHub.Core.Utilities;
using Microsoft.Extensions.Logging;
using SharpCompress.Archives;
using SharpCompress.Common;

namespace GenHub.Features.Content.Services.Common;

/// <summary>
/// Service for safely extracting archives and normalizing payload directory structures for game workspaces.
/// </summary>
public class ArchivePayloadProcessor(ILogger<ArchivePayloadProcessor> logger) : IArchivePayloadProcessor
{
    private const int MaxNestedExtractionDepth = 5;

    /// <inheritdoc />
    public async Task ExtractArchivesSafelyAsync(string extractedDirectory, CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(extractedDirectory))
        {
            return;
        }

        var depth = 0;
        while (depth < MaxNestedExtractionDepth)
        {
            cancellationToken.ThrowIfCancellationRequested();
            depth++;

            var archiveFiles = FindArchiveFiles(extractedDirectory);
            if (archiveFiles.Count == 0)
            {
                break;
            }

            logger.LogInformation(
                "Found {Count} archive(s) to extract in payload directory: {Directory} (pass {Pass})",
                archiveFiles.Count,
                extractedDirectory,
                depth);

            foreach (var archivePath in archiveFiles)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    EnsureValidArchivePayload(archivePath);
                    logger.LogInformation("Extracting archive safely: {ArchivePath}", archivePath);

                    ExtractSingleArchive(archivePath, extractedDirectory, cancellationToken);
                    File.Delete(archivePath);
                    logger.LogInformation("Extracted archive and removed archive source: {ArchivePath}", archivePath);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to extract archive: {ArchivePath}", archivePath);
                    throw;
                }
            }
        }

        await Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task NormalizeDirectoryStructureAsync(
        string extractedDirectory,
        ContentType contentType,
        GameType targetGame,
        CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(extractedDirectory))
        {
            return;
        }

        cancellationToken.ThrowIfCancellationRequested();

        // 1. Purge system junk files and folders
        PurgeSystemJunk(extractedDirectory);

        // 2. Iteratively strip single wrapper directories
        StripSingleWrapperDirectories(extractedDirectory, contentType, cancellationToken);

        // 3. Handle game-specific subdirectories (e.g. ZH, Zero Hour, Generals, CCG)
        RouteGameSpecificSubdirectories(extractedDirectory, targetGame, cancellationToken);

        // 4. Heuristic root content detection (single mod directory alongside loose documentation files)
        ReconcileContentRootWithDocumentation(extractedDirectory, contentType, cancellationToken);

        // 5. Cleanup empty directories
        CleanupEmptyDirectories(extractedDirectory);

        await Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task ProcessPayloadAsync(
        string extractedDirectory,
        ContentType contentType,
        GameType targetGame,
        CancellationToken cancellationToken = default)
    {
        await ExtractArchivesSafelyAsync(extractedDirectory, cancellationToken);
        await NormalizeDirectoryStructureAsync(extractedDirectory, contentType, targetGame, cancellationToken);
    }

    private static IReadOnlyList<string> FindArchiveFiles(string rootDirectory)
    {
        var allFiles = Directory.GetFiles(rootDirectory, "*", SearchOption.AllDirectories);
        var archives = new List<string>();

        foreach (var file in allFiles)
        {
            var extension = Path.GetExtension(file);
            if (GameContentConstants.ArchiveExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
            {
                archives.Add(file);
            }
            else if (string.IsNullOrEmpty(extension) && ZipValidation.IsValidZipFile(file))
            {
                archives.Add(file);
            }
        }

        return archives;
    }

    private static void EnsureValidArchivePayload(string archivePath)
    {
        var info = new FileInfo(archivePath);
        if (!info.Exists || info.Length == 0)
        {
            throw new InvalidDataException($"Archive file is missing or empty: {archivePath}");
        }

        Span<byte> header = stackalloc byte[16];
        using (var stream = File.OpenRead(archivePath))
        {
            var read = stream.Read(header);
            if (read == 0)
            {
                throw new InvalidDataException($"Archive file is empty: {archivePath}");
            }

            header = header[..read];
        }

        if (LooksLikeHtml(header))
        {
            var preview = ReadTextPreview(archivePath, maxChars: 120);
            throw new InvalidDataException(
                $"Downloaded file is HTML, not an archive (likely a broken download URL or HTTP error page): {archivePath}. Preview: {preview}");
        }
    }

    private static bool LooksLikeHtml(ReadOnlySpan<byte> header)
    {
        if (header.Length >= 3 && header[0] == 0xEF && header[1] == 0xBB && header[2] == 0xBF)
        {
            header = header[3..];
        }

        while (header.Length > 0 && (header[0] == (byte)' ' || header[0] == (byte)'\t' || header[0] == (byte)'\r' || header[0] == (byte)'\n'))
        {
            header = header[1..];
        }

        if (header.Length < 5)
        {
            return false;
        }

        Span<char> ascii = stackalloc char[Math.Min(header.Length, 9)];
        for (var i = 0; i < ascii.Length; i++)
        {
            ascii[i] = (char)header[i];
        }

        ReadOnlySpan<char> prefix = ascii;
        return prefix.StartsWith("<!doctype", StringComparison.OrdinalIgnoreCase)
            || prefix.StartsWith("<html", StringComparison.OrdinalIgnoreCase)
            || prefix.StartsWith("<?xml", StringComparison.OrdinalIgnoreCase);
    }

    private static string ReadTextPreview(string path, int maxChars)
    {
        try
        {
            using var reader = new StreamReader(path, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
            var buffer = new char[maxChars];
            var read = reader.Read(buffer, 0, buffer.Length);
            var text = new string(buffer, 0, read).Replace('\r', ' ').Replace('\n', ' ').Trim();
            return text.Length <= maxChars ? text : text[..maxChars];
        }
        catch
        {
            return "(unavailable)";
        }
    }

    private static void ExtractSingleArchive(
        string archivePath,
        string extractPath,
        CancellationToken cancellationToken)
    {
        using var archive = ArchiveFactory.OpenArchive(archivePath);
        var entryCount = 0;
        long totalUncompressedSize = 0;
        var extractRoot = Path.GetFullPath(extractPath);

        foreach (var entry in archive.Entries.Where(e => !e.IsDirectory))
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (string.IsNullOrEmpty(entry.Key))
            {
                continue;
            }

            entryCount++;
            if (entryCount > CatalogConstants.MaxZipEntryCount)
            {
                throw new InvalidDataException(
                    $"Archive exceeds maximum entry count of {CatalogConstants.MaxZipEntryCount}");
            }

            totalUncompressedSize += entry.Size;
            if (totalUncompressedSize > CatalogConstants.MaxZipUncompressedSizeBytes)
            {
                throw new InvalidDataException(
                    $"Archive exceeds maximum uncompressed size of {CatalogConstants.MaxZipUncompressedSizeBytes} bytes");
            }

            if (Path.IsPathRooted(entry.Key))
            {
                throw new InvalidDataException($"Archive entry has an unsafe path: {entry.Key}");
            }

            var pathResult = ContentPathPolicy.ResolveContainedFile(extractRoot, entry.Key);
            if (!pathResult.Success)
            {
                throw new InvalidDataException($"Archive entry has an unsafe path: {entry.Key}");
            }

            var destinationPath = pathResult.Data!;

            var destinationDir = Path.GetDirectoryName(destinationPath);
            if (!string.IsNullOrEmpty(destinationDir))
            {
                Directory.CreateDirectory(destinationDir);
            }

            entry.WriteToFile(destinationPath, new ExtractionOptions
            {
                ExtractFullPath = false,
                Overwrite = true,
            });
        }
    }

    private static void PurgeSystemJunk(string directory)
    {
        try
        {
            if (!Directory.Exists(directory))
            {
                return;
            }

            foreach (var subDir in Directory.GetDirectories(directory, "*", SearchOption.AllDirectories))
            {
                if (!Directory.Exists(subDir))
                {
                    continue;
                }

                var dirName = Path.GetFileName(subDir);
                if (GameContentConstants.SystemJunkNames.Contains(dirName, StringComparer.OrdinalIgnoreCase))
                {
                    Directory.Delete(subDir, recursive: true);
                }
            }

            foreach (var file in Directory.GetFiles(directory, "*", SearchOption.AllDirectories))
            {
                if (!File.Exists(file))
                {
                    continue;
                }

                var fileName = Path.GetFileName(file);
                if (GameContentConstants.SystemJunkNames.Contains(fileName, StringComparer.OrdinalIgnoreCase))
                {
                    File.Delete(file);
                }
            }
        }
        catch
        {
            // Ignore system junk removal failures
        }
    }

    private static bool ContainsRecognizedGameContent(string directory)
    {
        var subDirs = Directory.GetDirectories(directory, "*", SearchOption.TopDirectoryOnly)
            .Select(Path.GetFileName)
            .Where(name => !string.IsNullOrEmpty(name));

        if (subDirs.Any(name => GameContentConstants.RecognizedGameDirectories.Contains(name!, StringComparer.OrdinalIgnoreCase)))
        {
            return true;
        }

        var files = Directory.GetFiles(directory, "*", SearchOption.TopDirectoryOnly)
            .Select(Path.GetExtension)
            .Where(ext => !string.IsNullOrEmpty(ext));

        return files.Any(ext => GameContentConstants.RecognizedGameFileExtensions.Contains(ext!, StringComparer.OrdinalIgnoreCase));
    }

    private static bool DirectoryContainsMapFilesDirectly(string directory)
    {
        return Directory.GetFiles(directory, "*.map", SearchOption.TopDirectoryOnly).Length > 0;
    }

    private static void PromoteDirectoryContents(string sourceDirectory, string targetDirectory)
    {
        // Use a sibling staging directory on the same filesystem/volume for fast, safe move without nesting collisions
        var tempStaging = targetDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + "_staging_" + Guid.NewGuid().ToString("N");

        try
        {
            Directory.Move(sourceDirectory, tempStaging);

            foreach (var subFile in Directory.GetFiles(tempStaging, "*", SearchOption.AllDirectories))
            {
                var relativePath = Path.GetRelativePath(tempStaging, subFile);
                var destinationPath = Path.Combine(targetDirectory, relativePath);
                var destinationDir = Path.GetDirectoryName(destinationPath);
                if (!string.IsNullOrEmpty(destinationDir))
                {
                    Directory.CreateDirectory(destinationDir);
                }

                File.Move(subFile, destinationPath, overwrite: true);
            }
        }
        finally
        {
            if (Directory.Exists(tempStaging))
            {
                Directory.Delete(tempStaging, recursive: true);
            }
        }
    }

    private static void CleanupEmptyDirectories(string rootDirectory)
    {
        try
        {
            foreach (var subDir in Directory.GetDirectories(rootDirectory, "*", SearchOption.AllDirectories).OrderByDescending(d => d.Length))
            {
                if (Directory.Exists(subDir) && !Directory.EnumerateFileSystemEntries(subDir).Any())
                {
                    Directory.Delete(subDir);
                }
            }
        }
        catch
        {
            // Ignore directory cleanup exceptions
        }
    }

    private void StripSingleWrapperDirectories(
        string extractedDirectory,
        ContentType contentType,
        CancellationToken cancellationToken)
    {
        var depth = 0;
        while (depth < GameContentConstants.MaxWrapperNormalizationDepth)
        {
            cancellationToken.ThrowIfCancellationRequested();
            depth++;

            var rootFiles = Directory.GetFiles(extractedDirectory, "*", SearchOption.TopDirectoryOnly);
            var rootDirs = Directory.GetDirectories(extractedDirectory, "*", SearchOption.TopDirectoryOnly);

            if (rootFiles.Length != 0 || rootDirs.Length != 1)
            {
                break;
            }

            var singleDir = rootDirs[0];
            var dirName = Path.GetFileName(singleDir);

            // For map content, if the single directory contains .map files directly, preserve this directory
            if (contentType is ContentType.Map or ContentType.MapPack && DirectoryContainsMapFilesDirectly(singleDir))
            {
                logger.LogInformation("Preserving map folder structure for: {MapDir}", singleDir);
                break;
            }

            // If the single directory is a canonical game directory (e.g. Data, Art, Window, Maps, Audio),
            // it is already at the game root level (e.g. /Data/INI/...) and should NOT be flattened.
            if (GameContentConstants.IsRecognizedGameDirectory(dirName))
            {
                logger.LogInformation("Preserving canonical game root directory: {SingleDir}", singleDir);
                break;
            }

            logger.LogInformation("Flattening single wrapper directory: {SingleDir} into {Root}", singleDir, extractedDirectory);
            PromoteDirectoryContents(singleDir, extractedDirectory);
        }
    }

    private void RouteGameSpecificSubdirectories(
        string extractedDirectory,
        GameType targetGame,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var rootDirs = Directory.GetDirectories(extractedDirectory, "*", SearchOption.TopDirectoryOnly);
        if (rootDirs.Length == 0)
        {
            return;
        }

        var matchingAliases = targetGame switch
        {
            GameType.ZeroHour => GameContentConstants.ZeroHourSubfolderAliases,
            GameType.Generals => GameContentConstants.GeneralsSubfolderAliases,
            _ => null,
        };

        if (matchingAliases == null)
        {
            return;
        }

        foreach (var subDir in rootDirs)
        {
            var dirName = Path.GetFileName(subDir);
            if (matchingAliases.Contains(dirName, StringComparer.OrdinalIgnoreCase))
            {
                logger.LogInformation(
                    "Detected matching game-specific subdirectory '{DirName}' for game {Game}. Promoting contents to root.",
                    dirName,
                    targetGame);

                PromoteDirectoryContents(subDir, extractedDirectory);
                break;
            }
        }
    }

    private void ReconcileContentRootWithDocumentation(
        string extractedDirectory,
        ContentType contentType,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (contentType is ContentType.Map or ContentType.MapPack)
        {
            return;
        }

        var rootFiles = Directory.GetFiles(extractedDirectory, "*", SearchOption.TopDirectoryOnly);
        var rootDirs = Directory.GetDirectories(extractedDirectory, "*", SearchOption.TopDirectoryOnly);

        if (rootDirs.Length != 1)
        {
            return;
        }

        var singleDir = rootDirs[0];
        var dirName = Path.GetFileName(singleDir);

        // If the single directory is already a canonical game directory (e.g. Data), it should remain as is
        if (GameContentConstants.IsRecognizedGameDirectory(dirName))
        {
            return;
        }

        // Check if all files at the root level are loose documentation/metadata files
        var allRootFilesAreDocs = rootFiles.All(file =>
        {
            var ext = Path.GetExtension(file);
            return GameContentConstants.DocumentationExtensions.Contains(ext, StringComparer.OrdinalIgnoreCase);
        });

        if (!allRootFilesAreDocs)
        {
            return;
        }

        // Check if the single directory contains recognizable game root folders or files
        if (ContainsRecognizedGameContent(singleDir))
        {
            logger.LogInformation(
                "Promoting game content root from wrapper '{SingleDir}' to payload root alongside documentation",
                singleDir);

            PromoteDirectoryContents(singleDir, extractedDirectory);
        }
    }
}
