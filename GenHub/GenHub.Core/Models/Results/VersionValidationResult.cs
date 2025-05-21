using System;

namespace GenHub.Core.Models.Results
{
    /// <summary>
    /// Result of validating a game version
    /// </summary>
    public class VersionValidationResult
    {
        /// <summary>
        /// Whether the version is valid
        /// </summary>
        public bool IsValid { get; set; }

        /// <summary>
        /// Error message if validation failed
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// Number of files in the installation
        /// </summary>
        public int FileCount { get; set; }

        /// <summary>
        /// Total size of the installation in bytes
        /// </summary>
        public long SizeInBytes { get; set; }

        /// <summary>
        /// Human-readable formatted size
        /// </summary>
        public string FormattedSize
        {
            get
            {
                if (SizeInBytes < 1024)
                    return $"{SizeInBytes} bytes";
                else if (SizeInBytes < 1024 * 1024)
                    return $"{SizeInBytes / 1024.0:F1} KB";
                else if (SizeInBytes < 1024 * 1024 * 1024)
                    return $"{SizeInBytes / (1024.0 * 1024.0):F1} MB";
                else
                    return $"{SizeInBytes / (1024.0 * 1024.0 * 1024.0):F1} GB";
            }
        }

        /// <summary>
        /// When the validation was performed
        /// </summary>
        public DateTime ValidationTime { get; set; } = DateTime.Now;
    }
}
