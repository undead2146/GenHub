using System;
using System.Text.RegularExpressions;

namespace GenHub.Core.Helpers;

/// <summary>
/// Formats text and markdown to make GitHub PR/issue references, mentions, and raw URLs clickable.
/// </summary>
public static partial class MarkdownLinkFormatter
{
    /// <summary>
    /// Formats the input text into markdown with clickable links for PRs, issues, GitHub users, and bare URLs.
    /// </summary>
    /// <param name="text">The raw text or markdown to format.</param>
    /// <param name="sourceUrl">The optional repository or source URL to resolve relative issue/PR numbers.</param>
    /// <returns>The formatted markdown string.</returns>
    public static string FormatLinks(string? text, string? sourceUrl = null)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var (owner, repo) = ExtractGitHubOwnerRepo(sourceUrl, text);

        var result = text;

        // 1. Transform GitHub pull request / issue URLs into compact clickable links, e.g.
        // in https://github.com/Owner/Repo/pull/109 -> in [#109](https://github.com/Owner/Repo/pull/109)
        result = GitHubPullUrlRegex().Replace(result, m =>
        {
            var url = m.Value;
            var num = m.Groups["num"].Value;
            return $"[#{num}]({url})";
        });

        // 2. Transform GitHub commit URLs into compact clickable links, e.g.
        // https://github.com/Owner/Repo/commit/1234567890 -> [1234567](https://github.com/Owner/Repo/commit/1234567890)
        result = GitHubCommitUrlRegex().Replace(result, m =>
        {
            var url = m.Value;
            var sha = m.Groups["sha"].Value;
            var shortSha = sha.Length > 7 ? sha[..7] : sha;
            return $"[`{shortSha}`]({url})";
        });

        // 3. Transform PR/issue references like (#1234) or #1234 if owner and repo are known
        if (!string.IsNullOrEmpty(owner) && !string.IsNullOrEmpty(repo))
        {
            var baseRepoUrl = $"https://github.com/{owner}/{repo}";

            // Transform (#1234) -> ([#1234](https://github.com/owner/repo/pull/1234))
            result = ParenthesizedIssueRegex().Replace(result, m =>
            {
                var num = m.Groups["num"].Value;
                return $"([#{num}]({baseRepoUrl}/pull/{num}))";
            });

            // Transform standalone #1234 at word boundaries not already part of a link or markdown header
            result = StandaloneIssueRegex().Replace(result, m =>
            {
                var prefix = m.Groups["prefix"].Value;
                var num = m.Groups["num"].Value;
                return $"{prefix}[#{num}]({baseRepoUrl}/pull/{num})";
            });
        }

        // 4. Transform GitHub @username mentions (e.g. "by @Stubbjax") into clickable links
        result = GitHubMentionRegex().Replace(result, m =>
        {
            var prefix = m.Groups["prefix"].Value;
            var user = m.Groups["user"].Value;
            return $"{prefix}[@{user}](https://github.com/{user})";
        });

        // 5. Transform any remaining bare HTTP/HTTPS URLs into clickable markdown links
        result = BareUrlRegex().Replace(result, m =>
        {
            // If already matched by an existing markdown link [text](url), leave untouched
            if (m.Groups["mdlink"].Success)
            {
                return m.Value;
            }

            var url = m.Groups["url"].Value;
            var trimmedLength = url.Length;

            // Trim trailing punctuation like . , ; : ? ! ) ] from the URL
            while (trimmedLength > 0 && ".,;:?!)]".Contains(url[trimmedLength - 1]))
            {
                trimmedLength--;
            }

            if (trimmedLength == url.Length)
            {
                return $"[{url}]({url})";
            }

            var cleanUrl = url[..trimmedLength];
            var trailing = url[trimmedLength..];
            return $"[{cleanUrl}]({cleanUrl}){trailing}";
        });

        return result;
    }

    /// <summary>
    /// Extracts GitHub owner and repository name from a source URL or from text content.
    /// </summary>
    /// <param name="sourceUrl">The source URL to inspect.</param>
    /// <param name="fallbackText">Fallback text to search for a GitHub repository URL if sourceUrl is absent.</param>
    /// <returns>A tuple of (Owner, Repo) if found; otherwise (null, null).</returns>
    public static (string? Owner, string? Repo) ExtractGitHubOwnerRepo(string? sourceUrl, string? fallbackText = null)
    {
        if (!string.IsNullOrWhiteSpace(sourceUrl))
        {
            var match = GitHubRepoUrlRegex().Match(sourceUrl);
            if (match.Success)
            {
                return (match.Groups["owner"].Value, match.Groups["repo"].Value);
            }
        }

        if (!string.IsNullOrWhiteSpace(fallbackText))
        {
            var match = GitHubRepoUrlRegex().Match(fallbackText);
            if (match.Success)
            {
                return (match.Groups["owner"].Value, match.Groups["repo"].Value);
            }
        }

        return (null, null);
    }

    [GeneratedRegex(@"https?://github\.com/(?<owner>[a-zA-Z0-9_\-\.]+)/(?<repo>[a-zA-Z0-9_\-\.]+)(?:/|$|\.git)", RegexOptions.IgnoreCase)]
    private static partial Regex GitHubRepoUrlRegex();

    [GeneratedRegex(@"https?://github\.com/(?<owner>[a-zA-Z0-9_\-\.]+)/(?<repo>[a-zA-Z0-9_\-\.]+)/(?:pull|issues)/(?<num>\d+)", RegexOptions.IgnoreCase)]
    private static partial Regex GitHubPullUrlRegex();

    [GeneratedRegex(@"https?://github\.com/(?<owner>[a-zA-Z0-9_\-\.]+)/(?<repo>[a-zA-Z0-9_\-\.]+)/commit/(?<sha>[a-fA-F0-9]{7,40})", RegexOptions.IgnoreCase)]
    private static partial Regex GitHubCommitUrlRegex();

    [GeneratedRegex(@"\((?:#(?<num>\d+))\)")]
    private static partial Regex ParenthesizedIssueRegex();

    [GeneratedRegex(@"(?<prefix>(?:^|[\s,;]))#(?<num>\d+)\b")]
    private static partial Regex StandaloneIssueRegex();

    [GeneratedRegex(@"(?<prefix>(?:^|[\s(]))@(?<user>[a-zA-Z0-9_\-]+)\b(?!\.)")]
    private static partial Regex GitHubMentionRegex();

    [GeneratedRegex(@"(?<mdlink>\[[^\]]*\]\([^)]*\))|(?<url>https?://[^\s<>""]+)")]
    private static partial Regex BareUrlRegex();
}
