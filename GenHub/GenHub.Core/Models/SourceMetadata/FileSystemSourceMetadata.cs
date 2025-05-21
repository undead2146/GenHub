using System;

namespace GenHub.Core.Models.SourceMetadata
{
    /// <summary>
    /// Metadata for game versions installed from local file system
    /// </summary>
    public class FileSystemSourceMetadata : BaseSourceMetadata
    {
        /// <summary>
        /// Gets or sets the original path this version was installed from
        /// </summary>
        public string OriginalPath { get; set; } = string.Empty;
        
        /// <summary>
        /// Gets or sets the installation date
        /// </summary>
        public DateTime InstallationDate { get; set; } = DateTime.Now;

        /// <summary>
        /// Creates a deep copy of this metadata
        /// </summary>
        public DateTime ImportDate { get; set; } = DateTime.Now;

        /// <summary>
        /// Gets or sets the source this version was imported from
        /// </summary>
        public string ImportSource { get; set; } = string.Empty;

        /// <summary>
        /// Creates a deep copy of this metadata
        /// </summary>
        public FileSystemSourceMetadata Clone()
        {
            return new FileSystemSourceMetadata
            {
                ImportDate = ImportDate,
                OriginalPath = OriginalPath,
                ImportSource = ImportSource,
                InstallationDate = InstallationDate
            };
        }
    }
}
