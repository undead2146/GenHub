using GenHub.Core.Models.Tools.TextureEditor;
using GenHub.Core.Services.Tools.TextureEditor;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace GenHub.Tests.Core.Features.Tools.TextureEditor;

/// <summary>
/// Unit tests for <see cref="SageTextureCodec"/>.
/// </summary>
public sealed class SageTextureCodecTests
{
    private readonly SageTextureCodec _codec = new(NullLogger<SageTextureCodec>.Instance);

    /// <summary>
    /// Verifies that TGA and DDS extensions are supported while others are rejected.
    /// </summary>
    [Fact]
    public void SupportsExtension_KnownFormats_ReturnsExpected()
    {
        Assert.True(_codec.SupportsExtension(".tga"));
        Assert.True(_codec.SupportsExtension(".TGA"));
        Assert.True(_codec.SupportsExtension(".dds"));
        Assert.False(_codec.SupportsExtension(".png"));
        Assert.False(_codec.SupportsExtension(string.Empty));
    }

    /// <summary>
    /// Verifies that an uncompressed 32-bit TGA decodes to the expected RGBA pixels.
    /// </summary>
    [Fact]
    public void Decode_Uncompressed32BitTga_ReturnsRgbaPixels()
    {
        byte[] data = BuildTga(2, 1, 1, 32, [16, 32, 48, 200]);

        var result = _codec.Decode(data, ".tga", "test");

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal(1, result.Data.Width);
        Assert.Equal(1, result.Data.Height);
        Assert.Equal([48, 32, 16, 200], result.Data.PixelData);
        Assert.True(result.Data.HasAlpha);
    }

    /// <summary>
    /// Verifies that a 24-bit TGA gains full opacity and bottom-up rows flip to top-first order.
    /// </summary>
    [Fact]
    public void Decode_Uncompressed24BitTga_FlipsRowsAndAddsAlpha()
    {
        // Bottom row first: blue pixel, then top row: red pixel.
        byte[] data = BuildTga(2, 1, 2, 24, [255, 0, 0, 0, 0, 255]);

        var result = _codec.Decode(data, ".tga", "test");

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal(1, result.Data.Width);
        Assert.Equal(2, result.Data.Height);
        Assert.Equal([255, 0, 0, 255, 0, 0, 255, 255], result.Data.PixelData);
        Assert.False(result.Data.HasAlpha);
    }

    /// <summary>
    /// Verifies that RLE compressed TGA data decodes repeated and raw runs.
    /// </summary>
    [Fact]
    public void Decode_RleTga_ExpandsRuns()
    {
        // One run packet repeating a red pixel twice, then one raw packet with a green pixel.
        byte[] pixels = [0x81, 0, 0, 255, 0x00, 0, 255, 0];
        byte[] data = BuildTga(10, 3, 1, 24, pixels);

        var result = _codec.Decode(data, ".tga", "test");

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal([255, 0, 0, 255, 255, 0, 0, 255, 0, 255, 0, 255], result.Data.PixelData);
    }

    /// <summary>
    /// Verifies that unsupported TGA image types return failures.
    /// </summary>
    [Fact]
    public void Decode_UnsupportedTgaType_ReturnsFailure()
    {
        byte[] data = BuildTga(1, 1, 1, 24, [0, 0, 0]);

        var result = _codec.Decode(data, ".tga", "test");

        Assert.True(result.Failed);
        Assert.NotEmpty(result.Errors);
    }

    /// <summary>
    /// Verifies that truncated TGA data returns a failure.
    /// </summary>
    [Fact]
    public void Decode_TruncatedTga_ReturnsFailure()
    {
        var result = _codec.Decode([2, 0, 2], ".tga", "test");

        Assert.True(result.Failed);
    }

    /// <summary>
    /// Verifies that encoding then decoding a texture round-trips pixel data.
    /// </summary>
    [Fact]
    public void EncodeTga_DecodeRoundTrip_PreservesPixels()
    {
        var texture = new DecodedTexture(2, 2, [255, 0, 0, 255, 0, 255, 0, 128, 0, 0, 255, 64, 255, 255, 255, 0]);

        var encoded = _codec.EncodeTga(texture);
        Assert.True(encoded.Success);
        Assert.NotNull(encoded.Data);

        var decoded = _codec.Decode(encoded.Data, ".tga", "roundtrip");
        Assert.True(decoded.Success);
        Assert.NotNull(decoded.Data);
        Assert.Equal(texture.Width, decoded.Data.Width);
        Assert.Equal(texture.Height, decoded.Data.Height);
        Assert.Equal(texture.PixelData, decoded.Data.PixelData);
    }

    /// <summary>
    /// Verifies that encoding rejects pixel data whose length mismatches the dimensions.
    /// </summary>
    [Fact]
    public void EncodeTga_MismatchedPixels_ReturnsFailure()
    {
        var result = _codec.EncodeTga(new DecodedTexture(2, 2, [1, 2, 3]));

        Assert.True(result.Failed);
    }

