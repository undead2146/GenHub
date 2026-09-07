using System;
using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media;
using GenHub.Core.Constants;
using GenHub.Core.Models.Enums;

namespace GenHub.Infrastructure.Converters;

/// <summary>
/// Converts a ContentType enum value to a SolidColorBrush for UI visual distinction.
/// </summary>
public class ContentTypeToBrushConverter : IValueConverter
{
    /// <summary>
    /// Gets the singleton instance of the converter.
    /// </summary>
    public static readonly ContentTypeToBrushConverter Instance = new();

    private static readonly IBrush GameClientBrush = new SolidColorBrush(Color.Parse(UiConstants.ContentTypeGameClientColor)).ToImmutable();
    private static readonly IBrush ModBrush = new SolidColorBrush(Color.Parse(UiConstants.ContentTypeModColor)).ToImmutable();
    private static readonly IBrush PatchBrush = new SolidColorBrush(Color.Parse(UiConstants.ContentTypePatchColor)).ToImmutable();
    private static readonly IBrush MapBrush = new SolidColorBrush(Color.Parse(UiConstants.ContentTypeMapColor)).ToImmutable();
    private static readonly IBrush AddonBrush = new SolidColorBrush(Color.Parse(UiConstants.ContentTypeAddonColor)).ToImmutable();
    private static readonly IBrush ToolBrush = new SolidColorBrush(Color.Parse(UiConstants.ContentTypeToolColor)).ToImmutable();
    private static readonly IBrush BundleBrush = new SolidColorBrush(Color.Parse(UiConstants.ContentTypeBundleColor)).ToImmutable();
    private static readonly IBrush MissionBrush = new SolidColorBrush(Color.Parse(UiConstants.ContentTypeMissionColor)).ToImmutable();
    private static readonly IBrush SkinBrush = new SolidColorBrush(Color.Parse(UiConstants.ContentTypeSkinColor)).ToImmutable();

    /// <summary>
    /// Converts a ContentType to a SolidColorBrush.
    /// </summary>
    /// <param name="value">The ContentType value to convert.</param>
    /// <param name="targetType">The target type (ignored).</param>
    /// <param name="parameter">Optional parameter (ignored).</param>
    /// <param name="culture">The culture (ignored).</param>
    /// <returns>A SolidColorBrush representing the content type color.</returns>
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is ContentType contentType)
        {
            return contentType switch
            {
                ContentType.GameClient => GameClientBrush,
                ContentType.Mod => ModBrush,
                ContentType.Patch => PatchBrush,
                ContentType.Map or ContentType.MapPack => MapBrush,
                ContentType.Addon => AddonBrush,
                ContentType.ModdingTool or ContentType.Executable => ToolBrush,
                ContentType.ContentBundle => BundleBrush,
                ContentType.Mission => MissionBrush,
                ContentType.Skin or ContentType.LanguagePack => SkinBrush,
                _ => ModBrush,
            };
        }

        return ModBrush;
    }

    /// <summary>
    /// Converts back from a brush to a ContentType (not supported).
    /// </summary>
    /// <param name="value">The value to convert back.</param>
    /// <param name="targetType">The target type.</param>
    /// <param name="parameter">Optional parameter.</param>
    /// <param name="culture">The culture.</param>
    /// <returns><see cref="AvaloniaProperty.UnsetValue"/> as two-way conversion is not supported.</returns>
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return AvaloniaProperty.UnsetValue;
    }
}
