using MotionControl.Domain.Entities;

namespace MotionControl.Presentation.ViewModels;

public sealed class AlarmItemViewModel : IEquatable<AlarmItemViewModel>
{
    public AlarmItemViewModel(Alarm alarm)
    {
        Code = alarm.Code;
        Message = alarm.Message;
        Source = alarm.Source;
        Category = alarm.Category;
        Severity = alarm.Severity;
        OccurredAt = alarm.OccurredAt;
        IsActive = alarm.IsActive;
    }

    public string Code { get; }
    public string Message { get; }
    public string Source { get; }
    public string Category { get; }
    public string Severity { get; }
    public DateTime OccurredAt { get; }
    public bool IsActive { get; }

    public bool Equals(AlarmItemViewModel? other)
    {
        if (other is null)
        {
            return false;
        }

        return Code == other.Code
            && Message == other.Message
            && Source == other.Source
            && Category == other.Category
            && Severity == other.Severity
            && OccurredAt == other.OccurredAt
            && IsActive == other.IsActive;
    }

    public override bool Equals(object? obj) => Equals(obj as AlarmItemViewModel);

    public override int GetHashCode()
    {
        return HashCode.Combine(Code, Message, Source, Category, Severity, OccurredAt, IsActive);
    }
}
