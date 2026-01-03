using Avalonia.Controls;

namespace GenHub.Features.Tools.Views;

/// <summary>
/// View for managing and displaying tool plugins.
/// </summary>
public partial class ToolsView : UserControl
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ToolsView"/> class.
    /// </summary>
    public ToolsView()
    {
        InitializeComponent();
    }

    private void OnTriggerZonePointerEntered(object? sender, Avalonia.Input.PointerEventArgs e)
    {
        if (DataContext is ViewModels.ToolsViewModel vm)
        {
            vm.IsPaneOpen = true;
        }
    }

    private void OnContentPointerPressed(object? sender, Avalonia.Input.PointerPressedEventArgs e)
    {
        if (DataContext is ViewModels.ToolsViewModel vm)
        {
            vm.IsPaneOpen = false;
        }
    }
}