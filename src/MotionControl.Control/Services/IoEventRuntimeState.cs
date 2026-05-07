namespace MotionControl.Control.Services;

public sealed class IoEventRuntimeState
{
    private readonly object _syncLock = new();
    private readonly IEventLogStore _eventLogStore;
    private IoEventRecord[] _recentEvents = Array.Empty<IoEventRecord>();

    public event Action? EventsChanged;

    public IoEventRuntimeState() : this(null!) { }

    public IoEventRuntimeState(IEventLogStore eventLogStore)
    {
        _eventLogStore = eventLogStore;
    }

    public IReadOnlyList<IoEventRecord> RecentEvents
    {
        get
        {
            lock (_syncLock) { return _recentEvents; }
        }
    }

    public void Add(IoEventRecord record)
    {
        lock (_syncLock)
        {
            var newList = new IoEventRecord[Math.Min(_recentEvents.Length + 1, 300)];
            var startIndex = _recentEvents.Length >= 300 ? 1 : 0;
            if (_recentEvents.Length >= 300)
                Array.Copy(_recentEvents, 1, newList, 0, 299);
            else
                Array.Copy(_recentEvents, 0, newList, 0, _recentEvents.Length);
            newList[Math.Min(_recentEvents.Length, 299)] = record;
            _recentEvents = newList;
        }
        EventsChanged?.Invoke();

        _eventLogStore?.Enqueue(new RuntimeEventLogEntry
        {
            TimestampUtc = record.Timestamp,
            Module = "IO",
            EventType = record.IsOutput ? "DO" : "DI",
            Level = RuntimeEventLogEntry.DetermineLevel(null, record.IsOutput ? "DO" : "DI"),
            ObjectName = record.Name,
            Address = record.Address,
            IsOutput = record.IsOutput,
            BoolValue = record.Value,
            Message = record.Message,
        });
    }
}

public sealed class IoEventRecord
{
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
    public string Name { get; init; } = string.Empty;
    public int Address { get; init; }
    public bool IsOutput { get; init; }
    public bool Value { get; init; }
    public string Message { get; init; } = string.Empty;
}
