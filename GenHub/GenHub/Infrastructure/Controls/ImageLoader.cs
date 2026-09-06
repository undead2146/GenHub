using System;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using GenHub.Infrastructure.Services;

namespace GenHub.Infrastructure.Controls;

/// <summary>
/// Provides attached properties for loading remote and local images asynchronously into an <see cref="Image"/> control.
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
    public static readonly AttachedProperty<Bitmap?> PlaceholderProperty =
        AvaloniaProperty.RegisterAttached<Image, Bitmap?>("Placeholder", typeof(ImageLoader));

    private static readonly AttachedProperty<CancellationTokenSource?> CurrentCtsProperty =
        AvaloniaProperty.RegisterAttached<Image, CancellationTokenSource?>("CurrentCts", typeof(ImageLoader));

    static ImageLoader()
    {
        SourceProperty.Changed.AddClassHandler<Image>(OnSourceChanged);
    }

    /// <summary>
    /// Gets the image source URL or file path.
    /// </summary>
    /// <param name="element">The target image element.</param>
    /// <returns>The source string.</returns>
    public static string? GetSource(Image element)
    {
        ArgumentNullException.ThrowIfNull(element);
        return element.GetValue(SourceProperty);
    }

    /// <summary>
    /// Sets the image source URL or file path.
    /// </summary>
    /// <param name="element">The target image element.</param>
    /// <param name="value">The source string to load.</param>
    public static void SetSource(Image element, string? value)
    {
        ArgumentNullException.ThrowIfNull(element);
        element.SetValue(SourceProperty, value);
    }

    /// <summary>
    /// Gets the placeholder bitmap displayed while loading.
    /// </summary>
    /// <param name="element">The target image element.</param>
    /// <returns>The placeholder bitmap.</returns>
    public static Bitmap? GetPlaceholder(Image element)
    {
        ArgumentNullException.ThrowIfNull(element);
        return element.GetValue(PlaceholderProperty);
    }

    /// <summary>
    /// Sets the placeholder bitmap displayed while loading.
    /// </summary>
    /// <param name="element">The target image element.</param>
    /// <param name="value">The placeholder bitmap.</param>
    public static void SetPlaceholder(Image element, Bitmap? value)
    {
        ArgumentNullException.ThrowIfNull(element);
        element.SetValue(PlaceholderProperty, value);
    }

    private static void OnSourceChanged(Image image, AvaloniaPropertyChangedEventArgs e)
    {
        image.AttachedToVisualTree -= OnAttachedToVisualTree;
        image.DetachedFromVisualTree -= OnDetachedFromVisualTree;

        CancelActiveLoad(image);

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
        image.DetachedFromVisualTree += OnDetachedFromVisualTree;

        var cts = new CancellationTokenSource();
        image.SetValue(CurrentCtsProperty, cts);
        _ = ApplySourceAsync(image, url, cts.Token);
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

        CancelActiveLoad(image);
        var cts = new CancellationTokenSource();
        image.SetValue(CurrentCtsProperty, cts);
        _ = ApplySourceAsync(image, url, cts.Token);
    }

    private static void OnDetachedFromVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        if (sender is Image image)
        {
            CancelActiveLoad(image);
        }
    }

    private static void CancelActiveLoad(Image image)
    {
        var existingCts = image.GetValue(CurrentCtsProperty);
        if (existingCts != null)
        {
            try
            {
                existingCts.Cancel();
                existingCts.Dispose();
            }
            catch (ObjectDisposedException)
            {
                // Ignore if already disposed concurrently.
            }

            image.ClearValue(CurrentCtsProperty);
        }
    }

    private static async Task ApplySourceAsync(Image image, string url, CancellationToken cancellationToken)
    {
        try
        {
            var bitmap = ImageCacheService.Instance.GetBitmapFromMemory(url)
                ?? await ImageCacheService.Instance.GetBitmapAsync(url, cancellationToken);

            void UpdateSource()
            {
                if (cancellationToken.IsCancellationRequested || GetSource(image) != url)
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
        catch (OperationCanceledException)
        {
            // Expected when the load is cancelled by a subsequent Source change or detachment.
        }
        catch (Exception ex)
        {
            // Suppress unexpected background load failures to prevent unhandled task exceptions.
            System.Diagnostics.Debug.WriteLine($"[ImageLoader] Failed to load image from {url}: {ex}");
        }
        finally
        {
            void ClearCts()
            {
                var currentCts = image.GetValue(CurrentCtsProperty);
                if (currentCts == null)
                {
                    return;
                }

                try
                {
                    if (currentCts.Token == cancellationToken)
                    {
                        image.ClearValue(CurrentCtsProperty);
                        currentCts.Dispose();
                    }
                }
                catch (ObjectDisposedException)
                {
                    // Ignore if already disposed concurrently.
                }
            }

            if (Dispatcher.UIThread.CheckAccess())
            {
                ClearCts();
            }
            else
            {
                await Dispatcher.UIThread.InvokeAsync(ClearCts);
            }
        }
    }

    private static void InvalidateImage(Image image)
    {
        image.InvalidateMeasure();
        image.InvalidateArrange();
        image.InvalidateVisual();
    }
}
