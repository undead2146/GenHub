using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;

namespace GenHub.Features.Tools.IniEditor.ViewModels;

/// <summary>
/// File tree node for the INI explorer.
/// </summary>
public sealed partial class IniFileTreeNodeViewModel : ObservableObject
{
    /// <summary>
    /// Initializes a new instance of the <see cref="IniFileTreeNodeViewModel"/> class.
    /// </summary>
    /// <param name="name">The display name.</param>
    /// <param name="fullPath">The full path.</param>
    /// <param name="isDirectory">Whether the node is a directory.</param>
    /// <param name="parent">The parent node.</param>
    public IniFileTreeNodeViewModel(string name, string fullPath, bool isDirectory, IniFileTreeNodeViewModel? parent)
    {
        Name = name;
        FullPath = fullPath;
        IsDirectory = isDirectory;
        Parent = parent;
    }

    /// <summary>
    /// Gets the display name.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the full path.
    /// </summary>
    public string FullPath { get; }

    /// <summary>
    /// Gets a value indicating whether the node is a directory.
    /// </summary>
    public bool IsDirectory { get; }

    /// <summary>
    /// Gets the parent node.
    /// </summary>
    public IniFileTreeNodeViewModel? Parent { get; }

    /// <summary>
    /// Gets child nodes.
    /// </summary>
    public ObservableCollection<IniFileTreeNodeViewModel> Children { get; } = [];

    /// <summary>
    /// Gets or sets a value indicating whether the node is expanded.
    /// </summary>
    [ObservableProperty]
    private bool _isExpanded;

    /// <summary>
    /// Gets or sets a value indicating whether the node has visible descendants.
    /// </summary>
    public bool HasVisibleDescendants { get; set; }
}
