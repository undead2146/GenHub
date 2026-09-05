using System;
using System.Globalization;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using GenHub.Core.Constants;
using GenHub.Core.Models.Enums;
using GenHub.Infrastructure.Converters;
using Xunit;

namespace GenHub.Tests.Core.Infrastructure.Converters;

/// <summary>
/// Unit tests for <see cref="ContentStateToBrushConverter"/>, <see cref="ContentStateToPathDataConverter"/>,
/// and <see cref="ContentStateToTextConverter"/>.
/// </summary>
public class ContentStateConverterTests
{
    private readonly CultureInfo _culture = CultureInfo.InvariantCulture;

    /// <summary>
    /// Verifies that <see cref="ContentStateToBrushConverter"/> returns the expected brush for each content state.
    /// </summary>
    [AvaloniaFact]
    public void ContentStateToBrushConverter_ReturnsExpectedBrush()
    {
        var converter = new ContentStateToBrushConverter();
        foreach (var state in new[] { ContentState.Downloaded, ContentState.UpdateAvailable, ContentState.NotDownloaded })
        {
            var result = converter.Convert(state, typeof(IBrush), null, _culture);
            Assert.IsAssignableFrom<IBrush>(result);
            Assert.Throws<NotSupportedException>(() => converter.ConvertBack(result, typeof(ContentState), null, _culture));
        }
    }

    /// <summary>
    /// Verifies that <see cref="ContentStateToPathDataConverter"/> returns the expected SVG path for each content state.
    /// </summary>
    /// <param name="state">The content state to convert.</param>
    /// <param name="expectedPath">The expected SVG path string.</param>
    [Theory]
    [InlineData(ContentState.Downloaded, UiConstants.TransparentCheckmarkIconPath)]
    [InlineData(ContentState.NotDownloaded, UiConstants.DownloadArrowIconPath)]
    [InlineData(ContentState.UpdateAvailable, UiConstants.UpdateSyncIconPath)]
    public void ContentStateToPathDataConverter_ReturnsExpectedPathData(ContentState state, string expectedPath)
    {
        var converter = new ContentStateToPathDataConverter();
        var result = converter.Convert(state, typeof(string), null, _culture);

        Assert.Equal(expectedPath, result);
        Assert.Throws<NotSupportedException>(() => converter.ConvertBack(result, typeof(ContentState), null, _culture));
    }

    /// <summary>
    /// Verifies that <see cref="ContentStateToTextConverter"/> returns the expected text indicator for each content state.
    /// </summary>
    /// <param name="input">The input value.</param>
    /// <param name="expected">The expected text indicator.</param>
    [Theory]
    [InlineData(ContentState.Downloaded, "✅")]
    [InlineData(ContentState.UpdateAvailable, "🔄")]
    [InlineData(ContentState.NotDownloaded, "⇩")]
    [InlineData(null, "⇩")]
    public void ContentStateToTextConverter_ReturnsExpectedIndicator(object? input, string expected)
    {
        var converter = new ContentStateToTextConverter();
        var result = converter.Convert(input, typeof(string), null, _culture);

        Assert.Equal(expected, result);
        Assert.Throws<NotSupportedException>(() => converter.ConvertBack(result, typeof(ContentState), null, _culture));
    }
}
