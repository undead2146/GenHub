using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using GenHub.Features.Tools.IniEditor.ViewModels;

namespace GenHub.Features.Tools.IniEditor.Views;

/// <summary>
/// View for the INI editor tool.
/// </summary>
public partial class IniEditorView : UserControl
{
    /// <summary>
    /// Initializes a new instance of the <see cref="IniEditorView"/> class.
    /// </summary>
    public IniEditorView()
    {
        InitializeComponent();
        AddHandler(KeyDownEvent, OnPreviewKeyDown, RoutingStrategies.Tunnel);
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void OnPreviewKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not IniEditorViewModel viewModel)
        {
            return;
        }

        var modifiers = e.KeyModifiers;
        if ((modifiers & KeyModifiers.Control) == 0 || (modifiers & ~(KeyModifiers.Control | KeyModifiers.Shift)) != 0)
        {
            return;
        }

        var isShift = (modifiers & KeyModifiers.Shift) != 0;
        if (e.Key == Key.Z && !isShift && viewModel.UndoCommand.CanExecute(null))
        {
            viewModel.UndoCommand.Execute(null);
            e.Handled = true;
        }
        else if (((e.Key == Key.Y && !isShift) || (e.Key == Key.Z && isShift)) && viewModel.RedoCommand.CanExecute(null))
        {
            viewModel.RedoCommand.Execute(null);
            e.Handled = true;
        }
    }
}
