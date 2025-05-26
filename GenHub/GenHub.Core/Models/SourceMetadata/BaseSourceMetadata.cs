using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace GenHub.Core.Models.SourceMetadata
{
    /// <summary>
    /// Base class for source-specific metadata following MVVM patterns
    /// </summary>
    public abstract class BaseSourceMetadata : ICloneable
    {
        /// <summary>
        /// The type of source (e.g., "GitHub", "FileSystem", "Custom")
        /// </summary>
        public string SourceType { get; set; } = string.Empty;

        /// <summary>
        /// When this metadata was created
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// When this metadata was last updated
        /// </summary>
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Additional properties that can be stored as JSON
        /// </summary>
        [JsonExtensionData]
        public Dictionary<string, object>? ExtensionData { get; set; }

        /// <summary>
        /// Updates the UpdatedAt timestamp
        /// </summary>
        public void Touch()
        {
            UpdatedAt = DateTime.UtcNow;
        }

        /// <summary>
        /// Creates a shallow copy of this metadata instance
        /// Override in derived classes for deep copying
        /// </summary>
        public virtual object Clone()
        {
            return this.MemberwiseClone();
        }

        public override string ToString()
        {
            return $"{SourceType} Source Metadata";
        }
    }
}
