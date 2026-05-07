using System.Windows;
using System.Windows.Controls;

namespace MotionControl.App.Controls;

/// <summary>
/// Reusable event log section with standard card wrapper.
/// Wraps a Border with title + hint + injected DataGrid content.
/// Usage:
///   &lt;controls:EventLogPanel Title="Work Head Event Log" Hint="..."&gt;
///       &lt;DataGrid ItemsSource="{Binding WorkHeadEventLog.Events}" Style="{StaticResource SectionDataGridStyle}"&gt;
///           &lt;DataGrid.Columns&gt;
///               &lt;DataGridTextColumn Header="Time" Binding="{Binding Time}" Width="110" /&gt;
///               ...
///           &lt;/DataGrid.Columns&gt;
///       &lt;/DataGrid&gt;
///   &lt;/controls:EventLogPanel&gt;
/// </summary>
public partial class EventLogPanel : UserControl
{
    public static readonly DependencyProperty TitleProperty =
        DependencyProperty.Register(nameof(Title), typeof(string), typeof(EventLogPanel),
            new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty HintProperty =
        DependencyProperty.Register(nameof(Hint), typeof(string), typeof(EventLogPanel),
            new PropertyMetadata(string.Empty));

    /// <summary>
    /// The DataGrid (with columns) to display inside the log panel.
    /// Set via content element syntax in XAML.
    /// </summary>
    public static readonly DependencyProperty LogContentProperty =
        DependencyProperty.Register(nameof(LogContent), typeof(object), typeof(EventLogPanel),
            new PropertyMetadata(null));

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string Hint
    {
        get => (string)GetValue(HintProperty);
        set => SetValue(HintProperty, value);
    }

    /// <summary>
    /// The DataGrid (or other content) to display in the log area.
    /// Set via XAML content element syntax.
    /// </summary>
    public object LogContent
    {
        get => GetValue(LogContentProperty);
        set => SetValue(LogContentProperty, value);
    }

    public EventLogPanel()
    {
        InitializeComponent();
    }
}
