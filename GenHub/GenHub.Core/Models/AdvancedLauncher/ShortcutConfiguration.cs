using System;
using System.Collections.Generic;

namespace GenHub.Core.Models.AdvancedLauncher
{
    /// <summary>
    /// Configuration for creating desktop shortcuts
    /// </summary>
    public class ShortcutConfiguration
    {
        /// <summary>
        /// Gets or sets the unique identifier for this shortcut configuration
        /// </summary>
        public string Id { get; set; } = Guid.NewGuid().ToString();

        /// <summary>
        /// Gets or sets the display name for the shortcut
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the profile ID this shortcut is for
        /// </summary>
        public string ProfileId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the path to the icon file
        /// </summary>
        public string IconPath { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the type of shortcut to create
        /// </summary>
        public ShortcutType Type { get; set; } = ShortcutType.Profile;

        /// <summary>
        /// Gets or sets the launch mode for the shortcut
        /// </summary>
        public ShortcutLaunchMode LaunchMode { get; set; } = ShortcutLaunchMode.Normal;

        /// <summary>
        /// Gets or sets custom command line arguments
        /// </summary>
        public string CustomArguments { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets whether to run as administrator
        /// </summary>
        public bool RunAsAdmin { get; set; }

        /// <summary>
        /// Gets or sets the category for organizing shortcuts
        /// </summary>
        public string Category { get; set; } = "Games";

        /// <summary>
        /// Gets or sets the description for the shortcut
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets platform-specific options
        /// </summary>
        public Dictionary<string, string> PlatformSpecificOptions { get; set; } = new();

        /// <summary>
        /// Gets or sets when this configuration was created
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Gets or sets when this shortcut was last validated
        /// </summary>
        public DateTime LastValidated { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Gets or sets whether the shortcut is currently valid
        /// </summary>
        public bool IsValid { get; set; } = true;

        /// <summary>
        /// Gets or sets where to create the shortcut
        /// </summary>
        public ShortcutLocation Location { get; set; } = ShortcutLocation.Desktop;

        /// <summary>
        /// Gets or sets the working directory for the shortcut
        /// </summary>
        public string? WorkingDirectory { get; set; }

        /// <summary>
        /// Gets or sets whether to show the console window when launching
        /// </summary>
        public bool ShowConsole { get; set; }

        /// <summary>
        /// Creates a default configuration for a profile
        /// </summary>
        /// <param name="profileId">Profile ID</param>
        /// <param name="profileName">Profile name</param>
        /// <returns>Default shortcut configuration</returns>
        public static ShortcutConfiguration ForProfile(string profileId, string profileName)
        {
            return new ShortcutConfiguration
            {
                ProfileId = profileId,
                Name = profileName,
                Type = ShortcutType.Profile,
                Description = $"Launch {profileName} via GenHub"
            };
        }

        /// <summary>
        /// Creates a copy of this configuration
        /// </summary>
        /// <returns>Cloned configuration</returns>
        public ShortcutConfiguration Clone()
        {
            return new ShortcutConfiguration
            {
                Id = this.Id,
                Name = this.Name,
                ProfileId = this.ProfileId,
                IconPath = this.IconPath,
                Type = this.Type,
                LaunchMode = this.LaunchMode,
                CustomArguments = this.CustomArguments,
                RunAsAdmin = this.RunAsAdmin,
                Category = this.Category,
                Description = this.Description,
                PlatformSpecificOptions = new Dictionary<string, string>(this.PlatformSpecificOptions),
                CreatedAt = this.CreatedAt,
                LastValidated = this.LastValidated,
                IsValid = this.IsValid,
                Location = this.Location,
                WorkingDirectory = this.WorkingDirectory,
                ShowConsole = this.ShowConsole
            };
        }
    }
}
