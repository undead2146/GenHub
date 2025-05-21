using System;

namespace GenHub.Features.GameProfiles.ViewModels
{
    /// <summary>
    /// Represents a theme color item in the profile settings UI
    /// </summary>
    public class ProfileThemeColorItem
    {
        /// <summary>
        /// The display name of the color
        /// </summary>
        public string Name { get; set; }
        
        /// <summary>
        /// The color value (e.g., "#FF0000")
        /// </summary>
        public string ColorValue { get; set; }
        
        /// <summary>
        /// Creates a new ProfileThemeColorItem
        /// </summary>
        public ProfileThemeColorItem(string name, string colorValue)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            ColorValue = colorValue ?? throw new ArgumentNullException(nameof(colorValue));
        }
    }
}
