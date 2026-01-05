using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace GenHub.Windows.Features.ActionSets.UI;

/// <summary>
/// View for the GenPatcher tool.
/// </summary>
public partial class GenPatcherToolView : UserControl
{
    /// <summary>
    /// Initializes a new instance of the <see cref="GenPatcherToolView"/> class.
    /// </summary>
    public GenPatcherToolView()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
