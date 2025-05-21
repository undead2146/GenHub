using System;

namespace GenHub.Core.Models.SourceMetadata
{
    /// <summary>
    /// Base class for source-specific metadata for GameVersion and GameProfile.
    /// This provides a common base type for polymorphic behavior with different source types.
    /// </summary>
    public abstract class BaseSourceMetadata
    {
        
        /// <summary>
        /// Creates a clone of this metadata instance
        /// </summary>
        public virtual BaseSourceMetadata? Clone()
        {
            return this.GetType().GetConstructor(Type.EmptyTypes)?.Invoke(Array.Empty<object>()) as BaseSourceMetadata;
        }
    }
}