    /// <summary>
    /// Verifies that an uncompressed 32-bit DDS decodes BGRA pixels to RGBA.
    /// </summary>
    [Fact]
    public void Decode_UncompressedDds_ReturnsRgbaPixels()
    {
        byte[] data = BuildDds(2, 1, 32, [10, 20, 30, 40, 50, 60, 70, 80]);

        var result = _codec.Decode(data, ".dds", "test");

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal(2, result.Data.Width);
        Assert.Equal(1, result.Data.Height);
        Assert.Equal([30, 20, 10, 40, 70, 60, 50, 80], result.Data.PixelData);
    }

    /// <summary>
    /// Verifies that a DXT1 DDS block decodes the palette and indices.
    /// </summary>
    [Fact]
    public void Decode_Dxt1Dds_DecodesBlocks()
    {
        // 4x4 block: red and blue endpoints, all indices select color 0.
        byte[] block = [0x00, 0xF8, 0x1F, 0x00, 0x00, 0x00, 0x00, 0x00];
        byte[] data = BuildDds(4, 4, 0, block, fourCc: 0x31545844);

        var result = _codec.Decode(data, ".dds", "test");

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal(4, result.Data.Width);
        Assert.Equal(4, result.Data.Height);
        Assert.Equal(255, result.Data.PixelData[0]);
        Assert.Equal(0, result.Data.PixelData[1]);
        Assert.Equal(0, result.Data.PixelData[2]);
        Assert.Equal(255, result.Data.PixelData[3]);
        Assert.Equal(16 * 4, result.Data.PixelData.Length);
    }

    /// <summary>
    /// Verifies that DDS data with an invalid magic returns a failure.
    /// </summary>
    [Fact]
    public void Decode_InvalidDdsMagic_ReturnsFailure()
    {
        var result = _codec.Decode(new byte[140], ".dds", "test");

        Assert.True(result.Failed);
    }

    /// <summary>
    /// Verifies that unknown extensions return failures.
    /// </summary>
    [Fact]
    public void Decode_UnknownExtension_ReturnsFailure()
    {
        var result = _codec.Decode([1, 2, 3], ".png", "test");

        Assert.True(result.Failed);
    }

    /// <summary>
    /// Verifies that decoding a missing file returns a failure instead of throwing.
    /// </summary>
    /// <returns>A task representing the asynchronous unit test.</returns>
    [Fact]
    public async Task DecodeFileAsync_MissingFile_ReturnsFailureAsync()
    {
        var result = await _codec.DecodeFileAsync(Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".tga"));

        Assert.True(result.Failed);
    }

    /// <summary>
    /// Verifies that decoding a real TGA file on disk succeeds.
    /// </summary>
    /// <returns>A task representing the asynchronous unit test.</returns>
    [Fact]
    public async Task DecodeFileAsync_ValidTgaFile_DecodesAsync()
    {
        string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".tga");
        try
        {
            await File.WriteAllBytesAsync(path, BuildTga(2, 1, 1, 32, [1, 2, 3, 4]));
            var result = await _codec.DecodeFileAsync(path);

            Assert.True(result.Success);
            Assert.NotNull(result.Data);
            Assert.Equal([3, 2, 1, 4], result.Data.PixelData);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static byte[] BuildTga(int imageType, int width, int height, int bitsPerPixel, byte[] pixels)
    {
        var header = new byte[18];
        header[2] = (byte)imageType;
        header[12] = (byte)(width & 0xFF);
        header[13] = (byte)((width >> 8) & 0xFF);
        header[14] = (byte)(height & 0xFF);
        header[15] = (byte)((height >> 8) & 0xFF);
        header[16] = (byte)bitsPerPixel;
        header[17] = 0;
        return [.. header, .. pixels];
    }

    private static byte[] BuildDds(int width, int height, int bitCount, byte[] pixels, uint fourCc = 0)
    {
        var data = new byte[4 + 124 + pixels.Length];
        data[0] = (byte)'D';
        data[1] = (byte)'D';
        data[2] = (byte)'S';
        data[3] = (byte)' ';
        WriteInt32(data, 4, 124);
        WriteInt32(data, 4 + 12, height);
        WriteInt32(data, 4 + 16, width);
        WriteInt32(data, 4 + 76, 32);
        if (fourCc == 0)
        {
            WriteInt32(data, 4 + 80, 0x41);
            WriteInt32(data, 4 + 88, bitCount);
        }
        else
        {
            WriteInt32(data, 4 + 80, 0x4);
            WriteInt32(data, 4 + 84, unchecked((int)fourCc));
        }

        Array.Copy(pixels, 0, data, 4 + 124, pixels.Length);
        return data;
    }

    private static void WriteInt32(byte[] data, int offset, int value)
    {
        data[offset] = (byte)(value & 0xFF);
        data[offset + 1] = (byte)((value >> 8) & 0xFF);
        data[offset + 2] = (byte)((value >> 16) & 0xFF);
        data[offset + 3] = (byte)((value >> 24) & 0xFF);
    }
}
