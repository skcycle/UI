using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using MotionControl.Device.Abstractions.Controllers;
using MotionControl.Device.Abstractions.Models;
using MotionControl.Domain.Entities;
using MotionControl.Presentation.Commands;

namespace MotionControl.Presentation.ViewModels;

/// <summary>
/// 轴运动数据采集与曲线绘制 ViewModel。
/// 以 1ms 频率采集 Speed、Cmd Position、Encoder Position，
/// 支持信号选择、采集时长配置、自动 Y 轴缩放。
/// 采用双 Y 轴：左轴 = Speed，右轴 = Position (Cmd/Enc 共用)。
/// </summary>
public sealed class AxisDataCaptureViewModel : INotifyPropertyChanged
{
    private readonly Machine _machine;
    private readonly Func<bool> _canControlAxis;
    private CancellationTokenSource? _captureCts;

    public sealed class CapturePoint
    {
        public double TimeMs { get; init; }
        public double Speed { get; init; }
        public double CmdPositionMm { get; init; }
        public double EncPositionMm { get; init; }
    }

    public AxisDataCaptureViewModel(Machine machine, Func<bool> canControlAxis, IAxisMotionController motionController)
    {
        _machine = machine;
        _canControlAxis = canControlAxis;
        _motionController = motionController;

        StartCaptureCommand = new RelayCommand(async () => await StartCaptureAsync(), () => !IsCapturing && CanControl());
        StopCaptureCommand = new RelayCommand(StopCapture, () => IsCapturing);
        ClearCommand = new RelayCommand(Clear, () => DataPointCount > 0 && !IsCapturing);
        CaptureDurations = new[] { 500, 1000, 2000, 5000, 10000 };
    }

    // --- 信号选择 ---
    private bool _showSpeed = true;
    public bool ShowSpeed
    {
        get => _showSpeed;
        set { if (_showSpeed == value) return; _showSpeed = value; OnPropertyChanged(); UpdateChart(); }
    }

    private bool _showCmdPosition = true;
    public bool ShowCmdPosition
    {
        get => _showCmdPosition;
        set { if (_showCmdPosition == value) return; _showCmdPosition = value; OnPropertyChanged(); UpdateChart(); }
    }

    private bool _showEncPosition = true;
    public bool ShowEncPosition
    {
        get => _showEncPosition;
        set { if (_showEncPosition == value) return; _showEncPosition = value; OnPropertyChanged(); UpdateChart(); }
    }

    // --- 采集参数 ---
    private int _captureDurationMs = 1000;
    public int CaptureDurationMs
    {
        get => _captureDurationMs;
        set { if (_captureDurationMs == value) return; _captureDurationMs = value; OnPropertyChanged(); }
    }

    public IReadOnlyList<int> CaptureDurations { get; }

    private bool _isCapturing;
    public bool IsCapturing
    {
        get => _isCapturing;
        private set
        {
            if (_isCapturing == value) return;
            _isCapturing = value;
            OnPropertyChanged();
            StartCaptureCommand.RaiseCanExecuteChanged();
            StopCaptureCommand.RaiseCanExecuteChanged();
            ClearCommand.RaiseCanExecuteChanged();
        }
    }

    private int _dataPointCount;
    public int DataPointCount
    {
        get => _dataPointCount;
        private set { if (_dataPointCount == value) return; _dataPointCount = value; OnPropertyChanged(); ClearCommand.RaiseCanExecuteChanged(); }
    }

    private double _lastTimeMs;

    // Internal list for fast capture; converted to chart data on update
    private readonly List<CapturePoint> _captureBuffer = new();
    private readonly IAxisMotionController _motionController;

    // --- 图表数据 (绑定到 Polyline) ---
    private PointCollection? _speedLinePoints;
    public PointCollection? SpeedLinePoints
    {
        get => _speedLinePoints;
        private set { _speedLinePoints = value; OnPropertyChanged(); }
    }

    // Speed 使用右 Y 轴（mm/s）
    private PointCollection? _speedLinePointsRight;
    public PointCollection? SpeedLinePointsRight
    {
        get => _speedLinePointsRight;
        private set { _speedLinePointsRight = value; OnPropertyChanged(); }
    }

    public string SpeedUnitLabelRightY => "mm/s";

    private PointCollection? _cmdLinePoints;
    public PointCollection? CmdLinePoints
    {
        get => _cmdLinePoints;
        private set { _cmdLinePoints = value; OnPropertyChanged(); }
    }

    private PointCollection? _encLinePoints;
    public PointCollection? EncLinePoints
    {
        get => _encLinePoints;
        private set { _encLinePoints = value; OnPropertyChanged(); }
    }

