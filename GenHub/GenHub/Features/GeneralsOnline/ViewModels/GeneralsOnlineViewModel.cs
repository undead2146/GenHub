using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GenHub.Common.ViewModels;
using GenHub.Core.Interfaces.Common;
using GenHub.Core.Interfaces.GeneralsOnline;
using Microsoft.Extensions.Logging;

namespace GenHub.Features.GeneralsOnline.ViewModels;

/// <summary>
/// ViewModel for the Generals Online main tab.
/// </summary>
/// <param name="apiClient">The API client.</param>
/// <param name="authService">The authentication service.</param>
/// <param name="leaderboardViewModel">The leaderboard ViewModel.</param>
/// <param name="matchHistoryViewModel">The match history ViewModel.</param>
/// <param name="lobbiesViewModel">The lobbies ViewModel.</param>
/// <param name="serviceStatusViewModel">The service status ViewModel.</param>
/// <param name="externalLinkService">The external link service.</param>
/// <param name="logger">The logger.</param>
public partial class GeneralsOnlineViewModel(
    IGeneralsOnlineApiClient apiClient,
    IGeneralsOnlineAuthService authService,
    LeaderboardViewModel leaderboardViewModel,
    MatchHistoryViewModel matchHistoryViewModel,
    LobbiesViewModel lobbiesViewModel,
    ServiceStatusViewModel serviceStatusViewModel,
    IExternalLinkService externalLinkService,
    ILogger<GeneralsOnlineViewModel> logger) : ViewModelBase
{
    [ObservableProperty]
    private string username = "Loading...";

    [ObservableProperty]
    private string statusMessage = "Connecting...";

    [ObservableProperty]
    private bool isLoading;

    [ObservableProperty]
    private bool isAuthenticated;

    [ObservableProperty]
    private int selectedTabIndex;

    /// <summary>
    /// Gets the leaderboard ViewModel.
    /// </summary>
    public LeaderboardViewModel Leaderboard => leaderboardViewModel;

    /// <summary>
    /// Gets the match history ViewModel.
    /// </summary>
    public MatchHistoryViewModel MatchHistory => matchHistoryViewModel;

    /// <summary>
    /// Gets the lobbies ViewModel.
    /// </summary>
    public LobbiesViewModel Lobbies => lobbiesViewModel;

    /// <summary>
    /// Gets the service status ViewModel.
    /// </summary>
    public ServiceStatusViewModel ServiceStatus => serviceStatusViewModel;

    /// <summary>
    /// Initializes the ViewModel.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task InitializeAsync()
    {
        IsLoading = true;
        try
        {
            var token = await authService.GetAuthTokenAsync();
            if (string.IsNullOrEmpty(token))
            {
                Username = "Not Authenticated";
                StatusMessage = "Please log in via the Generals Online client";
                IsAuthenticated = false;

                // Still load public data
                await LoadPublicDataAsync();
                return;
            }

            var isValid = await apiClient.VerifyTokenAsync(token);
            if (!isValid)
            {
                Username = "Authentication Failed";
                StatusMessage = "Invalid token. Please re-login.";
                IsAuthenticated = false;

                // Still load public data
                await LoadPublicDataAsync();
                return;
            }

            Username = "Authenticated User";
            StatusMessage = "Connected to Generals Online";
            IsAuthenticated = true;

            await LoadAllDataAsync();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to initialize Generals Online view");
            StatusMessage = "Error connecting to service";
            IsAuthenticated = false;
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        await InitializeAsync();
    }

    [RelayCommand]
    private void OpenDiscord()
    {
        if (!externalLinkService.OpenUrl("https://discord.playgenerals.online/"))
        {
            logger.LogWarning("Failed to open Discord link");
        }
    }

    [RelayCommand]
    private void OpenWebsite()
    {
        if (!externalLinkService.OpenUrl("https://www.playgenerals.online/"))
        {
            logger.LogWarning("Failed to open website link");
        }
    }

    private async Task LoadAllDataAsync()
    {
        await Task.WhenAll(
            ServiceStatus.LoadAsync(),
            Leaderboard.LoadAsync(),
            Lobbies.LoadAsync(),
            MatchHistory.LoadAsync());
    }

    private async Task LoadPublicDataAsync()
    {
        // Load public data even without authentication
        await Task.WhenAll(
            ServiceStatus.LoadAsync(),
            Leaderboard.LoadAsync(),
            Lobbies.LoadAsync());
    }
}