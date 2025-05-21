using System;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;

namespace GenHub.Core.Interfaces.GitHub
{
    /// <summary>
    /// Interface for GitHub display items
    /// </summary>
    public interface IGitHubDisplayItem
    {
        /// <summary>
        /// Gets the display name for this item
        /// </summary>
        string DisplayName { get; }
        
        /// <summary>
        /// Gets the description of the item
        /// </summary>
        string? Description { get; }
        
        /// <summary>
        /// Gets the icon source for the item
        /// </summary>
        string? IconSource { get; }
        
        /// <summary>
        /// Gets a value indicating whether the item is active
        /// </summary>
        bool IsActive { get; }
        
        /// <summary>
        /// Gets or sets a value indicating whether the item is selected
        /// </summary>
        bool IsSelected { get; set; }
        
        /// </summary>
        DateTime SortDate { get; }
        
        /// <summary>
        /// Gets whether this item can be expanded
        /// </summary>
        bool IsExpandable { get; }
        
        /// <summary>
        /// Gets whether this item is currently expanded
        /// </summary>
        bool IsExpanded { get; set; }
        
        /// <summary>
        /// Gets whether this item is currently loading its children
        /// </summary>
        bool IsLoadingChildren { get; }
        
        /// <summary>
        /// Gets whether this item's children have been loaded
        /// </summary>
        bool ChildrenLoaded { get; }
        
        /// <summary>
        /// Gets the key for the icon to display
        /// </summary>
        string IconKey { get; }
        
        /// <summary>
        /// Gets whether this item represents a workflow run
        /// </summary>
        bool IsWorkflowRun { get; }
        
        /// <summary>
        /// Gets whether this item represents a release
        /// </summary>
        bool IsRelease { get; }
        
        /// <summary>
        /// Gets the children of this item
        /// </summary>
        ObservableCollection<IGitHubDisplayItem> Children { get; }
        
        /// <summary>
        /// Loads the children of this item
        /// </summary>
        Task LoadChildrenAsync(CancellationToken cancellationToken = default);
    }
}
