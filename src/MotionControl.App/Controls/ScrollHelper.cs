using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace MotionControl.App.Controls;

/// <summary>
/// Shared scroll handler for DataGrid mouse wheel events across panels.
/// Provides both an attached behavior (for clean XAML) and a static event handler
/// that walks up to the UserControl root to find the parent ScrollViewer.
/// </summary>
public static class ScrollHelper
{
    // ── Attached behavior ────────────────────────────────────────────

    public static readonly DependencyProperty EnableScrollOnDataGridProperty =
        DependencyProperty.RegisterAttached(
            "EnableScrollOnDataGrid",
            typeof(bool),
            typeof(ScrollHelper),
            new PropertyMetadata(false, OnEnableScrollChanged));

    public static bool GetEnableScrollOnDataGrid(DependencyObject obj)
        => (bool)obj.GetValue(EnableScrollOnDataGridProperty);

    public static void SetEnableScrollOnDataGrid(DependencyObject obj, bool value)
        => obj.SetValue(EnableScrollOnDataGridProperty, value);

    private static void OnEnableScrollChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is DataGrid dg && (bool)e.NewValue)
        {
            dg.PreviewMouseWheel += SectionDataGrid_PreviewMouseWheel;
        }
    }

    // ── Static event handler (usable directly in XAML via code-behind) ─

    public static void SectionDataGrid_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (sender is not DataGrid dataGrid) return;

        var userControl = FindAncestor<UserControl>(dataGrid);
        if (userControl == null) return;

        HandleDataGridMouseWheel(sender, e, userControl);
    }

    /// <summary>
    /// Handles PreviewMouseWheel on a DataGrid: scrolls the DataGrid's internal
    /// ScrollViewer first, then falls back to the parent ScrollViewer.
    /// </summary>
    public static void HandleDataGridMouseWheel(object sender, MouseWheelEventArgs e, DependencyObject parent)
    {
        if (sender is not DataGrid dataGrid) return;

        var dataGridScrollViewer = FindVisualChild<ScrollViewer>(dataGrid);
        if (dataGridScrollViewer != null)
        {
            var scrollingDown = e.Delta < 0;
            var scrollingUp = e.Delta > 0;
            var canScrollDown = dataGridScrollViewer.VerticalOffset < dataGridScrollViewer.ScrollableHeight;
            var canScrollUp = dataGridScrollViewer.VerticalOffset > 0;

            if ((scrollingDown && canScrollDown) || (scrollingUp && canScrollUp))
            {
                dataGridScrollViewer.ScrollToVerticalOffset(dataGridScrollViewer.VerticalOffset - e.Delta / 3.0);
                e.Handled = true;
                return;
            }
        }

        var parentScrollViewer = FindVisualChild<ScrollViewer>(parent);
        if (parentScrollViewer != null)
        {
            parentScrollViewer.ScrollToVerticalOffset(parentScrollViewer.VerticalOffset - e.Delta / 3.0);
            e.Handled = true;
        }
    }

    // ── Tree helpers ───────────────────────────────────────────────────

    public static T? FindAncestor<T>(DependencyObject current) where T : DependencyObject
    {
        while (current != null)
        {
            if (current is T result) return result;
            current = VisualTreeHelper.GetParent(current);
        }
        return null;
    }

    public static T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T result) return result;

            var grandChild = FindVisualChild<T>(child);
            if (grandChild != null) return grandChild;
        }
        return null;
    }
}
