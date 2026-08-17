using System;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using GenHub.Features.GameProfiles.ViewModels;

namespace GenHub.Features.GameProfiles.Views;

/// <summary>
/// window for adding local content to game profiles.
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
    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        GenHub.Infrastructure.Interop.AdminDragDropFix.Apply(this, OnAdminDrop);
    }

    /// <inheritdoc/>
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == WindowStateProperty)
        {
            var maximizeIcon = (change.Sender as Control)?.FindControl<Path>("MaximizeIcon");
            if (maximizeIcon != null)
            {
                maximizeIcon.Data = WindowState == WindowState.Maximized
                    ? Geometry.Parse("M4,8H8V4H20V16H16V20H4V8M16,8V6H10V8H16M6,10V18H14V10H6Z")
                    : Geometry.Parse("M4,4H20V20H4V4M6,8V18H18V8H6Z");
            }
        }
    }

    /// <inheritdoc />
    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (DataContext is AddLocalContentViewModel vm)
        {
            vm.RequestClose += (s, result) => Close(result);

            // wire up the browse delegates
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
                return result.Count > 0 ? result[0].Path.LocalPath : null;
            };

            vm.BrowseFileAction = async () =>
            {
                if (StorageProvider == null)
                {
                    return null;
                }

                var result = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
                {
                    Title = "Select Files",
                    AllowMultiple = true,
                    FileTypeFilter = [FilePickerFileTypes.All, new("Zip Archives") { Patterns = ["*.zip"] }],
                });
                return result.Count > 0 ? result.Select(f => f.Path.LocalPath).ToList() : null;
            };
        }
    }

    private void OnTitleBarPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(sender as Visual).Properties.IsLeftButtonPressed)
        {
            if (e.ClickCount == 2)
            {
                MaximizeButton_Click(sender, new RoutedEventArgs());
            }
            else
            {
                BeginMoveDrag(e);
            }
        }
    }

    private void MinimizeButton_Click(object? sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void MaximizeButton_Click(object? sender, RoutedEventArgs e)
    {
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    }

    private void CloseButton_Click(object? sender, RoutedEventArgs e)
    {
        Close();
    }

    private void OnAdminDrop(string[] files)
    {
        _ = HandleAdminDropAsync(files);
    }

    private async Task HandleAdminDropAsync(string[] files)
    {
        if (DataContext is not AddLocalContentViewModel vm) return;

        foreach (var file in files)
        {
            await vm.ImportContentAsync(file);
        }
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void OnDragOver(object? sender, DragEventArgs e)
    {
        e.DragEffects = e.Data.Contains(DataFormats.Files) ? DragDropEffects.Copy : DragDropEffects.None;
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
