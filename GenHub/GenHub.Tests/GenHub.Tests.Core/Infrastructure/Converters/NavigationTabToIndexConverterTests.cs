using GenHub.Common.Models;
using GenHub.Infrastructure.Converters;
using Xunit;

namespace GenHub.Tests.Core.Infrastructure.Converters;

/// <summary>
/// Unit tests for <see cref="NavigationTabToIndexConverter"/>.
/// </summary>
public class NavigationTabToIndexConverterTests
{
    /// <summary>
    /// Verifies conversion of NavigationTab to integer index.
    /// </summary>
    /// <param name="tab">The NavigationTab value to convert.</param>
    /// <param name="expectedIndex">The expected integer index.</param>
    [Theory]
    [InlineData(NavigationTab.GameProfiles, 0)]
    [InlineData(NavigationTab.Downloads, 1)]
    [InlineData(NavigationTab.Settings, 2)]
    public void Convert_ReturnsCorrectIndex(NavigationTab tab, int expectedIndex)
    {
        var result = NavigationTabToIndexConverter.Instance.Convert(tab, typeof(int), null, null);
        Assert.Equal(expectedIndex, result);
    }

    /// <summary>
    /// Verifies conversion with invalid value returns zero.
    /// </summary>
    [Fact]
    public void Convert_WithInvalidValue_ReturnsZero()
    {
        var result = NavigationTabToIndexConverter.Instance.Convert("invalid", typeof(int), null, null);
        Assert.Equal(0, result);
    }

    /// <summary>
    /// Verifies ConvertBack returns correct NavigationTab for valid indices.
    /// </summary>
    /// <param name="index">The integer index to convert back.</param>
    /// <param name="expectedTab">The expected NavigationTab.</param>
    [Theory]
    [InlineData(0, NavigationTab.GameProfiles)]
    [InlineData(1, NavigationTab.Downloads)]
    [InlineData(2, NavigationTab.Settings)]
    public void ConvertBack_ReturnsCorrectTab(int index, NavigationTab expectedTab)
    {
        var result = NavigationTabToIndexConverter.Instance.ConvertBack(index, typeof(NavigationTab), null, null);
        Assert.Equal(expectedTab, result);
    }

    /// <summary>
    /// Verifies ConvertBack returns GameProfiles for invalid index.
    /// </summary>
    [Fact]
    public void ConvertBack_WithInvalidIndex_ReturnsGameProfiles()
    {
        var result = NavigationTabToIndexConverter.Instance.ConvertBack(999, typeof(NavigationTab), null, null);
        Assert.Equal(NavigationTab.GameProfiles, result);
    }
}
