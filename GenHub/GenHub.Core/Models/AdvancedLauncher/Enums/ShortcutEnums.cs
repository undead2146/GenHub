namespace GenHub.Core.Models.AdvancedLauncher
{
    /// <summary>
    /// Defines the type of desktop shortcut
    /// </summary>
    public enum ShortcutType
    {
        /// <summary>
        /// Shortcut for launching a specific game profile
        /// </summary>
        Profile,

        /// <summary>
        /// Shortcut for launching a specific game version directly
        /// </summary>
        Game,

        /// <summary>
        /// Shortcut for opening GenHub in quick launch mode
        /// </summary>
        QuickLauncher,

        /// <summary>
        /// Shortcut for opening the GenHub shortcut manager
        /// </summary>
        Manager,

        /// <summary>
        /// Shortcut for opening GenHub diagnostics
        /// </summary>
        Diagnostics
    }

    /// <summary>
    /// Defines how the shortcut should launch the application
    /// </summary>
    public enum ShortcutLaunchMode
    {
        /// <summary>
        /// Show GenHub UI first, then launch the profile
        /// </summary>
        Normal,

        /// <summary>
        /// Launch the profile directly without showing GenHub UI
        /// </summary>
        Direct,

        /// <summary>
        /// Validate the profile first, then launch
        /// </summary>
        Validate,

        /// <summary>
        /// Ask the user how they want to launch
        /// </summary>
        Ask
    }

    /// <summary>
    /// Defines where to create the shortcut
    /// </summary>
    public enum ShortcutLocation
    {
        /// <summary>
        /// Create shortcut on the desktop
        /// </summary>
        Desktop,

        /// <summary>
        /// Create shortcut in the start menu
        /// </summary>
        StartMenu,

        /// <summary>
        /// Create shortcut in both desktop and start menu
        /// </summary>
        Both,

        /// <summary>
        /// Create shortcut in a custom location
        /// </summary>
        Custom
    }

    /// <summary>
    /// Defines the creation mode for shortcuts
    /// </summary>
    public enum ShortcutCreationMode
    {
        /// <summary>
        /// Create shortcut only if it doesn't exist
        /// </summary>
        CreateIfNotExists,

        /// <summary>
        /// Always create shortcut, overwriting existing ones
        /// </summary>
        Overwrite,

        /// <summary>
        /// Update existing shortcut or create if it doesn't exist
        /// </summary>
        UpdateOrCreate,

        /// <summary>
        /// Skip creation if shortcut already exists
        /// </summary>
        SkipIfExists
    }
}
