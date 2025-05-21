using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using System;
using System.Globalization;
using System.IO;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace GenHub.Infrastructure.Converters
{
    /// <summary>
    /// Converts a profile cover image path to a Bitmap or returns a default cover if not found
    /// </summary>
    public class ProfileCoverConverter : IValueConverter
    {
        // CORRECT EXTENSIONS - These should all be .png
        private static readonly string DefaultGeneralsCover = "/Assets/Covers/generals-cover.png"; 
        private static readonly string DefaultZeroHourCover = "/Assets/Covers/zerohour-cover.png";
        private static readonly string FallbackCover = "/Assets/Covers/generals-cover-2.png";
        
        private static IImage _fallbackImage; // Cache a fallback image
        
        private static readonly ILogger _logger;
        
        static ProfileCoverConverter()
        {
            // Create a static logger for the converter
            var loggerFactory = AppLocator.Services?.GetService(typeof(ILoggerFactory)) as ILoggerFactory;
            _logger = loggerFactory?.CreateLogger<ProfileCoverConverter>() ?? 
                      (ILogger)NullLogger.Instance;
        }
        
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            try
            {
                _logger.LogDebug("Converting path to image: {Path}", value);
                
                // Default to Generals cover if nothing provided
                if (value == null || string.IsNullOrEmpty(value.ToString()))
                {
                    return LoadDefaultCover(DefaultGeneralsCover);
                }

                string path = value.ToString();

                // Special handling for default covers - use the correct paths
                if (path != null && path.Contains("generals-cover", StringComparison.OrdinalIgnoreCase))
                {
                    if (path.Contains("generals-cover-2", StringComparison.OrdinalIgnoreCase))
                    {
                        return LoadDefaultCover("/Assets/Covers/generals-cover-2.png");
                    }
                    return LoadDefaultCover(DefaultGeneralsCover);
                }
                else if (path != null && path.Contains("zerohour", StringComparison.OrdinalIgnoreCase))
                {
                    return LoadDefaultCover(DefaultZeroHourCover);
                }

                // Try to load from file system if it's a file path
                if (path != null && File.Exists(path))
                {
                    try
                    {
                        return new Bitmap(path);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error loading image from file: {Path}", path);
                        return LoadDefaultCover(DefaultGeneralsCover);
                    }
                }

                // Try to load from application assets
                if (path != null && path.StartsWith("/"))
                {
                    return LoadDefaultCover(path);
                }

                // Fallback to default
                return LoadDefaultCover(DefaultGeneralsCover);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in converter");
                
                // Return the cached fallback image if we have one
                if (_fallbackImage != null)
                    return _fallbackImage;
                    
                // Otherwise try to create one
                try 
                {
                    using var stream = AssetLoader.Open(new Uri($"avares://GenHub{FallbackCover}"));
                    _fallbackImage = new Bitmap(stream);
                    return _fallbackImage;
                }
                catch
                {
                    // Last resort fallback
                    return new Bitmap(new MemoryStream(new byte[4])); // Tiny transparent image
                }
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }

        private IImage LoadDefaultCover(string assetPath)
        {
            try
            {
                _logger.LogDebug("Loading image from: {Path}", assetPath);
                
                // Use the Avalonia assets API
                var uri = new Uri($"avares://GenHub{assetPath}");
                
                // Use AssetLoader directly
                using var stream = AssetLoader.Open(uri);
                return new Bitmap(stream);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading resource {Path}: {Message}", assetPath, ex.Message);
                
                // Try fallback resource - generals-cover-2.png
                try
                {
                    _logger.LogDebug("Trying fallback resource: avares://GenHub{FallbackCover}", FallbackCover);
                    var fallbackUri = new Uri($"avares://GenHub{FallbackCover}");
                    using var stream = AssetLoader.Open(fallbackUri);
                    return new Bitmap(stream);
                }
                catch (Exception fallbackEx)
                {
                    _logger.LogError(fallbackEx, "Even fallback resource failed");
                    
                    // If all else fails, create a memory-based fallback
                    if (_fallbackImage == null)
                    {
                        var rtb = new RenderTargetBitmap(new Avalonia.PixelSize(100, 100));
                        _fallbackImage = rtb;
                    }
                    return _fallbackImage;
                }
            }
        }
    }
}
