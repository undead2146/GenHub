using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace GenHub.Infrastructure.Converters
{
    /// <summary>
    /// Converts an icon key to path data for use with PathIcon controls
    /// </summary>
    public class IconKeyToPathDataConverter : IValueConverter
    {
        private static readonly Dictionary<string, StreamGeometry> _iconCache = new Dictionary<string, StreamGeometry>();

        // Standard icons
        private static readonly StreamGeometry _fileIcon = StreamGeometry.Parse("M14,2H6A2,2 0 0,0 4,4V20A2,2 0 0,0 6,22H18A2,2 0 0,0 20,20V8L14,2M18,20H6V4H13V9H18V20Z");
        private static readonly StreamGeometry _folderIcon = StreamGeometry.Parse("M10,4H4C2.89,4 2,4.89 2,6V18A2,2 0 0,0 4,20H20A2,2 0 0,0 22,18V8C22,6.89 21.1,6 20,6H12L10,4Z");
        private static readonly StreamGeometry _downloadIcon = StreamGeometry.Parse("M5,20H19V18H5M19,9H15V3H9V9H5L12,16L19,9Z");
        private static readonly StreamGeometry _buildIcon = StreamGeometry.Parse("M14.6,16.6L19.2,12L14.6,7.4L16,6L22,12L16,18L14.6,16.6M9.4,16.6L4.8,12L9.4,7.4L8,6L2,12L8,18L9.4,16.6Z");
        private static readonly StreamGeometry _defaultIcon = StreamGeometry.Parse("M12,4A4,4 0 0,1 16,8A4,4 0 0,1 12,12A4,4 0 0,1 8,8A4,4 0 0,1 12,4M12,14C16.42,14 20,15.79 20,18V20H4V18C4,15.79 7.58,14 12,14Z");

        static IconKeyToPathDataConverter()
        {
            _iconCache["file"] = _fileIcon;
            _iconCache["folder"] = _folderIcon;
            _iconCache["download"] = _downloadIcon;
            _iconCache["build"] = _buildIcon;
            _iconCache["default"] = _defaultIcon;
            _iconCache["zip"] = StreamGeometry.Parse("M14,17H12V15H10V13H12V15H14M14,9H12V11H14V13H12V11H10V9H12V7H10V5H12V7H14M19,3H5C3.89,3 3,3.89 3,5V19A2,2 0 0,0 5,21H19A2,2 0 0,0 21,19V5C21,3.89 20.1,3 19,3Z");
            _iconCache["release"] = StreamGeometry.Parse("M17,3H7A2,2 0 0,0 5,5V21L12,18L19,21V5A2,2 0 0,0 17,3M12,7A2,2 0 0,1 14,9A2,2 0 0,1 12,11A2,2 0 0,1 10,9A2,2 0 0,1 12,7Z");
            _iconCache["workflow"] = StreamGeometry.Parse("M3,3H21V7H3V3M4,8H20V21H4V8M9,10V12H7V10H9M13,10V12H11V10H13M17,10V12H15V10H17M9,14V16H7V14H9M13,14V16H11V14H13M17,14V16H15V14H17Z");
            _iconCache["repository"] = StreamGeometry.Parse("M3,3H21V21H3V3M13,7H16V10H13V7M9,7H12V10H9V7M13,11H16V14H13V11M9,11H12V14H9V11M13,15H16V18H13V15M9,15H12V18H9V15Z");
            _iconCache["artifact"] = StreamGeometry.Parse("M21,16.5C21,16.88 20.79,17.21 20.47,17.38L12.57,21.82C12.41,21.94 12.21,22 12,22C11.79,22 11.59,21.94 11.43,21.82L3.53,17.38C3.21,17.21 3,16.88 3,16.5V7.5C3,7.12 3.21,6.79 3.53,6.62L11.43,2.18C11.59,2.06 11.79,2 12,2C12.21,2 12.41,2.06 12.57,2.18L20.47,6.62C20.79,6.79 21,7.12 21,7.5V16.5M12,4.15L6.04,7.5L12,10.85L17.96,7.5L12,4.15Z");
        }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string iconKey && !string.IsNullOrEmpty(iconKey))
            {
                if (_iconCache.TryGetValue(iconKey.ToLowerInvariant(), out var geometry))
                {
                    return geometry;
                }
            }
            
            return _iconCache["default"];
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
