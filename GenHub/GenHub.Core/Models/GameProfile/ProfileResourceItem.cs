namespace GenHub.Core.Models.GameProfile;

/// <summary>
/// Represents an icon or cover image for profiles.
/// </summary>
public class ProfileResourceItem
{
    /// <summary>
    /// Gets or sets the unique identifier for this resource.
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the path to the resource.
    /// </summary>
    public string Path { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the display name for this resource.
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether this is a built-in resource.
    /// </summary>
    public bool IsBuiltIn { get; set; }

    /// <summary>
    /// Gets or sets the game type this resource is associated with.
    /// </summary>
    public string? GameType { get; set; }
}
