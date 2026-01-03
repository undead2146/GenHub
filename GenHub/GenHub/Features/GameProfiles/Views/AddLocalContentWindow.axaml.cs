using System;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using GenHub.Features.GameProfiles.ViewModels;

namespace GenHub.Features.GameProfiles.Views;

/// <summary>
/// Window for adding local content to game profiles.
/// </summary>
public partial class AddLocalContentWindow : Window
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AddLocalContentWindow"/> class.
    /// </summary>
    public AddLocalContentWindow()
    {
        InitializeComponent();
        AddHandler(DragDrop.DropEvent, OnDrop);
        AddHandler(DragDrop.DragOverEvent, OnDragOver);
    }

    /// <inheritdoc />
    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (DataContext is AddLocalContentViewModel vm)
        {
            vm.RequestClose += (s, result) => Close(result);

            // Wire up the browse delegates
            vm.BrowseFolderAction = async () =>
            {
                if (StorageProvider == null)
                {
                    return null;
                }

                var result = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
                {
                    Title = "Select Content Folder",
                    AllowMultiple = false,
                });
                return result.FirstOrDefault()?.Path.LocalPath;
            };

            vm.BrowseFileAction = async () =>
            {
                if (StorageProvider == null)
                {
                    return null;
                }

                var result = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
                {
                    Title = "Select Archive File",
                    AllowMultiple = false,
                    FileTypeFilter = [FilePickerFileTypes.All, new("Zip Archives") { Patterns = ["*.zip"] }],
                });
                return result.FirstOrDefault()?.Path.LocalPath;
            };
        }
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    // Drag & Drop handlers
    private void OnDragOver(object? sender, DragEventArgs e)
    {
        if (e.Data.Contains(DataFormats.Files))
        {
            e.DragEffects = DragDropEffects.Copy;
        }
        else
        {
            e.DragEffects = DragDropEffects.None;
        }
    }

    private async void OnDrop(object? sender, DragEventArgs e)
    {
        if (DataContext is not AddLocalContentViewModel vm) return;

        var files = e.Data.GetFiles();
        if (files != null)
        {
            foreach (var file in files)
            {
                if (file?.Path?.LocalPath is { } path)
                {
                    await vm.ImportContentAsync(path);
                }
            }
        }
    }
}
