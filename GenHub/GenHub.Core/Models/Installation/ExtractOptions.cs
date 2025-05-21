using System;
using System.Collections.Generic;

namespace GenHub.Core.Models
{
    /// <summary>
    /// Options for extracting game versions
    /// </summary>
    public class ExtractOptions
    {
        /// <summary>
        /// Custom name to use for the installation directory
        /// </summary>
        public string? CustomInstallName { get; set; }
        
        /// <summary>
        /// Whether to delete the ZIP file after successful extraction
        /// </summary>
        public bool DeleteZipAfterExtraction { get; set; } = true;
        
        /// <summary>
        /// Whether to prefer Zero Hour executables when looking for the game
        /// </summary>
        public bool PreferZeroHour { get; set; } = false;
        
        /// <summary>
        /// Path to the archive file to extract
        /// </summary>
        public string? ArchivePath { get; set; }

        /// <summary>
        /// Additional parameters for extraction
        /// </summary>
        public Dictionary<string, string>? AdditionalParams { get; set; }
    }
}
