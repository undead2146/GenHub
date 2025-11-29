using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using GenHub.Core.Constants;
using GenHub.Core.Interfaces.GeneralsOnline;
using GenHub.Core.Models.GeneralsOnline;
using GenHub.Core.Models.Results;
using Microsoft.Extensions.Logging;

namespace GenHub.Features.GeneralsOnline.Services;

/// <summary>
/// Client for interacting with the Generals Online API with strongly-typed responses.
/// Extensibility: Add new methods here when new API endpoints become available. Follow the pattern of 
/// providing both typed methods (e.g., GetServiceStatsAsync) and raw JSON methods (e.g., GetServiceStatsJsonAsync).
/// </summary>
// TODO: Implement HTTP resilience patterns using Microsoft.Extensions.Http.Resilience to add retry policies,
// circuit breaker, and timeout handling for transient failures. This will improve reliability when the
// Generals Online service experiences temporary issues. Configure in DI registration with:
// services.AddHttpClient<IGeneralsOnlineApiClient, GeneralsOnlineApiClient>()
//     .AddStandardResilienceHandler(options => { /* configure retry, circuit breaker, etc. */ });
public class GeneralsOnlineApiClient(
    HttpClient httpClient,
    IGeneralsOnlineAuthService authService,
    ILogger<GeneralsOnlineApiClient> logger) : IGeneralsOnlineApiClient
{
    private readonly HttpClient _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
    private readonly IGeneralsOnlineAuthService _authService = authService ?? throw new ArgumentNullException(nameof(authService));
    private readonly ILogger<GeneralsOnlineApiClient> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    // ===== Service & Stats =====

    /// <inheritdoc />
    public async Task<ServiceStats?> GetServiceStatsAsync(CancellationToken cancellationToken = default)
    {
        var result = await GetServiceStatsJsonAsync(cancellationToken);
        return result.Success ? result.Data.DeserializeOrDefault<ServiceStats>() : null;
    }

    /// <inheritdoc />
    public async Task<OperationResult<string>> GetServiceStatsJsonAsync(CancellationToken cancellationToken = default)
    {
        return await GetAsync(GeneralsOnlineConstants.ServiceStatsEndpoint, cancellationToken);
    }

    // ===== Authentication =====

    /// <inheritdoc />
    public Task<bool> VerifyTokenAsync(string token, CancellationToken cancellationToken = default)
    {
        // When real API is available, call the VerifyTokenEndpoint
        // var result = await GetAsync($"{GeneralsOnlineConstants.VerifyTokenEndpoint}?token={token}", cancellationToken);
        // return result.DeserializeOrDefault<TokenVerification>()?.IsValid ?? false;
        return Task.FromResult(!string.IsNullOrWhiteSpace(token));
    }

    // ===== Matches =====

    /// <inheritdoc />
    public async Task<OperationResult<string>> GetActiveMatchesAsync(CancellationToken cancellationToken = default)
    {
        return await GetAsync(GeneralsOnlineConstants.MatchesEndpoint, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<OperationResult<string>> GetMatchDetailsAsync(int matchId, CancellationToken cancellationToken = default)
    {
        return await GetAsync($"{GeneralsOnlineConstants.ViewMatchEndpoint}?match={matchId}", cancellationToken);
    }

    /// <inheritdoc />
    public async Task<OperationResult<string>> GetMatchHistoryAsync(CancellationToken cancellationToken = default)
    {
        return await GetAsync(GeneralsOnlineConstants.MatchHistoryEndpoint, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<OperationResult<string>> GetPlayerMatchHistoryAsync(string playerName, CancellationToken cancellationToken = default)
    {
        return await GetAsync($"{GeneralsOnlineConstants.MatchHistoryEndpoint}?player={playerName}", cancellationToken);
    }

    // ===== Lobbies =====

    /// <inheritdoc />
    public async Task<List<LobbyInfo>> GetLobbiesAsync(CancellationToken cancellationToken = default)
    {
        var result = await GetLobbiesJsonAsync(cancellationToken);
        return result.Success ? result.Data.DeserializeList<LobbyInfo>() : new List<LobbyInfo>();
    }

    /// <inheritdoc />
    public async Task<OperationResult<string>> GetLobbiesJsonAsync(CancellationToken cancellationToken = default)
    {
        return await GetAuthenticatedAsync(GeneralsOnlineConstants.LobbiesEndpoint, cancellationToken);
    }

    // ===== Players =====

    /// <inheritdoc />
    public async Task<OperationResult<string>> GetActivePlayersAsync(CancellationToken cancellationToken = default)
    {
        return await GetAsync(GeneralsOnlineConstants.PlayersEndpoint, cancellationToken);
    }

    // ===== Leaderboards =====

    /// <inheritdoc />
    public async Task<List<LeaderboardEntry>> GetLeaderboardAsync(string period = "daily", CancellationToken cancellationToken = default)
    {
        var result = await GetLeaderboardJsonAsync(period, cancellationToken);
        return result.Success ? result.Data.DeserializeList<LeaderboardEntry>() : new List<LeaderboardEntry>();
    }

    /// <inheritdoc />
    public async Task<OperationResult<string>> GetLeaderboardJsonAsync(string period = "daily", CancellationToken cancellationToken = default)
    {
        return await GetAsync($"{GeneralsOnlineConstants.LeaderboardsEndpoint}?type={period}", cancellationToken);
    }

    // ===== Private Helper Methods =====
    // These methods handle the HTTP communication and error handling uniformly.
    // To add a new endpoint: just call GetAsync or GetAuthenticatedAsync with the URL.
    private async Task<OperationResult<string>> GetAsync(string url, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogDebug("Fetching data from {Url}", url);
            var response = await _httpClient.GetAsync(url, cancellationToken).ConfigureAwait(false);
            
            if (!response.IsSuccessStatusCode)
            {
                var error = $"API request failed with status {response.StatusCode} for {url}";
                _logger.LogWarning(error);
                return OperationResult<string>.CreateFailure(error);
            }

            var content = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            _logger.LogDebug("Successfully retrieved {Length} characters from {Url}", content.Length, url);
            return OperationResult<string>.CreateSuccess(content);
        }
        catch (HttpRequestException ex)
        {
            var error = $"HTTP error calling Generals Online API: {url}";
            _logger.LogError(ex, error);
            return OperationResult<string>.CreateFailure(error);
        }
        catch (Exception ex)
        {
            var error = $"Unexpected error calling Generals Online API: {url}";
            _logger.LogError(ex, error);
            return OperationResult<string>.CreateFailure(error);
        }
    }

    private async Task<OperationResult<string>> GetAuthenticatedAsync(string url, CancellationToken cancellationToken)
    {
        try
        {
            var token = await _authService.GetAuthTokenAsync().ConfigureAwait(false);
            if (string.IsNullOrEmpty(token))
            {
                var error = $"No authentication token available for {url}";
                _logger.LogWarning(error);
                return OperationResult<string>.CreateFailure(error);
            }

            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Add(GeneralsOnlineConstants.AuthTokenHeader, token);

            _logger.LogDebug("Fetching authenticated data from {Url}", url);
            var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            
            if (!response.IsSuccessStatusCode)
            {
                var error = $"Authenticated API request failed with status {response.StatusCode} for {url}";
                _logger.LogWarning(error);
                return OperationResult<string>.CreateFailure(error);
            }

            var content = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            _logger.LogDebug("Successfully retrieved {Length} characters from {Url}", content.Length, url);
            return OperationResult<string>.CreateSuccess(content);
        }
        catch (HttpRequestException ex)
        {
            var error = $"HTTP error calling authenticated Generals Online API: {url}";
            _logger.LogError(ex, error);
            return OperationResult<string>.CreateFailure(error);
        }
        catch (Exception ex)
        {
            var error = $"Unexpected error calling authenticated Generals Online API: {url}";
            _logger.LogError(ex, error);
            return OperationResult<string>.CreateFailure(error);
        }
    }
}
