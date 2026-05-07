using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace MotionControl.App.Controls;

public partial class StatusLightTextBox : UserControl
{
    public static readonly DependencyProperty StatusBrushProperty =
        DependencyProperty.Register(nameof(StatusBrush), typeof(Brush), typeof(StatusLightTextBox),
            new PropertyMetadata(Brushes.Gray));

    public static readonly DependencyProperty TextProperty =
        DependencyProperty.Register(nameof(Text), typeof(string), typeof(StatusLightTextBox),
            new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    public Brush StatusBrush
    {
        get => (Brush)GetValue(StatusBrushProperty);
        set => SetValue(StatusBrushProperty, value);
    }

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public StatusLightTextBox()
    {
        InitializeComponent();
    }
}
