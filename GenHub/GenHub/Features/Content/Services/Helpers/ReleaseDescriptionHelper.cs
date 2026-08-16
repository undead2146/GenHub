using System;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Markdig;

namespace GenHub.Features.Content.Services.Helpers;

/// <summary>
/// Converts raw GitHub release notes (markdown/HTML) into plain text suitable for
/// card and detail display. Strips block tags, ATX headers, images, and link
/// scaffolding while preserving readable sentence text.
/// </summary>
public static class ReleaseDescriptionHelper
{
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .UseAdvancedExtensions()
        .Build();

    // Block-level HTML tags that GitHub release bodies commonly carry.
    private static readonly Regex HtmlBlockTagsRegex = new(
        @"</?(p|div|br|hr|span|a|img|ul|ol|li|h[1-6]|pre|code|blockquote|strong|em|b|i)\b[^>]*>",
        RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant);

    // ATX headers (#### Title) and reference-style links.
    private static readonly Regex HeaderRegex = new(
        @"^#{1,6}\s*",
        RegexOptions.Multiline | RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex ReferenceLinkRegex = new(
        @"^\[[^\]]+\]:\s*\S+.*$",
        RegexOptions.Multiline | RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>
    /// Returns a whitespace-normalized plain-text rendering of the supplied markdown body.
    /// </summary>
    /// <param name="markdown">The raw release body (may be markdown, HTML, or plain text).</param>
    /// <returns>Cleaned text, or an empty string when the body collapses to nothing.</returns>
    public static string ToPlainText(string? markdown)
    {
        if (string.IsNullOrWhiteSpace(markdown))
        {
            return string.Empty;
        }

        var html = Markdig.Markdown.ToHtml(markdown.Trim(), Pipeline);
        var stripped = HtmlBlockTagsRegex.Replace(html, " ");
        stripped = ReferenceLinkRegex.Replace(stripped, string.Empty);
        stripped = HeaderRegex.Replace(stripped, string.Empty);

        return NormalizeWhitespace(stripped);
    }

    /// <summary>
    /// Produces a single-line summary suitable for a download card, collapsing the body
    /// to its first meaningful line and clamping it to <paramref name="maxLength"/>.
    /// </summary>
    /// <param name="markdown">The raw release body.</param>
    /// <param name="maxLength">Maximum character length of the summary.</param>
    /// <returns>A short summary string.</returns>
    public static string ToSummary(string? markdown, int maxLength = 150)
    {
        var plain = ToPlainText(markdown);
        if (plain.Length <= maxLength)
        {
            return plain;
        }

        var clamped = plain[..(maxLength - 3)];
        var lastSpace = clamped.LastIndexOf(' ');
        return (lastSpace > 0 ? clamped[..lastSpace] : clamped) + "...";
    }

    private static string NormalizeWhitespace(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        // Decode the few HTML entities GitHub commonly emits in release bodies.
        var decoded = text
            .Replace("&amp;", "&", StringComparison.OrdinalIgnoreCase)
            .Replace("&lt;", "<", StringComparison.OrdinalIgnoreCase)
            .Replace("&gt;", ">", StringComparison.OrdinalIgnoreCase)
            .Replace("&quot;", "\"", StringComparison.OrdinalIgnoreCase)
            .Replace("&#39;", "'", StringComparison.OrdinalIgnoreCase)
            .Replace("&nbsp;", " ", StringComparison.OrdinalIgnoreCase);

        var sb = new StringBuilder(decoded.Length);
        var previousWasSpace = false;
        foreach (var ch in decoded)
        {
            var isSpace = ch is ' ' or '\t' or '\r' or '\n';
            if (isSpace)
            {
                if (!previousWasSpace)
                {
                    sb.Append(' ');
                }

                previousWasSpace = true;
            }
            else
            {
                sb.Append(ch);
                previousWasSpace = false;
            }
        }

        return sb.ToString().Trim();
    }
}
