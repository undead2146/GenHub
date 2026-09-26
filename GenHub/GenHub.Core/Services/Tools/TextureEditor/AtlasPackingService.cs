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
        int sheetWidth = NextPowerOfTwo(ordered.Max(source => source.Texture.Width + (padding * 2)));
        sheetWidth = Math.Min(sheetWidth, maxDimension);

        var placements = new List<AtlasPlacement>(ordered.Count);
        int sheetHeight = PackShelves(ordered, placements, sheetWidth, padding, maxDimension);
        if (sheetHeight < 0)
        {
            return OperationResult<AtlasPackResult>.CreateFailure("Sprites do not fit inside the maximum atlas size.", Stopwatch.GetElapsedTime(started));
        }

        sheetHeight = NextPowerOfTwo(Math.Max(sheetHeight, 1));
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

    private static int NextPowerOfTwo(int value)
    {
        int size = 1;
        while (size < value && size < TextureEditorConstants.MaxTextureDimension)
        {
            size *= 2;
        }

        return size;
    }

    private static int PackShelves(List<AtlasSourceImage> ordered, List<AtlasPlacement> placements, int sheetWidth, int padding, int maxDimension)
    {
        int x = padding;
        int y = padding;
        int shelfHeight = 0;

        foreach (var source in ordered)
        {
            int width = source.Texture.Width;
            int height = source.Texture.Height;

            if (x + width + padding > sheetWidth)
            {
                x = padding;
                y += shelfHeight + padding;
                shelfHeight = 0;
            }

            if (y + height + padding > maxDimension)
            {
                return -1;
            }

            placements.Add(new AtlasPlacement(source.Name, x, y, width, height));
            x += width + padding;
            shelfHeight = Math.Max(shelfHeight, height);
        }

        return y + shelfHeight + padding;
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
