using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Input;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MotionControl.Control.Services;
using MotionControl.Presentation.Commands;

namespace MotionControl.Presentation.ViewModels;

/// <summary>
/// SQLite 事件日志查询 ViewModel（第二轮优化版）。
/// 支持动态模块/轴过滤、快捷筛选、详情面板、导出、诊断操作。
/// </summary>
public sealed class RuntimeEventLogQueryViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly IEventLogStore _eventLogStore;
    private readonly ILogger<RuntimeEventLogQueryViewModel> _logger;
    private CancellationTokenSource? _queryCts;

    private int _currentPage = 1;
    private const int PageSize = 200;
    private const int MaxQueryRows = 2000;

    public RuntimeEventLogQueryViewModel(EventLogQueryService queryService, ILogger<RuntimeEventLogQueryViewModel>? logger = null)
    {
        _eventLogStore = queryService; // EventLogQueryService implements IEventLogStore
        _logger = logger ?? NullLogger<RuntimeEventLogQueryViewModel>.Instance;

        // Default: Last 30 minutes
        FromTime = DateTime.Now.AddMinutes(-30);
        ToTime = null;

        // Commands
        SearchCommand = new RelayCommand(async () => await SearchAsync());
        ClearCommand = new RelayCommand(Clear);
        CancelCommand = new RelayCommand(CancelQuery);
        ExportCsvCommand = new RelayCommand(async () => await ExportCsvAsync());
        ExportJsonCommand = new RelayCommand(async () => await ExportJsonAsync());
        CopySelectedCommand = new RelayCommand(CopySelected, () => SelectedEvent != null);
        TestWriteCommand = new RelayCommand(TestWrite);
        CleanupCommand = new RelayCommand(async () => await CleanupAsync());
        PrevPageCommand = new RelayCommand(async () => await PrevPageAsync(), () => CurrentPage > 1);
        NextPageCommand = new RelayCommand(async () => await NextPageAsync(), () => Events.Count == PageSize);

        // Quick filters
        Last30MinCommand = new RelayCommand(() => SetTimeFilter(30));
        Last1HourCommand = new RelayCommand(() => SetTimeFilter(60));
        Last24HourCommand = new RelayCommand(() => SetTimeFilter(1440));
        OnlyErrorsCommand = new RelayCommand(() => { SelectedLevel = "Error"; SelectedModule = null; });
        OnlyWarningsCommand = new RelayCommand(() => { SelectedLevel = "Warning"; SelectedModule = null; });
        ClearFiltersCommand = new RelayCommand(ClearFilters);

        // Load module list from DB on first query
        _ = LoadModuleOptionsAsync();
    }

    // ── 过滤条件 ───────────────────────────────────────────────

    private DateTime? _fromTime;
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

    private string? _selectedCommandName;
    public string? SelectedCommandName { get => _selectedCommandName; set { _selectedCommandName = value; OnPropertyChanged(); } }

    private string? _selectedStatus;
    public string? SelectedStatus { get => _selectedStatus; set { _selectedStatus = value; OnPropertyChanged(); } }

    // ── 动态选项 ───────────────────────────────────────────────

    private ObservableCollection<string> _moduleOptions = new() { "All" };
    public ObservableCollection<string> ModuleOptions { get => _moduleOptions; set { _moduleOptions = value; OnPropertyChanged(); } }

    public string[] LevelOptions { get; } = { "All", "Error", "Warning", "Info" };
    public int[] AxisOptions { get; } = Enumerable.Range(0, 17).ToArray();

    // ── 结果 ───────────────────────────────────────────────────

    private ObservableCollection<RuntimeEventLogItemViewModel> _events = new();
    public ObservableCollection<RuntimeEventLogItemViewModel> Events
    {
        get => _events;
        set { _events = value; OnPropertyChanged(); }
    }

    private RuntimeEventLogItemViewModel? _selectedEvent;
    public RuntimeEventLogItemViewModel? SelectedEvent
    {
        get => _selectedEvent;
        set
        {
            _selectedEvent = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasSelectedEvent));
            OnPropertyChanged(nameof(SelectedEventJson));
            OnPropertyChanged(nameof(SelectedEventMessage));
        }
    }

    public bool HasSelectedEvent => _selectedEvent != null;
    public string? SelectedEventMessage => _selectedEvent?.Message;
    public string? SelectedEventJson => FormatJson(_selectedEvent?.PayloadJson);

    private string _statusText = "Ready";
    public string StatusText { get => _statusText; set { _statusText = value; OnPropertyChanged(); } }

    private bool _isLoading;
    public bool IsLoading { get => _isLoading; set { _isLoading = value; OnPropertyChanged(); } }

    private bool _isCancelling;
    public bool IsCancelling { get => _isCancelling; set { _isCancelling = value; OnPropertyChanged(); } }

    // ── 分页 ───────────────────────────────────────────────────

    private int _totalRows;
    public int TotalRows { get => _totalRows; set { _totalRows = value; OnPropertyChanged(); OnPropertyChanged(nameof(PaginationText)); OnPropertyChanged(nameof(HasPagination)); } }
    public string PaginationText => HasPagination ? $"{(CurrentPage - 1) * PageSize + 1}–{Math.Min(CurrentPage * PageSize, TotalRows)} of {TotalRows}" : "";
    public bool HasPagination => TotalRows > PageSize;
    public int CurrentPage { get => _currentPage; set { _currentPage = value; OnPropertyChanged(); OnPropertyChanged(nameof(PaginationText)); } }

    // ── 诊断 ───────────────────────────────────────────────────

    public long DroppedCount => _eventLogStore.DroppedCount;
    public int PendingCount => _eventLogStore.PendingCount;
    public string DatabasePath => _eventLogStore.DatabasePath;
    public int RetentionDays => _eventLogStore.RetentionDays;

    public string DiagnosticsText =>
        $"DB: {DatabasePath} | Retention: {RetentionDays}d | Pending: {PendingCount} | Dropped: {DroppedCount}";

    // ── 命令 ───────────────────────────────────────────────────

    public ICommand SearchCommand { get; }
    public ICommand ClearCommand { get; }
    public ICommand CancelCommand { get; }
    public ICommand ExportCsvCommand { get; }
    public ICommand ExportJsonCommand { get; }
    public ICommand CopySelectedCommand { get; }
    public ICommand TestWriteCommand { get; }
    public ICommand CleanupCommand { get; }
    public ICommand PrevPageCommand { get; }
    public ICommand NextPageCommand { get; }

    // Quick filter commands
    public ICommand Last30MinCommand { get; }
    public ICommand Last1HourCommand { get; }
    public ICommand Last24HourCommand { get; }
    public ICommand OnlyErrorsCommand { get; }
    public ICommand OnlyWarningsCommand { get; }
    public ICommand ClearFiltersCommand { get; }

    // ── 查询 ───────────────────────────────────────────────────

    private async Task SearchAsync()
    {
        _queryCts?.Cancel();
        _queryCts = new CancellationTokenSource();
        var ct = _queryCts.Token;

        IsLoading = true;
        StatusText = "Querying...";
        CurrentPage = 1;
        RefreshDiagnostics();

        try
        {
            var module = string.IsNullOrEmpty(SelectedModule) || SelectedModule == "All" ? null : SelectedModule;
            var level = string.IsNullOrEmpty(SelectedLevel) || SelectedLevel == "All" ? null : SelectedLevel;

            var fromUtc = FromTime?.ToUniversalTime();
            var toUtc = ToTime?.ToUniversalTime();

            // Warn if range is large
            if (fromUtc.HasValue && toUtc.HasValue && (toUtc.Value - fromUtc.Value).TotalDays > 1)
            {
                StatusText = $"⚠ Large range ({(toUtc.Value - fromUtc.Value).TotalDays:F0}d), this may take a moment...";
            }

            var results = await Task.Run(() =>
                _eventLogStore.QueryAsync(fromUtc, toUtc, module, SelectedAxisNo, level,
                    string.IsNullOrWhiteSpace(SelectedObjectName) ? null : SelectedObjectName, ct), ct);

            ct.ThrowIfCancellationRequested();

            TotalRows = results.Count;
            Events = new ObservableCollection<RuntimeEventLogItemViewModel>(
                results.Take(PageSize).Select(e => new RuntimeEventLogItemViewModel(e)));

            StatusText = $"{TotalRows} results | {DateTime.Now:HH:mm:ss}";
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
            IsCancelling = false;
            RefreshDiagnostics();
        }
    }

    private async Task NextPageAsync()
    {
        if (Events.Count < PageSize) return;
        CurrentPage++;
        await SearchPageAsync();
    }

    private async Task PrevPageAsync()
    {
        if (CurrentPage <= 1) return;
        CurrentPage--;
        await SearchPageAsync();
    }

    private async Task SearchPageAsync()
    {
        _queryCts?.Cancel();
        _queryCts = new CancellationTokenSource();
        var ct = _queryCts.Token;

        IsLoading = true;
        StatusText = $"Page {CurrentPage}...";

        try
        {
            var module = string.IsNullOrEmpty(SelectedModule) || SelectedModule == "All" ? null : SelectedModule;
            var level = string.IsNullOrEmpty(SelectedLevel) || SelectedLevel == "All" ? null : SelectedLevel;

            var results = await Task.Run(() =>
                _eventLogStore.QueryAsync(
                    FromTime?.ToUniversalTime(),
                    ToTime?.ToUniversalTime(),
                    module,
                    SelectedAxisNo,
                    level,
                    string.IsNullOrWhiteSpace(SelectedObjectName) ? null : SelectedObjectName,
                    ct), ct);

            Events = new ObservableCollection<RuntimeEventLogItemViewModel>(
                results.Skip((CurrentPage - 1) * PageSize).Take(PageSize)
                    .Select(e => new RuntimeEventLogItemViewModel(e)));

            StatusText = $"Page {CurrentPage} | {TotalRows} total | {DateTime.Now:HH:mm:ss}";
        }
        catch (OperationCanceledException)
        {
            StatusText = "Cancelled";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void CancelQuery()
    {
        if (!IsLoading) return;
        IsCancelling = true;
        StatusText = "Cancelling...";
        _queryCts?.Cancel();
    }

    // ── 动态加载模块列表 ───────────────────────────────────────

    private async Task LoadModuleOptionsAsync()
    {
        try
        {
            var modules = await _eventLogStore.GetDistinctModulesAsync();
            System.Windows.Application.Current?.Dispatcher?.Invoke(() =>
            {
                ModuleOptions = new ObservableCollection<string>(new[] { "All" }.Concat(modules));
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load distinct modules");
        }
    }

    // ── 工具方法 ───────────────────────────────────────────────

    private void Clear()
    {
        _queryCts?.Cancel();
        Events.Clear();
        SelectedEvent = null;
        FromTime = DateTime.Now.AddMinutes(-30);
        ToTime = null;
        SelectedModule = null;
        SelectedAxisNo = null;
        SelectedLevel = null;
        SelectedObjectName = null;
        SelectedCommandName = null;
        SelectedStatus = null;
        TotalRows = 0;
        CurrentPage = 1;
        StatusText = "Cleared";
    }

    private void ClearFilters()
    {
        SelectedModule = null;
        SelectedAxisNo = null;
        SelectedLevel = null;
        SelectedObjectName = null;
        SelectedCommandName = null;
        SelectedStatus = null;
        FromTime = DateTime.Now.AddMinutes(-30);
        ToTime = null;
    }

    private void SetTimeFilter(int minutes)
    {
        FromTime = DateTime.Now.AddMinutes(-minutes);
        ToTime = null;
    }

    private void TestWrite()
    {
        var modules = new[] { "AxisCommand", "IO", "Cylinder", "Magazine", "WorkHead", "PositionSetup" };
        var rnd = new Random();
        var module = modules[rnd.Next(modules.Length)];
        _eventLogStore.EnqueueTestEntry(module, "TestEvent", "Info", $"Debug test at {DateTime.Now:HH:mm:ss}");
        RefreshDiagnostics();
        StatusText = $"Test entry written to {module}";
    }

    private async Task CleanupAsync()
    {
        try
        {
            var cutoff = DateTime.UtcNow.AddDays(-_eventLogStore.RetentionDays);
            var deleted = await _eventLogStore.DeleteOlderThanAsync(cutoff);
            StatusText = $"Deleted {deleted} entries older than {_eventLogStore.RetentionDays} days";
            RefreshDiagnostics();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Cleanup failed");
            StatusText = $"Cleanup error: {ex.Message}";
        }
    }

    private void CopySelected()
    {
        if (SelectedEvent == null) return;
        var text = $"[{SelectedEvent.TimestampLocal}] [{SelectedEvent.Level}] [{SelectedEvent.Module}] {SelectedEvent.EventType}: {SelectedEvent.Message}";
        try
        {
            Clipboard.SetText(text);
            StatusText = "Copied to clipboard";
        }
        catch { /* ignore */ }
    }

    private async Task ExportCsvAsync()
    {
        if (Events.Count == 0) return;

        var sb = new StringBuilder();
        sb.AppendLine("Timestamp,Module,EventType,Level,AxisNo,ObjectName,Address,IO,CommandName,Status,Message,PayloadJson");

        foreach (var e in Events)
        {
            sb.AppendLine($"\"{e.TimestampLocal}\",\"{e.Module}\",\"{e.EventType}\",\"{e.Level}\"," +
                          $"\"{e.AxisNo}\",\"{e.ObjectName}\",\"{e.Address}\",\"{e.IO}\"," +
                          $"\"{e.CommandName}\",\"{e.Status}\",\"{EscapeCsv(e.Message)}\",\"{EscapeCsv(e.PayloadJson)}\"");
        }

        await SaveFileAsync(sb.ToString(), "event_log.csv", "CSV files (*.csv)|*.csv");
    }

    private async Task ExportJsonAsync()
    {
        if (Events.Count == 0) return;

        var json = JsonSerializer.Serialize(Events.Select(e => new
        {
            e.TimestampLocal,
            e.Module,
            e.EventType,
            e.Level,
            e.AxisNo,
            e.ObjectName,
            e.Address,
            e.IO,
            e.CommandName,
            e.Status,
            e.Message,
            e.PayloadJson
        }), new JsonSerializerOptions { WriteIndented = true });

        await SaveFileAsync(json, "event_log.json", "JSON files (*.json)|*.json");
    }

    private async Task SaveFileAsync(string content, string defaultFileName, string filter)
    {
        try
        {
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                FileName = defaultFileName,
                Filter = filter
            };

            if (dialog.ShowDialog() == true)
            {
                await System.IO.File.WriteAllTextAsync(dialog.FileName, content, Encoding.UTF8);
                StatusText = $"Exported to {System.IO.Path.GetFileName(dialog.FileName)}";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Export failed");
            StatusText = $"Export error: {ex.Message}";
        }
    }

    private static string EscapeCsv(string? s)
        => (s ?? "").Replace("\"", "\"\"").Replace("\n", " ").Replace("\r", "");

    private static string? FormatJson(string? json)
    {
        if (string.IsNullOrEmpty(json)) return null;
        try
        {
            var doc = JsonDocument.Parse(json);
            return JsonSerializer.Serialize(doc, new JsonSerializerOptions { WriteIndented = true });
        }
        catch { return json; }
    }

    private void RefreshDiagnostics()
    {
        OnPropertyChanged(nameof(DroppedCount));
        OnPropertyChanged(nameof(PendingCount));
        OnPropertyChanged(nameof(DiagnosticsText));
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
