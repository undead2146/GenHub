namespace GenHub.Core.Models
{
    /// <summary>
    /// Enum representing different stages of installation
    /// </summary>
    public enum InstallStage
    {
        /// <summary>
        /// Not currently installing
        /// </summary>
        None,
        
        /// <summary>
        /// Preparing for installation (creating directories, etc.)
        /// </summary>
        Preparing,
        
        /// <summary>
        /// Downloading artifact
        /// </summary>
        Downloading,
        
        /// <summary>
        /// Extracting downloaded content
        /// </summary>
        Extracting,
        
        /// <summary>
        /// Verifying installation
        /// </summary>
        Verifying,
        
        /// <summary>
        /// Creating shortcuts and finalizing
        /// </summary>
        Finalizing,
        
        /// <summary>
        /// Installation complete
        /// </summary>
        Completed,
        
        /// <summary>
        /// Installation failed
        /// </summary>
        Failed,
        /// <summary>
        /// Installation encountered an error
        /// </summary>
        Error,
    }
}
