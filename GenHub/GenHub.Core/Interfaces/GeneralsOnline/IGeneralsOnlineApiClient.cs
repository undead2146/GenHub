using GenHub.Core.Models.GeneralsOnline;
using GenHub.Core.Models.Results;

namespace GenHub.Core.Interfaces.GeneralsOnline;

/// <summary>
/// Interface for Generals Online API interactions with strongly-typed responses.
/// Extension-friendly design: Add new methods here when new API endpoints become available.
/// </summary>
public interface IGeneralsOnlineApiClient
{
    // ===== Service & Stats =====

    /// <summary>
    /// Gets service statistics including player counts and connection data.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>Service statistics or null if unavailable.</returns>
    Task<ServiceStats?> GetServiceStatsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets service statistics as raw JSON (for custom parsing or future extensibility).
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>An operation result containing the JSON string or error information.</returns>
    Task<OperationResult<string>> GetServiceStatsJsonAsync(CancellationToken cancellationToken = default);

    // ===== Authentication =====

    /// <summary>
    /// Verifies the provided authentication token with the server.
    /// </summary>
    /// <param name="token">The token to verify.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>True if the token is valid, otherwise false.</returns>
    Task<bool> VerifyTokenAsync(string token, CancellationToken cancellationToken = default);

    // ===== Matches =====

    /// <summary>
    /// Gets active matches and lobbies as HTML.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>An operation result containing the HTML string or error information.</returns>
    Task<OperationResult<string>> GetActiveMatchesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets details for a specific match as HTML.
    /// </summary>
    /// <param name="matchId">The match ID.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>An operation result containing the HTML string or error information.</returns>
    Task<OperationResult<string>> GetMatchDetailsAsync(int matchId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the match history as HTML.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>An operation result containing the HTML string or error information.</returns>
    Task<OperationResult<string>> GetMatchHistoryAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the match history for a specific player as HTML.
    /// </summary>
    /// <param name="playerName">The player name to look up.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>An operation result containing the HTML string or error information.</returns>
    Task<OperationResult<string>> GetPlayerMatchHistoryAsync(string playerName, CancellationToken cancellationToken = default);

    // ===== Lobbies =====

    /// <summary>
    /// Gets available lobbies.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A list of available lobbies.</returns>
    Task<List<LobbyInfo>> GetLobbiesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets lobbies as raw JSON (for custom parsing or future extensibility).
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>An operation result containing the JSON string or error information.</returns>
    Task<OperationResult<string>> GetLobbiesJsonAsync(CancellationToken cancellationToken = default);

    // ===== Players =====

    /// <summary>
    /// Gets a list of currently active players as HTML.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>An operation result containing the HTML string or error information.</returns>
    Task<OperationResult<string>> GetActivePlayersAsync(CancellationToken cancellationToken = default);

    // ===== Leaderboards =====

    /// <summary>
    /// Gets leaderboard data for the specified period.
    /// </summary>
    /// <param name="period">The leaderboard period (daily, monthly, annual).</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A list of leaderboard entries.</returns>
    Task<List<LeaderboardEntry>> GetLeaderboardAsync(string period = "daily", CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets leaderboard as raw JSON (for custom parsing or future extensibility).
    /// </summary>
    /// <param name="period">The leaderboard period.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>An operation result containing the JSON string or error information.</returns>
    Task<OperationResult<string>> GetLeaderboardJsonAsync(string period = "daily", CancellationToken cancellationToken = default);
}
