using GenHub.Core.Constants;
using GenHub.Core.Interfaces.GameReplays;
using GenHub.Core.Models.GameReplays;
using GenHub.Core.Models.Results;
using HtmlAgilityPack;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace GenHub.Core.Services.GameReplays;

/// <summary>
/// Parser implementation for GameReplays HTML content.
/// Extracts tournament and forum post data from HTML.
/// </summary>
public class GameReplaysParser(ILogger<GameReplaysParser> logger) : IGameReplaysParser
{
    private readonly ILogger<GameReplaysParser> _logger = logger;

    /// <inheritdoc/>
    public OperationResult<GameReplaysTournaments> ParseTournamentBoard(string html)
    {
        try
        {
            _logger.LogDebug("Parsing tournament board HTML");

            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            var result = new GameReplaysTournaments();

            // Find main post content
            var mainPost = doc.DocumentNode.SelectSingleNode("//div[contains(@class, 'post')]/div[@class='comment_wrapper']/div[@class='comment']/div[@class='comment_display_content']");

            if (mainPost == null)
            {
                return OperationResult<GameReplaysTournaments>.CreateFailure("Could not find main tournament post");
            }

            // Linear scan state machine
            var currentStatus = TournamentStatus.Finished; // Default bucket
            var foundAny = false;

            // Use Descendants to flatten the tree safely
            // This grabs ALL nodes in the tree in order of appearance (opening tag)
            var allNodes = mainPost.Descendants();

            foreach (var node in allNodes)
            {
                // Only care about Text nodes for headers
                if (node.NodeType == HtmlNodeType.Text)
                {
                   var text = node.InnerText.Trim();
                   if (string.IsNullOrWhiteSpace(text)) continue;

                    // Detect Section Headers
                    if (IsHeader(text, "Signups Open"))
                    {
                        currentStatus = TournamentStatus.SignupsOpen;
                        continue;
                    }
                    if (IsHeader(text, "Upcoming"))
                    {
                        currentStatus = TournamentStatus.Upcoming;
                        continue;
                    }
                    if (IsHeader(text, "Active"))
                    {
                        currentStatus = TournamentStatus.Active;
                        continue;
                    }
                    // Handle "Finished/Previous" and specifically "Finished"
                    if (IsHeader(text, "Finished") || IsHeader(text, "Previous"))
                    {
                        currentStatus = TournamentStatus.Finished;
                        continue;
                    }
                }

                // Parse Links (Element nodes)
                if (node.Name == "a")
                {
                    var href = node.GetAttributeValue("href", string.Empty);

                    // Basic validation that it's a topic link
                    if (Regex.IsMatch(href, @"showtopic=\d+"))
                    {
                        var tournament = ParseTournamentLink(node, currentStatus);
                        if (tournament != null)
                        {
                            foundAny = true;
                            // Add to list based on current status
                            switch (currentStatus)
                            {
                                case TournamentStatus.SignupsOpen:
                                    ((List<TournamentModel>)result.SignupsOpen).Add(tournament);
                                    break;
                                case TournamentStatus.Upcoming:
                                    ((List<TournamentModel>)result.Upcoming).Add(tournament);
                                    break;
                                case TournamentStatus.Active:
                                    ((List<TournamentModel>)result.Active).Add(tournament);
                                    break;
                                case TournamentStatus.Finished:
                                    ((List<TournamentModel>)result.Finished).Add(tournament);
                                    break;
                            }
                        }
                    }
                }
            }

            if (!foundAny)
            {
                 _logger.LogWarning("No tournaments found using linear scan.");
            }

            _logger.LogDebug(
                "Parsed tournament board: {SignupsOpen} signups open, {Upcoming} upcoming, {Active} active, {Finished} finished",
                result.SignupsOpen.Count(),
                result.Upcoming.Count(),
                result.Active.Count(),
                result.Finished.Count());

            return OperationResult<GameReplaysTournaments>.CreateSuccess(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing tournament board HTML");
            return OperationResult<GameReplaysTournaments>.CreateFailure($"Parse error: {ex.Message}");
        }
    }

    private bool IsHeader(string text, string headerKeyword)
    {
        return text.Contains(headerKeyword, StringComparison.OrdinalIgnoreCase);
    }

    private TournamentModel? ParseTournamentLink(HtmlNode linkNode, TournamentStatus status)
    {
        try
        {
            var href = linkNode.GetAttributeValue("href", string.Empty);
            var name = linkNode.InnerText.Trim();

            if (string.IsNullOrWhiteSpace(name)) return null;

            // Extract topic ID from URL
            var topicIdMatch = Regex.Match(href, @"showtopic=(\d+)");
            var topicId = topicIdMatch.Success ? topicIdMatch.Groups[1].Value : string.Empty;

            // Get host name from text (e.g., "hosted by AkRaMjOn")
            var host = ExtractHostName(name);

            // Get dates from nearby italic text
            // We search siblings or parent's siblings for the date info
            var dateInfo = FindDateInfo(linkNode);
            DateTime? signupsCloseDate = null;
            DateTime? startDate = null;

            if (!string.IsNullOrEmpty(dateInfo))
            {
                if (dateInfo.Contains("Signups close", StringComparison.OrdinalIgnoreCase))
                {
                    signupsCloseDate = ParseDateFromText(dateInfo);
                }

                if (dateInfo.Contains("Started", StringComparison.OrdinalIgnoreCase))
                {
                    startDate = ParseDateFromText(dateInfo);
                }

                // Sometimes it just says "May 12th 2025" without prefix for finished
                if (status == TournamentStatus.Finished && startDate == null)
                {
                     startDate = ParseDateFromText(dateInfo);
                }
            }

            return new TournamentModel
            {
                TopicId = topicId,
                Name = name.Replace(" hosted by " + host, string.Empty, StringComparison.OrdinalIgnoreCase).Trim(),
                Host = host,
                Url = href.StartsWith("/") ? GameReplaysConstants.BaseUrl + href : href,
                Status = status,
                SignupsCloseDate = signupsCloseDate,
                StartDate = startDate,
            };
        }
        catch
        {
            return null;
        }
    }

    private string FindDateInfo(HtmlNode linkNode)
    {
        // Strategy 1: Next sibling <i> tag (common structure)
        var nextNode = linkNode.NextSibling;
        while (nextNode != null)
        {
            if (nextNode.Name == "i" || (nextNode.Name == "span" && nextNode.InnerText.Contains("Started")))
            {
                return nextNode.InnerText;
            }
            // Stop if we hit a newline or another link
            if (nextNode.Name == "br" || nextNode.Name == "a") break;

            nextNode = nextNode.NextSibling;
        }

        // Strategy 2: Parent's text (if link is inside li or span)
        if (linkNode.ParentNode != null)
        {
           var parentText = linkNode.ParentNode.InnerText;
           if (parentText.Contains("(")) // Dates are often in brackets
           {
               return parentText;
           }
        }

        return string.Empty;
    }


    /// <inheritdoc/>
    public OperationResult<TournamentModel> ParseTournamentTopic(string html)
    {
        try
        {
            _logger.LogDebug("Parsing tournament topic HTML");

            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            var titleNode = doc.DocumentNode.SelectSingleNode("//h2");
            var title = titleNode?.InnerText.Trim() ?? "Unknown Tournament";

            var status = ExtractTournamentStatus(html);
            var (signupsCloseDate, startDate) = ExtractTournamentDates(html);

            var tournament = new TournamentModel
            {
                Name = title,
                Status = status,
                SignupsCloseDate = signupsCloseDate,
                StartDate = startDate,
                Description = FormatHtmlForDisplay(html),
            };

            return OperationResult<TournamentModel>.CreateSuccess(tournament);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing tournament topic HTML");
            return OperationResult<TournamentModel>.CreateFailure($"Parse error: {ex.Message}");
        }
    }

    /// <inheritdoc/>
    public OperationResult<IEnumerable<ForumPostModel>> ParseForumPosts(string html)
    {
        try
        {
            _logger.LogDebug("Parsing forum posts HTML");

            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            var posts = new List<ForumPostModel>();
            var postNodes = doc.DocumentNode.SelectNodes("//div[contains(@class, 'post')]");

            if (postNodes == null)
            {
                return OperationResult<IEnumerable<ForumPostModel>>.CreateSuccess(posts);
            }

            foreach (var postNode in postNodes)
            {
                var post = ParsePostNode(postNode);
                if (post != null)
                {
                    posts.Add(post);
                }
            }

            _logger.LogDebug("Parsed {Count} forum posts", posts.Count);

            return OperationResult<IEnumerable<ForumPostModel>>.CreateSuccess(posts);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing forum posts HTML");
            return OperationResult<IEnumerable<ForumPostModel>>.CreateFailure($"Parse error: {ex.Message}");
        }
    }

    /// <inheritdoc/>
    public OperationResult<OAuthTokenResponse> ParseOAuthTokenResponse(string json)
    {
        try
        {
            _logger.LogDebug("Parsing OAuth token response JSON");

            var options = new System.Text.Json.JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
            };

            var tokenResponse = System.Text.Json.JsonSerializer.Deserialize<OAuthTokenResponse>(json, options);

            if (tokenResponse == null)
            {
                return OperationResult<OAuthTokenResponse>.CreateFailure("Failed to deserialize token response");
            }

            return OperationResult<OAuthTokenResponse>.CreateSuccess(tokenResponse);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing OAuth token response JSON");
            return OperationResult<OAuthTokenResponse>.CreateFailure($"Parse error: {ex.Message}");
        }
    }

