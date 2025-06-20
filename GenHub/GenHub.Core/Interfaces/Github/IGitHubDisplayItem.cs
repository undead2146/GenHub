using System;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;

namespace GenHub.Core.Interfaces.GitHub
{
    /// <summary>
    /// Interface for GitHub display items with enhanced command support
    /// </summary>
    public interface IGitHubDisplayItem
    {
        // Basic properties
        /// <summary>
        /// Gets the display name of the item
        /// </summary>
        string DisplayName { get; }

        /// <summary>
        /// Gets the description of the item
        /// </summary>
        string Description { get; }

        /// <summary>
        /// Gets the date used for sorting
        /// </summary>
        DateTime SortDate { get; }

        /// <summary>
        /// Gets a value indicating whether this item is expandable
        /// </summary>
        bool IsExpandable { get; }

        /// <summary>
        /// Gets a value indicating whether this is a release item
        /// </summary>
        bool IsRelease { get; }

        /// <summary>
        /// Gets the key for the icon to display
        /// </summary>
        string IconKey { get; }

        /// <summary>
        /// Gets the icon source for the item
        /// </summary>
        string? IconSource { get; }

        // Hierarchy properties
        /// <summary>
        /// Gets the children of this item
        /// </summary>
        ObservableCollection<IGitHubDisplayItem> Children { get; }

        /// <summary>
        /// Gets or sets a value indicating whether this item is expanded
        /// </summary>
        bool IsExpanded { get; set; }

        /// <summary>
        /// Gets a value indicating whether children have been loaded
        /// </summary>
        bool ChildrenLoaded { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether this item is selected
        /// </summary>
        bool IsSelected { get; set; }

        /// <summary>
        /// Gets a value indicating whether children are currently being loaded
        /// </summary>
        bool IsLoadingChildren { get; set; }

        // Extended properties
        /// <summary>
        /// Gets the run number for workflow-related items
        /// </summary>
        int? RunNumber { get; }

        /// <summary>
        /// Gets a value indicating whether this item can be downloaded
        /// </summary>
        bool CanDownload { get; }

        /// <summary>
        /// Gets a value indicating whether this item can be installed
        /// </summary>
        bool CanInstall { get; }

        /// <summary>
        /// Gets a value indicating whether this is a workflow run
        /// </summary>
        bool IsWorkflowRun { get; }

        /// <summary>
        /// Gets a value indicating whether the item is active
        /// </summary>
        bool IsActive { get; }

        // Command properties
        /// <summary>
        /// Gets the command to install the item
        /// </summary>
        ICommand? InstallCommand { get; }

        /// <summary>
        /// Gets the command to download the item
        /// </summary>
        ICommand? DownloadCommand { get; }

        // Methods
        /// <summary>
        /// Loads children for this item asynchronously
        /// </summary>
        Task LoadChildrenAsync(CancellationToken cancellationToken = default);
    }
}
