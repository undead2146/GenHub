using GenHub.Core.Constants;

namespace GenHub.Core.Models.Tools.TextureEditor;

/// <summary>
/// Represents a single SAGE MappedImage entry binding a name to a texture sub-region.
/// Coordinates follow the SAGE rule from MappedImage.cpp: 0-indexed inclusive,
/// so Width = (Right - Left) + 1 and Height = (Bottom - Top) + 1.
/// </summary>
/// <param name="Name">The mapped image name referenced by WND and INI files.</param>
/// <param name="TextureFileName">The texture atlas file name, for example CommandBar.tga.</param>
/// <param name="TextureWidth">The full atlas width in pixels.</param>
/// <param name="TextureHeight">The full atlas height in pixels.</param>
/// <param name="Left">The inclusive left coordinate.</param>
/// <param name="Top">The inclusive top coordinate.</param>
/// <param name="Right">The inclusive right coordinate.</param>
/// <param name="Bottom">The inclusive bottom coordinate.</param>
/// <param name="Status">The SAGE status flag, usually NONE.</param>
/// <param name="SourcePath">The optional INI file this entry was parsed from.</param>
public sealed record MappedImageDefinition(
    string Name,
    string TextureFileName,
    int TextureWidth,
    int TextureHeight,
    int Left,
    int Top,
    int Right,
    int Bottom,
    string Status = TextureEditorConstants.DefaultStatus,
    string? SourcePath = null)
{
    /// <summary>
    /// Gets the slice width in pixels using inclusive SAGE coordinates.
    /// </summary>
    public int Width => (Right - Left) + 1;

    /// <summary>
    /// Gets the slice height in pixels using inclusive SAGE coordinates.
    /// </summary>
    public int Height => (Bottom - Top) + 1;

    /// <summary>
    /// Gets a value indicating whether coordinates are ordered and inside the texture bounds.
    /// </summary>
    public bool IsWithinTexture =>
        Left >= 0 && Top >= 0 &&
        Right >= Left && Bottom >= Top &&
        TextureWidth > 0 && TextureHeight > 0 &&
        Right < TextureWidth && Bottom < TextureHeight;

    /// <summary>
    /// Gets a value indicating whether a 1px alpha guard border fits inside the texture around the slice.
    /// </summary>
    public bool HasGuardBorder =>
        Left > 0 && Top > 0 &&
        TextureWidth > 0 && TextureHeight > 0 &&
        Right < TextureWidth - 1 && Bottom < TextureHeight - 1;
}
