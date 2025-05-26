using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Xaml.Interactivity;

namespace GenHub.Infrastructure.UI
{
    /// <summary>
    /// Behavior to enable window dragging
    /// </summary>
    public class WindowDragBehavior : Behavior<Control>
    {
        protected override void OnAttached()
        {
            base.OnAttached();
            if (AssociatedObject != null)
            {
                AssociatedObject.PointerPressed += OnPointerPressed;
            }
        }

        protected override void OnDetaching()
        {
            if (AssociatedObject != null)
            {
                AssociatedObject.PointerPressed -= OnPointerPressed;
            }
            base.OnDetaching();
        }

        private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (AssociatedObject != null && e.GetCurrentPoint(AssociatedObject).Properties.IsLeftButtonPressed)
            {
                var window = TopLevel.GetTopLevel(AssociatedObject) as Window;
                window?.BeginMoveDrag(e);
            }
        }
    }
}
