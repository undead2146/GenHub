using System;
using System.Threading.Tasks;
using GenHub.Core.Models.GeneralsOnline;

namespace GenHub.Core.Interfaces.GeneralsOnline;

/// <summary>
/// Interface for Generals Online authentication and credential management.
/// </summary>
public interface IGeneralsOnlineAuthService
{
    /// <summary>
    /// Gets an observable that emits the current authentication status.
    /// </summary>
    IObservable<bool> IsAuthenticated { get; }

    /// <summary>
    /// Gets the current authentication token, if any.
    /// </summary>
    string? CurrentToken { get; }

    /// <summary>
    /// Validates the presence and content of the credential file.
    /// </summary>
    /// <returns>True if a token is found in the file, otherwise false.</returns>
    Task<bool> ValidateCredentialsAsync();

    /// <summary>
    /// Verifies the provided token with the server.
    /// </summary>
    /// <param name="token">The token to verify.</param>
    /// <returns>True if the token is valid, otherwise false.</returns>
    Task<bool> VerifyTokenWithServerAsync(string token);

    /// <summary>
    /// Gets the authentication token if available.
    /// </summary>
    /// <returns>The token string or null.</returns>
    Task<string?> GetAuthTokenAsync();

    /// <summary>
    /// Initializes the service and starts monitoring for credential changes.
    /// </summary>
    /// <returns>A task representing the asynchronous initialization operation.</returns>
    Task InitializeAsync();

    /// <summary>
    /// Saves a refresh token to the credentials file.
    /// </summary>
    /// <param name="refreshToken">The refresh token to save.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task SaveRefreshTokenAsync(string refreshToken);
}
