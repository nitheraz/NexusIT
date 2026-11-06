
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace NexusIT.App.Services;

public static class GlobalMouseWheelScrolling
{
    public static void Attach(Window window)
    {
        ArgumentNullException.ThrowIfNull(window);

        window.AddHandler(
            UIElement.PreviewMouseWheelEvent,
            new MouseWheelEventHandler(HandleMouseWheel),
            true);
    }

    private static void HandleMouseWheel(
        object sender,
        MouseWheelEventArgs e)
    {
        if (e.Handled)
            return;

        DependencyObject? current =
            e.OriginalSource as DependencyObject;

        while (current != null)
        {
            if (current is ScrollViewer scrollViewer &&
                CanScroll(scrollViewer, e.Delta))
            {
                double newOffset =
                    scrollViewer.VerticalOffset - e.Delta;

                newOffset = Math.Max(
                    0,
                    Math.Min(
                        scrollViewer.ScrollableHeight,
                        newOffset));

                scrollViewer.ScrollToVerticalOffset(newOffset);

                e.Handled = true;
                return;
            }

            current = GetParentObject(current);
        }
    }

    private static bool CanScroll(
        ScrollViewer scrollViewer,
        int delta)
    {
        if (scrollViewer.ScrollableHeight <= 0)
            return false;

        // Positive delta means scrolling upwards.
        if (delta > 0)
            return scrollViewer.VerticalOffset > 0;

        // Negative delta means scrolling downwards.
        return scrollViewer.VerticalOffset <
               scrollViewer.ScrollableHeight;
    }

    private static DependencyObject? GetParentObject(
        DependencyObject child)
    {
        if (child is FrameworkContentElement contentElement)
            return contentElement.Parent;

        if (child is Visual || child is Visual3D)
            return VisualTreeHelper.GetParent(child);

        return LogicalTreeHelper.GetParent(child);
    }
}
