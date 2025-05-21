using CommunityToolkit.Mvvm.ComponentModel;

namespace GenHub.Features.GitHub.ViewModels
{
    /// <summary>
    /// View model for workflow definition dropdown items
    /// </summary>
    public partial class WorkflowDefinitionViewModel : ObservableObject
    {
        /// <summary>
        /// Gets or sets the workflow name
        /// </summary>
        [ObservableProperty]
        private string _name = string.Empty;
        
        /// <summary>
        /// Gets or sets the workflow file path
        /// </summary>
        [ObservableProperty]
        private string _path = string.Empty;
        
        /// <summary>
        /// Gets or sets the display name (friendly name)
        /// </summary>
        [ObservableProperty]
        private string _displayName = string.Empty;
    }
}
