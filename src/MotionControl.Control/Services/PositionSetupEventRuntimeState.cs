namespace MotionControl.Control.Services;

public sealed class PositionSetupEventRuntimeState
{
    private readonly object _syncLock = new();
    private readonly IEventLogStore _eventLogStore;
    private PositionSetupEventRecord[] _recentEvents = Array.Empty<PositionSetupEventRecord>();

    public event Action? EventsChanged;

    public PositionSetupEventRuntimeState() : this(null!) { }

    public PositionSetupEventRuntimeState(IEventLogStore eventLogStore)
    {
        _eventLogStore = eventLogStore;
    }

    public IReadOnlyList<PositionSetupEventRecord> RecentEvents
    {
        get
        {
            lock (_syncLock) { return _recentEvents; }
        }
    }

    public void Add(PositionSetupEventRecord record)
    {
        lock (_syncLock)
        {
            var newList = new PositionSetupEventRecord[Math.Min(_recentEvents.Length + 1, 200)];
            var startIndex = _recentEvents.Length >= 200 ? 1 : 0;
            if (_recentEvents.Length >= 200)
                Array.Copy(_recentEvents, 1, newList, 0, 199);
            else
                Array.Copy(_recentEvents, 0, newList, 0, _recentEvents.Length);
            newList[Math.Min(_recentEvents.Length, 199)] = record;
            _recentEvents = newList;
        }
        EventsChanged?.Invoke();

        _eventLogStore?.Enqueue(new RuntimeEventLogEntry
        {
            TimestampUtc = record.Timestamp,
            Module = "PositionSetup",
            EventType = record.EventType,
            Level = RuntimeEventLogEntry.DetermineLevel(null, record.EventType),
            ObjectName = record.PositionName,
            Message = record.Message,
            PayloadJson = PayloadBuilder.PositionSetup(
                record.SetupItem,
                record.PositionName,
                record.Stage,
                record.FailedAxis,
                record.ErrorMessage)
        });
    }
}

public sealed class PositionSetupEventRecord
{
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
    public string PositionName { get; init; } = string.Empty;
    public string EventType { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;

    // Optional structured fields for PayloadJson
    public string SetupItem { get; init; } = string.Empty;
    public string Stage { get; init; } = string.Empty;
    public int? FailedAxis { get; init; }
    public string? ErrorMessage { get; init; }
}
