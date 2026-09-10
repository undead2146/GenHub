using GenHub.Core.Helpers;
using Xunit;

namespace GenHub.Tests.Core.Helpers;

/// <summary>
/// Unit tests for <see cref="MarkdownLinkFormatter"/>.
/// </summary>
public sealed class MarkdownLinkFormatterTests
{
    /// <summary>
    /// Verifies that null or whitespace input returns empty string.
    /// </summary>
    /// <param name="input">The input string to format.</param>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void FormatLinks_NullOrWhitespace_ReturnsEmptyString(string? input)
    {
        var result = MarkdownLinkFormatter.FormatLinks(input);
        Assert.Equal(string.Empty, result);
    }

    /// <summary>
    /// Verifies that GitHub pull request URLs are converted to compact clickable links.
    /// </summary>
    [Fact]
    public void FormatLinks_GitHubPullRequestUrl_ConvertsToCompactLink()
    {
        var input = "• bugfix: Add missing audio by @Stubbjax in https://github.com/TheSuperHackers/GeneralsGamePatch2/pull/109";
        var result = MarkdownLinkFormatter.FormatLinks(input);

        Assert.Contains("[#109](https://github.com/TheSuperHackers/GeneralsGamePatch2/pull/109)", result);
        Assert.Contains("[@Stubbjax](https://github.com/Stubbjax)", result);
    }

    /// <summary>
    /// Verifies that issue/PR numbers in parentheses are converted when repo is known from sourceUrl.
    /// </summary>
    [Fact]
    public void FormatLinks_ParenthesizedIssue_ConvertsToLink()
    {
        var input = "• ci(release): Fix weekly release workflow permissions (#3256)";
        var sourceUrl = "https://github.com/TheSuperHackers/GeneralsGameCode/releases/tag/weekly-2026-09-05";
        var result = MarkdownLinkFormatter.FormatLinks(input, sourceUrl);

        Assert.Equal("• ci(release): Fix weekly release workflow permissions ([#3256](https://github.com/TheSuperHackers/GeneralsGameCode/pull/3256))", result);
    }

    /// <summary>
    /// Verifies that standalone issue/PR numbers are converted when repo is known.
    /// </summary>
    [Fact]
    public void FormatLinks_StandaloneIssue_ConvertsToLink()
    {
        var input = "Fixed issue #4525 in this release.";
        var sourceUrl = "https://github.com/TheSuperHackers/GeneralsGameCode";
        var result = MarkdownLinkFormatter.FormatLinks(input, sourceUrl);

        Assert.Equal("Fixed issue [#4525](https://github.com/TheSuperHackers/GeneralsGameCode/pull/4525) in this release.", result);
    }

    /// <summary>
    /// Verifies that bare URLs are made clickable markdown links and trailing punctuation is preserved.
    /// </summary>
    [Fact]
    public void FormatLinks_BareUrl_PreservesPunctuationAndLinks()
    {
        var input = "Visit https://generalshub.com for more info.";
        var result = MarkdownLinkFormatter.FormatLinks(input);

        Assert.Equal("Visit [https://generalshub.com](https://generalshub.com) for more info.", result);
    }

    /// <summary>
    /// Verifies that existing markdown links are not mangled or double-wrapped.
    /// </summary>
    [Fact]
    public void FormatLinks_ExistingMarkdownLink_PreservedAsIs()
    {
        var input = "Check out [our website](https://generalshub.com) today.";
        var result = MarkdownLinkFormatter.FormatLinks(input);

        Assert.Equal(input, result);
    }

    /// <summary>
    /// Verifies that email addresses are not turned into GitHub mention links.
    /// </summary>
    [Fact]
    public void FormatLinks_EmailAddress_NotMangled()
    {
        var input = "Contact support at help@example.com for assistance.";
        var result = MarkdownLinkFormatter.FormatLinks(input);

        Assert.DoesNotContain("[@example](https://github.com/example)", result);
    }
}
