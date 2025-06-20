using System;
using System.Text.Json.Serialization;
using GenHub.Core.Models.GitHub;

namespace GenHub.Core.Models.GitHub
{
    /// <summary>
    /// Represents an asset (file) attached to a GitHub release
    /// </summary>
    public class GitHubReleaseAsset
    {
        /// <summary>
        /// The asset ID
        /// </summary>
        public long Id { get; set; }

        /// <summary>
        /// The asset name (filename)
        /// </summary>
        public string Name { get; set; } = string.Empty;
        
        /// <summary>
        /// The label for the asset (typically null)
        /// </summary>
        public string? Label { get; set; }

        /// <summary>
        /// The content type of the asset
        /// </summary>
        [JsonPropertyName("content_type")]
        public string ContentType { get; set; } = string.Empty;

        /// <summary>
        /// The size of the asset in bytes
        /// </summary>
        public long Size { get; set; }

        /// <summary>
        /// How many times this asset has been downloaded
        /// </summary>
        [JsonPropertyName("download_count")]
        public int DownloadCount { get; set; }

        /// <summary>
        /// When the asset was created
        /// </summary>
        [JsonPropertyName("created_at")]
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// When the asset was last updated
        /// </summary>
        [JsonPropertyName("updated_at")]
        public DateTime UpdatedAt { get; set; }

        /// <summary>
        /// The URL to download this asset directly
        /// </summary>
        [JsonPropertyName("browser_download_url")]
        public string BrowserDownloadUrl { get; set; } = string.Empty;

        /// <summary>
        /// State of the asset (usually "uploaded")
        /// </summary>
        public string State { get; set; } = string.Empty;

        /// <summary>
        /// The release this asset belongs to
        /// </summary>
        [JsonIgnore]
        public GitHubRelease? Release { get; set; }

        /// <summary>
        /// Formatted file size for display
        /// </summary>
        [JsonIgnore]
        public string FormattedSize 
        { 
            get
            {
                string[] suffixes = { "B", "KB", "MB", "GB", "TB" };
                int suffixIndex = 0;
                double size = Size;
                
                while (size >= 1024 && suffixIndex < suffixes.Length - 1)
                {
                    size /= 1024;
                    suffixIndex++;
                }
                
                return $"{size:0.##} {suffixes[suffixIndex]}";
            }
        }

        /// <summary>
        /// Gets a formatted file size string
        /// </summary>
        public string GetFormattedSize()
        {
            if (Size < 1024)
                return $"{Size} B";
            if (Size < 1024 * 1024)
                return $"{Size / 1024.0:F1} KB";
            if (Size < 1024 * 1024 * 1024)
                return $"{Size / (1024.0 * 1024.0):F1} MB";
            
            return $"{Size / (1024.0 * 1024.0 * 1024.0):F1} GB";
        }

        /// <summary>
        /// Gets the file extension of the asset
        /// </summary>
        public string GetFileExtension()
        {
            return System.IO.Path.GetExtension(Name).ToLowerInvariant();
        }

        /// <summary>
        /// Gets whether this asset is likely an executable
        /// </summary>
        public bool IsExecutable()
        {
            var extension = GetFileExtension();
            return extension == ".exe" || extension == ".msi" || extension == ".deb" || extension == ".rpm" || extension == ".dmg";
        }

        /// <summary>
        /// Gets whether this asset is likely an archive
        /// </summary>
        public bool IsArchive()
        {
            var extension = GetFileExtension();
            return extension == ".zip" || extension == ".tar" || extension == ".gz" || extension == ".7z" || extension == ".rar";
        }
    }
}
