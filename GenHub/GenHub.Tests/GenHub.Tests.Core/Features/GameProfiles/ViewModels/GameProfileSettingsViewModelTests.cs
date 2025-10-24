using GenHub.Features.GameProfiles.ViewModels;
using Xunit;

namespace GenHub.Tests.Core.ViewModels;

/// <summary>
/// Contains tests for <see cref="GameProfileSettingsViewModel"/>.
/// </summary>
public class GameProfileSettingsViewModelTests
{
    /// <summary>
    /// Verifies that the <see cref="GameProfileSettingsViewModel"/> can be constructed.
    /// </summary>
    [Fact]
    public void CanConstruct()
    {
        var vm = new GameProfileSettingsViewModel(null, null, null, null, null, null, null, null, null);
        Assert.NotNull(vm);
        Assert.Equal(string.Empty, vm.Name);
        Assert.Equal(string.Empty, vm.Description);
    }

    /// <summary>
    /// Verifies that the <see cref="GameProfileSettingsViewModel"/> can be initialized for a new profile.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [Fact]
    public async Task CanInitializeForNewProfile()
    {
        var vm = new GameProfileSettingsViewModel(null, null, null, null, null, null, null, null, null);

        await vm.InitializeForNewProfileAsync();

        // New profile initialization sets default values
        Assert.Equal("New Profile", vm.Name);
        Assert.Equal("A new game profile", vm.Description);
    }
}
