using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace MotionControl.App.Views.Dialogs;

public enum DialogIcon
{
    None,
    Info,
    Success,
    Warning,
    Error,
    Alarm
}

public enum DialogButton
{
    OK,
    YesNo,
    YesNoCancel,
    OKCancel
}

public partial class DialogWindow : Window
{
    public static readonly DependencyProperty MessageProperty =
        DependencyProperty.Register(nameof(Message), typeof(string), typeof(DialogWindow), new PropertyMetadata(string.Empty));

    public string Message
    {
        get => (string)GetValue(MessageProperty);
        set => SetValue(MessageProperty, value);
    }

    public DialogButton Buttons { get; set; } = DialogButton.OK;
    public DialogIcon DialogKind { get; set; } = DialogIcon.Info;
    public MessageBoxResult Result { get; private set; } = MessageBoxResult.Cancel;

    static readonly SolidColorBrush InfoBg = new((Color)ColorConverter.ConvertFromString("#547E9D"));
    static readonly SolidColorBrush SuccessBg = new((Color)ColorConverter.ConvertFromString("#5E8F68"));
    static readonly SolidColorBrush WarningBg = new((Color)ColorConverter.ConvertFromString("#B8924A"));
    static readonly SolidColorBrush ErrorBg = new((Color)ColorConverter.ConvertFromString("#B85E5E"));
    static readonly SolidColorBrush AlarmBg = new((Color)ColorConverter.ConvertFromString("#D64545"));
    static readonly SolidColorBrush DarkBg = new((Color)ColorConverter.ConvertFromString("#0C141C"));
    static readonly SolidColorBrush LightFg = new((Color)ColorConverter.ConvertFromString("#FFFFFF"));
    static readonly SolidColorBrush TransparentBrush = Brushes.Transparent;

    public DialogWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        SetupIcon();
        SetupButtons();
    }

    private void SetupIcon()
    {
        var (bg, fg, symbol) = DialogKind switch
        {
            DialogIcon.Info => (InfoBg, LightFg, "i"),
            DialogIcon.Success => (SuccessBg, LightFg, "✓"),
            DialogIcon.Warning => (WarningBg, DarkBg, "⚠"),
            DialogIcon.Error => (ErrorBg, LightFg, "✕"),
            DialogIcon.Alarm => (AlarmBg, LightFg, "!"),
            _ => (TransparentBrush, TransparentBrush, "")
        };

        IconContainer.Background = bg;
        IconText.Text = symbol;
        IconText.Foreground = fg;
    }

    private void SetupButtons()
    {
        ButtonsPanel.Children.Clear();

        var config = Buttons switch
        {
            DialogButton.OK => new[] { ("OK", true, MessageBoxResult.OK) },
            DialogButton.YesNo => new[] { ("Yes", true, MessageBoxResult.Yes), ("No", false, MessageBoxResult.No) },
            DialogButton.YesNoCancel => new[] { ("Yes", true, MessageBoxResult.Yes), ("No", false, MessageBoxResult.No), ("Cancel", false, MessageBoxResult.Cancel) },
            DialogButton.OKCancel => new[] { ("OK", true, MessageBoxResult.OK), ("Cancel", false, MessageBoxResult.Cancel) },
            _ => new[] { ("OK", true, MessageBoxResult.OK) }
        };

        var primaryBg = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#547E9D"));
        var primaryBorder = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#6D95B0"));
        var primaryFg = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#081218"));
        var secondaryBg = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#465C6F"));
        var secondaryBorder = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#627A8D"));
        var secondaryFg = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E7EEF5"));

        foreach (var (label, isPrimary, result) in config)
        {
            var btn = new Button
            {
                Content = label,
                MinWidth = 88,
                MinHeight = 34,
                Padding = new Thickness(14, 7, 14, 7),
                Tag = result,
                FontWeight = FontWeights.SemiBold,
                Cursor = System.Windows.Input.Cursors.Hand,
                Background = isPrimary ? primaryBg : secondaryBg,
                BorderBrush = isPrimary ? primaryBorder : secondaryBorder,
                Foreground = isPrimary ? primaryFg : secondaryFg,
                BorderThickness = new Thickness(1)
            };

            // Corner radius via template
            var template = new ControlTemplate(typeof(Button));
            var border = new FrameworkElementFactory(typeof(Border));
            border.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Button.BackgroundProperty));
            border.SetValue(Border.BorderBrushProperty, new TemplateBindingExtension(Button.BorderBrushProperty));
            border.SetValue(Border.BorderThicknessProperty, new TemplateBindingExtension(Button.BorderThicknessProperty));
            border.SetValue(Border.CornerRadiusProperty, new CornerRadius(8));
            var content = new FrameworkElementFactory(typeof(ContentPresenter));
            content.SetValue(HorizontalAlignmentProperty, HorizontalAlignment.Center);
            content.SetValue(VerticalAlignmentProperty, VerticalAlignment.Center);
            border.AppendChild(content);
            template.VisualTree = border;
            btn.Template = template;

            btn.Click += (s, _) =>
            {
                Result = (MessageBoxResult)((Button)s).Tag;
                DialogResult = true;
                Close();
            };

            ButtonsPanel.Children.Add(btn);

            btn.Margin = new Thickness(4, 0, 4, 0);
        }
    }

}
