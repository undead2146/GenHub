using GenHub.Features.Downloads.ViewModels;
using Xunit;
using System.Threading.Tasks;

namespace GenHub.Tests.Core.ViewModels;

/// <summary>
/// Tests for DownloadsViewModel.
/// </summary>
public class DownloadsViewModelTests
{
    /// <summary>
    /// Ensures InitializeAsync completes successfully.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    [Fact]
    public async Task InitializeAsync_CompletesSuccessfully()
    {
        var vm = new DownloadsViewModel();
        await vm.InitializeAsync();
    }
}