    // --- 画布尺寸 ---
    private double _canvasWidth = 600;
    private double _canvasHeight = 280;

    public void UpdateCanvasSize(double width, double height)
    {
        if (Math.Abs(_canvasWidth - width) < 0.5 && Math.Abs(_canvasHeight - height) < 0.5)
            return;
        _canvasWidth = width;
        _canvasHeight = height;
        UpdateChart();
    }

    // --- 轴范围 ---
    private double _speedMax;
    public double SpeedMax
    {
        get => _speedMax;
        set { if (Math.Abs(_speedMax - value) < 0.01) return; _speedMax = value; OnPropertyChanged(); }
    }

    private double _positionMax;
    public double PositionMax
    {
        get => _positionMax;
        set { if (Math.Abs(_positionMax - value) < 0.01) return; _positionMax = value; OnPropertyChanged(); }
    }

    private double _valueMax;
    public double ValueMax
    {
        get => _valueMax;
        set { if (Math.Abs(_valueMax - value) < 0.01) return; _valueMax = value; OnPropertyChanged(); }
    }

    private double _timeMax;
    public double TimeMax
    {
        get => _timeMax;
        set { if (Math.Abs(_timeMax - value) < 0.1) return; _timeMax = value; OnPropertyChanged(); }
    }

    // --- 边距常量 ---
    private const double MarginLeft = 50;
    private const double MarginRight = 20;
    private const double MarginTop = 20;
    private const double MarginBottom = 30;

    public RelayCommand StartCaptureCommand { get; }
    public RelayCommand StopCaptureCommand { get; }
    public RelayCommand ClearCommand { get; }

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// 强制刷新所有命令的 CanExecute 状态（供外部调用）。
    /// </summary>
    public void RefreshCommandStates()
    {
        StartCaptureCommand.RaiseCanExecuteChanged();
        StopCaptureCommand.RaiseCanExecuteChanged();
        ClearCommand.RaiseCanExecuteChanged();
    }

    private bool CanControl() => _canControlAxis();

    private int _selectedAxisNo;

    public void SetSelectedAxisNo(int axisNo)
    {
        if (_selectedAxisNo == axisNo) return;
        _selectedAxisNo = axisNo;
        Clear();
    }

    private async Task StartCaptureAsync()
    {
        _captureBuffer.Clear();
        DataPointCount = 0;
        _lastTimeMs = 0;
        IsCapturing = true;

        _captureCts = new CancellationTokenSource();
        var token = _captureCts.Token;
        var durationMs = CaptureDurationMs;
        var axisNo = _selectedAxisNo;
        var pulseEq = 1000.0;
        var axis = _machine.Axes.FirstOrDefault(a => a.Id.Value == axisNo);
        if (axis != null && axis.PulseEquivalent > 0)
            pulseEq = axis.PulseEquivalent;

        // 整个采集循环跑在一个后台 Task.Run 内，同步读 DLL 避免反复线程池调度
        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        await Task.Run(() =>
        {
            var sw = Stopwatch.StartNew();
            var sampleIndex = 0;

            while (sw.ElapsedMilliseconds < durationMs && !token.IsCancellationRequested)
            {
                var t0 = sw.ElapsedMilliseconds;
                var snap = GetCaptureSnapshotSync(axisNo);
                if (snap.IsValid)
                {
                    _captureBuffer.Add(new CapturePoint
                    {
                        TimeMs = t0,
                        Speed = snap.MotorSpeed / pulseEq,
                        CmdPositionMm = snap.CommandPosition / pulseEq,
                        EncPositionMm = snap.EncoderPosition / pulseEq
                    });
                }

                // 目标 1ms 间隔，保证 ≥1ms 防止刷爆 DLL
                sampleIndex++;
                var targetMs = sampleIndex; // 1ms per sample
                var remainMs = (int)(targetMs - sw.ElapsedMilliseconds);
                if (remainMs < 1) remainMs = 1;
                if (token.IsCancellationRequested) break;
                try { System.Threading.Thread.Sleep(remainMs); }
                catch { break; }
            }

            dispatcher?.Invoke(() =>
            {
                IsCapturing = false;
                _lastTimeMs = _captureBuffer.Count > 0 ? _captureBuffer[^1].TimeMs : 0;
                DataPointCount = _captureBuffer.Count;
                UpdateChart();
            });
        }, token);

        _captureCts?.Dispose();
        _captureCts = null;
    }

    private AxisCaptureSnapshot GetCaptureSnapshotSync(int axisNo)
    {
        return _motionController.GetAxisCaptureSnapshot(axisNo);
    }

    private void StopCapture()
    {
        _captureCts?.Cancel();
    }

