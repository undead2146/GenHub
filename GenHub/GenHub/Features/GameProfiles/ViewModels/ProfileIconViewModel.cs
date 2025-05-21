using CommunityToolkit.Mvvm.ComponentModel;

namespace GenHub.Features.GameProfiles.ViewModels

{
    /// <summary>
    /// for icon selection in profile settings
    /// </summary>
    public class ProfileIconViewModel : ObservableObject
    {
        private string _displayName = string.Empty;
        private string _path = string.Empty;
        
        /// <summary>
        /// Display name of the icon or cover for UI
        /// </summary>
        public string DisplayName
        {
            get => _displayName;
            set => SetProperty(ref _displayName, value);
        }
        
        /// <summary>
        /// Path to the icon or cover file
        /// </summary>
        public string Path
        {
            get => _path;
            set => SetProperty(ref _path, value);
        }
        
        /// <summary>
        /// Name is kept for backward compatibility - use DisplayName for new code
        /// </summary>
        public string Name 
        { 
            get => DisplayName;
            set => DisplayName = value;
        }
        
        /// <summary>
        /// Default constructor
        /// </summary>
        public ProfileIconViewModel() { }
        
        /// <summary>
        /// Creates a new icon view model with the specified name and path
        /// </summary>
        /// <param name="displayName">The display name of the icon</param>
        /// <param name="path">The path to the icon file</param>
        public ProfileIconViewModel(string displayName, string path)
        {
            DisplayName = displayName;
            Path = path;
        }
    }
}
