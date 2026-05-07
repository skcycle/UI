using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using MotionControl.Presentation.ViewModels;

namespace MotionControl.App.Controls;

public partial class AxisConsolePanel : UserControl
{
    private const int JogHoldThresholdMs = 300;

    private MainWindowViewModel? ViewModel => DataContext as MainWindowViewModel;

    private bool _jogHolding;
    private bool _jogContinuousRunning;
    private bool _jogPositiveDirection;
    private DispatcherTimer? _jogHoldTimer;
    private bool _initialized;

    public AxisConsolePanel()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_initialized || ViewModel is null) return;
        _initialized = true;

        // Redraw chart grid when axis data capture properties change
        ViewModel.AxisDataCapture.PropertyChanged += (s, args) =>
        {
            if (args.PropertyName is nameof(AxisDataCaptureViewModel.ShowSpeed)
                or nameof(AxisDataCaptureViewModel.ShowCmdPosition)
                or nameof(AxisDataCaptureViewModel.ShowEncPosition)
                or nameof(AxisDataCaptureViewModel.SpeedMax)
                or nameof(AxisDataCaptureViewModel.PositionMax)
                or nameof(AxisDataCaptureViewModel.TimeMax))
            {
                Dispatcher.Invoke(() =>
                {
                    if (FindName("MotionCurveCanvas") is Canvas canvas)
                        DrawChartGrid(canvas);
                });
            }
        };
    }

    // ── Jog ──

    private void JogPositiveButton_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (ViewModel is null) return;

        e.Handled = true;
        ((UIElement)sender).CaptureMouse();

        _jogHolding = true;
        _jogContinuousRunning = false;
        _jogPositiveDirection = true;

        _jogHoldTimer?.Stop();
        _jogHoldTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(JogHoldThresholdMs) };
        _jogHoldTimer.Tick += OnJogHoldTimerTick;
        _jogHoldTimer.Start();
    }

    private void JogNegativeButton_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (ViewModel is null) return;

        e.Handled = true;
        ((UIElement)sender).CaptureMouse();

        _jogHolding = true;
        _jogContinuousRunning = false;
        _jogPositiveDirection = false;

        _jogHoldTimer?.Stop();
        _jogHoldTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(JogHoldThresholdMs) };
        _jogHoldTimer.Tick += OnJogHoldTimerTick;
        _jogHoldTimer.Start();
    }

    private void JogButton_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (ViewModel is null) return;

        e.Handled = true;
        ((UIElement)sender).ReleaseMouseCapture();

        if (_jogHoldTimer != null)
        {
            _jogHoldTimer.Stop();
            _jogHoldTimer.Tick -= OnJogHoldTimerTick;
            _jogHoldTimer = null;
        }

        _jogHolding = false;

        if (_jogContinuousRunning)
        {
            _jogContinuousRunning = false;
            _ = ViewModel.AxisDebug.StopJogAsync();
        }
        else
        {
            _ = ViewModel.AxisDebug.StepMoveAsync(_jogPositiveDirection);
        }
    }

    private void JogButton_MouseLeave(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed)
        {
            _jogHolding = false;

            if (_jogHoldTimer != null)
            {
                _jogHoldTimer.Stop();
                _jogHoldTimer.Tick -= OnJogHoldTimerTick;
                _jogHoldTimer = null;
            }
        }
    }

    private void OnJogHoldTimerTick(object? sender, EventArgs e)
    {
        if (_jogHoldTimer != null)
        {
            _jogHoldTimer.Stop();
            _jogHoldTimer.Tick -= OnJogHoldTimerTick;
            _jogHoldTimer = null;
        }

        if (!_jogHolding || ViewModel is null) return;

        _jogContinuousRunning = true;
        _ = ViewModel.AxisDebug.StartJogAsync(_jogPositiveDirection);
    }

    // ── Axis Monitor DataGrid scroll ──

    private void AxisMonitorDataGrid_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        var scrollViewer = ScrollHelper.FindVisualChild<ScrollViewer>(this);
        if (scrollViewer != null)
        {
            scrollViewer.ScrollToVerticalOffset(scrollViewer.VerticalOffset - e.Delta / 3.0);
            e.Handled = true;
        }
    }

    // ── Curve Canvas ──

    private void MotionCurveCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (sender is Canvas canvas && ViewModel?.AxisDataCapture != null)
        {
            ViewModel.AxisDataCapture.UpdateCanvasSize(canvas.ActualWidth, canvas.ActualHeight);
            DrawChartGrid(canvas);
        }
    }

    private void DrawChartGrid(Canvas canvas)
    {
        var vm = ViewModel?.AxisDataCapture;
        if (vm == null) return;

        // Remove old grid/label elements (keep data polylines)
        var toRemove = canvas.Children.OfType<UIElement>()
            .Where(c => c is not Polyline && c is FrameworkElement fe && fe.Tag?.ToString() == "chart-label")
            .ToList();
        foreach (var item in toRemove)
            canvas.Children.Remove(item);

        const double marginLeft = 50, marginRight = 20, marginTop = 20, marginBottom = 32;
        var width = canvas.ActualWidth;
        var height = canvas.ActualHeight;
        var plotWidth = width - marginLeft - marginRight;
        var plotHeight = height - marginTop - marginBottom;
        if (plotWidth <= 0 || plotHeight <= 0) return;

        var gridBrush = new SolidColorBrush(Color.FromRgb(0x1E, 0x2E, 0x3D));

        // Horizontal grid lines (5 lines = 4 segments)
        for (int i = 0; i <= 4; i++)
        {
            double y = marginTop + (plotHeight * i / 4.0);
            canvas.Children.Add(new Line
            {
                X1 = marginLeft, Y1 = y, X2 = width - marginRight, Y2 = y,
                Stroke = gridBrush, StrokeThickness = 0.5,
                Tag = "chart-label"
            });
        }

        // Vertical grid lines (6 lines = 5 segments)
        for (int i = 0; i <= 5; i++)
        {
            double x = marginLeft + (plotWidth * i / 5.0);
            canvas.Children.Add(new Line
            {
                X1 = x, Y1 = marginTop, X2 = x, Y2 = height - marginBottom,
                Stroke = gridBrush, StrokeThickness = 0.5,
                Tag = "chart-label"
            });
        }

        var axisBrush = new SolidColorBrush(Color.FromRgb(0x44, 0x5A, 0x6C));

        // Axes
        canvas.Children.Add(new Line
        {
            X1 = marginLeft, Y1 = marginTop, X2 = marginLeft, Y2 = height - marginBottom,
            Stroke = axisBrush, StrokeThickness = 1, Tag = "chart-label"
        });
        canvas.Children.Add(new Line
        {
            X1 = marginLeft, Y1 = height - marginBottom, X2 = width - marginRight, Y2 = height - marginBottom,
            Stroke = axisBrush, StrokeThickness = 1, Tag = "chart-label"
        });
        canvas.Children.Add(new Line
        {
            X1 = width - marginRight, Y1 = marginTop, X2 = width - marginRight, Y2 = height - marginBottom,
            Stroke = axisBrush, StrokeThickness = 1, Tag = "chart-label"
        });

        var labelBrush = new SolidColorBrush(Color.FromRgb(0x7A, 0x8E, 0x9E));
        var labelFontSize = 9.0;

        // Left Y axis = Position (mm)
        if (vm.ShowCmdPosition || vm.ShowEncPosition)
        {
            var posMax = vm.PositionMax > 0 ? vm.PositionMax : 10;
            for (int i = 0; i <= 4; i++)
            {
                double val = posMax * (4 - i) / 4.0;
                double y = marginTop + (plotHeight * i / 4.0);
                var tb = new TextBlock
                {
                    Text = val.ToString("F1"),
                    Foreground = labelBrush,
                    FontSize = labelFontSize,
                    Tag = "chart-label"
                };
                Canvas.SetLeft(tb, 3);
                Canvas.SetTop(tb, y - 7);
                canvas.Children.Add(tb);
            }
            var leftUnitTb = new TextBlock
            {
                Text = "mm", Foreground = labelBrush, FontSize = labelFontSize, Tag = "chart-label"
            };
            Canvas.SetLeft(leftUnitTb, 3);
            Canvas.SetTop(leftUnitTb, marginTop - 24);
            canvas.Children.Add(leftUnitTb);
        }

        // Right Y axis = Speed (mm/s)
        if (vm.ShowSpeed)
        {
            double speedMax = vm.SpeedMax > 0 ? vm.SpeedMax : 10;
            double speedMin = -speedMax;
            for (int i = 0; i <= 4; i++)
            {
                double fraction = (4.0 - i) / 4.0;
                double val = speedMin + (speedMax - speedMin) * fraction;
                double y = marginTop + plotHeight * fraction;
                var tb = new TextBlock
                {
                    Text = val.ToString("F0"),
                    Foreground = labelBrush,
                    FontSize = labelFontSize,
                    Tag = "chart-label"
                };
                Canvas.SetLeft(tb, width - marginRight + 4);
                Canvas.SetTop(tb, y - 7);
                canvas.Children.Add(tb);
            }
            var rightUnitTb = new TextBlock
            {
                Text = "mm/s", Foreground = labelBrush, FontSize = labelFontSize, Tag = "chart-label"
            };
            Canvas.SetLeft(rightUnitTb, width - marginRight - 6);
            Canvas.SetTop(rightUnitTb, marginTop - 24);
            canvas.Children.Add(rightUnitTb);
        }

        // Time labels (X axis)
        var timeMax = vm.TimeMax > 0 ? vm.TimeMax : 1000;
        for (int i = 0; i <= 5; i++)
        {
            double val = timeMax * i / 5.0;
            double x = marginLeft + (plotWidth * i / 5.0);
            var tb = new TextBlock
            {
                Text = val.ToString("F0"),
                Foreground = labelBrush,
                FontSize = labelFontSize,
                Tag = "chart-label"
            };
            Canvas.SetLeft(tb, x - 12);
            Canvas.SetTop(tb, height - marginBottom + 5);
            canvas.Children.Add(tb);
        }
        var xUnitTb = new TextBlock
        {
            Text = "ms", Foreground = labelBrush, FontSize = labelFontSize, Tag = "chart-label"
        };
        Canvas.SetLeft(xUnitTb, width - marginRight - 16);
        Canvas.SetTop(xUnitTb, height - marginBottom + 12);
        canvas.Children.Add(xUnitTb);

        // Legend
        double legendX = marginLeft + 8;
        double legendY = marginTop + 4;
        var legendItems = new[]
        {
            (Text: "Speed", Color: Color.FromRgb(0xF0, 0xC0, 0x40), Show: vm.ShowSpeed),
            (Text: "Cmd Pos", Color: Color.FromRgb(0x4F, 0xC3, 0xF7), Show: vm.ShowCmdPosition),
            (Text: "Enc Pos", Color: Color.FromRgb(0x81, 0xC7, 0x84), Show: vm.ShowEncPosition),
        };
        foreach (var (text, color, show) in legendItems)
        {
            if (!show) continue;
            var dot = new Ellipse
            {
                Width = 7, Height = 7,
                Fill = new SolidColorBrush(color),
                Stroke = new SolidColorBrush(Color.FromRgb(0x44, 0x5A, 0x6C)),
                StrokeThickness = 0.5,
                Tag = "chart-label"
            };
            Canvas.SetLeft(dot, legendX);
            Canvas.SetTop(dot, legendY + 3);
            canvas.Children.Add(dot);
            var tb = new TextBlock
            {
                Text = text,
                Foreground = new SolidColorBrush(color),
                FontSize = 11,
                Tag = "chart-label"
            };
            Canvas.SetLeft(tb, legendX + 11);
            Canvas.SetTop(tb, legendY);
            canvas.Children.Add(tb);
            legendX += 72;
        }
    }

    // ── Axis Parameters scroll ──

    private void AxisParametersScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (sender is ScrollViewer scrollViewer)
        {
            scrollViewer.ScrollToVerticalOffset(scrollViewer.VerticalOffset - e.Delta / 3.0);
            e.Handled = true;
        }
    }

    // ── Section DataGrid scroll (for any DataGrid in this panel that uses it) ──

    private void SectionDataGrid_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        ScrollHelper.HandleDataGridMouseWheel(sender, e, this);
    }
}