    /// <inheritdoc/>
    public OperationResult<OAuthUserInfo> ParseOAuthUserInfo(string json)
    {
        try
        {
            _logger.LogDebug("Parsing OAuth user info JSON");

            var options = new System.Text.Json.JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
            };

            var userInfo = System.Text.Json.JsonSerializer.Deserialize<OAuthUserInfo>(json, options);

            if (userInfo == null)
            {
                return OperationResult<OAuthUserInfo>.CreateFailure("Failed to deserialize user info");
            }

            return OperationResult<OAuthUserInfo>.CreateSuccess(userInfo);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing OAuth user info JSON");
            return OperationResult<OAuthUserInfo>.CreateFailure($"Parse error: {ex.Message}");
        }
    }

    /// <inheritdoc/>
    public TournamentStatus ExtractTournamentStatus(string html)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        // Check for status indicators in main post
        var mainPost = doc.DocumentNode.SelectSingleNode("//div[contains(@class, 'post')]/div[@class='comment_wrapper']/div[@class='comment']/div[@class='comment_display_content']");

        if (mainPost == null)
        {
            return TournamentStatus.Finished;
        }

        // Check for signups open (orange)
        if (FindSectionByColor(mainPost, "orange") != null)
        {
            return TournamentStatus.SignupsOpen;
        }

        // Check for upcoming (red)
        if (FindSectionByColor(mainPost, "red") != null)
        {
            return TournamentStatus.Upcoming;
        }

        // Check for active (green)
        if (FindSectionByColor(mainPost, "green") != null)
        {
            return TournamentStatus.Active;
        }

        // Default to finished
        return TournamentStatus.Finished;
    }

    /// <inheritdoc/>
    public (DateTime? SignupsCloseDate, DateTime? StartDate) ExtractTournamentDates(string html)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        var mainPost = doc.DocumentNode.SelectSingleNode("//div[contains(@class, 'post')]/div[@class='comment_wrapper']/div[@class='comment']/div[@class='comment_display_content']");

        if (mainPost == null)
        {
            return (null, null);
        }

        DateTime? signupsCloseDate = null;
        DateTime? startDate = null;

        // Look for date patterns in italic text
        var italicNodes = mainPost.SelectNodes(".//i");
        if (italicNodes != null)
        {
            foreach (var italicNode in italicNodes)
            {
                var text = italicNode.InnerText;

                // Try to parse "Signups close: {date}" pattern
                if (text.Contains("Signups close", StringComparison.OrdinalIgnoreCase))
                {
                    signupsCloseDate = ParseDateFromText(text);
                }

                // Try to parse "Started: {date}" pattern
                if (text.Contains("Started", StringComparison.OrdinalIgnoreCase))
                {
                    startDate = ParseDateFromText(text);
                }
            }
        }

        return (signupsCloseDate, startDate);
    }

    /// <inheritdoc/>
    public string FormatHtmlForDisplay(string html)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        // Remove unwanted elements
        var nodesToRemove = doc.DocumentNode.SelectNodes(
            "//script | //style | //iframe | //noscript | " +
            "//div[contains(@class, 'signature')] | " +
            "//div[contains(@class, 'post_buttons_primary')] | " +
            "//div[contains(@class, 'comment_authorinfo')]");

        if (nodesToRemove != null)
        {
            foreach (var node in nodesToRemove)
            {
                node.Remove();
            }
        }

        // Clean up attributes
        var allNodes = doc.DocumentNode.SelectNodes(".//*");
        if (allNodes != null)
        {
            foreach (var node in allNodes)
            {
                // Keep only safe attributes
                var attributesToRemove = node.Attributes
                    .Where(a => !IsSafeAttribute(a.Name))
                    .ToList();

                foreach (var attr in attributesToRemove)
                {
                    node.Attributes.Remove(attr);
                }
            }
        }

        return doc.DocumentNode.OuterHtml;
    }

    /// <inheritdoc/>
    public string ExtractPlainText(string html)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        // Remove script and style elements
        var nodesToRemove = doc.DocumentNode.SelectNodes("//script | //style");
        if (nodesToRemove != null)
        {
            foreach (var node in nodesToRemove)
            {
                node.Remove();
            }
        }

        return doc.DocumentNode.InnerText.Trim();
    }



    private ForumPostModel? ParsePostNode(HtmlNode postNode)
    {
        try
        {
            var commentWrapper = postNode.SelectSingleNode(".//div[@class='comment_wrapper']");
            if (commentWrapper == null)
            {
                return null;
            }

            var comment = commentWrapper.SelectSingleNode(".//div[@class='comment']");
            if (comment == null)
            {
                return null;
            }

            var header = comment.SelectSingleNode(".//div[@class='comment_header']");
            var content = comment.SelectSingleNode(".//div[@class='comment_display_content']");

            if (header == null || content == null)
            {
                return null;
            }

            // Extract post number
            var postNumberNode = header.SelectSingleNode(".//a[@class='post_number']");
            var postId = postNumberNode?.GetAttributeValue("name", string.Empty) ?? string.Empty;

            // Extract author
            var authorNode = header.SelectSingleNode(".//span[@class='member_name']/a");
            var authorId = authorNode?.GetAttributeValue("href", string.Empty) ?? string.Empty;
            var authorName = authorNode?.InnerText.Trim() ?? "Unknown";

            // Extract author ID from URL
            var authorIdMatch = Regex.Match(authorId, @"showuser=(\d+)");
            var authorIdValue = authorIdMatch.Success ? authorIdMatch.Groups[1].Value : string.Empty;

            // Extract post date
            var dateText = header.InnerText;
            var postedAt = ParsePostDate(dateText);

            // Extract content HTML
            var contentHtml = content.InnerHtml;

            // Check if edited
            var editedNode = content.SelectSingleNode(".//span[@class='edited']");
            bool isEdited = editedNode != null;
            DateTime? editedAt = null;

            if (isEdited && editedNode != null)
            {
                var editedText = editedNode.InnerText;
                editedAt = ParseEditDate(editedText);
            }

            return new ForumPostModel
            {
                PostId = postId,
                TopicId = string.Empty, // Will be set by caller
                Author = authorIdValue,
                AuthorDisplayName = authorName,
                PostedAt = postedAt,
                ContentHtml = contentHtml,
                Comments = new List<CommentModel>(),
                IsEdited = isEdited,
                EditedAt = editedAt,
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error parsing post node");
            return null;
        }
    }

    private string ExtractHostName(string text)
    {
        var match = Regex.Match(text, @"hosted by (.+)", RegexOptions.IgnoreCase);
        return match.Success ? match.Groups[1].Value.Trim() : "Unknown";
    }

    private DateTime? ParseDateFromText(string text)
    {
        // Try various date formats
        var formats = new[]
        {
            "MMMM d, yyyy",
            "MMM d, yyyy",
            "MMMM d yyyy",
            "d MMMM yyyy",
            "dd MMMM yyyy"
        };

        // Extract date from text
        var dateMatch = Regex.Match(text, @"(\w+ \d{1,2}(?:st|nd|rd|th)?,? \d{4})");
        if (dateMatch.Success)
        {
            var dateStr = dateMatch.Groups[1].Value;

            // Remove ordinal suffixes
            dateStr = Regex.Replace(dateStr, @"(\d+)(st|nd|rd|th)", "$1");
            dateStr = dateStr.Replace(",", ""); // Remove commas for consistent parsing

            foreach (var format in formats)
            {
                // Try to parse with spaces condensed
                if (DateTime.TryParse(dateStr, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
                {
                    return date;
                }

                 if (DateTime.TryParseExact(dateStr, format, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dateExact))
                {
                    return dateExact;
                }
            }
        }

        return null;
    }

    private DateTime ParsePostDate(string text)
    {
        // Format: "Dec 30 2022, 09:03 AM"
        var match = Regex.Match(text, @"(\w{3} \d{1,2} \d{4}, \d{1,2}:\d{2} [AP]M)");
        if (match.Success)
        {
            if (DateTime.TryParseExact(match.Value, "MMM dd yyyy, hh:mm tt", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            {
                return date;
            }
        }

        return DateTime.UtcNow;
    }

    private DateTime? ParseEditDate(string text)
    {
        // Format: "This post has been edited by <b>Name</b>: Jan 11 2023, 14:00 PM"
        var match = Regex.Match(text, @": (\w{3} \d{1,2} \d{4}, \d{1,2}:\d{2} [AP]M)");
        if (match.Success)
        {
            if (DateTime.TryParseExact(match.Value, ": MMM dd yyyy, hh:mm tt", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            {
                return date;
            }
        }

        return null;
    }

    private bool IsSafeAttribute(string attributeName)
    {
        var safeAttributes = new[]
        {
            "href", "src", "alt", "title", "class", "id", "style",
        };

        return safeAttributes.Contains(attributeName.ToLowerInvariant());
    }

    private HtmlNode? FindSectionByColor(HtmlNode parentNode, string color)
    {
        // Look for span with specific color
        var colorSpan = parentNode.SelectSingleNode($".//span[@style='color:{color}']");
        if (colorSpan == null)
        {
            return null;
        }

        // Get parent ul element
        var ulNode = colorSpan.ParentNode?.ParentNode;
        return ulNode?.Name == "ul" ? ulNode : null;
    }
}
