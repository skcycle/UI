using MotionControl.Control.Services;

namespace MotionControl.Presentation.ViewModels;

public sealed class WorkHeadEventItemViewModel : IEquatable<WorkHeadEventItemViewModel>
{
    public WorkHeadEventItemViewModel(WorkHeadEventRecord record)
    {
        Time = record.Timestamp.ToLocalTime().ToString("HH:mm:ss");
        WorkHeadName = record.WorkHeadName;
        EventType = record.EventType;
        Message = record.Message;
    }

    public string Time { get; }
    public string WorkHeadName { get; }
    public string EventType { get; }
    public string Message { get; }

    public bool Equals(WorkHeadEventItemViewModel? other)
    {
        if (other is null) return false;
        return Time == other.Time && WorkHeadName == other.WorkHeadName && EventType == other.EventType && Message == other.Message;
    }

    public override bool Equals(object? obj) => Equals(obj as WorkHeadEventItemViewModel);
    public override int GetHashCode() => HashCode.Combine(Time, WorkHeadName, EventType, Message);
}
