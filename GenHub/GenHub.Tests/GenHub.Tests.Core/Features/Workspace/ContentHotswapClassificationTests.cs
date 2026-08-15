using System;
using GenHub.Core.Models.Workspace;
using Xunit;
using ContentType = GenHub.Core.Models.Enums.ContentType;

namespace GenHub.Tests.Core.Features.Workspace;

/// <summary>
/// Contains unit tests for <see cref="ContentHotswapClassification"/>.
/// </summary>
public class ContentHotswapClassificationTests
{
    /// <summary>
    /// Verifies that IsHotswappable returns expected truth values for all content types.
    /// </summary>
    /// <param name="contentType">The content type under test.</param>
    /// <param name="expected">The expected boolean result.</param>
    [Theory]
    [InlineData(ContentType.Map, true)]
    [InlineData(ContentType.MapPack, true)]
    [InlineData(ContentType.Patch, false)]
    [InlineData(ContentType.Replay, true)]
    [InlineData(ContentType.Mod, false)]
    [InlineData(ContentType.GameClient, false)]
    [InlineData(ContentType.GameInstallation, false)]
    [InlineData(ContentType.Addon, false)]
    [InlineData(ContentType.Executable, false)]
    [InlineData(ContentType.ModdingTool, false)]
    [InlineData(ContentType.Mission, false)]
    [InlineData(ContentType.Skin, false)]
    [InlineData(ContentType.LanguagePack, false)]
    [InlineData(ContentType.UnknownContentType, false)]
    public void IsHotswappable_ReturnsExpectedResult(ContentType contentType, bool expected)
    {
        // Act
        var result = ContentHotswapClassification.IsHotswappable(contentType);

        // Assert
        Assert.Equal(expected, result);
    }

    /// <summary>
    /// Verifies that IsLocked returns the exact opposite of IsHotswappable.
    /// </summary>
    /// <param name="contentType">The content type under test.</param>
    /// <param name="expected">The expected boolean result.</param>
    [Theory]
    [InlineData(ContentType.Map, false)]
    [InlineData(ContentType.MapPack, false)]
    [InlineData(ContentType.Patch, true)]
    [InlineData(ContentType.Replay, false)]
    [InlineData(ContentType.Mod, true)]
    [InlineData(ContentType.GameClient, true)]
    [InlineData(ContentType.GameInstallation, true)]
    [InlineData(ContentType.Addon, true)]
    [InlineData(ContentType.Executable, true)]
    [InlineData(ContentType.ModdingTool, true)]
    [InlineData(ContentType.Mission, true)]
    [InlineData(ContentType.Skin, true)]
    [InlineData(ContentType.LanguagePack, true)]
    [InlineData(ContentType.UnknownContentType, true)]
    public void IsLocked_ReturnsOppositeOfIsHotswappable(ContentType contentType, bool expected)
    {
        // Act
        var result = ContentHotswapClassification.IsLocked(contentType);

        // Assert
        Assert.Equal(expected, result);
    }
}
