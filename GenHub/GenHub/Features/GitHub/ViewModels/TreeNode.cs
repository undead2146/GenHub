using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using GenHub.Features.GitHub.ViewModels.Items;

namespace GenHub.Features.GitHub.ViewModels;

/// <summary>
/// Tree node for GitHub items display.
/// </summary>
public class TreeNode : INotifyPropertyChanged
{
    private bool _isExpanded;

    /// <summary>
    /// Occurs when a property value changes.
    /// </summary>
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// Gets or sets the display name.
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the description.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the icon source.
    /// </summary>
    public string IconSource { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether this is a folder node.
    /// </summary>
    public bool IsFolder { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this node is expanded.
    /// </summary>
    public bool IsExpanded
    {
        get => _isExpanded;
        set
        {
            if (_isExpanded != value)
            {
                _isExpanded = value;
                OnPropertyChanged();

                // Sync with child item if it exists
                if (Child != null)
                {
                    Child.IsExpanded = value;
                }
            }
        }
    }

    /// <summary>
    /// Gets or sets the child item if leaf node.
    /// </summary>
    public GitHubDisplayItemViewModel? Child { get; set; }

    /// <summary>
    /// Gets the children collection for folder nodes.
    /// </summary>
    public ObservableCollection<TreeNode> Children { get; } = new();

    /// <summary>
    /// Raises the PropertyChanged event.
    /// </summary>
    /// <param name="propertyName">Name of the property that changed.</param>
    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
