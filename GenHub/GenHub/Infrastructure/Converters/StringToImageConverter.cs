using Avalonia;
using Avalonia.Controls;
using Avalonia.Data.Converters;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using Microsoft.Extensions.Logging;

namespace GenHub.Infrastructure.Converters
{
    /// <summary>
    /// Converts a string path to an image source
    /// </summary>
    public class StringToImageConverter : IValueConverter
    {
        private static readonly string[] FallbackIcons = new[]
        {
            "avares://GenHub/Assets/Icons/genhub-icon.png",
            "avares://GenHub/Assets/Icons/genhub-logo.png",
            "avares://GenHub/Assets/Icons/generals-icon.png",
            "avares://GenHub/Assets/Icons/zerohour-icon.png"
        };
        
        private static readonly string[] FallbackCovers = new[]
        {
            "avares://GenHub/Assets/Covers/generals-cover-2.png",
            "avares://GenHub/Assets/Covers/generals-cover.png",
            "avares://GenHub/Assets/Covers/zerohour-cover.png",
        };
        
        // Logger instance (optional but helpful for debugging)
        private static readonly ILogger? _logger = AppLocator.Services?.GetService(typeof(ILogger<StringToImageConverter>)) as ILogger;
        
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is not string path || string.IsNullOrEmpty(path))
                return null;
            
            try
            {
                Console.WriteLine($"StringToImageConverter: Converting {path} to image");
                
                // If this is a direct file path and it exists, load it directly
                if (File.Exists(path))
                {
                    return new Bitmap(path);
                }
                
                // Handle different path formats
                if (path.StartsWith("avares://"))
                {
                    // It's already an Avalonia resource URI
                    try
                    {
                        var uri = new Uri(path);
                        var asset = AssetLoader.Open(uri);
                        return new Bitmap(asset);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error loading resource {path}: {ex.Message}");
                        // Fall through to fallbacks
                    }
                }
                else if (path.StartsWith("/"))
                {
                    string resourcePath;
                    
                    // Check if the path already starts with "/Assets/"
                    if (path.StartsWith("/Assets/", StringComparison.OrdinalIgnoreCase))
                    {
                        // Remove the leading slash and construct URI
                        resourcePath = $"avares://GenHub{path}";
                    }
                    else
                    {
                        // For paths like "/Icons/..." that don't already have "Assets/"
                        resourcePath = $"avares://GenHub/Assets{path}";
                    }
                    
                    try
                    {
                        var uri = new Uri(resourcePath);
                        Console.WriteLine($"Trying resource path: {resourcePath}");
                        var asset = AssetLoader.Open(uri);
                        return new Bitmap(asset);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error loading resource {resourcePath}: {ex.Message}");
                        
                        // Try with just the filename as fallback
                        string filename = Path.GetFileName(path);
                        string altPath = $"avares://GenHub/Assets/{filename}";
                        
                        try
                        {
                            Console.WriteLine($"Trying alternative resource path: {altPath}");
                            var uri = new Uri(altPath);
                            var asset = AssetLoader.Open(uri);
                            return new Bitmap(asset);
                        }
                        catch
                        {
                            // Fall through to fallbacks
                        }
                    }
                }
                
                // Try fallback paths since the original path doesn't work
                string[] fallbacks = path.Contains("cover", StringComparison.OrdinalIgnoreCase) ? FallbackCovers : FallbackIcons;
                
                foreach (var fallbackPath in fallbacks)
                {
                    try
                    {
                        Console.WriteLine($"Trying fallback resource: {fallbackPath}");
                        var uri = new Uri(fallbackPath);
                        var asset = AssetLoader.Open(uri);
                        return new Bitmap(asset);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error loading fallback {fallbackPath}: {ex.Message}");
                        // Try the next fallback
                    }
                }
                
                // If we get here, all attempts failed
                Console.WriteLine("All image loading attempts failed");
                return null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in StringToImageConverter: {ex.Message}");
                return null;
            }
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
