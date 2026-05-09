using System.ComponentModel;
using System.Runtime.CompilerServices;
using MotionControl.Control.Services;
using MotionControl.Device.Abstractions.Models;
using MotionControl.Domain.Entities;

namespace MotionControl.Presentation.ViewModels;

public sealed class DashboardViewModel : INotifyPropertyChanged
{
    public sealed record RuntimeEventLogItem(string Time, string Axis, string Event, string Message);

    private readonly Machine _machine;
    private readonly CommandFeedbackRuntimeState _commandFeedbackRuntimeState;
    private readonly IEventLogStore _eventLogStore;
    private EtherCatControllerStatus? _controllerStatus;
    private const int DroppedAlarmThreshold = 100;
    private RuntimeEventLogItem[] _lastRecentCommandFeedback = Array.Empty<RuntimeEventLogItem>();
    private string[] _lastActiveAlarmSummary = Array.Empty<string>();
    private EtherCatSlaveViewModel[] _lastEtherCatSlaves = Array.Empty<EtherCatSlaveViewModel>();
    private IReadOnlyList<RuntimeEventLogItem> _recentPersistedErrors = Array.Empty<RuntimeEventLogItem>();

    public DashboardViewModel(Machine machine, CommandFeedbackRuntimeState commandFeedbackRuntimeState, IEventLogStore eventLogStore)
    {
        _machine = machine;
        _commandFeedbackRuntimeState = commandFeedbackRuntimeState;
        _eventLogStore = eventLogStore;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public string SystemState => _machine.CurrentState.ToString();
    public bool IsConnected => _machine.IsConnected;
    public string ConnectionStatusText => _machine.IsConnected ? "Online" : "Offline";
    public string ConnectionStatusColor => _machine.IsConnected ? "#1FD6B5" : "#EF4444";
    public int AxisCount => _machine.Axes.Count;
    public int AlarmCount => _machine.Alarms.Count(alarm => alarm.IsActive);
    public int ActiveInputCount => _machine.IoPoints.Count(io => !io.IsOutput && io.Value);
    public int ActiveOutputCount => _machine.IoPoints.Count(io => io.IsOutput && io.Value);
    public string EtherCatNetworkState => _controllerStatus?.NetworkState ?? "Unknown";
    public bool EtherCatConnected => _controllerStatus?.IsConnected ?? false;
    public int EtherCatOnlineSlaveCount => _controllerStatus?.OnlineSlaveCount ?? 0;
    public IReadOnlyList<EtherCatSlaveViewModel> EtherCatSlaves { get; private set; } = Array.Empty<EtherCatSlaveViewModel>();
    public IReadOnlyList<RuntimeEventLogItem> RecentCommandFeedback { get; private set; } = Array.Empty<RuntimeEventLogItem>();
    public IReadOnlyList<RuntimeEventLogItem> RecentAxisCommandFeedback { get; private set; } = Array.Empty<RuntimeEventLogItem>();
    public IReadOnlyList<string> AlarmLog { get; private set; } = Array.Empty<string>();
    public IReadOnlyList<string> ActiveAlarmSummary { get; private set; } = Array.Empty<string>();

    /// <summary>从 SQLite 拉取的最近 Error 事件（跨重启持久化）</summary>
    public IReadOnlyList<RuntimeEventLogItem> RecentPersistedErrors
    {
        get => _recentPersistedErrors;
        private set { _recentPersistedErrors = value; OnPropertyChanged(); }
    }

    /// <summary>事件日志健康状态：丢弃计数（超过阈值报警）</summary>
    public long EventLogDroppedCount => _eventLogStore.DroppedCount;

    /// <summary>事件日志健康状态：待写计数</summary>
    public int EventLogPendingCount => _eventLogStore.PendingCount;

    /// <summary>事件日志是否健康（无丢弃或丢弃在阈值内）</summary>
    public bool IsEventLogHealthy => _eventLogStore.DroppedCount < DroppedAlarmThreshold;

    /// <summary>事件日志健康状态文本，供 Dashboard UI 直接绑定</summary>
    public string EventLogHealthText => IsEventLogHealthy
        ? $"Dropped: {EventLogDroppedCount} | Pending: {EventLogPendingCount}"
        : $"⚠ SYS-EVENTLOG-DROPPED: {EventLogDroppedCount} events lost | Pending: {EventLogPendingCount}";

    public async Task RefreshPersistedErrorsAsync(CancellationToken ct = default)
    {
        try
        {
            var errors = await _eventLogStore.QueryAsync(level: "Error", maxRows: 15, ct: ct);
            RecentPersistedErrors = errors.Select(e => new RuntimeEventLogItem(
                e.TimestampUtc.ToLocalTime().ToString("MM-dd HH:mm:ss"),
                e.AxisNo?.ToString() ?? "-",
                $"{e.Module}/{e.EventType}",
                e.Message ?? "")).ToList();
        }
        catch (OperationCanceledException) { /* skip */ }
        catch (Exception) { /* silently skip transient query failures */ }
    }

    public void Refresh(EtherCatControllerStatus? controllerStatus = null)
    {
        _controllerStatus = controllerStatus ?? _controllerStatus;

        // Notify all Dashboard card bindings
        OnPropertyChanged(nameof(SystemState));
        OnPropertyChanged(nameof(IsConnected));
        OnPropertyChanged(nameof(ConnectionStatusText));
        OnPropertyChanged(nameof(ConnectionStatusColor));
        OnPropertyChanged(nameof(EtherCatConnected));
        OnPropertyChanged(nameof(EtherCatNetworkState));
        OnPropertyChanged(nameof(EtherCatOnlineSlaveCount));
        OnPropertyChanged(nameof(AlarmCount));
        OnPropertyChanged(nameof(ActiveInputCount));
        OnPropertyChanged(nameof(ActiveOutputCount));

        var latestSlaves = _controllerStatus?.Slaves.Select(slave => new EtherCatSlaveViewModel(slave)).ToArray()
            ?? Array.Empty<EtherCatSlaveViewModel>();
        if (!_lastEtherCatSlaves.SequenceEqual(latestSlaves))
        {
            EtherCatSlaves = latestSlaves;
            _lastEtherCatSlaves = latestSlaves;
        }

        var latestFeedback = _commandFeedbackRuntimeState.RecentFeedback
            .Reverse()
            .Take(100)
            .Select(item => new RuntimeEventLogItem(
                item.Timestamp.ToLocalTime().ToString("HH:mm:ss"),
                item.AxisNo?.ToString() ?? "-",
                $"{item.Status} / {item.CommandName}",
                item.Message))
            .ToArray();
        // Use length comparison to avoid SequenceEqual false negatives from new array references each run
        var newLen = latestFeedback.Length;
        var lastLen = _lastRecentCommandFeedback.Length;
        if (newLen != lastLen || (newLen > 0 && (lastLen == 0 || !latestFeedback.SequenceEqual(_lastRecentCommandFeedback))))
        {
            RecentCommandFeedback = latestFeedback;
            _lastRecentCommandFeedback = latestFeedback;
            OnPropertyChanged(nameof(RecentCommandFeedback));
        }

        var latestAxisFeedback = _commandFeedbackRuntimeState.RecentFeedback
            .Where(item => item.AxisNo.HasValue)
            .Reverse()
            .Take(100)
            .Select(item => new RuntimeEventLogItem(
                item.Timestamp.ToLocalTime().ToString("HH:mm:ss"),
                item.AxisNo?.ToString() ?? "-",
                $"{item.Status} / {item.CommandName}",
                item.Message))
            .ToArray();
        if (!RecentAxisCommandFeedback.SequenceEqual(latestAxisFeedback))
        {
            RecentAxisCommandFeedback = latestAxisFeedback;
            OnPropertyChanged(nameof(RecentAxisCommandFeedback));
        }

        var latestAlarmSummary = _machine.Alarms
            .Where(alarm => alarm.IsActive)
            .OrderByDescending(alarm => alarm.OccurredAt)
            .Take(5)
            .Select(alarm => $"[{alarm.Severity}] {alarm.Code} {alarm.Message}")
            .ToArray();
        if (!_lastActiveAlarmSummary.SequenceEqual(latestAlarmSummary))
        {
            ActiveAlarmSummary = latestAlarmSummary;
            _lastActiveAlarmSummary = latestAlarmSummary;
            OnPropertyChanged(nameof(ActiveAlarmSummary));
        }

        var latestAlarmLog = _machine.Alarms
            .OrderByDescending(alarm => alarm.OccurredAt)
            .Take(20)
            .Select(alarm => $"[{alarm.Severity}] {alarm.OccurredAt:MM-dd HH:mm:ss} {alarm.Code} {alarm.Message}")
            .ToArray();
        if (!AlarmLog.SequenceEqual(latestAlarmLog))
        {
            AlarmLog = latestAlarmLog;
            OnPropertyChanged(nameof(AlarmLog));
        }

        // 事件日志健康状态（丢弃监控）
        OnPropertyChanged(nameof(EventLogDroppedCount));
        OnPropertyChanged(nameof(EventLogPendingCount));
        OnPropertyChanged(nameof(IsEventLogHealthy));
        OnPropertyChanged(nameof(EventLogHealthText));
    }
}
