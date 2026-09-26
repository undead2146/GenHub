using GenHub.Core.Constants;
using GenHub.Core.Interfaces.Tools.TextureEditor;
using GenHub.Core.Models.Results;
using GenHub.Core.Models.Tools.TextureEditor;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace GenHub.Core.Services.Tools.TextureEditor;

/// <summary>
/// Indexes MappedImage entries scanned from INI files with SAGE load-order semantics.
/// </summary>
public sealed class MappedImageRegistry(ISageMappedImageParser parser, ILogger<MappedImageRegistry> logger) : IMappedImageRegistry
{
    private readonly Dictionary<string, MappedImageDefinition> _entries = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _syncLock = new();

    /// <inheritdoc />
    public IReadOnlyList<MappedImageDefinition> All
    {
        get
        {
            lock (_syncLock)
            {
                return _entries.Values.OrderBy(image => image.Name, StringComparer.OrdinalIgnoreCase).ToList();
            }
        }
    }

    /// <inheritdoc />
    public int Count
    {
        get
        {
            lock (_syncLock)
            {
                return _entries.Count;
            }
        }
    }

    /// <inheritdoc />
    public async Task<OperationResult<MappedImageScanResult>> ScanDirectoryAsync(string directory, CancellationToken cancellationToken = default)
    {
        var started = Stopwatch.GetTimestamp();
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);

        if (!Directory.Exists(directory))
        {
            return OperationResult<MappedImageScanResult>.CreateFailure($"Directory not found: {directory}", Stopwatch.GetElapsedTime(started));
        }

        string[] files;
        try
        {
            files = Directory.GetFiles(directory, TextureEditorConstants.MappedImagesFilePattern, SearchOption.AllDirectories);
        }
        catch (IOException ex)
        {
            logger.LogWarning(ex, "Failed to enumerate MappedImages directory: {Directory}", directory);
            return OperationResult<MappedImageScanResult>.CreateFailure($"Failed to enumerate directory: {directory}", Stopwatch.GetElapsedTime(started));
        }
        catch (UnauthorizedAccessException ex)
        {
            logger.LogWarning(ex, "Access denied enumerating MappedImages directory: {Directory}", directory);
            return OperationResult<MappedImageScanResult>.CreateFailure($"Access denied enumerating directory: {directory}", Stopwatch.GetElapsedTime(started));
        }

        Array.Sort(files, StringComparer.OrdinalIgnoreCase);
        var errors = new List<string>();
        int images = 0;

        foreach (var file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var parsed = await parser.ParseFileAsync(file, cancellationToken).ConfigureAwait(false);
            if (parsed.Data is not null)
            {
                lock (_syncLock)
                {
                    foreach (var image in parsed.Data)
                    {
                        _entries[image.Name] = image;
                        images++;
                    }
                }
            }

            if (parsed.Failed)
            {
                errors.AddRange(parsed.Errors);
            }
        }

        var elapsed = Stopwatch.GetElapsedTime(started);
        logger.LogInformation("Scanned {Files} MappedImages INI files with {Images} entries from {Directory}", files.Length, images, directory);

        var scan = new MappedImageScanResult(files.Length, images);
        return errors.Count > 0
            ? OperationResult<MappedImageScanResult>.CreateFailure(errors, scan, elapsed)
            : OperationResult<MappedImageScanResult>.CreateSuccess(scan, elapsed);
    }

    /// <inheritdoc />
    public MappedImageDefinition? GetByName(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        lock (_syncLock)
        {
            return _entries.TryGetValue(name, out var image) ? image : null;
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<MappedImageDefinition> GetByTexture(string textureFileName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(textureFileName);
        lock (_syncLock)
        {
            return _entries.Values
                .Where(image => image.TextureFileName.Equals(textureFileName, StringComparison.OrdinalIgnoreCase))
                .OrderBy(image => image.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
    }

    /// <inheritdoc />
    public void Clear()
    {
        lock (_syncLock)
        {
            _entries.Clear();
        }
    }
}
