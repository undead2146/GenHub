using System;
using System.Collections.Generic;
using System.Linq;

namespace GenHub.Core.Models.SourceMetadata
{
    /// <summary>
    /// Metadata for manually installed game versions
    /// </summary>
    public class CustomSourceMetadata : BaseSourceMetadata
    {
        /// <summary>
        /// Gets or sets user-provided notes for this installation
        /// </summary>
        public string Notes { get; set; } = string.Empty;
        /// <summary>
        /// Gets or sets custom key-value data for this installation
        /// </summary>
        public Dictionary<string, string> CustomData { get; set; } = new Dictionary<string, string>();
        /// <summary>
        /// Gets or sets the installation date
        /// </summary>
        public DateTime InstallationDate { get; set; } = DateTime.Now;

        /// <summary>
        /// Creates a deep copy of this metadata
        /// </summary>
        public CustomSourceMetadata Clone()
        {
            return new CustomSourceMetadata
            {
                // Create a new dictionary with the same key-value pairs
                CustomData = CustomData?.ToDictionary(entry => entry.Key, entry => entry.Value)
            };
        }
    }
}
