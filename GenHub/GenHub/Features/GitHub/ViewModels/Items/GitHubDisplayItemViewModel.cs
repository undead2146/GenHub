using System;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using GenHub.Core.Interfaces.GitHub;

namespace GenHub.Features.GitHub.ViewModels
{
    /// <summary>
    /// Base class for GitHub display items
    /// </summary>
    public abstract partial class GitHubDisplayItemViewModel : ObservableObject, IGitHubDisplayItem
    {
        // Protected fields accessible to derived classes
        protected string _iconKey = "FileIcon";
        
        private readonly ObservableCollection<IGitHubDisplayItem> _children = new();
        private bool _isExpanded;
        private bool _isSelected;
        private bool _isLoadingChildren;
        private bool _childrenLoaded;
        
        // Abstract properties that must be implemented by derived classes
        public abstract string DisplayName { get; }
        public abstract string Description { get; }
        public abstract bool IsExpandable { get; }
        public abstract DateTime SortDate { get; }
        public abstract bool IsRelease { get; }

        /// <summary>
        /// Gets the icon source for the item
        /// </summary>
        public virtual string? IconSource => null;

        /// <summary>
        /// Gets the children of this item
        /// </summary>
        public ObservableCollection<IGitHubDisplayItem> Children => _children;
        
        /// <summary>
        /// Gets a value indicating whether this item represents a workflow run
        /// </summary>
        public virtual bool IsWorkflowRun => false;
        
        /// <summary>
        /// Gets a value indicating whether this item is active
        /// </summary>
        public virtual bool IsActive => false;
        
        /// <summary>
        /// Gets or sets a value indicating whether this item is selected
        /// </summary>
        public bool IsSelected 
        { 
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }
        
        /// <summary>
        /// Gets or sets a value indicating whether the item is expanded
        /// </summary>
        public bool IsExpanded
        {
            get => _isExpanded;
            set => SetProperty(ref _isExpanded, value);
        }
        
        /// <summary>
        /// Gets whether this item is currently loading its children
        /// </summary>
        public bool IsLoadingChildren => _isLoadingChildren;
        
        /// <summary>
        /// Gets whether this item's children have been loaded
        /// </summary>
        public bool ChildrenLoaded => _childrenLoaded;
        
        /// <summary>
        /// Gets the key for the icon to use for this item
        /// </summary>
        public string IconKey => _iconKey;

        /// <summary>
        /// Loads child items for this display item
        /// </summary>
        public virtual Task LoadChildrenAsync(CancellationToken cancellationToken = default)
        {
            // Base implementation is empty - derived classes will override as needed
            return Task.CompletedTask;
        }
        
        /// <summary>
        /// Internal method for loading children that can be overridden by derived classes
        /// </summary>
        protected virtual Task LoadChildrenInternalAsync(CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
        
        // Helper method to update loading state
        protected void SetLoadingState(bool isLoading)
        {
            _isLoadingChildren = isLoading;
            OnPropertyChanged(nameof(IsLoadingChildren));
        }
        
        // Helper method to update loaded state
        protected void SetLoadedState(bool isLoaded)
        {
            _childrenLoaded = isLoaded;
            OnPropertyChanged(nameof(ChildrenLoaded));
        }
    }
}
