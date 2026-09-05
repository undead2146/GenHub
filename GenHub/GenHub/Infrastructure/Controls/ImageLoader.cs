using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using GenHub.Infrastructure.Services;

namespace GenHub.Infrastructure.Controls;

/// <summary>
/// Attached property for asynchronously loading and caching image URLs onto Avalonia Image controls.
/// </summary>
public static class ImageLoader
{
    /// <summary>
    /// Identifies the Source attached property.
    /// </summary>
    public static readonly AttachedProperty<string?> SourceProperty =
        AvaloniaProperty.RegisterAttached<Image, string?>("Source", typeof(ImageLoader));

    /// <summary>
    /// Identifies the Placeholder attached property.
    /// </summary>
    public static readonly AttachedProperty<IImage?> PlaceholderProperty =
        AvaloniaProperty.RegisterAttached<Image, IImage?>("Placeholder", typeof(ImageLoader));

    static ImageLoader()
    {
        SourceProperty.Changed.AddClassHandler<Image>(OnSourceChanged);
    }

    /// <summary>
    /// Gets the Source property value.
    /// </summary>
    /// <param name="element">The Image control.</param>
    /// <returns>The string image URL or path.</returns>
    public static string? GetSource(Image element) => element.GetValue(SourceProperty);

    /// <summary>
    /// Sets the Source property value.
    /// </summary>
    /// <param name="element">The Image control.</param>
    /// <param name="value">The string image URL or path.</param>
    public static void SetSource(Image element, string? value) => element.SetValue(SourceProperty, value);

    /// <summary>
    /// Gets the Placeholder property value.
    /// </summary>
    /// <param name="element">The Image control.</param>
    /// <returns>The placeholder image, or <see langword="null"/>.</returns>
    public static IImage? GetPlaceholder(Image element) => element.GetValue(PlaceholderProperty);

    /// <summary>
    /// Sets the Placeholder property value.
    /// </summary>
    /// <param name="element">The Image control.</param>
    /// <param name="value">The placeholder image.</param>
    public static void SetPlaceholder(Image element, IImage? value) => element.SetValue(PlaceholderProperty, value);

    private static void OnSourceChanged(Image image, AvaloniaPropertyChangedEventArgs e)
    {
        image.AttachedToVisualTree -= OnAttachedToVisualTree;

        // Clear previous source immediately to avoid displaying stale thumbnails during virtual scrolling or loading
        var placeholder = GetPlaceholder(image);
        image.Source = placeholder;
        InvalidateImage(image);

        var url = e.NewValue as string;
        if (string.IsNullOrWhiteSpace(url))
        {
            return;
        }

        image.AttachedToVisualTree += OnAttachedToVisualTree;
        _ = ApplySourceAsync(image, url);
    }

    private static void OnAttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        if (sender is not Image image)
        {
            return;
        }

        var url = GetSource(image);
        if (string.IsNullOrWhiteSpace(url))
        {
            return;
        }

        var placeholder = GetPlaceholder(image);
        if (image.Source != null && !ReferenceEquals(image.Source, placeholder))
        {
            InvalidateImage(image);
            return;
        }

        _ = ApplySourceAsync(image, url);
    }

    private static async Task ApplySourceAsync(Image image, string url)
    {
        var bitmap = ImageCacheService.Instance.GetBitmapFromMemory(url)
            ?? await ImageCacheService.Instance.GetBitmapAsync(url);

        void UpdateSource()
        {
            if (GetSource(image) != url)
            {
                return;
            }

            image.Source = bitmap ?? GetPlaceholder(image);
            InvalidateImage(image);
        }

        if (Dispatcher.UIThread.CheckAccess())
        {
            UpdateSource();
        }
        else
        {
            await Dispatcher.UIThread.InvokeAsync(UpdateSource);
        }
    }

    private static void InvalidateImage(Image image)
    {
        image.InvalidateMeasure();
        image.InvalidateArrange();
        image.InvalidateVisual();
    }
}
