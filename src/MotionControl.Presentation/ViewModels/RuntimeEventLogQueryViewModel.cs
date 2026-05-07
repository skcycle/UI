using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MotionControl.Control.Services;
using MotionControl.Presentation.Commands;

namespace MotionControl.Presentation.ViewModels;

/// <summary>
/// SQLite 事件日志查询 ViewModel。
/// 支持按时间/模块/轴号/级别/对象名过滤，DataGrid 展示结果。
/// </summary>
public sealed class RuntimeEventLogQueryViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly EventLogQueryService _queryService;
    private readonly ILogger<RuntimeEventLogQueryViewModel> _logger;
    private CancellationTokenSource? _queryCts;

    public RuntimeEventLogQueryViewModel(EventLogQueryService queryService, ILogger<RuntimeEventLogQueryViewModel>? logger = null)
    {
        _queryService = queryService;
        _logger = logger ?? NullLogger<RuntimeEventLogQueryViewModel>.Instance;

        SearchCommand = new RelayCommand(async () => await SearchAsync());
        ClearCommand = new RelayCommand(Clear);
        LoadRecentCommand = new RelayCommand(async () => await SearchAsync(recent: true));
    }

    // ── 过滤条件 ──

    private DateTime? _fromTime = DateTime.Now.AddHours(-1);
    public DateTime? FromTime { get => _fromTime; set { _fromTime = value; OnPropertyChanged(); } }

    private DateTime? _toTime;
    public DateTime? ToTime { get => _toTime; set { _toTime = value; OnPropertyChanged(); } }

    private string? _selectedModule;
    public string? SelectedModule { get => _selectedModule; set { _selectedModule = value; OnPropertyChanged(); } }

    private int? _selectedAxisNo;
    public int? SelectedAxisNo { get => _selectedAxisNo; set { _selectedAxisNo = value; OnPropertyChanged(); } }

    private string? _selectedLevel;
    public string? SelectedLevel { get => _selectedLevel; set { _selectedLevel = value; OnPropertyChanged(); } }

    private string? _selectedObjectName;
    public string? SelectedObjectName { get => _selectedObjectName; set { _selectedObjectName = value; OnPropertyChanged(); } }

    public string[] ModuleOptions { get; } = { "All", "AxisCommand", "IO", "Cylinder", "Magazine", "WorkHead", "PositionSetup" };
    public string[] LevelOptions { get; } = { "All", "Error", "Warning", "Info" };
    public int[] AxisOptions { get; } = Enumerable.Range(0, 17).ToArray();

    // ── 结果 ──

    private ObservableCollection<RuntimeEventLogItemViewModel> _events = new();
    public ObservableCollection<RuntimeEventLogItemViewModel> Events
    {
        get => _events;
        set { _events = value; OnPropertyChanged(); }
    }

    private string _statusText = "Ready";
    public string StatusText { get => _statusText; set { _statusText = value; OnPropertyChanged(); } }

    private bool _isLoading;
    public bool IsLoading { get => _isLoading; set { _isLoading = value; OnPropertyChanged(); } }

    // ── 诊断 ──

    public long DroppedCount => _queryService.DroppedCount;
    public int PendingCount => _queryService.PendingCount;
    public string QueueStatus => $"Pending: {PendingCount} | Dropped: {DroppedCount}";

    // ── 命令 ──

    public ICommand SearchCommand { get; }
    public ICommand ClearCommand { get; }
    public ICommand LoadRecentCommand { get; }

    private async Task SearchAsync(bool recent = false)
    {
        _queryCts?.Cancel();
        _queryCts = new CancellationTokenSource();
        var ct = _queryCts.Token;

        IsLoading = true;
        StatusText = "Querying...";

        try
        {
            IReadOnlyList<RuntimeEventLogEntry> results;

            if (recent)
            {
                results = await _queryService.QueryRecentAsync(500, ct);
            }
            else
            {
                var module = string.IsNullOrEmpty(SelectedModule) || SelectedModule == "All" ? null : SelectedModule;
                var level = string.IsNullOrEmpty(SelectedLevel) || SelectedLevel == "All" ? null : SelectedLevel;

                results = await _queryService.QueryAsync(
                    fromUtc: FromTime?.ToUniversalTime(),
                    toUtc: ToTime?.ToUniversalTime(),
                    module: module,
                    axisNo: SelectedAxisNo,
                    level: level,
                    objectName: string.IsNullOrWhiteSpace(SelectedObjectName) ? null : SelectedObjectName,
                    ct: ct);
            }

            ct.ThrowIfCancellationRequested();

            Events = new ObservableCollection<RuntimeEventLogItemViewModel>(
                results.Select(e => new RuntimeEventLogItemViewModel(e)));

            StatusText = $"{Events.Count} results at {DateTime.Now:HH:mm:ss}";
        }
        catch (OperationCanceledException)
        {
            StatusText = "Query cancelled";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Event log query failed");
            StatusText = $"Error: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void Clear()
    {
        _queryCts?.Cancel();
        Events.Clear();
        StatusText = "Cleared";
        FromTime = null;
        ToTime = null;
        SelectedModule = null;
        SelectedAxisNo = null;
        SelectedLevel = null;
        SelectedObjectName = null;
    }

    /// <summary>定期刷新队列状态（供外部定时器调用）</summary>
    public void RefreshQueueStatus()
    {
        OnPropertyChanged(nameof(DroppedCount));
        OnPropertyChanged(nameof(PendingCount));
        OnPropertyChanged(nameof(QueueStatus));
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    public void Dispose()
    {
        _queryCts?.Cancel();
        _queryCts?.Dispose();
    }
}
