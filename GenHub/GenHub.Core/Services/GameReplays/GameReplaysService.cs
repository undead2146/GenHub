using GenHub.Core.Constants;
using GenHub.Core.Interfaces.GameReplays;
using GenHub.Core.Models.GameReplays;
using GenHub.Core.Models.Results;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace GenHub.Core.Services.GameReplays;

/// <summary>
/// Main service implementation for GameReplays integration.
/// Provides access to tournaments, authentication, and comments.
/// </summary>
public class GameReplaysService(
    IGameReplaysHttpClient httpClient,
    IGameReplaysParser parser,
    IGameReplaysAuthService authService,
    IGameReplaysCommentService commentService,
    ILogger<GameReplaysService> logger) : IGameReplaysService
{
    private readonly IGameReplaysHttpClient _httpClient = httpClient;
    private readonly IGameReplaysParser _parser = parser;
    private readonly IGameReplaysAuthService _authService = authService;
    private readonly IGameReplaysCommentService _commentService = commentService;
    private readonly ILogger<GameReplaysService> _logger = logger;

    private GameReplaysTournaments? _cachedTournaments;
    private DateTime _cacheExpiry;

    /// <inheritdoc/>

    /// <inheritdoc/>
    public async Task<OperationResult<GameReplaysTournaments>> GetTournamentsAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Fetching tournaments from tournament board");

            // Check cache
            if (_cachedTournaments != null && DateTime.UtcNow < _cacheExpiry)
            {
                _logger.LogDebug("Returning cached tournaments");
                return OperationResult<GameReplaysTournaments>.CreateSuccess(_cachedTournaments);
            }

            // Fetch tournament board HTML
            var url = $"{GameReplaysConstants.BaseUrl}/community/index.php?showtopic={GameReplaysConstants.TournamentBoardTopicId}";
            var htmlResult = await _httpClient.GetHtmlAsync(url, cancellationToken);

            if (!htmlResult.Success)
            {
                return OperationResult<GameReplaysTournaments>.CreateFailure(htmlResult.FirstError ?? "Failed to fetch tournament board");
            }

            // Parse tournaments
            var parseResult = _parser.ParseTournamentBoard(htmlResult.Data);

            if (!parseResult.Success)
            {
                return OperationResult<GameReplaysTournaments>.CreateFailure(parseResult.FirstError ?? "Failed to parse tournament board");
            }

            // Update cache
            _cachedTournaments = parseResult.Data;
            _cacheExpiry = DateTime.UtcNow.AddMinutes(GameReplaysConstants.CacheDurationMinutes);

            _logger.LogDebug(
                "Successfully fetched tournaments: {SignupsOpen} signups open, {Upcoming} upcoming, {Active} active, {Finished} finished",
                parseResult.Data.SignupsOpen.Count(),
                parseResult.Data.Upcoming.Count(),
                parseResult.Data.Active.Count(),
                parseResult.Data.Finished.Count());

            return OperationResult<GameReplaysTournaments>.CreateSuccess(parseResult.Data);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching tournaments");
            return OperationResult<GameReplaysTournaments>.CreateFailure($"Error: {ex.Message}");
        }
    }

    /// <inheritdoc/>
    public async Task<OperationResult<IEnumerable<TournamentModel>>> GetTournamentsByStatusAsync(
        TournamentStatus status,
        CancellationToken cancellationToken = default)
    {
        var tournamentsResult = await GetTournamentsAsync(cancellationToken);

        if (!tournamentsResult.Success)
        {
            return OperationResult<IEnumerable<TournamentModel>>.CreateFailure(tournamentsResult.FirstError ?? "Failed to fetch tournaments");
        }

        var tournaments = status switch
        {
            TournamentStatus.SignupsOpen => tournamentsResult.Data.SignupsOpen,
            TournamentStatus.Upcoming => tournamentsResult.Data.Upcoming,
            TournamentStatus.Active => tournamentsResult.Data.Active,
            TournamentStatus.Finished => tournamentsResult.Data.Finished,
            _ => Enumerable.Empty<TournamentModel>(),
        };

        return OperationResult<IEnumerable<TournamentModel>>.CreateSuccess(tournaments);
    }

    /// <inheritdoc/>
    public async Task<OperationResult<TournamentModel>> GetTournamentAsync(
        string topicId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Fetching tournament details for topic: {TopicId}", topicId);

            var url = $"{GameReplaysConstants.BaseUrl}/community/index.php?showtopic={topicId}";
            var htmlResult = await _httpClient.GetHtmlAsync(url, cancellationToken);

            if (!htmlResult.Success)
            {
                return OperationResult<TournamentModel>.CreateFailure(htmlResult.FirstError ?? "Failed to fetch tournament");
            }

            var parseResult = _parser.ParseTournamentTopic(htmlResult.Data);

            if (!parseResult.Success)
            {
                return OperationResult<TournamentModel>.CreateFailure(parseResult.FirstError ?? "Failed to parse tournament");
            }

            var tournament = parseResult.Data;
            tournament.TopicId = topicId;

            return OperationResult<TournamentModel>.CreateSuccess(tournament);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching tournament details");
            return OperationResult<TournamentModel>.CreateFailure($"Error: {ex.Message}");
        }
    }

    /// <inheritdoc/>
    public async Task<OperationResult<IEnumerable<ForumPostModel>>> GetTopicPostsAsync(
        string topicId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Fetching posts for topic: {TopicId}", topicId);

            var url = $"{GameReplaysConstants.BaseUrl}/community/index.php?showtopic={topicId}";
            var htmlResult = await _httpClient.GetHtmlAsync(url, cancellationToken);

            if (!htmlResult.Success)
            {
                return OperationResult<IEnumerable<ForumPostModel>>.CreateFailure(htmlResult.FirstError ?? "Failed to fetch topic posts");
            }

            var parseResult = _parser.ParseForumPosts(htmlResult.Data);

            if (!parseResult.Success)
            {
                return OperationResult<IEnumerable<ForumPostModel>>.CreateFailure(parseResult.FirstError ?? "Failed to parse topic posts");
            }

            // Set topic ID for all posts
            foreach (var post in parseResult.Data)
            {
                post.TopicId = topicId;
            }

            return OperationResult<IEnumerable<ForumPostModel>>.CreateSuccess(parseResult.Data);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching topic posts");
            return OperationResult<IEnumerable<ForumPostModel>>.CreateFailure($"Error: {ex.Message}");
        }
    }

    /// <inheritdoc/>
    public bool IsAuthenticated()
    {
        return _authService.GetAccessToken() != null;
    }

    /// <inheritdoc/>
    public OperationResult<OAuthUserInfo> GetCurrentUser()
    {
        var user = _authService.GetCurrentUser();

        if (user == null)
        {
            return OperationResult<OAuthUserInfo>.CreateFailure("Not authenticated");
        }

        return OperationResult<OAuthUserInfo>.CreateSuccess(user);
    }

    /// <inheritdoc/>
    public async Task<OperationResult<string>> InitiateLoginAsync()
    {
        try
        {
            _logger.LogDebug("Initiating OAuth login flow");

            var authUrlResult = _authService.GetAuthorizationUrl();

            if (!authUrlResult.Success)
            {
                return OperationResult<string>.CreateFailure(authUrlResult.FirstError ?? "Failed to generate authorization URL");
            }

            await Task.CompletedTask;
            return OperationResult<string>.CreateSuccess(authUrlResult.Data);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error initiating login");
            return OperationResult<string>.CreateFailure($"Error: {ex.Message}");
        }
    }

    /// <inheritdoc/>
    public async Task<OperationResult<bool>> HandleCallbackAsync(
        string code,
        string state)
    {
        try
        {
            _logger.LogDebug("Handling OAuth callback");

            // Validate state
            if (!_authService.ValidateState(state))
            {
                return OperationResult<bool>.CreateFailure("Invalid OAuth state");
            }

            // Exchange code for token
            var tokenResult = await _authService.ExchangeCodeForTokenAsync(code);

            if (!tokenResult.Success)
            {
                return OperationResult<bool>.CreateFailure(tokenResult.FirstError ?? "Failed to exchange code for token");
            }

            // Fetch user info
            var userInfoResult = await _authService.GetUserInfoAsync(tokenResult.Data.AccessToken);

            if (!userInfoResult.Success)
            {
                return OperationResult<bool>.CreateFailure(userInfoResult.FirstError ?? "Failed to fetch user info");
            }

            // Set current user
            _authService.SetCurrentUser(userInfoResult.Data);

            _logger.LogDebug("Successfully authenticated user: {UserId}", userInfoResult.Data.Id);

            return OperationResult<bool>.CreateSuccess(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling OAuth callback");
            return OperationResult<bool>.CreateFailure($"Error: {ex.Message}");
        }
    }

    /// <inheritdoc/>
    public async Task<OperationResult<bool>> LogoutAsync()
    {
        try
        {
            _logger.LogDebug("Logging out user");

            await _authService.ClearTokenAsync();

            // Clear cache
            _cachedTournaments = null;

            return OperationResult<bool>.CreateSuccess(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error logging out");
            return OperationResult<bool>.CreateFailure($"Error: {ex.Message}");
        }
    }

    /// <inheritdoc/>
    public async Task<OperationResult<bool>> PostCommentAsync(
        string topicId,
        string comment,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Posting comment to topic: {TopicId}", topicId);

            var result = await _commentService.PostCommentAsync(topicId, comment, cancellationToken);

            if (!result.Success)
            {
                return OperationResult<bool>.CreateFailure(result.FirstError ?? "Failed to post comment");
            }

            return OperationResult<bool>.CreateSuccess(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error posting comment");
            return OperationResult<bool>.CreateFailure($"Error: {ex.Message}");
        }
    }

    /// <inheritdoc/>
    public async Task<OperationResult<IEnumerable<CommentModel>>> GetCommentsAsync(
        string topicId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Fetching comments for topic: {TopicId}", topicId);

            var result = await _commentService.GetCommentsAsync(topicId, cancellationToken);

            if (!result.Success)
            {
                return OperationResult<IEnumerable<CommentModel>>.CreateFailure(result.FirstError ?? "Failed to fetch comments");
            }

            return OperationResult<IEnumerable<CommentModel>>.CreateSuccess(result.Data);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching comments");
            return OperationResult<IEnumerable<CommentModel>>.CreateFailure($"Error: {ex.Message}");
        }
    }

    /// <summary>
    /// Clears the tournament cache.
    /// </summary>
    public void ClearCache()
    {
        _cachedTournaments = null;
        _logger.LogDebug("Cleared tournament cache");
    }
}
