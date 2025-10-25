using System.Text.Json.Serialization;

namespace GenHub.Plugins.GeneralsOnline;

/// <summary>
/// Represents the API response from the Generals Online CDN.
/// </summary>
public class GeneralsOnlineApiResponse
{
    /// <summary>
    /// Gets or sets the version of the release.
    /// </summary>
    [JsonPropertyName("version")]
    public string Version { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the download URL for the release.
    /// </summary>
    [JsonPropertyName("download_url")]
    public string DownloadUrl { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the release notes.
    /// </summary>
    [JsonPropertyName("release_notes")]
    public string ReleaseNotes { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the file size in bytes.
    /// </summary>
    [JsonPropertyName("size")]
    public long Size { get; set; }

    /// <summary>
    /// Gets or sets the SHA256 hash of the file.
    /// </summary>
    [JsonPropertyName("sha256")]
    public string Sha256 { get; set; } = string.Empty;
}