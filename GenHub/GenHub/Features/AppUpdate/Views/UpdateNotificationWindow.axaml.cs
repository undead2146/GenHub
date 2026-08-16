using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using GenHub.Features.AppUpdate.ViewModels;
using Microsoft.Extensions.Logging;

namespace GenHub.Features.AppUpdate.Views;

/// <summary>
/// window for displaying update notifications.
/// </summary>
public partial class UpdateNotificationWindow : Window
{
    private readonly ILogger<UpdateNotificationWindow>? _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateNotificationWindow"/> class.
    /// </summary>
    public UpdateNotificationWindow()
    {
        _logger = AppLocator.GetServiceOrDefault<ILogger<UpdateNotificationWindow>>();

        InitializeComponent();

        try
        {
            // set up the datacontext with proper dependency injection resolution
            DataContext = AppLocator.GetServiceOrDefault<UpdateNotificationViewModel>();
            _logger?.LogInformation("UpdateNotificationWindow initialized with ViewModel");
        }
        catch (System.Exception ex)
        {
            _logger?.LogError(ex, "Failed to initialize UpdateNotificationWindow ViewModel");
        }
    }

    /// <summary>
    /// shows the update notification window as a dialog.
    /// </summary>
    /// <param name="parent">the parent window.</param>
    /// <returns>a task representing the asynchronous operation.</returns>
    public static async Task ShowAsync(Window parent)
    {
        var window = new UpdateNotificationWindow();
        await window.ShowDialog(parent);
    }

    /// <summary>
    /// performs asynchronous initialization logic for the window.
    /// </summary>
    /// <returns>a <see cref="Task"/> representing the asynchronous operation.</returns>
    public async Task InitializeAsync()
    {
        if (DataContext is UpdateNotificationViewModel)
        {
            await Task.CompletedTask;
        }
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

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    /// <summary>
    /// handles pointer pressed event for the title bar to enable window dragging.
    /// </summary>
    /// <param name="sender">the sender.</param>
    /// <param name="e">the pointer event args.</param>
    private void TitleBar_PointerPressed(object sender, PointerPressedEventArgs e)
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

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
