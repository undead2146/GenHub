namespace GenHub.Core.Models.Enums;

/// <summary>
/// Represents texture quality levels.
/// </summary>
public enum TextureQuality
{
    /// <summary>
    /// Low texture quality (highest performance).
    /// </summary>
    Low = 0,

    /// <summary>
    /// Medium texture quality.
    /// </summary>
    Medium = 1,

    /// <summary>
    /// High texture quality (lowest performance).
    /// </summary>
    High = 2,

    /// <summary>
    /// Very High texture quality (TheSuperHackers client only).
    /// </summary>
    VeryHigh = 3,
}
