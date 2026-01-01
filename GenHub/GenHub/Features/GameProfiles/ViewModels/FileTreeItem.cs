using System.Collections.ObjectModel;

namespace GenHub.Features.GameProfiles.ViewModels;

/// <summary>
/// Represents an item in the file tree view.
/// </summary>
public class FileTreeItem
{
    /// <summary>
    /// Gets or sets the name of the file or directory.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether this item is a file.
    /// </summary>
    public bool IsFile { get; set; }

    /// <summary>
    /// Gets or sets the full path of the file or directory.
    /// </summary>
    public string FullPath { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the children of this item (for directories).
    /// </summary>
    public ObservableCollection<FileTreeItem> Children { get; set; } = [];
}
