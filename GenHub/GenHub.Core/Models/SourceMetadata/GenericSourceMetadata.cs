namespace GenHub.Core.Models.SourceMetadata
{
    /// <summary>
    /// Generic metadata for when the source type is unknown or unspecified
    /// </summary>
    public class GenericSourceMetadata : BaseSourceMetadata
    {
        /// <summary>
        /// Additional information (if any)
        /// </summary>
        public string AdditionalInfo { get; set; } = string.Empty;
    }
}
