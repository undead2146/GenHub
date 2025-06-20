using System;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using GenHub.Core.Interfaces.GitHub;

namespace GenHub.Features.GitHub.ViewModels
{
    /// <summary>
    /// Base class for all GitHub display items with common functionality
    /// </summary>
    public abstract partial class GitHubDisplayItemViewModel : ObservableObject, IGitHubDisplayItem
    {
        protected string _iconKey = "FileIcon";
        
        #region Observable Properties
        [ObservableProperty]
        private ObservableCollection<IGitHubDisplayItem> _children = new();

        [ObservableProperty]
        private bool _isExpanded = false;

        [ObservableProperty]
        private bool _childrenLoaded = false;

        [ObservableProperty]
        private bool _isSelected = false;

        [ObservableProperty]
        private bool _isLoadingChildren = false;
        #endregion

        #region Abstract Properties (Required by Interface)
        public abstract string DisplayName { get; }
        public abstract string Description { get; }
        public abstract DateTime SortDate { get; }
        public abstract bool IsExpandable { get; }
        public abstract bool IsRelease { get; }
        #endregion

        #region Virtual Properties (Can be overridden)
        public virtual int? RunNumber => null;
        public virtual bool CanDownload => false;
        public virtual bool CanInstall => false;
        public virtual bool IsWorkflowRun => false;
        public virtual bool IsActive => false;
        public virtual string? IconSource => null;

        // Command properties - virtual so they can be overridden
        public virtual ICommand? InstallCommand => null;
        public virtual ICommand? DownloadCommand => null;
        #endregion

        #region Common Properties
        public string IconKey => _iconKey;
        #endregion

        #region Abstract Methods
        public abstract Task LoadChildrenAsync(CancellationToken cancellationToken = default);
        #endregion
    }
}
