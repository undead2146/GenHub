using System;
using System.Collections.Generic;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;

namespace GenHub.Core.Models.GameProfiles
{
    /// <summary>
    /// Comparer for DataPathItem to prevent duplicates in collections
    /// </summary>
    public class DataPathItemComparer : IEqualityComparer<DataPathItem>
    {
        public bool Equals(DataPathItem? x, DataPathItem? y)
        {
            if (ReferenceEquals(x, y)) return true;
            if (x is null || y is null) return false;

            return string.Equals(x.Path, y.Path, StringComparison.OrdinalIgnoreCase);
        }

        public int GetHashCode(DataPathItem obj)
        {
            return obj.Path?.ToLowerInvariant().GetHashCode() ?? 0;
        }
    }

    /// <summary>
    /// Comparer for ExecutablePathItem to prevent duplicates in collections
    /// </summary>
    public class ExecutablePathItemComparer : IEqualityComparer<ExecutablePathItem>
    {
        public bool Equals(ExecutablePathItem? x, ExecutablePathItem? y)
        {
            if (ReferenceEquals(x, y)) return true;
            if (x is null || y is null) return false;

            return string.Equals(x.Path, y.Path, StringComparison.OrdinalIgnoreCase);
        }

        public int GetHashCode(ExecutablePathItem obj)
        {
            return obj.Path?.ToLowerInvariant().GetHashCode() ?? 0;
        }
    }
    /// <summary>
    /// Represents a data path item for the dropdown
    /// </summary>
    public partial class DataPathItem : ObservableObject
    {
        [ObservableProperty]
        private string _path;

        [ObservableProperty]
        private string _displayName;
        /// <summary>
        /// Source type of the installation
        /// </summary>
        public GameInstallationType SourceType { get; set; } = GameInstallationType.Unknown;


        [ObservableProperty]
        private string _gameType;

        [ObservableProperty]
        private bool _isValidSource;

        public DataPathItem(string path, string displayName, string gameType, bool isValidSource)
        {
            _path = path;
            _displayName = displayName;
            _gameType = gameType;
            _isValidSource = isValidSource;
        }
    }    /// <summary>
         /// Represents an executable path item for profile settings UI
         /// </summary>
    public class ExecutablePathItem
    {
        /// <summary>
        /// The full path to the executable file
        /// </summary>
        public string Path { get; set; } = string.Empty;

        /// <summary>
        /// Display name for the UI
        /// </summary>
        public string DisplayName { get; set; } = string.Empty;
        public string GameType { get; set; } = string.Empty;

        /// <summary>
        /// Source type of the installation
        /// </summary>
        public GameInstallationType SourceType { get; set; } = GameInstallationType.Unknown;

        /// <summary>
        /// Whether this is the "Browse..." option
        /// </summary>
        public bool IsBrowseOption { get; set; } = false;

        /// <summary>
        /// Returns the display name for the item
        /// </summary>
        public override string ToString() => DisplayName;
    }
}
