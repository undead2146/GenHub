namespace GenHub.Core.Models.AdvancedLauncher
{
    /// <summary>
    /// Defines the mode for launching applications
    /// </summary>
    public enum LaunchMode
    {
        /// <summary>
        /// Normal launch showing the GenHub UI first
        /// </summary>
        Normal,

        /// <summary>
        /// Quick launch that bypasses the UI and launches directly
        /// </summary>
        Quick,

        /// <summary>
        /// Validate only mode - checks if the game can be launched but doesn't launch it
        /// </summary>
        Validate,

        /// <summary>
        /// Background launch mode for automated scenarios
        /// </summary>
        Background,

        /// <summary>
        /// Diagnostic mode that provides detailed information about the launch process
        /// </summary>
        Diagnostic
    }

    /// <summary>
    /// Defines validation levels for launch operations
    /// </summary>
    public enum LaunchValidation
    {
        /// <summary>
        /// No validation performed
        /// </summary>
        None,

        /// <summary>
        /// Basic validation (file existence, basic checks)
        /// </summary>
        Basic,

        /// <summary>
        /// Full validation (all checks including game file integrity)
        /// </summary>
        Full
    }

    /// <summary>
    /// Defines actions that can be performed via protocol URLs
    /// </summary>
    public enum ProtocolAction
    {
        /// <summary>
        /// Launch a specific profile
        /// </summary>
        Launch,

        /// <summary>
        /// Create a desktop shortcut
        /// </summary>
        CreateShortcut,

        /// <summary>
        /// Open the shortcut manager
        /// </summary>
        ManageShortcuts,

        /// <summary>
        /// Perform quick setup
        /// </summary>
        QuickSetup,

        /// <summary>
        /// Show diagnostics information
        /// </summary>
        Diagnostics,

        /// <summary>
        /// Validate a profile
        /// </summary>
        Validate
    }
}
