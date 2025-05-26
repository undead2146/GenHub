using System;
using System.Collections.Generic;
using System.Text.Json;
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
        /// Creates a deep copy of this metadata instance using JSON serialization
        /// Override in derived classes for more efficient copying if needed
        /// </summary>
        public virtual object Clone()
        {
            try
            {
                // Use JSON serialization for deep cloning
                var options = new JsonSerializerOptions
                {
                    WriteIndented = false,
                    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
                };

                string json = JsonSerializer.Serialize(this, this.GetType(), options);
                var cloned = JsonSerializer.Deserialize(json, this.GetType(), options);
                
                return cloned ?? this.MemberwiseClone();
            }
            catch (Exception)
            {
                // Fallback to shallow clone if JSON serialization fails
                return this.MemberwiseClone();
            }
        }

        public override string ToString()
        {
            return $"{SourceType} Source Metadata";
        }
    }
}
