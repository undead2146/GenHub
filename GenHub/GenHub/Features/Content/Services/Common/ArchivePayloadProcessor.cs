using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
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
    private static readonly byte[] SevenZipSignature = [0x37, 0x7A, 0xBC, 0xAF, 0x27, 0x1C];
    private static readonly byte[] RarSignature = [0x52, 0x61, 0x72, 0x21, 0x1A, 0x07];
    private static readonly byte[] SmartInstallMakerSignature = [0x77, 0x77, 0x67, 0x54, 0x29, 0x48, 0x35, 0x14];

    /// <inheritdoc />
    public Task ExtractArchivesSafelyAsync(
        string extractedDirectory,
        ContentType? contentType = null,
        CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(extractedDirectory))
        {
            return Task.CompletedTask;
        }

        return Task.Run(
            () =>
            {
                var depth = 0;
                while (depth < MaxNestedExtractionDepth)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    depth++;

                    var archiveFiles = FindArchiveFiles(extractedDirectory, contentType);
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
            },
            cancellationToken);
    }

    /// <inheritdoc />
    public Task NormalizeDirectoryStructureAsync(
        string extractedDirectory,
        ContentType contentType,
        GameType targetGame,
        CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(extractedDirectory))
        {
            return Task.CompletedTask;
        }

        return Task.Run(
            () =>
            {
                cancellationToken.ThrowIfCancellationRequested();

                // 1. Purge system junk files and folders
                PurgeSystemJunk(extractedDirectory);

                // 2. Iteratively strip single wrapper directories
                StripSingleWrapperDirectories(extractedDirectory, contentType, cancellationToken);

                // 3. Handle game-specific subdirectories (e.g. ZH, Zero Hour, Generals, CCG)
                RouteGameSpecificSubdirectories(extractedDirectory, targetGame, cancellationToken);

                // 4. Heuristic root content detection (single mod directory alongside loose documentation files)
                ReconcileContentRootWithDocumentation(extractedDirectory, contentType, cancellationToken);

                // 5. Normalize inactive .gib mod archive files to .big
                NormalizeGibExtensions(extractedDirectory, contentType);

                // 6. Cleanup empty directories
                CleanupEmptyDirectories(extractedDirectory);
            },
            cancellationToken);
    }

    /// <inheritdoc />
    public async Task ProcessPayloadAsync(
        string extractedDirectory,
        ContentType contentType,
        GameType targetGame,
        CancellationToken cancellationToken = default)
    {
        await ExtractArchivesSafelyAsync(extractedDirectory, contentType, cancellationToken);
        await NormalizeDirectoryStructureAsync(extractedDirectory, contentType, targetGame, cancellationToken);
    }

    private static bool ShouldAttemptExecutableExtraction(ContentType? contentType)
    {
        if (!contentType.HasValue)
        {
            return false;
        }

        return contentType.Value switch
        {
            ContentType.ModdingTool => false,
            ContentType.Executable => false,
            ContentType.GameClient => false,
            ContentType.GameInstallation => false,
            _ => true,
        };
    }

    private static bool IsArchiveFile(string filePath, ContentType? contentType = null)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
        {
            return false;
        }

        try
        {
            var info = new FileInfo(filePath);
            if (info.Length == 0)
            {
                return false;
            }

            var extension = Path.GetExtension(filePath);

            if (extension.Equals(".zip", StringComparison.OrdinalIgnoreCase) ||
                extension.Equals(".7z", StringComparison.OrdinalIgnoreCase) ||
                extension.Equals(".rar", StringComparison.OrdinalIgnoreCase) ||
                extension.Equals(".tar", StringComparison.OrdinalIgnoreCase) ||
                extension.Equals(".gz", StringComparison.OrdinalIgnoreCase) ||
                extension.Equals(".tgz", StringComparison.OrdinalIgnoreCase) ||
                extension.Equals(".bz2", StringComparison.OrdinalIgnoreCase) ||
                extension.Equals(".xz", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (extension.Equals(".dat", StringComparison.OrdinalIgnoreCase))
            {
                return ArchiveFactory.IsArchive(filePath, out _) || ZipValidation.IsValidZipFile(filePath);
            }

            if (extension.Equals(".exe", StringComparison.OrdinalIgnoreCase))
            {
                if (!ShouldAttemptExecutableExtraction(contentType))
                {
                    return false;
                }

                return IsSelfExtractingArchive(filePath);
            }

            if (string.IsNullOrEmpty(extension))
            {
                return ArchiveFactory.IsArchive(filePath, out _) || ZipValidation.IsValidZipFile(filePath);
            }

            return false;
        }
        catch
        {
            return false;
        }
    }

    private static bool IsSelfExtractingArchive(string filePath)
    {
        try
        {
            using var zipArchive = ZipFile.OpenRead(filePath);
            if (zipArchive.Entries.Count > 0)
            {
                return true;
            }
        }
        catch
        {
            // Not a ZIP SFX
        }

        try
        {
            if (ArchiveFactory.IsArchive(filePath, out _) || ZipValidation.IsValidZipFile(filePath))
            {
                return true;
            }
        }
        catch
        {
            // Ignore
        }

        try
        {
            using var stream = File.OpenRead(filePath);
            if (FindSignatureOffset(stream, SevenZipSignature) >= 0)
            {
                return true;
            }

            stream.Position = 0;
            if (FindSignatureOffset(stream, RarSignature) >= 0)
            {
                return true;
            }

            stream.Position = 0;
            if (FindSignatureOffset(stream, SmartInstallMakerSignature) >= 0)
            {
                return true;
            }
        }
        catch
        {
            // Ignore
        }

        return false;
    }

    private static long FindSignatureOffset(Stream stream, byte[] signature)
    {
        var buffer = new byte[8192];
        long offset = 0;
        int read = 0;
        int matchIndex = 0;

        while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
        {
            for (int i = 0; i < read; i++)
            {
                if (buffer[i] == signature[matchIndex])
                {
                    matchIndex++;
                    if (matchIndex == signature.Length)
                    {
                        return offset + i - signature.Length + 1;
                    }
                }
                else
                {
                    if (matchIndex > 0)
                    {
                        i -= matchIndex;
                        matchIndex = 0;
                    }
                }
            }

            offset += read;
        }

        return -1;
    }

    private static IReadOnlyList<string> FindArchiveFiles(string rootDirectory, ContentType? contentType = null)
    {
        var allFiles = Directory.GetFiles(rootDirectory, "*", SearchOption.AllDirectories);
        var archives = new List<string>();

        foreach (var file in allFiles)
        {
            if (IsArchiveFile(file, contentType))
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
        var isExe = Path.GetExtension(archivePath).Equals(".exe", StringComparison.OrdinalIgnoreCase);
        if (isExe)
        {
            if (TryExtractZipArchive(archivePath, extractPath, cancellationToken))
            {
                return;
            }

            if (TryExtractSubStreamArchive(archivePath, extractPath, cancellationToken))
            {
                return;
            }

            if (TryExtractSmartInstallMakerArchive(archivePath, extractPath, cancellationToken))
            {
                return;
            }
        }

        try
        {
            using var archive = ArchiveFactory.OpenArchive(archivePath);
            ExtractSharpCompressArchive(archive, extractPath, cancellationToken);
        }
        catch when (isExe)
        {
            throw new InvalidDataException($"Executable is not a supported self-extracting archive: {archivePath}");
        }
    }

    private static void ExtractSharpCompressArchive(
        IArchive archive,
        string extractPath,
        CancellationToken cancellationToken)
    {
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

    private static bool TryExtractZipArchive(
        string archivePath,
        string extractPath,
        CancellationToken cancellationToken)
    {
        if (!ZipValidation.IsValidZipFile(archivePath))
        {
            return false;
        }

        try
        {
            using var zip = ZipFile.OpenRead(archivePath);
            if (zip.Entries.Count == 0)
            {
                return false;
            }

            var entryCount = 0;
            long totalUncompressedSize = 0;
            var extractRoot = Path.GetFullPath(extractPath);

            foreach (var entry in zip.Entries)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (string.IsNullOrEmpty(entry.FullName) || entry.FullName.EndsWith('/') || entry.FullName.EndsWith('\\'))
                {
                    continue;
                }

                entryCount++;
                if (entryCount > CatalogConstants.MaxZipEntryCount)
                {
                    throw new InvalidDataException(
                        $"Archive exceeds maximum entry count of {CatalogConstants.MaxZipEntryCount}");
                }

                totalUncompressedSize += entry.Length;
                if (totalUncompressedSize > CatalogConstants.MaxZipUncompressedSizeBytes)
                {
                    throw new InvalidDataException(
                        $"Archive exceeds maximum uncompressed size of {CatalogConstants.MaxZipUncompressedSizeBytes} bytes");
                }

                if (Path.IsPathRooted(entry.FullName))
                {
                    throw new InvalidDataException($"Archive entry has an unsafe path: {entry.FullName}");
                }

                var pathResult = ContentPathPolicy.ResolveContainedFile(extractRoot, entry.FullName);
                if (!pathResult.Success)
                {
                    throw new InvalidDataException($"Archive entry has an unsafe path: {entry.FullName}");
                }

                var destinationPath = pathResult.Data!;
                var destinationDir = Path.GetDirectoryName(destinationPath);
                if (!string.IsNullOrEmpty(destinationDir))
                {
                    Directory.CreateDirectory(destinationDir);
                }

                entry.ExtractToFile(destinationPath, overwrite: true);
            }

            return true;
        }
        catch (InvalidDataException)
        {
            throw;
        }
        catch
        {
            return false;
        }
    }

    private static bool TryExtractSubStreamArchive(
        string archivePath,
        string extractPath,
        CancellationToken cancellationToken)
    {
        try
        {
            using var stream = File.OpenRead(archivePath);
            var offset = FindSignatureOffset(stream, SevenZipSignature);
            if (offset < 0)
            {
                stream.Position = 0;
                offset = FindSignatureOffset(stream, RarSignature);
            }

            if (offset < 0)
            {
                return false;
            }

            stream.Position = offset;
            using var subStream = new SubStream(stream, offset, stream.Length - offset);
            using var archive = ArchiveFactory.OpenArchive(subStream);
            ExtractSharpCompressArchive(archive, extractPath, cancellationToken);
            return true;
        }
        catch (InvalidDataException)
        {
            throw;
        }
        catch
        {
            return false;
        }
    }

    private static bool TryExtractSmartInstallMakerArchive(
        string archivePath,
        string extractPath,
        CancellationToken cancellationToken)
    {
        try
        {
            using var stream = File.OpenRead(archivePath);
            var sigOffset = FindSignatureOffset(stream, SmartInstallMakerSignature);
            if (sigOffset < 0)
            {
                return false;
            }

            stream.Position = sigOffset + SmartInstallMakerSignature.Length;
            var (fileTableData, payloadOffset) = ReadSmartInstallMakerMetadata(stream);
            if (fileTableData == null || fileTableData.Length == 0 || payloadOffset < 0)
            {
                return false;
            }

            var records = ParseSmartInstallMakerFileTable(fileTableData);
            if (records.Count == 0)
            {
                return false;
            }

            var extractRoot = Path.GetFullPath(extractPath);
            var extractedCount = ExtractSmartInstallMakerPayload(stream, payloadOffset, records, extractRoot, cancellationToken);
            return extractedCount > 0;
        }
        catch (InvalidDataException)
        {
            throw;
        }
        catch
        {
            return false;
        }
    }

    private static (byte[]? TableData, long PayloadOffset) ReadSmartInstallMakerMetadata(Stream stream)
    {
        using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);

        var blocks = new List<(long Pos, int CompSize, byte CompType, long DataStart)>();
        var blockIdx = 0;
        while (stream.Position < stream.Length - 13)
        {
            var pos = stream.Position;
            _ = blockIdx == 0 ? reader.ReadInt16() : reader.ReadInt32();
            var compSize = reader.ReadInt32();
            _ = reader.ReadInt32();
            var compType = reader.ReadByte();
            var dataLength = compSize - 5;
            var dataStart = stream.Position;

            blocks.Add((pos, compSize, compType, dataStart));
            blockIdx++;

            if (dataLength > 0 && stream.Position + dataLength <= stream.Length)
            {
                stream.Position += dataLength;
            }
            else
            {
                break;
            }
        }

        if (blocks.Count < 2)
        {
            return (null, -1);
        }

        var payloadOffset = blocks[^1].DataStart;
        var tableBlock = blocks[^2];
        if (tableBlock.CompType == 1)
        {
            stream.Position = tableBlock.DataStart + 2; // skip zlib 78-DA header
            using var def = new DeflateStream(stream, CompressionMode.Decompress, leaveOpen: true);
            using var ms = new MemoryStream();
            var buf = new byte[8192];
            var r = 0;
            while ((r = def.Read(buf, 0, buf.Length)) > 0)
            {
                ms.Write(buf, 0, r);
            }

            return (ms.ToArray(), payloadOffset);
        }

        return (null, payloadOffset);
    }

    private static int ExtractSmartInstallMakerPayload(
        Stream stream,
        long payloadOffset,
        List<(string Name, uint UncompressedSize, uint StreamOffset, uint CompressedSize)> records,
        string extractRoot,
        CancellationToken cancellationToken)
    {
        var extractedCount = 0;
        var copyBuffer = new byte[65536];

        foreach (var rec in records)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var pathResult = ContentPathPolicy.ResolveContainedFile(extractRoot, rec.Name);
            if (!pathResult.Success)
            {
                throw new InvalidDataException($"Smart Install Maker entry has an unsafe path: {rec.Name}");
            }

            var destinationPath = pathResult.Data!;
            var destinationDir = Path.GetDirectoryName(destinationPath);
            if (!string.IsNullOrEmpty(destinationDir))
            {
                Directory.CreateDirectory(destinationDir);
            }

            var filePos = payloadOffset + rec.StreamOffset;
            if (filePos < 0 || filePos >= stream.Length)
            {
                continue;
            }

            stream.Position = filePos;
            var header = new byte[2];
            var headerRead = stream.Read(header, 0, 2);
            stream.Position = filePos;

            long written = 0;

            if (headerRead >= 2 && header[0] == 'B' && header[1] == 'Z')
            {
                using var bz2 = SharpCompress.Compressors.BZip2.BZip2Stream.Create(
                    stream,
                    SharpCompress.Compressors.CompressionMode.Decompress,
                    decompressConcatenated: false,
                    leaveOpen: true);

                using var outStream = File.Create(destinationPath);
                while (written < rec.UncompressedSize)
                {
                    var toRead = (int)Math.Min(copyBuffer.Length, rec.UncompressedSize - written);
                    var readBytes = bz2.Read(copyBuffer, 0, toRead);
                    if (readBytes <= 0)
                    {
                        break;
                    }

                    outStream.Write(copyBuffer, 0, readBytes);
                    written += readBytes;
                }
            }
            else if (headerRead >= 2 && header[0] == 0x78 && (header[1] == 0xDA || header[1] == 0x9C || header[1] == 0x01 || header[1] == 0x5E))
            {
                stream.Position = filePos + 2; // skip zlib header
                using var def = new DeflateStream(stream, CompressionMode.Decompress, leaveOpen: true);
                using var outStream = File.Create(destinationPath);
                while (written < rec.UncompressedSize)
                {
                    var toRead = (int)Math.Min(copyBuffer.Length, rec.UncompressedSize - written);
                    var readBytes = def.Read(copyBuffer, 0, toRead);
                    if (readBytes <= 0)
                    {
                        break;
                    }

                    outStream.Write(copyBuffer, 0, readBytes);
                    written += readBytes;
                }
            }
            else
            {
                using var outStream = File.Create(destinationPath);
                while (written < rec.UncompressedSize)
                {
                    var toRead = (int)Math.Min(copyBuffer.Length, rec.UncompressedSize - written);
                    var readBytes = stream.Read(copyBuffer, 0, toRead);
                    if (readBytes <= 0)
                    {
                        break;
                    }

                    outStream.Write(copyBuffer, 0, readBytes);
                    written += readBytes;
                }
            }

            if (written == rec.UncompressedSize)
            {
                extractedCount++;
            }
        }

        return extractedCount;
    }

    private static List<(string Name, uint UncompressedSize, uint StreamOffset, uint CompressedSize)> ParseSmartInstallMakerFileTable(byte[] tableData)
    {
        var records = new List<(string Name, uint UncompressedSize, uint StreamOffset, uint CompressedSize)>();
        for (var i = 0; i < tableData.Length - 4; i++)
        {
            if (tableData[i] == '.' && i >= 40)
            {
                var start = i;
                while (start > 0 && tableData[start - 1] != 0 && tableData[start - 1] >= 32 && tableData[start - 1] <= 126)
                {
                    start--;
                }

                var end = i;
                while (end < tableData.Length && tableData[end] != 0 && tableData[end] >= 32 && tableData[end] <= 126)
                {
                    end++;
                }

                var name = Encoding.Latin1.GetString(tableData, start, end - start);
                if (name.Contains('.') &&
                    !name.StartsWith(' ') &&
                    name.Length > 3 &&
                    !name.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase) &&
                    !name.EndsWith("Intrnl.exe", StringComparison.OrdinalIgnoreCase) &&
                    start >= 40)
                {
                    var ext = Path.GetExtension(name);
                    if (!string.IsNullOrEmpty(ext) && ext.Length <= 5)
                    {
                        var uncompSize = BitConverter.ToUInt32(tableData, start - 40);
                        var streamOffset = BitConverter.ToUInt32(tableData, start - 36);
                        var compSize = BitConverter.ToUInt32(tableData, start - 32);

                        if (uncompSize > 0 && compSize > 0 && (ulong)uncompSize < (ulong)CatalogConstants.MaxZipUncompressedSizeBytes)
                        {
                            if (!records.Exists(r => r.Name == name && r.StreamOffset == streamOffset))
                            {
                                records.Add((name, uncompSize, streamOffset, compSize));
                            }
                        }
                    }

                    i = end;
                }
            }
        }

        return records;
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

    private void NormalizeGibExtensions(string extractedDirectory, ContentType contentType)
    {
        if (contentType is ContentType.ModdingTool or ContentType.Executable or ContentType.GameClient or ContentType.GameInstallation)
        {
            return;
        }

        try
        {
            foreach (var gibFile in Directory.GetFiles(extractedDirectory, "*.gib", SearchOption.AllDirectories))
            {
                var bigFile = Path.ChangeExtension(gibFile, ".big");
                if (File.Exists(bigFile))
                {
                    File.Delete(gibFile);
                    logger.LogInformation("Removed redundant inactive file '{GibFile}' as '{BigFile}' already exists", gibFile, bigFile);
                }
                else
                {
                    File.Move(gibFile, bigFile);
                    logger.LogInformation("Normalized inactive mod archive '{GibFile}' to '{BigFile}'", gibFile, bigFile);
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to normalize .gib file extensions in: {Directory}", extractedDirectory);
        }
    }

    private sealed class SubStream(Stream baseStream, long offset, long length) : Stream
    {
        private long _position;

        public override bool CanRead => baseStream.CanRead;

        public override bool CanSeek => baseStream.CanSeek;

        public override bool CanWrite => false;

        public override long Length => length;

        public override long Position
        {
            get => _position;
            set
            {
                ArgumentOutOfRangeException.ThrowIfNegative(value);
                ArgumentOutOfRangeException.ThrowIfGreaterThan(value, length);
                _position = value;
            }
        }

        public override void Flush() => baseStream.Flush();

        public override int Read(byte[] buffer, int offsetInBuffer, int count)
        {
            if (_position >= length)
            {
                return 0;
            }

            var toRead = (int)Math.Min(count, length - _position);
            baseStream.Position = offset + _position;
            var read = baseStream.Read(buffer, offsetInBuffer, toRead);
            _position += read;
            return read;
        }

        public override long Seek(long offsetFromOrigin, SeekOrigin origin)
        {
            var target = origin switch
            {
                SeekOrigin.Begin => offsetFromOrigin,
                SeekOrigin.Current => _position + offsetFromOrigin,
                SeekOrigin.End => length + offsetFromOrigin,
                _ => throw new ArgumentOutOfRangeException(nameof(origin)),
            };
            Position = target;
            return _position;
        }

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offsetInBuffer, int count) => throw new NotSupportedException();
    }
}
