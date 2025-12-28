using GenHub.Common.ViewModels;
using GenHub.Core.Interfaces.GameProfiles;
using GenHub.Core.Models.GameProfile;

namespace GenHub.Features.GameProfiles.ViewModels;

/// <summary>
/// ViewModel for the "Add New Profile" item in the profiles grid.
/// Inherits from GameProfileItemViewModel to allow coexistence in the same collection.
/// </summary>
public partial class AddProfileItemViewModel : GameProfileItemViewModel
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AddProfileItemViewModel"/> class.
    /// </summary>
    public AddProfileItemViewModel()
        : base("add_new_profile", new GameProfile { Name = "Add New Profile" }, string.Empty, string.Empty)
    {
        // Add Profile is effectively a special state, we can use IsEditMode to distinguish or type checks
    }
}
