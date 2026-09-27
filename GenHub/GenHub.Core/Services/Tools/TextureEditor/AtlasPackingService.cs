using GenHub.Core.Constants;
using GenHub.Core.Interfaces.Tools.TextureEditor;
using GenHub.Core.Models.Results;
using GenHub.Core.Models.Tools.TextureEditor;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace GenHub.Core.Services.Tools.TextureEditor;

/// <summary>
/// Packs loose sprites into power-of-two texture atlases using a shelf algorithm.
/// </summary>
public sealed class AtlasPackingService(
    ISageTextureCodec codec,
    ISageMappedImageParser parser,
    ILogger<AtlasPackingService> logger) : IAtlasPackingService
{
    /// <inheritdoc />
    public OperationResult<AtlasPackResult> Pack(IReadOnlyList<AtlasSourceImage> sources, int padding = TextureEditorConstants.DefaultPadding, int maxDimension = TextureEditorConstants.MaxTextureDimension)
    {
        var started = Stopwatch.GetTimestamp();
        ArgumentNullException.ThrowIfNull(sources);

        if (sources.Count == 0)
        {
            return OperationResult<AtlasPackResult>.CreateFailure("No source images to pack.", Stopwatch.GetElapsedTime(started));
        }

        if (padding < 0)
        {
            return OperationResult<AtlasPackResult>.CreateFailure("Padding cannot be negative.", Stopwatch.GetElapsedTime(started));
        }

        var duplicate = sources
            .GroupBy(source => source.Name, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicate is not null)
        {
            return OperationResult<AtlasPackResult>.CreateFailure(
                $"Duplicate sprite name '{duplicate.Key}'.",
                Stopwatch.GetElapsedTime(started));
        }

        var oversized = sources.FirstOrDefault(source =>
            source.Texture.Width <= 0 || source.Texture.Height <= 0 ||
            source.Texture.Width > maxDimension || source.Texture.Height > maxDimension);
        if (oversized is not null)
        {
            return OperationResult<AtlasPackResult>.CreateFailure(
                $"Source '{oversized.Name}' ({oversized.Texture.Width}x{oversized.Texture.Height}) exceeds the {maxDimension}px limit.",
                Stopwatch.GetElapsedTime(started));
        }

        var ordered = sources.OrderByDescending(source => source.Texture.Height).ThenByDescending(source => source.Texture.Width).ToList();
        int sheetWidth = Math.Min(NextPowerOfTwo(ordered.Max(source => source.Texture.Width + (padding * 2)), maxDimension), maxDimension);

        var placements = new List<AtlasPlacement>(ordered.Count);
        var (sheetHeight, unplaceable) = PackShelves(ordered, placements, sheetWidth, padding, maxDimension);
        if (unplaceable is not null)
        {
            return OperationResult<AtlasPackResult>.CreateFailure(
                $"Sprite '{unplaceable.Name}' ({unplaceable.Texture.Width}x{unplaceable.Texture.Height}) does not fit inside the {sheetWidth}px atlas width.",
                Stopwatch.GetElapsedTime(started));
        }

        if (sheetHeight < 0)
        {
            return OperationResult<AtlasPackResult>.CreateFailure("Sprites do not fit inside the maximum atlas size.", Stopwatch.GetElapsedTime(started));
        }

        sheetHeight = Math.Min(NextPowerOfTwo(Math.Max(sheetHeight, 1), maxDimension), maxDimension);
        var pack = new AtlasPackResult(sheetWidth, sheetHeight, placements);
        return OperationResult<AtlasPackResult>.CreateSuccess(pack, Stopwatch.GetElapsedTime(started));
    }

    /// <inheritdoc />
    public OperationResult<DecodedTexture> ComposeSheet(IReadOnlyList<AtlasSourceImage> sources, AtlasPackResult pack)
    {
        var started = Stopwatch.GetTimestamp();
        ArgumentNullException.ThrowIfNull(sources);
        ArgumentNullException.ThrowIfNull(pack);

        if (sources.Count != pack.Placements.Count)
        {
            return OperationResult<DecodedTexture>.CreateFailure("Source count does not match placement count.", Stopwatch.GetElapsedTime(started));
        }

        var sheet = new byte[pack.SheetWidth * pack.SheetHeight * 4];
        for (int i = 0; i < sources.Count; i++)
        {
            var source = sources[i];
            var placement = pack.Placements[i];
            if (!string.Equals(source.Name, placement.Name, StringComparison.Ordinal))
            {
                return OperationResult<DecodedTexture>.CreateFailure($"Placement order mismatch at index {i}.", Stopwatch.GetElapsedTime(started));
            }

            long expectedBytes = (long)source.Texture.Width * source.Texture.Height * 4;
            if (source.Texture.PixelData.Length < expectedBytes)
            {
                return OperationResult<DecodedTexture>.CreateFailure($"Source '{source.Name}' pixel data is shorter than its declared dimensions.", Stopwatch.GetElapsedTime(started));
            }

            Blit(source.Texture, sheet, pack.SheetWidth, placement);
        }

        var texture = new DecodedTexture(pack.SheetWidth, pack.SheetHeight, sheet);
        return OperationResult<DecodedTexture>.CreateSuccess(texture, Stopwatch.GetElapsedTime(started));
    }

    /// <inheritdoc />
    public async Task<OperationResult<TextureAtlasBuildResult>> BuildAtlasAsync(
        TextureAtlasBuildRequest request,
        ITextureImageLoader loader,
        CancellationToken cancellationToken = default)
    {
        var started = Stopwatch.GetTimestamp();
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(loader);

        if (string.IsNullOrWhiteSpace(request.SourceDirectory) || !Directory.Exists(request.SourceDirectory))
        {
            return OperationResult<TextureAtlasBuildResult>.CreateFailure($"Source directory not found: {request.SourceDirectory}", Stopwatch.GetElapsedTime(started));
        }

        var files = Directory.GetFiles(request.SourceDirectory)
            .Where(IsSupportedSource)
            .OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (files.Count == 0)
        {
            return OperationResult<TextureAtlasBuildResult>.CreateFailure($"No supported source images in: {request.SourceDirectory}", Stopwatch.GetElapsedTime(started));
        }

        var sources = new List<AtlasSourceImage>(files.Count);
        foreach (var file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var loaded = await loader.LoadAsync(file, cancellationToken).ConfigureAwait(false);
            if (loaded.Failed || loaded.Data is null)
            {
                return OperationResult<TextureAtlasBuildResult>.CreateFailure(loaded, Stopwatch.GetElapsedTime(started));
            }

            sources.Add(loaded.Data);
        }

        var packed = Pack(sources, request.Padding);
        if (packed.Failed || packed.Data is null)
        {
            return OperationResult<TextureAtlasBuildResult>.CreateFailure(packed, Stopwatch.GetElapsedTime(started));
        }

        var composed = ComposeSheet(CollectOrderedSources(sources, packed.Data), packed.Data);
        if (composed.Failed || composed.Data is null)
        {
            return OperationResult<TextureAtlasBuildResult>.CreateFailure(composed, Stopwatch.GetElapsedTime(started));
        }

        var encoded = codec.EncodeTga(composed.Data);
        if (encoded.Failed || encoded.Data is null)
        {
            return OperationResult<TextureAtlasBuildResult>.CreateFailure(encoded, Stopwatch.GetElapsedTime(started));
        }

        string textureFileName = Path.GetFileName(request.TargetTexture);
        var images = packed.Data.Placements
            .Select(placement => new MappedImageDefinition(
                placement.Name,
                textureFileName,
                packed.Data.SheetWidth,
                packed.Data.SheetHeight,
                placement.X,
                placement.Y,
                placement.Right,
                placement.Bottom))
            .ToList();
        string iniContent = parser.Serialize(images, $"Generated by GenHub {TextureEditorConstants.ToolName} from {request.SourceDirectory}");

        var result = new TextureAtlasBuildResult(composed.Data, packed.Data.Placements, images, iniContent, encoded.Data);
        logger.LogInformation("Built texture atlas {Texture} ({Width}x{Height}) with {Count} sprites", textureFileName, packed.Data.SheetWidth, packed.Data.SheetHeight, images.Count);
        return OperationResult<TextureAtlasBuildResult>.CreateSuccess(result, Stopwatch.GetElapsedTime(started));
    }

    private static bool IsSupportedSource(string path)
    {
        string extension = Path.GetExtension(path);
        return extension.Equals(".png", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".tga", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".bmp", StringComparison.OrdinalIgnoreCase);
    }

    private static int NextPowerOfTwo(int value, int ceiling)
    {
        int size = 1;
        while (size < value && size < ceiling)
        {
            size *= 2;
        }

        return size;
    }

    private static (int SheetHeight, AtlasSourceImage? Unplaceable) PackShelves(List<AtlasSourceImage> ordered, List<AtlasPlacement> placements, int sheetWidth, int padding, int maxDimension)
    {
        int x = padding;
        int y = padding;
        int shelfHeight = 0;

        foreach (var source in ordered)
        {
            int width = source.Texture.Width;
            int height = source.Texture.Height;

            if ((long)x + width + padding > sheetWidth)
            {
                x = padding;
                y += shelfHeight + padding;
                shelfHeight = 0;
                if ((long)x + width + padding > sheetWidth)
                {
                    return (-1, source);
                }
            }

            if ((long)y + height + padding > maxDimension)
            {
                return (-1, null);
            }

            placements.Add(new AtlasPlacement(source.Name, x, y, width, height));
            x += width + padding;
            shelfHeight = Math.Max(shelfHeight, height);
        }

        return (y + shelfHeight + padding, null);
    }

    private static IReadOnlyList<AtlasSourceImage> CollectOrderedSources(List<AtlasSourceImage> sources, AtlasPackResult pack)
    {
        var byName = sources.ToDictionary(source => source.Name, StringComparer.Ordinal);
        return pack.Placements.Select(placement => byName[placement.Name]).ToList();
    }

    private static void Blit(DecodedTexture source, byte[] sheet, int sheetWidth, AtlasPlacement placement)
    {
        int srcStride = source.Width * 4;
        int dstStride = sheetWidth * 4;
        for (int row = 0; row < source.Height; row++)
        {
            int srcRow = row * srcStride;
            int dstRow = ((placement.Y + row) * dstStride) + (placement.X * 4);
            Array.Copy(source.PixelData, srcRow, sheet, dstRow, srcStride);
        }
    }
}