    private void Clear()
    {
        _captureBuffer.Clear();
        DataPointCount = 0;
        _lastTimeMs = 0;
        SpeedLinePoints = null;
        SpeedLinePointsRight = null;
        CmdLinePoints = null;
        EncLinePoints = null;
        SpeedMax = 0;
        PositionMax = 0;
        TimeMax = 0;
        TimeMax = 0;
    }

    /// <summary>
    /// 将捕捉缓冲区的数据点映射到 Canvas 坐标系并构建 Polyline。
    /// X 轴 = 数据值（Speed/Cmd/Enc 共用），Y 轴 = 时间（0 在底部）。
    /// 坐标轴随数据最大值自动缩放（含 15% 上边距）。
    /// </summary>
    public void UpdateChart()
    {
        if (_captureBuffer.Count == 0)
        {
            SpeedLinePoints = null;
            CmdLinePoints = null;
            EncLinePoints = null;
            SpeedMax = 0;
            PositionMax = 0;
            ValueMax = 0;
            TimeMax = 0;
            return;
        }

        var canvasW = _canvasWidth - MarginLeft - MarginRight;
        var canvasH = _canvasHeight - MarginTop - MarginBottom;
        if (canvasW <= 0 || canvasH <= 0) return;

        var timeRange = _captureBuffer[^1].TimeMs;
        if (timeRange <= 0) timeRange = 1;
        TimeMax = timeRange;

        // 自动计算 Y 轴范围（数据值，完整正负范围）
        var posRange = 1.0;
        if (ShowCmdPosition || ShowEncPosition)
        {
            var pmax = 0.0;
            if (ShowCmdPosition) pmax = _captureBuffer.Max(p => Math.Abs(p.CmdPositionMm));
            if (ShowEncPosition) pmax = Math.Max(pmax, _captureBuffer.Max(p => Math.Abs(p.EncPositionMm)));
            posRange = pmax > 0 ? pmax * 1.15 : 1.0;
        }

        // 右 Y 轴（Speed mm/s）：完整范围 [min, max]，包含正负
        double speedMin = 0, speedMax = 0;
        if (ShowSpeed && _captureBuffer.Count > 0)
        {
            speedMin = _captureBuffer.Min(p => p.Speed);
            speedMax = _captureBuffer.Max(p => p.Speed);
            // 确保范围至少有一点空间
            if (Math.Abs(speedMax - speedMin) < 0.01)
            {
                if (speedMax > 0) { speedMin = 0; }
                else if (speedMax < 0) { speedMax = 0; }
                else { speedMax = 1; speedMin = -1; }
            }
        }
        if (ShowCmdPosition || ShowEncPosition)
        {
            var pmax = 0.0;
            if (ShowCmdPosition) pmax = _captureBuffer.Max(p => Math.Abs(p.CmdPositionMm));
            if (ShowEncPosition) pmax = Math.Max(pmax, _captureBuffer.Max(p => Math.Abs(p.EncPositionMm)));
            posRange = pmax > 0 ? pmax * 1.15 : 1.0;
        }

        PositionMax = Math.Ceiling(posRange);

        // SpeedMax/Min 供 XAML.cs 轴标签使用
        SpeedMax = Math.Ceiling(Math.Max(Math.Abs(speedMin), Math.Abs(speedMax)));

        // X = 时间（从左到右），Y = 数据值（0 在底部，向上增长）
        // 左 Y 轴 = 位置（mm, Cmd/Enc），右 Y 轴 = 速度（mm/s, Speed）
        double MapXTime(double t) => MarginLeft + t / timeRange * canvasW;
        double MapYLeft(double v) => MarginTop + canvasH - v / posRange * canvasH;  // 位置 mm
        // 右 Y 轴：speedMin→底部(speedMin在下方)，speedMax→顶部(speedMax在上方)
        double MapYRight(double v) => MarginTop + canvasH - (v - speedMin) / (speedMax - speedMin) * (canvasH - MarginTop - MarginBottom);

        // Cmd/Enc → 左 Y 轴（mm）
        CmdLinePoints = ShowCmdPosition
            ? new PointCollection(_captureBuffer.Select(p => new Point(MapXTime(p.TimeMs), MapYLeft(p.CmdPositionMm))))
            : null;

        EncLinePoints = ShowEncPosition
            ? new PointCollection(_captureBuffer.Select(p => new Point(MapXTime(p.TimeMs), MapYLeft(p.EncPositionMm))))
            : null;

        // Speed → 右 Y 轴（mm/s）
        SpeedLinePoints = null; // 占用左 Y 轴的旧数据废弃
        SpeedLinePointsRight = ShowSpeed
            ? new PointCollection(_captureBuffer.Select(p => new Point(MapXTime(p.TimeMs), MapYRight(p.Speed))))
            : null;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
