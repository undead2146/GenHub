namespace GenHub.Core.Constants;

/// <summary>
/// Constants for the Texture Editor tool and shared SAGE texture services.
/// </summary>
public static class TextureEditorConstants
{
    /// <summary>
    /// Tool identifier for Texture Editor.
    /// </summary>
    public const string ToolId = "texture-editor";

    /// <summary>
    /// Tool display name for Texture Editor.
    /// </summary>
    public const string ToolName = "Texture Editor";

    /// <summary>
    /// Tool description for Texture Editor.
    /// </summary>
    public const string ToolDescription = "Slice texture atlases, edit MappedImages visually, and pack loose icons into SAGE sheets.";

    /// <summary>
    /// Icon URI for Texture Editor tool.
    /// </summary>
    public const string IconPath = UriConstants.TextureEditorIconUri;

    /// <summary>
    /// Root SAGE directory for MappedImages INI files, relative to game data.
    /// </summary>
    public const string MappedImagesRootDirectory = "Data/INI/MappedImages";

    /// <summary>
    /// INI block keyword for a mapped image entry.
    /// </summary>
    public const string IniBlockName = "MappedImage";

    /// <summary>
    /// INI block terminator keyword.
    /// </summary>
    public const string IniBlockEnd = "End";

    /// <summary>
    /// Default status value written for MappedImage entries.
    /// </summary>
    public const string DefaultStatus = "NONE";

    /// <summary>
    /// Maximum texture dimension in pixels supported by SAGE Direct3D 8 atlases.
    /// </summary>
    public const int MaxTextureDimension = 2048;

    /// <summary>
    /// Default padding in pixels between packed sprites to prevent texture bleed.
    /// </summary>
    public const int DefaultPadding = 1;

    /// <summary>
    /// Standard large cameo width in pixels.
    /// </summary>
    public const int CameoLargeWidth = 64;

    /// <summary>
    /// Standard large cameo height in pixels.
    /// </summary>
    public const int CameoLargeHeight = 64;

    /// <summary>
    /// Standard small cameo width in pixels.
    /// </summary>
    public const int CameoSmallWidth = 60;

    /// <summary>
    /// Standard small cameo height in pixels.
    /// </summary>
    public const int CameoSmallHeight = 48;

    /// <summary>
    /// Standard HUD button width in pixels.
    /// </summary>
    public const int HudButtonWidth = 32;

    /// <summary>
    /// Standard HUD button height in pixels.
    /// </summary>
    public const int HudButtonHeight = 32;

    /// <summary>
    /// Item type identifier for texture atlas build rules in ModBuilder project files.
    /// </summary>
    public const string ModBuilderItemType = "TextureAtlas";

    /// <summary>
    /// File pattern for MappedImages INI files.
    /// </summary>
    public const string MappedImagesFilePattern = "*.ini";

    /// <summary>
    /// Supported texture file extensions for the editor.
    /// </summary>
    public static readonly string[] TextureExtensions = [".tga", ".dds", ".png"];

    /// <summary>
    /// Valid power-of-two atlas dimensions.
    /// </summary>
    public static readonly int[] PowerOfTwoSizes = [32, 64, 128, 256, 512, 1024, 2048];
}
