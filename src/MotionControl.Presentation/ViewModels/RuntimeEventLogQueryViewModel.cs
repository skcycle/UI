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
/// 保存的筛选模板条目
/// </summary>
public sealed class SavedFilterEntry
{
    public string Name { get; init; } = string.Empty;
    public DateTime? FromTime { get; init; }
    public DateTime? ToTime { get; init; }
    public string? Module { get; init; }
    public int? AxisNo { get; init; }
    public string? Level { get; init; }
    public string? ObjectName { get; init; }
    public string? CommandName { get; init; }
    public string? Status { get; init; }
    public string? MessageSearch { get; init; }
    public override string ToString() => Name;
}

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

    public RuntimeEventLogQueryViewModel(EventLogQueryService queryService, ILogger<RuntimeEventLogQueryViewModel>? logger = null)
    {
        _eventLogStore = queryService; // EventLogQueryService implements IEventLogStore
        _logger = logger ?? NullLogger<RuntimeEventLogQueryViewModel>.Instance;

        // Default: Last 30 minutes
        FromTime = DateTime.Now.AddMinutes(-30);
        ToTime = null;
        _fromTimeText = FromTime?.ToString("yyyy-MM-dd HH:mm:ss") ?? "";
        _toTimeText = "";

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
        ExportAllResultsCommand = new RelayCommand(async () => await ExportAllResultsAsync());
        TraceModuleCommand = new RelayCommand<string?>(TraceModule);
        TraceObjectCommand = new RelayCommand<string?>(TraceObject);
        SaveFilterCommand = new RelayCommand(SaveCurrentFilter, () => !string.IsNullOrWhiteSpace(SavedFilterName));
        LoadFilterCommand = new RelayCommand<SavedFilterEntry>(LoadFilter);
        DeleteFilterCommand = new RelayCommand<SavedFilterEntry>(DeleteFilter);

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

    // ── 扩展过滤维度 ────────────────────────────────────────

    private string? _selectedAddressText;
    public string? SelectedAddressText { get => _selectedAddressText; set { _selectedAddressText = value; OnPropertyChanged(); } }

    private string _selectedIoOption = "All";
    public string SelectedIoOption { get => _selectedIoOption; set { _selectedIoOption = value; OnPropertyChanged(); } }

    private string _selectedBoolValueOption = "All";
    public string SelectedBoolValueOption { get => _selectedBoolValueOption; set { _selectedBoolValueOption = value; OnPropertyChanged(); } }

    private string? _selectedMessageSearch;
    public string? SelectedMessageSearch { get => _selectedMessageSearch; set { _selectedMessageSearch = value; OnPropertyChanged(); } }

    public string[] IoOptions { get; } = { "All", "DI (Input)", "DO (Output)" };
    public string[] BoolValueOptions { get; } = { "All", "ON", "OFF" };

    // ── DateTime 文本输入属性 ───────────────────────────────

    private string _fromTimeText = "";
    public string FromTimeText
    {
        get => _fromTimeText;
        set
        {
            _fromTimeText = value;
            OnPropertyChanged();
            if (DateTime.TryParse(value, out var dt))
                FromTime = dt;
            else if (string.IsNullOrWhiteSpace(value))
                FromTime = null;
        }
    }

    private string _toTimeText = "";
    public string ToTimeText
    {
        get => _toTimeText;
        set
        {
            _toTimeText = value;
            OnPropertyChanged();
            if (string.IsNullOrWhiteSpace(value))
                ToTime = null;
            else if (DateTime.TryParse(value, out var dt))
                ToTime = dt;
        }
    }

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

    // ── 保存的筛选模板 ──────────────────────────────────────

    private string _savedFilterName = "";
    public string SavedFilterName { get => _savedFilterName; set { _savedFilterName = value; OnPropertyChanged(); } }

    private ObservableCollection<SavedFilterEntry> _savedFilters = new();
    public ObservableCollection<SavedFilterEntry> SavedFilters { get => _savedFilters; set { _savedFilters = value; OnPropertyChanged(); } }
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
    public ICommand ExportAllResultsCommand { get; }
    public ICommand TraceModuleCommand { get; }
    public ICommand TraceObjectCommand { get; }
    public ICommand SaveFilterCommand { get; }
    public ICommand LoadFilterCommand { get; }
    public ICommand DeleteFilterCommand { get; }

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

            var (objName, cmdName, st, addr, isOut, bVal, msgSearch) = BuildFilterValues();

            // 直接并发 await，SQLite 操作本身已经是 async，不需要再包 Task.Run
            var countTask = _eventLogStore.QueryCountAsync(fromUtc, toUtc, module, SelectedAxisNo, level, objName, cmdName, st, addr, isOut, bVal, msgSearch, ct);
            var resultsTask = _eventLogStore.QueryAsync(fromUtc, toUtc, module, SelectedAxisNo, level, objName, cmdName, st, addr, isOut, bVal, msgSearch, PageSize, 0, ct);

            await Task.WhenAll(countTask, resultsTask);
            ct.ThrowIfCancellationRequested();

            TotalRows = await countTask;
            var results = await resultsTask;
            Events = new ObservableCollection<RuntimeEventLogItemViewModel>(
                results.Select(e => new RuntimeEventLogItemViewModel(e)));

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

            var (objName, cmdName, st, addr, isOut, bVal, msgSearch) = BuildFilterValues();
            var offset = (CurrentPage - 1) * PageSize;

            var results = await _eventLogStore.QueryAsync(
                FromTime?.ToUniversalTime(),
                ToTime?.ToUniversalTime(),
                module,
                SelectedAxisNo,
                level,
                objName,
                cmdName,
                st,
                addr,
                isOut,
                bVal,
                msgSearch,
                PageSize,
                offset,
                ct);

            Events = new ObservableCollection<RuntimeEventLogItemViewModel>(
                results.Select(e => new RuntimeEventLogItemViewModel(e)));

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
        FromTimeText = FromTime?.ToString("yyyy-MM-dd HH:mm:ss") ?? "";
        ToTimeText = "";
        SelectedModule = null;
        SelectedAxisNo = null;
        SelectedLevel = null;
        SelectedObjectName = null;
        SelectedCommandName = null;
        SelectedStatus = null;
        SelectedAddressText = null;
        SelectedIoOption = "All";
        SelectedBoolValueOption = "All";
        SelectedMessageSearch = null;
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
        SelectedAddressText = null;
        SelectedIoOption = "All";
        SelectedBoolValueOption = "All";
        SelectedMessageSearch = null;
        FromTime = DateTime.Now.AddMinutes(-30);
        ToTime = null;
        FromTimeText = FromTime?.ToString("yyyy-MM-dd HH:mm:ss") ?? "";
        ToTimeText = "";
    }

    /// <summary>从 UI 字符串转换为查询参数元组</summary>
    private (string? objName, string? cmdName, string? status, int? address, bool? isOutput, bool? boolValue, string? messageSearch) BuildFilterValues()
    {
        var objName = string.IsNullOrWhiteSpace(SelectedObjectName) ? null : SelectedObjectName;
        var cmdName = string.IsNullOrWhiteSpace(SelectedCommandName) ? null : SelectedCommandName;
        var st = string.IsNullOrWhiteSpace(SelectedStatus) ? null : SelectedStatus;
        var addr = !string.IsNullOrWhiteSpace(SelectedAddressText) && int.TryParse(SelectedAddressText, out var a) ? a : (int?)null;
        var isOut = SelectedIoOption switch { "DI (Input)" => false, "DO (Output)" => true, _ => (bool?)null };
        var bVal = SelectedBoolValueOption switch { "ON" => true, "OFF" => false, _ => (bool?)null };
        var msgSearch = string.IsNullOrWhiteSpace(SelectedMessageSearch) ? null : SelectedMessageSearch;
        return (objName, cmdName, st, addr, isOut, bVal, msgSearch);
    }

    private void SetTimeFilter(int minutes)
    {
        FromTime = DateTime.Now.AddMinutes(-minutes);
        ToTime = null;
        FromTimeText = FromTime?.ToString("yyyy-MM-dd HH:mm:ss") ?? "";
        ToTimeText = "";
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

    /// <summary>
    /// 从 Dashboard 错误追踪：设置模块 + 最近 24h Error/Warning 筛选
    /// </summary>
    public void TraceModule(string? module)
    {
        if (string.IsNullOrWhiteSpace(module)) return;
        ClearFilters();
        SelectedModule = module;
        SelectedLevel = "Error";
        FromTime = DateTime.Now.AddHours(-24);
        ToTime = null;
        FromTimeText = FromTime?.ToString("yyyy-MM-dd HH:mm:ss") ?? "";
        ToTimeText = "";
        _ = SearchAsync();
    }

    /// <summary>
    /// 从 Dashboard 追踪某个对象的完整操作链
    /// </summary>
    public void TraceObject(string? objectName)
    {
        if (string.IsNullOrWhiteSpace(objectName)) return;
        ClearFilters();
        SelectedObjectName = objectName;
        FromTime = DateTime.Now.AddHours(-24);
        ToTime = null;
        FromTimeText = FromTime?.ToString("yyyy-MM-dd HH:mm:ss") ?? "";
        ToTimeText = "";
        _ = SearchAsync();
    }

    /// <summary>
    /// 导出当前筛选条件的全部结果（不受分页限制）
    /// </summary>
    public async Task ExportAllResultsAsync()
    {
        if (TotalRows == 0) { StatusText = "No results to export"; return; }
        if (TotalRows > 50_000) { StatusText = "⚠ Too many results (>50k), please narrow the filter first."; return; }

        IsLoading = true;
        StatusText = $"Exporting {TotalRows} results...";
        try
        {
            var module = string.IsNullOrEmpty(SelectedModule) || SelectedModule == "All" ? null : SelectedModule;
            var level = string.IsNullOrEmpty(SelectedLevel) || SelectedLevel == "All" ? null : SelectedLevel;
            var (objName, cmdName, st, addr, isOut, bVal, msgSearch) = BuildFilterValues();

            // 批量获取所有结果（一次查询最多 50000 条）
            var allResults = await _eventLogStore.QueryAsync(
                FromTime?.ToUniversalTime(), ToTime?.ToUniversalTime(),
                module, SelectedAxisNo, level, objName, cmdName, st, addr, isOut, bVal, msgSearch,
                maxRows: 50_000, offset: null);

            var sb = new StringBuilder();
            sb.AppendLine("Timestamp,Module,EventType,Level,AxisNo,ObjectName,Address,IO,CommandName,Status,Message,PayloadJson");
            foreach (var e in allResults)
                sb.AppendLine($"\"{e.TimestampUtc:yyyy-MM-dd HH:mm:ss}\",\"{e.Module}\",\"{e.EventType}\",\"{e.Level}\",\"{e.AxisNo}\",\"{e.ObjectName}\",\"{e.Address}\",\"{e.IsOutput}\",\"{e.CommandName}\",\"{e.Status}\",\"{EscapeCsv(e.Message)}\",\"{EscapeCsv(e.PayloadJson)}\"");

            await SaveFileAsync(sb.ToString(), "event_log_full.csv", "CSV files (*.csv)|*.csv");
            StatusText = $"Exported {allResults.Count} results";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Export all results failed");
            StatusText = $"Export error: {ex.Message}";
        }
        finally { IsLoading = false; }
    }

    /// <summary>
    /// 保存当前筛选条件为命名模板
    /// </summary>
    public void SaveCurrentFilter()
    {
        if (string.IsNullOrWhiteSpace(SavedFilterName)) return;
        if (SavedFilters.Any(f => f.Name == SavedFilterName))
        { StatusText = $"Filter '{SavedFilterName}' already exists"; return; }
        SavedFilters.Add(new SavedFilterEntry
        {
            Name = SavedFilterName,
            FromTime = FromTime,
            ToTime = ToTime,
            Module = SelectedModule,
            AxisNo = SelectedAxisNo,
            Level = SelectedLevel,
            ObjectName = SelectedObjectName,
            CommandName = SelectedCommandName,
            Status = SelectedStatus,
            MessageSearch = SelectedMessageSearch
        });
        StatusText = $"Filter '{SavedFilterName}' saved";
    }

    /// <summary>
    /// 加载已保存的筛选模板
    /// </summary>
    public void LoadFilter(SavedFilterEntry? filter)
    {
        if (filter == null) return;
        FromTime = filter.FromTime;
        ToTime = filter.ToTime;
        FromTimeText = filter.FromTime?.ToString("yyyy-MM-dd HH:mm:ss") ?? "";
        ToTimeText = filter.ToTime?.ToString("yyyy-MM-dd HH:mm:ss") ?? "";
        SelectedModule = filter.Module;
        SelectedAxisNo = filter.AxisNo;
        SelectedLevel = filter.Level;
        SelectedObjectName = filter.ObjectName;
        SelectedCommandName = filter.CommandName;
        SelectedStatus = filter.Status;
        SelectedMessageSearch = filter.MessageSearch;
        StatusText = $"Loaded filter: {filter.Name}";
        _ = SearchAsync();
    }

    /// <summary>
    /// 删除已保存的筛选模板
    /// </summary>
    public void DeleteFilter(SavedFilterEntry? filter)
    {
        if (filter == null) return;
        SavedFilters.Remove(filter);
        StatusText = $"Deleted filter: {filter.Name}";
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
