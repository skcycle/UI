namespace MotionControl.Control.Services;

public sealed class CommandFeedbackRuntimeState
{
    private readonly object _syncLock = new();
    private readonly IEventLogStore _eventLogStore;
    private CommandFeedback[] _recentFeedback = Array.Empty<CommandFeedback>();

    public event Action? FeedbackChanged;

    public CommandFeedbackRuntimeState() : this(null!) { }

    public CommandFeedbackRuntimeState(IEventLogStore eventLogStore)
    {
        _eventLogStore = eventLogStore;
    }

    public IReadOnlyList<CommandFeedback> RecentFeedback
    {
        get
        {
            lock (_syncLock)
            {
                return _recentFeedback;
            }
        }
    }

    public void Add(CommandFeedback feedback)
    {
        lock (_syncLock)
        {
            var lastFeedback = _recentFeedback.Length > 0 ? _recentFeedback[^1] : null;
            var isDuplicateAxisStateChange = lastFeedback is not null
                && feedback.CommandName == "AxisState"
                && lastFeedback.CommandName == feedback.CommandName
                && lastFeedback.AxisNo == feedback.AxisNo
                && lastFeedback.Status == feedback.Status
                && lastFeedback.Message == feedback.Message;

            if (isDuplicateAxisStateChange)
            {
                return;
            }

            var newList = new CommandFeedback[Math.Min(_recentFeedback.Length + 1, 100)];
            var startIndex = _recentFeedback.Length >= 100 ? 1 : 0;
            if (_recentFeedback.Length >= 100)
            {
                Array.Copy(_recentFeedback, 1, newList, 0, 99);
            }
            else
            {
                Array.Copy(_recentFeedback, 0, newList, 0, _recentFeedback.Length);
            }
            newList[Math.Min(_recentFeedback.Length, 99)] = feedback;
            _recentFeedback = newList;
        }

        FeedbackChanged?.Invoke();

        // 异步入队到 SQLite（不阻塞控制循环）
        _eventLogStore?.Enqueue(new RuntimeEventLogEntry
        {
            TimestampUtc = feedback.Timestamp.UtcDateTime,
            Module = "AxisCommand",
            EventType = feedback.Status,
            Level = RuntimeEventLogEntry.DetermineLevel(feedback.Status, feedback.CommandName),
            AxisNo = feedback.AxisNo,
            CommandName = feedback.CommandName,
            Status = feedback.Status,
            Message = feedback.Message,
        });
    }

    public void AddStarted(string commandName, int? axisNo = null, string message = "")
        => Add(new CommandFeedback { CommandName = commandName, AxisNo = axisNo, Status = "Started", Message = message });

    public void AddSucceeded(string commandName, int? axisNo = null, string message = "")
        => Add(new CommandFeedback { CommandName = commandName, AxisNo = axisNo, Status = "Succeeded", Message = message });

    public void AddFailed(string commandName, int? axisNo = null, string message = "")
        => Add(new CommandFeedback { CommandName = commandName, AxisNo = axisNo, Status = "Failed", Message = message });
}
