namespace GenHub.Features.Downloads.ViewModels;

/// <summary>
/// ViewModel for an addon item in the Addons section.
/// Inherits common downloadable item and expandable row behavior from <see cref="DownloadableItemViewModel"/>.
/// </summary>
public partial class AddonItemViewModel : DownloadableItemViewModel
{
    /// <summary>
    /// Gets or sets the short summary or description of the addon.
    /// Maps to <see cref="DownloadableItemViewModel.FullDescription"/>.
    /// </summary>
    public string? Description
    {
        get => FullDescription;
        set => FullDescription = value;
    }
}
