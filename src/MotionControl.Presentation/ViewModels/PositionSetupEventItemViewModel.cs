using MotionControl.Control.Services;

namespace MotionControl.Presentation.ViewModels;

public sealed class PositionSetupEventItemViewModel : IEquatable<PositionSetupEventItemViewModel>
{
    public PositionSetupEventItemViewModel(PositionSetupEventRecord record)
    {
        Time = record.Timestamp.ToLocalTime().ToString("HH:mm:ss");
        PositionName = record.PositionName;
        EventType = record.EventType;
        Message = record.Message;
    }

    public string Time { get; }
    public string PositionName { get; }
    public string EventType { get; }
    public string Message { get; }

    public bool Equals(PositionSetupEventItemViewModel? other)
    {
        if (other is null) return false;
        return Time == other.Time && PositionName == other.PositionName && EventType == other.EventType && Message == other.Message;
    }

    public override bool Equals(object? obj) => Equals(obj as PositionSetupEventItemViewModel);
    public override int GetHashCode() => HashCode.Combine(Time, PositionName, EventType, Message);
}
